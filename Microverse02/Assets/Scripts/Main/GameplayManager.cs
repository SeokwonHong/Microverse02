using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] FieldButton[] buttons;
    [SerializeField] Gate gate;
    [SerializeField] LevelTimer timer;

    public bool ButtonsLocked { get; private set; }
    public bool InputLocked { get; private set; }

    bool lastState=false;
    bool completed = false;

    public bool IsCompleted => completed;


    void Awake()
    {
        if (buttons == null || buttons.Length == 0)
            buttons = FindObjectsByType<FieldButton>(FindObjectsSortMode.None);
    }
    private void Start()
    {
        if (timer != null)
            timer.OnTimerEnded += OnLose;
    }

    void OnDestroy()
    {
        if (timer != null)
            timer.OnTimerEnded -= OnLose;
    }

    void Update()
    {
        if (completed) return;
        if (buttons == null || buttons.Length == 0) return;



        bool allPressed = true;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || !buttons[i].IsPressed)
            {
                allPressed = false;
                break;
            }
        }

        // state change (for gate)
        if (allPressed != lastState)
        {
            lastState = allPressed;
            gate.SetOpen(allPressed);
        }

        // win condition (only once)
        if (allPressed)
        {
            ButtonsLocked = true;
            InputLocked = true;
            completed = true;
            OnWin();
        }
    }

    void OnWin()
    {
        Debug.Log("WIN");

        if (timer != null)
            timer.StopTimer();

        // later:
        // play sound
        // load next level
    }

    void OnLose()
    {
        if (completed) return;

        Debug.Log("LOSE");

        InputLocked = true;
        ButtonsLocked = false;
        lastState = false;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].ForceUnpress();
        }

        if (gate != null)
            gate.SetOpen(false);
    }
}
