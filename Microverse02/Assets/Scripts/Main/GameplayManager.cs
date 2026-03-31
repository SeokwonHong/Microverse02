using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] Button[] buttons;
    [SerializeField] Gate gate;

    bool buttonAllPressed = false;

    bool allActive = false;

    private void Start()
    {
        allActive = true;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
