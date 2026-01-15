
using System;
using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class CellManager : MonoBehaviour
{
    [Header("Mouse and Player")]
    float mousePlayerDistance;
    private Vector2 mousePos;
    float playerSpeed;
    float normalSpeed=0;
    float maxSpeed=5f;
    float threshold=5f;


    private float playerRadius;
    private float playerInfluenceRadius;
    int playerCellIndex=-1; // -1이란 뜻은 플레이어가 할당되지 않았다는 뜻. 플레이어가 만들어지면 제대로된 정수 인덱스가 들어감.
    [SerializeField] private float playerPushStrength =1.0f;  


    public float cellSpeed = 2f;

    //해쉬 
    SpatialHash spatialHash;
    [SerializeField] float BoxSize = 3f;


    private List<Cell> cells = new List<Cell>();
    List<Organisms> organisms = new List<Organisms>();

    enum CellRole { Player, Core, Shell, WhiteBlood}

    public bool isOrganismDead = false;
    const float maxDeadTime = 20f;
    class Cell
    {
        public Vector2 currentPos;
        public Vector2 currentVelocity;

        public Vector2 nextPos;
        public Vector2 nextVelocity;

        public float cellRadius; // 셀 하나하나의 경계
        public float detectRadius; // 이웃 인식 경계

        public int organismId; //-1 이면 독립 셀, 1 이면 생물1
        public CellRole role; // Core / Shell / WhiteBlood
    }

    class Organisms
    {
        public int id; // 이 셀이 무엇인지
        public int coreIndex; // 중심은 누구냐 (인덱스로 찾을거임)
        public List<int> members = new List<int>(); // // 그룹의 집합 다 넣을거임
        public float coreDistance; // 쉘과 심장과의 거리

        public Vector2 anchorPos;
        public Vector2 heading; //normalized 방향
        public float headingPower; // 속도보다는 tendency 로 봐야함. 값을 낮게 유지시켜 더 생물같이 표현해야함.
        public bool anchorEnabled; //앵커가 쉘들을 붙잡거나 놓아버리거나: 나중에 죽으면 구조가 파괴되게
        public float hp;
        public bool isDead;
        public float deadTimer;
    }

    //velocity 는 vector 2 의 좌표를 하나 찍고 그걸  0,0 로 직선연결한다 가정. 끝부분에 화살표를 단것이라 보면 됨: 방향+힘
    void Start()
    {
        spatialHash = new SpatialHash(BoxSize);

        

        CreatePlayerCell(Vector2.zero);
        playerRadius = GetPlayerRadius();
        playerInfluenceRadius = playerRadius*7f;

        CreateOrganism(Vector2.zero); //*****************************************************************************
        //CreateOrganism(new Vector2(  8f,   6f ));
        //CreateOrganism(new Vector2( -9f,   7f ));
        //CreateOrganism(new Vector2( 12f,  -4f ));
        //CreateOrganism(new Vector2( -6f, -10f ));
        //CreateOrganism(new Vector2( 15f,   3f ));
        //CreateOrganism(new Vector2( -14f,  2f ));
        //CreateOrganism(new Vector2(  4f,  14f ));
        //CreateOrganism(new Vector2( -3f, -15f ));
        //CreateOrganism(new Vector2( 18f, -12f ));
        //CreateOrganism(new Vector2( -17f, 11f ));
        //CreateOrganism(new Vector2( 10f,  18f ));
        //CreateOrganism(new Vector2( -19f, -5f ));


    }
/// <summary>
/// ////////////////////////////////////////////////////////////////////////////////////////////////
/// </summary>
    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))isOrganismDead=!isOrganismDead;
        // 1) 해시 적용
        

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            c.nextVelocity = c.currentVelocity; //더블버퍼중 첫번째
            c.nextPos = c.currentPos + c.nextVelocity * cellSpeed * Time.deltaTime;
            cells[i] = c;
        }

        spatialHash.Clear(); // 딕셔너리 내부 데이터 지우기
        for (int i = 0; i < cells.Count; i++)
        {
            spatialHash.Insert(cells[i].nextPos, i);
        }

        for (int i = 0; i < cells.Count; i++)
        {
            foreach (int otherIndex in spatialHash.Query(cells[i].nextPos)) // QQuery 에서 인덱스 int 를 하나하나 줄거임. 그걸 쓰면 바로 other Index 는 덮어씌워질거임.
            {
                if (otherIndex <= i) continue;
                ResolveOverlap(i, otherIndex);
                ApplyCohesion(i, otherIndex);
              
            }
        }
        ApplyPlayerInput();
        //ApplyPlayerFunctions();
       
        
        ApplyOrganismTendency();
        ApplyCoreAnchor();
        
        
        for(int iter = 0; iter<8; iter++)
        {
            ApplyCoreShellConstraints();
            ApplyOrganismJelly();
            for(int i = 0;i < cells.Count;i++) ResolvePlayerOverlap(i);
           
        }

        ApplyCellMovement();
        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            c.currentVelocity = c.nextVelocity;//더블버퍼중 두번째
            c.currentPos = c.nextPos;
            cells[i] = c;
        }
        ApplyOrganismDeath();
        UpdateDeadOrganisms();
    }
/// <summary>
/// ////////////////////////////////////////////////////////////////////////////////////////////////
/// </summary>
/// 
    #region Input, player function

    void ApplyInput(Cell player)
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePlayerDistance = Vector2.Distance(GetPlayerPosition(),mousePos);

        playerSpeed = Mathf.Lerp(normalSpeed,maxSpeed,Mathf.InverseLerp(0f,threshold,mousePlayerDistance));

        player.nextPos = Vector2.MoveTowards(GetPlayerNextPosition(), mousePos,playerSpeed*Time.deltaTime);
    }
    void ApplyPlayerInput()
    {
        if(playerCellIndex<0)return;

        Cell player = cells[playerCellIndex];
        ApplyInput(player);
        cells[playerCellIndex]= player;
    }
    public Vector2 GetPlayerPosition()
    {
        if(playerCellIndex<0)
        {
            return Vector2.zero;
        }
        return cells[playerCellIndex].currentPos;
    }
    Vector2 GetPlayerNextPosition()
    {
        if(playerCellIndex<0)
        {
            return Vector2.zero;
        }
        return cells[playerCellIndex].nextPos;
    }

    float GetPlayerRadius()
    {
        if(playerCellIndex<0)
        {
            return 0.4f;
        }
        return cells[playerCellIndex].cellRadius;
    }

    #endregion
    #region Create

    void CreatePlayerCell(Vector2 pos)
    {
        Cell player = new Cell();
        player.currentPos = pos;
        player.currentVelocity = Vector2.zero;

        player.cellRadius = 0.4f;
        player.detectRadius = player.cellRadius*6f;

        player.organismId =-1;
        player.role=CellRole.Player;

        playerCellIndex = cells.Count;
        cells.Add(player);
    }
    void CreateOrganism(Vector2 currentPos)
    {
        Organisms org = new Organisms();
        int shellCount = 33;
        float coreDistance = 1.5f;

        org.id = organisms.Count;
        org.coreDistance = coreDistance;

        
        //core 

        Cell core = new Cell();
        core.currentPos = currentPos; 
        core.currentVelocity = Vector2.zero;

        core.cellRadius = 0.2f;
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
        //org.anchorEnabled=true;
        
        organisms.Add(org);

    }
#endregion

    #region Cell_Constraint
    void ResolveOverlap(int CurrentIndex, int OtherIndex) //기본 충돌
    {
        Cell currentCell = cells[CurrentIndex];
        Cell otherCell = cells[OtherIndex];


        Vector2 delta = otherCell.nextPos - currentCell.nextPos;
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

    void ResolvePlayerOverlap(int cellIndex) //플레이어 - 세포 기본 충돌
    {
        var c = cells[cellIndex];
        if(c.role==CellRole.Player) return;
        if (c.role == CellRole.Core) return;

        Vector2 playerPos = GetPlayerNextPosition();

        Vector2 delta = c.nextPos - playerPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;
        
        float dist = Mathf.Sqrt(d2);
        float minDist = c.cellRadius + GetPlayerRadius();

        if(dist >=minDist) return;

        Vector2 dir = delta/ dist;
        float penetration = (minDist - dist);

        c.nextPos += dir * penetration * playerPushStrength;

        cells[cellIndex] = c;   
    }
    void ApplyCoreAnchor() //셀의 앵커 등록
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
            if(org.isDead)continue;

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
    void ApplyOrganismDeath() //생물 죽었을때 함수
    {
        for (int i=0; i<organisms.Count; i++)  
        {
            var org = organisms[i];
            //if(org.hp>0f) continue; 


            if(!isOrganismDead)continue;

            if(org.isDead) continue;

            org.isDead = true;
            org.anchorEnabled = false;
            org.heading = Vector2.zero;
            org.headingPower = 0f;
            org.deadTimer=0;
            organisms[i] = org;
        }
    }
    void UpdateDeadOrganisms()
    {
        for(int i =0; i<organisms.Count; i++)
        {
            var org = organisms[i];
            if(!org.isDead) continue;

            org.deadTimer+=Time.deltaTime;
            if(org.deadTimer>=maxDeadTime)
            {
                org.deadTimer=maxDeadTime;
            }
            organisms[i]=org;
        }
    }
    void ApplyOrganismTendency() //생물 움직임
    {
        Vector2 playerPos = GetPlayerNextPosition();

        
        for (int i=0; i<organisms.Count; i++)
        {
            var org = organisms[i];
            if (org.isDead) continue;
            //if(org.hp<=0f) continue;
            int coreIdx = org.coreIndex;
            if (coreIdx < 0) continue;

            Vector2 corePos = cells[coreIdx].nextPos;
            Vector2 toPlayer = playerPos-corePos;

            float d2 = toPlayer.sqrMagnitude;
            if (d2 < 1e-8f) continue;

            org.heading = toPlayer.normalized;
            org.headingPower = 0.5f;

            Cell core = cells[coreIdx];
            core.nextPos += org.heading * org.headingPower * Time.deltaTime;
            cells[coreIdx] = core;
            organisms[i]=org;
        }
    }

    void ApplyKeepDistance(int CurrentIndex, int OtherIndex) //핵과 쉘 거리유지 
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
        float tolerance = 0.01f; // 적정거리에서 이정도면 봐줄게 +-

        Vector2 delta = shell.nextPos - core.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float dist = Mathf.Sqrt(d2);
        float error = dist - target;
        float absErr = Mathf.Abs(error);
        if (absErr < tolerance) return; //  coreDistance 내부에서 에러가 기준선보다 더 커지면 밀어냄. 
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

    void ApplyOrganismJelly()
    {
        if(playerCellIndex<0) return; //플레이어가 만들어지지 않았으면 return. CreatePlayerCell() 에서 정상적으로 만들어졌으면 양수가 와야함.

        Cell player = cells[playerCellIndex];
        float dt = Time.deltaTime;

        float k = 30f; //스프링 강도(커질수록 딱딱 / 반발 큼)
        float c = 1f; // 댐핑(커질수록 덜 튐, 끈적)
        float skin = 1f; // 이 이상 깊게 들어가면 힘을 더 세게(클램프용)

        for(int o=0; o<organisms.Count; o++)
        {
            var org = organisms[o];
            if(org.isDead) continue;

            Cell core = cells[org.coreIndex];

            float barrier = org.coreDistance+player.cellRadius;
            Vector2 delta= player.nextPos - core.nextPos;
            float d2 = delta.sqrMagnitude;
            if(d2<1e-8f)continue;

            float dist = Mathf.Sqrt(d2);
            float penetration = barrier- dist;


            Vector2 n = delta/dist;
            

            float x = Mathf.Min(penetration,skin); //둘중 더 작은값을 반환함. 
            float v_n = Vector2.Dot(player.nextVelocity-core.nextVelocity,n); //플레이어 방향값과 플레이어와 핵과의 방향이 얼마나 일치하는지?

            float accel = (k*x)-(c*v_n);
            Debug.Log(accel);
        
            player.nextVelocity += n*accel*dt;
            //player.nextPos += n* x*0.15f;
        }
        cells[playerCellIndex]=player;
    }


    #endregion

    #region Cell_Rules

    void ApplyCohesion(int CurrentIndex, int OtherIndex)  //세포 집결 함수
    {
        Cell currentCell = cells[(CurrentIndex)];
        Cell otherCell = cells[(OtherIndex)];

        // 셀 종류에 따른 거름망. 쉘이 아니면 함수 실행을 안함.
        if (currentCell.organismId != otherCell.organismId) return; // 같은 생물 아니면 무시
        if (currentCell.role != CellRole.Shell) return; //쉘들만 모이게
        if (otherCell.role != CellRole.Shell) return; //비교대상이 쉘이 아니면 무시



        Vector2 delta = otherCell.nextPos - currentCell.nextPos;
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

    
    void ApplyCellMovement()
    {
        
        for (int i=0; i<cells.Count; i++)
        {
            Cell c = cells[i];
            if(c.role==CellRole.Core) continue;
            if(c.organismId<0||c.organismId>=organisms.Count)continue;
            Organisms org = organisms[c.organismId];
            if (!org.isDead) continue;

            float t = Mathf.Clamp01(org.deadTimer/maxDeadTime);
        
            Vector2 ramdomDir = UnityEngine.Random.insideUnitCircle;
            if(ramdomDir.sqrMagnitude<1e-6f)continue;

            float speed = Mathf.Lerp(0.4f,0.0f,t);
            float drag = 9f;
            c.nextVelocity *= Mathf.Exp(-drag*Time.deltaTime);
            c.nextVelocity += ramdomDir*speed;
        
            cells[i] = c;
        }
    }
    void ApplyPlayerFunctions()
    {
        ApplyPlayerPush();
    }

    void ApplyPlayerPush()
    {
        Vector2 playerPos = GetPlayerNextPosition();
        

        for(int i =0; i<cells.Count;i++)
        {
            Cell c = cells[i];
            if(c.role !=CellRole.Shell) continue;

            Vector2 delta = c.nextPos - playerPos;
            float d2 = delta.sqrMagnitude;
            if(d2 >playerInfluenceRadius*playerInfluenceRadius) continue;

            float dist=Mathf.Sqrt(d2);
            if(dist<1e-5f) continue;


            Vector2 dir = delta/dist;
            float force = (playerInfluenceRadius - dist)/playerInfluenceRadius;

            c.nextPos += dir*force*playerPushStrength*Time.deltaTime;
            cells[i]=c;

        }
    }
    #endregion


    void OnDrawGizmos()
    {
        if(!Application.isPlaying) return; 
        if (cells == null||cells.Count==0)
        {
            Debug.Log("cell list 없음");
            return;
        }

        
        foreach (Cell c in cells)
        {
            switch (c.role)
        {
            case CellRole.Player:
                Gizmos.color = Color.white;
                break;
            case CellRole.Core:
                Gizmos.color = Color.green; 
                break;
            default: // Shell
                Gizmos.color = Color.green;
                break;
        }
            Gizmos.DrawSphere(c.currentPos, c.cellRadius);
        }
    }
}



