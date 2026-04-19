#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(MapManager))]
public class MapManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
#endif