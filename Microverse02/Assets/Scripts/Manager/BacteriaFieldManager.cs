using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BacteriaFieldManager : MonoBehaviour
{
    
    [Header("Chemo Grid")]
    float[,] chemoField;
    float[,] chemoNext;

    int chemoWidth = 128; 
    int chemoHeight = 128; 
    float chemoDepositAmount = 1f; 
    float chemoDecayPerSecond = 1f; 
    float chemoDiffuseRate = 0.15f; 
    float chemoSensorDistance = 0.8f; 
    float chemoSensorAngle = 35f; 
    float chemoSteerStrength = 20f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
