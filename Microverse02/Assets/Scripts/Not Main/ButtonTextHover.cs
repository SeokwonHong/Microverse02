using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class ButtonTextHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] TextMeshProUGUI text;

    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color hoverColor = Color.grey;

    public bool isCredit = false;
    float hoverScale = 1.15f;

    Vector3 originalScale;

    void Awake()
    {
        if (text != null)
            originalScale = text.rectTransform.localScale;
    }

    void OnEnable()
    {
        if (text == null) return;

        text.color = normalColor;
        text.rectTransform.localScale = originalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (text == null) return;
        text.color = hoverColor;
        if (isCredit) return;
        text.rectTransform.localScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (text == null) return;
        text.color = normalColor;


        text.rectTransform.localScale = originalScale;
    }
}