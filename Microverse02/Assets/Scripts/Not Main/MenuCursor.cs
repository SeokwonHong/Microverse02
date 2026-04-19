using UnityEngine;

public class MenuCursor : MonoBehaviour
{
    [SerializeField] RectTransform cursor;   
    [SerializeField] Camera cam;
    [SerializeField] MapManager mapManager;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
        if (cursor == null || cam == null || mapManager == null)
            return;


        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = -cam.transform.position.z;
        Vector3 world = cam.ScreenToWorldPoint(mouseScreen);

        Vector2 half = mapManager.MapSize * 0.5f;

        float minX = mapManager.MapCentre.x - half.x+2.9f;
        float maxX = mapManager.MapCentre.x + half.x-2.9f;
        float minY = mapManager.MapCentre.y - half.y+2.9f;
        float maxY = mapManager.MapCentre.y + half.y-2.9f;

        world.x = Mathf.Clamp(world.x, minX, maxX);
        world.y = Mathf.Clamp(world.y, minY, maxY);

 
        Vector3 clampedScreen = cam.WorldToScreenPoint(world);

        cursor.position = clampedScreen;
    }
}