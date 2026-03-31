using System.Collections.Generic;
using UnityEngine;

public class CellRenderer : MonoBehaviour
{
    [SerializeField] private CellManager cellManager;

    [Header("Prefabs")]
    [SerializeField] private GameObject organismBodyPrefab;
    [SerializeField] private GameObject organismDetectPrefab;

    [Header("Detect Visuals")]
    [Range(0f, 1f)]
    [SerializeField] private float organismDetectAlpha = 0.1f;

    private readonly List<SpriteRenderer> organismBody = new();
    private readonly List<SpriteRenderer> organismDetect = new();


    [Header("Colours")]
    [SerializeField] Color playerColour;
    [SerializeField] Color wbcColour;
    [SerializeField] Color organismColour;
    [SerializeField] Color deadColour;

    [Header("Debug")]
    [SerializeField] bool showBacteria = true;
    private void Awake()
    {
        if(cellManager == null) cellManager = FindAnyObjectByType<CellManager>();



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
            var role = cellManager.GetRole(i);

            var rBody = organismBody[i];

            // hide bacteria if debug toggle is off
            if (!showBacteria && role == CellManager.CellRole.Bacteria)
            {
                rBody.gameObject.SetActive(false);

                if (organismDetect != null)
                    organismDetect[i].gameObject.SetActive(false);

                continue;
            }



            rBody.gameObject.SetActive(!isDead);

            if (!isDead)
            {
                float d = cellManager.GetRadius(i) * 2f;

                rBody.transform.position = new Vector3(pos.x, pos.y, 0f);
                rBody.transform.localScale = new Vector3(d, d, 1f);

                rBody.color = ComputeColour(i);
            }

            // Detect radius
            if (organismDetect == null) continue;

            var rDet = organismDetect[i];
            rDet.gameObject.SetActive(!isDead);

            if (!isDead)
            {
                float detectRadius = cellManager.GetRadius(i)*4.5f;

                if (cellManager.GetRole(i) == CellManager.CellRole.Bacteria)
                {
                    detectRadius *= 0.5f;   // half size only for player
                }

                float dd = detectRadius * 2f;

                rDet.transform.position = rBody.transform.position;
                rDet.transform.localScale = new Vector3(dd, dd, 1f);

                Color dc = ComputeColour(i);
                dc.a = organismDetectAlpha;
                rDet.color = dc;
            }
        }
    }

    private Color ComputeColour(int i)
    {
        var role = cellManager.GetRole(i);

     
       return playerColour;

        
    }

    private void EnsurePool(int count)
    {
        while (organismBody.Count < count)
        {
            var go = Instantiate(organismBodyPrefab, transform);
            organismBody.Add(go.GetComponent<SpriteRenderer>());
        }

        if (organismDetectPrefab == null) return;

        while (organismDetect.Count < count)
        {
            var go = Instantiate(organismDetectPrefab, transform);
            organismDetect.Add(go.GetComponent<SpriteRenderer>());
        }

    }



    
}
