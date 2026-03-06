using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OpenCloseButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{

    //UI Button
    [SerializeField] GameObject refToDownIcon;
    [SerializeField] GameObject refToUpIcon;
    private float closedButtonHeight = -152.6f;
    private float openedButtonHeight = 152.6199f;
    [SerializeField] RectTransform refToButton;
    float targetButtonHeight;
    float buttonAnimSpeed = 15f;


    //Big panel
    [SerializeField] RectTransform refToPanel;
    private float closedPanelHeight = 211.6616f;
    private float openedPanelHeight = 1953.595f;
    float panelAnimSpeed = 15f;
    private float targetPanelHeight;


    //preview bg panel
    [SerializeField] RectTransform refToPreviewBgPanel;
    private float openedPreviewBgPanelHeight = 863.0674f;
    private float closedPreviewBgPanelHeight = 0f;
    float targetPreviewBgpanelHeight;
    float previewbgAnimSpeed = 35f;

    //Upgrade text panel
    [SerializeField] GameObject refToUpgrageText;

    private bool isOpended = true;
    //Colours
    private Color normalColor = new Color32(84, 84, 84, 255);
    private Color hoverColor = new Color32(130,130,130,255);
    //Transition
    private float colorChangeSpeed = 8f;

    private Image button;
    private Color targetColor;

    void Awake()
    {
        button = GetComponent<Image>();
        targetColor = normalColor;
        button.color = normalColor;

        isOpended = true;

        targetPanelHeight = openedPanelHeight;
        targetButtonHeight = openedButtonHeight;
        targetPreviewBgpanelHeight = openedPreviewBgPanelHeight;

        UpdateIconsPanel();

        SetPanelHeightImmediate(targetPanelHeight);



    }

    // Update is called once per frame
    void Update()
    {
        button.color = Color.Lerp(button.color, targetColor,colorChangeSpeed *Time.deltaTime);

        UpdateIconsPanel();
        AnimatePanelHeight();
        AnimateButtonColour();
        AnimatePreviewBgHeight();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetColor = hoverColor;
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        targetColor = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        isOpended = !isOpended;

        targetPanelHeight = isOpended ? openedPanelHeight:closedPanelHeight;
        targetButtonHeight = isOpended ? openedButtonHeight : closedButtonHeight;
        targetPreviewBgpanelHeight = isOpended ? openedPreviewBgPanelHeight : closedPreviewBgPanelHeight;
    }

    void UpdateIconsPanel()
    {
 
        refToDownIcon.SetActive(isOpended);
        refToUpIcon.SetActive(!isOpended);
        refToUpgrageText.SetActive(isOpended);
    }

    void AnimatePanelHeight()
    {
        Vector2 size = refToPanel.sizeDelta;
        size.y = Mathf.Lerp(size.y, targetPanelHeight,panelAnimSpeed*Time.deltaTime);
        refToPanel.sizeDelta = size;
    }

    void AnimatePreviewBgHeight()
    {
        Vector2 size = refToPreviewBgPanel.sizeDelta;
        size.y = Mathf.Lerp(size.y, targetPreviewBgpanelHeight, previewbgAnimSpeed * Time.deltaTime);
        refToPreviewBgPanel.sizeDelta= size;
    }

    void AnimateButtonColour()
    {

        Vector2 pos = refToButton.anchoredPosition;
        pos.y = Mathf.Lerp(pos.y, targetButtonHeight, buttonAnimSpeed * Time.deltaTime);
        refToButton.anchoredPosition = pos;
    }



    void SetPanelHeightImmediate(float height)
    {
        if(refToPanel == null) return;
        Vector2 size = refToPanel.sizeDelta;
        size.y  = height;
        refToPanel.sizeDelta = size;
    }



}
