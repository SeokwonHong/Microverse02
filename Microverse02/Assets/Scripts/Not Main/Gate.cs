using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gate : MonoBehaviour
{

    [SerializeField] Animator animator;
    [SerializeField] CellManager cellManager;

    bool currentState;

    public void SetOpen(bool open)
    {
        if (currentState == open) return;
        currentState = open;

        if (animator!= null)
        {
            animator.SetBool("AllPressed", open);
        }


    }
}
