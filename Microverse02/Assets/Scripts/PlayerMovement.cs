using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    [SerializeField] float playerSpeed = 2f;

    private Vector2 mousePos;

   
    float mousePlayerDistance;
    private void Awake()
    {
        
    }

    /*플레이어가 마우스가 가르키는 방향으로 이동하게 해야함:

    vector 를 써서 방향을 계산할수 있음. normalized 를 써서 강도를 무시하자. 

    */
    private void Start()
    {
        mousePlayerDistance=Vector2.Distance(mousePos, this.transform.position);
    }
    void Update()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        
        this.transform.position = Vector3.MoveTowards(this.transform.position,mousePos,playerSpeed*Time.deltaTime);


        if( mousePlayerDistance >= 0 && mousePlayerDistance <= 7)
        {
            


        }

    }

  
}
