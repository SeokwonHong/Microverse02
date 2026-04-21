using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
public class GameOverUI : MonoBehaviour
{
    [SerializeField] SardineManager sardineManager;
    [SerializeField] GameObject gameEndPanel;
    [SerializeField] string mainMenuSceneName = "Main";

    //score
    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] OceanFieldManager oceanFieldManager;

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

        if (gameEndPanel != null)
            gameEndPanel.SetActive(true);

        // set score text
        if (scoreText != null && oceanFieldManager != null)
        {
            scoreText.text = "SCORE: " + oceanFieldManager.Score.ToString();
        }

        Time.timeScale = 0f;
    }

}