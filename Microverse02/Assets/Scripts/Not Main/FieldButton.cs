using UnityEngine;


public class FieldButton : MonoBehaviour
{
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] CellManager.Team teamToDetect = CellManager.Team.Player;
    float radius = 20f;
    float pressDepositAverageValue = 0.08f;

    [SerializeField] SpriteRenderer visual;
    [SerializeField] Color idleColor = Color.red;
    [SerializeField] Color activeColor = Color.green;   

    public bool IsPressed { get; private set; } 


    // Start is called before the first frame update
    void Awake()
    {
        if(bacteriaFieldManager ==null)
            bacteriaFieldManager = FindAnyObjectByType<BacteriaFieldManager>();

        this.transform.localScale = new Vector3(radius, radius, 1f);
    }

    // Update is called once per frame
    void Update()
    {
        if (bacteriaFieldManager == null) return;

        float value = bacteriaFieldManager.SampleButtonArea((Vector2)transform.position, radius,teamToDetect);
        IsPressed = value >= pressDepositAverageValue;

        if (visual != null)
            visual.color = IsPressed ? activeColor : idleColor;
    }
}
