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

    private MaterialPropertyBlock mpb ;
    private static readonly int JellyContact0ID = Shader.PropertyToID("_JellyContact0");
    private static readonly int JellyContact1ID = Shader.PropertyToID("_JellyContact1");
    private static readonly int JellyContact2ID = Shader.PropertyToID("_JellyContact2");
    private static readonly int JellyContact3ID = Shader.PropertyToID("_JellyContact3");

    private void Awake()
    {
        if(cellManager == null) cellManager = FindAnyObjectByType<CellManager>();
        mpb=new MaterialPropertyBlock();
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

                mpb.Clear();

                Vector4 c0 = Vector4.zero;
                Vector4 c1 = Vector4.zero;
                Vector4 c2 = Vector4.zero;
                Vector4 c3 = Vector4.zero;

                int jellyContactCount = cellManager.GetJellyContactCount(i);

                for(int k =0; k<jellyContactCount; k++)
                {
                    if (!cellManager.TryGetJellyContact(i, k, out Vector2 dir, out float depth)) continue;

                    float cutPos = 1f - (depth/(2f*jellyRadius));
                    cutPos = Mathf.Clamp(cutPos, 0.05f, 1f);


                    Vector4 packed = new Vector4(dir.x, dir.y, cutPos, 0f);

                    if(k==0) c0 = packed;
                    else if (k == 1) c1 = packed;
                    else if (k == 2) c2 = packed;
                    else if (k == 3) c3 = packed;

                }
                mpb.SetVector(JellyContact0ID, c0);
                mpb.SetVector(JellyContact1ID, c1);
                mpb.SetVector(JellyContact2ID, c2);
                mpb.SetVector(JellyContact3ID, c3);

                rJelly.SetPropertyBlock(mpb);

            }
            else
            {
                rJelly.SetPropertyBlock (null);
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
