using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;

public class PlanktonManager : MonoBehaviour
{
    [SerializeField] OceanFieldManager oceanFieldManager;
    [SerializeField] MapManager mapManager;
    [SerializeField] bool spawnOnStart = false;

    [Header("Spawn Settings")]
    [SerializeField] int startClusterCount = 20;
    [SerializeField] int pointsPerCluster = 25;
    [SerializeField] float clusterRadius = 2.5f;

    private const int amountPerSpawn = 1;

    float previousPlanktonTotal;
    float planktonLossSinceLastSpawn;

    void Start()
    {
        if (oceanFieldManager == null || mapManager == null)
            return;

        if (spawnOnStart)
            SpawnInitialClusters();

        previousPlanktonTotal = oceanFieldManager.GetTotalPlankton();
    }

    void Update()
    {
        if (oceanFieldManager == null) return;

        //if (Input.GetKeyDown(KeyCode.N))
        //{
        //    int currentTotal = oceanFieldManager.GetTotalPlankton();
        //    Debug.Log("Current plankton total: " + currentTotal);
        //}

        if (!spawnOnStart) return;

        int currentTotalForSpawn = oceanFieldManager.GetTotalPlankton();
        int targetTotal = startClusterCount * pointsPerCluster;
        int deficit = targetTotal - currentTotalForSpawn;

        if (deficit >= pointsPerCluster)
        {
            SpawnOneCluster();
        }
    }

    void SpawnInitialClusters()
    {
        for (int i = 0; i < startClusterCount; i++)
        {
            SpawnOneCluster();
        }
    }

    void SpawnOneCluster()
    {
        Vector2 centre = GetRandomWorldPoint();
        int placedCount = 0;
        int totalAttempts = 0;
        int maxAttempts = 200; 

        while (placedCount < pointsPerCluster && totalAttempts < maxAttempts)
        {
            totalAttempts++;
            Vector2 offset = Random.insideUnitCircle * clusterRadius;
            Vector2 p = centre + offset;

 
            if (oceanFieldManager.AddPlankton(p, amountPerSpawn))
            {
                placedCount++;
            }
        }
    }

    Vector2 GetRandomWorldPoint()
    {
        Vector2 centre = mapManager.MapCentre;
        Vector2 size = mapManager.MapSize;

        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        float minX = centre.x - halfW + clusterRadius;
        float maxX = centre.x + halfW - clusterRadius;
        float minY = centre.y - halfH + clusterRadius;
        float maxY = centre.y + halfH - clusterRadius;

        if (minX > maxX) minX = maxX = centre.x;
        if (minY > maxY) minY = maxY = centre.y;

        return new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
    }

    // for tutorial
    public void SpawnTutorialCluster()
    {
        SpawnOneCluster();
    }
}