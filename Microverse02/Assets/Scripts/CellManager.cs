
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.PlayerSettings;
using Vector2 = UnityEngine.Vector2;

public class CellManager : MonoBehaviour
{


    [Header("Defalut Settings")]
    public int organismCount = 20;
    public int WBCCount = 10;
    public float SceneGenerateSize = 20;


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


    [Header("cells | organisms Array")]
    List<Cell> cells = new List<Cell>();
    List<Organisms> organisms = new List<Organisms>();


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

        public bool infected;  
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
                ApplyCellInfection(i, otherIndex);
                ApplyCellPushing(i, otherIndex);
                
                //ApplyCohesion(i, otherIndex);

            }
        }

        if(Input.GetMouseButton(0))
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
            ApplyKeepShape();
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

        player.nextPos = pos;
        player.nextVelocity = Vector2.zero;

        player.cellRadius = 0.2f;
        player.detectRadius = player.cellRadius * 6f;

        player.organismId = -1;
        player.role = CellRole.Player;

        player.infected = true;
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
        
        Vector2 playerPos = GetPlayerNextPosition();


        for (int i = 0; i < organisms.Count; i++)
        {
            Organisms org = organisms[i];
            if (org.isDead) continue;
            //if(org.hp<=0f) continue;
            int coreIdx = org.coreIndex;
            if (coreIdx < 0) continue;

            Vector2 corePos = cells[coreIdx].nextPos;
            Vector2 toPlayer = playerPos - corePos;

            float d2 = toPlayer.sqrMagnitude;
            if (d2 < 1e-8f) continue;

            org.heading = toPlayer.normalized;
            org.headingPower = 70000f;

            Cell core = cells[coreIdx];
            core.nextVelocity += org.heading * org.headingPower * Time.deltaTime;
            cells[coreIdx] = core;
            organisms[i] = org;
        }
    }



    void ApplyKeepShape()
    {
        float dt = Time.deltaTime;

        float tolerance = 0.02f;

        float c = 1.1f;     // damping
        float maxForce = 200f;

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
            float k = (org.playerInside == 1) ? 3f : 10f; // spring
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
        if(a.role == CellRole.WhiteBlood || b.role == CellRole.WhiteBlood) return;

        Vector2 delta = b.nextPos - a.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float dist = Mathf.Sqrt(d2);
        float minDist = a.cellRadius+b.cellRadius;
        float maxDist = a.detectRadius+b.detectRadius;

        if (minDist < 1e-6f) return;

        float overlap = maxDist - dist;
        if (overlap <= 0f) return;

        Vector2 dir = delta / dist;

        float dt = Time.deltaTime;
        float pushStrength = 120f;

        Vector2 dv = dir * (overlap * pushStrength);

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

            if(penetration>maxPenetration) penetration = maxPenetration;

            Vector2 n = delta / dist;


            float v_n = Vector2.Dot(player.nextVelocity - core.nextVelocity, n); //player direction vs core direction
            // v_n > 0  = Moving in the same direction as n
            // v_n < 0  = Moving opposite to n
            // v_n == 0 = 90 degree 


            float accelMag = (k * penetration) - (c * v_n);
            if (accelMag <= 0f) continue;

            totalAccel += n * accelMag;
        }

        if(totalAccel.sqrMagnitude>maxAccel*maxAccel)
        {
            totalAccel = totalAccel.normalized*maxAccel;
        }
        player.nextVelocity = totalAccel * dt ;

        cells[playerCellIndex] = player;
    }

    void ApplyCellInfection(int a, int b)
    {
        Cell A = cells[a];
        Cell B = cells[b];

        if(A.infected)
        {
            float r = (A.detectRadius + B.cellRadius)*1.7f;
            bool inRange = ((A.nextPos - B.nextPos).sqrMagnitude <= r * r);
            B.infected = B.infected || inRange;
            cells[b] = B;
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
            if (cells[i].infected == true)
            {
                speed = Mathf.Lerp(55f, 0.0f, t);
            }
            else speed = Mathf.Lerp(20f, 0.0f, t);



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
        if(playerCellIndex < 0 ) return;    

        Cell player = cells[playerCellIndex];

        for (int i = 0; i < organisms.Count; i++)
        {
            var org = organisms[i];
            if(org.isDead) { organisms[i] = org; continue; }

            org.playerInside = 0;

            int coreIndex = org.coreIndex;
            if (coreIndex < 0 || coreIndex >= cells.Count)
            {
                organisms[i] = org; continue;
            }

            Cell coreCell = cells[coreIndex];
          
            Vector2 delta = coreCell.nextPos - player.nextPos;

            float d2 = delta.sqrMagnitude;

            if (d2 < 1e-8f)
            {
                organisms[i] = org; continue;
            }

            float minDist = player.cellRadius + org.coreDistance + coreCell.cellRadius;
            float minDist2 = minDist * minDist;

           if(d2<=minDist2)
           {
                org.playerInside = 1;

                if(d2 - coreCell.cellRadius <0.3f)
                {
                    org.isDead = true;
                }
           }

            organisms[i] = org;
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

            
            if (c.infected == true)
            {
                Gizmos.color = Color.red;
            }
            else if (c.role == CellRole.WhiteBlood)
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


