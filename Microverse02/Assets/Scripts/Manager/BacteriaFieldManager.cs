using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class BacteriaFieldManager : MonoBehaviour
{
    [SerializeField] MapManager mapManager;
    [SerializeField] Renderer fieldRenderer;

    //Grid
    int chemoWidth = 400;
    int chemoHeight;

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

    [Header("Tick")]
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
    float drawVisualStrength = 3f;

    float trailMaxDeposit = 1f;
    float foodMaxDeposit = 5f;
    float drawMaxDeposit = 5f;

    Texture2D trailTexture;


    [Header("Chemo Grid")]
    NativeArray<float> exploreField;
    NativeArray<float> exploreNext;

    NativeArray<float> foodField;
    NativeArray<float> foodNext;

    NativeArray<float> drawField;
    NativeArray<float> drawNext;

    NativeArray<Color32> trailPixels;

    float fieldTickTimer = 0f;
    int foodTickCounter;

    int CellCount => chemoWidth * chemoHeight;

    //getter
    public float SensorDistance => chemoSensorDistance;
    public float SensorAngle => chemoSensorAngle;
    public float SteerStrength => chemoSteerStrength;
    public MapManager MapManager => mapManager;


    Vector2 MapCentre => mapManager.MapCentre;
    Vector2 MapSize => mapManager.MapSize;

    private void Awake()
    {
        if (mapManager == null)
            mapManager = FindAnyObjectByType<MapManager>();

        if (mapManager == null)
        {
            Debug.LogError("BacteriaFieldManager: MapManager not found.");
            enabled = false;
            return;
        }
        SyncGridToMapAspect();
        AllocateArrays();
        CreateTexture();
        SyncRenderer();
        
        drawDiffuseRate = Mathf.Clamp01(drawDiffuseRate);
    }
    void OnDestroy()
    {
        DisposeArrays();
    }

    void OnDisable()
    {
        if (trailTexture != null && fieldRenderer != null && fieldRenderer.material != null)
            fieldRenderer.material.mainTexture = null;
    }

    void SyncGridToMapAspect()
    {
        float safeHeight = Mathf.Max(0.0001f, MapSize.y);
        float aspect = MapSize.x / safeHeight;
        chemoHeight = Mathf.Max(1, Mathf.RoundToInt(chemoWidth / aspect));
    }
    void AllocateArrays()
    {
        int count = CellCount;

        exploreField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        exploreNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        foodField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        foodNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        drawField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        drawNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        trailPixels = new NativeArray<Color32>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    void DisposeArrays()
    {
        if (exploreField.IsCreated) exploreField.Dispose();
        if (exploreNext.IsCreated) exploreNext.Dispose();

        if (foodField.IsCreated) foodField.Dispose();
        if (foodNext.IsCreated) foodNext.Dispose();

        if (drawField.IsCreated) drawField.Dispose();
        if (drawNext.IsCreated) drawNext.Dispose();

        if (trailPixels.IsCreated) trailPixels.Dispose();
    }

    void CreateTexture()
    {
        trailTexture = new Texture2D(chemoWidth, chemoHeight, TextureFormat.RGBA32, false);
        trailTexture.wrapMode = TextureWrapMode.Clamp;
        trailTexture.filterMode = FilterMode.Point;
    }
    void SyncRenderer()
    {
        if (fieldRenderer == null) return;

        fieldRenderer.material.mainTexture = trailTexture;
        fieldRenderer.transform.position = new Vector3(MapCentre.x, MapCentre.y, 0f);
        fieldRenderer.transform.localScale = new Vector3(MapSize.x, MapSize.y, 1f);
    }

    int Index(int x, int y)
    {
        return x + y * chemoWidth;
    }

    bool WorldToGrid(Vector2 worldPos, out int gx, out int gy)
    {
        Vector2 halfSize = MapSize * 0.5f;

        float px = (worldPos.x - (MapCentre.x - halfSize.x)) / MapSize.x;
        float py = (worldPos.y - (MapCentre.y - halfSize.y)) / MapSize.y;

        gx = Mathf.FloorToInt(px * chemoWidth);
        gy = Mathf.FloorToInt(py * chemoHeight);

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
        if (!exploreField.IsCreated) return;

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
        if (!foodField.IsCreated) return;

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
        if (!drawField.IsCreated) return;
        if (mapManager == null) return;

        if (mapManager.IsWallWorld(worldPos))
            return;

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

            RunFieldUpdate(exploreField, exploreNext, 0f, chemoDecayPerSecond, fieldTickInterval);
            Swap(ref exploreField, ref exploreNext);

            RunFieldUpdate(drawField, drawNext, drawDiffuseRate, drawDecayPerSecond, fieldTickInterval);
            Swap(ref drawField, ref drawNext);

            foodTickCounter++;
            if (foodTickCounter >= 2)
            {
                foodTickCounter = 0;
                RunFieldUpdate(foodField, foodNext, foodDiffuseRate, foodDecayPerSecond, fieldTickInterval * 2f);
                Swap(ref foodField, ref foodNext);
            }
        }

        return updated;
    }

    void RunFieldUpdate(NativeArray<float> source, NativeArray<float> target, float diffuseRate, float decayPerSecond, float dt)
    {
        var job = new UpdateFieldJob
        {
            source = source,
            target = target,
            width = chemoWidth,
            height = chemoHeight,
            diffuseRate = diffuseRate,
            decay = decayPerSecond * dt
        };

        JobHandle handle = job.Schedule(CellCount, 128);
        handle.Complete();
    }
    void Swap(ref NativeArray<float> a, ref NativeArray<float> b) //for double buffering
    {
        NativeArray<float> temp = a;
        a = b;
        b = temp;
    }

    public void UpdateTrailTexture()
    {
        if (trailTexture == null || !trailPixels.IsCreated) return;

        var job = new BuildPixelsJob
        {
            exploreField = exploreField,
            foodField = foodField,
            drawField = drawField,
            pixels = trailPixels,

            weakColor = ToFloat4(weakColor),
            midColor = ToFloat4(midColor),
            strongColor = ToFloat4(strongColor),
            foodColor = ToFloat4(foodColor),
            drawColor = ToFloat4(drawColor),

            foodVisualStrength = foodVisualStrength,
            drawVisualStrength = drawVisualStrength
        };

        JobHandle handle = job.Schedule(CellCount, 128);
        handle.Complete();

        trailTexture.SetPixelData(trailPixels, 0);
        trailTexture.Apply(false);
    }
    static float4 ToFloat4(Color c)
    {
        return new float4(c.r, c.g, c.b, c.a);
    }

    [BurstCompile]
    struct UpdateFieldJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> source;
        [WriteOnly] public NativeArray<float> target;

        public int width;
        public int height;
        public float diffuseRate;
        public float decay;

        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;

            float center = source[index];
            float sum = 0f;
            int count = 0;

            for (int oy = -1; oy <= 1; oy++)
            {
                int ny = y + oy;
                if (ny < 0 || ny >= height) continue;

                int row = ny * width;

                for (int ox = -1; ox <= 1; ox++)
                {
                    int nx = x + ox;
                    if (nx < 0 || nx >= width) continue;

                    sum += source[row + nx];
                    count++;
                }
            }

            float avg = sum / count;
            float value = math.lerp(center, avg, diffuseRate);
            target[index] = math.max(0f, value - decay);
        }
    }

    [BurstCompile]
    struct BuildPixelsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> exploreField;
        [ReadOnly] public NativeArray<float> foodField;
        [ReadOnly] public NativeArray<float> drawField;

        [WriteOnly] public NativeArray<Color32> pixels;

        public float4 weakColor;
        public float4 midColor;
        public float4 strongColor;
        public float4 foodColor;
        public float4 drawColor;

        public float foodVisualStrength;
        public float drawVisualStrength;

        public void Execute(int index)
        {
            float4 c = new float4(0f, 0f, 0f, 0f);

            float draw = math.saturate(drawField[index] * drawVisualStrength);
            if (draw > 0f)
            {
                float4 drawOverlay = drawColor;
                drawOverlay.w = draw;
                c = math.lerp(c, drawOverlay, draw);
            }

            float food = math.saturate(foodField[index] * foodVisualStrength);
            if (food > 0f)
            {
                float4 foodOverlay = foodColor;
                foodOverlay.w = food;
                c = math.lerp(c, foodOverlay, food);
            }

            float t = math.saturate(exploreField[index]);
            float alpha = math.saturate(math.pow(t, 0.6f));

            if (t > 0f)
            {
                float4 trailColor;

                if (t < 0.96f)
                {
                    trailColor = math.lerp(weakColor, midColor, t / 0.96f);
                }
                else
                {
                    trailColor = math.lerp(midColor, strongColor, (t - 0.96f) / 0.05f);
                }

                trailColor.w = alpha;
                c = math.lerp(c, trailColor, alpha);
            }

            pixels[index] = Float4ToColor32(c);
        }

        static Color32 Float4ToColor32(float4 c)
        {
            c = math.saturate(c);

            return new Color32(
                (byte)math.round(c.x * 255f),
                (byte)math.round(c.y * 255f),
                (byte)math.round(c.z * 255f),
                (byte)math.round(c.w * 255f)
            );
        }
    }
}


  
   

