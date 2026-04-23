using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{

    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject tutorialPanel;
    [SerializeField] GameObject creditPanel;
    [SerializeField] ScreenFader screenFader;
    [SerializeField] UIButtonSound buttonSound;

    bool creditIsOpen = false;

    private void Update()
    {
        if (creditIsOpen)
        {
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                buttonSound.PlayClick();
                CloseCredits();
            }
        }
    }
    public void StartGame()
    {
        if (screenFader != null)
            StartCoroutine(screenFader.FadeOut("GamePlay"));
    }

    public void OpenCredits()
    {
        creditIsOpen = true;
        mainMenuPanel.SetActive(false);
        creditPanel.SetActive(true);
    }

    public void StartTutorial()
    {
        if (screenFader != null)
            StartCoroutine(screenFader.FadeOut("Tutorial"));
    }
    public void OpenTutorialPanel()
    {
        tutorialPanel.SetActive(true);
    }

    public void CloseCredits()
    {
        creditIsOpen = false;
        creditPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit pressed");
    }
}