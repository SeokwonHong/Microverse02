using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChenimalDraw : MonoBehaviour
{
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] Camera cam;
    [SerializeField] float drawStrengthMultiplier = 1f;
    [SerializeField] float stepSpacing = 0.35f;

    [SerializeField] PlayerEnergy playerEnergy;
    float drawCostPerSecond = 5f;
    float recoverPerSecond = 1f;
    float recoverDelay = 0.2f;
    float lastDrawTime;

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


        float cost = drawCostPerSecond * Time.deltaTime;
        float recover  = recoverPerSecond * Time.deltaTime;
        


        if (isDrawing)
        {

            lastDrawTime = Time.time;

            if (!playerEnergy.TryConsume(cost))
            {
                wasDrawingLastFrame = false;
                return;
            }
                

            Vector2 mouseWorld = GetMouseWorldPosition();

            if (!wasDrawingLastFrame)
            {
                bacteriaFieldManager.DepositDraw(mouseWorld, drawStrengthMultiplier);
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

                bacteriaFieldManager.DepositDraw(p, drawStrengthMultiplier);
            }

            previousWorldPos = mouseWorld;
        }
        else if (Time.time - lastDrawTime > recoverDelay)
        {
            playerEnergy.AddEnergy(recover);
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
