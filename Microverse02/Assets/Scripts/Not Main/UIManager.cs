using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField] PlayerEnergy playerEnergy;
    [SerializeField] RectTransform refToEnergyBar;

    [SerializeField] LevelTimer levelTimer;
    [SerializeField] TextMeshPro timeText;

    private float EnergyBarFullSize = 3.39f;

    Vector3 originalScale;


    void Awake()
    {
        originalScale = refToEnergyBar.localScale;
    }
    void Update()
    {
        if (playerEnergy == null || refToEnergyBar == null)
            return;

        float t = Mathf.Clamp01(playerEnergy.NormalizedEnergy);
        refToEnergyBar.localScale = new Vector3(
            EnergyBarFullSize * t,
            originalScale.y,
            originalScale.z
        );

        if (timeText != null)
        {
            float time = levelTimer.CurrentTime;

            int seconds = Mathf.FloorToInt(time);
            int centiseconds = Mathf.FloorToInt((time - seconds) * 100f);

            timeText.text = $"{seconds:00}:{centiseconds:00}";
        }

    }
}