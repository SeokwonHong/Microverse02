using System.Xml.Serialization;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance;

    private void Awake()
    {
        if(Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadNextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;

        if(next<SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(next);
        }
        else
        {
            Debug.Log("Game Finished");
        }
    }

    public void LoadLevel(int index)
    {
        SceneManager.LoadScene(index);
    }
}
