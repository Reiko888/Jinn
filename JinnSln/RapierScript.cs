using GameNetcodeStuff;
using Jinn;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Jinn
{
    public class RapierItem : GrabbableObject
    {
        public AudioSource rapierAudio;
        private List<RaycastHit> objectsHitByRapierList = new List<RaycastHit>();
        public PlayerControllerB previousPlayerHeldBy;
        private RaycastHit[] objectsHitByRapier;

        public int rapierHitForce = 1;
        public AudioClip[] hitSFX;
        public AudioClip[] swingSFX;

        private int rapierMask = 1084754248;
        private float timeAtLastDamageDealt;
        public ParticleSystem bloodParticle;

        public RuntimeAnimatorController rapierLocalAnimator;
        public RuntimeAnimatorController rapierRemoteAnimator;

        private static readonly Dictionary<ulong, RuntimeAnimatorController> _SAVED_ANIMATORS = new();
        private bool _animatorReplaced;

        private AnimatorStateInfo _savedState;
        private float _savedNormalizedTime;
        private bool _savedCrouching;
        private bool _savedWalking;
        private bool _savedJumping;
        private bool _savedSprinting;

        private void Start()
        {
            rapierHitForce = JinnContentHandler.Instance.jinnAssets.GetConfig<int>("ConfigJinnBaseSpeed").Value;
        }

        public override void EquipItem()
        {
            base.EquipItem();
            previousPlayerHeldBy = playerHeldBy;

            if (playerHeldBy != null)
            {
                previousPlayerHeldBy.equippedUsableItemQE = true;
                EnableRapierAnimator();
                playerHeldBy.playerBodyAnimator.SetBool(itemProperties.grabAnim, true);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!isHeld && !isPocketed)
            {
                EnableItemMeshes(true);
            }
        }

        public override void GrabItem()
        {
            if (playerHeldBy != null) EnableRapierAnimator();
            base.GrabItem();
        }

        public override void PocketItem()
        {
            base.PocketItem();
            DisableRapierAnimator();
        }

        public override void DiscardItem()
        {
            base.DiscardItem();
            DisableRapierAnimator();
        }

        private void EnableRapierAnimator()
        {
            Debug.Log($"[RapierDebug] EnableRapierAnimator called. Current _animatorReplaced status: {_animatorReplaced}");
            if (_animatorReplaced) return;

            if (playerHeldBy == null)
            {
                Debug.LogWarning("[RapierDebug] EnableRapierAnimator failed: playerHeldBy is null.");
                return;
            }

            Debug.Log($"[RapierDebug] Rapier animators in Inspector - Local: {rapierLocalAnimator != null}, Remote: {rapierRemoteAnimator != null}");

            if (!_SAVED_ANIMATORS.ContainsKey(playerHeldBy.playerClientId))
            {
                Debug.Log($"[RapierDebug] Saving original animator for client {playerHeldBy.playerClientId}: {playerHeldBy.playerBodyAnimator.runtimeAnimatorController.name}");
                _SAVED_ANIMATORS[playerHeldBy.playerClientId] = playerHeldBy.playerBodyAnimator.runtimeAnimatorController;
            }

            SaveAnimatorState(playerHeldBy.playerBodyAnimator);

            if (playerHeldBy == GameNetworkManager.Instance.localPlayerController)
            {
                Debug.Log("[RapierDebug] Applying LOCAL rapier animator.");
                playerHeldBy.playerBodyAnimator.runtimeAnimatorController = rapierLocalAnimator;
            }
            else
            {
                Debug.Log("[RapierDebug] Applying REMOTE rapier animator.");
                playerHeldBy.playerBodyAnimator.runtimeAnimatorController = rapierRemoteAnimator;
            }

            Debug.Log("[RapierDebug] Rebinding and Updating animator...");
            playerHeldBy.playerBodyAnimator.Rebind();
            playerHeldBy.playerBodyAnimator.Update(0f);

            RestoreAnimatorState(playerHeldBy.playerBodyAnimator, true);
            _animatorReplaced = true;
            Debug.Log("[RapierDebug] EnableRapierAnimator finished successfully.");
        }

        private void DisableRapierAnimator()
        {
            Debug.Log($"[RapierDebug] DisableRapierAnimator called. Current _animatorReplaced status: {_animatorReplaced}");
            if (!_animatorReplaced) return;

            PlayerControllerB player = playerHeldBy != null ? playerHeldBy : previousPlayerHeldBy;
            if (player == null)
            {
                Debug.LogWarning("[RapierDebug] DisableRapierAnimator failed: Both playerHeldBy and previousPlayerHeldBy are null.");
                return;
            }

            SaveAnimatorState(player.playerBodyAnimator);

            if (_SAVED_ANIMATORS.TryGetValue(player.playerClientId, out var original))
            {
                Debug.Log($"[RapierDebug] Restoring original animator for client {player.playerClientId}: {original.name}");
                player.playerBodyAnimator.runtimeAnimatorController = original;
                _SAVED_ANIMATORS.Remove(player.playerClientId);
            }
            else
            {
                Debug.LogError($"[RapierDebug] FATAL: Could not find original animator in dictionary for client {player.playerClientId}!");
            }

            Debug.Log("[RapierDebug] Rebinding and Updating original animator...");
            player.playerBodyAnimator.Rebind();
            player.playerBodyAnimator.Update(0f);

            RestoreAnimatorState(player.playerBodyAnimator, false);
            _animatorReplaced = false;
            Debug.Log("[RapierDebug] DisableRapierAnimator finished successfully.");
        }

        private void SaveAnimatorState(Animator anim)
        {
            _savedState = anim.GetCurrentAnimatorStateInfo(0);
            _savedNormalizedTime = _savedState.normalizedTime;

            _savedCrouching = anim.GetBool("crouching");
            _savedWalking = anim.GetBool("Walking");
            _savedJumping = anim.GetBool("Jumping");
            _savedSprinting = anim.GetBool("Sprinting");

            Debug.Log($"[RapierDebug] Saved State -> Hash: {_savedState.fullPathHash}, Time: {_savedNormalizedTime}, Walk: {_savedWalking}, Crouch: {_savedCrouching}");
        }

        private void RestoreAnimatorState(Animator anim, bool isEquipping)
        {
            Debug.Log($"[RapierDebug] Restoring Animator State. isEquipping: {isEquipping}");
            try
            {
                anim.Play(_savedState.fullPathHash, 0, _savedNormalizedTime);

                anim.SetBool("crouching", _savedCrouching);
                anim.SetBool("Walking", _savedWalking);
                anim.SetBool("Jumping", _savedJumping);
                anim.SetBool("Sprinting", _savedSprinting);

                if (isEquipping)
                {
                    Debug.Log("[RapierDebug] Forcing 'HoldRapier' on layer 2...");
                    anim.Play("HoldRapier", 2, 0f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RapierDebug] Exception during RestoreAnimatorState: {ex.Message}");
            }
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            Debug.Log($"[RapierDebug-Attack] ItemActivate called. buttonDown: {buttonDown}, IsOwner: {base.IsOwner}");

            if (rapierAudio != null && swingSFX != null && swingSFX.Length > 0)
            {
                int randIndex = UnityEngine.Random.Range(0, swingSFX.Length);
                rapierAudio.PlayOneShot(swingSFX[randIndex], 1f);
            }

            if (playerHeldBy != null)
            {
                previousPlayerHeldBy = playerHeldBy;
                if (playerHeldBy.IsOwner)
                {
                    Debug.Log("[RapierDebug-Attack] Triggering 'SwingRapier' on player animator.");
                    playerHeldBy.playerBodyAnimator.SetTrigger("SwingRapier");
                    StartCoroutine(TraceAnimatorState(playerHeldBy.playerBodyAnimator, 2));
                }
            }

            if (base.IsOwner)
            {
                Debug.Log("[RapierDebug-Attack] We are the owner. Calling HitRapier()...");
                HitRapier();
            }
        }

        private System.Collections.IEnumerator TraceAnimatorState(Animator anim, int layerIndex)
        {
            Debug.Log($"[RapierDebug-Trace] --- STARTING 1-SECOND ANIMATION TRACE ON LAYER {layerIndex} ---");

            for (int i = 0; i < 10; i++)
            {
                if (anim == null) break;
                AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(layerIndex);
                AnimatorClipInfo[] clipInfo = anim.GetCurrentAnimatorClipInfo(layerIndex);

                string clipName = clipInfo.Length > 0 ? clipInfo[0].clip.name : "NO CLIP (Empty State)";

                Debug.Log($"[RapierDebug-Trace] +{i * 0.1f}s | Clip: {clipName} | Normalized Time: {stateInfo.normalizedTime:F2} | Transitioning: {anim.IsInTransition(layerIndex)}");

                yield return new WaitForSeconds(0.1f);
            }

            Debug.Log($"[RapierDebug-Trace] --- TRACE FINISHED ---");
        }

        public void HitRapier(bool cancel = false)
        {
            Debug.Log($"[RapierDebug-Attack] HitRapier started. cancel: {cancel}");
            if (previousPlayerHeldBy == null)
            {
                Debug.LogWarning("[RapierDebug-Attack] HitRapier aborted: previousPlayerHeldBy is null.");
                return;
            }

            previousPlayerHeldBy.activatingItem = false;
            bool flag = false;
            bool flag2 = false;
            int num = -1;
            bool flag3 = false;

            float timeSinceLastHit = Time.realtimeSinceStartup - timeAtLastDamageDealt;

            if (!cancel && timeSinceLastHit > 0.43f)
            {
                Debug.Log($"[RapierDebug-Attack] Swing valid. Cooldown cleared ({timeSinceLastHit}s elapsed). Casting sphere...");
                previousPlayerHeldBy.twoHanded = false;

                objectsHitByRapier = Physics.SphereCastAll(previousPlayerHeldBy.gameplayCamera.transform.position + previousPlayerHeldBy.gameplayCamera.transform.right * 0.1f, 0.3f, previousPlayerHeldBy.gameplayCamera.transform.forward, 1.5f, rapierMask, QueryTriggerInteraction.Collide);
                objectsHitByRapierList = objectsHitByRapier.OrderBy((RaycastHit x) => x.distance).ToList();

                Debug.Log($"[RapierDebug-Attack] SphereCast found {objectsHitByRapierList.Count} colliders.");
                List<EnemyAI> list = new List<EnemyAI>();

                for (int num2 = 0; num2 < objectsHitByRapierList.Count; num2++)
                {
                    GameObject hitObj = objectsHitByRapierList[num2].collider.gameObject;

                    if (hitObj.layer == 8 || hitObj.layer == 11)
                    {
                        if (objectsHitByRapierList[num2].collider.isTrigger) continue;
                        flag = true;
                        Debug.Log($"[RapierDebug-Attack] Hit surface: {hitObj.name} (Layer: {hitObj.layer})");

                        string text = hitObj.tag;
                        for (int num3 = 0; num3 < StartOfRound.Instance.footstepSurfaces.Length; num3++)
                        {
                            if (StartOfRound.Instance.footstepSurfaces[num3].surfaceTag == text)
                            {
                                num = num3;
                                break;
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"[RapierDebug-Attack] Hit potential entity: {hitObj.name} (Layer: {hitObj.layer})");

                        if (!objectsHitByRapierList[num2].transform.TryGetComponent<IHittable>(out var component) || objectsHitByRapierList[num2].transform == previousPlayerHeldBy.transform || (!(objectsHitByRapierList[num2].point == Vector3.zero) && Physics.Linecast(previousPlayerHeldBy.gameplayCamera.transform.position, objectsHitByRapierList[num2].point, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)))
                        {
                            Debug.Log($"[RapierDebug-Attack] Entity {hitObj.name} rejected (No IHittable, is self, or blocked by wall).");
                            continue;
                        }

                        flag = true;
                        Vector3 forward = previousPlayerHeldBy.gameplayCamera.transform.forward;
                        try
                        {
                            EnemyAICollisionDetect component2 = objectsHitByRapierList[num2].transform.GetComponent<EnemyAICollisionDetect>();
                            if (component2 != null)
                            {
                                if (!(component2.mainScript == null) && !list.Contains(component2.mainScript) && (!StartOfRound.Instance.hangarDoorsClosed || component2.mainScript.isInsidePlayerShip == previousPlayerHeldBy.isInHangarShipRoom))
                                {
                                    Debug.Log($"[RapierDebug-Attack] Valid Enemy Hit: {component2.mainScript.gameObject.name}");
                                    goto IL_033f;
                                }
                                Debug.Log($"[RapierDebug-Attack] Enemy hit rejected (Already hit this swing, or ship door blocked).");
                                continue;
                            }
                            if (!(objectsHitByRapierList[num2].transform.GetComponent<PlayerControllerB>() != null))
                            {
                                Debug.Log($"[RapierDebug-Attack] Valid Object Hit (Turret, Mine, etc).");
                                goto IL_033f;
                            }
                            if (!flag3)
                            {
                                flag3 = true;
                                Debug.Log($"[RapierDebug-Attack] Valid Player Hit.");
                                goto IL_033f;
                            }
                            //Debug.Log($"[RapierDebug-Attack] Player hit rejected (Already hit a player this swing).");
                            goto end_IL_029e;

                        IL_033f:
                            bool flag4 = component.Hit(rapierHitForce, forward, previousPlayerHeldBy, playHitSFX: true, 5);
                            //Debug.Log($"[RapierDebug-Attack] Sent Hit() event to entity. Success flag: {flag4}");

                            if (flag4 && component2 != null)
                            {
                                list.Add(component2.mainScript);
                            }
                            if (!flag2 && flag4)
                            {
                                flag2 = true;
                                timeAtLastDamageDealt = Time.realtimeSinceStartup;
                                if (bloodParticle != null) bloodParticle.Play(withChildren: true);
                            }
                        end_IL_029e:;
                        }
                        catch (Exception arg)
                        {
                            //Debug.Log($"[RapierDebug-Attack] Exception caught when hitting object: {arg}");
                        }
                    }
                }
            }
            else if (!cancel)
            {
                //Debug.Log($"[RapierDebug-Attack] Attack ignored. Cooldown active! Only {timeSinceLastHit}s elapsed.");
            }

            if (flag)
            {
                //Debug.Log($"[RapierDebug-Attack] Attack sequence finished. Processing sounds and network sync. Hit Entity? {flag2}, Surface ID: {num}");

                if (rapierAudio != null && hitSFX != null && hitSFX.Length > 0)
                {
                    int randIndex = UnityEngine.Random.Range(0, hitSFX.Length);
                    rapierAudio.PlayOneShot(hitSFX[randIndex], 1f);
                }

                if (UnityEngine.Object.FindObjectOfType<RoundManager>() != null)
                {
                    UnityEngine.Object.FindObjectOfType<RoundManager>().PlayAudibleNoise(base.transform.position, 17f, 0.8f);
                }

                if (!flag2 && num != -1)
                {
                    if (rapierAudio != null) rapierAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
                    if (rapierAudio != null) WalkieTalkie.TransmitOneShotAudio(rapierAudio, StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
                }
                HitRapierServerRpc(num);
            }
        }

        [ServerRpc]
        public void HitRapierServerRpc(int hitSurfaceID)
        {
            HitRapierClientRpc(hitSurfaceID);
        }

        [ClientRpc]
        public void HitRapierClientRpc(int hitSurfaceID)
        {
            if (!base.IsOwner)
            {
                if (rapierAudio != null && hitSFX != null && hitSFX.Length > 0)
                {
                    int randIndex = UnityEngine.Random.Range(0, hitSFX.Length);
                    rapierAudio.PlayOneShot(hitSFX[randIndex], 1f);
                }

                if (hitSurfaceID != -1)
                {
                    if (rapierAudio != null)
                    {
                        rapierAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
                        WalkieTalkie.TransmitOneShotAudio(rapierAudio, StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
                    }
                }
            }
        }
    }
}