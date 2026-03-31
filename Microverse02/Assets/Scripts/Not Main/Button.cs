using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Button : MonoBehaviour
{
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] CellManager.Team teamToDetect = CellManager.Team.Player;
    float radius = 10f;
    float pressDepositAverageValue = 0.1f;


    [SerializeField] Color idleColor = Color.red;
    [SerializeField] Color activeColor = Color.green;   

    public bool IsPressed { get; private set; } 


    // Start is called before the first frame update
    void Start()
    {
        if(bacteriaFieldManager ==null)
            bacteriaFieldManager = FindAnyObjectByType<BacteriaFieldManager>();

        this.transform.localScale = new Vector3(radius, radius, 1f);
    }

    // Update is called once per frame
    void Update()
    {
        if (bacteriaFieldManager == null) return;


    }
}
