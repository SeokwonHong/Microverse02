using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class BacteriaFieldManager : MonoBehaviour
{
    Vector2 mapCentre = Vector2.zero;
    float mapRadius = 50f;
    
    [Header("Chemo Grid")]
    float[] exploreField;
    float[] exploreNext;

    float[] foodField;
    float[] foodNext;
    

    int chemoWidth = 512; 
    int chemoHeight = 512;
    int CellCount => chemoWidth * chemoHeight;

    [Header("Trail")]
    [SerializeField] float chemoDepositAmount = 0.3f;  // how strong bacteria chemo is 
    float chemoDiffuseRate = 0.01f;  //How much chemical spreads to neighbours. Like blurring.
    float chemoDecayPerSecond = 0.01f;

    [Header("Food")]
    [SerializeField] float foodDepositAmount = 0.5f;
    float foodDecayPerSecond = 0.02f;
    float foodDiffuseRate = 0.01f;


    [Header("Sensors")]
    [SerializeField] float chemoSensorDistance = 6f; 
    float chemoSensorAngle = 35f; 
    float chemoSteerStrength = 5f;

    [Header("Sampling Weights")]
    [SerializeField] float trailWeight = 1f;
    [SerializeField] float foodWeight = 2f;

    [Header("Debug")]
    [SerializeField] bool enableDiffuse = true;

    int foodTickCounter;
    //getter
    public float SensorDistance => chemoSensorDistance;
    public float SensorAngle => chemoSensorAngle;
    public float SteerStrength => chemoSteerStrength;   



    private void Awake()
    {
        int cellCount =chemoWidth * chemoHeight;

        exploreField = new float[cellCount];
        exploreNext = new float[cellCount];

        foodField = new float[cellCount];
        foodNext = new float[cellCount];
    }
    int Index(int x, int y)
    {
        return x + y * chemoWidth;
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
    public void DepositTrail(Vector2 worldPos, float amountMultiplier = 1f) //put chemecals insdie of the space
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            exploreField[Index(gx, gy)] += chemoDepositAmount * amountMultiplier;
        }
    }
    public void DepositFood(Vector2 worldPos, float amountMultiplier = 1f)
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            foodField[Index(gx, gy)] += foodDepositAmount * amountMultiplier;
        }
    }

    public float Sample(Vector2 worldPos) //taking the value out from the hash
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);
            float trail = exploreField[idx] * trailWeight;
            float food = foodField[idx] * foodWeight;
            return trail + food;
        }

        return 0f;
    }
    public void TickField(float dt) //update pretty much
    {
        UpdateField(exploreField, exploreNext, chemoDiffuseRate, chemoDecayPerSecond, dt);
        Swap(ref exploreField, ref exploreNext);

        foodTickCounter++;
        if (foodTickCounter >= 2)
        {
            foodTickCounter = 0;
            UpdateField(foodField, foodNext, foodDiffuseRate, foodDecayPerSecond, dt * 2f);
            Swap(ref foodField, ref foodNext);
        }
    }

    void UpdateField(float[] source, float[] target, float diffuseRate, float decayPerSecond, float dt) //blur + decay
    {
        float decay = decayPerSecond * dt;

        int maxX = chemoWidth - 1;
        int maxY = chemoHeight - 1;

        for (int x = 1; x < maxX; x++)
        {
            for (int y = 1; y < maxY; y++)
            {
                int idx = Index(x, y);

                float center = source[idx];
                float sum =
                    center +
                    source[Index(x - 1, y)] +
                    source[Index(x + 1, y)] +
                    source[Index(x, y - 1)] +
                    source[Index(x, y + 1)];

                float value;

                if (enableDiffuse)
                {
                    float blurred = Mathf.Lerp(center, sum * 0.2f, diffuseRate);
                    value = blurred;
                }
                else
                {
                    value = center;
                }

                target[idx] = Mathf.Max(0f, value - decay);
            }
        }

        for (int x = 0; x < chemoWidth; x++)
        {
            UpdateEdgeCell(source, target, x, 0, diffuseRate, decay);
            UpdateEdgeCell(source, target, x, maxY, diffuseRate, decay);
        }

        for (int y = 1; y < maxY; y++)
        {
            UpdateEdgeCell(source, target, 0, y, diffuseRate, decay);
            UpdateEdgeCell(source, target, maxX, y, diffuseRate, decay);
        }
    }
    void UpdateEdgeCell(float[] source, float[] target, int x, int y, float diffuseRate, float decay)
    {
        float center = source[Index(x, y)];
        float sum = center;
        int count = 1;

        if (x > 0) { sum += source[Index(x - 1, y)]; count++; }
        if (x < chemoWidth - 1) { sum += source[Index(x + 1, y)]; count++; }
        if (y > 0) { sum += source[Index(x, y - 1)]; count++; }
        if (y < chemoHeight - 1) { sum += source[Index(x, y + 1)]; count++; }

        float value;

        if (enableDiffuse)
        {
            float blurred = Mathf.Lerp(center, sum / count, diffuseRate);
            value = blurred;
        }
        else
        {
            value = center;
        }

        target[Index(x, y)] = Mathf.Max(0f, value - decay);
    }

    void Swap(ref float[] a, ref float[] b) //for double buffering
    {
        float[] temp = a;
        a = b;
        b = temp;
    }


    void OnDrawGizmos()
    {
        if (exploreField == null) return;

        float mapSize = mapRadius * 2f;
        float cellSizeX = mapSize / chemoWidth;
        float cellSizeY = mapSize / chemoHeight;

        for (int x = 0; x < chemoWidth; x++)
        {
            for (int y = 0; y < chemoHeight; y++)
            {
                float v = exploreField[Index(x, y)];

                if (v <= 0.001f) continue;

                float wx = mapCentre.x - mapRadius + x * cellSizeX;
                float wy = mapCentre.y - mapRadius + y * cellSizeY;

                float alpha = Mathf.Clamp01(v * 0.1f);
                Gizmos.color = new Color(1f, 0f, 0f, alpha);

                Gizmos.DrawCube(
                    new Vector3(wx, wy, 0),
                    new Vector3(cellSizeX, cellSizeY, 0.01f)
                );
            }
        }
    }
}
