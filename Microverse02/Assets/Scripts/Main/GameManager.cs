using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] private SardineManager sardineManager;
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
        sardineManager = FindAnyObjectByType<SardineManager>();
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
        if (sardineManager == null) return;

        if(Input.GetKeyDown(KeyCode.R))
        {
            OnLevelRestart();
        }
   
    }
}