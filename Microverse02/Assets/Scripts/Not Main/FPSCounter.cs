using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    float deltaTime;
    GUIStyle style;

    void Start()
    {
        style = new GUIStyle();
        style.fontSize = 10; // make smaller here
        style.normal.textColor = Color.white;
    }

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        float fps = 1.0f / deltaTime;
        GUI.Label(new Rect(10, 10, 150, 30), $"{Mathf.Ceil(fps)}", style);
    }
}