using UnityEngine;
using UnityEngine.UI;

public class TitleImageEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Image titleImage;
    [SerializeField] RectTransform titleRect;

    [Header("Pulse")]
    [SerializeField] float pulseSpeed = 2.5f;
    [SerializeField] float scaleAmount = 0.035f;

    [Header("Glow Colour")]
    [SerializeField] Color normalColour = Color.white;
    [SerializeField] Color glowColour = new Color(1f, 0.45f, 1f, 1f);
    [SerializeField] float glowAmount = 0.35f;

    [Header("Floating")]
    [SerializeField] float floatSpeed = 1.4f;
    [SerializeField] float floatAmount = 6f;

    Vector3 startScale;
    Vector2 startPos;

    void Awake()
    {
        if (titleImage == null)
            titleImage = GetComponent<Image>();

        if (titleRect == null)
            titleRect = GetComponent<RectTransform>();

        startScale = titleRect.localScale;
        startPos = titleRect.anchoredPosition;
    }

    void Update()
    {
        float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

        // scale pulse
        float scale = 1f + pulse * scaleAmount;
        titleRect.localScale = startScale * scale;

        // colour glow pulse
        titleImage.color = Color.Lerp(normalColour, glowColour, pulse * glowAmount);

        // subtle floating
        float yOffset = Mathf.Sin(Time.unscaledTime * floatSpeed) * floatAmount;
        titleRect.anchoredPosition = startPos + new Vector2(0f, yOffset);
    }
}
