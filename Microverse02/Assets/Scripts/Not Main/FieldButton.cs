using UnityEngine;

public class FieldButton : MonoBehaviour
{
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] GameObject refToNotPressed;
    [SerializeField] GameObject refToPressed;

    [Header("Line")]
    [SerializeField] Transform gateTarget;
    [SerializeField] LineRenderer line;
    [SerializeField] Color offColor = Color.red;
    [SerializeField] Color onColor = Color.green;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip pressedSound;

    [SerializeField] CellManager.Team teamToDetect = CellManager.Team.Player;
    float radius = 7.5f;
    [SerializeField] float pressDepositAverageValue = 0.08f;

    public bool IsPressed { get; private set; }

    void Awake()
    {
        if (bacteriaFieldManager == null)
            bacteriaFieldManager = FindAnyObjectByType<BacteriaFieldManager>();

        if(line !=null &&gateTarget != null)
        {
            line.positionCount = 2;
            line.SetPosition(0,this.transform.position);
            line.SetPosition(1,gateTarget.position);

            line.startWidth = 3f;
            line.endWidth = 3f;
        }
    }

    private void Start()
    {
        UpdateLineColor();
    }

    void Update()
    {
        if (bacteriaFieldManager == null) return;

        float value = bacteriaFieldManager.SampleButtonArea((Vector2)transform.position + Vector2.up * 0.9f, radius,teamToDetect);

        bool newState = value >= pressDepositAverageValue;

        if (newState != IsPressed)
        {
            IsPressed = newState;

            refToNotPressed.SetActive(!IsPressed);
            refToPressed.SetActive(IsPressed);

            if (audioSource != null && pressedSound != null)
                audioSource.PlayOneShot(pressedSound);

            UpdateLineColor();
        }
    }

    void UpdateLineColor()
    {
        if (line == null) return;

        Color c= IsPressed?onColor: offColor;
        line.startColor = c;
        line.endColor = c;
        
    }
}