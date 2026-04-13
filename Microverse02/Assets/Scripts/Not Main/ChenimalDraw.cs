using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChenimalDraw : MonoBehaviour
{
    [SerializeField] Transform cursorCircle;

    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] CellManager cellManager;
    [SerializeField] Camera cam;

    [Header("Draw")]
    [SerializeField] float drawStrengthMultiplier = 1f;
    [SerializeField] float stepSpacing = 0.35f;

    [Header("Erase")]
    [SerializeField] float eraseRadius = 1.5f;
    [SerializeField] float eraseStepSpacing = 0.35f;
    [SerializeField] bool eraseOnlyEnemy = false;

    [SerializeField] PlayerEnergy playerEnergy;
    float drawCostPerSecond = 0f;
    float recoverPerSecond = 1f;
    float recoverDelay = 0.2f;
    float lastDrawTime;

    Vector2 previousWorldPos;
    bool wasDrawingLastFrame;
    bool wasErasingLastFrame;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip sprayingSound;

    void Awake()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        if (cam == null)
            cam = Camera.main;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.clip = sprayingSound;
        }
    }

    void Update()
    {
        if (bacteriaFieldManager == null || cam == null || playerEnergy == null)
            return;

        UpdateCursorVisual();

        bool isDrawing = Input.GetMouseButton(0);
        bool isErasing = Input.GetMouseButton(1);

        float cost = drawCostPerSecond * Time.deltaTime;
        float recover = recoverPerSecond * Time.deltaTime;

        if (isDrawing)
        {
            wasErasingLastFrame = false;
            lastDrawTime = Time.time;

            if (!playerEnergy.TryConsume(cost))
            {
                StopSpraySound();
                wasDrawingLastFrame = false;
                return;
            }

            Vector2 mouseWorld = GetMouseWorldPosition();

            if (!wasDrawingLastFrame)
            {
                bacteriaFieldManager.DepositDraw(mouseWorld, drawStrengthMultiplier);
                previousWorldPos = mouseWorld;
                wasDrawingLastFrame = true;

                StartSpraySound();
                return;
            }

            StartSpraySound();

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
        else if (isErasing)
        {
            StopSpraySound();
            wasDrawingLastFrame = false;

            Vector2 mouseWorld = GetMouseWorldPosition();

            if (!wasErasingLastFrame)
            {
                EraseAt(mouseWorld);
                bacteriaFieldManager.UpdateTrailTexture();

                previousWorldPos = mouseWorld;
                wasErasingLastFrame = true;
                return;
            }

            float distance = Vector2.Distance(previousWorldPos, mouseWorld);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / eraseStepSpacing));

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(previousWorldPos, mouseWorld, t);
                EraseAt(p);
            }

            bacteriaFieldManager.UpdateTrailTexture();
            previousWorldPos = mouseWorld;
        }
        else
        {
            StopSpraySound();

            if (Time.time - lastDrawTime > recoverDelay)
            {
                playerEnergy.AddEnergy(recover);
            }

            wasDrawingLastFrame = false;
            wasErasingLastFrame = false;
        }
    }

    void EraseAt(Vector2 p)
    {
        bacteriaFieldManager.EraseField(p, eraseRadius);

        if (cellManager == null) return;

        if (eraseOnlyEnemy)
            cellManager.RemoveCellsInRadius(p, eraseRadius, CellManager.Team.Enemy);
        else
            cellManager.RemoveCellsInRadius(p, eraseRadius);
    }

    void StartSpraySound()
    {
        if (audioSource == null || sprayingSound == null)
            return;

        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    void StopSpraySound()
    {
        if (audioSource == null)
            return;

        if (audioSource.isPlaying)
            audioSource.Stop();
    }

    Vector2 GetMouseWorldPosition()
    {
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = -cam.transform.position.z;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);
        return new Vector2(mouseWorld.x, mouseWorld.y);
    }

    void UpdateCursorVisual()
    {
        if (cursorCircle == null) return;

        Vector2 pos = GetMouseWorldPosition();
        cursorCircle.position = new Vector3(pos.x, pos.y, 0f);

        float size = eraseRadius * 2f;
        cursorCircle.localScale = new Vector3(size, size, 1f);
    }
}