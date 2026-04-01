using UnityEngine;

[ExecuteAlways]
public class MapManager : MonoBehaviour
{
    public enum PaintMode
    {
        Draw,
        Erase
    }

    [Header("Grid")]
    int gridWidth = 400;
     int gridHeight = 400;

    [Header("World")]
    [SerializeField] Vector2 mapCentre = Vector2.zero;
    [SerializeField] Vector2 mapSize = new Vector2(50f, 50f);

    [Header("Paint")]
    [SerializeField] int brushRadius = 3;
    [SerializeField] PaintMode paintMode = PaintMode.Draw;

    [Header("Visual")]
    [SerializeField] Renderer targetRenderer;
    [SerializeField] FilterMode filterMode = FilterMode.Point;
    [SerializeField] Color emptyColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] Color wallColor = Color.white;

    [SerializeField] Color wallEdgeColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] Color wallInnerColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [SerializeField] int edgeWidthInCells = 6;

    [Header("bg")]
    [SerializeField] GameObject refToBg;

    [SerializeField, HideInInspector] byte[] wallField;

    Texture2D wallTexture;
    Color[] pixels;

    public int Width => gridWidth;
    public int Height => gridHeight;
    public int CellCount => gridWidth * gridHeight;
    public int BrushRadius => brushRadius;

    public float CellWidth => mapSize.x / gridWidth;
    public float CellHeight => mapSize.y / gridHeight;
    public float BrushRadiusWorld => Mathf.Max(CellWidth, CellHeight) * brushRadius;

    public Vector2 MapCentre => mapCentre;
    public Vector2 MapSize => mapSize;
    public PaintMode CurrentPaintMode => paintMode;

    void OnEnable()
    {
        refToBg.transform.localScale = mapSize;


        EnsureInitialised();
        SyncRendererToMap();
        RebuildTexture();
    }

    void OnValidate()
    {
        if (brushRadius < 1) brushRadius = 1;

        float safeHeight = Mathf.Max(0.0001f, mapSize.y);
        float aspect = mapSize.x / safeHeight;
        gridHeight = Mathf.Max(1, Mathf.RoundToInt(gridWidth / aspect));

        EnsureInitialised();
        SyncRendererToMap();
        RebuildTexture();
    }

    void EnsureInitialised()
    {
        int count = CellCount;

        if (wallField == null || wallField.Length != count)
            wallField = new byte[count];

        if (pixels == null || pixels.Length != count)
            pixels = new Color[count];

        if (wallTexture == null || wallTexture.width != gridWidth || wallTexture.height != gridHeight)
        {
            wallTexture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false);
            wallTexture.filterMode = filterMode;
            wallTexture.wrapMode = TextureWrapMode.Clamp;
            wallTexture.name = "MapTexture";
        }

        ApplyTextureToRenderer();
    }

    public void SetPaintMode(PaintMode mode)
    {
        paintMode = mode;
    }

    public void PaintWorld(Vector2 worldPos, int radius, byte value)
    {
        Vector2Int centreCell = WorldToCell(worldPos);

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > radius * radius)
                    continue;

                int cx = centreCell.x + x;
                int cy = centreCell.y + y;

                if (!InBounds(cx, cy))
                    continue;

                wallField[ToIndex(cx, cy)] = value;
            }
        }

        RebuildTexture();
    }

    public void PaintAtCurrentMode(Vector2 worldPos, int radius)
    {
        byte value = paintMode == PaintMode.Draw ? (byte)1 : (byte)0;
        PaintWorld(worldPos, radius, value);
    }

    public bool IsWallWorld(Vector2 worldPos)
    {
        Vector2Int cell = WorldToCell(worldPos);
        if (!InBounds(cell.x, cell.y)) return false;
        return wallField[ToIndex(cell.x, cell.y)] == 1;
    }

    public void ClearAll()
    {
        EnsureInitialised();

        for (int i = 0; i < wallField.Length; i++)
            wallField[i] = 0;

        RebuildTexture();
    }

    public void FillAll()
    {
        EnsureInitialised();

        for (int i = 0; i < wallField.Length; i++)
            wallField[i] = 1;

        RebuildTexture();
    }

    public void RebuildTexture()
    {
        EnsureInitialised();

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int i = ToIndex(x, y);

                if (wallField[i] == 0)
                {
                    pixels[i] = emptyColor;
                    continue;
                }

                int distToEdge = DistanceToEmpty(x, y, edgeWidthInCells);
                float t = Mathf.Clamp01(distToEdge / (float)edgeWidthInCells);

                pixels[i] = Color.Lerp(wallEdgeColor, wallInnerColor, t);
            }
        }

        wallTexture.SetPixels(pixels);
        wallTexture.Apply();

        ApplyTextureToRenderer();
    }
    int DistanceToEmpty(int centreX, int centreY, int maxDistance)
    {
        for (int r = 1; r <= maxDistance; r++)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    int cx = centreX + x;
                    int cy = centreY + y;

                    if (!InBounds(cx, cy))
                        return r - 1;

                    if (wallField[ToIndex(cx, cy)] == 0)
                        return r - 1;
                }
            }
        }

        return maxDistance;
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

        float cellWidth = mapSize.x / gridWidth;
        float cellHeight = mapSize.y / gridHeight;

        return new Vector2(
            min.x + (x + 0.5f) * cellWidth,
            min.y + (y + 0.5f) * cellHeight
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

    void SyncRendererToMap()
    {
        if (targetRenderer == null) return;

        Transform t = targetRenderer.transform;
        t.position = new Vector3(mapCentre.x, mapCentre.y, 0f);
        t.localScale = new Vector3(mapSize.x, mapSize.y, 1f);
    }

    void ApplyTextureToRenderer()
    {
        if (targetRenderer == null) return;

        Material mat = Application.isPlaying ? targetRenderer.material : targetRenderer.sharedMaterial;
        if (mat == null) return;

        mat.mainTexture = wallTexture;
        mat.mainTextureScale = Vector2.one;
        mat.mainTextureOffset = Vector2.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(mapCentre, new Vector3(mapSize.x, mapSize.y, 0f));
    }
}