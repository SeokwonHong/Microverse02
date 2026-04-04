using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gate : MonoBehaviour
{

    [SerializeField] Animator animator;
    [SerializeField] CellManager cellManager;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip gateOpenSound;
    [SerializeField] AudioClip gateBeeping;

    bool currentState;

    public void SetOpen(bool open)
    {
        if (currentState == open) return;
        currentState = open;

        if (animator!= null)
        {
            animator.SetBool("AllPressed", open);

            if (audioSource != null && gateOpenSound != null && gateBeeping != null)
            {
                audioSource.PlayOneShot(gateOpenSound,0.65f);
                audioSource.PlayOneShot(gateBeeping);

            }
        }


    }
}
