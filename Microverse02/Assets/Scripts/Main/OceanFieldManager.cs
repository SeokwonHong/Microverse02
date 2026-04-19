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
    int chemoWidth = 500; //500
    int chemoHeight;

    //score
    [SerializeField] int score;

    [Header("Trail")]
    [SerializeField] float chemoDepositAmount = 0.45f;  // how strong bacteria chemo is 
    [SerializeField] float chemoDecayPerSecond = 0.07f;

    [Header("Draw")]
    [SerializeField] float drawDepositAmount = 23f;
    [SerializeField] float drawDecayPerSecond = 0.095f;
    [SerializeField] float drawDiffuseRate = 0.3f;

    [Header("Plankton")]
    [SerializeField] int planktonMaxAmount = 2;

    [Header("Plankton Colours")]
    [SerializeField] Color planktonColor = new Color(0.6f, 1f, 0.7f, 1f);

    [Header("Plankton Motion")]
    [SerializeField] float planktonMoveChance = 0.08f;
    [SerializeField] uint planktonRandomSeed = 12345;

    [Header("Sensors")]
    [SerializeField] float chemoSensorDistance = 4.5f;
    float chemoSensorAngle = 38f;


    [Header("Sampling Weights")]
    [SerializeField] float trailWeight = 1f;

    [Header("Tick")]
    [SerializeField] float fieldTickInterval = 1f / 30f; // 30 Hz

    [Header("Player Trail Colours")]
    [SerializeField] Color playerWeakColor = new Color(0.4f, 0f, 0f, 0f);
    [SerializeField] Color playerStrongColor = new Color(1f, 1f, 0.6f, 1f);
    [Header("Enemy Trail Colours")]
    [SerializeField] Color enemyWeakColor = new Color(0f, 0.2f, 0.4f, 0f);
    [SerializeField] Color enemyStrongColor = new Color(0.3f, 0.9f, 1f, 1f);

    [Header("Draw Colours")]
    Color drawColor;
    float drawVisualStrength = 3f;

    public float trailMaxDeposit = 1.5f;
    float drawMaxDeposit = 30f;

    Texture2D trailTexture;
    Texture2D planktonTexture;

    NativeArray<Color32> planktonPixels;

    [Header("Chemo Grid_Player")]
    NativeArray<float> playerTrailField;
    NativeArray<float> playerTrailNext;
    [Header("Chemo Grid_Enemy")]
    NativeArray<float> enemyTrailField;
    NativeArray<float> enemyTrailNext;

    [Header("Plankton Grid")]
    NativeArray<int> planktonField;
    NativeArray<int> planktonNext;
    uint planktonTick;

    NativeArray<float> drawField;
    NativeArray<float> drawNext;


    NativeArray<Color32> trailPixels;

    float fieldTickTimer = 0f;
    int foodTickCounter;





    //getter
    int CellCount => chemoWidth * chemoHeight;
    public float SensorDistance // visual
    {
        get => chemoSensorDistance;
        set => chemoSensorDistance = value;
    }
    public Color EnemyWeakColor// visual
    {
        get => enemyWeakColor;
        set => enemyWeakColor = value;
    }

    public Color EnemyStrongColor// visual
    {
        get => enemyStrongColor;
        set => enemyStrongColor = value;
    }
    public float SensorAngle => chemoSensorAngle;
    public MapManager MapManager => mapManager;

    public int Score => score;
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

        drawColor = playerStrongColor; 
        
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

        enemyTrailField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        enemyTrailNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        drawField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        drawNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        planktonField = new NativeArray<int>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        planktonNext = new NativeArray<int>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        trailPixels = new NativeArray<Color32>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        planktonPixels = new NativeArray<Color32>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    void DisposeArrays()
    {
        if (playerTrailField.IsCreated) playerTrailField.Dispose();
        if (playerTrailNext.IsCreated) playerTrailNext.Dispose();

        if (enemyTrailField.IsCreated) enemyTrailField.Dispose();
        if (enemyTrailNext.IsCreated) enemyTrailNext.Dispose();

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
    }
    public void DepositEnemyTrail(Vector2 worldPos, float amountMultiplier = 1f)
    {
        if (!WorldToGrid(worldPos, out int gx, out int gy))
            return;

        int idx = Index(gx, gy);

        enemyTrailField[idx] = Mathf.Min(
            enemyTrailField[idx] + chemoDepositAmount * amountMultiplier,
            trailMaxDeposit
        );

     
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
    void ClearDrawWhereEnemyExists()
    {
        for (int i = 0; i < CellCount; i++)
        {
            if (enemyTrailField[i] > 0f)
                drawField[i] = 0f;
        }
    }
    public bool AddPlankton(Vector2 worldPos, int amount = 1)
    {
        if (!WorldToGrid(worldPos, out int gx, out int gy)) return false;
        int idx = Index(gx, gy);

        if (planktonField[idx] < planktonMaxAmount)
        {
            planktonField[idx] = Mathf.Min(planktonField[idx] + amount, planktonMaxAmount);
            return true;
        }
        return false;
    }

    public bool EatPlanktonAt(Vector2 worldPos, int radiusCells = 1, bool addScore = true)
    {
        if (!planktonField.IsCreated) return false;
        if (!WorldToGrid(worldPos, out int gx, out int gy)) return false;

        bool ateAny = false;

        for (int oy = -radiusCells; oy <= radiusCells; oy++)
        {
            int ny = gy + oy;
            if (ny < 0 || ny >= chemoHeight) continue;

            for (int ox = -radiusCells; ox <= radiusCells; ox++)
            {
                int nx = gx + ox;
                if (nx < 0 || nx >= chemoWidth) continue;

                int idx = Index(nx, ny);

                if (planktonField[idx] > 0)
                {
                    if (addScore)
                        score++;

                    planktonField[idx] = 0;
                    ateAny = true;
                }
            }
        }

        return ateAny;
    }
    public int SamplePlankton(Vector2 worldPos)
    {
        if (!planktonField.IsCreated)
            return 0;

        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);
            return planktonField[idx];
        }

        return 0;
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
    public float SamplePlayerTrailOnly(Vector2 worldPos)
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);
            return playerTrailField[idx];
        }

        return 0f;
    }
    public float SampleEnemyTrailOnly(Vector2 worldPos)
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);

            float trail = enemyTrailField[idx] * trailWeight;
            return trail;
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




    public bool TickField(float dt)
    {
        bool updated = false;
        fieldTickTimer += dt;

        while (fieldTickTimer >= fieldTickInterval)
        {
            fieldTickTimer -= fieldTickInterval;
            updated = true;

            RunFieldUpdate(playerTrailField, playerTrailNext, 0f, chemoDecayPerSecond, fieldTickInterval);
            Swap(ref playerTrailField, ref playerTrailNext);

            RunFieldUpdate(enemyTrailField, enemyTrailNext, 0f, chemoDecayPerSecond, fieldTickInterval);
            Swap(ref enemyTrailField, ref enemyTrailNext);

            RunFieldUpdate(drawField, drawNext, drawDiffuseRate, drawDecayPerSecond, fieldTickInterval);
            Swap(ref drawField, ref drawNext);

            ClearDrawWhereEnemyExists();
        }

        return updated;
    }
    void RunPlanktonMove(float dt)
    {
        for (int i = 0; i < planktonNext.Length; i++)
            planktonNext[i] = 0;

        for (int index = 0; index < planktonField.Length; index++)
        {
            int value = planktonField[index];
            if (value <= 0)
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

            if (planktonNext[targetIndex] > planktonMaxAmount)
                planktonNext[targetIndex] = planktonMaxAmount;
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
    void Swap<T>(ref NativeArray<T> a, ref NativeArray<T> b) where T : struct
    {
        NativeArray<T> temp = a;
        a = b;
        b = temp;
    }
    public bool TryWorldToGrid(Vector2 worldPos, out int gx, out int gy)
    {
        return WorldToGrid(worldPos, out gx, out gy);
    }
    public void UpdateTrailTexture()
    {
        if (trailTexture == null || !trailPixels.IsCreated || !planktonPixels.IsCreated)
            return;

        var trailJob = new BuildPixelsJob
        {
            playerTrailField = playerTrailField,
            enemyTrailField = enemyTrailField,
            drawField = drawField,
            pixels = trailPixels,

            playerWeakColor = ToFloat4(playerWeakColor),
            playerStrongColor = ToFloat4(playerStrongColor),
            enemyWeakColor = ToFloat4(enemyWeakColor),
            enemyStrongColor = ToFloat4(enemyStrongColor),
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

    public int GetTotalPlankton()
    {
        if (!planktonField.IsCreated) return 0;

        int total = 0;

        for (int i = 0; i < planktonField.Length; i++)
        {
            total += planktonField[i];
        }

        return total;
    }
    public bool HasPlanktonAt(Vector2 worldPos)
    {
        if (!planktonField.IsCreated)
            return false;

        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            return planktonField[Index(gx, gy)] > 0;
        }

        return false;
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
        [ReadOnly] public NativeArray<int> planktonField;
        [WriteOnly] public NativeArray<Color32> pixels;

        public float4 planktonColor;
        public int planktonMaxAmount;

        public void Execute(int index)
        {
            float v = math.saturate((float)planktonField[index] / math.max(1, planktonMaxAmount));

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
        [ReadOnly] public NativeArray<float> enemyTrailField;
        [ReadOnly] public NativeArray<float> drawField;

        [WriteOnly] public NativeArray<Color32> pixels;

        public float4 playerWeakColor;
        public float4 playerStrongColor;
        public float4 enemyWeakColor;
        public float4 enemyStrongColor;
        public float4 drawColor;

        public float chemoDepositAmount;
        public float trailMaxDeposit;
        public float drawVisualStrength;

        public void Execute(int index)
        {
            float4 c = new float4(0f, 0f, 0f, 0f);

            ////////////////////////////////
            //Draw deposit colour
            ////////////////////////////////
            ///
            float draw = math.saturate(drawField[index] * drawVisualStrength);
            if (draw > 0f)
            {
                float4 drawOverlay = drawColor;
                drawOverlay.w = 1f;
                c.xyz = math.lerp(c.xyz, drawOverlay.xyz, draw);
            }
            ////////////////////////////////
            //Trail deposit colour and alpha
            ////////////////////////////////
            float playerV = playerTrailField[index];
            float enemyV = enemyTrailField[index];

            if (playerV > 0f || enemyV > 0f)
            {
                float maxValue = math.max(0.0001f, trailMaxDeposit);

                float pk = playerV / maxValue;
                float ek = enemyV / maxValue;

                float4 playerColor = math.lerp(playerWeakColor, playerStrongColor, pk);
                float4 enemyColor = math.lerp(enemyWeakColor, enemyStrongColor, ek);

                float playerBrightness = math.lerp(0.5f, 1.5f, pk);
                float enemyBrightness = math.lerp(0.5f, 1.5f, ek);

                playerColor.xyz *= playerBrightness;
                enemyColor.xyz *= enemyBrightness;

                float total = playerV + enemyV;

                if (total > 0f)
                {
                    float playerWeight = playerV / total;
                    float enemyWeight = enemyV / total;

                    c.xyz = playerColor.xyz * playerWeight + enemyColor.xyz * enemyWeight;
                    c.w = math.saturate(math.max(pk, ek));
                }
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


  
   

