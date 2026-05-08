using GameNetcodeStuff;
using HarmonyLib;
using Obake;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace Obake
{
    class ObakeAI : EnemyAI
    {
        System.Random enemyRandom = null!;
        float checkTimer = 0f;
        private Vector3 currentPatrolNode = Vector3.zero;
        private int previousStateChecked = -1;
        private float attackCooldown = 0f;
        public Transform AttackArea;
        public float attackRange = 2.0f;
        private float lostSightTimer = 0f;
        private bool canAttack = false;
        private bool isManifesting = false;
        private bool hasManifested = false;
        private List<Material> allObakeMaterials = new List<Material>();
        public GameObject ScanNode;
        private Vector3 lastSeenPlayerPos = Vector3.zero;
        public GameObject VoidFace;
        private bool hasHeardGramophone = false;
        private Vector3 gramophoneTargetLocation = Vector3.zero;

        public AudioClip[] customFootstepSounds;
        public AudioSource movementAudio;
        private float footstepTimer = 0f;
        private Vector3 lastPosition;
        public AudioClip emergeSFX;
        public AudioClip laughSFX;
        public AudioClip swingSFX;
        public AudioClip hitSFX;
        public AudioSource CreatureMiscSFX;

        enum State
        {
            InvisibleSearch,
            Chase,
            Manifest,
            Investigate
        }

        [Conditional("DEBUG")]
        void LogIfDebugBuild(string text)
        {
            if (Plugin.Logger != null)
            {
                Plugin.Logger.LogInfo(text);
            }
            else
            {

                UnityEngine.Debug.Log($"[Obake] {text}");
            }
        }

        public override void Start()
        {
            base.Start();
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            base.GetAINodes();
            if (AttackArea == null) AttackArea = transform;

            LogIfDebugBuild("Obake Spawned");
            SpawnCursedGramophoneLocally();
            ChangeBehaviorState((int)State.InvisibleSearch);
            StartSearch(transform.position);
            foreach (SkinnedMeshRenderer smr in skinnedMeshRenderers) allObakeMaterials.AddRange(smr.materials);
            foreach (MeshRenderer mr in meshRenderers) allObakeMaterials.AddRange(mr.materials);
            if (ScanNode == null)
            {
                Transform[] children = GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children)
                {
                    if (child.name == "ScanNode")
                    {
                        ScanNode = child.gameObject;
                    }
                    else if (child.name == "VoidFace")
                    {
                        VoidFace = child.gameObject;
                    }
                }
            }
        }

        public void SpawnCursedGramophoneLocally()
        {
            if (!IsServer) return;
            Item gramophoneItem = null;
            foreach (Item item in StartOfRound.Instance.allItemsList.itemsList)
            {
                if (item.itemName == "CursedGramophone")
                {
                    gramophoneItem = item;
                    break;
                }
            }

            if (gramophoneItem == null)
            {
                LogIfDebugBuild("CursedGramophone item not found in allItemsList!");
                return;
            }

            RandomScrapSpawn[] allScrapSpawns = FindObjectsOfType<RandomScrapSpawn>();
            Vector3 rawSpawnPos = transform.position + (Vector3.up * 1.5f);

            if (allScrapSpawns != null && allScrapSpawns.Length > 0)
            {
                RandomScrapSpawn chosenNode = allScrapSpawns[enemyRandom.Next(0, allScrapSpawns.Length)];
                rawSpawnPos = chosenNode.transform.position;
            }

            Vector3 safeSpawnPos = RoundManager.Instance.GetNavMeshPosition(rawSpawnPos, RoundManager.Instance.navHit, 5f, -1);
            if (!RoundManager.Instance.GotNavMeshPositionResult)
            {
                safeSpawnPos = rawSpawnPos;
            }

            safeSpawnPos.y += 1.0f;
            GameObject gramophoneDrop = Instantiate(gramophoneItem.spawnPrefab, safeSpawnPos, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
            GrabbableObject gramophoneGrabbable = gramophoneDrop.GetComponent<GrabbableObject>();

            int gramophoneValue = enemyRandom.Next(150, 350);
            gramophoneGrabbable.SetScrapValue(gramophoneValue);

            gramophoneGrabbable.fallTime = 0f;
            gramophoneGrabbable.targetFloorPosition = gramophoneGrabbable.GetItemFloorPosition(safeSpawnPos);
            gramophoneDrop.GetComponent<NetworkObject>().Spawn();
            gramophoneGrabbable.EnableItemMeshes(true);

            gramophoneGrabbable.grabbable = false;
            gramophoneDrop.layer = LayerMask.NameToLayer("Default");

            LogIfDebugBuild("Dropped CursedGramophone item at random node");
        }

        public void SpawnRapierLocally()
        {
            if (!IsServer) return;
            Item rapierItem = null;
            foreach (Item item in StartOfRound.Instance.allItemsList.itemsList)
            {
                if (item.itemName == "Rapier")
                {
                    rapierItem = item;
                    break;
                }
            }

            if (rapierItem == null)
            {
                LogIfDebugBuild("Rapier item not found in allItemsList!");
                return;
            }

            Vector3 rawSpawnPos = transform.position + (Vector3.up * 1.5f);
            Vector3 safeSpawnPos = RoundManager.Instance.GetNavMeshPosition(rawSpawnPos, RoundManager.Instance.navHit, 5f, -1);
            if (!RoundManager.Instance.GotNavMeshPositionResult)
            {
                safeSpawnPos = rawSpawnPos;
            }

            GameObject rapierDrop = Instantiate(rapierItem.spawnPrefab, safeSpawnPos, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
            GrabbableObject rapierGrabbable = rapierDrop.GetComponent<GrabbableObject>();

            rapierGrabbable.fallTime = 0f;
            rapierGrabbable.targetFloorPosition = rapierGrabbable.GetItemFloorPosition(safeSpawnPos);
            rapierDrop.GetComponent<NetworkObject>().Spawn();
            rapierGrabbable.EnableItemMeshes(true);

            LogIfDebugBuild("Dropped Rapier item upon Obake's defeat");
        }

        public void ChangeBehaviorState(int stateIndex)
        {
            currentBehaviourStateIndex = stateIndex;
            if (IsServer) SyncStateClientRpc(stateIndex);

            if (ScanNode != null)
            {
                if (stateIndex == (int)State.Chase)
                {
                    ScanNode.SetActive(true);
                }
                else if (stateIndex == (int)State.InvisibleSearch)
                {
                    ScanNode.SetActive(false);
                }
            }
        }

        [ClientRpc]
        public void SyncStateClientRpc(int stateIndex)
        {
            currentBehaviourStateIndex = stateIndex;
        }


        public override void Update()
        {
            base.Update();
            if (isEnemyDead) return;
            if (checkTimer > 0f) checkTimer -= Time.deltaTime;
            if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;

            if ((hasManifested || isManifesting) && currentBehaviourStateIndex != (int)State.InvisibleSearch)
            {
                HandleCustomFootsteps();
            }

            if (currentBehaviourStateIndex == (int)State.Chase && targetPlayer != null)
            {
                Vector3 directionToPlayer = targetPlayer.transform.position - transform.position;
                directionToPlayer.y = 0f; 

                if (directionToPlayer.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
                }
            }

            if (currentBehaviourStateIndex == (int)State.Chase && attackCooldown <= 0f && canAttack == true)
            {
                PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                if (localPlayer != null && !localPlayer.isPlayerDead && AttackArea != null)
                {
                    if (Vector3.Distance(AttackArea.position, localPlayer.transform.position) <= attackRange)
                    {
                        TriggerAttack(localPlayer);
                    }
                }
            }
        }

        private void HandleCustomFootsteps()
        {
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);
            if (distanceMoved > 0.02f)
            {
                footstepTimer += Time.deltaTime;
                if (footstepTimer > 0.6f)
                {
                    footstepTimer = 0f;
                    if (customFootstepSounds != null && customFootstepSounds.Length > 0 && movementAudio != null)
                    {
                        int randomIndex = UnityEngine.Random.Range(0, customFootstepSounds.Length);
                        movementAudio.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                        movementAudio.PlayOneShot(customFootstepSounds[randomIndex], 1f);
                    }
                }
            }
            else
            {
                footstepTimer = 0f;
            }
            lastPosition = transform.position;
        }

        public override void DoAIInterval()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

            base.DoAIInterval();

            if (!IsOwner) return;

            bool playerInSight = false;

            switch (currentBehaviourStateIndex)
            {
                case (int)State.InvisibleSearch:
                    EnableEnemyMesh(false, false, true);
                    agent.speed = 5f;
                    TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: true);
                    if (targetPlayer != null)
                    {
                        LogIfDebugBuild("Seen player, manifesting");
                        StopSearch(currentSearch);
                        ChangeBehaviorState((int)State.Manifest);
                    }
                    break;

                case (int)State.Manifest:
                    agent.speed = 2f;
                    if (creatureVoice != null)
                    {
                        creatureVoice.Stop();
                    }
                    StopSearch(currentSearch);
                    playerInSight = TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: false);
                    if (playerInSight && targetPlayer != null && hasManifested == false)
                    {
                        SetMovingTowardsTargetPlayer(targetPlayer);
                        if (!isManifesting)
                        {
                            isManifesting = true;
                            StartCoroutine(manifestSequence());
                        }
                    }
                    ChangeBehaviorState((int)State.Chase);
                    lostSightTimer = 10f;
                    break;

                case (int)State.Investigate:
                    agent.speed = 6f;
                    SetDestinationToPosition(gramophoneTargetLocation, checkForPath: true);

                    if (Vector3.Distance(transform.position, gramophoneTargetLocation) < 2.5f)
                    {
                        agent.speed = 0f;
                    }

                    lostSightTimer -= AIIntervalTime;

                    if (lostSightTimer <= 0f)
                    {
                        LogIfDebugBuild("Gave up on the gramophone. Demanifesting.");
                        movingTowardsTargetPlayer = false;
                        if (creatureVoice != null) creatureVoice.Play();
                        if (VoidFace != null) VoidFace.SetActive(false);

                        isManifesting = false;
                        canAttack = false;
                        hasManifested = false;
                        hasHeardGramophone = false;

                        ChangeBehaviorState((int)State.InvisibleSearch);
                        StartSearch(transform.position);
                    }
                    break;

                case (int)State.Chase:
                    if (attackCooldown > 0f)
                    {
                        agent.speed = 4.0f;
                    }
                    else if (canAttack == true && hasManifested == true)
                    {
                        agent.speed = 6f;
                    }

                    playerInSight = TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: true, viewWidth: 360f);

                    if (playerInSight && targetPlayer != null)
                    {
                        lostSightTimer = 10f;
                        SetMovingTowardsTargetPlayer(targetPlayer);
                        lastSeenPlayerPos = targetPlayer.transform.position;
                    }
                    else if (isManifesting == false)
                    {
                        lostSightTimer -= AIIntervalTime;
                        SetDestinationToPosition(lastSeenPlayerPos, checkForPath: true);
                        if (lostSightTimer <= 0f)
                        {
                            LogIfDebugBuild("10 seconds passed. Demanifesting.");
                            movingTowardsTargetPlayer = false;
                            if (creatureVoice != null)
                            {
                                creatureVoice.Play();
                            }

                            if (VoidFace != null)
                            {
                                VoidFace.SetActive(false);
                            }

                            isManifesting = false;
                            canAttack = false;
                            hasManifested = false;
                            hasHeardGramophone = false;
                            ChangeBehaviorState((int)State.InvisibleSearch);
                            StartSearch(transform.position);
                        }
                    }
                    break;
                }
            }
        IEnumerator manifestSequence()
        {
            if (creatureSFX != null)
            {
                creatureSFX.Play();
            }
            if (IsServer) PlayObakeAudioClientRpc(0);
            SetObakeAlpha(1f);
            EnableEnemyMesh(true, false, true);
            StartCoroutine(FadeObakeAlpha(1f, 0f, 4.0f));
            yield return new WaitForSeconds(0.75f);
            if (VoidFace != null) VoidFace.SetActive(true);
            yield return new WaitForSeconds(1.25f);
            DoAnimationClientRpc("isManifesting");
            yield return new WaitForSeconds(1.5f);
            if (VoidFace != null) VoidFace.SetActive(false);
            yield return new WaitForSeconds(0.5f);
            SetObakeAlpha(0f);
            canAttack = true;
            LogIfDebugBuild("I can now attack");
            if (IsServer) PlayObakeAudioClientRpc(1);
            DoAnimationClientRpc("hasManifested");
            hasManifested = true;
            isManifesting = false;
        }
        private void SetObakeAlpha(float alphaVal)
        {
            foreach (Material mat in allObakeMaterials)
            {
                mat.SetFloat("_AlphaCutoff", alphaVal);
            }
        }

        IEnumerator FadeObakeAlpha(float startAlpha, float endAlpha, float duration)
        {
            float timeElapsed = 0f;
            while (timeElapsed < duration)
            {
                timeElapsed += Time.deltaTime;
                float currentCutoff = Mathf.Lerp(startAlpha, endAlpha, timeElapsed / duration);

                SetObakeAlpha(currentCutoff);
                yield return null;
            }
            SetObakeAlpha(endAlpha);
        }

        public void HearGramophone(Vector3 gramophonePosition)
        {
            if (hasHeardGramophone) return;
            hasHeardGramophone = true;
            lostSightTimer = 15f;

            LogIfDebugBuild("The Obake hears the Gramophone!");

            if (currentBehaviourStateIndex == (int)State.InvisibleSearch || currentBehaviourStateIndex == (int)State.Chase)
            {
                targetPlayer = null;
                movingTowardsTargetPlayer = false;
                gramophoneTargetLocation = gramophonePosition;
                ChangeBehaviorState((int)State.Investigate);
            }
        }

        public void DefeatObake()
        {
            if (isEnemyDead) return;
            LogIfDebugBuild("Gramophone fully wound. Obake defeated!");

            SpawnRapierLocally();

            KillEnemyOnOwnerClient(true);
        }

        private void TriggerAttack(PlayerControllerB victim)
        {
            attackCooldown = 2.0f;

            if (IsServer)
            {
                DoAnimationClientRpc("isAttacking");
                PlayObakeAudioClientRpc(2);
            }

            if ((victim == GameNetworkManager.Instance.localPlayerController) && canAttack == true)
            {
                LogIfDebugBuild("Obake hit the local player! Sending damage to Server...");
                victim.DamagePlayer(25, hasDamageSFX: true, callRPC: true, CauseOfDeath.Stabbing);
            }
            else if (IsServer && canAttack == true)
            {
                victim.DamagePlayer(25, hasDamageSFX: true, callRPC: true, CauseOfDeath.Stabbing);
            }
        }


        [ClientRpc]
        public void DoAnimationClientRpc(string animationName)
        {
            if (creatureAnimator != null)
            {
                creatureAnimator.SetTrigger(animationName);
            }
        }

        [ClientRpc]
        public void PlayObakeAudioClientRpc(int soundType)
        {
            if (CreatureMiscSFX == null) return;

            if (soundType == 0 && emergeSFX != null)
            {
                CreatureMiscSFX.PlayOneShot(emergeSFX);
            }
            else if (soundType == 1 && laughSFX != null)
            {
                CreatureMiscSFX.PlayOneShot(laughSFX);
            }
            else if (soundType == 2)
            {
                if (swingSFX != null) CreatureMiscSFX.PlayOneShot(swingSFX);
                if (hitSFX != null) CreatureMiscSFX.PlayOneShot(hitSFX);
            }
        }
    }
}