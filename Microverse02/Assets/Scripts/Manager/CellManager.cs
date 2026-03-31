using System;
using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;
using System.Security.Cryptography;
using static CellManager;
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
    Vector2 mapCentre = Vector2.zero;
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] float wallBounciness = 0.5f;

    [Header("Map")]
    [SerializeField] MapManager mapManager;

    [Header("Bacterias Pooling")]
    readonly List<int> deadBacteriaPool = new List<int>(128);


    [Header("Destination")]
    [SerializeField] GameObject refToDestination;
    float DestinationRadius = 2f;
    int arrivedBacteriaCount = 0;

    [SerializeField] float drawChemicalSpeedBoost = 5f;
    [SerializeField] float drawChemicalSpeedSensitivity = 1f;

    //GPU instancing
    public enum CellRole {Bacteria}

    public int CellCount =>cells.Count;
    public bool IsDead(int i) => cells[i].isDead;
    public Vector2 GetPos(int i) => cells[i].currentPos;
    public float GetRadius(int i) => cells[i].cellRadius;
    public float GetDetectRadius(int i) => cells[i].detectRadius;
    public CellRole GetRole(int i) => cells[i].role;
    public int GetOrganismId(int i) => cells[i].organismId;
    public Team GetTeam(int i) => cells[i].team;

    [Header ("cells  |  organisms")]
    List<Cell> cells = new List<Cell>();


    [Header("Game Values")]
    public float ReproductionEnergy = 0;
    public float SystemStability = 0;

    public enum Team
    {
        Player,
        Enemy
    }

    class Cell
    {
        public Vector2 currentPos;
        public Vector2 currentVelocity;

        public Vector2 nextPos;
        public Vector2 nextVelocity;

        public float cellRadius;
        public float detectRadius;

        public int organismId; // -1 = indipendent cell
        public CellRole role; // Core / Shell / WhiteBlood

        public bool detected;
        public bool isDead;

        // bacteria
        public bool isBacteriaAttachedToWBC;
        public float energy;

        // WBC
        public bool WBCInSight;
        
        //cell movement
        public Vector2 heading;
        public float headingTimer;
        public float wanderAngle;
        public Vector2 cohesionDV;

        public Team team;
    }


    void Awake()
    {
        refToDestination.transform.localScale = new Vector3(DestinationRadius * 2f, DestinationRadius * 2f, 1f);


        Vector2 spawnPos1 = playerSpawn.transform.position;
        Vector2 spawnPos2 = enemySpawn.transform.position;

        for (int i = 0; i < bacteriaCount/2; i++)
        {
            CreateBacteriaCell(spawnPos1, Team.Player);
            CreateBacteriaCell(spawnPos2,Team.Enemy);
        }

        bacteriaCount = 0;

    }

    void Update()
    {
        float dt = Time.deltaTime;


        // 0) Double buffer start
        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            c.nextVelocity = c.currentVelocity; 
            c.nextPos = c.currentPos;
            c.detected = false;
            c.isBacteriaAttachedToWBC = false;

            c.cohesionDV = Vector2.zero;

            cells[i] = c;
        }

        //bactera rules
        DepositBacteriaField();

        bool updated = bacteriaFieldManager.TickField(Time.deltaTime);
        if (updated)
        {
            bacteriaFieldManager.UpdateTrailTexture();
        }
        ApplyBacteriaFieldSteering();


        // 7) Map boundary + end buffer

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            if(c.isDead) continue;

            c.nextPos += c.nextVelocity * dt;

            if (mapManager != null && mapManager.IsWallWorld(c.nextPos))
            {
                c.nextPos = c.currentPos;
                c.nextVelocity *= -wallBounciness;
            }

            cells[i] = c;
           
            ApplyRectangleBoundary(i);
            c =cells[i];

            if(!IsFinite(c.nextPos)||!IsFinite(c.nextVelocity))
            {
                c.nextPos = c.currentPos;
                c.nextVelocity = Vector2.zero;
            }

            c.currentVelocity = c.nextVelocity;
            c.currentPos = c.nextPos;
            cells[i] = c;


        }
        DestinationDetectoin();

        if (ReproductionEnergy <= 0) ReproductionEnergy = 0;


        if (Input.GetKeyDown(KeyCode.V))
        {

            Debug.Log(arrivedBacteriaCount);
        }
    }

    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    /// 




    #region Map



    void ApplyRectangleBoundary(int i)
    {
        if (mapManager == null) return;

        Cell c = cells[i];

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

    #endregion

    #region Input



    static bool IsFinite(Vector2 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y));
    }


    #endregion



    void CreateBacteriaCell(Vector2 pos, Team team)
    {
        if (deadBacteriaPool.Count > 0)
        {
            int idx = deadBacteriaPool[^1];
            deadBacteriaPool.RemoveAt(deadBacteriaPool.Count - 1);

            Cell c = cells[idx];

            c.isDead = false;
            c.isBacteriaAttachedToWBC = false;

            c.currentPos = pos;
            c.nextPos = pos;
            c.currentVelocity = Vector2.zero;
            c.nextVelocity = Vector2.zero;

            c.organismId = -1;
            c.role = CellRole.Bacteria;
            c.team = team;

            c.cellRadius = 0.3f;
            c.detectRadius = c.cellRadius * 13f;
            c.detected = true;
            c.cohesionDV = Vector2.zero;

            cells[idx] = c;
            return;
        }

        Cell clone = new Cell();

        clone.currentPos = pos;
        clone.nextPos = pos;
        clone.currentVelocity = Vector2.zero;
        clone.nextVelocity = Vector2.zero;

        clone.cellRadius = 0.3f;
        clone.detectRadius = clone.cellRadius * 13f;

        clone.organismId = -1;
        clone.role = CellRole.Bacteria;
        clone.team = team;


        clone.detected = true;
        clone.isDead = false;
        clone.isBacteriaAttachedToWBC = false;
        clone.cohesionDV = Vector2.zero;

        cells.Add(clone);
    }

 
    Vector2 Rotate(Vector2 v, float degrees)  // makes normalized vector2 with applied degrees
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
    }

    void DepositBacteriaField() // use the Deposit() funtion per bacteria
    {
        if (bacteriaFieldManager == null) return;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            if (c.isDead) continue;
            if (c.role != CellRole.Bacteria) continue;

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
            Cell c = cells[i];
            if (c.isDead) continue;
            if (c.role != CellRole.Bacteria) continue;

            Vector2 forward = c.nextVelocity.sqrMagnitude > 0.0001f
                ? c.nextVelocity.normalized
                : Random.insideUnitCircle.normalized;

            Vector2 leftDir = Rotate(forward, -bacteriaFieldManager.SensorAngle);
            Vector2 rightDir = Rotate(forward, bacteriaFieldManager.SensorAngle);

            Vector2 forwardPos = c.currentPos + forward * bacteriaFieldManager.SensorDistance;
            Vector2 leftPos = c.currentPos + leftDir * bacteriaFieldManager.SensorDistance;
            Vector2 rightPos = c.currentPos + rightDir * bacteriaFieldManager.SensorDistance;

            float forwardValue = bacteriaFieldManager.Sample(forwardPos);
            float leftValue = bacteriaFieldManager.Sample(leftPos);
            float rightValue = bacteriaFieldManager.Sample(rightPos);

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
                    c.wanderAngle = Random.Range(-8f, 8f);
                    c.headingTimer = Random.Range(0.15f, 0.35f);
                }

                desiredDir = Rotate(forward, c.wanderAngle);
            }

            Vector2 newDir = Vector2.Lerp(forward, desiredDir, turnRate * dt).normalized;

            float drawValue = bacteriaFieldManager.SampleDraw(c.currentPos);
            float draw01 = Mathf.Clamp01(drawValue * drawChemicalSpeedSensitivity);
            float speed = bacteriaSpeed * Mathf.Lerp(1f, drawChemicalSpeedBoost, draw01);

            c.nextVelocity = newDir * speed;

            cells[i] = c;
        }
    }
    public void DestinationDetectoin()
    {
        Vector2 dest = refToDestination.transform.position;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell bacteria = cells[i];

            if (bacteria.isDead) continue;
            if (bacteria.role != CellRole.Bacteria) continue;

            Vector2 d = dest - bacteria.currentPos;

            if (d.sqrMagnitude < DestinationRadius * DestinationRadius)
            {
                bacteria.isDead = true;
                cells[i] = bacteria;
                deadBacteriaPool.Add(i);
                arrivedBacteriaCount++;
            }
        }
    }


    


}


