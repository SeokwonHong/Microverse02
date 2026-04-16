using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;

public class SardineManager : MonoBehaviour
{
    [Header("Sardine Spawn")]
    [SerializeField] GameObject playerSpawn;
    [SerializeField] int sardineCount = 200;
    [SerializeField] int maxSardineCount = 10000;
    [SerializeField] float sardineSpeed = 1.5f;

    [Header("Enemy")]
    [SerializeField] int enemySpawnScoreStep = 10;
    int lastEnemySpawnStep = 0;

    [Header("Map generation")]
    [SerializeField] OceanFieldManager OceanFieldManager;
    [SerializeField] float wallBounciness = 0.5f;

    [Header("Map")]
    [SerializeField] MapManager mapManager;

    [Header("Sardine Pooling")]
    readonly List<int> deadSardinePool = new List<int>(128);

    [SerializeField] float wanderTimer;
    [SerializeField] float wanderRate;

    [SerializeField] float drawChemicalSpeedBoost = 5f;
    [SerializeField] float drawChemicalSpeedSensitivity = 1f;

    public int CellCount => sardines.Count;
    public bool IsDead(int i) => sardines[i].isDead;
    public Vector2 GetPos(int i) => sardines[i].currentPos;
    public float GetRadius(int i) => sardines[i].cellRadius;

    [Header("Sardines")]
    List<BacteriaData> sardines = new List<BacteriaData>();

    [Header("Game Values")]
    public float ReproductionEnergy = 0;
    public float SystemStability = 0;


    struct BacteriaData
    {
        public enum Team
        {
            Player,
            Enemy

        }
        public Team team;

        public Vector2 currentPos;
        public Vector2 currentVelocity;

        public Vector2 nextPos;
        public Vector2 nextVelocity;

        public float headingTimer;
        public float wanderAngle;
        public float cellRadius;

        public bool isDead;

        public float lifeTimer; //only for enemy
    }

    void Awake()
    {
  

        Vector2 spawnPos1 = playerSpawn.transform.position;

        for (int i = 0; i < sardineCount; i++)
        {
            CreateSardine(spawnPos1, BacteriaData.Team.Player);

        }
        CreateSardine(GetRandomPositionInMap(), BacteriaData.Team.Enemy);


        sardineCount = 0;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        DepositBacteriaField();

        bool updated = OceanFieldManager.TickField(dt);
        if (updated)
        {
            OceanFieldManager.UpdateTrailTexture();
        }

        ApplyBacteriaFieldSteering();

        //int currentSpawnStep = OceanFieldManager.Score / enemySpawnScoreStep;

        //if (currentSpawnStep > lastEnemySpawnStep)
        //{
        //    int amountToSpawn = currentSpawnStep - lastEnemySpawnStep;

        //    for (int i = 0; i < amountToSpawn; i++)
        //    {
        //        CreateSardine(GetRandomPositionInMap(), BacteriaData.Team.Enemy);
        //    }

        //    lastEnemySpawnStep = currentSpawnStep;
        //}

        for (int i = 0; i < sardines.Count; i++)
        {
            BacteriaData c = sardines[i];
            if (c.isDead) continue;

            c.nextPos = c.currentPos;
            c.nextVelocity = c.currentVelocity;

            c.nextPos += c.nextVelocity * dt;

            if (mapManager != null && mapManager.IsWallWorld(c.nextPos))
            {
                c.nextPos = c.currentPos;
                c.nextVelocity *= -wallBounciness;
            }

            sardines[i] = c;

            ApplyRectangleBoundary(i);
            c = sardines[i];

            if (!IsFinite(c.nextPos) || !IsFinite(c.nextVelocity))
            {
                c.nextPos = c.currentPos;
                c.nextVelocity = Vector2.zero;
            }

            c.currentVelocity = c.nextVelocity;
            c.currentPos = c.nextPos;

            sardines[i] = c;
        }


        if (ReproductionEnergy < 0f)
            ReproductionEnergy = 0f;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            sardineSpeed = 40f;
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            sardineSpeed = 3.6f;
        }
        else if(Input.GetKeyDown(KeyCode.N))
        {
           int planktonNum = OceanFieldManager.GetTotalPlankton();
            Debug.Log("Sardines Count: " + sardines.Count + ", Plankton Count: " + planktonNum);
        }

    }

    void ApplyRectangleBoundary(int i)
    {
        if (mapManager == null) return;

        BacteriaData c = sardines[i];

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
        sardines[i] = c;
    }

    static bool IsFinite(Vector2 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y));
    }

    void CreateSardine(Vector2 pos, BacteriaData.Team team)
    {
        if (deadSardinePool.Count > 0)
        {
            int idx = deadSardinePool[^1];
            deadSardinePool.RemoveAt(deadSardinePool.Count - 1);

            BacteriaData c = sardines[idx];
            c.team = team;
            c.isDead = false;
            c.currentPos = pos;
            c.nextPos = pos;
            c.currentVelocity = Vector2.zero;
            c.nextVelocity = Vector2.zero;
            c.cellRadius = 0.6f;
            c.headingTimer = 0f;
            c.wanderAngle = 0f;
            c.lifeTimer = 0f;

            sardines[idx] = c;
            return;
        }

        BacteriaData clone = new BacteriaData
        {
            team = team,
            currentPos = pos,
            nextPos = pos,
            currentVelocity = Vector2.zero,
            nextVelocity = Vector2.zero,
            headingTimer = 0f,
            wanderAngle = 0f,
            cellRadius = 0.6f,
            isDead = false,
            lifeTimer = 0f
        };

        sardines.Add(clone);
    }
    Vector2 GetRandomPositionInMap()
    {
        Vector2 centre = mapManager.MapCentre;
        Vector2 size = mapManager.MapSize;

        for (int tries = 0; tries < 50; tries++)
        {
            float x = Random.Range(centre.x - size.x * 0.5f, centre.x + size.x * 0.5f);
            float y = Random.Range(centre.y - size.y * 0.5f, centre.y + size.y * 0.5f);

            Vector2 p = new Vector2(x, y);

            if (!mapManager.IsWallWorld(p))
                return p;
        }

        return centre;
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
        if (OceanFieldManager == null) return;

        for (int i = 0; i < sardines.Count; i++)
        {
            BacteriaData c = sardines[i];
            if (c.isDead) continue;

            if (c.team == BacteriaData.Team.Player)
                OceanFieldManager.DepositTrail(c.currentPos);
            else
                OceanFieldManager.DepositEnemyTrail(c.currentPos);
        }
    }

    void ApplyBacteriaFieldSteering()
    {
        if (OceanFieldManager == null) return;

        float dt = Time.deltaTime;
        float turnRate = 18f;
        float turnThreshold = 0.0015f;

        for (int i = 0; i < sardines.Count; i++)
        {
            BacteriaData c = sardines[i];
            if (c.isDead) continue;

            ///////////////////////////////
            // PLAYER: die on max enemy trail, eat + reproduce
            ///////////////////////////////
            if (c.team == BacteriaData.Team.Player)
            {
                float enemyTrail = OceanFieldManager.SampleEnemyTrailOnly(c.currentPos);

          
                if (enemyTrail >= OceanFieldManager.trailMaxDeposit)
                {
                    c.isDead = true;
                    c.currentVelocity = Vector2.zero;
                    c.nextVelocity = Vector2.zero;
                    sardines[i] = c;
                    deadSardinePool.Add(i);

         
                    if (sardines.Count < maxSardineCount)
                    {
                     
                        CreateSardine(c.currentPos, BacteriaData.Team.Enemy);
                    }

                    continue; 
                }

                // Player reproduction by eating (keep this if you want players to grow)
                bool atePlankton = OceanFieldManager.EatPlanktonAt(c.currentPos, 1);
                if (atePlankton && sardines.Count < maxSardineCount)
                {
                    CreateSardine(c.currentPos, BacteriaData.Team.Player);
                }
            }
            ///////////////////////////////
            // ENEMY: reproduce on strong player trail
            ///////////////////////////////
            else
            {
                c.lifeTimer -= dt;

                //float playerTrail = OceanFieldManager.SamplePlayerTrailOnly(c.currentPos);

                //if (playerTrail >= OceanFieldManager.trailMaxDeposit && c.lifeTimer <= 0f)
                //{
                //    if (sardines.Count < maxSardineCount)
                //    {
                //        CreateSardine(c.currentPos, BacteriaData.Team.Enemy);
                //        c.lifeTimer = 0.5f;
                //    }
                //}
            }

            ////////////////////////////////
            // SENSOR DIRECTIONS
            ////////////////////////////////
            Vector2 forward = c.currentVelocity.sqrMagnitude > 0.0001f
                ? c.currentVelocity.normalized
                : Random.insideUnitCircle.normalized;

            Vector2 leftDir = Rotate(forward, -OceanFieldManager.SensorAngle);
            Vector2 rightDir = Rotate(forward, OceanFieldManager.SensorAngle);

            Vector2 forwardPos = c.currentPos + forward * OceanFieldManager.SensorDistance;
            Vector2 leftPos = c.currentPos + leftDir * OceanFieldManager.SensorDistance;
            Vector2 rightPos = c.currentPos + rightDir * OceanFieldManager.SensorDistance;

            float forwardValue;
            float leftValue;
            float rightValue;

            ////////////////////////////////
            // SAMPLING
            ////////////////////////////////
            if (c.team == BacteriaData.Team.Player)
            {
                forwardValue = OceanFieldManager.SamplePlayer(forwardPos);
                leftValue = OceanFieldManager.SamplePlayer(leftPos);
                rightValue = OceanFieldManager.SamplePlayer(rightPos);
            }
            else
            {
                forwardValue = OceanFieldManager.SampleEnemyTrailOnly(forwardPos);
                leftValue = OceanFieldManager.SampleEnemyTrailOnly(leftPos);
                rightValue = OceanFieldManager.SampleEnemyTrailOnly(rightPos);
            }

            ////////////////////////////////
            // STEERING DECISION
            ////////////////////////////////
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
                }

                desiredDir = Rotate(forward, c.wanderAngle);
            }

            ////////////////////////////////
            // FINAL VELOCITY
            ////////////////////////////////
            Vector2 newDir = Vector2.Lerp(forward, desiredDir, turnRate * dt).normalized;

            float speed = sardineSpeed;

            if (c.team == BacteriaData.Team.Player)
            {
                float drawValue = OceanFieldManager.SampleDraw(c.currentPos);
                float draw01 = Mathf.Clamp01(drawValue * drawChemicalSpeedSensitivity);
                speed = sardineSpeed * Mathf.Lerp(1f, drawChemicalSpeedBoost, draw01);
            }

            c.currentVelocity = newDir * speed;

            sardines[i] = c;
        }
    }

    public void RemoveCellsInRadius(Vector2 worldPos, float radius)
    {
        float radiusSqr = radius * radius;

        for (int i = 0; i < sardines.Count; i++)
        {
            BacteriaData c = sardines[i];
            if (c.isDead) continue;


            Vector2 delta = c.currentPos - worldPos;
            if (delta.sqrMagnitude > radiusSqr)
                continue;

            c.isDead = true;
            c.currentVelocity = Vector2.zero;
            c.nextVelocity = Vector2.zero;

            sardines[i] = c;
            deadSardinePool.Add(i);
        }
    }
}