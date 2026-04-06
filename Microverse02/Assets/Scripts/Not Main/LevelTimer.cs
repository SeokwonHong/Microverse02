using UnityEngine;
using System;

public class LevelTimer : MonoBehaviour
{
    [SerializeField] float timeLimit = 30f;
    [SerializeField] float currentTime;
    bool isRunning = true;

    public float NormalizedTime => currentTime / timeLimit;

    public Action OnTimerEnded;

    public float CurrentTime => currentTime;
    public float TimeLimit => timeLimit;
    void Start()
    {
        currentTime = timeLimit;
    }

    void Update()
    {
        if (!isRunning) return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            isRunning = false;

            OnTimeUp();
        }
    }

    void OnTimeUp()
    {

        OnTimerEnded?.Invoke();
        // later:
        // restart level
        // show UI
    }

    public void StopTimer()
    {
        isRunning = false;
    }
}