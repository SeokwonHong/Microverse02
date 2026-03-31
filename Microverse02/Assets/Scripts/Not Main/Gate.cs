using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gate : MonoBehaviour
{
    [SerializeField] Collider2D col;
    [SerializeField] GameObject visual;

    public void SetOpen(bool open)
    {
        if (col != null)
            col.enabled = !open;

        if (visual != null)
            visual.SetActive(!open);
    }
}
