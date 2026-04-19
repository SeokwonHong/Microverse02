using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Xml.Serialization;

public class ButtonTextHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] TextMeshProUGUI text;

    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color hoverColor = Color.grey;

    [SerializeField] Color quitNormalrColor = Color.white;
    [SerializeField] Color quitHoverColor = Color.red;
    public bool isQuit = false;

    void OnEnable()
    {
        if (text == null) return;

        text.color = isQuit ? quitNormalrColor : normalColor;
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if(isQuit)
        {
            text.color = quitHoverColor;
        }
        else text.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        text.color = isQuit ? quitNormalrColor : normalColor;
    }
}

