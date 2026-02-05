using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraPosition : MonoBehaviour
{
    [SerializeField] CellManager cellManager;
    float camZ = -10;
    private void Awake()
    {
        
    }
    // Start is called before the first frame update
    void Start()
    {
        
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if(cellManager==null) return;

        Vector2 p2 = cellManager.GetPlayerPosition();
        if (float.IsNaN(p2.x) || float.IsNaN(p2.y) || float.IsInfinity(p2.x) || float.IsInfinity(p2.y))
            return;


        Vector3 pos = new Vector3(p2.x, p2.y,camZ);
        this.transform.position = pos;
    }
}
