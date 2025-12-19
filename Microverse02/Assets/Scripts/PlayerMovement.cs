using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    public float playerFollowSpeed = 5f;
    


    private void Awake()
    {
        
    }
    void Update()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        transform.position=Vector3.MoveTowards(this.transform.position,mousePos,Time.deltaTime*playerFollowSpeed);
    }
}
