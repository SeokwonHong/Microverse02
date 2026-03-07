using System.Collections.Generic;
using UnityEngine;

public class CellRenderer : MonoBehaviour
{
    [SerializeField] private CellManager cellManager;

    [Header("Prefabs")]
    [SerializeField] private GameObject cellBodyPrefab;
    [SerializeField] private GameObject cellJellyPrefab;

    [Header("Detect Visuals")]
    [Range(0f, 1f)]
    [SerializeField] private float organismDetectAlpha = 0.1f;

    private readonly List<SpriteRenderer> organismBody = new();
    private readonly List<SpriteRenderer> organismJelly = new();


    [Header("Colours")]
    [SerializeField] Color playerColour;
    [SerializeField] Color wbcColour;
    [SerializeField] Color organismColour;
    [SerializeField] Color deadColour;


    //link to shader

    private readonly MaterialPropertyBlock mpb = new();
    //private static readonlyon

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

            int contactCount = cellManager.GetJellyContactCount(i);

            for(int k =  0; k < contactCount; k++)
            {
                if(cellManager.TryGetJellyContact(i,k,out Vector2 dir, out float depth))
                {
                    Debug.DrawLine(pos,pos+dir*depth,Color.red);
                }
            }

            var rBody = organismBody[i];

   

            rBody.gameObject.SetActive(!isDead);

            if (!isDead)
            {
                float d = cellManager.GetRadius(i) *2f;

                rBody.transform.position = new Vector3(pos.x, pos.y, 0f);
                rBody.transform.localScale = new Vector3(d, d, 1f);

                rBody.color = ComputeColour(i);
            }

            // Jelly radius

            var rJelly = organismJelly[i];
            rJelly.gameObject.SetActive(!isDead);

            if (!isDead)
            {
                float jellyRadius = cellManager.GetJellyRadius(i);

                float dd = jellyRadius * 2f;

                rJelly.transform.position = rBody.transform.position;
                rJelly.transform.localScale = new Vector3(dd, dd, 1f);

                Color dc = ComputeColour(i);
                dc.a = organismDetectAlpha;
                rJelly.color = dc;
            }
        }
    }

    private Color ComputeColour(int i)
    {
        var role = cellManager.GetRole(i);

        if (role == CellManager.CellRole.Player)
            return playerColour;

        if (role == CellManager.CellRole.WhiteBlood)
            return wbcColour;

        if (cellManager.IsOrganismDead(i))
            return deadColour;

        int organismId = cellManager.GetOrganismId(i);
        return GetLifespanColour(organismId);
    }

    private void EnsurePool(int count)
    {
        while (organismBody.Count < count)
        {
            var go = Instantiate(cellBodyPrefab, transform);
            organismBody.Add(go.GetComponent<SpriteRenderer>());
        }

        if (cellJellyPrefab == null) return;

        while (organismJelly.Count < count)
        {
            var go = Instantiate(cellJellyPrefab, transform);
            organismJelly.Add(go.GetComponent<SpriteRenderer>());
        }

    }

    private Color GetLifespanColour(int organismId)
    {
        float life = cellManager.GetOrganismEnergy(organismId);

        float t = Mathf.InverseLerp(1f, 8f, life);

        Color baseCol = organismColour;

        Color.RGBToHSV(baseCol, out float h, out float s, out float v);

        // reduce saturation over lifespan
        s = Mathf.Lerp(0.3f, 1.25f, t);

        return Color.HSVToRGB(h, s, v);
    }


}
