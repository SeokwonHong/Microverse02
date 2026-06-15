using UnityEngine;

public class ChenimalDraw : MonoBehaviour
{
    [Header("UI Cursor")]
    [SerializeField] RectTransform cursorUI;

    [Header("Refs")]
    [SerializeField] OceanFieldManager oceanFieldManager;
    [SerializeField] Camera cam;
    //[SerializeField] PlayerEnergy playerEnergy;

    [Header("Draw")]
    [SerializeField] float drawStrengthMultiplier = 1f;
    [SerializeField] float stepSpacing = 0.35f;

    //[Header("Energy")]
    //float drawCostPerSecond = 3f;//5
    //float recoverPerSecond = 3f;//5
    //float recoverDelay = 0.04f;

    [Header("Cursor")]
    [SerializeField] float cursorSize = 40f; 

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip sprayingSound;
    float sprayVolume = 0.7f;
    float lastDrawTime;
    Vector2 previousWorldPos;
    bool wasDrawingLastFrame;

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
            audioSource.volume = sprayVolume;
        }
    }

    void Update()
    {
        if (oceanFieldManager == null || cam == null)
            return;

        Vector2 mouseWorld = GetMouseWorldPosition();
        UpdateCursorVisual(mouseWorld);

        if (Time.timeScale == 0f)
        {
            StopSpraySound();
            wasDrawingLastFrame = false;
            return;
        }

        bool isDrawing = Input.GetMouseButton(0);

        //float cost = drawCostPerSecond * Time.deltaTime;
        //float recover = recoverPerSecond * Time.deltaTime;

        if (isDrawing)
        {
            lastDrawTime = Time.time;

            if (!wasDrawingLastFrame)
            {
                if (!IsWall(mouseWorld))
                    oceanFieldManager.DepositDraw(mouseWorld, drawStrengthMultiplier);

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

                if (!IsWall(p))
                    oceanFieldManager.DepositDraw(p, drawStrengthMultiplier);
            }

            previousWorldPos = mouseWorld;
        }
        else
        {
            StopSpraySound();
            wasDrawingLastFrame = false;
        }
    }

    bool IsWall(Vector2 p)
    {
        return oceanFieldManager.MapManager != null &&
               oceanFieldManager.MapManager.IsWallWorld(p);
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
        Vector2 pos = new Vector2(mouseWorld.x, mouseWorld.y);

        if (oceanFieldManager != null && oceanFieldManager.MapManager != null)
        {
            var map = oceanFieldManager.MapManager;

            Vector2 halfSize = map.MapSize * 0.5f;
            float minX = map.MapCentre.x - halfSize.x + 2f;
            float maxX = map.MapCentre.x + halfSize.x - 2f;
            float minY = map.MapCentre.y - halfSize.y + 2f;
            float maxY = map.MapCentre.y + halfSize.y - 2f;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
        }

        return pos;
    }

    void UpdateCursorVisual(Vector2 worldPos)
    {
        if (cursorUI == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(new Vector3(worldPos.x, worldPos.y, 0f));
        cursorUI.position = screenPos;
        cursorUI.sizeDelta = new Vector2(cursorSize, cursorSize);
    }
}