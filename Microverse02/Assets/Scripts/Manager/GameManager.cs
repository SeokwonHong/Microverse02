using UnityEngine;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance;

    public int currentLevelIndex;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void OnLevelCompleted()
    {
        SceneController.Instance.LoadNextLevel();
    }

    public void OnLevelRestart()
    {
        SceneController.Instance.LoadLevel(0);
    }

}
