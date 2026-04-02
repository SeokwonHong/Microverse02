using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gate : MonoBehaviour
{
    [SerializeField] Collider2D col;
    [SerializeField] Animator animator;

    bool currentState;

    public void SetOpen(bool open)
    {
        if (currentState == open) return;
        currentState = open;

        if (animator!= null)
        {
            animator.SetBool("AllPressed", open);
        }

        if (col != null)
            col.enabled = !open;

    }
}
