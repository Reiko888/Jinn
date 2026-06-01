using UnityEngine;
using Unity.Netcode;
using Jinn;

public class GramophoneProp : NetworkBehaviour
{
    public InteractTrigger windingTrigger;
    public AudioSource windingAudioSource;
    public AudioSource whisperAudioSource;
    public AudioClip windingLoopSFX;
    public Animator gramophoneAnimator;

    private bool isDefeated = false;

    public void Start()
    {
        int hintDistance = JinnContentHandler.Instance.jinnAssets.GetConfig<int>("ConfigJinnGramoHintDist").Value;
        if (whisperAudioSource != null)
        {
            whisperAudioSource.maxDistance = hintDistance;
        }
    }

    public void OnWindingStarted()
    {
        if (isDefeated) return;
        SetWindingStateServerRpc(true);

        JinnAI obake = FindObjectOfType<JinnAI>();
        if (obake != null)
        {
            obake.HearGramophoneServerRpc(transform.position);
        }
    }

    public void OnWindingStopped()
    {
        if (isDefeated) return;
        SetWindingStateServerRpc(false);
    }

    public void OnWindingComplete(GameNetcodeStuff.PlayerControllerB playerWhoWoundIt)
    {
        if (isDefeated) return;
        isDefeated = true;
        CompleteWindingServerRpc();
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
            if (gramophoneAnimator != null) gramophoneAnimator.SetBool("isWinding", true);
        }
        else
        {
            if (windingAudioSource != null && windingAudioSource.isPlaying) windingAudioSource.Stop();
            if (gramophoneAnimator != null) gramophoneAnimator.SetBool("isWinding", false);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CompleteWindingServerRpc()
    {
        if (!IsServer) return;

        JinnAI obake = FindObjectOfType<JinnAI>();

        bool enableGramophonePrize = JinnContentHandler.Instance.jinnAssets.GetConfig<bool>("ConfigDropGramoOnDeath").Value;
        if (enableGramophonePrize)
        {
            Item gramophoneScrapItem = null;
            foreach (Item item in StartOfRound.Instance.allItemsList.itemsList)
            {
                if (item.itemName == "Cursed Gramophone")
                {
                    gramophoneScrapItem = item;
                    break;
                }
            }

            if (gramophoneScrapItem != null)
            {
                GameObject scrapDrop = Instantiate(gramophoneScrapItem.spawnPrefab, transform.position, transform.rotation, StartOfRound.Instance.propsContainer);
                GrabbableObject grabbable = scrapDrop.GetComponent<GrabbableObject>();

                grabbable.fallTime = 0f;
                grabbable.targetFloorPosition = grabbable.GetItemFloorPosition(transform.position);

                NetworkObject netObj = scrapDrop.GetComponent<NetworkObject>();
                netObj.Spawn();

                int scrapValue = (int)(UnityEngine.Random.Range(150, 350) * RoundManager.Instance.scrapValueMultiplier);
                grabbable.SetScrapValue(scrapValue);

                // Sync the value to clients!
                if (obake != null)
                {
                    obake.SyncScrapValueClientRpc(netObj, scrapValue);
                }
            }
        }
        if (obake != null) obake.DefeatObakeServerRpc();
        NetworkObject.Despawn(true);
    }
}