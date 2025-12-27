
using System.Collections.Generic;
using UnityEngine;


public class CellManager : MonoBehaviour
{
    public Transform PlayerPos;

    public float cellSpeed = 2f;

    //해쉬 
    SpatialHash spatialHash;
    [SerializeField] float BoxSize = 4f;


    private List<Cell> cells = new List<Cell>();
    List<Organisms> organisms = new List<Organisms>();

    enum CellRole { Core, Shell, WhiteBlood }



    class Cell
    {
        public Vector2 currentPos;
        public Vector2 currentVelocity;

        public Vector2 nextPos;
        public Vector2 nextVelocity;

        public float cellRadius; // 셀 하나하나의 경계
        public float detectRadius; // 이웃 인식 경계

        public int organismId; //-1 이면 독립 셀 (백혈구), 1 이면 생물1
        public CellRole role; // Core / Shell / WhiteBlood
    }

    class Organisms
    {
        public int id; // 이 셀이 무엇인지
        public int coreIndex; // 중심은 누구냐 (인덱스로 찾을거임)
        public List<int> members = new List<int>(); // 그룹의 집합 다 넣을거임
        public float coreDistance; // 쉘과 심장과의 거리

        public Vector2 anchorPos;
        public Vector2 heading; //normalized 방향
        public float headingPower; // 속도보다는 tendency 로 봐야함. 값을 낮게 유지시켜 더 생물같이 표현해야함.
        public bool anchorEnabled; //앵커가 쉘들을 붙잡거나 놓아버리거나: 나중에 죽으면 구조가 파괴되게
        public float hp;

    }

    //velocity 는 vector 2 의 좌표를 하나 찍고 그걸  0,0 로 직선연결한다 가정. 끝부분에 화살표를 단것이라 보면 됨: 방향+힘
    void Start()
    {
        spatialHash = new SpatialHash(BoxSize);

        CreateOrganism(Vector2.zero); //*****************************************************************************


    }

    // Update is called once per frame
    void Update()
    {
        spatialHash.Clear(); // 딕셔너리 내부 데이터 지우기

        for (int i = 0; i < cells.Count; i++)
        {
            spatialHash.Insert(cells[i].currentPos, i);
        }

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            c.nextVelocity = c.currentVelocity; //더블버퍼중 첫번째
            c.nextPos = c.currentPos + c.nextVelocity * cellSpeed * Time.deltaTime;
            cells[i] = c;

            foreach (int otherIndex in spatialHash.Query(c.currentPos)) // Query 에서 인덱스 int 를 하나하나 줄거임. 그걸 쓰면 바로 other Index 는 덮어씌워질거임.
            {
                if (otherIndex == i) continue;
                ResolveOverlap(i, otherIndex);
                ApplyCohesion(i, otherIndex);
              
            }
        }
        
        
        ApplyOrganismTendency();
        ApplyCoreAnchor();
        ApplyOrganismDeath();
        
        for(int iter = 0; iter<8; iter++)
        {
            ApplyCoreShellConstraints();
            //ApplyShellBarrierShape();
        }

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            c.currentVelocity = c.nextVelocity;//더블버퍼중 두번째
            c.currentPos = c.nextPos;
            cells[i] = c;
        }
        
    }

    void CreateOrganism(Vector2 currentPos)
    {
        Organisms org = new Organisms();
        int shellCount = 40;
        float coreDistance = 2f;

        org.id = organisms.Count;
        org.coreDistance = coreDistance;

        
        //core 

        Cell core = new Cell();
        core.currentPos = currentPos; // 이건 상관없음
        core.currentVelocity = Vector2.zero;

        //이부분부터 프로퍼티화해야할듯.
        core.cellRadius = 0.30f;
        core.detectRadius = core.cellRadius * 5f;

        core.organismId = org.id;
        core.role = CellRole.Core;

        int coreIndex = cells.Count;
        cells.Add(core);

        org.coreIndex = coreIndex;
        org.members.Add(coreIndex);

        //Shell

        for (int i = 0; i < shellCount; i++)
        {
            float angle = (Mathf.PI * 2f) * (i / (float)shellCount); //(Mathf.PI * 2f) 는 각도로 이해 * 그걸 비율로 슬라이스
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); //각도를 normalized 된 벡터값으로 바꿈 그걸 해주는게 x 축인 cos , y 축인 sin
            //+ 계산하기 편하게 사분면에 표현가능하게 단위화
            Vector2 pos = currentPos + dir * coreDistance;

            Cell shell = new Cell();
            shell.currentPos = pos;
            shell.currentVelocity = Vector2.zero;

            //이부분부터 프로퍼티화해야할듯.
            shell.cellRadius = 0.15f;
            shell.detectRadius = shell.cellRadius * 5f;

            shell.organismId = org.id;
            shell.role = CellRole.Shell;

            int shellIndex = cells.Count;
            cells.Add(shell);
            org.members.Add(shellIndex);

        }

        organisms.Add(org);
    }


    #region Cell_Constraint
    void ResolveOverlap(int CurrentIndex, int OtherIndex)
    {
        Cell currentCell = cells[CurrentIndex];
        Cell otherCell = cells[OtherIndex];


        Vector2 delta = otherCell.currentPos - currentCell.currentPos;
        float d2 = delta.sqrMagnitude;
        if (d2 <= 0f) return;

        float minDist = currentCell.cellRadius + otherCell.cellRadius;
        float minDist2 = minDist * minDist;
        if (d2 >= minDist2) return;

        float dist = Mathf.Sqrt(d2); //dist 는 거리값이니까
        Vector2 direction = delta / dist; //벡터값을 거리로 나눠서 방향만 나오게

        float overlap = minDist - dist;
        Vector2 push = direction * (overlap * 0.5f); // push 는 방향 * 상쇠값의 절반

        currentCell.nextPos -= push;
        otherCell.nextPos += push;

        cells[CurrentIndex] = currentCell;
        cells[OtherIndex] = otherCell;

    }
    void ApplyCoreAnchor()
    {
        foreach (var org in organisms)
        {
            if(!org.anchorEnabled) continue; 

            int coreIdx = org.coreIndex;
            Cell core = cells[coreIdx];

            core.nextPos = org.anchorPos;
            core.nextVelocity = Vector2.zero;
            cells[coreIdx] = core;
        }
    }
    void ApplyCoreShellConstraints() //해쉬 거치지 않음 - 셀 하나가 기존 해쉬 영역을 초과해도 organism 규칙 따르게 
    {
        foreach(var org in organisms)
        {
            int coreIdx = org.coreIndex;
            if(coreIdx < 0) continue;

            for(int i =0; i<org.members.Count; i++)
            {
                int idx = org.members[i];
                if(idx==coreIdx) continue;

                ApplyKeepDistance(coreIdx, idx);
            }
        } 
            
    }
    void ApplyOrganismDeath()
    {
        foreach(var org in organisms)
        {
            if(org.hp>0f) continue; 

            org.anchorEnabled = false;
            org.heading = Vector2.zero;
            org.headingPower = 0f;
        }
    }

    void ApplyOrganismTendency()
    {
        Vector2 playerPos = (Vector2)PlayerPos.position;

        foreach (var org in organisms)
        {
            //if(org.hp<=0f) continue;
            int coreIdx = org.coreIndex;
            if (coreIdx < 0) continue;

            Vector2 corePos = cells[coreIdx].currentPos;
            Vector2 toPlayer = playerPos-corePos;

            float d2 = toPlayer.sqrMagnitude;
            if (d2 < 1e-8f) continue;

            org.heading = toPlayer.normalized;
            org.headingPower = 0.5f;

            Cell core = cells[coreIdx];
            core.nextPos += org.heading * org.headingPower * Time.deltaTime;
            cells[coreIdx] = core;
        }
    }

    //void ApplyShellBarrierShape()
    //{
    //    foreach (var org in organisms)
    //    {
    //        if (org.hp <= 0f) continue;

    //        int coreIdx = org.coreIndex;
    //        if (coreIdx < 0) continue;

    //        int total = org.members.Count;
    //        if (total < 4) continue; //구조물은 core 포함 셀 4개 이상여야함

    //        int shellCount = total - 1;
    //        float targetDist = (2f * Mathf.PI * org.coreDistance) / shellCount;

    //        float tolerance = targetDist * 0.15f;
    //        float strength = 1.0f;

    //        for (int k = 1; k < total; k++)
    //        {
    //            int aIdx = org.members[k];
    //            int bIdx = org.members[(k == total - 1) ? 1 : (k + 1)]; //마지막 쉘의 다음 쉘은 1, 그게 아니면 현재 인덱스+1

    //            Cell a = cells[aIdx];
    //            Cell b = cells[bIdx];

    //            Vector2 d = b.nextPos - a.nextPos;
    //            float d2 = d.sqrMagnitude;
    //            if (d2 < 1e-8f) continue;

    //            float dist = Mathf.Sqrt(d2);
    //            float error = dist - targetDist;
    //            if (Mathf.Abs(error) < tolerance) continue; //정상범위면 쉘에 힘들 안더함

    //            Vector2 dir = d / dist;

    //            Vector2 corr = dir * (error * 0.5f * strength);

    //            a.nextPos += corr;
    //            b.nextPos -= corr;

    //            cells[aIdx] = a;
    //            cells[bIdx] = b;
    //        }
    //    }
    //}
    #endregion


    #region Cell_Rules

    void ApplyCohesion(int CurrentIndex, int OtherIndex)
    {
        Cell currentCell = cells[(CurrentIndex)];
        Cell otherCell = cells[(OtherIndex)];

        // 셀 종류에 따른 거름망. 쉘이 아니면 함수 실행을 안함.
        if (currentCell.organismId != otherCell.organismId) return; // 같은 생물 아니면 무시
        if (currentCell.role != CellRole.Shell) return; //쉘들만 모이게
        if (otherCell.role != CellRole.Shell) return; //비교대상이 쉘이 아니면 무시



        Vector2 delta = otherCell.currentPos - currentCell.currentPos;
        float d2 = delta.sqrMagnitude;
        if (d2 <= 0f) return;


        float detectR = currentCell.detectRadius;
        float detectR2 = detectR * detectR;
        if (d2 > detectR2) return;

        float minDist = currentCell.cellRadius + otherCell.cellRadius;
        float minDist2 = minDist * minDist;
        if (d2 <= minDist2) return;

        float dist = Mathf.Sqrt(d2);
        Vector2 dir = delta / dist; //벡터를 순수 거리로 나눔

        float speed = 0.01f;
        Vector2 move = dir * (speed * Time.deltaTime);

        currentCell.nextPos += move * 0.5f;
        otherCell.nextPos -= move * 0.5f;

        cells[CurrentIndex] = currentCell;
        cells[OtherIndex] = otherCell;
    }

    void ApplyKeepDistance(int CurrentIndex, int OtherIndex)
    {
        Cell currentCell = cells[CurrentIndex];
        Cell otherCell = cells[OtherIndex];

        bool currentIsCore = currentCell.role == CellRole.Core;
        bool otherIsCore = otherCell.role == CellRole.Core;
        if(currentIsCore==otherIsCore) return;
        
        
        Cell core = currentIsCore? currentCell : otherCell; //current 가 핵이면 핵, current 가 핵이 아니면 other 이 핵
        Cell shell = currentIsCore ? otherCell : currentCell; // current 가 핵이면 other 은 쉘, current 가 핵이 아니면 쉘은 current

        

        if(core.organismId != shell.organismId) return; //서로 다른 생명체면 무시

        float target = organisms[core.organismId].coreDistance; //적정거리
        float tolerance = 0.05f; // 적정거리에서 이정도면 봐줄게 +-

        Vector2 delta = shell.nextPos - core.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float dist = Mathf.Sqrt(d2);
        float error = dist - target;
        float absErr = Mathf.Abs(error);
        if (absErr < tolerance) return; // coreDistance 내부에서 에러가 기준선보다 더 커지면 밀어냄. 
        //tolerance 안쪽이면 형태유지. 

        float rampRange = target * 0.5f;
        float t = Mathf.Clamp01((absErr - tolerance) / rampRange);
        float strength = t;
        Vector2 dir = delta / dist;
        Vector2 move = dir * (error * strength);

        

        
        

        shell.nextPos -= move;

        if(currentIsCore)
        {
            otherCell = shell;
            cells[OtherIndex] = otherCell;
        }
        else
        {
            currentCell = shell;
            cells[CurrentIndex] = currentCell;
        }
    }


    #endregion


    void OnDrawGizmos()
    {
        if (cells == null)
        {
            Debug.Log("cell list 가 비어있음");
            return;
        }

        Gizmos.color = Color.green;
        foreach (Cell c in cells)
        {
            Gizmos.DrawSphere(c.currentPos, c.cellRadius);
        }
    }
}



