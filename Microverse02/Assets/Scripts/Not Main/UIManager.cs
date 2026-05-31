using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{



    //[SerializeField] LevelTimer levelTimer;
    //[SerializeField] TextMeshPro timeText;

    [SerializeField] OceanFieldManager fieldManager;
    [SerializeField] TextMeshProUGUI scoreText;
    private int score = 0;

    private float EnergyBarFullSize = 3.39f;

    Vector3 originalScale;


    void Awake()
    {
    
        score = 0;
    }
    void Update()
    {


        //if (timeText != null)
        //{
        //    float time = levelTimer.CurrentTime;

        //    int seconds = Mathf.FloorToInt(time);
        //    int centiseconds = Mathf.FloorToInt((time - seconds) * 100f);

        //    timeText.text = $"{seconds:00}:{centiseconds:00}";


        //}

        if(scoreText != null)
        {
            score = fieldManager.Score;
            scoreText.text = $"SCORE: {score}";
        }



    }
}