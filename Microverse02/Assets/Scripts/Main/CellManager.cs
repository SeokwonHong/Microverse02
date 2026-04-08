using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;

public class CellManager : MonoBehaviour
{
    [Header("Bacteria Spawn")]
    [SerializeField] GameObject playerSpawn;
    [SerializeField] GameObject enemySpawn;
    [SerializeField] float bacteriaSpawnInterval = 0.0001f;
    float bacteriaSpawnTimer = 0f;
    [SerializeField] int bacteriaCount = 200;
    [SerializeField] float bacteriaSpeed = 1.5f;

    [Header("Map generation")]
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] float wallBounciness = 0.5f;

    [Header("Map")]
    [SerializeField] MapManager mapManager;

    [Header("Bacteria Pooling")]
    readonly List<int> deadBacteriaPool = new List<int>(128);

    [SerializeField] float wanderTimer;
    [SerializeField] float wanderRate;

    [SerializeField] float drawChemicalSpeedBoost = 5f;
    [SerializeField] float drawChemicalSpeedSensitivity = 1f;

    public int CellCount => cells.Count;
    public bool IsDead(int i) => cells[i].isDead;
    public Vector2 GetPos(int i) => cells[i].currentPos;
    public float GetRadius(int i) => cells[i].cellRadius;
    public Team GetTeam(int i) => cells[i].team;

    [Header("Cells")]
    List<BacteriaData> cells = new List<BacteriaData>();

    [Header("Game Values")]
    public float ReproductionEnergy = 0;
    public float SystemStability = 0;

    public enum Team
    {
        Player,
        Enemy
    }

    struct BacteriaData
    {
        public Vector2 currentPos;
        public Vector2 currentVelocity;

        public Vector2 nextPos;
        public Vector2 nextVelocity;

        public float headingTimer;
        public float wanderAngle;
        public float cellRadius;

        public bool isDead;
        public Team team;
    }

    void Awake()
    {
  

        Vector2 spawnPos1 = playerSpawn.transform.position;
        Vector2 spawnPos2 = enemySpawn.transform.position;

        for (int i = 0; i < bacteriaCount / 2; i++)
        {
            CreateBacteriaCell(spawnPos1, Team.Player);
            //CreateBacteriaCell(spawnPos2, Team.Enemy);
        }

        bacteriaCount = 0;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        DepositBacteriaField();

        bool updated = bacteriaFieldManager.TickField(dt);
        if (updated)
        {
            bacteriaFieldManager.UpdateTrailTexture();
        }

        ApplyBacteriaFieldSteering();

        for (int i = 0; i < cells.Count; i++)
        {
            BacteriaData c = cells[i];
            if (c.isDead) continue;

            c.nextPos = c.currentPos;
            c.nextVelocity = c.currentVelocity;

            c.nextPos += c.nextVelocity * dt;

            if (mapManager != null && mapManager.IsWallWorld(c.nextPos))
            {
                c.nextPos = c.currentPos;
                c.nextVelocity *= -wallBounciness;
            }

            cells[i] = c;

            ApplyRectangleBoundary(i);
            c = cells[i];

            if (!IsFinite(c.nextPos) || !IsFinite(c.nextVelocity))
            {
                c.nextPos = c.currentPos;
                c.nextVelocity = Vector2.zero;
            }

            c.currentVelocity = c.nextVelocity;
            c.currentPos = c.nextPos;

            cells[i] = c;
        }


        if (ReproductionEnergy < 0f)
            ReproductionEnergy = 0f;

        

        if(Input.GetKeyDown(KeyCode.Q))
        {
            bacteriaSpeed = 80f;
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            bacteriaSpeed = 3.6f;
        }

    }

    void ApplyRectangleBoundary(int i)
    {
        if (mapManager == null) return;

        BacteriaData c = cells[i];

        Vector2 p = c.nextPos;
        Vector2 v = c.nextVelocity;

        Vector2 centre = mapManager.MapCentre;
        Vector2 size = mapManager.MapSize;

        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        float minX = centre.x - halfWidth + c.cellRadius;
        float maxX = centre.x + halfWidth - c.cellRadius;
        float minY = centre.y - halfHeight + c.cellRadius;
        float maxY = centre.y + halfHeight - c.cellRadius;

        bool hitX = false;
        bool hitY = false;

        if (p.x < minX)
        {
            p.x = minX;
            hitX = true;
        }
        else if (p.x > maxX)
        {
            p.x = maxX;
            hitX = true;
        }

        if (p.y < minY)
        {
            p.y = minY;
            hitY = true;
        }
        else if (p.y > maxY)
        {
            p.y = maxY;
            hitY = true;
        }

        if (hitX) v.x = -v.x * wallBounciness;
        if (hitY) v.y = -v.y * wallBounciness;

        c.nextPos = p;
        c.nextVelocity = v;
        cells[i] = c;
    }

    static bool IsFinite(Vector2 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y));
    }

    void CreateBacteriaCell(Vector2 pos, Team team)
    {
        if (deadBacteriaPool.Count > 0)
        {
            int idx = deadBacteriaPool[^1];
            deadBacteriaPool.RemoveAt(deadBacteriaPool.Count - 1);

            BacteriaData c = cells[idx];
            c.isDead = false;
            c.currentPos = pos;
            c.nextPos = pos;
            c.currentVelocity = Vector2.zero;
            c.nextVelocity = Vector2.zero;
            c.cellRadius = 0.3f;
            c.headingTimer = 0f;
            c.wanderAngle = 0f;
            c.team = team;

            cells[idx] = c;
            return;
        }

        BacteriaData clone = new BacteriaData
        {
            currentPos = pos,
            nextPos = pos,
            currentVelocity = Vector2.zero,
            nextVelocity = Vector2.zero,
            headingTimer = 0f,
            wanderAngle = 0f,
            cellRadius = 0.3f,
            isDead = false,
            team = team
        };

        cells.Add(clone);
    }

    Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
    }

    void DepositBacteriaField()
    {
        if (bacteriaFieldManager == null) return;

        for (int i = 0; i < cells.Count; i++)
        {
            BacteriaData c = cells[i];
            if (c.isDead) continue;

            bacteriaFieldManager.DepositTrail(c.currentPos, c.team);
        }
    }

    void ApplyBacteriaFieldSteering()
    {
        if (bacteriaFieldManager == null) return;

        float dt = Time.deltaTime;
        float turnRate = 18f;
        float turnThreshold = 0.0015f;

        for (int i = 0; i < cells.Count; i++)
        {
            BacteriaData c = cells[i];
            if (c.isDead) continue;

            Vector2 forward = c.currentVelocity.sqrMagnitude > 0.0001f
                ? c.currentVelocity.normalized
                : Random.insideUnitCircle.normalized;

            Vector2 leftDir = Rotate(forward, -bacteriaFieldManager.SensorAngle);
            Vector2 rightDir = Rotate(forward, bacteriaFieldManager.SensorAngle);

            Vector2 forwardPos = c.currentPos + forward * bacteriaFieldManager.SensorDistance;
            Vector2 leftPos = c.currentPos + leftDir * bacteriaFieldManager.SensorDistance;
            Vector2 rightPos = c.currentPos + rightDir * bacteriaFieldManager.SensorDistance;

            float forwardValue;
            float leftValue;
            float rightValue;

            if (c.team == Team.Player)
            {
                forwardValue = bacteriaFieldManager.SamplePlayer(forwardPos);
                leftValue = bacteriaFieldManager.SamplePlayer(leftPos);
                rightValue = bacteriaFieldManager.SamplePlayer(rightPos);
            }
            else
            {
                forwardValue = bacteriaFieldManager.SampleEnemy(forwardPos);
                leftValue = bacteriaFieldManager.SampleEnemy(leftPos);
                rightValue = bacteriaFieldManager.SampleEnemy(rightPos);
            }

            Vector2 desiredDir = forward;

            if (leftValue > forwardValue + turnThreshold && leftValue > rightValue + turnThreshold)
            {
                desiredDir = leftDir;
            }
            else if (rightValue > forwardValue + turnThreshold && rightValue > leftValue + turnThreshold)
            {
                desiredDir = rightDir;
            }
            else
            {
                c.headingTimer -= dt;

                if (c.headingTimer <= 0f)
                {
                    c.wanderAngle = Random.Range(-16f, 16f);
                    c.headingTimer = Random.Range(0.15f, 0.35f);
                    //c.headingTimer = 0.15f;
                }

                desiredDir = Rotate(forward, c.wanderAngle);
            }

            Vector2 newDir = Vector2.Lerp(forward, desiredDir, turnRate * dt).normalized;

            float draw01 = 0f;
            if (c.team == Team.Player)
            {
                float drawValue = bacteriaFieldManager.SampleDraw(c.currentPos);
                draw01 = Mathf.Clamp01(drawValue * drawChemicalSpeedSensitivity);
            }

            float speed = bacteriaSpeed * Mathf.Lerp(1f, drawChemicalSpeedBoost, draw01);
            c.currentVelocity = newDir * speed;

            cells[i] = c;
        }
    }

   
}