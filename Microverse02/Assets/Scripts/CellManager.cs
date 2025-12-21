
using System.Collections.Generic;
using UnityEngine;

public class CellManager : MonoBehaviour
{
    SpatialHash spatialHash;

    public float cellSpeed = 2f;
    [SerializeField] float BoxSize = 4f;

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

        for (int i = 0; i < 10; i++)
        {
            Cell c = new Cell();
            c.currentPos = Random.insideUnitCircle * 5f;
            c.currentVelocity = Random.insideUnitCircle.normalized;
            c.cellRadius = 2f;
            c.detectRadius = 4f;
            cells.Add(c);
        }

        
    }

    // Update is called once per frame
    void Update()
    {
        foreach (Cell c in cells)
        {
            c.nextVelocity = c.currentVelocity;
            c.nextPos = c.currentPos+c.nextVelocity*cellSpeed*Time.deltaTime;


        }

        foreach (Cell c in cells)
        {
            c.currentVelocity = c.nextVelocity;
            c.currentPos = c.nextPos;
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
            Gizmos.DrawSphere(c.currentPos, 0.1f);
        }
    }
}

    

