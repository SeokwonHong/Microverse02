using UnityEngine;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance;
    public int currentLevelIndex;

    [SerializeField] CellManager cellManager;
    bool win;

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


    private void Start()
    {
        
    }

    private void Update()
    {
        if (win) return;
        if (cellManager == null) return;

        if (cellManager.IsLevelWin())
        {
            win = true;
            Debug.Log("WIN!");
        }
    }
}
