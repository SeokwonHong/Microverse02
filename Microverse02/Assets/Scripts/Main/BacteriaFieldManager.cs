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
    int chemoWidth = 500;
    int chemoHeight;

    [Header("Trail")]
    [SerializeField] float chemoDepositAmount = 0.45f;  // how strong bacteria chemo is 
    [SerializeField] float chemoDecayPerSecond = 0.07f;

    [Header("Draw")]
    [SerializeField] float drawDepositAmount = 23f;
    [SerializeField] float drawDecayPerSecond = 0.095f;
    [SerializeField] float drawDiffuseRate = 0.3f;


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
    [SerializeField] Color playerMidColor = new Color(1f, 0.3f, 0.05f, 1f);
    [SerializeField] Color playerStrongColor = new Color(1f, 1f, 0.6f, 1f);

    [Header("Enemy Trail Colours")]
    [SerializeField] Color enemyWeakColor = new Color(0f, 0f, 0.4f, 0f);
    [SerializeField] Color enemyMidColor = new Color(0.2f, 0.5f, 1f, 1f);
    [SerializeField] Color enemyStrongColor = new Color(0.8f, 1f, 1f, 1f);

    [Header("Food Colours")]
    [SerializeField] Color foodColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] float foodVisualStrength = 1f;

    [Header("Draw Colours")]
    [SerializeField] Color drawColor = new Color(0.2f, 0.8f, 1f, 1f);
    float drawVisualStrength = 3f;

    public float trailMaxDeposit = 1.5f;
    float drawMaxDeposit = 23f;

    Texture2D trailTexture;


    [Header("Chemo Grid")]
    NativeArray<float> playerTrailField;
    NativeArray<float> playerTrailNext;

    NativeArray<float> enemyTrailField;
    NativeArray<float> enemyTrailNext;

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

        playerTrailField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        playerTrailNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        enemyTrailField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        enemyTrailNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        foodField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        foodNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        drawField = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        drawNext = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        trailPixels = new NativeArray<Color32>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    void DisposeArrays()
    {
        if (playerTrailField.IsCreated) playerTrailField.Dispose();
        if (playerTrailNext.IsCreated) playerTrailNext.Dispose();

        if (enemyTrailField.IsCreated) enemyTrailField.Dispose();
        if (enemyTrailNext.IsCreated) enemyTrailNext.Dispose();

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
    public void DepositTrail(Vector2 worldPos, CellManager.Team team, float amountMultiplier = 1f)
    {
        if (!playerTrailField.IsCreated || !enemyTrailField.IsCreated) return;

        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);

            if (team == CellManager.Team.Player)
            {
                playerTrailField[idx] = Mathf.Min(
                    playerTrailField[idx] + chemoDepositAmount * amountMultiplier,
                    trailMaxDeposit
                );
            }
            else
            {
                enemyTrailField[idx] = Mathf.Min(
                    enemyTrailField[idx] + chemoDepositAmount * amountMultiplier,
                    trailMaxDeposit
                );
            }
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

            float trail = (playerTrailField[idx] + enemyTrailField[idx]) * trailWeight;
            float food = foodField[idx] * foodWeight;
            float draw = drawField[idx];

            return trail + food + draw;
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
            float food = foodField[idx] * foodWeight;
            float draw = drawField[idx];

            return trail + food + draw;
        }

        return 0f;
    }

    public float SampleEnemy(Vector2 worldPos)
    {
        if (WorldToGrid(worldPos, out int gx, out int gy))
        {
            int idx = Index(gx, gy);

            float trail = enemyTrailField[idx] * trailWeight;
            float food = foodField[idx] * foodWeight;
            // no draw if enemy should ignore draw too
            return trail + food;
        }

        return 0f;
    }

    public float SampleButtonArea(Vector2 worldPos, float radius, CellManager.Team team)
    {
        NativeArray<float> field =
            team == CellManager.Team.Player ? playerTrailField : enemyTrailField;

        if (!field.IsCreated) return 0f;
        if (!WorldToGrid(worldPos, out int gx, out int gy)) return 0f;

        float cellSizeX = MapSize.x / chemoWidth;
        float cellSizeY = MapSize.y / chemoHeight;

        int rx = Mathf.CeilToInt(radius / cellSizeX);
        int ry = Mathf.CeilToInt(radius / cellSizeY);

        float sum = 0f;
        int hitCount = 0;
        float radiusSqr = radius * radius;

        for (int y = gy - ry; y <= gy + ry; y++)
        {
            if (y < 0 || y >= chemoHeight) continue;

            for (int x = gx - rx; x <= gx + rx; x++)
            {
                if (x < 0 || x >= chemoWidth) continue;

                Vector2 cellWorld = GridToWorldCentre(x, y);
                Vector2 delta = cellWorld - worldPos;

                if (delta.sqrMagnitude > radiusSqr)
                    continue;

                sum += field[Index(x, y)];
                hitCount++;
            }
        }

        if (hitCount == 0) return 0f;

        return sum / hitCount; // average
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

    public bool TickField(float dt) //update pretty much
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
            playerTrailField = playerTrailField,
            enemyTrailField = enemyTrailField,
            foodField = foodField,
            drawField = drawField,
            pixels = trailPixels,

            playerWeakColor = ToFloat4(playerWeakColor),
            playerMidColor = ToFloat4(playerMidColor),
            playerStrongColor = ToFloat4(playerStrongColor),

            enemyWeakColor = ToFloat4(enemyWeakColor),
            enemyMidColor = ToFloat4(enemyMidColor),
            enemyStrongColor = ToFloat4(enemyStrongColor),

            foodColor = ToFloat4(foodColor),
            drawColor = ToFloat4(drawColor),

            foodVisualStrength = foodVisualStrength,
            drawVisualStrength = drawVisualStrength,
            chemoDepositAmount = chemoDepositAmount,
            trailMaxDeposit = trailMaxDeposit
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
        [ReadOnly] public NativeArray<float> playerTrailField;
        [ReadOnly] public NativeArray<float> enemyTrailField;
        [ReadOnly] public NativeArray<float> foodField;
        [ReadOnly] public NativeArray<float> drawField;

        [WriteOnly] public NativeArray<Color32> pixels;

        public float4 playerWeakColor;
        public float4 playerMidColor;
        public float4 playerStrongColor;

        public float4 enemyWeakColor;
        public float4 enemyMidColor;
        public float4 enemyStrongColor;

        public float4 foodColor;
        public float4 drawColor;

        public float chemoDepositAmount;
        public float trailMaxDeposit;

        public float foodVisualStrength;
        public float drawVisualStrength;

        public void Execute(int index)
        {
            float4 c = new float4(0f, 0f, 0f, 0f);

            // -------------------------
            // DRAW VISUAL (RGB only)
            // -------------------------
            float draw = math.saturate(drawField[index] * drawVisualStrength);
            if (draw > 0f)
            {
                float4 drawOverlay = drawColor;
                drawOverlay.w = 1f;

                c.xyz = math.lerp(c.xyz, drawOverlay.xyz, draw);
            }

            // -------------------------
            // FOOD VISUAL (RGB only)
            // -------------------------
            float food = math.saturate(foodField[index] * foodVisualStrength);
            if (food > 0f)
            {
                float4 foodOverlay = foodColor;
                foodOverlay.w = 1f;

                c.xyz = math.lerp(c.xyz, foodOverlay.xyz, food);
            }

            // -------------------------
            // PLAYER TRAIL VISUAL
            // -------------------------
            float playerV = playerTrailField[index];

            if (playerV > 0f)
            {
                float4 trailColor;

                float midValue = math.max(0.0001f, chemoDepositAmount);
                float maxValue = math.max(midValue, trailMaxDeposit);

                if (playerV <= midValue)
                {
                    float k = math.saturate(playerV / midValue);
                    trailColor = math.lerp(playerWeakColor, playerMidColor, k);
                }
                else
                {
                    float range = math.max(0.0001f, maxValue - midValue);
                    float k = math.saturate((playerV - midValue) / range);
                    trailColor = math.lerp(playerMidColor, playerStrongColor, k);
                }

                float light01 = math.saturate(playerV / maxValue);
                float brightness = math.lerp(0.5f, 1.5f, light01);
                trailColor.xyz *= brightness;

                // player trail wins visually where it exists
                c.xyz = trailColor.xyz;

                // alpha stores TRAIL MASK ONLY
                c.w = 1f;
            }

            // -------------------------
            // ENEMY TRAIL VISUAL
            // -------------------------
            float enemyV = enemyTrailField[index];

            if (enemyV > 0f)
            {
                float4 trailColor;

                float midValue = math.max(0.0001f, chemoDepositAmount);
                float maxValue = math.max(midValue, trailMaxDeposit);

                if (enemyV <= midValue)
                {
                    float k = math.saturate(enemyV / midValue);
                    trailColor = math.lerp(enemyWeakColor, enemyMidColor, k);
                }
                else
                {
                    float range = math.max(0.0001f, maxValue - midValue);
                    float k = math.saturate((enemyV - midValue) / range);
                    trailColor = math.lerp(enemyMidColor, enemyStrongColor, k);
                }

                float light01 = math.saturate(enemyV / maxValue);
                float brightness = math.lerp(0.5f, 1.5f, light01);
                trailColor.xyz *= brightness;

                // if both exist, enemy overwrites player visually
                c.xyz = trailColor.xyz;

                // alpha stores TRAIL MASK ONLY
                c.w = 1f;
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


  
   

