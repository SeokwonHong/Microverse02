
using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.PlayerSettings;
using Vector2 = UnityEngine.Vector2;

public class CellManager : MonoBehaviour
{


    public int organismCount = 20;
    public int WBCCount = 10;

    public float SceneGenerateSize = 20;



    [Header("Mouse and Player")]
    float mousePlayerDistance;
    private Vector2 mousePos;
    float playerSpeed;

    private float maxSpeed = 10f;
    private float threshold = 17f;


    private float playerRadius;
    private float playerInfluenceRadius;
    int playerCellIndex = -1; //-1 means player not allocated yet. If player is made, int number will be allocated
    public float playerPushStrength;


    public float cellSpeed;

    //해쉬 
    SpatialHash spatialHash;
    [SerializeField] float BoxSize = 3f;


    private List<Cell> cells = new List<Cell>();
    List<Organisms> organisms = new List<Organisms>();

    enum CellRole { Player, Core, Shell, WhiteBlood }

    public bool isOrganismDead = false;
    const float maxDeadTime = 20f;
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

        public float detected;  // 1=detected  0=not detected
    }

    class Organisms
    {
        public int id; // who this organism is 
        public int coreIndex; // what's the core (find with index)
        public List<int> members = new List<int>(); // // all cell memebers inside the organism
        public float coreDistance; // distance between Core and Shell

        public Vector2 anchorPos;
        public Vector2 heading; //normalized direction
        public float headingPower; // Its more tendency likely than speed. must keep value low to inturrupt less
        public bool anchorEnabled; //anchor holds cells: structure is destroied once its dead
        public float hp;
        public bool isDead;
        public float deadTimer;
        public float playerInside;
    }

    //vector assume that there's two points and in the end of the point they have a invisible arrow
    //direction * power(magnitude)
    void Start()
    {
        spatialHash = new SpatialHash(BoxSize);



        CreatePlayerCell(Vector2.zero);
        playerRadius = GetPlayerRadius();
        playerInfluenceRadius = playerRadius * 7f;




        float minX = -SceneGenerateSize;
        float maxX = SceneGenerateSize;
        float minY = -SceneGenerateSize;
        float maxY = SceneGenerateSize;


        for (int i =0; i<WBCCount; i++)
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
        if (Input.GetKeyDown(KeyCode.V))
        {
            Cell player = cells[playerCellIndex];
            Debug.Log(cells.Count);
            Debug.Log(player.nextVelocity);
        }


        // 1) Apply Hash


        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            c.nextVelocity = c.currentVelocity; //First among double buffer
            c.nextPos = c.currentPos + c.nextVelocity * cellSpeed * Time.deltaTime;
            cells[i] = c;
        }

        spatialHash.Clear(); // Delete data inside the dictionary 
        for (int i = 0; i < cells.Count; i++)
        {
            spatialHash.Insert(cells[i].nextPos, i);
        }

        for (int i = 0; i < cells.Count; i++)
        {
            foreach (int otherIndex in spatialHash.Query(cells[i].nextPos)) // QQuery will give this the index id. Once it's sent, it will be replaced to next one right after
            {
                if (otherIndex <= i) continue;


                ResolveOverlap(i, otherIndex);
                ApplyCellPlayerDetection(i, otherIndex);
                ApplyCellPushing(i, otherIndex);
                
                
                ApplyKeepDistance(i, otherIndex);
                //ApplyCohesion(i, otherIndex);
                ApplyJelly(i, otherIndex);

            }
        }
        ApplyPlayerInput();
        ApplyPlayerFunctions();


        //ApplyOrganismTendency();
        ApplyCoreAnchor();




        for (int iter = 0; iter < 3; iter++) // play iter times in one frame
        {

            
           // for (int i = 0; i < cells.Count; i++) ResolvePlayerOverlap(i);

        }
        ApplyPlayerKillsOrganism();
        ApplyCellMovement();
        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            c.currentVelocity = c.nextVelocity;// second among double buffer
            c.currentPos = c.nextPos;
            cells[i] = c;
        }

        ApplyOrganismDeath();
        UpdateDeadOrganisms();

    }
    /// <summary>
    /// ////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    /// 
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

        player.cellRadius = 0.4f;
        player.detectRadius = player.cellRadius * 6f;

        player.organismId = -1;
        player.role = CellRole.Player;

        playerCellIndex = cells.Count;
        cells.Add(player);
    }

    void CreateWBCCell(Vector2 pos)
    {



        Cell w = new Cell();
        w.currentPos = pos + UnityEngine.Random.insideUnitCircle * 1.2f;
        w.currentVelocity = Vector2.zero;

        w.cellRadius = 0.25f;
        w.detectRadius = w.cellRadius * 3f;

        w.organismId = -1;
        w.role = CellRole.WhiteBlood;
        w.detected = 0;

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
        core.detectRadius = core.cellRadius * 5f;
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
        //org.anchorEnabled=true;

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

    //void ResolvePlayerOverlap(int cellIndex) //player - basic cell collision
    //{
    //    var c = cells[cellIndex];
    //    if (c.role == CellRole.Player) return;
    //    if (c.role == CellRole.Core) return;

    //    Vector2 playerPos = GetPlayerNextPosition();

    //    Vector2 delta = c.nextPos - playerPos;
    //    float d2 = delta.sqrMagnitude;
    //    if (d2 < 1e-8f) return;

    //    float dist = Mathf.Sqrt(d2);
    //    float minDist = c.cellRadius + GetPlayerRadius();

    //    if (dist >= minDist) return;

    //    Vector2 dir = delta / dist;
    //    float penetration = (minDist - dist);

    //    c.nextPos += dir * penetration * playerPushStrength;

    //    cells[cellIndex] = c;
    //}
    void ApplyCoreAnchor() //apply core cell an anchor
    {
        foreach (var org in organisms)
        {
            if (!org.anchorEnabled) continue;

            int coreIdx = org.coreIndex;
            Cell core = cells[coreIdx];

            core.nextPos = org.anchorPos;
            core.nextVelocity = Vector2.zero;
            cells[coreIdx] = core;
        }
    }

    void ApplyOrganismDeath() //function when the orgarnism is die
    {
        for (int i = 0; i < organisms.Count; i++)
        {
            var org = organisms[i];
            //if(org.hp>0f) continue; 


            if (!isOrganismDead) continue;

            if (org.isDead) continue;

            org.isDead = true;
            org.anchorEnabled = false;
            org.heading = Vector2.zero;
            org.headingPower = 0f;
            org.deadTimer = 0;
            organisms[i] = org;
        }
    }
    void UpdateDeadOrganisms()
    {
        for (int i = 0; i < organisms.Count; i++)
        {
            var org = organisms[i];
            if (!org.isDead) continue;

            org.deadTimer += Time.deltaTime;
            if (org.deadTimer >= maxDeadTime)
            {
                org.deadTimer = maxDeadTime;
            }
            organisms[i] = org;
        }
    }
    void ApplyOrganismTendency() //Organism movement, more likely tendency
    {
        Vector2 playerPos = GetPlayerNextPosition();


        for (int i = 0; i < organisms.Count; i++)
        {
            var org = organisms[i];
            if (org.isDead) continue;
            //if(org.hp<=0f) continue;
            int coreIdx = org.coreIndex;
            if (coreIdx < 0) continue;

            Vector2 corePos = cells[coreIdx].nextPos;
            Vector2 toPlayer = playerPos - corePos;

            float d2 = toPlayer.sqrMagnitude;
            if (d2 < 1e-8f) continue;

            org.heading = toPlayer.normalized;
            org.headingPower = 0.5f;

            Cell core = cells[coreIdx];
            core.nextPos += org.heading * org.headingPower * Time.deltaTime;
            cells[coreIdx] = core;
            organisms[i] = org;
        }
    }



    void ApplyKeepDistance(int CurrentIndex, int OtherIndex)
    {

        Cell a = cells[CurrentIndex];
        Cell b = cells[OtherIndex];




        bool aIsCore = a.role == CellRole.Core;
        bool bIsCore = b.role == CellRole.Core;
        if ((aIsCore && bIsCore) || (!aIsCore && !bIsCore)) return;


        int coreIdx = aIsCore ? CurrentIndex : OtherIndex;
        int shellIdx = aIsCore ? OtherIndex : CurrentIndex;

        Cell core = cells[coreIdx];
        Cell shell = cells[shellIdx];



        int orgId = core.organismId;
        if (orgId < 0 || orgId >= organisms.Count) return;
        if (shell.organismId != orgId) return;

        if (organisms[orgId].isDead) return;

        float coreDist = organisms[orgId].coreDistance;

        Vector2 delta = shell.nextPos - core.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float distance = Mathf.Sqrt(d2);
        Vector2 direction = delta / distance;

        float shellGap = distance - coreDist;

        float tolerance = 0.02f;
        if (Mathf.Abs(shellGap) < tolerance) return; //  if  |shellGap| < tolerance 

        float k = 300f;
        float c = 6f;

        float vectorAngle = Vector2.Dot(shell.nextVelocity - core.nextVelocity, direction);

        float springPower = (-k * shellGap) - (c * vectorAngle);

        springPower = Mathf.Clamp(springPower, -1000f, 1000f);

        float massCore = Mathf.Max(0.001f, core.cellRadius * core.cellRadius);
        float massShell = Mathf.Max(0, 001f, shell.cellRadius * shell.cellRadius);

        float massRatio = 1f / (massCore + massShell);

        float coreShare = massShell * massRatio;
        float shellShare = massCore * massRatio;

        core.nextVelocity -= direction * (springPower * coreShare) * Time.deltaTime;
        shell.nextVelocity += direction * (springPower * shellShare) * Time.deltaTime;


        cells[coreIdx] = core;
        cells[shellIdx] = shell;
    }

    void ApplyCellPushing(int currentIndex, int otherIndex)
    {
        Cell Current = cells[currentIndex];
        Cell Other = cells[otherIndex];

        if (Current.role == CellRole.Player || Other.role == CellRole.Player) return;


        Vector2 delta = Other.nextPos - Current.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-5f) return;

        float distance = Mathf.Sqrt(d2);

        float minDist = Current.cellRadius + Other.cellRadius;
        float maxDist = Current.detectRadius + Other.detectRadius;

        if (distance <= minDist) return;
        if (distance > maxDist) return;


        Vector2 dir = delta / distance;

        float penetration = (maxDist - distance) * 0.3f;


        Vector2 push = dir * penetration;



        Current.nextPos -= push;
        Other.nextPos += push;



        cells[currentIndex] = Current;
        cells[otherIndex] = Other;
    }
    void ApplyJelly(int currentIdx, int otherIdx) //apply this to organisms instead of ApplyKeepDistance()?? 
    {

        Cell a = cells[currentIdx];
        Cell b = cells[otherIdx];

        Vector2 delta = a.nextPos - b.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float dist = Mathf.Sqrt(d2);
        Vector2 n = delta / dist; // points from b -> a

        // Keep your values
        float k = 400f;
        float c = 1.3f;
        float dt = Time.deltaTime;

        // Default barrier: just stop them getting too close (soft repulsion)
        float barrier = a.cellRadius + b.cellRadius;

        // If this is core <-> member of the same organism, use organism boundary instead
        bool aIsCore = a.role == CellRole.Core;
        bool bIsCore = b.role == CellRole.Core;

        if (aIsCore ^ bIsCore)
        {
            int coreIdx = aIsCore ? currentIdx : otherIdx;
            int cellIdx = aIsCore ? otherIdx : currentIdx;

            Cell core = cells[coreIdx];
            Cell cCell = cells[cellIdx];

            int orgId = core.organismId;
            if (orgId >= 0 && orgId < organisms.Count && cCell.organismId == orgId && !organisms[orgId].isDead)
            {
                // Barrier radius around the core = coreDistance (your chosen shell radius) + intruder cell radius
                barrier = organisms[orgId].coreDistance + cCell.cellRadius;
            }
        }

        float penetration = barrier - dist;
        if (penetration <= 0f) return;

        // Normal relative speed (damping term)
        float v_n = Vector2.Dot(a.nextVelocity - b.nextVelocity, n);

        // Spring-damper along the normal
        float accel = (k * penetration) - (c * v_n);
        if (accel <= 0f) return;

        // Mass split (area ~ r^2). Bigger cell moves less.
        float mA = Mathf.Max(0.001f, a.cellRadius * a.cellRadius);
        float mB = Mathf.Max(0.001f, b.cellRadius * b.cellRadius);
        float invSum = 1f / (mA + mB);

        float aShare = mB * invSum;
        float bShare = mA * invSum;

        a.nextVelocity += n * (accel * aShare) * dt;
        b.nextVelocity -= n * (accel * bShare) * dt;

        cells[currentIdx] = a;
        cells[otherIdx] = b;
    }

    void ApplyCellPlayerDetection(int a, int b)
    {
        Cell A = cells[a];
        Cell B = cells[b];

        if (A.role == CellRole.Player && B.role != CellRole.Player)
        {
            float r = A.detectRadius + B.cellRadius;
            if ((A.nextPos - B.nextPos).sqrMagnitude <= r * r)
            {
                B.detected = 1;
                cells[b] = B;
            }
            else B.detected = -1;
        }
        else if (B.role == CellRole.Player && A.role != CellRole.Player)
        {
            float r = B.detectRadius + A.cellRadius;
            if ((B.nextPos - A.nextPos).sqrMagnitude <= r * r)
            {
                A.detected = 1;
                cells[a] = A;
            }
            else A.detected = -1;
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

        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];

            if (c.organismId < 0 || c.organismId >= organisms.Count) continue;
            Organisms org = organisms[c.organismId];


            float t = Mathf.Clamp01(org.deadTimer / maxDeadTime);

            Vector2 ramdomDir = UnityEngine.Random.insideUnitCircle;
            if (ramdomDir.sqrMagnitude < 1e-6f) continue;

            float speed;
            if (cells[i].detected == 1)
            {
                speed = Mathf.Lerp(3f, 0.0f, t);
            }
            else speed = Mathf.Lerp(2f, 0.0f, t);



            float drag = 9f;
            c.nextVelocity *= Mathf.Exp(-drag * Time.deltaTime);
            c.nextVelocity += ramdomDir * speed;

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
            // if(c.role !=CellRole.Shell) continue;

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

    #region Player VS Shell

    void ApplyPlayerKillsOrganism()
    {
        for (int i = 0; i < organisms.Count; i++)
        {
            var org = organisms[i];
            int CoreIndex = org.coreIndex;

            Cell coreCell = cells[CoreIndex];
            Cell player = cells[playerCellIndex];
            Vector2 delta = coreCell.nextPos - player.nextPos;

            float d2 = delta.sqrMagnitude;

            if (d2 < 1e-5f) continue;

            float minDist = player.cellRadius + org.coreDistance + coreCell.cellRadius;
            float minDist2 = minDist * minDist;

            float dist = Mathf.Sqrt(d2);

            if (dist > minDist)
            {
                continue;
            }

            if (dist <= minDist)
            {
                org.playerInside = 1;

                if (d2 - coreCell.cellRadius < 0.3f)
                {
                    org.isDead = true;
                }
            }

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
            Gizmos.color = Color.green;

            if (c.role == CellRole.Player)
            {
                Gizmos.color = Color.red;
            }
            else if (c.role == CellRole.WhiteBlood)
            {
                Gizmos.color = Color.yellow;
            }
            else if (c.organismId >= 0 && c.organismId < organisms.Count)
            {
                if (organisms[c.organismId].isDead)
                {
                    Gizmos.color = Color.white;
                }
            }

            Gizmos.DrawSphere(c.currentPos, c.cellRadius);

        }
    }

    #endregion
}



