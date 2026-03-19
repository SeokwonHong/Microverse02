using UnityEngine;
using Vector2 = UnityEngine.Vector2;

[ExecuteAlways]
public class WallManager : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] int gridWidth = 400;
    [SerializeField] int gridHeight = 400;
    [SerializeField] float mapRadius = 50f;
    [SerializeField] Vector2 mapCentre = Vector2.zero;

    [Header("Visual")]
    [SerializeField] Renderer wallRenderer;
    [SerializeField] Color emptyColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] Color wallColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] FilterMode filterMode = FilterMode.Point;

    [Header("Paint")]
    [SerializeField] int defaultBrushRadius = 2;

    byte[] wallField;
    Color[] wallPixels;
    Texture2D wallTexture;

    public int Width => gridWidth;
    public int Height => gridHeight;
    public int CellCount => gridWidth * gridHeight;

    void Awake()
    {
        EnsureInitialized();
    }

    void OnEnable()
    {
        EnsureInitialized();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureInitialized();
        RefreshTexture();
    }
#endif

    void EnsureInitialized()
    {
        if (gridWidth <= 0) gridWidth = 1;
        if (gridHeight <= 0) gridHeight = 1;

        int count = CellCount;

        if (wallField == null || wallField.Length != count)
            wallField = new byte[count];

        if (wallPixels == null || wallPixels.Length != count)
            wallPixels = new Color[count];

        if (wallTexture == null || wallTexture.width != gridWidth || wallTexture.height != gridHeight)
        {
            wallTexture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false);
            wallTexture.wrapMode = TextureWrapMode.Clamp;
            wallTexture.filterMode = filterMode;
        }

        if (wallRenderer != null)
        {
            wallRenderer.sharedMaterial.mainTexture = wallTexture;
            wallRenderer.transform.position = new Vector3(mapCentre.x, mapCentre.y, 0f);
            wallRenderer.transform.localScale = new Vector3(mapRadius * 2f, mapRadius * 2f, 1f);
        }
    }

    int Index(int x, int y)
    {
        return x + y * gridWidth;
    }

    public bool WorldToGrid(Vector2 worldPos, out int gx, out int gy)
    {
        float mapSize = mapRadius * 2f;

        float px = (worldPos.x - (mapCentre.x - mapRadius)) / mapSize;
        float py = (worldPos.y - (mapCentre.y - mapRadius)) / mapSize;

        gx = Mathf.FloorToInt(px * gridWidth);
        gy = Mathf.FloorToInt(py * gridHeight);

        bool inside = gx >= 0 && gx < gridWidth && gy >= 0 && gy < gridHeight;

        if (!inside)
        {
            gx = -1;
            gy = -1;
        }

        return inside;
    }

    public Vector2 GridToWorld(int gx, int gy)
    {
        float mapSize = mapRadius * 2f;

        float px = (gx + 0.5f) / gridWidth;
        float py = (gy + 0.5f) / gridHeight;

        float wx = (mapCentre.x - mapRadius) + px * mapSize;
        float wy = (mapCentre.y - mapRadius) + py * mapSize;

        return new Vector2(wx, wy);
    }

    public bool IsInsideGrid(int gx, int gy)
    {
        return gx >= 0 && gx < gridWidth && gy >= 0 && gy < gridHeight;
    }

    public bool IsWallCell(int gx, int gy)
    {
        EnsureInitialized();

        if (!IsInsideGrid(gx, gy)) return true;
        return wallField[Index(gx, gy)] != 0;
    }

    public bool IsWallAtWorld(Vector2 worldPos)
    {
        EnsureInitialized();

        if (!WorldToGrid(worldPos, out int gx, out int gy))
            return true;

        return wallField[Index(gx, gy)] != 0;
    }

    public void SetWallCell(int gx, int gy, bool isWall)
    {
        EnsureInitialized();

        if (!IsInsideGrid(gx, gy)) return;
        wallField[Index(gx, gy)] = (byte)(isWall ? 1 : 0);
    }

    public void PaintWall(Vector2 worldPos, int brushRadius = -1)
    {
        PaintCircle(worldPos, brushRadius < 0 ? defaultBrushRadius : brushRadius, true);
    }

    public void EraseWall(Vector2 worldPos, int brushRadius = -1)
    {
        PaintCircle(worldPos, brushRadius < 0 ? defaultBrushRadius : brushRadius, false);
    }

    public void PaintCircle(Vector2 worldPos, int radius, bool isWall)
    {
        EnsureInitialized();

        if (!WorldToGrid(worldPos, out int cx, out int cy)) return;

        int r = Mathf.Max(0, radius);
        int r2 = r * r;

        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > r2) continue;

                int gx = cx + x;
                int gy = cy + y;

                if (!IsInsideGrid(gx, gy)) continue;

                wallField[Index(gx, gy)] = (byte)(isWall ? 1 : 0);
            }
        }
    }

    public void PaintLine(Vector2 worldA, Vector2 worldB, int brushRadius, bool isWall)
    {
        EnsureInitialized();

        if (!WorldToGrid(worldA, out int ax, out int ay)) return;
        if (!WorldToGrid(worldB, out int bx, out int by)) return;

        int dx = Mathf.Abs(bx - ax);
        int dy = Mathf.Abs(by - ay);
        int sx = ax < bx ? 1 : -1;
        int sy = ay < by ? 1 : -1;
        int err = dx - dy;

        int x = ax;
        int y = ay;

        while (true)
        {
            PaintCircle(GridToWorld(x, y), brushRadius, isWall);

            if (x == bx && y == by) break;

            int e2 = err * 2;

            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    public void ClearAllWalls()
    {
        EnsureInitialized();

        for (int i = 0; i < wallField.Length; i++)
            wallField[i] = 0;

        RefreshTexture();
    }

    public void FillAllWalls()
    {
        EnsureInitialized();

        for (int i = 0; i < wallField.Length; i++)
            wallField[i] = 1;

        RefreshTexture();
    }

    public void RefreshTexture()
    {
        EnsureInitialized();
        if (wallTexture == null) return;

        for (int i = 0; i < CellCount; i++)
        {
            wallPixels[i] = wallField[i] != 0 ? wallColor : emptyColor;
        }

        wallTexture.SetPixels(wallPixels);
        wallTexture.Apply(false);
    }

    public bool ResolveCircleCollision(ref Vector2 pos, ref Vector2 vel, float radius, float bounciness)
    {
        EnsureInitialized();

        if (!WorldToGrid(pos, out int cx, out int cy))
            return false;

        int cellRadius = Mathf.CeilToInt((radius / (mapRadius * 2f)) * gridWidth) + 1;
        bool hit = false;
        Vector2 totalPush = Vector2.zero;

        for (int y = cy - cellRadius; y <= cy + cellRadius; y++)
        {
            for (int x = cx - cellRadius; x <= cx + cellRadius; x++)
            {
                if (!IsWallCell(x, y)) continue;

                Vector2 cellCenter = GridToWorld(x, y);

                float cellSizeX = (mapRadius * 2f) / gridWidth;
                float cellSizeY = (mapRadius * 2f) / gridHeight;
                Vector2 half = new Vector2(cellSizeX * 0.5f, cellSizeY * 0.5f);

                Vector2 closest = new Vector2(
                    Mathf.Clamp(pos.x, cellCenter.x - half.x, cellCenter.x + half.x),
                    Mathf.Clamp(pos.y, cellCenter.y - half.y, cellCenter.y + half.y)
                );

                Vector2 delta = pos - closest;
                float d2 = delta.sqrMagnitude;

                if (d2 >= radius * radius) continue;

                float dist = Mathf.Sqrt(d2);
                Vector2 n;

                if (dist > 0.0001f)
                {
                    n = delta / dist;
                }
                else
                {
                    n = (pos - cellCenter).sqrMagnitude > 0.0001f
                        ? (pos - cellCenter).normalized
                        : Vector2.up;
                }

                float penetration = radius - dist;
                totalPush += n * penetration;
                hit = true;
            }
        }

        if (hit)
        {
            pos += totalPush;

            Vector2 n = totalPush.sqrMagnitude > 0.0001f ? totalPush.normalized : Vector2.up;
            float vn = Vector2.Dot(vel, n);

            if (vn < 0f)
            {
                vel = vel - 2f * vn * n;
                vel *= bounciness;
            }
        }

        return hit;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(mapCentre, new Vector3(mapRadius * 2f, mapRadius * 2f, 0f));
    }
}