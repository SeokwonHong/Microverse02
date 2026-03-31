using System.Collections.Generic;
using UnityEngine;

public class CellRenderer : MonoBehaviour
{
    [SerializeField] private CellManager cellManager;

    [Header("Prefabs")]
    [SerializeField] private GameObject bacteriaBodyPrefab;
    [SerializeField] private GameObject bacteriaDetectPrefab;

    [Header("Detect Visuals")]
    [Range(0f, 1f)]
    [SerializeField] private float detectAlpha = 0.1f;

    private readonly List<SpriteRenderer> bacteriaBody = new();
    private readonly List<SpriteRenderer> bacteriaDetect = new();

    [Header("Colours")]
    [SerializeField] private Color playerColour = Color.red;
    [SerializeField] private Color enemyColour = Color.blue;
    [SerializeField] private Color deadColour = Color.gray;

    [Header("Debug")]
    [SerializeField] private bool showBacteria = true;
    [SerializeField] private bool showDetectRadius = true;
    [SerializeField] private float detectRadiusMultiplier = 2.25f;

    private void Awake()
    {
        if (cellManager == null)
            cellManager = FindAnyObjectByType<CellManager>();
    }

    private void LateUpdate()
    {
        if (cellManager == null) return;

        int count = cellManager.CellCount;
        EnsurePool(count);

        for (int i = 0; i < count; i++)
        {
            bool isDead = cellManager.IsDead(i);
            Vector2 pos = cellManager.GetPos(i);

            var rBody = bacteriaBody[i];

            if (!showBacteria)
            {
                rBody.gameObject.SetActive(false);

                if (i < bacteriaDetect.Count)
                    bacteriaDetect[i].gameObject.SetActive(false);

                continue;
            }

            rBody.gameObject.SetActive(!isDead);

            if (!isDead)
            {
                float diameter = cellManager.GetRadius(i) * 2f;

                rBody.transform.position = new Vector3(pos.x, pos.y, 0f);
                rBody.transform.localScale = new Vector3(diameter, diameter, 1f);
                rBody.color = ComputeColour(i);
            }
            else
            {
                rBody.color = deadColour;
            }

            if (bacteriaDetectPrefab == null || !showDetectRadius)
            {
                if (i < bacteriaDetect.Count)
                    bacteriaDetect[i].gameObject.SetActive(false);
                continue;
            }

            var rDet = bacteriaDetect[i];
            rDet.gameObject.SetActive(!isDead);

            if (!isDead)
            {
                float detectRadius = cellManager.GetRadius(i) * detectRadiusMultiplier;
                float diameter = detectRadius * 2f;

                rDet.transform.position = rBody.transform.position;
                rDet.transform.localScale = new Vector3(diameter, diameter, 1f);

                Color dc = ComputeColour(i);
                dc.a = detectAlpha;
                rDet.color = dc;
            }
        }
    }

    private Color ComputeColour(int i)
    {
        if (cellManager.IsDead(i))
            return deadColour;

        var team = cellManager.GetTeam(i);

        return team == CellManager.Team.Player ? playerColour : enemyColour;
    }

    private void EnsurePool(int count)
    {
        while (bacteriaBody.Count < count)
        {
            var go = Instantiate(bacteriaBodyPrefab, transform);
            bacteriaBody.Add(go.GetComponent<SpriteRenderer>());
        }

        if (bacteriaDetectPrefab == null) return;

        while (bacteriaDetect.Count < count)
        {
            var go = Instantiate(bacteriaDetectPrefab, transform);
            bacteriaDetect.Add(go.GetComponent<SpriteRenderer>());
        }
    }
}