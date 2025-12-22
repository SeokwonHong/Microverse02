
using System.Collections.Generic;
using UnityEngine;

public class CellManager : MonoBehaviour
{
    SpatialHash spatialHash;

    public float cellSpeed = 2f;
    [SerializeField] float BoxSize = 4f;
    [SerializeField] int cellAmount = 10;

    private List<Cell> cells = new List<Cell>();

    class Cell
    {
        public Vector2 currentPos;
        public Vector2 currentVelocity;

        public Vector2 nextPos;
        public Vector2 nextVelocity;

        public float cellRadius; // 셀 하나하나의 경계
        public float detectRadius; // 이웃 인식 경계

       
    }

    //velocity 는 vector 2 의 좌표를 하나 찍고 그걸  0,0 로 직선연결한다 가정. 끝부분에 화살표를 단것이라 보면 됨: 방향+힘
    void Start()
    {
        spatialHash = new SpatialHash(BoxSize);

        for (int i = 0; i < cellAmount; i++)
        {
            Cell c = new Cell();
            c.currentPos = Random.insideUnitCircle * 5f;
            c.currentVelocity = Random.insideUnitCircle.normalized;
            c.cellRadius = 0.2f;
            c.detectRadius = 4f;
            cells.Add(c);
        }

        
    }

    // Update is called once per frame
    void Update()
    {
        spatialHash.Clear(); // 딕셔너리 내부 데이터 지우기

        for (int i = 0;  i < cellAmount;i++)
        {
            Cell c = cells[i];

            spatialHash.Insert(c.currentPos, i);
        }

        for (int i = 0;  i < cellAmount;i++) 
        {
            Cell c = cells[i];


            c.nextVelocity = c.currentVelocity;
            c.nextPos = c.currentPos + c.nextVelocity * cellSpeed * Time.deltaTime;

            foreach (int otherIndex in spatialHash.Query(c.currentPos)) // Query 에서 인덱스 int 를 하나하나 줄거임. 그걸 쓰면 바로 other Index 는 덮어씌워질거임.
            {
                if(otherIndex == i) continue;
                CellGathering(i,otherIndex);
            }

        }

        for (int i = 0; i < cellAmount; i++)
        {
            Cell c = cells[i];

            c.currentVelocity = c.nextVelocity;
            c.currentPos = c.nextPos;
        }
      
    }

    void CellGathering(int CurrentIndex,int OtherIndex)
    {
        Cell currentCell = cells[CurrentIndex];
        Cell otherCell = cells[OtherIndex];

        Vector2 distance = otherCell.currentPos - currentCell.currentPos; // 크기+ 방향
        Vector2 direction = distance.normalized; //방향
        

        float minDist = currentCell.cellRadius + otherCell.cellRadius; // minimum distance ***반지름의 합***
        float minDist2 = minDist * minDist;
        float d2 = distance.sqrMagnitude;

        

        if (d2 < minDist2 && d2>0f) // 곂침
        {
            
        }
        else //둘 사이에 공간이 있음.
        {
            
        } 
            
       

    }


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

    

