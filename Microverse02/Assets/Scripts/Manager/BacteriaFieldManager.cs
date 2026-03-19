using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class BacteriaFieldManager : MonoBehaviour
{
    Vector2 mapCentre = Vector2.zero;
    float mapRadius = 90f;
    
    [Header("Chemo Grid")]
    float[] exploreField;
    float[] exploreNext;

    float[] foodField;
    float[] foodNext;

    float[] drawField;
    float[] drawNext;

   
    

    [SerializeField] Renderer fieldRenderer;

    Texture2D trailTexture;
    Color[] trailPixels;

    int chemoWidth = 400; 
    int chemoHeight = 400;
    int CellCount => chemoWidth * chemoHeight;

    [Header("Trail")]
    [SerializeField] float chemoDepositAmount = 0.3f;  // how strong bacteria chemo is 
    [SerializeField] float chemoDecayPerSecond = 0.2f;

    [Header("Food")]
    [SerializeField] float foodDepositAmount = 0.8f;
    float foodDecayPerSecond = 0.88f;
    float foodDiffuseRate = 1f; //How much chemical spreads to neighbours. Like blurring.

    [Header("Draw")]
    [SerializeField] float drawDepositAmount = 3f;
    [SerializeField] float drawDecayPerSecond = 0.3f;
    [SerializeField] float drawDiffuseRate = 0f;
 

    [Header("Sensors")]
    [SerializeField] float chemoSensorDistance = 6f; 
    float chemoSensorAngle = 35f; 
    float chemoSteerStrength = 5f;

    [Header("Sampling Weights")]
    [SerializeField] float trailWeight = 1f;
    [SerializeField] float foodWeight = 2f;

    float trailMaxDeposit = 1f;
    float foodMaxDeposit = 5f;
    float drawMaxDeposit = 30f;

    float fieldTickTimer = 0f;
    [SerializeField] float fieldTickInterval = 1f / 30f; // 30 Hz

    [Header("Trail Colours")]
    [SerializeField] Color weakColor = new Color(0.4f, 0f, 0f, 0f);
    [SerializeField] Color midColor = new Color(1f, 0.3f, 0.05f, 1f);
    [SerializeField] Color strongColor = new Color(1f, 1f, 0.6f, 1f);

    [Header("Food Colours")]
    [SerializeField] Color foodColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] float foodVisualStrength = 1f;

    [Header("Draw Colours")]
    [SerializeField] Color drawColor = new Color(0.2f, 0.8f, 1f, 1f);
    float drawVisualStrength = 1f;

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

        drawField = new float[cellCount];
        drawNext = new float[cellCount];
        //GPU
        trailTexture = new Texture2D(chemoWidth, chemoHeight, TextureFormat.RGBA32, false);
        trailTexture.wrapMode = TextureWrapMode.Clamp;
        trailTexture.filterMode = FilterMode.Point;

        trailPixels = new Color[CellCount];

        if (fieldRenderer != null)
        {
            fieldRenderer.material.mainTexture = trailTexture;
            fieldRenderer.transform.position = new Vector3(mapCentre.x, mapCentre.y, 0f);
            fieldRenderer.transform.localScale = new Vector3(mapRadius * 2f, mapRadius * 2f, 1f);
        }

        drawDiffuseRate = Mathf.Clamp01(drawDiffuseRate);
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
            int idx = Index(gx, gy);
            exploreField[idx] = Mathf.Min(
                exploreField[idx] + chemoDepositAmount * amountMultiplier,
                trailMaxDeposit
            );
        }
    }
    public void DepositFood(Vector2 worldPos, float amountMultiplier = 1f)
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);
            foodField[idx] = Mathf.Min(
            foodField[idx] + foodDepositAmount * amountMultiplier,
            foodMaxDeposit
);
        }
    }
    public void DepositDraw(Vector2 worldPos, float amountMultiplier = 1f)
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);
            drawField[idx] = Mathf.Min(
                drawField[idx] + drawDepositAmount * amountMultiplier,
                drawMaxDeposit
            );
        }
    }

    public float Sample(Vector2 worldPos) //taking the value out from the hash
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);

            float trail = exploreField[idx] * trailWeight;
            float food = foodField[idx] * foodWeight;
            float draw = drawField[idx];

            return trail + food + draw;
        }

        return 0f;
    }
    public bool TickField(float dt) //update pretty much
    {
        bool updated = false;
        fieldTickTimer += dt;

        while (fieldTickTimer >= fieldTickInterval)
        {
            fieldTickTimer -= fieldTickInterval;
            updated = true;

            UpdateField(exploreField, exploreNext, 0f, chemoDecayPerSecond, fieldTickInterval);
            Swap(ref exploreField, ref exploreNext);

            UpdateField(drawField, drawNext, drawDiffuseRate, drawDecayPerSecond, fieldTickInterval);
            Swap(ref drawField, ref drawNext);

            foodTickCounter++;
            if (foodTickCounter >= 2)
            {
                foodTickCounter = 0;
                UpdateField(foodField, foodNext, foodDiffuseRate, foodDecayPerSecond, fieldTickInterval * 2f);
                Swap(ref foodField, ref foodNext);
            }
        }

        return updated;
    }


    void UpdateField(float[] source, float[] target, float diffuseRate, float decayPerSecond, float dt)
    {
        float decay = decayPerSecond * dt;

        int maxX = chemoWidth - 1;
        int maxY = chemoHeight - 1;

        for (int y = 1; y < maxY; y++)
        {
            int row = y * chemoWidth;
            int rowUp = (y - 1) * chemoWidth;
            int rowDown = (y + 1) * chemoWidth;

            for (int x = 1; x < maxX; x++)
            {
                int idx = row + x;

                float center = source[idx];
                float sum =
                    source[idx] +
                    source[idx - 1] +
                    source[idx + 1] +
                    source[rowUp + x] +
                    source[rowDown + x] +
                    source[rowUp + x - 1] +
                    source[rowUp + x + 1] +
                    source[rowDown + x - 1] +
                    source[rowDown + x + 1];

                float value = Mathf.Lerp(center, sum /9f, diffuseRate);

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

        float value = Mathf.Lerp(center, sum / count, diffuseRate);

        target[Index(x, y)] = Mathf.Max(0f, value - decay);
    }

    void Swap(ref float[] a, ref float[] b) //for double buffering
    {
        float[] temp = a;
        a = b;
        b = temp;
    }

    //GPU


    public void UpdateTrailTexture()
    {
        if (trailTexture == null) return;

        for (int i = 0; i < CellCount; i++)
        {
            Color c = new Color(0f, 0f, 0f, 0f);

            // 1. draw field first
            float draw = Mathf.Clamp01(drawField[i] * drawVisualStrength);
            if (draw > 0f)
            {
                Color drawOverlay = drawColor;
                drawOverlay.a = draw;
                c = Color.Lerp(c, drawOverlay, draw);
            }

            // 2. food on top of draw
            float food = Mathf.Clamp01(foodField[i] * foodVisualStrength);
            if (food > 0f)
            {
                Color foodOverlay = foodColor;
                foodOverlay.a = food;
                c = Color.Lerp(c, foodOverlay, food);
            }

            // 3. bacteria trail last, so it appears on top
            float v = exploreField[i];
            float t = Mathf.Clamp01(v);
            float alpha = Mathf.Clamp01(Mathf.Pow(t, 0.6f));

            if (t > 0f)
            {
                Color trailColor;

                if (t < 0.96f)
                {
                    trailColor = Color.Lerp(weakColor, midColor, t / 0.96f);
                }
                else
                {
                    trailColor = Color.Lerp(midColor, strongColor, (t - 0.96f) / 0.05f);
                }

                trailColor.a = alpha;

                c = Color.Lerp(c, trailColor, alpha);
            }

            trailPixels[i] = c;
        }

        trailTexture.SetPixels(trailPixels);
        trailTexture.Apply(false);
    }

}
