using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] CellManager cellManager;
    [SerializeField] TMP_Text energyText;
    [SerializeField] TMP_Text systemStabilityText;

    private int organismCount; 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        organismCount = cellManager.CountOrganismNum();

        energyText.text = "Energy: " + cellManager.ReproductionEnergy.ToString("F0");
        systemStabilityText.text = "Organism Left: " + organismCount;
    }
}
