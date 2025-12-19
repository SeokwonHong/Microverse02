using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraPosition : MonoBehaviour
{
    public Transform refToPlayer;
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
        if (refToPlayer == null) return;
        Vector3 pos = refToPlayer.position;
        pos.z = camZ;
        this.transform.position = pos;
    }
}
