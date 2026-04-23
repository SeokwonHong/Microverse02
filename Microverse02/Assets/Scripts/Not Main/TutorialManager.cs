using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] SardineManager sardineManager;

    [Header("UI")]
    [SerializeField] GameObject tutorialBar;
    [SerializeField] TextMeshProUGUI tutorialText;
    [SerializeField] GameObject animatedMouseUI;

    [Header("Scene / Flow")]
    [SerializeField] string gameplaySceneName = "GamePlay";
    float stepDelay = 2.5f;

    [Header("Optional References")]
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

            // Step 1: wait until player drags a bit
            case 0:
                if (Input.GetMouseButton(0) &&
                    (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.7f ||
                     Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.7f))
                {
                    CompleteStep();
                }
                break;

            case 1:
                if (Input.GetMouseButtonUp(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    CompleteStep();
                    //FinishTutorial();
                }
                break;
            case 2:
                if (sardineManager != null && sardineManager.IsAnyPlayerFollowingDraw())
                {
                    CompleteStep();
                }
                break;

            case 3:
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
                SetTutorialUI(true, "DRAW THE LINE!");
                SetMouseUI(true);
                break;

            case 1:
                SetTutorialUI(true, "GREAT!");
                SetMouseUI(false);
                Invoke(nameof(GoToNextStep), stepDelay);
                break;

            case 2:
                SetTutorialUI(true, "NOW DRAW ON THE BLUE AGENT!");
                SetMouseUI(false);

                if (sardineManager != null)
                {
                    sardineManager.SpawnOnePlayer();
                    sardineManager.SetPlayerSpawnStart(true);
                }
                break;

            case 3:
                SetTutorialUI(true, "WELL DONE!");
                SetMouseUI(false);
                Invoke(nameof(GoToNextStep), stepDelay);
                return;
            case 4:
                SetTutorialUI(true, "NOW MAKE AGENT REACH TO THE FOOD!");
                SetMouseUI(false);
                break;
            case 5:
                SetTutorialUI(true, "WONDERFUL!");
                SetMouseUI(false);
                Invoke(nameof(GoToNextStep), stepDelay);
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
            tutorialText.text = textToShow;
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
}