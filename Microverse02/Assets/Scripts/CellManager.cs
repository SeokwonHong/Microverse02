
using System.Collections.Generic;
using UnityEngine;


public class CellManager : MonoBehaviour
{
    SpatialHash spatialHash;

    public float cellSpeed = 2f;

    //해쉬 
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

        public int organismId; //-1 이면 독립 셀 (백혈구)
        public CellRole role; // Core / Shell / WhiteBlood
    }

    class Organisms
    {
        public int id; // 이 셀이 무엇인지
        public int coreIndex; // 중심은 누구냐 (인덱스로 찾을거임)
        public List<int> members= new List<int>(); // 그룹의 집합 다 넣을거임
        public float targetRadius; // 쉘과 심장과의 거리
        public float hp;

    }

    //velocity 는 vector 2 의 좌표를 하나 찍고 그걸  0,0 로 직선연결한다 가정. 끝부분에 화살표를 단것이라 보면 됨: 방향+힘
    void Start()
    {
        spatialHash = new SpatialHash(BoxSize);

        for (int i = 0; i < 55; i++)
        {
            Cell c = new Cell();
            c.currentPos = Random.insideUnitCircle * 5f;
            //c.currentVelocity = Random.insideUnitCircle.normalized;
            c.cellRadius = 0.1f;
            c.detectRadius = c.cellRadius*5;
            cells.Add(c);
        }

        
    }

    // Update is called once per frame
    void Update()
    {
        spatialHash.Clear(); // 딕셔너리 내부 데이터 지우기

        for (int i = 0;  i < cells.Count; i++)
        {
            Cell c = cells[i];

            spatialHash.Insert(c.currentPos, i);
        }

        for (int i = 0;  i < cells.Count; i++) 
        {
            Cell c = cells[i];
            c.nextVelocity = c.currentVelocity; //더블버퍼중 첫번째
            c.nextPos = c.currentPos + c.nextVelocity * cellSpeed * Time.deltaTime;
            cells[i] = c;

            foreach (int otherIndex in spatialHash.Query(c.currentPos)) // Query 에서 인덱스 int 를 하나하나 줄거임. 그걸 쓰면 바로 other Index 는 덮어씌워질거임.
            {
                if(otherIndex == i) continue;
                ResolveOverlap(i,otherIndex);
                ApplyCohesion(i, otherIndex);
            }

            
        }

        for (int i = 0; i < cells.Count; i++) 
        {
            Cell c = cells[i];

            c.currentVelocity = c.nextVelocity;//더블버퍼중 두번째
            c.currentPos = c.nextPos;
            cells[i] = c;

            
        }
        
    }

    void ResolveOverlap(int CurrentIndex,int OtherIndex)
    {
        Cell currentCell = cells[CurrentIndex];
        Cell otherCell = cells[OtherIndex];


        Vector2 delta = otherCell.currentPos - currentCell.currentPos;
        float d2 = delta.sqrMagnitude;
        if (d2 <= 0f) return;

        float minDist = currentCell.cellRadius +otherCell.cellRadius;
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
   

#region Cell_Rules

    void ApplyCohesion(int CurrentIndex, int OtherIndex)
    {
        Cell currentCell = cells[(CurrentIndex)];
        Cell otherCell = cells[(OtherIndex)];

        // 셀 종류에 따른 거름망. 쉘이 아니면 함수 실행을 안함.
        if (currentCell.organismId != otherCell.organismId) return;
        if (currentCell.role != CellRole.Shell) return;
        if (otherCell.role != CellRole.Shell) return;



        Vector2 delta = otherCell.currentPos - currentCell.currentPos;
        float d2 = delta.sqrMagnitude;
        if(d2 <= 0f) return;


        float detectR = currentCell.detectRadius;
        float detectR2 = detectR * detectR;
        if(d2 > detectR2) return;

        float minDist = currentCell.cellRadius + otherCell.cellRadius;
        float minDist2 = minDist * minDist;
        if(d2 <= minDist2) return;

        float dist = Mathf.Sqrt(d2);
        Vector2 dir = delta / dist; //벡터를 순수 거리로 나눔

        float speed = 0.01f;
        Vector2 move = dir * (speed*Time.deltaTime);

        currentCell.nextPos += move * 0.5f;
        otherCell.nextPos -=move * 0.5f;

        cells[CurrentIndex] = currentCell;
        cells[OtherIndex] = otherCell;
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

    

