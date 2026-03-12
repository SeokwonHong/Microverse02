using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class WallDrawing : MonoBehaviour
{
    
    Vector2 mapCentre = Vector2.zero;
    float mapRadius = 50f;

    [Header("Wall Drawing")]
    float[] mapField;
    int mapWidth = 512;
    int mapHeight = 512;    
    int MapSize => mapWidth * mapHeight;

    [Header("Mouse Pos")]
    MouseCursor mousePos;




    [SerializeField] Color mapColour;

    bool isDrawn;

    private void Awake()
    {
        int mapSize = mapWidth * mapHeight;

        mapField = new float[MapSize];
  
    }

    // Update is called once per frame
    void Update()
    {
        
    }



}
