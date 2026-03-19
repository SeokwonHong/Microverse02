using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class WallManager : MonoBehaviour
{

    [SerializeField] bool drawRuntimeWalls = true;
    [SerializeField] Material wallMaterial;

    readonly List<Vector2> cachedPointAPos = new List<Vector2>();
    readonly List<Vector2> cachedPointBPos = new List<Vector2>();

    float rebuildTimer;

    [System.Serializable]
    public class WallAuthoring
    {
        public Transform pointA;
        public Transform pointB;
        [Min(0.01f)] public float thickness = 0.35f;
    }

    public struct WallSegment
    {
        public Vector2 a;
        public Vector2 b;
        public float radius;

        public WallSegment(Vector2 a, Vector2 b, float radius)
        {
            this.a = a;
            this.b = b;
            this.radius = radius;
        }
    }

    [Header("Authoring")]
    [SerializeField] List<WallAuthoring> authoredWalls = new List<WallAuthoring>();

    [Header("Debug")]
    [SerializeField] bool drawGizmos = true;
    //[SerializeField] Color wallColor = new Color(1f, 0.85f, 0.2f, 1f);
    //[SerializeField] Color wallFillColor = new Color(1f, 0.85f, 0.2f, 0.15f);

    [Header("Hash")]
    [SerializeField] float hashCellSize = 2f;

    readonly List<WallSegment> runtimeWalls = new List<WallSegment>();
    readonly Dictionary<Vector2Int, List<int>> wallHash = new Dictionary<Vector2Int, List<int>>();
    readonly HashSet<int> queryDedup = new HashSet<int>();

    List<GameObject> wallVisuals = new List<GameObject>();
    public int WallCount => runtimeWalls.Count;
    public IReadOnlyList<WallSegment> Walls => runtimeWalls;

    void Awake()
    {
        BuildWalls();
        BuildWallVisuals();
        CacheWallPoints();
    }

    private void Update()
    {
        if (DidAnyWallPointMove())
        {
            BuildWalls();
            BuildWallVisuals();
            CacheWallPoints();
        }
    }
#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
            BuildWalls();
    }
#endif

    public void BuildWalls()
    {
        runtimeWalls.Clear();
        wallHash.Clear();

        for (int i = 0; i < authoredWalls.Count; i++)
        {
            WallAuthoring w = authoredWalls[i];
            if (w == null || w.pointA == null || w.pointB == null) continue;

            Vector2 a = w.pointA.position;
            Vector2 b = w.pointB.position;
            float r = Mathf.Max(0.01f, w.thickness * 0.5f);

            runtimeWalls.Add(new WallSegment(a, b, r));
        }

        BuildHash();
    }
    void BuildWallVisuals()
    {
        if (!drawRuntimeWalls) return;

        foreach (var v in wallVisuals)
            if (v != null) Destroy(v);

        wallVisuals.Clear();

        for (int i = 0; i < runtimeWalls.Count; i++)
        {
            WallSegment w = runtimeWalls[i];

            Vector2 dir = w.b - w.a;
            float length = dir.magnitude;
            float thickness = w.radius * 2f;

            Vector2 mid = (w.a + w.b) * 0.5f;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(transform);

            quad.transform.position = new Vector3(mid.x, mid.y, 1f);
            quad.transform.rotation = Quaternion.Euler(0, 0, angle);
            quad.transform.localScale = new Vector3(length, thickness, 1f);

            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            if (wallMaterial != null)
                quad.GetComponent<MeshRenderer>().material = wallMaterial;

            wallVisuals.Add(quad);
        }
    }
    void BuildHash()
    {
        for (int i = 0; i < runtimeWalls.Count; i++)
        {
            WallSegment w = runtimeWalls[i];

            float minX = Mathf.Min(w.a.x, w.b.x) - w.radius;
            float maxX = Mathf.Max(w.a.x, w.b.x) + w.radius;
            float minY = Mathf.Min(w.a.y, w.b.y) - w.radius;
            float maxY = Mathf.Max(w.a.y, w.b.y) + w.radius;

            Vector2Int minCell = WorldToCell(new Vector2(minX, minY));
            Vector2Int maxCell = WorldToCell(new Vector2(maxX, maxY));

            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    Vector2Int key = new Vector2Int(x, y);

                    if (!wallHash.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>(4);
                        wallHash.Add(key, list);
                    }

                    list.Add(i);
                }
            }
        }
    }

    Vector2Int WorldToCell(Vector2 p)
    {
        return new Vector2Int(
            Mathf.FloorToInt(p.x / hashCellSize),
            Mathf.FloorToInt(p.y / hashCellSize)
        );
    }

    public void QueryNearbyWalls(Vector2 pos, float radius, List<int> results)
    {
        results.Clear();
        queryDedup.Clear();

        Vector2Int min = WorldToCell(pos - Vector2.one * radius);
        Vector2Int max = WorldToCell(pos + Vector2.one * radius);

        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                Vector2Int key = new Vector2Int(x, y);

                if (!wallHash.TryGetValue(key, out List<int> list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    int wallIndex = list[i];
                    if (queryDedup.Add(wallIndex))
                        results.Add(wallIndex);
                }
            }
        }
    }

    public bool ResolveCircleAgainstNearbyWalls(
        ref Vector2 pos,
        ref Vector2 vel,
        float circleRadius,
        float bounciness,
        List<int> wallBuffer,
        out Vector2 wallNormal)
    {
        wallNormal = Vector2.zero;
        bool hitAny = false;

        QueryNearbyWalls(pos, circleRadius + hashCellSize, wallBuffer);

        for (int i = 0; i < wallBuffer.Count; i++)
        {
            WallSegment wall = runtimeWalls[wallBuffer[i]];

            Vector2 closest = ClosestPointOnSegment(pos, wall.a, wall.b);
            Vector2 delta = pos - closest;
            float d2 = delta.sqrMagnitude;

            float minDist = circleRadius + wall.radius;
            float minDist2 = minDist * minDist;

            if (d2 >= minDist2) continue;

            Vector2 n;
            float dist = Mathf.Sqrt(d2);

            if (dist > 0.0001f)
            {
                n = delta / dist;
            }
            else
            {
                Vector2 line = wall.b - wall.a;
                if (line.sqrMagnitude < 0.0001f)
                    n = Vector2.up;
                else
                {
                    line.Normalize();
                    n = new Vector2(-line.y, line.x);
                }
            }

            float penetration = minDist - dist;
            pos += n * penetration;

            float vn = Vector2.Dot(vel, n);
            if (vn < 0f)
            {
                vel = vel - 2f * vn * n;
                vel *= bounciness;
            }

            wallNormal = n;
            hitAny = true;
        }

        return hitAny;
    }

    public static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float ab2 = ab.sqrMagnitude;

        if (ab2 <= 0.000001f) return a;

        float t = Vector2.Dot(p - a, ab) / ab2;
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }



    List<WallSegment> GetEditorWalls()
    {
        List<WallSegment> list = new List<WallSegment>();

        for (int i = 0; i < authoredWalls.Count; i++)
        {
            WallAuthoring w = authoredWalls[i];
            if (w == null || w.pointA == null || w.pointB == null) continue;

            list.Add(new WallSegment(
                w.pointA.position,
                w.pointB.position,
                Mathf.Max(0.01f, w.thickness * 0.5f)
            ));
        }

        return list;
    }

    void CacheWallPoints()
    {
        cachedPointAPos.Clear();
        cachedPointBPos.Clear();

        for (int i = 0; i < authoredWalls.Count; i++)
        {
            WallAuthoring w = authoredWalls[i];

            if (w == null || w.pointA == null || w.pointB == null)
            {
                cachedPointAPos.Add(Vector2.zero);
                cachedPointBPos.Add(Vector2.zero);
                continue;
            }

            cachedPointAPos.Add(w.pointA.position);
            cachedPointBPos.Add(w.pointB.position);
        }
    }

    bool DidAnyWallPointMove()
    {
        if (authoredWalls.Count != cachedPointAPos.Count) return true;

        for (int i = 0; i < authoredWalls.Count; i++)
        {
            WallAuthoring w = authoredWalls[i];
            if (w == null || w.pointA == null || w.pointB == null) return true;

            if ((Vector2)w.pointA.position != cachedPointAPos[i]) return true;
            if ((Vector2)w.pointB.position != cachedPointBPos[i]) return true;
        }

        return false;
    }

    public void AddRuntimeWall(Vector2 a, Vector2 b, float thickness)
    {
        float radius = Mathf.Max(0.01f, thickness * 0.5f);

        runtimeWalls.Add(new WallSegment(a, b, radius));

        int wallIndex = runtimeWalls.Count - 1;
        AddWallToHash(runtimeWalls[wallIndex], wallIndex);

        if (drawRuntimeWalls)
            CreateWallVisual(runtimeWalls[wallIndex]);
    }

    void AddWallToHash(WallSegment w, int wallIndex)
    {
        float minX = Mathf.Min(w.a.x, w.b.x) - w.radius;
        float maxX = Mathf.Max(w.a.x, w.b.x) + w.radius;
        float minY = Mathf.Min(w.a.y, w.b.y) - w.radius;
        float maxY = Mathf.Max(w.a.y, w.b.y) + w.radius;

        Vector2Int minCell = WorldToCell(new Vector2(minX, minY));
        Vector2Int maxCell = WorldToCell(new Vector2(maxX, maxY));

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                Vector2Int key = new Vector2Int(x, y);

                if (!wallHash.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>(4);
                    wallHash.Add(key, list);
                }

                list.Add(wallIndex);
            }
        }
    }

    void CreateWallVisual(WallSegment w)
    {
        Vector2 dir = w.b - w.a;
        float length = dir.magnitude;
        if (length <= 0.001f) return;

        float thickness = w.radius * 2f;
        Vector2 mid = (w.a + w.b) * 0.5f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.SetParent(transform);
        quad.transform.position = new Vector3(mid.x, mid.y, 1f);
        quad.transform.rotation = Quaternion.Euler(0, 0, angle);
        quad.transform.localScale = new Vector3(length, thickness, 1f);

        var col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        if (wallMaterial != null)
            quad.GetComponent<MeshRenderer>().material = wallMaterial;

        wallVisuals.Add(quad);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        List<WallSegment> drawList = Application.isPlaying ? runtimeWalls : GetEditorWalls();
        if (drawList == null) return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < drawList.Count; i++)
        {
            DrawWallGizmo(drawList[i]);
        }
    }

    void DrawWallGizmo(WallSegment w)
    {
        Gizmos.DrawLine(w.a, w.b);

        Gizmos.DrawWireSphere(w.a, w.radius);
        Gizmos.DrawWireSphere(w.b, w.radius);

        Vector2 dir = (w.b - w.a).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * w.radius;

        Gizmos.DrawLine(w.a + normal, w.b + normal);
        Gizmos.DrawLine(w.a - normal, w.b - normal);
    }
}