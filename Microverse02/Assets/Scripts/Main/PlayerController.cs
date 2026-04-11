using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] MapManager mapManager;
    [SerializeField] float playerRadius = 0.2f;

    [Header("Move")]
    [SerializeField] float moveSpeed = 5f;

    [Header("Draw")]
    [SerializeField] BacteriaFieldManager bacteriaFieldManager;
    [SerializeField] PlayerEnergy playerEnergy;
    [SerializeField] float drawStrengthMultiplier = 1f;
    [SerializeField] float stepSpacing = 0.35f;

    float drawCostPerSecond = 0f;
    float recoverPerSecond = 2.8f;
    float recoverDelay = 0.1f;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip sprayingSound;

    float lastDrawTime;
    Vector2 previousWorldPos;
    bool wasDrawingLastFrame;

    void Awake()
    {
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
        HandleMovement();
        HandleDrawing();
    }

    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(h, v, 0f).normalized;
        transform.position += move * moveSpeed * Time.deltaTime;

        ClampInsideMap();
    }

    void ClampInsideMap()
    {
        if (mapManager == null) return;

        Vector2 centre = mapManager.MapCentre;
        Vector2 size = mapManager.MapSize;

        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(
            pos.x,
            centre.x - halfW + playerRadius/2,
            centre.x + halfW - playerRadius/2
        );

        pos.y = Mathf.Clamp(
            pos.y,
            centre.y - halfH + playerRadius/2,
            centre.y + halfH - playerRadius/2
        );

        transform.position = pos;
    }
    void HandleDrawing()
    {
        if (bacteriaFieldManager == null || playerEnergy == null)
            return;

        bool isDrawing = true;
            
            //Input.GetKey(KeyCode.Space);

        float cost = drawCostPerSecond * Time.deltaTime;
        float recover = recoverPerSecond * Time.deltaTime;

        if (isDrawing)
        {
            lastDrawTime = Time.time;

            if (!playerEnergy.TryConsume(cost))
            {
                StopSpraySound();
                wasDrawingLastFrame = false;
                return;
            }

            Vector2 currentWorldPos = transform.position;

            if (!wasDrawingLastFrame)
            {
                if (bacteriaFieldManager.MapManager == null ||
                    !bacteriaFieldManager.MapManager.IsWallWorld(currentWorldPos))
                {
                    bacteriaFieldManager.DepositDraw(currentWorldPos, drawStrengthMultiplier);
                }

                previousWorldPos = currentWorldPos;
                wasDrawingLastFrame = true;

                StartSpraySound();
                return;
            }

            StartSpraySound();

            float distance = Vector2.Distance(previousWorldPos, currentWorldPos);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / stepSpacing));

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(previousWorldPos, currentWorldPos, t);

                if (bacteriaFieldManager.MapManager != null &&
                    bacteriaFieldManager.MapManager.IsWallWorld(p))
                    continue;

                bacteriaFieldManager.DepositDraw(p, drawStrengthMultiplier);
            }

            previousWorldPos = currentWorldPos;
        }
        else
        {
            StopSpraySound();

            if (Time.time - lastDrawTime > recoverDelay)
            {
                playerEnergy.AddEnergy(recover);
            }

            wasDrawingLastFrame = false;
        }
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
}