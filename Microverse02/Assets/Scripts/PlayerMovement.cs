using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    float normalSpeed = 0f;
    float farSpeed = 5f;
    float threshold = 5f;
    float playerSpeed;


    [Header("Mouse and Player")]
    float mousePlayerDistance;
    private Vector2 mousePos;
   

   
    private void Awake()
    {
        
    }

    /*플레이어가 마우스가 가르키는 방향으로 이동하게 해야함:

    vector 를 써서 방향을 계산할수 있음. normalized 를 써서 강도를 무시하자. 

    */
    private void Start()
    {
        
    }
    void Update()
    {
        mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePlayerDistance = Vector2.Distance(this.transform.position, mousePos);


        playerSpeed = Mathf.Lerp(normalSpeed, farSpeed, Mathf.InverseLerp(0f, threshold, mousePlayerDistance));


        this.transform.position = Vector2.MoveTowards(this.transform.position, mousePos, playerSpeed * Time.deltaTime);
    }

  // ui 로 특정 속도 이상이면 바이러스가 그쪽방향으로 쏠리는듯한 효과도 고려
}
