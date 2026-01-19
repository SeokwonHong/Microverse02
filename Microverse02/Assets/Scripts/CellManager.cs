
using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using Vector2 = UnityEngine.Vector2;

public class CellManager : MonoBehaviour
{

    [Header("Mouse and Player")]
    float mousePlayerDistance;
    private Vector2 mousePos;
    float playerSpeed;
    float normalSpeed=0;
    float maxSpeed=5f;
    float threshold=5f;


    private float playerRadius;
    private float playerInfluenceRadius;
    int playerCellIndex=-1; //-1 means player not allocated yet. If player is made, int number will be allocated
    [SerializeField] private float playerPushStrength =1.0f;  


    public float cellSpeed = 2f;

    //해쉬 
    SpatialHash spatialHash;
    [SerializeField] float BoxSize = 3f;


    private List<Cell> cells = new List<Cell>();
    List<Organisms> organisms = new List<Organisms>();

    enum CellRole { Player, Core, Shell, WhiteBlood}

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
    }

    //vector assume that there's two points and in the end of the point they have a invisible arrow
    //direction * power(magnitude)
    void Start()
    {
        spatialHash = new SpatialHash(BoxSize);

        

        CreatePlayerCell(Vector2.zero);
        playerRadius = GetPlayerRadius();
        playerInfluenceRadius = playerRadius*7f;

        //CreateOrganism(Vector2.zero); //*****************************************************************************
        CreateOrganism(new Vector2(  8f,   6f ));
        CreateOrganism(new Vector2(-9f, 7f));
        CreateOrganism(new Vector2(12f, -4f));
        CreateOrganism(new Vector2(-6f, -10f));
        CreateOrganism(new Vector2(15f, 3f));
        CreateOrganism(new Vector2(-14f, 2f));
        CreateOrganism(new Vector2(4f, 14f));
        CreateOrganism(new Vector2(-3f, -15f));
        CreateOrganism(new Vector2(18f, -12f));
        CreateOrganism(new Vector2(-17f, 11f));
        CreateOrganism(new Vector2(10f, 18f));
        CreateOrganism(new Vector2(-19f, -5f));


    }
/// <summary>
/// ////////////////////////////////////////////////////////////////////////////////////////////////
/// </summary>
    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))isOrganismDead=!isOrganismDead;
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
                ApplyCellPushing(i, otherIndex);
                //ApplyCohesion(i, otherIndex);

            }
        }
        ApplyPlayerInput();
        ApplyPlayerFunctions();
       
        
        ApplyOrganismTendency();
        ApplyCoreAnchor();

        


        for (int iter = 0; iter<3; iter++) // play eight times in one frame
        {
            ApplyCoreShellConstraints();
            ApplyOrganismJelly();
            for(int i = 0;i < cells.Count;i++) ResolvePlayerOverlap(i);
           
        }

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

        playerSpeed = Mathf.Lerp(normalSpeed, maxSpeed, Mathf.InverseLerp(0f, threshold, mousePlayerDistance));

        player.nextPos = Vector2.MoveTowards(GetPlayerNextPosition(), mousePos, playerSpeed * Time.deltaTime);

        return player;

    }
    void ApplyPlayerInput()
    {
        if(playerCellIndex<0)return;

        Cell player = cells[playerCellIndex];
        player = ApplyInput(player);
        player.nextVelocity = Vector2.zero;
        cells[playerCellIndex]= player;
    }
    public Vector2 GetPlayerPosition()
    {
        if(playerCellIndex<0)
        {
            return Vector2.zero;
        }
        return cells[playerCellIndex].currentPos;
    }
    Vector2 GetPlayerNextPosition()
    {
        if(playerCellIndex<0)
        {
            return Vector2.zero;
        }
        return cells[playerCellIndex].nextPos;
    }

    float GetPlayerRadius()
    {
        if(playerCellIndex<0)
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
        player.detectRadius = player.cellRadius*6f;

        player.organismId =-1;
        player.role=CellRole.Player;

        playerCellIndex = cells.Count;
        cells.Add(player);
    }
    void CreateOrganism(Vector2 currentPos)
    {
        Organisms org = new Organisms();
        int shellCount = 15;
        float coreDistance = 1.5f;

        org.id = organisms.Count;
        org.coreDistance = coreDistance;

        
        //core 

        Cell core = new Cell();
        core.currentPos = currentPos; 
        core.currentVelocity = Vector2.zero;

        core.cellRadius = 0.3f;
        core.detectRadius = core.cellRadius * 5f;

        core.organismId = org.id;
        core.role = CellRole.Core;

        int coreIndex = cells.Count; 
        cells.Add(core);

        org.coreIndex = coreIndex;
        org.members.Add(coreIndex);

        //Shell

        for (int i = 0; i < shellCount; i++)
        {
            float angle = (Mathf.PI * 2f) * (i / (float)shellCount); //(Mathf.PI * 2f) 는 각도로 이해 * 그걸 비율로 슬라이스
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); //각도를 normalized 된 벡터값으로 바꿈 그걸 해주는게 x 축인 cos , y 축인 sin
            //+ 계산하기 편하게 사분면에 표현가능하게 단위화
            Vector2 pos = currentPos + dir * coreDistance;

            Cell shell = new Cell();
            shell.currentPos = pos;
            shell.currentVelocity = Vector2.zero;

            //이부분부터 프로퍼티화해야할듯.
            shell.cellRadius = 0.15f;
            shell.detectRadius = shell.cellRadius * 5f;

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

    void ResolvePlayerOverlap(int cellIndex) //player - basic cell collision
    {
        var c = cells[cellIndex];
        if(c.role==CellRole.Player) return;
        if (c.role == CellRole.Core) return;

        Vector2 playerPos = GetPlayerNextPosition();

        Vector2 delta = c.nextPos - playerPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;
        
        float dist = Mathf.Sqrt(d2);
        float minDist = c.cellRadius + GetPlayerRadius();

        if(dist >=minDist) return;

        Vector2 dir = delta/ dist;
        float penetration = (minDist - dist);

        c.nextPos += dir * penetration * playerPushStrength;

        cells[cellIndex] = c;   
    }
    void ApplyCoreAnchor() //apply core cell an anchor
    {
        foreach (var org in organisms)
        {
            if(!org.anchorEnabled) continue; 

            int coreIdx = org.coreIndex;
            Cell core = cells[coreIdx];

            core.nextPos = org.anchorPos;
            core.nextVelocity = Vector2.zero;
            cells[coreIdx] = core;
        }
    }
    void ApplyCoreShellConstraints() // not through hash system- even if two cells are too far away, they'll detect still.
                                     // CHANGE FOR OPTIMIZATION???
                                     // CHANGE FOR OPTIMIZATION???
                                     // CHANGE FOR OPTIMIZATION???
                                     // CHANGE FOR OPTIMIZATION???
                                     // CHANGE FOR OPTIMIZATION???
                                     // CHANGE FOR OPTIMIZATION???
                                     // otherwise each organism must detect 50 cells per cell - crazy
    {
        
        foreach(var org in organisms)
        {
            if(org.isDead)continue;

            int coreIdx = org.coreIndex;
            if(coreIdx < 0) continue;

            for(int i =0; i<org.members.Count; i++)
            {
                int idx = org.members[i];
                if(idx==coreIdx) continue;

                ApplyKeepDistance(coreIdx, idx);
            }
        } 
            
    }
    void ApplyOrganismDeath() //function when the orgarnism is die
    {
        for (int i=0; i<organisms.Count; i++)  
        {
            var org = organisms[i];
            //if(org.hp>0f) continue; 


            if(!isOrganismDead)continue;

            if(org.isDead) continue;

            org.isDead = true;
            org.anchorEnabled = false;
            org.heading = Vector2.zero;
            org.headingPower = 0f;
            org.deadTimer=0;
            organisms[i] = org;
        }
    }
    void UpdateDeadOrganisms()
    {
        for(int i =0; i<organisms.Count; i++)
        {
            var org = organisms[i];
            if(!org.isDead) continue;

            org.deadTimer+=Time.deltaTime;
            if(org.deadTimer>=maxDeadTime)
            {
                org.deadTimer=maxDeadTime;
            }
            organisms[i]=org;
        }
    }
    void ApplyOrganismTendency() //Organism movement, more likely tendency
    {
        Vector2 playerPos = GetPlayerNextPosition();

        
        for (int i=0; i<organisms.Count; i++)
        {
            var org = organisms[i];
            if (org.isDead) continue;
            //if(org.hp<=0f) continue;
            int coreIdx = org.coreIndex;
            if (coreIdx < 0) continue;

            Vector2 corePos = cells[coreIdx].nextPos;
            Vector2 toPlayer = playerPos-corePos;

            float d2 = toPlayer.sqrMagnitude;
            if (d2 < 1e-8f) continue;

            org.heading = toPlayer.normalized;
            org.headingPower = 0.5f;

            Cell core = cells[coreIdx];
            core.nextPos += org.heading * org.headingPower * Time.deltaTime;
            cells[coreIdx] = core;
            organisms[i]=org;
        }
    }

    void ApplyKeepDistance(int CurrentIndex, int OtherIndex) //distance betwween core and shell - keep organism shape still  
        ////////
        ////////
        ////
        ///////
        /////
        /////// OPTIMIZATION
        //////
        /////
        ///////
        ////
        /////
    {
        Cell currentCell = cells[CurrentIndex];
        Cell otherCell = cells[OtherIndex];

        bool currentIsCore = currentCell.role == CellRole.Core;
        bool otherIsCore = otherCell.role == CellRole.Core;
        if(currentIsCore==otherIsCore) return;
        
        
        Cell core = currentIsCore? currentCell : otherCell; // if current is core, core. if current is not core, shell
        Cell shell = currentIsCore ? otherCell : currentCell; // if current is core, other is shell, if current is not core, other is core

        

        if(core.organismId != shell.organismId) return; //if organism is different, ignore

        float target = organisms[core.organismId].coreDistance; // appropritate distance
        float tolerance = 0.06f; // allow gap +-

        Vector2 delta = shell.nextPos - core.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-8f) return;

        float dist = Mathf.Sqrt(d2);
        float error = dist - target;
        float absErr = Mathf.Abs(error);
        if (absErr < tolerance) return; //  if core and shell distance is under control(tolerance can cover),
        // apply no power

        float rampRange = target * 0.5f;
        float t = Mathf.Clamp01((absErr - tolerance) / rampRange);
        float strength = t;
        Vector2 dir = delta / dist;
        Vector2 move = dir * (error * strength);

        shell.nextPos -= move;

        if(currentIsCore)
        {
            otherCell = shell;
            cells[OtherIndex] = otherCell;
        }
        else
        {
            currentCell = shell;
            cells[CurrentIndex] = currentCell;
        }
    }
    void ApplyCellPushing(int currentIndex, int otherIndex)
    {
        Cell Current = cells[currentIndex];
        Cell Other = cells[otherIndex];

        if(Current.role==CellRole.Player || Other.role==CellRole.Player) return;
        if (Current.role == CellRole.Core || Other.role == CellRole.Core) return;
        if (Current.organismId != Other.organismId) return;

        Vector2 delta = Other.nextPos - Current.nextPos;
        float d2 = delta.sqrMagnitude;
        if (d2 < 1e-5f) return;

        float distance = Mathf.Sqrt(d2);

        float minDist = Current.cellRadius+ Other.cellRadius;
        float maxDist = Current.detectRadius+ Other.detectRadius;

        if (distance <= minDist) return;
        if(distance > maxDist) return;

      
        Vector2 dir = delta / distance;

        float penetration = (maxDist - distance) * 0.1f;

        Vector2 push = dir * (penetration * 0.5f);

        Current.nextPos -= push;
        Other.nextPos += push;

        cells[currentIndex] = Current;
        cells[otherIndex] = Other;   
    }
    void ApplyOrganismJelly() //apply this to organisms instead of ApplyKeepDistance()?? 
    {

        if(playerCellIndex<0) return; //if player is not made yet, return. if player is successfully made using CreatePlayerCell(), playerCellIndex will be integer

        Cell player = cells[playerCellIndex];
        float dt = Time.deltaTime;

        float k = 800f; // spring strengh
        float c = 1.5f; // damping (bigger, more tough surface)
       

        for(int o=0; o<organisms.Count; o++)
        {
            var org = organisms[o];
            if(org.isDead) continue;

            Cell core = cells[org.coreIndex];

            float barrier = org.coreDistance+player.cellRadius;
            Vector2 delta= player.nextPos - core.nextPos;
            float d2 = delta.sqrMagnitude;
            if(d2<1e-2f)continue;

            float dist = Mathf.Sqrt(d2);
            float penetration = barrier- dist; // if player is inside of organism, penetration is integer. deeper = greater value
            if(penetration <= 1e-2f) penetration = 0f;

            Vector2 n = delta/dist;


            float x = penetration; //return smaller value. prevent player goes all the way in and bounce off heavily

            float v_n = Vector2.Dot(player.nextVelocity-core.nextVelocity,n); //player direction vs core direction
            // v_n > 0  = Moving in the same direction as n
            // v_n < 0  = Moving opposite to n
            // v_n == 0 = 90 degree 

            float accel = (k*x)-(c*v_n);
            if (accel <= 1e-2f)
            {
                accel = 0f;
                continue;
            }
            
            Vector2 pushingPower = n * accel * dt;

           

            player.nextVelocity += pushingPower;
            
            
        }
        cells[playerCellIndex]=player;
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
        
        for (int i=0; i<cells.Count; i++)
        {
            Cell c = cells[i];
            if(c.role==CellRole.Core) continue;
            if(c.organismId<0||c.organismId>=organisms.Count)continue;
            Organisms org = organisms[c.organismId];
            if (!org.isDead) continue;

            float t = Mathf.Clamp01(org.deadTimer/maxDeadTime);
        
            Vector2 ramdomDir = UnityEngine.Random.insideUnitCircle;
            if(ramdomDir.sqrMagnitude<1e-6f)continue;

            float speed = Mathf.Lerp(0.4f,0.0f,t);
            float drag = 9f;
            c.nextVelocity *= Mathf.Exp(-drag*Time.deltaTime);
            c.nextVelocity += ramdomDir*speed;
        
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
        

        for(int i =0; i<cells.Count;i++)
        {
            Cell c = cells[i];
            if(c.role !=CellRole.Shell) continue;

            Vector2 delta = c.nextPos - playerPos;
            float d2 = delta.sqrMagnitude;
            if(d2 >playerInfluenceRadius*playerInfluenceRadius) continue;

            float dist=Mathf.Sqrt(d2);
            if(dist<1e-5f) continue;


            Vector2 dir = delta/dist;
            float force = (playerInfluenceRadius - dist)/playerInfluenceRadius;

            c.nextPos += dir*force*playerPushStrength*Time.deltaTime;
            cells[i]=c;

        }
    }
    #endregion


    void OnDrawGizmos()
    {
        if(!Application.isPlaying) return; 
        if (cells == null||cells.Count==0)
        {
            Debug.Log("there's no cell list!");
            return;
        }

        
        foreach (Cell c in cells)
        {
            switch (c.role)
        {
            case CellRole.Player:
                Gizmos.color = Color.white;
                break;
            case CellRole.Core:
                Gizmos.color = Color.green; 
                break;
            default: // Shell
                Gizmos.color = Color.green;
                break;
        }
            Gizmos.DrawSphere(c.currentPos, c.cellRadius);
        }
    }
}



