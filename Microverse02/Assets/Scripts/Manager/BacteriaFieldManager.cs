using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class BacteriaFieldManager : MonoBehaviour
{
    Vector2 mapCentre = Vector2.zero;
    float mapRadius = 25f;
    
    [Header("Chemo Grid")]
    float[,] exploreField;
    float[,] exploreNext;

    float[,] foodField;
    float[,] foodNext;

    int chemoWidth = 128; 
    int chemoHeight = 128;
    [SerializeField] float chemoDepositAmount = 0.05f;  // how strong bacteria chemo is 
    [SerializeField] float chemoOrganismDepositAmount = 0.5f; // for organism

    float chemoDecayPerSecond = 0.001f; 
    float chemoDiffuseRate = 0.6f;  //How much chemical spreads to neighbours. Like blurring.
    
    [SerializeField] float chemoSensorDistance = 1.2f; 
    float chemoSensorAngle = 35f; 
    float chemoSteerStrength = 5f;



    //getter
    public float SensorDistance => chemoSensorDistance;
    public float SensorAngle => chemoSensorAngle;
    public float SteerStrength => chemoSteerStrength;   



    private void Awake()
    {

        exploreField = new float[chemoWidth, chemoHeight];
        exploreNext = new float[chemoWidth, chemoHeight];

    }

    bool WorldToGrid(Vector2 worldPos, out int gx, out int gy) //check if bacteria's inside of array map + return with cordinate of a bacteria
    {
        float mapSize = mapRadius * 2f;

        float px = (worldPos.x - (mapCentre.x - mapRadius)) / mapSize;
        float py = (worldPos.y - (mapCentre.y - mapRadius)) / mapSize;

        gx = Mathf.FloorToInt(px * chemoWidth);
        gy = Mathf.FloorToInt(py*chemoHeight);

        bool inside = gx >= 0 && gx < chemoWidth && gy >= 0 && gy < chemoHeight;
        if (!inside)
        {
            gx = -1;
            gy = -1;
        }

        return inside;
    }
    public void Deposit(Vector2 worldPos, float amountMultiplier = 1f) //put chemecals insdie of the space
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            exploreField[gx, gy] += chemoDepositAmount * amountMultiplier * Time.deltaTime;
        }
    }
    public void DepositOrganism(Vector2 worldPos)
    {
        Deposit(worldPos, chemoOrganismDepositAmount);
    }


    public float Sample(Vector2 worldPos) // take chemicals value out
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
            return exploreField[gx, gy];

        return 0f;
    }
    public void TickField(float dt) // apply decay its positoin and its neibors
    {
        float decay = chemoDecayPerSecond * dt;

        for (int x = 0; x < chemoWidth; x++)
        {
            for (int y = 0; y < chemoHeight; y++)
            {
                float center = exploreField[x, y];
                float sum = center;
                int count = 1;

                if (x > 0) { sum += exploreField[x - 1, y]; count++; }
                if (x < chemoWidth - 1) { sum += exploreField[x + 1, y]; count++; }
                if (y > 0) { sum += exploreField[x, y - 1]; count++; }
                if (y < chemoHeight - 1) { sum += exploreField[x, y + 1]; count++; }

                float blurred = Mathf.Lerp(center, sum / count, chemoDiffuseRate);
                exploreField[x, y] = Mathf.Max(0f, blurred - decay);
            }
        }

        var temp = exploreField;
        exploreField = exploreNext;
        exploreNext = temp;
    }

}
