using UnityEngine;

public class MenuVisual : MonoBehaviour
{
    [SerializeField] OceanFieldManager oceanFieldManager;

    [Header("Menu Visual Settings")]
    [SerializeField] float currentRadius = 7f;

    [Header("Rainbow Settings")]
    [SerializeField] float hueSpeed = 0.1f;
    [SerializeField] float saturation = 0.48f;
    [SerializeField] float strongValue = 0.76f;
    [SerializeField] float weakValue = 0.35f;

    [Header("Radius Change")]
    [SerializeField] float minRadius = 0.1f;
    [SerializeField] float maxRadius = 20f;
    [SerializeField] float changeInterval = 5f;
    [SerializeField] float smoothSpeed = 3f;

    float hueStrong;
    float hueWeak;

    float targetRadius;
    float timer;

    void Start()
    {
        if (oceanFieldManager == null) return;

        hueStrong = Random.value;
        hueWeak = Random.value;


        targetRadius = Random.Range(minRadius, maxRadius);
        currentRadius = targetRadius;
    }

    void Update()
    {
        if (oceanFieldManager == null) return;

        hueStrong += hueSpeed * Time.deltaTime;
        hueWeak += (hueSpeed * 0.7f) * Time.deltaTime;

        if (hueStrong > 1f) hueStrong -= 1f;
        if (hueWeak > 1f) hueWeak -= 1f;

        Color strong = Color.HSVToRGB(hueStrong, saturation, strongValue);
        Color weak = Color.HSVToRGB(hueWeak, saturation, weakValue);

        oceanFieldManager.EnemyStrongColor = strong;
        oceanFieldManager.EnemyWeakColor = weak;

        timer += Time.deltaTime;

        if (timer >= changeInterval)
        {
            timer = 0f;
            targetRadius = Random.Range(minRadius, maxRadius);
        }

        currentRadius = Mathf.Lerp(currentRadius, targetRadius, Time.deltaTime * smoothSpeed);
        oceanFieldManager.SensorDistance = currentRadius;

        if (Time.frameCount % 2 == 0)
            oceanFieldManager.UpdateTrailTexture();
    }
}