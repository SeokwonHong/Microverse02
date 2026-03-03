using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    private bool loadedAfterWin = false;

    void OnEnable()
    {
        SceneManager.sceneLoaded += ResetWinGate;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= ResetWinGate;
    }

    private void ResetWinGate(Scene s, LoadSceneMode m)
    {
        loadedAfterWin = false;
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.win && !loadedAfterWin)
        {
            loadedAfterWin = true;
            gm.OnLevelCompleted();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space)) gm.OnLevelCompleted();
        if (Input.GetKeyDown(KeyCode.R)) gm.OnLevelRestart();
        if (Input.GetKeyDown(KeyCode.G)) gm.OnGameRestart();
    }
}