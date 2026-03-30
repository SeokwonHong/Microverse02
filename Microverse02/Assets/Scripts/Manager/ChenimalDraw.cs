using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChenimalDraw : MonoBehaviour
{
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] Camera cam;
    [SerializeField] float drawStrengthMultiplier = 1f;
    [SerializeField] float stepSpacing = 0.35f;

    Vector2 previousWorldPos;
    bool wasDrawingLastFrame;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
        if (bacteriaFieldManager == null || cam == null)
            return;

        bool isDrawing = Input.GetMouseButton(0);
        bool isErasing = Input.GetMouseButton(1); // RIGHT CLICK

        if (isDrawing || isErasing)
        {
            Vector2 mouseWorld = GetMouseWorldPosition();

            if (!wasDrawingLastFrame)
            {
                if (isDrawing)
                    bacteriaFieldManager.DepositDraw(mouseWorld, drawStrengthMultiplier);
                else
                    bacteriaFieldManager.DepositRemove(mouseWorld, drawStrengthMultiplier);

                previousWorldPos = mouseWorld;
                wasDrawingLastFrame = true;
                return;
            }

            float distance = Vector2.Distance(previousWorldPos, mouseWorld);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / stepSpacing));

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(previousWorldPos, mouseWorld, t);

                if (bacteriaFieldManager.MapManager != null &&
                    bacteriaFieldManager.MapManager.IsWallWorld(p))
                    continue;

                if (isDrawing)
                    bacteriaFieldManager.DepositDraw(p, drawStrengthMultiplier);
                else
                    bacteriaFieldManager.DepositRemove(p, drawStrengthMultiplier);
            }

            previousWorldPos = mouseWorld;
        }
        else
        {
            wasDrawingLastFrame = false;
        }
    }

    Vector2 GetMouseWorldPosition()
    {
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = -cam.transform.position.z;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);
        return new Vector2(mouseWorld.x, mouseWorld.y);
    }
}
