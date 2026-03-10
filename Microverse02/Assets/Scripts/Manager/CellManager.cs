using System;
using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;
using System.Security.Cryptography;
public class CellManager : MonoBehaviour
{
    [Header("Defalut Settings")]
    [SerializeField] int firstOrganismCount;
    public int maxOrganismCount = 100;

    [Header("Bacteria Spawn")]
    [SerializeField] GameObject reftoBacteriaSpawnPos;
    [SerializeField] float bacteriaSpawnInterval = 1f;
    float bacteriaSpawnTimer = 0f;
    [SerializeField] int bacteriaCount = 200;
    [SerializeField] float bacteriaSpeed = 1.5f;

    [Header("Map generation")]
    Vector2 mapCentre = Vector2.zero;
    [SerializeField] float mapRadius = 25f;
    public GameObject refToBg;
    [SerializeField] float wallBounciness = 0.5f;
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;


    [Header("Spatial Hash")]
    SpatialHash spatialHash;
    [SerializeField] float BoxSize = 1.25f;
    readonly List<int> neighbourBuffer = new List<int>(128);
    readonly List<int> nearestBacteriaBuffer = new List<int>(128);

    [Header("Wall")]
    [SerializeField] WallManager wallManager;
    readonly List<int> wallBuffer = new List<int>(32);

    [Header("Bacterias Pooling")]
    readonly List<int> deadBacteriaPool = new List<int>(128);

    [Header("Organism Death")]
    bool isOrganismDead = false;
    const float maxDeadTime = 20f;

    //GPU instancing
    public enum CellRole { Bacteria, Core, Shell, WhiteBlood}

    public int CellCount =>cells.Count;
    public bool IsDead(int i) => cells[i].isDead;
    public Vector2 GetPos(int i) => cells[i].currentPos;
    public float GetRadius(int i) => cells[i].cellRadius;
    public float GetDetectRadius(int i) => cells[i].detectRadius;
    public CellRole GetRole(int i) => cells[i].role;
    public int GetOrganismId(int i) => cells[i].organismId;


    [Header ("cells  |  organisms")]
    List<Cell> cells = new List<Cell>();
    List<Organisms> organisms = new List<Organisms>();


    [Header("Game Values")]
    public float ReproductionEnergy = 0;
    public float SystemStability = 0;


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
    }

    class Organisms
    {
        public int id;
        public int coreIndex;
        public List<int> members = new List<int>(32);
        public float coreDistance; // distance between Core and Shell
        public float defaultCoreDistance;

        public Vector2 heading; 
        public float headingPower; 
        public bool anchorEnabled; //anchor holds cells: structure is destroied once its dead

        public float wanderTimer;

        public float energy;
        public float wbcCooldown;

        public bool isDead;
        public float deadTimer;
        public bool attackedByBacteria;
    }

    void Awake()
    {
        spatialHash = new SpatialHash(BoxSize);

        if(refToBg !=null) refToBg.transform.localScale = new Vector3(mapRadius * 2f, mapRadius * 2f, 1);

        float minX = -mapRadius;
        float maxX = mapRadius;
        float minY = -mapRadius;
        float maxY = mapRadius;


        for (int i = 0; i < firstOrganismCount; i++)
        {
            Vector2 pos = new Vector2(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(minY, maxY)
            );
            CreateOrganism(pos);
        }

        SystemStability = 100f;

    }
    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    void Update()
    {
        float dt = Time.deltaTime;

        bacteriaSpawnTimer += dt;

        if (bacteriaSpawnTimer >= bacteriaSpawnInterval && bacteriaCount>0)
        {
            bacteriaSpawnTimer -= bacteriaSpawnInterval;

            Vector2 spawnPos = reftoBacteriaSpawnPos.transform.position;
            CreateBacteriaCell(spawnPos);
            bacteriaCount--;
        }
        if(bacteriaCount <= 0) bacteriaCount = 0;

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
        for(int i =0; i<organisms.Count;  i++)
        {
            Organisms org = organisms[i];
            org.attackedByBacteria = false;
            organisms[i] = org;
        }

        // 1) Spatial Hash
        spatialHash.BeginFrame(); 
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].isDead) continue;
            spatialHash.Insert(cells[i].nextPos, i);
        }

        // 2) Pair Detection & Interaction
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].isDead) continue;

            spatialHash.Query(cells[i].nextPos, neighbourBuffer);

            for (int n = 0; n < neighbourBuffer.Count; n++) // QQuery will give this the index id. Once it's sent, it will be replaced to next one right after
            {
                int otherIndex = neighbourBuffer[n];
                if (otherIndex <= i) continue;

                if (cells[otherIndex].isDead) continue;

                ResolveOverlap(i, otherIndex);
                ApplyCellPushing(i, otherIndex);
                ApplyBacteriaAttackingOrganism(i, otherIndex);
            }
        }


        // 4) WBC
        ApplyWBCAttachingWBCEnergy();
        ApplyWBCDamageBacteria();

        // 5) Organism constraints
        ApplyKeepOrganismShape();

        // 6) Cell rules
        ApplyCellOrganismEnergyDeath();

        //bactera rules
        DepositOrganismField();
        DepositBacteriaField();

        if (bacteriaFieldManager != null)
            bacteriaFieldManager.TickField(Time.deltaTime);

        ApplyBacteriaFieldSteering();
  

        ApplyCellWiggling();
        ApplyDragToCells();
        //ApplyEmitWBCFromOrganism();


        // 7) Map boundary + end buffer

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            if(c.isDead) continue;

            c.nextPos += c.nextVelocity * dt;

            if (wallManager != null)
            {
                wallManager.ResolveCircleAgainstNearbyWalls(
                    ref c.nextPos,
                    ref c.nextVelocity,
                    c.cellRadius,
                    wallBounciness,
                    wallBuffer,
                    out _
                );
            }

            cells[i] = c;
            ApplyCircleBoundary(i);
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
        //ApplyOrganismReproduction();
        ApplyOrganismDeath();
        UpdateDeadOrganisms();
        CountOrganismNum();

        if (ReproductionEnergy <= 0) ReproductionEnergy = 0;


        if (Input.GetKeyDown(KeyCode.V))
        {
            int organismCount = CountOrganismNum();
            Debug.Log($" {cells.Count}, {organismCount}");
        }
    }



    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    /// 




    #region Map
    void ApplyCircleBoundary(int i)
    {
        Cell c = cells[i];

        Vector2 p = c.nextPos;
        Vector2 v = c.nextVelocity;

        Vector2 to = p - mapCentre;
        float dist = to.magnitude;

        float allowed = mapRadius - c.cellRadius;
        if (dist <= allowed || dist < 1e-6f) return;

        Vector2 n = to / dist;
        c.nextPos = mapCentre + n * allowed;

        float vn = Vector2.Dot(v, n);
        if (vn > 0f)
        {
            v = v - 2f * vn * n;
            v *= wallBounciness;
            c.nextVelocity = v;

        }
        cells[i] = c;
    }

    #endregion

    #region Input



    static bool IsFinite(Vector2 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y));
    }


    #endregion

    #region Create

    void CreateBacteriaCell(Vector2 pos)
    {
        //reuse pool
        if (deadBacteriaPool.Count > 0)
        {
            int idx = deadBacteriaPool[^1];
            deadBacteriaPool.RemoveAt(deadBacteriaPool.Count - 1);

            Cell c = cells[idx];

            c.isDead = false;
            c.energy = 2f;
            c.isBacteriaAttachedToWBC = false;

            c.currentPos = pos;
            c.nextPos = pos;
            c.currentVelocity = Vector2.zero;
            c.nextVelocity = Vector2.zero;

            c.organismId = -1;
            c.role = CellRole.Bacteria;

            c.cellRadius = 0.15f;
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

        clone.cellRadius = 0.15f;
        clone.detectRadius = clone.cellRadius * 13f;

        clone.organismId = -1;
        clone.role = CellRole.Bacteria;

        clone.energy = 2f;
        clone.detected = true;
        clone.isDead = false;
        clone.isBacteriaAttachedToWBC = false;

        clone.cohesionDV = Vector2.zero;

        cells.Add(clone);
    }

    void CreateWBCCell(Vector2 pos)
    {
        Cell w = new Cell();
        w.currentPos = pos;
        w.nextPos = w.currentPos;
        w.currentVelocity = Vector2.zero;
        w.nextVelocity = Vector2.zero;

        w.cellRadius = 0.15f;
        w.detectRadius = w.cellRadius * 8f;

        w.energy = 1f;

        w.organismId = -1;
        w.role = CellRole.WhiteBlood;

        cells.Add(w);
    }
    void CreateOrganism(Vector2 currentPos)
    {
        Organisms org = new Organisms();

        org.id = organisms.Count;

        //core 
        Cell core = new Cell();
        core.currentPos = currentPos;
        core.currentVelocity = Vector2.zero;
        core.nextVelocity = Vector2.zero;
        core.energy = 8f;

        org.energy = UnityEngine.Random.Range(1f, 4f);
        float energy2 = Mathf.InverseLerp(1f, 10f, org.energy);
        core.cellRadius = Mathf.Lerp(0.25f, 0.3f, energy2);
        int shellCount = Mathf.RoundToInt(Mathf.Lerp(20f, 30f, energy2));

        core.detectRadius = core.cellRadius * 5f;


        org.coreDistance = core.detectRadius;
        org.defaultCoreDistance = org.coreDistance;


        core.organismId = org.id;
        core.role = CellRole.Core;

        int coreIndex = cells.Count;
        cells.Add(core);

        org.coreIndex = coreIndex;
        org.members.Add(coreIndex);
        
        //Shell
        float shellRadius = Mathf.Lerp(0.1f, 0.12f, energy2);
        for (int i = 0; i < shellCount; i++)
        {
            float angle = (Mathf.PI * 2f) * (i / (float)shellCount); //(Mathf.PI * 2f) 는 각도로 이해 * 그걸 비율로 슬라이스
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); //각도를 normalized 된 벡터값으로 바꿈 그걸 해주는게 x 축인 cos , y 축인 sin
            //+ 계산하기 편하게 사분면에 표현가능하게 단위화
            Vector2 pos = currentPos + dir * org.coreDistance;

            Cell shell = new Cell();
            shell.currentPos = pos;
            shell.nextPos = pos;
            shell.currentVelocity = Vector2.zero;
            shell.nextVelocity = Vector2.zero;
            shell.energy = 4f;

            //이부분부터 프로퍼티화해야할듯.
            shell.cellRadius = shellRadius;
            shell.detectRadius = shell.cellRadius * 4.5f;

            shell.organismId = org.id;
            shell.role = CellRole.Shell;

            int shellIndex = cells.Count;
            cells.Add(shell);

            org.members.Add(shellIndex);
        }

        organisms.Add(org);
    }
    #endregion

    #region Cell_Constraint
    void ResolveOverlap(int CurrentIndex, int OtherIndex) //basic colliding 
    {
        Cell currentCell = cells[CurrentIndex];
        Cell otherCell = cells[OtherIndex];

        Vector2 delta = otherCell.nextPos - currentCell.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 <= 0f) return;

        float minDist = currentCell.cellRadius + otherCell.cellRadius;
        float minDist2 = minDist * minDist;
        if (d2 >= minDist2) return;

        float dist = Mathf.Sqrt(d2); //get distance
        Vector2 direction = delta / dist; //get the direction by dividing the vector by its distance     vector/distance = distance

        float overlap = minDist - dist;
        Vector2 push = direction * (overlap * 0.5f); // push(power*direction) * half of the 

        currentCell.nextPos -= push;
        otherCell.nextPos += push;

        cells[CurrentIndex] = currentCell;
        cells[OtherIndex] = otherCell;
    }


    void ApplyCoreAnchor() //apply core cell an anchor
    {
        for(int i =0; i<organisms.Count; i++)
        {
            var org = organisms[i]; 
            if(!org.anchorEnabled) continue;

            int coreIdx = org.coreIndex;
            if (coreIdx < 0 || coreIdx>= cells.Count) continue;

            Cell core = cells[coreIdx];
            core.nextVelocity = Vector2.zero;
            cells[coreIdx] = core;  
        }
    }


    void ApplyOrganismTendency() //Organism movement, more likely tendency
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < organisms.Count; i++)
        {
            Organisms org = organisms[i];
            if (org.isDead) continue;

            int coreIdx = org.coreIndex;
            if (coreIdx < 0) continue;

            Cell core = cells[coreIdx];

            if (org.heading.sqrMagnitude < 1e-6f)
            {
                org.heading = Random.insideUnitCircle.normalized;
                org.wanderTimer = Random.Range(0.5f, 2.0f);
            }

            org.wanderTimer -= dt;
            if (org.wanderTimer <= 0f)
            {
                Vector2 jitter = Random.insideUnitCircle * 0.25f;
                org.heading = (org.heading + jitter).normalized;

                org.wanderTimer = Random.Range(0.5f, 2.0f);
            }

            float speed = 30f;


            core.nextVelocity += org.heading * speed * dt;
            cells[coreIdx] = core;
            organisms[i] = org;
        }
    }



    void ApplyKeepOrganismShape()
    {
        float dt = Time.deltaTime;

        float tolerance = 0.2f;
        float c = 1.1f;     // damping
        float maxForce = 40f;

        for (int i = 0; i < organisms.Count; i++)
        {

            var org = organisms[i];
            if (org.isDead) continue;

            int coreIdx = org.coreIndex;
            if (coreIdx < 0 || coreIdx >= cells.Count) continue;

            Cell core = cells[coreIdx];
            float coreDist = org.coreDistance;

            float massCore = Mathf.Max(0.001f, core.cellRadius * core.cellRadius);

            //float k = (org.playerInside == 1) ? 150f : 10f; // spring
            float k = (org.attackedByBacteria == true) ? 200f : 70f; // spring
            // apply to shells only (members excluding core)
            for (int m = 0; m < org.members.Count; m++)
            {
                int shellIdx = org.members[m];
                if (shellIdx == coreIdx) continue;
                if (shellIdx < 0 || shellIdx >= cells.Count) continue;

                Cell shell = cells[shellIdx];
                if (shell.organismId != core.organismId) continue; // safety

                Vector2 delta = shell.nextPos - core.nextPos;
                float d2 = delta.sqrMagnitude;
                if (d2 < 1e-8f) continue;

                float dist = Mathf.Sqrt(d2);
                Vector2 dir = delta / dist;

                float shellGap = dist - coreDist;
                if (Mathf.Abs(shellGap) < tolerance) continue;

                float relVelAlongDir = Vector2.Dot(shell.nextVelocity - core.nextVelocity, dir);

                float force = (-k * shellGap) - (c * relVelAlongDir);
                force = Mathf.Clamp(force, -maxForce, maxForce);

                float massShell = Mathf.Max(0.001f, shell.cellRadius * shell.cellRadius);
                float invSum = 1f / (massCore + massShell);

                float coreShare = massShell * invSum;
                float shellShare = massCore * invSum;

                core.nextVelocity -= dir * (force * coreShare) * dt;
                shell.nextVelocity += dir * (force * shellShare) * dt;

                cells[shellIdx] = shell; // write-back (Cell is a struct)
            }

            cells[coreIdx] = core; // write-back
        }
    }



    void ApplyCellPushing(int currentIndex, int otherIndex)
    {
        Cell a = cells[currentIndex];
        Cell b = cells[otherIndex];

        if (a.role == CellRole.Bacteria || b.role == CellRole.Bacteria) return; //---
        if (a.role == CellRole.WhiteBlood || b.role == CellRole.WhiteBlood) return;
        if(a.role == CellRole.Core || b.role == CellRole.Core) return;

        Vector2 delta = b.nextPos - a.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float dist = Mathf.Sqrt(d2);
        float maxDist = a.detectRadius + b.cellRadius;

        float overlap = maxDist - dist;
        if (overlap <= 0f) return;

        Vector2 dir = delta / dist;

        float dt = Time.deltaTime;
        float pushStrength = 180f;

        Vector2 dv = dir * (overlap * pushStrength);

        bool sameOrg = (a.organismId >= 0 && a.organismId ==b.organismId);

        if (sameOrg)
        {
            if(a.role == CellRole.Core && b.role == CellRole.Shell)
            {
                b.nextVelocity += dv * dt;
                cells[otherIndex] = b;
                return;
            }
            if (b.role == CellRole.Core && a.role == CellRole.Shell)
            {
                a.nextVelocity -= dv * dt;
                cells[currentIndex] = a;
                return;
            }

        }

        a.nextVelocity -= dv * dt;
        b.nextVelocity += dv * dt;

        cells[currentIndex] = a;
        cells[otherIndex] = b;
    }

    void ApplyCellOrganismEnergyDeath()
    {
        for(int i=0; i<cells.Count; i++)
        {
            Cell c = cells[i];
            if(c.isDead) continue;

            if(c.energy<=0f)
            {
                c.isDead = true;
            }
            cells[i] = c;
        }

        for(int i = 0; i<organisms.Count; i++)
        {
            if(organisms[i].isDead) continue;

            if(organisms[i].energy<=0f)
            {
                KillEachCellInsideOrganism(i);
            }
        }
    }
    void KillEachCellInsideOrganism(int orgId)
    {
        if (orgId < 0 || orgId >= organisms.Count) return;

        var org = organisms[orgId];
        bool alreadyDead = org.isDead;

        org.isDead = true;
        org.anchorEnabled = false;
        org.heading = Vector2.zero;
        org.headingPower = 0f;

        if (!alreadyDead)
        {
            org.deadTimer = 0f;
        }

        for (int m = 0; m < org.members.Count; m++)
        {
            int cellIdx = org.members[m];
            if (cellIdx < 0 || cellIdx >= cells.Count) continue;

            Cell c = cells[cellIdx];
            c.isDead = true;

            cells[cellIdx] = c;
        }

        organisms[orgId] = org;
    }
    void ApplyCohesion(int aIndex, int bIndex)  //cell gathering method
    {
        float dt = Time.deltaTime;

        // only if player involved (optional)
        Cell A = cells[aIndex];
        Cell B = cells[bIndex];
        if (A.role != CellRole.Core && B.role != CellRole.Core) return;

        Vector2 delta = B.nextPos - A.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float cohesionRadius = 2.0f;
        if (d2 > cohesionRadius * cohesionRadius) return;

        float dist = Mathf.Sqrt(d2);
        Vector2 dir = delta / dist;

        float minDist = A.cellRadius + B.cellRadius;

        // IMPORTANT: don't apply cohesion near contact
        float start = minDist * 10f;       // tune
        if (dist <= start) return;

        // spring accel
        float k = 1.2f;
        float accel = k * (dist - start);

        float maxAccel = 2f;
        accel = Mathf.Min(accel, maxAccel);

        Vector2 dv = dir * (accel * dt);

        // per-cell cohesion dv cap (this is the key)
        float maxCohesionDV = 0.3f; // tune (units/sec change per frame)
        Vector2 aNew = A.cohesionDV + dv;
        Vector2 bNew = B.cohesionDV - dv;

        if (aNew.sqrMagnitude > maxCohesionDV * maxCohesionDV)
            dv = Vector2.ClampMagnitude(dv, Mathf.Max(0f, maxCohesionDV - A.cohesionDV.magnitude));

        if ((B.cohesionDV - dv).sqrMagnitude > maxCohesionDV * maxCohesionDV)
            dv = Vector2.ClampMagnitude(dv, Mathf.Max(0f, maxCohesionDV - B.cohesionDV.magnitude));

        // apply
        A.nextVelocity += dv;
        B.nextVelocity -= dv;

        A.cohesionDV += dv;
        B.cohesionDV -= dv;

        cells[aIndex] = A;
        cells[bIndex] = B;
    }

    void ApplyDragToCells()
    {
        float dt = Time.deltaTime;
        float baseDrag = 10f;
        float minRadius = 0.05f;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            if (c.isDead) continue;

            float r = Mathf.Max(minRadius, c.cellRadius);
            float drag = baseDrag * (r * 3);

            c.nextVelocity *= Mathf.Exp(-drag * dt);
            cells[i] = c;
        }
    }
    void ApplyOrganismJelly() //apply this to organisms instead of ApplyKeepDistance()?? 
    {
        float dt = Time.deltaTime;
        float k = 0f;     // spring strength
        float c = 1.1f;     // damping
        float maxPenetration = 0.35f;
        float maxAccel = 900f;

        // Loop through all cells and apply jelly only to the roles you want
        for (int i = 0; i < cells.Count; i++)
        {
            Cell target = cells[i];


            if (target.role==CellRole.Core)
            {
                k = 5;
            }
            else
            {
                k = 50f;
            }

               // k = isPlayer ? 300f : 5f;

            if (target.isDead) continue;

            // Only push these roles
            if (target.role != CellRole.Bacteria && target.role != CellRole.WhiteBlood && target.role != CellRole.Core) continue;


            Vector2 totalAccel = Vector2.zero;

            for (int o = 0; o < organisms.Count; o++)
            {
                var org = organisms[o];
                if (org.isDead) continue;

                // Optional: don’t push cells that are part of this organism
                if (target.organismId == o) continue;

                Cell core = cells[org.coreIndex];


                float barrier=1f;
                if(target.role==CellRole.WhiteBlood || target.role == CellRole.Bacteria)
                {
                    barrier = org.coreDistance + target.cellRadius;
                }
                else barrier= org.coreDistance + target.detectRadius;

                Vector2 delta = target.nextPos - core.nextPos;
                float d2 = delta.sqrMagnitude;
                if (d2 < 1e-6f) continue;

                float dist = Mathf.Sqrt(d2);

                float penetration = barrier - dist;
                //if (penetration > 0f) continue; // add this line if I don't want to apply shell cells jelly force when they're inside of core radisu
                if (penetration <= 0f) continue;  // remove this line if I don't want to apply shell cells jelly force when they're inside of core radisu

                if (penetration > maxPenetration) penetration = maxPenetration;

                Vector2 n = delta / dist;

                float v_n = Vector2.Dot(target.nextVelocity - core.nextVelocity, n);

                float accelMag = (k * penetration) - (c * v_n);
                if (accelMag <= 0f) continue;

                totalAccel += n * accelMag;
            }

            // Clamp accel
            float a2 = totalAccel.sqrMagnitude;
            float maxA2 = maxAccel * maxAccel;
            if (a2 > maxA2)
                totalAccel = totalAccel * (maxAccel / Mathf.Sqrt(a2));

            
            target.nextVelocity += totalAccel * dt;

            cells[i] = target;
        }
    }

    void ApplyCellDetection(int a, int b)
    {
        Cell A = cells[a];
        Cell B = cells[b];

        if (A.role == CellRole.Bacteria && B.role != CellRole.Bacteria)
        {
            float r = A.detectRadius + B.cellRadius;
            if ((A.nextPos - B.nextPos).sqrMagnitude <= r * r)
            {
                B.detected = true;
                cells[b] = B;
            }
            else B.detected = false;
        }
        else if (B.role == CellRole.Bacteria && A.role != CellRole.Bacteria)
        {
            float r = B.detectRadius + A.cellRadius;
            if ((B.nextPos - A.nextPos).sqrMagnitude <= r * r)
            {
                A.detected = true;
                cells[a] = A;
            }
            
        }
    }
    #endregion

    #region Cell_Rules

    void ApplyCellMovement()
    {
        float dt = Time.deltaTime;
        float accel = 40f;

        for(int i = 0;i < cells.Count;i++)
        {
            Cell c = cells[i];
            if (c.isDead) continue;
            if(c.role == CellRole.Shell) continue;

            if(c.heading.sqrMagnitude <1e-6f)
            {
                c.heading = Random.insideUnitCircle.normalized;
                c.headingTimer = Random.Range(0.6f, 1.4f);
            }

            c.headingTimer -= dt;

            if(c.headingTimer <=0f)
            {
                Vector2 jitter = Random.insideUnitCircle * 0.5f;
                c.heading = (c.heading +jitter).normalized;
                c.headingTimer = Random.Range(0.6f, 1.4f);
            }

            c.nextVelocity += c.heading * accel * dt;
            cells[i] = c;
        }
        
    }



    void ApplyCellWiggling()// cell tendency
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            if (c.isDead) continue;

            Vector2 randomDir = Random.insideUnitCircle;
            if (randomDir.sqrMagnitude < 1e-6f) continue;

            float speed;


            if (c.role == CellRole.WhiteBlood)
            {
                speed = 60f;
            }

            // ORGANISM WIGGLE
            else if (c.organismId >= 0 && c.organismId < organisms.Count)
            {
                Organisms org = organisms[c.organismId];
                float energy01 = Mathf.InverseLerp(0f, 10f, org.energy);
                float baseSpeed = Mathf.Lerp(1f, 30f, energy01);

                float t = Mathf.Clamp01(org.deadTimer / maxDeadTime);
                speed = Mathf.Lerp(baseSpeed,0f,t);
                

                if (org.attackedByBacteria)
                {
                    speed = Mathf.Lerp(20f, 0f, t);
                    org.coreDistance = org.defaultCoreDistance ;
                }
                else
                {
                    speed = Mathf.Lerp(10f, 0f, t);
                    org.coreDistance = org.defaultCoreDistance * 1.1f;
                }
                organisms[c.organismId] = org;
            }
            else
            {
                continue;
            }
            c.nextVelocity += randomDir * speed * dt;

            cells[i] = c;
        }
    }


    #endregion

    #region Organism 


    void ApplyOrganismReproduction()
    {
        float dt = Time.deltaTime;

        int organismCount = CountOrganismNum();
        if (organismCount > maxOrganismCount) return;

        for (int i = 0; i < organisms.Count; i++)
        {
            if (CountOrganismNum() >= maxOrganismCount) return;

            Organisms org = organisms[i];
            if (org.isDead) continue;

            org.energy += dt;

            if (org.energy >= 8)
            {
                org.energy -=8f;

                if (org.coreIndex >= 0 && org.coreIndex < cells.Count)
                {
                    Vector2 pos = cells[org.coreIndex].currentPos;
                    CreateOrganism(pos);
                }
            }
            organisms[i] = org;
        }
    }

    void ApplyOrganismDeath() //function when the orgarnism is dead
    {
        int organismCount = CountOrganismNum();
        if (!isOrganismDead) return;

        for(int i=0; i<organismCount; i++)
        {
            if (organisms[i].isDead) continue;
            KillEachCellInsideOrganism(i);
        }
    }

    void UpdateDeadOrganisms()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < organisms.Count; i++)
        {
            var org = organisms[i];
            if (!org.isDead) continue;

            org.deadTimer = Mathf.Min(maxDeadTime, org.deadTimer+dt);
            organisms[i] = org;
        }
    }

    void ApplyBacteriaAttackingOrganism(int a, int b)
    {
        float dt = Time.deltaTime;

        Cell A = cells[a];
        Cell B = cells[b];

        if (A.isDead || B.isDead) return;

        bool AisBacteria = (A.role == CellRole.Bacteria && A.organismId == -1);
        bool BisBacteria = (B.role == CellRole.Bacteria && B.organismId == -1);

        if(!AisBacteria && !BisBacteria) return;

        int bacteriaIndex = AisBacteria ? a:b;
        int targetIndex = AisBacteria ? b : a;

        Cell bacteria = cells[bacteriaIndex];
        Cell target = cells[targetIndex];

        if (target.role != CellRole.Shell && target.role != CellRole.Core) return;

        int organismId = target.organismId;
        if (organismId < 0 || organismId >= organisms.Count) return;

        Vector2 delta = target.nextPos - bacteria.nextPos;
        float d2 = delta.sqrMagnitude;
        float sqrDist = delta.sqrMagnitude;
        float detect = bacteria.detectRadius;
        if(sqrDist<=detect*detect)
        {
            //Attach
            float dist = Mathf.Sqrt(sqrDist);
            if (dist <= 0.0001f) return;
            Vector2 dir = delta / dist;
            float chaseForce = 10f;
            bacteria.nextVelocity += dir * chaseForce * dt;

            //Energy Sucking
            float suckDist = bacteria.cellRadius + target.cellRadius;
            if (d2 <= suckDist * suckDist)
            {
                const float suckPerSecond = 0.1f;
                float want = suckPerSecond * dt;
                Organisms org = organisms[organismId];
                org.attackedByBacteria = true;

                float taken = Mathf.Min(want, org.energy);
                org.energy -= taken;
                bacteria.energy += taken;
                ReproductionEnergy += taken;
                organisms[organismId] = org;
            }
            
            cells[bacteriaIndex] = bacteria;
        }

        
    }
    void ApplyEmitWBCFromOrganism()
    {
        float dt = Time.deltaTime;
        const float interval = 1.3f;

        for (int i = 0; i < organisms.Count; i++)
        {
            Organisms o = organisms[i];
            if (o.isDead) continue;

            o.wbcCooldown -= dt;

            if (o.wbcCooldown <= 0f &&
                o.attackedByBacteria &&
                o.coreIndex >= 0 && o.coreIndex < cells.Count &&
                o.energy >= 1f)
            {
                Cell core = cells[o.coreIndex];
                CreateWBCCell(core.currentPos);

                o.energy -= 0.3f;
                o.wbcCooldown = interval;
            }

            organisms[i] = o;
        }
    }

    #endregion

    int FindNearestBacteriaIndex(Vector2 pos, float maxRange)
    {
        float bestD2 = maxRange * maxRange;
        int bestIdx = -1;

        nearestBacteriaBuffer.Clear();
        spatialHash.Query(pos, nearestBacteriaBuffer);

        for (int k = 0; k < nearestBacteriaBuffer.Count; k++)
        {
            int j = nearestBacteriaBuffer[k];
            Cell c = cells[j];
            if (c.role != CellRole.Bacteria) continue;
            if (c.isDead) continue;

            Vector2 d = c.nextPos - pos;
            float d2 = d.sqrMagnitude;

            if (d2 < bestD2)
            {
                bestD2 = d2;
                bestIdx = j;
            }
        }

        return bestIdx;
    }
    #region WBC Constraints


    void ApplyWBCAttachingWBCEnergy()
    {
        float dt = Time.deltaTime;

        float attractStrength = 8f;
        float drag = 30f;

        for (int i = 0; i<cells.Count; i++)
        {
            if (cells[i].role != CellRole.Bacteria) continue; 
            Cell p = cells[i];
            p.isBacteriaAttachedToWBC = false;
            cells[i] = p;    
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].role != CellRole.WhiteBlood) continue;

            Cell w = cells[i];
            if(w.isDead ) continue; 

            int targetIdx = FindNearestBacteriaIndex(w.nextPos, w.detectRadius);

            
            if (targetIdx < 0)
            {
                w.energy -= dt;
                w.nextVelocity *= Mathf.Exp(-drag * dt);
                cells[i] = w;
                continue;
            }
            if (cells[targetIdx].isDead) continue;
            Cell bacteria = cells[targetIdx];

            Vector2 delta = bacteria.nextPos - w.nextPos;
            float d2 = delta.sqrMagnitude;

            float r = w.detectRadius;

            w.WBCInSight = (d2 <= r * r) && (d2 > bacteria.cellRadius*bacteria.cellRadius);
            

            if (w.WBCInSight)
            {
                w.energy += 5f*dt;
                float dist = Mathf.Sqrt(d2);
                Vector2 dir = delta / dist;

                float force = (r - dist) / r;
                w.nextPos += dir * (force * attractStrength) * dt;
            }
            else
            {
                w.energy -= 1*dt;
            }
            if (w.energy >= 5f)
            {
                w.energy = 5f;
            }

            float attachDist = bacteria.cellRadius + w.cellRadius;
            bool isAttachedToBacteria = (d2 <= (attachDist * attachDist) * 1.5f);

            if (isAttachedToBacteria)
            {
                bacteria.isBacteriaAttachedToWBC = true;
            }

            cells[i] = w;
            cells[targetIdx] = bacteria;
        }


    }

    void ApplyWBCDamageBacteria()
    {
        float dt = Time.deltaTime;
        float damagePerSecond = 1f;
        
        for(int i = 0; i<cells.Count; i++)
        {
            Cell p = cells[i];
            if (p.role != CellRole.Bacteria) continue;
            if(p.isDead) continue;

            if(p.isBacteriaAttachedToWBC)
            {
                p.energy = Mathf.Max(p.energy - damagePerSecond *dt, 0);
            }

            if(p.energy <= 0 && !p.isDead)
            {
                p.energy = 0;
                p.isDead = true;
                deadBacteriaPool.Add(i);
            }

            cells[i] = p;
        }
    }

    void ApplyWBCWandering()
    {
        float dt = Time.deltaTime;

        float maxSpeed = 30f;
        float drag = 2.0f;

        float wanderCircleDist = 1.2f;   // how far ahead the circle is
        float wanderCircleRadius = 0.9f; // how wide it can turn
        float wanderJitter = 2.5f;       // how quickly angle changes (radians/sec)
        float steerStrength = 20f;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell w = cells[i];
            if (w.role != CellRole.WhiteBlood) continue;
            if (w.WBCInSight) continue;

            // forward direction from velocity (fallback if almost stopped)
            Vector2 forward = w.nextVelocity.sqrMagnitude > 0.001f
                ? w.nextVelocity.normalized
                : Random.insideUnitCircle.normalized;

            // slowly vary the wander angle
            w.wanderAngle += Random.Range(-1f, 1f) * wanderJitter * dt;

            // point on a circle in front of the agent
            Vector2 circleCenter = forward * wanderCircleDist;
            Vector2 displacement = new Vector2(Mathf.Cos(w.wanderAngle), Mathf.Sin(w.wanderAngle)) * wanderCircleRadius;

            Vector2 desiredDir = (circleCenter + displacement).normalized;
            Vector2 desiredVel = desiredDir * maxSpeed;

            Vector2 steer = (desiredVel - w.nextVelocity) * steerStrength;
            w.nextVelocity += steer * dt;

            // drag + clamp
            w.nextVelocity *= Mathf.Exp(-drag * dt);
            float speed = w.nextVelocity.magnitude;
            if (speed > maxSpeed) w.nextVelocity *= (maxSpeed / speed);

            cells[i] = w;
        }
    }
    #endregion

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
    void DepositOrganismField()
    {
        if (bacteriaFieldManager == null) return;

        for (int i = 0; i < organisms.Count; i++)
        {
            Organisms org = organisms[i];
            if (org.isDead) continue;

            for (int m = 0; m < org.members.Count; m++)
            {
                int cellIdx = org.members[m];
                if (cellIdx < 0 || cellIdx >= cells.Count) continue;

                Cell c = cells[cellIdx];
                if (c.isDead) continue;

                Vector2 pos = c.currentPos;
                bacteriaFieldManager.DepositOrganism(pos);
            }
        }
    }
    void DepositBacteriaField() // use the Deposit() funtion per bacteria
    {
        if (bacteriaFieldManager == null) return;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            if (c.isDead) continue;
            if (c.role != CellRole.Bacteria) continue;

            bacteriaFieldManager.Deposit(c.currentPos);
        }
    }
    void ApplyBacteriaFieldSteering() //makes bacteria to move(steer) based on the chemical field
    {
        if (bacteriaFieldManager == null) return;

        float dt = Time.deltaTime;

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

            if (leftValue > forwardValue && leftValue > rightValue)
                desiredDir = leftDir;
            else if (rightValue > forwardValue && rightValue > leftValue)
                desiredDir = rightDir;

            c.nextVelocity += bacteriaSpeed* desiredDir * bacteriaFieldManager.SteerStrength * dt;
            cells[i] = c;
        }
    }


    public int CountOrganismNum()
    {
        int count = 0;  
        for(int i = 0; i<organisms.Count; i++)
        {
            if (!organisms[i].isDead) count++;
        }

        return count;
    }

    //gpu instancing

    public bool IsOrganismDead(int i)
    {
        int oid = cells[i].organismId;
        return (oid >= 0 && oid < organisms.Count && organisms[oid].isDead);
    }
    public float GetOrganismEnergy(int organismId)
    {
        return organisms[organismId].energy;
    }

    public bool IsLevelWin()
    {
        int cellCount = CountOrganismNum();
        if (organisms.Count ==0) return false;

        if (cellCount == 0) return true;

        return false;
    }


    


}


