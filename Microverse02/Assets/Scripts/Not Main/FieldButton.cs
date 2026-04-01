using UnityEngine;

public class FieldButton : MonoBehaviour
{
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] GameObject refToNotPressed;
    [SerializeField] GameObject refToPressed;


    [SerializeField] CellManager.Team teamToDetect = CellManager.Team.Player;
    [SerializeField] float radius = 13f;
    [SerializeField] float pressDepositAverageValue = 0.08f;

    public bool IsPressed { get; private set; }

    void Awake()
    {
        if (bacteriaFieldManager == null)
            bacteriaFieldManager = FindAnyObjectByType<BacteriaFieldManager>();
    }

    void Update()
    {
        if (bacteriaFieldManager == null) return;

        float value = bacteriaFieldManager.SampleButtonArea((Vector2)transform.position + Vector2.up * 6f, radius,teamToDetect);

        bool newState = value >= pressDepositAverageValue;

        if (newState != IsPressed)
        {
            IsPressed = newState;

            refToNotPressed.SetActive(!IsPressed);
            refToPressed.SetActive(IsPressed);
        }
    }
}