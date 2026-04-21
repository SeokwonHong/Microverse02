using UnityEngine;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] SardineManager sardineManager;
    [SerializeField] GameObject gameEndPanel;
    [SerializeField] OceanFieldManager oceanFieldManager;
    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] TextMeshProUGUI bestScoresText;
    [SerializeField] HighScoreManager highScoreManager;

    bool isGameOver;

    void Start()
    {
        if (gameEndPanel != null)
            gameEndPanel.SetActive(false);

        isGameOver = false;
    }

    void Update()
    {
        if (isGameOver) return;
        if (sardineManager == null) return;

        if (!sardineManager.HasAlivePlayer())
        {
            TriggerGameOver();
        }
    }

    void TriggerGameOver()
    {
        isGameOver = true;

        int finalScore = 0;

        if (oceanFieldManager != null)
            finalScore = oceanFieldManager.Score;

        if (scoreText != null)
            scoreText.text = "SCORE: " + finalScore;

        if (highScoreManager != null)
        {
            highScoreManager.SaveScore(finalScore);
            ShowBestScores();
        }

        if (gameEndPanel != null)
            gameEndPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    void ShowBestScores()
    {
        if (bestScoresText == null || highScoreManager == null)
            return;

        var scores = highScoreManager.LoadScores();

        bestScoresText.text = "BEST SCORES\n";

        for (int i = 0; i < scores.Count; i++)
        {
            bestScoresText.text += (i + 1) + ". " + scores[i] + "\n";
        }
    }
}