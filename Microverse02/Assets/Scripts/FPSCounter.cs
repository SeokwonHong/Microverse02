using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    float deltaTime;

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }


    void OnGUI()
    {
        float fps = 1.0f / deltaTime;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.UpperRight;

        GUI.Label(
            new Rect(0, 10, Screen.width - 10, 30),
            $"FPS: {Mathf.Ceil(fps)}",
            style
        );
    }
}
