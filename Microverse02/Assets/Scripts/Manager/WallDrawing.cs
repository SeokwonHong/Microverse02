using UnityEngine;
using Vector2 = UnityEngine.Vector2;

public class WallDrawing : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] WallManager wallManager;
    [SerializeField] Camera cam;

    [Header("Map")]
    [SerializeField] Vector2 mapCentre = Vector2.zero;
    [SerializeField] float mapRadius = 50f;

    [Header("Draw")]
    [SerializeField] float wallThickness = 0.35f;
    [SerializeField] float minSegmentDistance = 0.25f;
    [SerializeField] bool drawOnlyWhileHolding = true;

    bool isDrawing;
    Vector2 lastDrawPos;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (wallManager == null || cam == null) return;

        if (drawOnlyWhileHolding)
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDrawing = true;
                lastDrawPos = GetMouseWorldPos();
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDrawing = false;
            }

            if (isDrawing)
            {
                TryDraw();
            }
        }
        else
        {
            TryDraw();
        }
    }

    void TryDraw()
    {
        Vector2 mouseWorld = GetMouseWorldPos();

        if (!IsInsideMap(mouseWorld))
            return;

        float d = Vector2.Distance(lastDrawPos, mouseWorld);
        if (d < minSegmentDistance)
            return;

        Vector2 a = ClampToMap(lastDrawPos);
        Vector2 b = ClampToMap(mouseWorld);

        wallManager.AddRuntimeWall(a, b, wallThickness);
        lastDrawPos = b;
    }

    Vector2 GetMouseWorldPos()
    {
        Vector3 p = cam.ScreenToWorldPoint(Input.mousePosition);
        return new Vector2(p.x, p.y);
    }

    bool IsInsideMap(Vector2 p)
    {
        return (p - mapCentre).sqrMagnitude <= mapRadius * mapRadius;
    }

    Vector2 ClampToMap(Vector2 p)
    {
        Vector2 delta = p - mapCentre;
        float dist = delta.magnitude;

        if (dist <= mapRadius || dist <= 0.0001f)
            return p;

        return mapCentre + delta / dist * mapRadius;
    }
}