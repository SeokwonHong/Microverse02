using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;

public class PlanktonManager : MonoBehaviour
{
    [SerializeField] OceanFieldManager oceanFieldManager;
    [SerializeField] MapManager mapManager;

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

        SpawnInitialClusters();
        previousPlanktonTotal = oceanFieldManager.GetTotalPlankton();
    }

    void Update()
    {
        if (oceanFieldManager == null) return;

        int currentTotal = oceanFieldManager.GetTotalPlankton();
        int targetTotal = startClusterCount * pointsPerCluster;
        int deficit = targetTotal - currentTotal;

  
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

        float minX = centre.x - size.x * 0.5f;
        float maxX = centre.x + size.x * 0.5f;
        float minY = centre.y - size.y * 0.5f;
        float maxY = centre.y + size.y * 0.5f;

        return new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
    }
}