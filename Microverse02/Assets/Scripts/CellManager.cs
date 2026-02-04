
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
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


    //public float cellSpeed;

    //해쉬 
    SpatialHash spatialHash;
    [SerializeField] float BoxSize = 3f;
    readonly List<int> neighbourBuffer = new List<int>(128);


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
        public float coreDistance; // distance between Core and Shell

        public Vector2 heading; //normalized direction
        public float headingPower; // Its more tendency likely than speed. must keep value low to inturrupt less
        public bool anchorEnabled; //anchor holds cells: structure is destroied once its dead
       
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
        playerInfluenceRadius = playerRadius * 10f;




        float minX = -SceneGenerateSize;
        float maxX = SceneGenerateSize;
        float minY = -SceneGenerateSize;
        float maxY = SceneGenerateSize;


        //for (int i =0; i<WBCCount; i++)
        //{
        //    Vector2 pos = new Vector2(
        //        UnityEngine.Random.Range(minX, maxX),
        //        UnityEngine.Random.Range(minY, maxY)
        //    );

        //    CreateWBCCell(pos);

        //}


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
 
        }


        // 1) Apply Hash


        for (int i = 0; i < cells.Count; i++)
        {
            Cell c = cells[i];
            c.nextVelocity = c.currentVelocity; //First among double buffer
            c.nextPos = c.currentPos + c.nextVelocity * Time.deltaTime;
            cells[i] = c;
        }

        spatialHash.BeginFrame(); // Delete data inside the dictionary 
        for (int i = 0; i < cells.Count; i++)
        {
            spatialHash.Insert(cells[i].nextPos, i);
        }

        for (int i = 0; i < cells.Count; i++)
        {
            spatialHash.Query(cells[i].nextPos,neighbourBuffer);

            for(int n = 0; n < neighbourBuffer.Count;n++) // QQuery will give this the index id. Once it's sent, it will be replaced to next one right after
            {
                int otherIndex = neighbourBuffer[n];
                if (otherIndex <= i) continue;


                ResolveOverlap(i, otherIndex);
                ApplyCellPlayerDetection(i, otherIndex);
                ApplyCellPushing(i, otherIndex);
                ApplyKeepShape(i, otherIndex);
                //ApplyCohesion(i, otherIndex);

            }
        }

        if(Input.GetKey(KeyCode.Space))
        {
            Cell player = cells[playerCellIndex];
            //player.cellRadius += 0.1f;

            CreatePlayerCell(player.nextPos);
        }
        ApplyDragToCells();
        ApplyPlayerInput();
        ApplyPlayerFunctions();

        //wbc
        ApplyWBCAttaching();

        //ApplyOrganismTendency();
        ApplyCoreAnchor();




        for (int iter = 0; iter < 3; iter++) // play iter times in one frame
        {

            ApplyOrganismJelly(Time.fixedDeltaTime);
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

        player.cellRadius = 0.25f;
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
        w.detectRadius = w.cellRadius * 30f;

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

        }


        organisms.Add(org);

    }
    #endregion

    #region Cell_Constraint
    void ResolveOverlap(int CurrentIndex, int OtherIndex) //basic colliding 
    {
        Cell a = cells[CurrentIndex];
        Cell b = cells[OtherIndex];


        if (!GetPairInfo(a, b, out var p)) return;

        float minDist = a.cellRadius + b.cellRadius;
        float overlap = minDist - p.dist;
        if(overlap<=0) return;

        Vector2 push = p.dir * (overlap * 0.5f);
        a.nextPos -= push;
        b.nextPos += push;

        cells[CurrentIndex] = a;
        cells[OtherIndex] = b;
    }


    void ApplyCoreAnchor() //apply core cell an anchor
    {
        foreach (var org in organisms)
        {
            //if (!org.anchorEnabled) continue;

            int coreIdx = org.coreIndex;
            Cell core = cells[coreIdx];

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
        if (playerCellIndex < 0) return;

        Cell playerCell = cells[playerCellIndex];
        playerCell.nextPos = GetPlayerNextPosition();

        float dt = Time.deltaTime;

        for(int i = 0; i<organisms.Count;i++)
        {
            Organisms org = organisms[i];
            if (org.isDead) continue;

            int coreIdx = org.coreIndex;
            if (coreIdx < 0) continue;

            Cell core = cells[coreIdx];

            if(!GetPairInfo(core,playerCell,out PairInfo p)) continue;

            org.heading = p.dir;
            org.headingPower = 500f;

            core.nextVelocity += p.dir * org.headingPower * dt;

            cells[coreIdx] = core;
            organisms[i] = org;
        }
    }



    void ApplyKeepShape(int CurrentIndex, int OtherIndex)
    {
        Cell a = cells[CurrentIndex];
        Cell b = cells[OtherIndex];

        bool aIsCore = a.role == CellRole.Core;
        bool bIsCore = b.role == CellRole.Core;
        if ((aIsCore && bIsCore) || (!aIsCore && !bIsCore)) return;

        int coreIdx = aIsCore ?CurrentIndex : OtherIndex;
        int shellIdx = aIsCore ? OtherIndex : CurrentIndex;

        Cell core = cells[coreIdx];
        Cell shell = cells[shellIdx];

        int orgId = core.organismId;
        if(orgId <0||orgId>=organismCount) return;
        if (shell.organismId != orgId) return;
        if (organisms[orgId].isDead)return;

        float coreDist = organisms[orgId].coreDistance;

        if (!GetPairInfo(core, shell, out PairInfo p)) return;

        float shellGap = p.dist - coreDist;

        float tolerance = 0.02f;
        if(Mathf.Abs(shellGap) < tolerance) return;

        float k = 300f;
        float c = 6f;

        // Relative velocity
        float vRelative = Vector2.Dot(shell.nextVelocity - core.nextVelocity, p.dir);

        float springPower = (-k*shellGap) - (c*vRelative);
        springPower = Mathf.Clamp(springPower, -1000f, 1000f);

        float massCore = Mathf.Max(0.001f, core.cellRadius * core.cellRadius);
        float massShell = Mathf.Max(0.001f, shell.cellRadius * shell.cellRadius);

        float invSum = 1f/(massCore + massShell);  
        float coreShare = massShell*invSum;
        float shellShare = massCore *invSum;

        float dt = Time.deltaTime;

        core.nextVelocity -= p.dir * (springPower * coreShare) * dt;
        shell.nextVelocity += p.dir * (springPower * shellShare) * dt;

        cells[coreIdx] = core;
        cells[shellIdx] = shell;    
    }

    void ApplyCellPushing(int currentIndex, int otherIndex)
    {
        Cell a = cells[currentIndex];
        Cell b = cells[otherIndex];

        if (a.role == CellRole.Player || b.role == CellRole.Player) return;
        if(a.role == CellRole.WhiteBlood || b.role == CellRole.WhiteBlood) return;

        if(!GetPairInfo(a, b, out PairInfo p)) return;

        float minDist = a.cellRadius + b.cellRadius;
        float maxDist = a.detectRadius + b.detectRadius;

        if (minDist < 1e-6f) return;

        float overlap = maxDist - p.dist;
        if(overlap<=0) return;

        float dt = Time.deltaTime;
        float pushStrength = 60f;

        Vector2 dv = p.dir*(overlap*pushStrength);

        a.nextVelocity -=dv * dt;
        b.nextVelocity += dv * dt;

        cells[currentIndex] = a;
        cells[otherIndex] = b;
    }

    void ApplyDragToCells()
    {
        float dt = Time.deltaTime;

        float baseDrag = 10f;
        float minRadius = 0.05f;

        for(int i =0; i<cells.Count; i++)
        {
            Cell c = cells[i];

            float r = Mathf.Max(minRadius, c.cellRadius);

            float drag = baseDrag * (r*3);

            c.nextVelocity *= Mathf.Exp(-drag*dt);

            cells[i] = c;
        }
    }
    void ApplyOrganismJelly(float dt) //apply this to organisms instead of ApplyKeepDistance()?? 
    {

        if (playerCellIndex < 0) return; //if player is not made yet, return. if player is successfully made using CreatePlayerCell(), playerCellIndex will be integer

        Cell player = cells[playerCellIndex];
        dt = Time.deltaTime;

        float k = 50f; // spring strengh
        float c = 1.3f; // damping (bigger, more tough surface)


        for (int o = 0; o < organisms.Count; o++)
        {
            var org = organisms[o];
            if (org.isDead) continue;

            int coreIdx = org.coreIndex;
            if (coreIdx < 0) continue;

            Cell core = cells[coreIdx];

            if(!GetPairInfo(core,player, out PairInfo p)) continue;

            float barrier = org.coreDistance + player.cellRadius;

            if (p.d2 < 1e-2f) continue;

            float penetration = barrier - p.dist;
            if(penetration<=0f) continue;

            Vector2 n = p.dir;

            float v_n = Vector2.Dot(player.nextVelocity-core.nextVelocity, n);

            float accel = (k*penetration) - (c*v_n);
            if(accel <=0f) continue;

            player.nextVelocity += n * accel * dt;

        }

        cells[playerCellIndex] = player;
    }

    void ApplyCellPlayerDetection(int a, int b)
    {
        Cell A = cells[a];
        Cell B = cells[b];

        if (A.role == CellRole.Player && B.role != CellRole.Player)
        {
            float r = A.detectRadius + B.cellRadius;
            B.detected = ((A.nextPos - B.nextPos).sqrMagnitude <= r*r) ? 1f : 0f;
            cells[b] = B;
        }
        else if (B.role == CellRole.Player && A.role != CellRole.Player)
        {
            float r = B.detectRadius + A.cellRadius;
            A.detected = ((B.nextPos - A.nextPos).sqrMagnitude <= r* r ? 1f: 0f); 
            cells[a] =A;
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

            if (c.role==CellRole.Player) continue;

            if (c.organismId < 0 || c.organismId >= organisms.Count) continue;
            Organisms org = organisms[c.organismId];


            float t = Mathf.Clamp01(org.deadTimer / maxDeadTime);

            Vector2 ramdomDir = UnityEngine.Random.insideUnitCircle;
            if (ramdomDir.sqrMagnitude < 1e-6f) continue;

            float speed;
            if (cells[i].detected == 1)
            {
                speed = Mathf.Lerp(45f, 0.0f, t);
            }
            else speed = Mathf.Lerp(25f, 0.0f, t);



            float drag = 9f;
            c.nextVelocity *= Mathf.Exp(-drag * Time.deltaTime);
            c.nextVelocity += ramdomDir * speed * Time.deltaTime;

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


    #region WBC Constraints

    void ApplyWBCAttaching()
    {
        if (playerCellIndex < 0) return;
        Cell player = cells[playerCellIndex];

        float dt = Time.deltaTime;
        float attractStrength = 10f;
        float drag = 30f;

        for (int i = 0;i < cells.Count; i++)
        {
            if (cells[i].role != CellRole.WhiteBlood) continue;

            Cell w = cells[i];

            Vector2 delta = player.nextPos - w.nextPos;
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

            if (c.role == CellRole.Player)
            {
                Gizmos.color = Color.green;
            }
            else if (c.role == CellRole.WhiteBlood)
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


