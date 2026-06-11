using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] SardineManager sardineManager;
    [SerializeField] PlanktonManager planktonManager;


    [Header("UI")]
    [SerializeField] GameObject tutorialBar;
    [SerializeField] TextMeshProUGUI tutorialText;
    [SerializeField] GameObject animatedMouseUI;

    [SerializeField] UnityEngine.UI.Image characterImage;
    [SerializeField] Sprite defaultVirusSprite;
    [SerializeField] Sprite happyVirusSprite;

    [Header("Scene / Flow")]
    [SerializeField] string gameplaySceneName = "GamePlay";
    float stepDelay = 0.5f;


    [SerializeField] ScreenFader screenFader;


    int currentStep = 0;
    bool tutorialFinished = false;
    bool waitingForInput = false;
    bool canProceed = false;

    void Start()
    {
        if (sardineManager != null)
            sardineManager.SetPlayerSpawnStart(false);

        StartStep(0);
        
    }

    void Update()
    {
        if (tutorialFinished) return;
        if (!canProceed) return;
        if (waitingForInput) return;

        switch (currentStep)
        {

            case 0:
                if (Input.GetMouseButton(0) &&
                    (Mathf.Abs(Input.GetAxis("Mouse X")) > 1f ||
                     Mathf.Abs(Input.GetAxis("Mouse Y")) > 1f))
                {
                    CompleteStep();
                }
                break;

     
            case 2:
                if (sardineManager != null && sardineManager.IsAnyPlayerFollowingDraw())
                {
                    CompleteStep();
                }
                break;

            case 4:
                if(sardineManager.HasPlayerEatFood())
                {
                    CompleteStep();
                }
                break;
        }
    }

    void StartStep(int stepIndex)
    {
        currentStep = stepIndex;
        canProceed = false;

        switch (currentStep)
        {
            case 0:
                SetTutorialUI(true, "DRAW A LINE!");
                SetMouseUI(true);
                SetCharacterMood(false);
                break;

            case 1:
                SetTutorialUI(true, "WELL DONE!");
                SetMouseUI(false);
                SetCharacterMood(true);
                Invoke(nameof(GoToNextStep), 2f);
                break;

            case 2:
                SetTutorialUI(true, "DRAW ON THE BLUE AGENT!");
                SetMouseUI(false);

                if (sardineManager != null)
                {
                    sardineManager.SpawnOnePlayer();
                    sardineManager.SetPlayerSpawnStart(true);
                    SetCharacterMood(false);
                }
                break;

            case 3:
                SetTutorialUI(true, "EXCELLENT!");
                SetMouseUI(false);
                SetCharacterMood(true);
                Invoke(nameof(GoToNextStep), 2);
                return;
            case 4:
                SetTutorialUI(true, "GUIDE IT TO THE <color=#99FFB2>FOOD</color>!");
                SetMouseUI(false);
                SetCharacterMood(false);
                if (planktonManager != null)
                {
                    planktonManager.SpawnTutorialCluster();
                }
                break;
            case 5:
                SetTutorialUI(true, "WONDERFUL!");
                SetMouseUI(false);
                SetCharacterMood(true);
                Invoke(nameof(GoToNextStep), 2);
                return;
            case 6:
                SetTutorialUI(true, "GET AS HIGH <color=#99FFB2>SCORE</color> AS YOU CAN!");
                SetCharacterMood(false);
                Invoke(nameof(GoToNextStep), 2.3f);
                return;
            case 7:
                SetTutorialUI(true, "AND AVOID THE <color=#FF544C>RED</color>!");
                SetCharacterMood(false);
                Invoke(nameof(GoToNextStep), 2.2f);
                return;
            case 8:
                SetTutorialUI(true, "GO!");
                SetCharacterMood(true);
                Invoke(nameof(FinishTutorial), 1f);
                return;
        }

        Invoke(nameof(EnableInput), 0.6f);
    }
    void EnableInput()
    {
        canProceed = true;
    }
    void CompleteStep()
    {
        if (waitingForInput) return;
        waitingForInput = true;
        canProceed = false;

        GoToNextStep();
    }

    void GoToNextStep()
    {
        waitingForInput = false;
        StartStep(currentStep + 1);
    }

    void FinishTutorial()
    {
        tutorialFinished = true;
        SetTutorialUI(false, "");
        SetMouseUI(false);

        if (screenFader != null)
            StartCoroutine(screenFader.FadeOut(gameplaySceneName));
        else
            SceneManager.LoadScene(gameplaySceneName);
    }

    void SetTutorialUI(bool state, string textToShow)
    {
        if (tutorialBar != null)
            tutorialBar.SetActive(state);

        if (tutorialText != null)
        {
            StopAllCoroutines();
            StartCoroutine(TypeText(textToShow));
        }
    }
    IEnumerator TypeText(string fullText)
    {
        tutorialText.text = "";

        bool insideTag = false;

        for (int i = 0; i < fullText.Length; i++)
        {
            char c = fullText[i];

            if (c == '<')
                insideTag = true;

            tutorialText.text += c;

            if (!insideTag)
            {
                if (c == ' ')
                    yield return new WaitForSeconds(0.05f);
                else
                    yield return new WaitForSeconds(0.03f);
            }

            if (c == '>')
                insideTag = false;
        }
    }
    void SetMouseUI(bool state)
    {
        if (animatedMouseUI != null)
            animatedMouseUI.SetActive(state);
    }

    public void SkipTutorial()
    {
        FinishTutorial();
    }

    void SetCharacterMood(bool happy)
    {
        if (characterImage == null) return;

        characterImage.sprite = happy ? happyVirusSprite : defaultVirusSprite;
    }
}