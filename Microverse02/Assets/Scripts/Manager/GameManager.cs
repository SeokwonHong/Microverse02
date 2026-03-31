using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] private CellManager cellManager;
    public bool win;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-find scene objects after load
        cellManager = FindAnyObjectByType<CellManager>();
        win = false;
    }

    public void OnLevelCompleted()
    {
        win = false;
        SceneController.Instance.LoadNextLevel();
    }

    public void OnLevelRestart()
    {
        win = false;
        SceneController.Instance.LoadCurrentLevel();
    }

    public void OnGameRestart()
    {
        win = false;
        SceneController.Instance.LoadLevel(0);
    }

    void Update()
    {
        if (win) return;
        if (cellManager == null) return;

   
    }
}