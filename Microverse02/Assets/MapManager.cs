using UnityEngine;

[ExecuteAlways]
public class MapManager : MonoBehaviour
{
    [Header("Grid")]
    int gridWidth = 200;
    int gridHeight = 200;

    [Header("World")]
    [SerializeField] Vector2 mapCentre = Vector2.zero;
    [SerializeField] Vector2 mapSize = new Vector2(50f, 50f);

    [SerializeField, HideInInspector] byte[] wallField;

    public int Width => gridWidth;
    public int Height => gridHeight;
    public int CellCount => gridWidth * gridHeight;

    public float CellWidth => mapSize.x / gridWidth;
    public float CellHeight => mapSize.y / gridHeight;

    public Vector2 MapCentre => mapCentre;
    public Vector2 MapSize => mapSize;

    void OnEnable()
    {
        EnsureInitialised();
    }

    void OnValidate()
    {
        float safeHeight = Mathf.Max(0.0001f, mapSize.y);
        float aspect = mapSize.x / safeHeight;
        gridHeight = Mathf.Max(1, Mathf.RoundToInt(gridWidth / aspect));

        EnsureInitialised();
    }

    void EnsureInitialised()
    {
        int count = CellCount;

        if (wallField == null || wallField.Length != count)
            wallField = new byte[count];
    }

    public bool IsWallWorld(Vector2 worldPos)
    {
        Vector2Int cell = WorldToCell(worldPos);
        if (!InBounds(cell.x, cell.y)) return false;
        return wallField[ToIndex(cell.x, cell.y)] == 1;
    }

    public Vector2Int WorldToCell(Vector2 worldPos)
    {
        Vector2 min = mapCentre - mapSize * 0.5f;
        Vector2 max = mapCentre + mapSize * 0.5f;

        float tx = Mathf.InverseLerp(min.x, max.x, worldPos.x);
        float ty = Mathf.InverseLerp(min.y, max.y, worldPos.y);

        int x = Mathf.Clamp(Mathf.FloorToInt(tx * gridWidth), 0, gridWidth - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(ty * gridHeight), 0, gridHeight - 1);

        return new Vector2Int(x, y);
    }

    public Vector2 CellToWorld(int x, int y)
    {
        Vector2 min = mapCentre - mapSize * 0.5f;

        return new Vector2(
            min.x + (x + 0.5f) * CellWidth,
            min.y + (y + 0.5f) * CellHeight
        );
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

    int ToIndex(int x, int y)
    {
        return y * gridWidth + x;
    }

}