using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class BacteriaFieldManager : MonoBehaviour
{
    Vector2 mapCentre = Vector2.zero;
    float mapRadius = 50f;
    
    [Header("Chemo Grid")]
    float[] exploreField;
    float[] exploreNext;

    float[] headingHomeField;
    float[] headingHomeNext;

    [SerializeField] Renderer fieldRenderer;

    Texture2D trailTexture;
    Color[] trailPixels;

    int chemoWidth = 512; 
    int chemoHeight = 512;
    int CellCount => chemoWidth * chemoHeight;

    [Header("Trail")]
    [SerializeField] float chemoDepositAmount = 0.3f;  // how strong bacteria chemo is 
    [SerializeField] float chemoDecayPerSecond = 0.2f;
    float trailMaxDeposit = 2f;


    [Header("HeadingHome")]
    [SerializeField] float targetDepositAmount = 1f;
    [SerializeField] float targetDecayPerSecond = 0.01f;
    [SerializeField] float targetWeight = 2f;
    [SerializeField] float targetMaxDeposit = 3f;

    [Header("Sensors")]
    [SerializeField] float chemoSensorDistance = 6f; 
    float chemoSensorAngle = 35f; 
    float chemoSteerStrength = 5f;

    [Header("Sampling Weights")]
    [SerializeField] float trailWeight = 1f;

    


    float fieldTickTimer = 0f;
    [SerializeField] float fieldTickInterval = 1f / 30f; // 30 Hz

    [Header("Trail Colours")]
    [SerializeField] Color weakColor = new Color(0.4f, 0f, 0f, 0f);
    [SerializeField] Color midColor = new Color(1f, 0.3f, 0.05f, 1f);
    [SerializeField] Color strongColor = new Color(1f, 1f, 0.6f, 1f);



    //getter
    public float SensorDistance => chemoSensorDistance;
    public float SensorAngle => chemoSensorAngle;
    public float SteerStrength => chemoSteerStrength;



    private void Awake()
    {
        int cellCount =chemoWidth * chemoHeight;

        exploreField = new float[cellCount];
        exploreNext = new float[cellCount];

        headingHomeField = new float[cellCount];
        headingHomeNext = new float[cellCount];

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

    public void DepositTarget(Vector2 worldPos, float amountMultiplier = 1f)
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);
            headingHomeField[idx] = Mathf.Min(
                headingHomeField[idx] + targetDepositAmount * amountMultiplier,
                targetMaxDeposit
            );
        }
    }

    public float SampleTrail(Vector2 worldPos) //taking the value out from the hash
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);
            float trail = exploreField[idx] * trailWeight;
            return trail;
        }

        return 0f;
    }

    public float SampleHeadingHome(Vector2 worldPos)
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);
            return headingHomeField[idx] * targetWeight;
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

            UpdateField(headingHomeField, headingHomeNext, 0f, targetDecayPerSecond, fieldTickInterval);
            Swap(ref headingHomeField, ref headingHomeNext);

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
                    center +
                    source[idx - 1] +
                    source[idx + 1] +
                    source[rowUp + x] +
                    source[rowDown + x];

                float value = Mathf.Lerp(center, sum * 0.2f, diffuseRate);

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


    //public void UpdateTrailTexture()
    //{
    //    if (trailTexture == null) return;

    //    for (int i = 0; i < CellCount; i++)
    //    {
    //        float explore = exploreField[i];
    //        float home = headingHomeField[i];

    //        float v = Mathf.Max(explore, home); // or explore + home

    //        float t = Mathf.Clamp01(v / trailMaxDeposit);
    //        float alpha = Mathf.Pow(t, 0.7f);

    //        Color c;

    //        if (t < 0.8f)
    //        {
    //            c = Color.Lerp(weakColor, midColor, t / 0.8f);
    //        }
    //        else
    //        {
    //            c = Color.Lerp(midColor, strongColor, (t - 0.8f) / 0.2f);
    //        }

    //        c.a = alpha;
    //        trailPixels[i] = c;
    //    }

    //    trailTexture.SetPixels(trailPixels);
    //    trailTexture.Apply(false);
    //}
    public void UpdateTrailTexture()
    {
        if (trailTexture == null) return;

        for (int i = 0; i < CellCount; i++)
        {
            float explore = exploreField[i];
            float home = headingHomeField[i];

            // --- NORMAL TRAIL (red/orange)
            float t = Mathf.Clamp01(explore / trailMaxDeposit);
            float alpha = Mathf.Pow(t, 0.7f);

            Color c;

            if (t < 0.8f)
            {
                c = Color.Lerp(weakColor, midColor, t / 0.8f);
            }
            else
            {
                c = Color.Lerp(midColor, strongColor, (t - 0.8f) / 0.2f);
            }

            c.a = alpha;

            // --- HOME TRAIL (green overlay)
            if (home > 0f)
            {
                float ht = Mathf.Clamp01(home / targetMaxDeposit);

                Color homeColor = Color.green;
                homeColor.a = ht;

                // blend green on top
                c = Color.Lerp(c, homeColor, ht);
            }

            trailPixels[i] = c;
        }

        trailTexture.SetPixels(trailPixels);
        trailTexture.Apply(false);
    }
}
