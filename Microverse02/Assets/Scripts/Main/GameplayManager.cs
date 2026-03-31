using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] FieldButton[] buttons;
    [SerializeField] Gate gate;


    bool lastState=false;

    void Awake()
    {
        buttons = FindObjectsByType<FieldButton>(FindObjectsSortMode.None);
    }

    void Update()
    {
        if (buttons == null || buttons.Length == 0) return;

        bool allPressed=true;

        for(int i = 0; i<buttons.Length; i++)
        {
            if (buttons[i]==null || !buttons[i].IsPressed)
            {
                allPressed = false;
                break;
            }
        }

        if (allPressed != lastState)
        {
            lastState = allPressed;
            gate.SetOpen(allPressed);
  
        }
    }
}
