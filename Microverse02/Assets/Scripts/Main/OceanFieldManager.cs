using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class OceanFieldManager : MonoBehaviour
{
    [SerializeField] MapManager mapManager;
    [SerializeField] Renderer fieldRenderer;

    //Grid
    int chemoWidth = 500;
    int chemoHeight;

    [Header("Trail")]
    [SerializeField] float chemoDepositAmount = 0.45f;  // how strong bacteria chemo is 
    [SerializeField] float chemoDecayPerSecond = 0.07f;

    [Header("Draw")]
    [SerializeField] float drawDepositAmount = 23f;
    [SerializeField] float drawDecayPerSecond = 0.095f;
    [SerializeField] float drawDiffuseRate = 0.3f;

    [Header("Plankton")]
    [SerializeField] float planktonAmount = 1f;
    [SerializeField] float planktonMaxAmount = 2f;

    [Header("Plankton Colours")]
    [SerializeField] Color planktonColor = new Color(0.6f, 1f, 0.7f, 1f);
    [SerializeField] float planktonVisualStrength = 1f;

    [Header("Plankton Motion")]
    [SerializeField] float planktonMoveChance = 0.08f;
    [SerializeField] uint planktonRandomSeed = 12345;

    [Header("Sensors")]
    [SerializeField] float chemoSensorDistance = 4.5f;
    float chemoSensorAngle = 38f;


    [Header("Sampling Weights")]
    [SerializeField] float trailWeight = 1f;
    [SerializeField] float foodWeight = 5f;

    [Header("Tick")]
    [SerializeField] float fieldTickInterval = 1f / 30f; // 30 Hz

    [Header("Player Trail Colours")]
    [SerializeField] Color playerWeakColor = new Color(0.4f, 0f, 0f, 0f);
    [SerializeField] Color playerStrongColor = new Color(1f, 1f, 0.6f, 1f);


    [Header("Draw Colours")]
    [SerializeField] Color drawColor = new Color(0.2f, 0.8f, 1f, 1f);
    float drawVisualStrength = 3f;

    public float trailMaxDeposit = 1.5f;
    float drawMaxDeposit = 30f;

    Texture2D trailTexture;
    Texture2D planktonTexture;

    NativeArray<Color32> planktonPixels;

    [Header("Chemo Grid")]
    NativeArray<float> playerTrailField;
    NativeArray<float> playerTrailNext;

    [Header("Plankton Grid")]
    NativeArray<float> planktonField;
    NativeArray<float> planktonNext;
    uint planktonTick;

    NativeArray<float> drawField;
    NativeArray<float> drawNext;


    NativeArray<Color32> trailPixels;

    float fieldTickTimer = 0f;
    int foodTickCounter;

    int CellCount => chemoWidth * chemoHeight;

    //getter
    public float SensorDistance => chemoSensorDistance;
    public float SensorAngle => chemoSensorAngle;
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
        if (fieldRenderer != null && fieldRenderer.material != null)
        {
            fieldRenderer.material.mainTexture = null;
            fieldRenderer.material.SetTexture("_TrailTex", null);
            fieldRenderer.material.SetTexture("_PlanktonTex", null);
        }
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

        playerTrailField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        playerTrailNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        drawField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        drawNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        planktonField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        planktonNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        trailPixels = new NativeArray<Color32>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        planktonPixels = new NativeArray<Color32>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    void DisposeArrays()
    {
        if (playerTrailField.IsCreated) playerTrailField.Dispose();
        if (playerTrailNext.IsCreated) playerTrailNext.Dispose();

        if (planktonField.IsCreated) planktonField.Dispose();
        if (planktonNext.IsCreated) planktonNext.Dispose();
        if (drawField.IsCreated) drawField.Dispose();
        if (drawNext.IsCreated) drawNext.Dispose();

        if (trailPixels.IsCreated) trailPixels.Dispose();
        if (planktonPixels.IsCreated) planktonPixels.Dispose();
    }

    void CreateTexture()
    {
        trailTexture = new Texture2D(chemoWidth, chemoHeight, TextureFormat.RGBA32, false);
        trailTexture.wrapMode = TextureWrapMode.Clamp;
        trailTexture.filterMode = FilterMode.Point;

        planktonTexture = new Texture2D(chemoWidth, chemoHeight, TextureFormat.RGBA32, false);
        planktonTexture.wrapMode = TextureWrapMode.Clamp;
        planktonTexture.filterMode = FilterMode.Bilinear;
    }
    void SyncRenderer()
    {
        if (fieldRenderer == null) return;

        fieldRenderer.material.SetTexture("_TrailTex", trailTexture);
        fieldRenderer.material.SetTexture("_PlanktonTex", planktonTexture);
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
    public void DepositTrail(Vector2 worldPos, float amountMultiplier = 1f)
    {
        if (!WorldToGrid(worldPos, out int gx, out int gy))
            return;

        int idx = Index(gx, gy);

        playerTrailField[idx] = Mathf.Min(
            playerTrailField[idx] + chemoDepositAmount * amountMultiplier,
            trailMaxDeposit
        );

        if (planktonField.IsCreated && planktonField[idx] > 0f)
        {
            planktonField[idx] = 0f;
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

    public void AddPlankton(Vector2 worldPos, float amountMultiplier = 1f)
    {
        if (!planktonField.IsCreated) return;
        if (mapManager == null) return;

        if (mapManager.IsWallWorld(worldPos))
            return;

        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);

            planktonField[idx] = Mathf.Min(
                planktonField[idx] + planktonAmount * amountMultiplier,
                planktonMaxAmount
            );
        }
    }

    public float SamplePlankton(Vector2 worldPos)
    {
        if (!planktonField.IsCreated)
            return 0f;

        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);
            return planktonField[idx];
        }

        return 0f;
    }
    public float Sample(Vector2 worldPos) //taking the value out from the hash
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);

            float trail = playerTrailField[idx] * trailWeight;
            float draw = drawField[idx];

            return trail +draw;
        }

        return 0f;
    }

    public float SampleDraw(Vector2 worldPos)
    {
        int x, y;
        if (!WorldToGrid(worldPos, out x, out y))
            return 0f;

        int index = x + y * chemoWidth;
        return drawField[index];
    }
    public float SamplePlayer(Vector2 worldPos)
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);

            float trail = playerTrailField[idx] * trailWeight;
            float draw = drawField[idx];

            return trail +draw;
        }

        return 0f;
    }



    Vector2 GridToWorldCentre(int gx, int gy)
    {
        Vector2 halfSize = MapSize * 0.5f;

        float px = (gx + 0.5f) / chemoWidth;
        float py = (gy + 0.5f) / chemoHeight;

        float wx = (MapCentre.x - halfSize.x) + px * MapSize.x;
        float wy = (MapCentre.y - halfSize.y) + py * MapSize.y;

        return new Vector2(wx, wy);
    }

   

    public bool TickField(float dt)//update pretty much
    {
        bool updated = false;
        fieldTickTimer += dt;

        while (fieldTickTimer >= fieldTickInterval)
        {
            fieldTickTimer -= fieldTickInterval;
            updated = true;

            RunFieldUpdate(playerTrailField, playerTrailNext, 0f, chemoDecayPerSecond, fieldTickInterval);
            Swap(ref playerTrailField, ref playerTrailNext);

            RunFieldUpdate(drawField, drawNext, drawDiffuseRate, drawDecayPerSecond, fieldTickInterval);
            Swap(ref drawField, ref drawNext);

            RunPlanktonMove(fieldTickInterval);
        }

        return updated;
    }
    void RunPlanktonMove(float dt)
    {
        for (int i = 0; i < planktonNext.Length; i++)
            planktonNext[i] = 0f;

        for (int index = 0; index < planktonField.Length; index++)
        {
            float value = planktonField[index];
            if (value <= 0f)
                continue;

            int x = index % chemoWidth;
            int y = index / chemoWidth;

            uint h = (uint)(index * 73856093) ^ (planktonTick * 19349663) ^ planktonRandomSeed;
            float r = Hash01(h);

            int nx = x;
            int ny = y;

            if (r < planktonMoveChance)
            {
                int dir = (int)(Hash01(h ^ 0x9E3779B9u) * 9f);

                switch (dir)
                {
                    case 0: nx = x; ny = y - 1; break;
                    case 1: nx = x; ny = y + 1; break;
                    case 2: nx = x - 1; ny = y; break;
                    case 3: nx = x + 1; ny = y; break;
                    case 4: nx = x - 1; ny = y - 1; break;
                    case 5: nx = x + 1; ny = y - 1; break;
                    case 6: nx = x - 1; ny = y + 1; break;
                    case 7: nx = x + 1; ny = y + 1; break;
                    default: nx = x; ny = y; break;
                }

                nx = Mathf.Clamp(nx, 0, chemoWidth - 1);
                ny = Mathf.Clamp(ny, 0, chemoHeight - 1);
            }

            int targetIndex = nx + ny * chemoWidth;
            planktonNext[targetIndex] += value;
        }

        Swap(ref planktonField, ref planktonNext);
        planktonTick++;
    }

    static float Hash01(uint x)
    {
        x ^= x >> 17;
        x *= 0xED5AD4BBu;
        x ^= x >> 11;
        x *= 0xAC4C1B51u;
        x ^= x >> 15;
        x *= 0x31848BABu;
        x ^= x >> 14;
        return (x & 0x00FFFFFFu) / 16777215f;
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
        if (trailTexture == null || !trailPixels.IsCreated || !planktonPixels.IsCreated)
            return;

        var trailJob = new BuildPixelsJob
        {
            playerTrailField = playerTrailField,
            drawField = drawField,
            pixels = trailPixels,

            playerWeakColor = ToFloat4(playerWeakColor),
            playerStrongColor = ToFloat4(playerStrongColor),
            drawColor = ToFloat4(drawColor),

            drawVisualStrength = drawVisualStrength,
            chemoDepositAmount = chemoDepositAmount,
            trailMaxDeposit = trailMaxDeposit
        };

        JobHandle handle1 = trailJob.Schedule(CellCount, 128);
        handle1.Complete();

        trailTexture.SetPixelData(trailPixels, 0);
        trailTexture.Apply(false);

        var planktonJob = new BuildPlanktonPixelsJob
        {
            planktonField = planktonField,
            pixels = planktonPixels,
            planktonColor = ToFloat4(planktonColor),
            planktonMaxAmount = planktonMaxAmount
        };

        JobHandle handle2 = planktonJob.Schedule(CellCount, 128);
        handle2.Complete();

        planktonTexture.SetPixelData(planktonPixels, 0);
        planktonTexture.Apply(false);
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
    struct BuildPlanktonPixelsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> planktonField;
        [WriteOnly] public NativeArray<Color32> pixels;

        public float4 planktonColor;
        public float planktonMaxAmount;

        public void Execute(int index)
        {
            float v = math.saturate(planktonField[index] / planktonMaxAmount);

            float4 c = new float4(0f, 0f, 0f, 0f);

            if (v > 0f)
            {
                c.xyz = planktonColor.xyz;
                c.w = v;
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
    [BurstCompile]
    struct BuildPixelsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> playerTrailField;
        [ReadOnly] public NativeArray<float> drawField;

        [WriteOnly] public NativeArray<Color32> pixels;

        public float4 playerWeakColor;
        public float4 playerStrongColor;
        public float4 drawColor;

        public float chemoDepositAmount;
        public float trailMaxDeposit;
        public float drawVisualStrength;

        public void Execute(int index)
        {
            float4 c = new float4(0f, 0f, 0f, 0f);

            float draw = math.saturate(drawField[index] * drawVisualStrength);
            if (draw > 0f)
            {
                float4 drawOverlay = drawColor;
                drawOverlay.w = 1f;
                c.xyz = math.lerp(c.xyz, drawOverlay.xyz, draw);
            }

            float playerV = playerTrailField[index];

            if (playerV > 0f)
            {
                float maxValue = math.max(0.0001f, trailMaxDeposit);
                float k = playerV / maxValue;

                float4 trailColor = math.lerp(playerWeakColor, playerStrongColor, k);
                float brightness = math.lerp(0.5f, 1.5f, k);
                trailColor.xyz *= brightness;

                float alpha = math.lerp(0.5f, 1f, k);

                c.xyz = trailColor.xyz;
                c.w = alpha;
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


  
   

