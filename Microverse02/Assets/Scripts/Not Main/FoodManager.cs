using System.Collections.Generic;
using UnityEngine;

public class FoodManager : MonoBehaviour
{
    [SerializeField] GameObject refToFoodSprite;
    [SerializeField] MapManager mapManager;
    [SerializeField] SardineFieldManager bacteriaFieldManager;

    [SerializeField] int foodCount = 5;
    [SerializeField] float foodRadius = 5f;
    [SerializeField] float triggerTrailSum = 20f;
    [SerializeField] float respawnCooldown = 0.2f;

    List<GameObject> foods = new List<GameObject>();
    List<float> foodCooldowns = new List<float>();

    void Start()
    {
        SpawnFoods();
    }

    void Update()
    {
       
    }

    void SpawnFoods()
    {
        if (mapManager == null || refToFoodSprite == null) return;

        Vector2 centre = mapManager.MapCentre;
        Vector2 size = mapManager.MapSize;

        float minX = centre.x - size.x * 0.5f ;
        float maxX = centre.x + size.x * 0.5f;
        float minY = centre.y - size.y * 0.5f;
        float maxY = centre.y + size.y * 0.5f;

        for (int i = 0; i < foodCount; i++)
        {
            Vector2 pos = GetRandomPosition(minX, maxX, minY, maxY);

            GameObject food = Instantiate(refToFoodSprite, pos, Quaternion.identity);
            foods.Add(food);
            foodCooldowns.Add(0f);
        }
    }

 
    void MoveFoodElsewhere(int foodIndex)
    {
        if (foodIndex < 0 || foodIndex >= foods.Count) return;
        if (foods[foodIndex] == null || mapManager == null) return;

        Vector2 centre = mapManager.MapCentre;
        Vector2 size = mapManager.MapSize;

        float minX = centre.x - size.x * 0.5f;
        float maxX = centre.x + size.x * 0.5f;
        float minY = centre.y - size.y * 0.5f;
        float maxY = centre.y + size.y * 0.5f;

        foods[foodIndex].transform.position = GetRandomPosition(minX, maxX, minY, maxY);
    }

    Vector2 GetRandomPosition(float minX, float maxX, float minY, float maxY)
    {
        float safeMinX = minX + foodRadius;
        float safeMaxX = maxX - foodRadius;
        float safeMinY = minY + foodRadius;
        float safeMaxY = maxY - foodRadius;

        for (int i = 0; i < 30; i++)
        {
            Vector2 pos = new Vector2(
                Random.Range(safeMinX, safeMaxX),
                Random.Range(safeMinY, safeMaxY)
            );

            if (mapManager != null && mapManager.IsWallWorld(pos))
                continue;

            return pos;
        }

        return new Vector2(
            Mathf.Clamp(mapManager.MapCentre.x, safeMinX, safeMaxX),
            Mathf.Clamp(mapManager.MapCentre.y, safeMinY, safeMaxY)
        );
    }

    public void RespawnAll()
    {
        foreach (var f in foods)
        {
            if (f != null) Destroy(f);
        }

        foods.Clear();
        foodCooldowns.Clear();
        SpawnFoods();
    }
}