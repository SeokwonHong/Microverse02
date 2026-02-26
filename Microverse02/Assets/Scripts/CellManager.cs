using System;
using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;
public class CellManager : MonoBehaviour
{
    [Header("Defalut Settings")]
    public int organismCount = 20;
    public int WBCCount = 10;

    [Header("Map generation")]
    [SerializeField] Vector2 mapCentre = Vector2.zero;
    [SerializeField] float mapRadius = 25f;
    public GameObject refToBg;
    [SerializeField] float wallBounciness = 0.5f;

    [Header("Mouse and Player")]
    float mousePlayerDistance;
    float playerSpeed;

    [Header("Bacteria - WBC Reproduce Timer")]
    float reproduceTimer = 0;
    float reproduceInterval = 0.01f;
    float WBCSpawnTimer;

    [Header("Player")]
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] float threshold = 17f;
    private float playerRadius;
    private float playerInfluenceRadius;
    int playerCellIndex = -1; //-1 means player not allocated yet. If player is made, int number will be allocated
    public float playerPushStrength;

    [Header("Spatial Hash")]
    SpatialHash spatialHash;
    [SerializeField] float BoxSize = 1.25f;
    readonly List<int> neighbourBuffer = new List<int>(128);
    readonly List<int> nearestPlayerBuffer = new List<int>(128);

    [Header("Players Pooling")]
    readonly List<int> deadPlayerPool = new List<int>(128);

    [Header("Organism Death")]
    bool isOrganismDead = false;
    const float maxDeadTime = 20f;

    [Header("GPU instancing")]
    [SerializeField] GameObject cellDebugPrefeb;
    readonly List<SpriteRenderer> debugRenderers = new();

    [Header ("cells  |  organisms")]
    List<Cell> cells = new List<Cell>();
    List<Organisms> organisms = new List<Organisms>();

    [Header("Game Values")]
    public float ReproductionEnergy = 0;
    public float OrganismLeftCount = 0;
    public float SystemStability = 0;

    enum CellRole { Player, Core, Shell, WhiteBlood }

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

        // players
        public bool isPlayerAttachedToWBC;
        public float hp;

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

        public float lifespan;

        public bool isDead;
        public float deadTimer;
        public bool playerInside;
    }

    void Awake()
    {
        spatialHash = new SpatialHash(BoxSize);

        if(refToBg !=null) refToBg.transform.localScale = new Vector3(mapRadius * 2f, mapRadius * 2f, 1);

        CreatePlayerCell(Vector2.zero);
        playerRadius = GetPlayerRadius();
        playerInfluenceRadius = playerRadius * 10f;

        float minX = -mapRadius;
        float maxX = mapRadius;
        float minY = -mapRadius;
        float maxY = mapRadius;


        for (int i = 0; i < organismCount; i++)
        {
            Vector2 pos = new Vector2(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(minY, maxY)
            );
            CreateOrganism(pos);
        }

        ReproductionEnergy = 1000;
        OrganismLeftCount = organismCount;
        SystemStability = 100f;

        //GPU
        debugRenderers.Capacity = cells.Count;
        for (int i = 0; i < cells.Count; i++)
        {
            GameObject go = Instantiate(cellDebugPrefeb, transform);
            debugRenderers.Add(go.GetComponent<SpriteRenderer>());
        }
    }
    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
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
            c.isPlayerAttachedToWBC = false;

            c.cohesionDV = Vector2.zero;

            cells[i] = c;
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
                ApplyCellDetection(i, otherIndex);
                ApplyCellPushing(i, otherIndex);
                //ApplyCohesion(i, otherIndex);   
            }
        }

        // 3) Player input + functions
        ApplyPlayerInput();
        ApplyPlayerFunctions();

        // 4) WBC
        ApplyWBCAttaching();
        ApplyWBCDamagePlayer();

        // 5) Organism constraints
        ApplyCoreAnchor();
        for (int iter = 0; iter < 2; iter++) // play iter times in one frame
        {
            ApplyOrganismJelly(Time.fixedDeltaTime);
            ApplyKeepOrganismShape();
        }

        // 6) Cell rules
        ApplyPlayerKillsOrganism();
        ApplyCellWiggling();
        ApplyPlayerHeadToOrganism(dt);

        ApplyDragToCells();

        ApplyEmitWBCFromOrganism();
        //ApplyCellMovement();
        //ApplyOrganismTendency();


        // 7) Map boundary + end buffer

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            if(c.isDead) continue;

            c.nextPos += c.nextVelocity * dt;

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

        ApplyOrganismDeath();
        UpdateDeadOrganisms();

        if (Input.GetKeyDown(KeyCode.V))
        {
            Debug.Log($" {cells.Count}, playerCellIndex = {playerCellIndex}");
            ApplyOrganismReproduction();
        }
    }

    void FixedUpdate()
    {
        if (playerCellIndex < 0) return;

        if (Input.GetMouseButton(0))
        {
            if (ReproductionEnergy <= 0) return;

            Cell player = cells[playerCellIndex];

            reproduceTimer += Time.fixedDeltaTime;

            if(reproduceTimer>=reproduceInterval)
            {
                reproduceTimer = 0f;
                ReproductionEnergy -= 1;
                CreatePlayerCell(player.currentPos);
            }
        }
    }
    void EnsureDebugPool()
    {
        if (cellDebugPrefeb == null) return;
        while (debugRenderers.Count < cells.Count)
        {
            var go = Instantiate(cellDebugPrefeb, transform);
            debugRenderers.Add(go.GetComponent<SpriteRenderer>());
        }
    }


    void LateUpdate() // Cell Colouring
    {
        EnsureDebugPool();

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            SpriteRenderer r = debugRenderers[i];

            if (c.role == CellRole.Player && c.isDead)
            {
                r.gameObject.SetActive(false);
                continue;
            }
            r.gameObject.SetActive(true);

            r.transform.position = new Vector3(c.currentPos.x, c.currentPos.y, 0f);

            float d = c.cellRadius * 2f;
            r.transform.localScale = new Vector3(d, d, 1f);

            if (c.role == CellRole.WhiteBlood) r.color = Color.blue;
            else if (c.role == CellRole.Player) r.color = Color.red;
            else if (c.organismId >= 0 && c.organismId < organisms.Count && organisms[c.organismId].isDead)
                r.color = new Color32(255, 255, 170, 255);
            else r.color = Color.yellow;
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

    #region Input, player function

    Cell ApplyInput(Cell player)
    {

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePlayerDistance = Vector2.Distance(GetPlayerPosition(), mousePos);

        playerSpeed = Mathf.Lerp(0, maxSpeed, Mathf.InverseLerp(0f, threshold, mousePlayerDistance));

        player.nextPos = Vector2.MoveTowards(GetPlayerNextPosition(), mousePos, playerSpeed * Time.deltaTime);
        return player;
    }
    void ApplyPlayerInput()
    {
        if (playerCellIndex < 0 || playerCellIndex >=cells.Count) return;

        Cell player = cells[playerCellIndex];
        player = ApplyInput(player);
        player.nextVelocity = Vector2.zero;
        cells[playerCellIndex] = player;
    }
    static bool IsFinite(Vector2 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y));
    }
    public Vector2 GetPlayerPosition()
    {
        if (playerCellIndex < 0 || playerCellIndex >= cells.Count) return Vector2.zero;
        Vector2 p = cells[playerCellIndex].currentPos;
        return IsFinite(p) ? p : Vector2.zero;
    }
    Vector2 GetPlayerNextPosition()
    {
        if (playerCellIndex < 0 || playerCellIndex >= cells.Count) return Vector2.zero;
        return cells[playerCellIndex].nextPos;
    }

    float GetPlayerRadius()
    {
        if (playerCellIndex < 0 || playerCellIndex >= cells.Count) return 0.4f;
        return cells[playerCellIndex].cellRadius;
    }

    #endregion

    #region Create

    void CreatePlayerCell(Vector2 pos)
    {
        //reuse pool
        if (deadPlayerPool.Count > 0)
        {
            int idx = deadPlayerPool[^1];
            deadPlayerPool.RemoveAt(deadPlayerPool.Count - 1);

            Cell c = cells[idx];

            c.isDead = false;
            c.hp = 2f;
            c.isPlayerAttachedToWBC = false;

            c.currentPos = pos;
            c.nextPos = pos;
            c.currentVelocity = Vector2.zero;
            c.nextVelocity = Vector2.zero;

            c.organismId = -1;
            c.role = CellRole.Player;

            c.cellRadius = 0.2f;
            c.detectRadius = c.cellRadius * 4f;

            c.detected = true;

            c.cohesionDV = Vector2.zero;

            cells[idx] = c;

            playerCellIndex = idx;
            return;
        }

       int newIdx = cells.Count;

        Cell clone = new Cell();

        clone.currentPos = pos;
        clone.nextPos = pos;
        clone.currentVelocity = Vector2.zero;
        clone.nextVelocity = Vector2.zero;

        clone.cellRadius = 0.2f;
        clone.detectRadius = clone.cellRadius * 4f;

        clone.organismId = -1;
        clone.role = CellRole.Player;

        clone.hp = 2f;
        clone.detected = true;
        clone.isDead = false;
        clone.isPlayerAttachedToWBC = false;

        clone.cohesionDV = Vector2.zero;

        cells.Add(clone);

        playerCellIndex = newIdx;   
    }

    void CreateWBCCell(Vector2 pos)
    {
        Cell w = new Cell();
        w.currentPos = pos;
        w.nextPos = w.currentPos;
        w.currentVelocity = Vector2.zero;
        w.nextVelocity = Vector2.zero;

        w.cellRadius = 0.2f;
        w.detectRadius = w.cellRadius * 10f;

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

        org.lifespan = UnityEngine.Random.Range(1f, 5f);
        float life = Mathf.InverseLerp(1f, 5f, org.lifespan);
        core.cellRadius = Mathf.Lerp(0.25f, 0.3f, life);
        int shellCount = Mathf.RoundToInt(Mathf.Lerp(15f, 25f, life));

        core.detectRadius = core.cellRadius * 7.5f;


        org.coreDistance = core.detectRadius;
        org.defaultCoreDistance = org.coreDistance;

        core.organismId = org.id;
        core.role = CellRole.Core;

        int coreIndex = cells.Count;
        cells.Add(core);

        org.coreIndex = coreIndex;
        org.members.Add(coreIndex);
        
        //Shell
        float shellRadius = Mathf.Lerp(0.1f, 0.12f, life);
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

            //이부분부터 프로퍼티화해야할듯.
            shell.cellRadius = shellRadius;
            shell.detectRadius = shell.cellRadius * 4f;

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

            float speed = 10f;


            core.nextVelocity += org.heading * speed * dt;
            cells[coreIdx] = core;
            organisms[i] = org;
        }
    }



    void ApplyKeepOrganismShape()
    {
        float dt = Time.deltaTime;

        float tolerance = 0.02f;
        float c = 1.1f;     // damping
        float maxForce = 80f;

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
            float k = (org.playerInside == true) ? 250f : 100f; // spring
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

    void ApplyEmitWBCFromOrganism()
    {
        float dt = Time.deltaTime;
        WBCSpawnTimer += dt;

        const float interval = 0.5f;
        if (WBCSpawnTimer < interval) return;

        WBCSpawnTimer -=interval;

        for (int i = 0; i < organisms.Count; i++)
        {
            Organisms o = organisms[i];
            if(o.isDead) continue;
            if(!o.playerInside) continue;
            if(o.coreIndex < 0 || o.coreIndex >= cells.Count) continue;

            Cell core = cells[o.coreIndex];
            CreateWBCCell(core.currentPos+(Random.insideUnitCircle*(core.cellRadius*0.8f)));
            return;
        }
    }

    void ApplyCellPushing(int currentIndex, int otherIndex)
    {
        Cell a = cells[currentIndex];
        Cell b = cells[otherIndex];

        if (a.role == CellRole.Player || b.role == CellRole.Player) return; //---
        if (a.role == CellRole.WhiteBlood || b.role == CellRole.WhiteBlood) return;
        if(a.role == CellRole.Core || b.role == CellRole.Core) return;

        Vector2 delta = b.nextPos - a.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float dist = Mathf.Sqrt(d2);
        float maxDist = a.detectRadius + b.detectRadius;

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

    void ApplyCohesion(int aIndex, int bIndex)  //cell gathering method
    {
        float dt = Time.deltaTime;

        // only if player involved (optional)
        Cell A = cells[aIndex];
        Cell B = cells[bIndex];
        if (A.role != CellRole.Player && B.role != CellRole.Player) return;

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
    void ApplyOrganismJelly(float dt) //apply this to organisms instead of ApplyKeepDistance()?? 
    {

        float k = 0f;     // spring strength
        float c = 1.1f;     // damping
        float maxPenetration = 0.35f;
        float maxAccel = 900f;

        // Loop through all cells and apply jelly only to the roles you want
        for (int i = 0; i < cells.Count; i++)
        {
            Cell target = cells[i];

            bool isPlayer = (i==playerCellIndex);

            if(isPlayer)
            {
                k = 300f;
            }
            else if (target.role==CellRole.Core)
            {
                k = 50;
            }
            else
            {
                k = 5f;
            }

               // k = isPlayer ? 300f : 5f;

            if (target.isDead) continue;

            // Only push these roles
            if (target.role != CellRole.Player && target.role != CellRole.WhiteBlood && target.role != CellRole.Core) continue;


            Vector2 totalAccel = Vector2.zero;

            for (int o = 0; o < organisms.Count; o++)
            {
                var org = organisms[o];
                if (org.isDead) continue;

                // Optional: don’t push cells that are part of this organism
                if (target.organismId == o) continue;

                Cell core = cells[org.coreIndex];


                float barrier=1f;
                if(target.role==CellRole.WhiteBlood)
                {
                    barrier = org.coreDistance + target.cellRadius;
                }
                else barrier= org.coreDistance + target.detectRadius;

                Vector2 delta = target.nextPos - core.nextPos;
                float d2 = delta.sqrMagnitude;
                if (d2 < 1e-6f) continue;

                float dist = Mathf.Sqrt(d2);
                float penetration = barrier - dist;
                if (penetration <= 0f) continue;

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

        if (A.role == CellRole.Player && B.role != CellRole.Player)
        {
            float r = A.detectRadius + B.cellRadius;
            if ((A.nextPos - B.nextPos).sqrMagnitude <= r * r)
            {
                B.detected = true;
                cells[b] = B;
            }
            else B.detected = false;
        }
        else if (B.role == CellRole.Player && A.role != CellRole.Player)
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

            if (c.role == CellRole.Player)
            {
                speed = 130f;
            }
            else if (c.role == CellRole.WhiteBlood)
            {
                speed = 30f;
            }

            // ORGANISM WIGGLE
            else if (c.organismId >= 0 && c.organismId < organisms.Count)
            {
                Organisms org = organisms[c.organismId];
                float t = Mathf.Clamp01(org.deadTimer / maxDeadTime);

                //if (c.detected)
                //    speed = Mathf.Lerp(600f, 0f, t);
                //else 
                //    speed = Mathf.Lerp(100f, 0f, t);

                if (org.playerInside)
                {
                    speed = Mathf.Lerp(100f, 0f, t);
                    org.coreDistance = org.defaultCoreDistance ;
                }
                else
                {
                    speed = Mathf.Lerp(50f, 0f, t);
                    org.coreDistance = org.defaultCoreDistance * 1.1f;
                }
                
            }
            else
            {
                continue;
            }
            c.nextVelocity += randomDir * speed * dt;

            cells[i] = c;
        }
    }
    void ApplyPlayerFunctions()
    {
        ApplyPlayerPush();
    }

    void ApplyPlayerPush()
    {
        Vector2 playerPos = GetPlayerNextPosition();

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];

            if (c.role == CellRole.Player || c.role == CellRole.WhiteBlood) continue;

            Vector2 delta = c.nextPos - playerPos;
            float d2 = delta.sqrMagnitude;
            if (d2 > playerInfluenceRadius * playerInfluenceRadius) continue;

            float dist = Mathf.Sqrt(d2);
            if (dist < 1e-5f) continue;

            Vector2 dir = delta / dist;
            float force = (playerInfluenceRadius - dist) / playerInfluenceRadius;

            c.nextPos += dir * force * playerPushStrength * Time.deltaTime;
            cells[i] = c;
        }
    }
    #endregion

    #region Organism 

    void ApplyOrganismReproduction()
    {
        int initialCount = organisms.Count;

        for(int i = 0; i< initialCount; i++)
        {
            Organisms org = organisms[i];

            if(org.coreIndex <0||org.coreIndex >= cells.Count) continue;
            if(org.isDead) continue;
            if(org.lifespan==0) continue;
            
            //if(org.lifespan>4)
            {
                Cell centre = cells[org.coreIndex];
                CreateOrganism(centre.currentPos);
            }

            
        }
    }
    void ApplyOrganismDeath() //function when the orgarnism is die
    {
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

    void ApplyPlayerHeadToOrganism(float dt)
    {
        

        for( int i =0; i<organismCount; i++)
        {
            Organisms o = organisms[i];
            if (o.isDead) continue;

            Cell core = cells[o.coreIndex];
            
            for(int j= 0; j<cells.Count; j++) 
            {
                if (j == playerCellIndex) continue;

                Cell p = cells[j];       
                if(p.role != CellRole.Player) continue;
                if(p.isDead) continue;
                
                Vector2 d = core.nextPos - p.currentPos;
                float d2 = d.magnitude;

                if(d2>o.coreDistance) continue;

                p.nextVelocity += d.normalized * 20f * dt;

                cells[j] = p;
                
            }
            

        }
    }


    void ApplyPlayerKillsOrganism()
    {
        List<int> playerCells = new List<int>(16);
        for(int p =0;p<cells.Count;p++)
        {
            if (cells[p].role==CellRole.Player && !cells[p].isDead)
            {
                playerCells.Add(p);
            }
        }
        if (playerCells.Count == 0) return;

        for (int i = 0; i < organisms.Count; i++)
        {
            var org = organisms[i];
            if (org.isDead) continue;

            org.playerInside = false;

            int coreIndex = org.coreIndex;
            if (coreIndex < 0 || coreIndex >= cells.Count)
            {
                organisms[i] = org;
                continue;
            }

            Cell coreCell = cells[coreIndex];

            bool killed = false;    

            float insideDist = org.coreDistance +coreCell.cellRadius;
            
            for(int k =0; k<playerCells.Count;k++)
            {
                Cell player = cells[playerCells[k]];

                Vector2 delta = coreCell.nextPos - player.nextPos;
                float d2 = delta.sqrMagnitude;
                if (d2 < 1e-8f) continue;

                float inside = player.cellRadius + insideDist;
                if(d2<=inside*inside)
                {
                    org.playerInside = true;

                    float killDist = player.cellRadius + coreCell.cellRadius;

                    if (d2 <= killDist * killDist)
                    {
                        organisms[i] = org;
                        KillEachCellInsideOrganism(i);
                        killed = true;
                        break;
                    }
                }
            }
            if(!killed)
            {
                organisms[i] = org;
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

        OrganismLeftCount--;

        if (!alreadyDead)
        {
            org.deadTimer = 0f;
        }

        for(int m = 0; m<org.members.Count; m++)
        {
            int cellIdx = org.members[m];
            if(cellIdx <0 ||cellIdx>=cells.Count) continue;

            Cell c = cells[cellIdx];
            c.isDead = true;

            ReproductionEnergy++;

            cells[cellIdx] = c;
        }

        organisms[orgId] = org;
    }
    #endregion

    int FindNearestPlayerIndex(Vector2 pos, float maxRange)
    {
        float bestD2 = maxRange * maxRange;
        int bestIdx = -1;

        nearestPlayerBuffer.Clear();
        spatialHash.Query(pos, nearestPlayerBuffer);

        for (int k = 0; k < nearestPlayerBuffer.Count; k++)
        {
            int j = nearestPlayerBuffer[k];
            Cell c = cells[j];
            if (c.role != CellRole.Player) continue;
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


    void ApplyWBCAttaching()
    {
        float dt = Time.deltaTime;

        float attractStrength = 8f;
        float drag = 30f;

        for (int i = 0; i<cells.Count; i++)
        {
            if (cells[i].role != CellRole.Player) continue; 
            Cell p = cells[i];
            p.isPlayerAttachedToWBC = false;
            cells[i] = p;    
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].role != CellRole.WhiteBlood) continue;

            Cell w = cells[i];

            int targetIdx = FindNearestPlayerIndex(w.nextPos, w.detectRadius);

            
            if (targetIdx < 0)
            {
                w.nextVelocity *= Mathf.Exp(-drag * dt);
                cells[i] = w;
                continue;
            }
            if (cells[targetIdx].isDead) continue;
            Cell player = cells[targetIdx];

            Vector2 delta = player.nextPos - w.nextPos;
            float d2 = delta.sqrMagnitude;

            float r = w.detectRadius;

            w.WBCInSight = (d2 <= r * r) && (d2 > player.cellRadius*player.cellRadius);
            

            if (w.WBCInSight)
            {
                float dist = Mathf.Sqrt(d2);
                Vector2 dir = delta / dist;

                float force = (r - dist) / r;
                w.nextPos += dir * (force * attractStrength) * dt;
            }

            float attachDist = player.cellRadius + w.cellRadius;
            bool isAttachedToPlayer = (d2 <= (attachDist * attachDist) * 1.5f);

            if (isAttachedToPlayer)
            {
                player.isPlayerAttachedToWBC = true;
            }

            cells[i] = w;
            cells[targetIdx] = player;
        }


    }

    void ApplyWBCDamagePlayer()
    {
        float dt = Time.deltaTime;
        float damagePerSecond = 1f;
        
        for(int i = 0; i<cells.Count; i++)
        {
            Cell p = cells[i];
            if (p.role != CellRole.Player) continue;
            if(p.isDead) continue;

            if(p.isPlayerAttachedToWBC)
            {
                p.hp = Mathf.Max(p.hp - damagePerSecond *dt, 0);
            }

            if(p.hp<=0 && !p.isDead)
            {
                p.hp = 0;
                p.isDead = true;
                deadPlayerPool.Add(i);
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

    public bool IsLevelWin()
    {
        if(organisms.Count ==0) return false;   

        for(int i = 0; i<organismCount; i++)
        {
            if (!organisms[i].isDead) return false;
        }
        return true;
    }


    #endregion


}


