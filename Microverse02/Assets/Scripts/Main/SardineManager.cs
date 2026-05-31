using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;

public class SardineManager : MonoBehaviour
{
    [Header("Sardine Spawn")]
    [SerializeField] GameObject playerSpawn;
    [SerializeField] int sardineCount = 200;
    [SerializeField] int maxSardineCount = 100000;
    [SerializeField] float sardineSpeed = 1.5f;

    [Header("Enemy")]
    int lastEnemySpawnStep = 0;
    [SerializeField] float agentsLastSeconds = 10f;
    [SerializeField] float enemyMinLifetime = 10f;
    [SerializeField] float enemyMaxLifetime = 25f;
    [SerializeField] float enemyLifetimeScoreMax = 30000f;
    float spawnTimer = 0f;

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

    //for tutorial

    [SerializeField]  bool playerSpawnStart;
    [SerializeField] bool enemySpawnStart = true;
    [SerializeField] bool isMainMenu = false;
    bool playerAteFood;
    public void SetPlayerSpawnStart(bool value)
    {
        playerSpawnStart = value;
    }
    public void SetEnemySpawnStart(bool value)
    {
        enemySpawnStart = value;
    }
    public int CellCount => sardines.Count;
    public bool IsDead(int i) => sardines[i].isDead;
    public Vector2 GetPos(int i) => sardines[i].currentPos;
    public float GetRadius(int i) => sardines[i].cellRadius;



    [Header("Sardines")]
    List<BacteriaData> sardines = new List<BacteriaData>();

    

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
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;

        if (playerSpawnStart)
        {
            SpawnOnePlayer();
        }
    }
    public void SpawnOnePlayer()
    {
        if (playerSpawn == null) return;
        if (HasAlivePlayer()) return;

        CreateSardine(playerSpawn.transform.position, BacteriaData.Team.Player);
    }
    void Update()
    {
        float dt = Time.deltaTime;

        DepositBacteriaField();
        bool updated = OceanFieldManager.TickField(dt);

        if (enemySpawnStart)
        {
            spawnTimer += dt;
            float currentInterval = GetEnemySpawnInterval(OceanFieldManager.Score);

            if (spawnTimer >= currentInterval)
            {
                spawnTimer = 0f;

                int amountToSpawn = 1 + (OceanFieldManager.Score / 15000);

                for (int j = 0; j < amountToSpawn; j++)
                {
                    CreateSardine(GetRandomPositionInMap(), BacteriaData.Team.Enemy);
                }
            }
        }

        if (updated)
        {
            OceanFieldManager.UpdateTrailTexture();
        }

    
        ApplyBacteriaFieldSteering();


        for (int i = 0; i < sardines.Count; i++)
        {
            BacteriaData c = sardines[i];
            if (c.isDead) continue;

            c.nextPos = c.currentPos + c.currentVelocity * dt;
            c.nextVelocity = c.currentVelocity;

            if (mapManager != null && mapManager.IsWallWorld(c.nextPos))
            {
                c.nextPos = c.currentPos;
                c.nextVelocity = -c.currentVelocity * wallBounciness;
            }

            sardines[i] = c;
            ApplyRectangleBoundary(i);

            c = sardines[i];
            c.currentPos = c.nextPos;
            c.currentVelocity = c.nextVelocity;
            sardines[i] = c;
        }

        //for DEBUGGING hmm...

        if (Input.GetKeyDown(KeyCode.Q))
            {
                sardineSpeed = 40f;
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                sardineSpeed = 3.6f;
            }
            else if (Input.GetKeyDown(KeyCode.N))
            {
                int planktonNum = OceanFieldManager.GetTotalPlankton();
                Debug.Log("Sardines Count: " + sardines.Count + ", Plankton Count: " + planktonNum);
            }

    }
    float GetEnemySpawnInterval(int score)
    {

        float difficultyFactor = Mathf.Max(5f, 120f - (score / 23f));
        return difficultyFactor / 10f;
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
            c.lifeTimer = (team == BacteriaData.Team.Enemy) ? GetCurrentEnemyLifetime() : agentsLastSeconds;

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
            lifeTimer = (team == BacteriaData.Team.Enemy) ? GetCurrentEnemyLifetime() : agentsLastSeconds
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
            // PLAYER
            ///////////////////////////////
            if (c.team == BacteriaData.Team.Player)
            {
                c.lifeTimer -= dt;

                float enemyTrail = OceanFieldManager.SampleEnemyTrailOnly(c.currentPos);

          
                if (enemyTrail >= OceanFieldManager.trailMaxDeposit*0.5f)
                {
                    c.isDead = true;
                    c.currentVelocity = Vector2.zero;
                    c.nextVelocity = Vector2.zero;
                    sardines[i] = c;
                    deadSardinePool.Add(i);


                    if (sardines.Count < maxSardineCount)
                    {

                        CreateSardine(c.currentPos, BacteriaData.Team.Enemy); // I think its better to keep it annotation
                    }
                    CreateSardine(c.currentPos, BacteriaData.Team.Enemy);
                    continue; 
                }

                // Player reproduction by eating

                bool atePlankton = OceanFieldManager.EatPlanktonAt(c.currentPos, 1);
                if (atePlankton)
                {
                    playerAteFood = true;
                }
                if (atePlankton && playerSpawnStart && sardines.Count < maxSardineCount)
                {
                    c.lifeTimer = agentsLastSeconds;
                    CreateSardine(c.currentPos, BacteriaData.Team.Player);
                }
                if (c.lifeTimer <= 0f)
                {
                    c.isDead = true;
                    c.currentVelocity = Vector2.zero;
                    c.nextVelocity = Vector2.zero;
                    sardines[i] = c;
                    deadSardinePool.Add(i);
                    continue;
                }
            }
            ///////////////////////////////
            // ENEMY
            ///////////////////////////////
            else
            {
                c.lifeTimer -= dt;

                if(isMainMenu)
                {
                    bool atePlankton = OceanFieldManager.EatPlanktonAt(c.currentPos, 1, false);

                    if (atePlankton)
                    {

                        c.lifeTimer = agentsLastSeconds;

                        if (sardines.Count < maxSardineCount)
                        {
                            CreateSardine(c.currentPos, BacteriaData.Team.Enemy);
                        }
                    }
                }

                float playerTrail = OceanFieldManager.SamplePlayer(c.currentPos);


                //Player can kill Enemies?: I don't think so...

                //if (playerTrail >= OceanFieldManager.trailMaxDeposit * 0.5f)
                //{
                //    c.isDead = true;
                //    c.currentVelocity = Vector2.zero;
                //    c.nextVelocity = Vector2.zero;
                //    sardines[i] = c;
                //    deadSardinePool.Add(i);
                //    continue;
                //}

                if (c.lifeTimer <= 0f)
                {
                    c.isDead = true;
                    c.currentVelocity = Vector2.zero;
                    c.nextVelocity = Vector2.zero;

                    sardines[i] = c;
                    deadSardinePool.Add(i);
                    continue;
                }
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
                float enemyForward = OceanFieldManager.SampleEnemyTrailOnly(forwardPos);
                float enemyLeft = OceanFieldManager.SampleEnemyTrailOnly(leftPos);
                float enemyRight = OceanFieldManager.SampleEnemyTrailOnly(rightPos);

                float playerForward = OceanFieldManager.SamplePlayerTrailOnly(forwardPos);
                float playerLeft = OceanFieldManager.SamplePlayerTrailOnly(leftPos);
                float playerRight = OceanFieldManager.SamplePlayerTrailOnly(rightPos);

                float playerTrailFollowWeight = 1.5f; 

                forwardValue = enemyForward + playerForward * playerTrailFollowWeight;
                leftValue = enemyLeft + playerLeft * playerTrailFollowWeight;
                rightValue = enemyRight + playerRight * playerTrailFollowWeight;
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
            // FINAL VELO // double buffer-like hmm....
            ////////////////////////////////
            Vector2 newDir = Vector2.Lerp(forward, desiredDir, turnRate * dt).normalized;

            float speed = sardineSpeed;
      

            
                float t = Mathf.Clamp01(OceanFieldManager.Score / 33000f);
                float speedMultiplier = Mathf.Lerp(1f, 3.3f, t);

                speed *= speedMultiplier;
            

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
    float GetCurrentEnemyLifetime()
    {
        float t = Mathf.Clamp01(OceanFieldManager.Score / enemyLifetimeScoreMax);
        return Mathf.Lerp(enemyMinLifetime, enemyMaxLifetime, t);
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

    public bool HasAlivePlayer()
    {
        for (int i = 0; i < sardines.Count; i++)
        {
            if (sardines[i].isDead) continue;
            if (sardines[i].team == BacteriaData.Team.Player)
                return true;
        }

        return false;
    }

    public bool HasPlayerEatFood() //for tutorial
    {
        if (!playerAteFood) return false;
        playerAteFood = false;
        return true;
    }
    public bool IsAnyPlayerFollowingDraw(float threshold = 5f) //for tutorial
    {
        if (OceanFieldManager == null) return false;

        for (int i = 0; i < sardines.Count; i++)
        {
            BacteriaData c = sardines[i];
            if (c.isDead) continue;
            if (c.team != BacteriaData.Team.Player) continue;

            float drawValue = OceanFieldManager.SampleDraw(c.currentPos);

            if (drawValue >= threshold)
                return true;
        }

        return false;
    }
}