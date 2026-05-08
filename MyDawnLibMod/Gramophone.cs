using UnityEngine;
using GameNetcodeStuff;
using Unity.Netcode;
using Obake;

public class GramophoneItem : GrabbableObject
{
    public InteractTrigger windingTrigger;
    public AudioSource gramophoneAudio;
    public AudioSource windingAudioSource;
    public AudioClip windingLoopSFX;
    public Animator gramophoneAnimator;
    public GameObject smokeEffect;

    private bool isDefeated = false;

    public void OnWindingProgress(float progress)
    {
        if (isDefeated) return;
    }

    public void OnWindingStarted()
    {
        if (isDefeated) return;

        SetWindingStateServerRpc(true);

        ObakeAI obake = FindObjectOfType<ObakeAI>();
        if (obake != null)
        {
            obake.HearGramophone(transform.position);
        }
    }

    public void OnWindingStopped()
    {
        if (isDefeated) return;
        SetWindingStateServerRpc(false);
    }

    public void OnWindingComplete(PlayerControllerB playerWhoWoundIt)
    {
        if (isDefeated) return;

        SetGrabbableServerRpc();

        ObakeAI obake = FindObjectOfType<ObakeAI>();
        if (obake != null)
        {
            obake.DefeatObake();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetWindingStateServerRpc(bool isWinding)
    {
        SetWindingStateClientRpc(isWinding);
    }
    [ClientRpc]
    public void SetWindingStateClientRpc(bool isWinding)
    {
        if (isDefeated) return;

        if (isWinding)
        {
            if (windingAudioSource != null && windingLoopSFX != null)
            {
                windingAudioSource.clip = windingLoopSFX;
                windingAudioSource.loop = true;
                if (!windingAudioSource.isPlaying) windingAudioSource.Play();
            }
            if (gramophoneAnimator != null)
            {
                gramophoneAnimator.SetBool("isWinding", true);
            }
        }
        else
        {
            if (windingAudioSource != null && windingAudioSource.isPlaying)
            {
                windingAudioSource.Stop();
            }
            if (gramophoneAnimator != null)
            {
                gramophoneAnimator.SetBool("isWinding", false);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetGrabbableServerRpc()
    {
        SetGrabbableClientRpc();
    }

    [ClientRpc]
    public void SetGrabbableClientRpc()
    {
        isDefeated = true;

        if (gramophoneAudio != null) gramophoneAudio.Stop();
        if (windingAudioSource != null) windingAudioSource.Stop();

        if (gramophoneAnimator != null)
        {
            gramophoneAnimator.SetBool("isWinding", false);
        }
        if (smokeEffect != null)
        {
            smokeEffect.SetActive(false);
        }

        if (windingTrigger != null)
        {
            windingTrigger.interactable = false;
            windingTrigger.gameObject.SetActive(false);
        }

        gameObject.layer = LayerMask.NameToLayer("Props");
        grabbable = true;
    }
}