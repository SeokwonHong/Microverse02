using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    private Vector2 lastMousePos;

    public enum StepType
    {
        ShowMessage,
        HoverMouse
    }

    [Serializable]
    public class Step
    {
        public string message;
        public StepType type;
    }

    [SerializeField] private TMP_Text tutorialText;

    private List<Step> steps = new List<Step>();
    private int stepIndex = -1;

    private void Start()
    {
        // Define tutorial steps in code
        steps.Add(new Step
        {
            type = StepType.HoverMouse,
            message = "Hover the mouse to control!"
        });

        steps.Add(new Step
        {
            type = StepType.ShowMessage,
            message = "Well done"
        });

        Advance();
    }

    private void Update()
    {
        if (stepIndex < 0 || stepIndex >= steps.Count) return;

        Step s = steps[stepIndex];

        if (s.type == StepType.HoverMouse)
        {
            Vector2 currentMouse = Input.mousePosition;
            float moved = (currentMouse - lastMousePos).sqrMagnitude;

            lastMousePos = currentMouse;

            if (moved > 9f)
            {
                Advance();
            }
        }
    }

    private void Advance()
    {
        stepIndex++;

        if (stepIndex >= steps.Count)
        {
            tutorialText.text = "";
            return;
        }

        Step s = steps[stepIndex];

        tutorialText.text = s.message;

        if (s.type == StepType.HoverMouse)
        {
            lastMousePos = Input.mousePosition;
        }
    }
}
