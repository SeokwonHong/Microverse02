using System.Collections.Generic;
using UnityEngine;

public class FoodManager : MonoBehaviour
{
    [SerializeField] GameObject refToFoodSprite;
    [SerializeField] MapManager mapManager;

    [SerializeField] int foodCount = 5;

    List<GameObject> foods = new List<GameObject>();

    void Start()
    {
        SpawnFoods();
    }

    void SpawnFoods()
    {
        if (mapManager == null || refToFoodSprite == null) return;

        Vector2 centre = mapManager.MapCentre;
        Vector2 size = mapManager.MapSize;

        float minX = centre.x - size.x * 0.5f;
        float maxX = centre.x + size.x * 0.5f;
        float minY = centre.y - size.y * 0.5f;
        float maxY = centre.y + size.y * 0.5f;

        for (int i = 0; i < foodCount; i++)
        {
            Vector2 pos = new Vector2(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY)
            );

            GameObject food = Instantiate(refToFoodSprite, pos, Quaternion.identity);
            foods.Add(food);
        }
    }

    // optional: call this later when food eaten
    public void RespawnAll()
    {
        foreach (var f in foods)
        {
            if (f != null) Destroy(f);
        }

        foods.Clear();
        SpawnFoods();
    }
}