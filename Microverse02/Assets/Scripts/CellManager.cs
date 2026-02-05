using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.PlayerSettings;
using Vector2 = UnityEngine.Vector2;

public class CellManager : MonoBehaviour
{


    [Header("Defalut Settings")]
    public int organismCount = 20;
    public int WBCCount = 10;

    [Header("Map generation")]
    [SerializeField] Vector2 mapCentre = Vector2.zero;
    [SerializeField] float mapRadius;
    public GameObject refToBg;
    [SerializeField] float wallBounciness = 0.5f;


    [Header("Mouse and Player")]
    float mousePlayerDistance;
    Vector2 mousePos;
    float playerSpeed;


    [Header("Player")]
    private float maxSpeed = 10f;
    private float threshold = 17f;
    private float playerRadius;
    private float playerInfluenceRadius;
    int playerCellIndex = -1; //-1 means player not allocated yet. If player is made, int number will be allocated
    public float playerPushStrength;


    [Header("Spatial Hash")]
    SpatialHash spatialHash;
    [SerializeField] float BoxSize = 3f;
    readonly List<int> neighbourBuffer = new List<int>(128);


    [Header("cells | organisms | neighbourBuffer Array")]
    List<Cell> cells = new List<Cell>();
    List<Organisms> organisms = new List<Organisms>();
    readonly List<int> wbcBuffer = new List<int>(128);

    [Header("Organism Death")]
    bool isOrganismDead = false;
    const float maxDeadTime = 20f;


    enum CellRole { Player, Core, Shell, WhiteBlood }


    class Cell
    {
        public Vector2 currentPos;
        public Vector2 currentVelocity;

        public Vector2 nextPos;
        public Vector2 nextVelocity;

        public float cellRadius; // each cell's radius
        public float detectRadius; // neibor detecting radius

        public int organismId; //-1 = indipendent cell, 1 = Organism 1
        public CellRole role; // Core / Shell / WhiteBlood

        public bool detected;
        public bool isDead = false; 
    }

    class Organisms
    {
        public int id; // who this organism is 
        public int coreIndex; // what's the core (find with index)
        public List<int> members = new List<int>(32);
        public float coreDistance; // distance between Core and Shell

        public Vector2 heading; //normalized direction
        public float headingPower; // Its more tendency likely than speed. must keep value low to inturrupt less
        public bool anchorEnabled; //anchor holds cells: structure is destroied once its dead

        public float wanderTimer;

        public bool isDead;
        public float deadTimer;
        public bool playerInside;


    }

    //vector assume that there's two points and in the end of the point they have a invisible arrow
    //direction * power(magnitude)
    void Start()
    {
        spatialHash = new SpatialHash(BoxSize);

        refToBg.transform.localScale = new Vector3(mapRadius*2f, mapRadius*2f, 1);

        CreatePlayerCell(Vector2.zero);
        playerRadius = GetPlayerRadius();
        playerInfluenceRadius = playerRadius * 10f;

        float minX = -mapRadius;
        float maxX = mapRadius;
        float minY = -mapRadius;
        float maxY = mapRadius;


        for (int i = 0; i < WBCCount; i++)
        {
            Vector2 pos = new Vector2(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(minY, maxY)
            );

            CreateWBCCell(pos);

        }


        for (int i = 0; i < organismCount; i++)
        {
            Vector2 pos = new Vector2(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(minY, maxY)
            );

            CreateOrganism(pos);
        }


    }
    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    // Update is called once per frame
    void Update()
    {
        

        float dt = Time.deltaTime;  
        // 1) Apply Hash


        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            c.nextVelocity = c.currentVelocity; //First among double buffer
            c.nextPos = c.currentPos;
            c.detected = false;
            cells[i] = c;
            
        }

        spatialHash.BeginFrame(); // Delete data inside the dictionary 

        for (int i = 0; i < cells.Count; i++)
        {
            spatialHash.Insert(cells[i].nextPos, i);
        }


        for (int i = 0; i < cells.Count; i++)
        {
            spatialHash.Query(cells[i].nextPos, neighbourBuffer);

            for (int n = 0; n < neighbourBuffer.Count; n++) // QQuery will give this the index id. Once it's sent, it will be replaced to next one right after
            {
                int otherIndex = neighbourBuffer[n];
                if (otherIndex <= i) continue;


                ResolveOverlap(i, otherIndex);
                ApplyCellDetection(i, otherIndex);
                ApplyCellPushing(i, otherIndex);
                //ApplyCohesion(i, otherIndex);
            }
        }

        
        ApplyDragToCells();
        ApplyPlayerInput();
        ApplyPlayerFunctions();

        //wbc
        ApplyWBCAttaching();

        ApplyOrganismTendency();
        ApplyCoreAnchor();




        for (int iter = 0; iter < 3; iter++) // play iter times in one frame
        {

            ApplyOrganismJelly(Time.fixedDeltaTime);
            ApplyKeepShape();
            // for (int i = 0; i < cells.Count; i++) ResolvePlayerOverlap(i);

        }
        ApplyPlayerKillsOrganism();
        ApplyCellMovement();
        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];

            c.nextPos += c.nextVelocity * dt;
            cells[i] = c;

            ApplyCircleBoundary(i);

            c= cells[i];
            c.currentVelocity = c.nextVelocity;// second among double buffer
            c.currentPos = c.nextPos;
            cells[i] = c;
        }

        ApplyOrganismDeath();
        UpdateDeadOrganisms();

    }

    private void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            Cell player = cells[playerCellIndex];
            Debug.Log(cells.Count);

        }
        if (Input.GetMouseButton(0))
        {
            Cell player = cells[playerCellIndex];
            //player.cellRadius += 0.1f;

            CreatePlayerCell(player.nextPos);
            
        }
    }
    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    /// 

    

    #region Map
    void ApplyCircleBoundary(int i)
    {
        Cell c= cells[i];

        Vector2 p = c.nextPos; 
        Vector2 v = c.nextVelocity;

        Vector2 to = p-mapCentre;
        float dist = to.magnitude;

        float allowed = mapRadius - c.cellRadius;
        if (dist <= allowed || dist < 1e-6f) return;

        Vector2 n = to / dist;
        c.nextPos = mapCentre + n*allowed;

        float vn = Vector2.Dot(v, n);
        if(vn>9f)
        {
            v = v - 2f * vn * n;
            v*=wallBounciness;
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
        if (playerCellIndex < 0) return;

        Cell player = cells[playerCellIndex];
        player = ApplyInput(player);
        player.nextVelocity = Vector2.zero;
        cells[playerCellIndex] = player;
    }
    public Vector2 GetPlayerPosition()
    {
        if (playerCellIndex < 0)
        {
            return Vector2.zero;
        }
        return cells[playerCellIndex].currentPos;
    }
    Vector2 GetPlayerNextPosition()
    {
        if (playerCellIndex < 0)
        {
            return Vector2.zero;
        }
        return cells[playerCellIndex].nextPos;
    }

    float GetPlayerRadius()
    {
        if (playerCellIndex < 0)
        {
            return 0.4f;
        }
        return cells[playerCellIndex].cellRadius;
    }

    #endregion

    

    #region Create

    void CreatePlayerCell(Vector2 pos)
    {
        Cell player = new Cell();

        player.currentPos = pos;
        player.currentVelocity = Vector2.zero;

        player.nextPos = pos;
        player.nextVelocity = Vector2.zero;


        player.cellRadius = 0.2f;
        player.detectRadius = player.cellRadius * 6f;

        player.organismId = -1;
        player.role = CellRole.Player;

        player.detected = true;
        playerCellIndex = cells.Count;
        cells.Add(player);
    }

    void CreateWBCCell(Vector2 pos)
    {



        Cell w = new Cell();
        w.currentPos = pos + UnityEngine.Random.insideUnitCircle * 1.2f;
        w.currentVelocity = Vector2.zero;

        w.cellRadius = 0.25f;
        w.detectRadius = w.cellRadius * 30f;

        w.organismId = -1;
        w.role = CellRole.WhiteBlood;
        //w.detected = false;

        cells.Add(w);


    }
    void CreateOrganism(Vector2 currentPos)
    {
        Organisms org = new Organisms();
        int shellCount = UnityEngine.Random.Range(20, 26);

        //float coreDistance = 2f;

        org.id = organisms.Count;



        //core 

        Cell core = new Cell();
        core.currentPos = currentPos;
        core.currentVelocity = Vector2.zero;

        core.cellRadius = UnityEngine.Random.Range(0.3f, 0.4f);
        core.detectRadius = core.cellRadius * 5.5f;
        org.coreDistance = core.detectRadius;

        core.organismId = org.id;
        core.role = CellRole.Core;



        int coreIndex = cells.Count;
        cells.Add(core);

        org.coreIndex = coreIndex;
        org.members.Add(coreIndex);

        //Shell
        float CellRadius = UnityEngine.Random.Range(0.1f, 0.13f);
        for (int i = 0; i < shellCount; i++)
        {
            float angle = (Mathf.PI * 2f) * (i / (float)shellCount); //(Mathf.PI * 2f) 는 각도로 이해 * 그걸 비율로 슬라이스
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); //각도를 normalized 된 벡터값으로 바꿈 그걸 해주는게 x 축인 cos , y 축인 sin
            //+ 계산하기 편하게 사분면에 표현가능하게 단위화
            Vector2 pos = currentPos + dir * org.coreDistance;

            Cell shell = new Cell();
            shell.currentPos = pos;
            shell.currentVelocity = Vector2.zero;

            //이부분부터 프로퍼티화해야할듯.
            shell.cellRadius = CellRadius;
            shell.detectRadius = shell.cellRadius * 3f;

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
            if (coreIdx < 0) continue;

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

            
            if(org.heading.sqrMagnitude <1e-6f)
            {
                org.heading= Random.insideUnitCircle.normalized;
                org.wanderTimer = Random.Range(0.5f, 2.0f);
            }

            org.wanderTimer -= dt;
            if(org.wanderTimer<=0f)
            {
                Vector2 jitter = Random.insideUnitCircle * 0.25f;
                org.heading =(org.heading+jitter).normalized;

                org.wanderTimer = Random.Range(0.5f, 2.0f);
            }

            float speed = 5f;
            

            core.nextVelocity += org.heading * speed * dt;
            cells[coreIdx] = core;
            organisms[i] = org;
        }
    }



    void ApplyKeepShape()
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
            float k = (org.playerInside == true) ? 3f : 40f; // spring
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

        if (a.role == CellRole.Player || b.role == CellRole.Player) return;
        if (a.role == CellRole.WhiteBlood || b.role == CellRole.WhiteBlood) return;

        Vector2 delta = b.nextPos - a.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float dist = Mathf.Sqrt(d2);
        float minDist = a.cellRadius + b.cellRadius;
        float maxDist = a.detectRadius + b.detectRadius;

        if (minDist < 1e-6f) return;

        float overlap = maxDist - dist;
        if (overlap <= 0f) return;

        Vector2 dir = delta / dist;

        float dt = Time.deltaTime;
        float pushStrength = 120f;

        Vector2 dv = dir * (overlap * pushStrength);

        a.nextVelocity -= dv * dt;
        b.nextVelocity += dv * dt;

        cells[currentIndex] = a;
        cells[otherIndex] = b;
    }

    void ApplyDragToCells()
    {
        float dt = Time.deltaTime;

        float baseDrag = 10f;
        float minRadius = 0.05f;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];

            float r = Mathf.Max(minRadius, c.cellRadius);

            float drag = baseDrag * (r * 3);

            c.nextVelocity *= Mathf.Exp(-drag * dt);

            cells[i] = c;
        }
    }
    void ApplyOrganismJelly(float dt) //apply this to organisms instead of ApplyKeepDistance()?? 
    {

        if (playerCellIndex < 0) return; //if player is not made yet, return. if player is successfully made using CreatePlayerCell(), playerCellIndex will be integer

        Cell player = cells[playerCellIndex];

        float k = 600f; // spring strengh
        float c = 1.1f; // damping (bigger, more tough surface)

        float maxPenetration = 0.35f;
        float maxAccel = 900f;

        Vector2 totalAccel = Vector2.zero;

        for (int o = 0; o < organisms.Count; o++)
        {
            var org = organisms[o];
            if (org.isDead) continue;

            Cell core = cells[org.coreIndex];

            float barrier = org.coreDistance + player.cellRadius;

            Vector2 delta = player.nextPos - core.nextPos;
            float d2 = delta.sqrMagnitude;
            if (d2 < 1e-6f) continue;

            float dist = Mathf.Sqrt(d2);
            float penetration = barrier - dist; // if player is inside of organism, penetration is integer. deeper = greater value
            if (penetration <= 0f) continue;

            if (penetration > maxPenetration) penetration = maxPenetration;

            Vector2 n = delta / dist;


            float v_n = Vector2.Dot(player.nextVelocity - core.nextVelocity, n); //player direction vs core direction
            // v_n > 0  = Moving in the same direction as n
            // v_n < 0  = Moving opposite to n
            // v_n == 0 = 90 degree 


            float accelMag = (k * penetration) - (c * v_n);
            if (accelMag <= 0f) continue;

            totalAccel += n * accelMag;
        }

        if (totalAccel.sqrMagnitude > maxAccel * maxAccel)
        {
            totalAccel = totalAccel.normalized * maxAccel;
        }
        player.nextVelocity = totalAccel * dt;

        cells[playerCellIndex] = player;
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
            else A.detected = false;
        }
    }





    #endregion

    #region Cell_Rules

    void ApplyCohesion(int CurrentIndex, int OtherIndex)  //cell gathering method
    {
        Cell currentCell = cells[(CurrentIndex)];
        Cell otherCell = cells[(OtherIndex)];

        // cell strainer. aint gonna operate when its not shell
        if (currentCell.organismId != otherCell.organismId) return; // 같은 생물 아니면 무시
        if (currentCell.role != CellRole.Shell) return; //쉘들만 모이게
        if (otherCell.role != CellRole.Shell) return; //비교대상이 쉘이 아니면 무시



        Vector2 delta = otherCell.nextPos - currentCell.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 <= 0f) return;


        float detectR = currentCell.detectRadius;
        float detectR2 = detectR * detectR;
        if (d2 > detectR2) return;

        float minDist = currentCell.cellRadius + otherCell.cellRadius;
        float minDist2 = minDist * minDist;
        if (d2 <= minDist2) return;

        float dist = Mathf.Sqrt(d2);
        Vector2 dir = delta / dist; //벡터를 순수 거리로 나눔

        float speed = 0.1f;
        Vector2 move = dir * (speed * Time.deltaTime);

        currentCell.nextPos += move * 0.5f;
        otherCell.nextPos -= move * 0.5f;

        cells[CurrentIndex] = currentCell;
        cells[OtherIndex] = otherCell;
    }



    void ApplyCellMovement()// cell tendency
    {
        float dt = Time.deltaTime;  
        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];


            //if (c.role == CellRole.Player) continue;

            if (c.organismId < 0 || c.organismId >= organisms.Count) continue;
            Organisms org = organisms[c.organismId];


            float t = Mathf.Clamp01(org.deadTimer / maxDeadTime);

            Vector2 ramdomDir = UnityEngine.Random.insideUnitCircle;
            if (ramdomDir.sqrMagnitude < 1e-6f) continue;

            float speed;
            if (c.detected)
            {
                speed = Mathf.Lerp(100f, 0.0f, t);
            }
            else speed = Mathf.Lerp(55f, 0.0f, t);



            float drag = 9f;
            c.nextVelocity *= Mathf.Exp(-drag * dt);
            c.nextVelocity += ramdomDir * speed * dt;

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

    #region Organism killing
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

        if(!alreadyDead)
        {
            org.deadTimer = 0f;
        }

        for(int m = 0; m<org.members.Count; m++)
        {
            int cellIdx = org.members[m];
            if(cellIdx <0 ||cellIdx>=cells.Count) continue;

            Cell c = cells[cellIdx];
            c.isDead = true;
            cells[cellIdx] = c;
        }

        organisms[orgId] = org;
    }
    #endregion


    #region WBC Constraints

    int FindNearestPlayerIndex(Vector2 pos, float maxRange)
    {
        float bestD2 = maxRange * maxRange;
        int bestIdx = -1;

        wbcBuffer.Clear();
        spatialHash.Query(pos, wbcBuffer);

        for (int k = 0; k < wbcBuffer.Count; k++)
        {
            int j = wbcBuffer[k];
            Cell c = cells[j];
            if (c.role != CellRole.Player) continue;

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


    void ApplyWBCAttaching()
    {
        float dt = Time.deltaTime;

        float attractStrength = 10f;
        float drag = 30f;

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

            Cell target = cells[targetIdx];

            Vector2 delta = target.nextPos - w.nextPos;
            float d2 = delta.sqrMagnitude;

            float r = w.detectRadius;

            bool inSight = (d2 <= r * r) && (d2 > 1e-5f);

            if (inSight)
            {
                float dist = Mathf.Sqrt(d2);
                Vector2 dir = delta / dist;

                float force = (r - dist) / r;
                w.nextPos += dir * (force * attractStrength) * dt;
            }
            else
            {
                w.nextVelocity *= Mathf.Exp(-drag * dt);
            }



            cells[i] = w;
        }


    }



    #endregion




    #region Gizmo
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (cells == null || cells.Count == 0)
        {
            Debug.Log("there's no cell list!");
            return;
        }


        foreach (Cell c in cells)
        {
            Gizmos.color = Color.yellow;



            if (c.role == CellRole.WhiteBlood)
            {
                Gizmos.color = Color.blue;
            }
            else if (c.role == CellRole.Player)
            {
                Gizmos.color = Color.red;
            }

            else if (c.organismId >= 0 && c.organismId < organisms.Count)
            {
                if (organisms[c.organismId].isDead)
                {
                    Gizmos.color = new Color32(255, 255, 170, 255);
                }
            }



            Gizmos.DrawSphere(c.currentPos, c.cellRadius);

        }
    }

    #endregion
}


