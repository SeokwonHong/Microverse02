using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;

public class PlanktonManager : MonoBehaviour
{
    [SerializeField] OceanFieldManager oceanFieldManager;
    [SerializeField] MapManager mapManager;

    [Header("Spawn")]
    [SerializeField] int startClusterCount = 20;
    [SerializeField] int pointsPerCluster = 25;
    [SerializeField] float clusterRadius = 2.5f;
    [SerializeField] float amountMultiplier = 1f;



    void Start()
    {
        SpawnInitialPlankton();
    }

    void Update()
    {
        if (oceanFieldManager == null)
            return;


    }

    void SpawnInitialPlankton()
    {
        if (oceanFieldManager == null || mapManager == null)
            return;

        for (int i = 0; i < startClusterCount; i++)
        {
            Vector2 centre = GetRandomWorldPoint();

            for (int j = 0; j < pointsPerCluster; j++)
            {
                Vector2 offset = Random.insideUnitCircle * clusterRadius;
                Vector2 p = centre + offset;

                if (mapManager.IsWallWorld(p))
                    continue;

                oceanFieldManager.AddPlankton(p, amountMultiplier);
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

        return new Vector2(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY)
        );
    }
}