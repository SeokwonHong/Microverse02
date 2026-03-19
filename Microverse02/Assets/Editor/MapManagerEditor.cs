#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapManager))]
public class MapManagerEditor : Editor
{
    MapManager map;

    void OnEnable()
    {
        map = (MapManager)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);

        if (GUILayout.Button("Clear All"))
        {
            Undo.RecordObject(map, "Clear Map");
            map.ClearAll();
            EditorUtility.SetDirty(map);
        }

        if (GUILayout.Button("Fill All"))
        {
            Undo.RecordObject(map, "Fill Map");
            map.FillAll();
            EditorUtility.SetDirty(map);
        }
    }

    void OnSceneGUI()
    {
        if (map == null) return;

        Event e = Event.current;

        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (!plane.Raycast(ray, out float enter))
            return;

        Vector3 hit = ray.GetPoint(enter);

        Handles.color = e.shift ? Color.red : Color.green;
        Handles.DrawWireDisc(hit, Vector3.forward, map.BrushRadius * 0.1f);

        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) &&
            e.button == 0 &&
            !e.alt)
        {
            Undo.RecordObject(map, "Paint Map");

            byte value = (byte)(e.shift ? 0 : 1);
            map.PaintWorld(hit, map.BrushRadius, value);

            EditorUtility.SetDirty(map);
            e.Use();
        }
    }
}
#endif