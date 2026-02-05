using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerShadow : MonoBehaviour
{
    [SerializeField] CellManager cellManager;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (cellManager == null) return;

        Vector2 p2 = cellManager.GetPlayerPosition();
        if (float.IsNaN(p2.x) || float.IsNaN(p2.y) || float.IsInfinity(p2.x) || float.IsInfinity(p2.y))
            return;

        transform.position = new Vector3(p2.x, p2.y, transform.position.z);
    }
}
