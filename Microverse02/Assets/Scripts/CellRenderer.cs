using System.Collections.Generic;
using UnityEngine;

public class CellRenderer : MonoBehaviour
{
    [SerializeField] private CellManager cellManager;

    [Header("Prefabs")]
    [SerializeField] private GameObject bodyPrefab;
    [SerializeField] private GameObject detectPrefab;

    [Header("Detect Visuals")]
    [Range(0f, 1f)]
    [SerializeField] private float detectAlpha = 0.1f;

    private readonly List<SpriteRenderer> body = new();
    private readonly List<SpriteRenderer> detect = new();

    private void Awake()
    {
        if(cellManager == null) cellManager = FindAnyObjectByType<CellManager>();
    }

    private void LateUpdate()
    {
        if (cellManager == null) return;

        int count = cellManager.CellCount;
        EnsurePool(count);

        for(int i =0; i<count; i++)
        {
            bool isDead = cellManager.IsDead(i);
            Vector2 pos = cellManager.GetPos(i);

            var rBody = body[i];
            rBody.gameObject.SetActive(!isDead);    
            if (!isDead)
            {
                float d = cellManager.GetRadius(i) * 2f;
                rBody.transform.position = new Vector3(pos.x, pos.y, 0f);
                rBody.transform.localScale = new Vector3(d, d, 1f);
                 
                Color bodyCol = cellManager.GetColor(i);
                rBody.color = bodyCol;
            }


            //detect radius
            if (detectPrefab == null) continue;

            var rDet = detect[i];
            rDet.gameObject.SetActive(!isDead);
            if(!isDead)
            {
                float dd = cellManager.GetDetectRadius(i) * 2f;
                rDet.transform.position = rBody.transform.position;
                rDet.transform.localScale = new Vector3(dd, dd, 1f);

                Color dc = cellManager.GetColor(i);
                dc.a = detectAlpha;
                rDet.color = dc;
            }

        }
        
    }

    private void EnsurePool(int count)
    {
        while(body.Count<count)
        {
            var go = Instantiate(bodyPrefab,transform);
            body.Add(go.GetComponent<SpriteRenderer>());    
        }

        if(detectPrefab == null) return;

        while (detect.Count < count)
        {
            var go = Instantiate(detectPrefab, transform);
            detect.Add(go.GetComponent<SpriteRenderer>());
        }
    }
}
