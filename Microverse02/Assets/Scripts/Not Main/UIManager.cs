using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] PlayerEnergy playerEnergy;
    [SerializeField] RectTransform refToEnergyBar;

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
            3.39f *t,
            originalScale.y,
            originalScale.z
        );
    }
}