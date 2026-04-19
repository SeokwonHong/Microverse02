using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{

    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject creditPanel;
    public void PressPlay()
    {

    }

    public void StartGame()
    {
        SceneManager.LoadScene("GamePlay");
    }

    public void OpenCredits()
    {


        mainMenuPanel.SetActive(false);
        creditPanel.SetActive(true);
    }

    public void CloseCredits()
    {
        creditPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit pressed");
    }
}