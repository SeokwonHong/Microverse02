#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using Vector2 = UnityEngine.Vector2;

[ExecuteAlways]
public class WallDrawing : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] WallManager wallManager;
    [SerializeField] Camera cam;

    [Header("Map")]
    [SerializeField] Vector2 mapCentre = Vector2.zero;
    [SerializeField] float mapRadius = 50f;

    [Header("Draw")]
    [SerializeField] int brushRadius = 2;
    [SerializeField] float minSegmentDistance = 0.25f;
    [SerializeField] bool drawOnlyWhileHolding = true;
    [SerializeField] bool eraseMode = false;

    bool isDrawing;
    Vector2 lastDrawPos;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (wallManager == null || cam == null) return;

        if (drawOnlyWhileHolding)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mouseWorld = GetMouseWorldPos();

                if (!IsInsideMap(mouseWorld)) return;

                isDrawing = true;
                lastDrawPos = ClampToMap(mouseWorld);

                wallManager.PaintCircle(lastDrawPos, brushRadius, !eraseMode);
                wallManager.RefreshTexture();
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

        Vector2 clampedMouse = ClampToMap(mouseWorld);

        float d = Vector2.Distance(lastDrawPos, clampedMouse);
        if (d < minSegmentDistance)
            return;

        wallManager.PaintLine(lastDrawPos, clampedMouse, brushRadius, !eraseMode);
        wallManager.RefreshTexture();

        lastDrawPos = clampedMouse;
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

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        if (wallManager == null) return;

        HandleSceneDrawing();
    }

    void HandleSceneDrawing()
    {
        Event e = Event.current;
        if (e == null) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        float t = 0f;
        if (Mathf.Abs(ray.direction.z) > 0.0001f)
            t = -ray.origin.z / ray.direction.z;

        Vector3 world = ray.origin + ray.direction * t;
        Vector2 mouseWorld = new Vector2(world.x, world.y);

        if (!IsInsideMap(mouseWorld)) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            lastDrawPos = ClampToMap(mouseWorld);
            wallManager.PaintCircle(lastDrawPos, brushRadius, true);
            wallManager.RefreshTexture();
            e.Use();
        }

        if (e.type == EventType.MouseDown && e.button == 1)
        {
            lastDrawPos = ClampToMap(mouseWorld);
            wallManager.PaintCircle(lastDrawPos, brushRadius, false);
            wallManager.RefreshTexture();
            e.Use();
        }

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            Vector2 clampedMouse = ClampToMap(mouseWorld);
            wallManager.PaintLine(lastDrawPos, clampedMouse, brushRadius, true);
            wallManager.RefreshTexture();
            lastDrawPos = clampedMouse;
            e.Use();
        }

        if (e.type == EventType.MouseDrag && e.button == 1)
        {
            Vector2 clampedMouse = ClampToMap(mouseWorld);
            wallManager.PaintLine(lastDrawPos, clampedMouse, brushRadius, false);
            wallManager.RefreshTexture();
            lastDrawPos = clampedMouse;
            e.Use();
        }
    }
#endif
}