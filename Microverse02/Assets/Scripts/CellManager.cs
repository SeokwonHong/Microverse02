
using System.Collections.Generic;
using UnityEngine;

public class CellManager : MonoBehaviour
{
    private List<Cell> cells = new List<Cell>();
    // Start is called before the first frame update
    public float cellSpeed = 2f;
    class Cell
    {
        public Vector2 currentPos;
        public Vector2 currentVelocity;

        public Vector2 nextPos;
        public Vector2 nextVelocity;
    }
    void Start()
    {
        for (int i = 0; i < 10; i++)
        {
            Cell c = new Cell();
            c.currentPos = Random.insideUnitCircle * 5f;
            c.currentVelocity = Random.insideUnitCircle.normalized;

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

        foreach (var c in cells)
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

    

