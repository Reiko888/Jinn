using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;

namespace Obake
{
    class ObakeAI : EnemyAI
    {
        System.Random enemyRandom = null!;

        public float minDistance;

        public float maxDistance;

        private float attackCooldown = 0f;

        public SkinnedMeshRenderer skin;

        public Material[] thisMaterial;

        public GameObject BloodParticles;

        public GameObject TeleportSwirl;

        private GameObject currentNetworkSwirl;

        private bool hasHeardGramophone = false;

        private Vector3 lastPosition;

        private bool hasLOS;

        public AISearchRoutine searchRoutine;

        public float chaseTimer = 0f;

        private bool isPreparingTeleport = false;

        private float teleportWarningTimer = 0f;

        private Vector3 pendingTeleportPosition;

        private PlayerControllerB pendingTeleportTarget;

        public float teleportWarningDelay = 6f;

        public float maxDistanceAfterWarning = 18f;

        public float minDistanceAfterWarning = 5f;


        private float gramoDash;

        private float timeSinceLastAttack;

        private float timeSinceSeen;

        private float timesinceHearingGramophone;

        private float tpRollTimer;

        private float continuousChaseTimer = 0f;

        public AudioSource movementAudio;

        public AudioClip emergeSFX;

        public AudioClip laughSFX;

        public AudioClip swingSFX;

        public AudioClip[] teleportSFX;

        public AudioClip chargeUpTPSFX;

        public ParticleSystem mistParticles;

        public ParticleSystem decayingMatterParticles;

        private bool isStunned = false;


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

        public override void Awake()
        {
            base.Awake();
        }

        public override void Start()
        {
            base.Start();
            thisMaterial = skin.materials;
            base.GetAINodes();

            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            if (creatureVoice != null)
            {
                creatureVoice.Play();
            }

            LogIfDebugBuild("Obake Spawned");
            SpawnCursedGramophoneLocally();
        }

        private void OnEnable()
        {
            ObakeEventManager.OnShipLeft += HandleShipLeft;
        }

        private void OnDisable()
        {
            ObakeEventManager.OnShipLeft -= HandleShipLeft;
        }

        private void HandleShipLeft()
        {
            Plugin.Logger.LogInfo("Ship is leaving! Cleaning up");

            if (creatureVoice != null) creatureVoice.Stop();
            KillEnemy(true);
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

        public override void Update()
        {
            base.Update();
            timeSinceLastAttack += Time.deltaTime;
            SetVisibility();
            if (isEnemyDead) return;
            if (StartOfRound.Instance.allPlayersDead)
            {
                return;
            }

            if (!hasLOS && chaseTimer > 0f)
            {
                chaseTimer -= Time.deltaTime;
            }
            if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;

            if (!base.IsOwner)
            {
                return;
            }

            timeSinceSeen += Time.deltaTime;
            timesinceHearingGramophone += Time.deltaTime;

            if (isPreparingTeleport)
            {
                teleportWarningTimer -= Time.deltaTime;
                if (teleportWarningTimer <= 0f)
                {
                    ExecuteDelayedTeleport();
                }
            }

            if (isEnemyDead)
            {
                agent.speed = 0f;
            }
            else
            {
                if (hasLOS && targetPlayer != null)
                {
                    continuousChaseTimer += Time.deltaTime;

                    if (continuousChaseTimer >= 5f && !isPreparingTeleport)
                    {
                        tpRollTimer += Time.deltaTime;

                        if (tpRollTimer >= 5f)
                        {
                            tpRollTimer = 0f;

                            if (UnityEngine.Random.value <= 0.80f)
                            {
                                LogIfDebugBuild("Teleport has been accepted Teleporting...");
                                TpNearestNode(targetPlayer);
                            }
                            else
                            {
                                LogIfDebugBuild("Teleport denied. Will try again in 5 seconds.");
                            }
                        }
                    }
                }
                else
                {
                    continuousChaseTimer = 0f;
                    tpRollTimer = 0f;
                }
                if (hasHeardGramophone)
                {
                    agent.speed = gramoDash;

                    if (timesinceHearingGramophone > 12f)
                    {
                        hasHeardGramophone = false;
                        LogIfDebugBuild("Gramophone timeout reached");
                    }
                }
                else if (timeSinceLastAttack < 2f)
                {
                    agent.speed = 2f;
                }
                else
                {
                    if (timeSinceSeen > 3f)
                    {
                        agent.speed = 6f;
                    }
                    else
                    {
                        agent.speed = 5f;
                    }
                }
            }
        }

        public override void DoAIInterval ()
        {
            base.DoAIInterval();
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            }

            PlayerControllerB previousTarget = targetPlayer;

            PlayerControllerB playerControllerB = targetPlayer;
            if (TargetClosestPlayer(5f, requireLineOfSight: true, 120f))
            {
                hasLOS = true;
                timeSinceSeen = 0f;
                chaseTimer = 10f;
                if (searchRoutine.inProgress)
                {
                    StopSearch(searchRoutine);
                }
                SetMovingTowardsTargetPlayer(targetPlayer);
            }
            else if (chaseTimer <= 0f)
            {
                if (hasLOS)
                {
                    hasLOS = false;
                }
                if (!searchRoutine.inProgress)
                {
                    StartSearch(base.transform.position, searchRoutine);
                }
            }
            else if (previousTarget != null)
            {
                SetMovingTowardsTargetPlayer(previousTarget);
            }
        }

        private void TpNearestNode(PlayerControllerB targetPlayer)
        {
            if (targetPlayer == null || targetPlayer.isPlayerDead || allAINodes == null || allAINodes.Length == 0) return;

            List<GameObject> validNodes = new List<GameObject>();
            float initialTeleportRadius = 10f;

            foreach (GameObject node in allAINodes)
            {
                if (node == null) continue;

                if (Vector3.Distance(node.transform.position, targetPlayer.transform.position) <= initialTeleportRadius)
                {
                    validNodes.Add(node);
                }
            }

            if (validNodes.Count > 0)
            {
                GameObject chosenNode = validNodes[enemyRandom.Next(0, validNodes.Count)];

                pendingTeleportPosition = chosenNode.transform.position;
                pendingTeleportTarget = targetPlayer;
                teleportWarningTimer = teleportWarningDelay;
                isPreparingTeleport = true;

                if (movementAudio != null && chargeUpTPSFX != null)
                {
                    movementAudio.PlayOneShot(chargeUpTPSFX, 0.8f);
                }

                LogIfDebugBuild($"Found {validNodes.Count} nodes. Spawning warning swirl and starting timer");

                if (IsServer)
                {
                    LogIfDebugBuild("--- DEBUG: STARTING SWIRL SPAWN SEQUENCE ---");

                    if (TeleportSwirl == null)
                    {
                        LogIfDebugBuild("DEBUG ERROR: TeleportSwirl prefab is NULL! The slot in the inspector/script is empty.");
                    }
                    else
                    {
                        LogIfDebugBuild($"DEBUG: TeleportSwirl prefab found. Name: {TeleportSwirl.name}");
                        Vector3 spawnPos = pendingTeleportPosition + (Vector3.up * 1.5f);
                        LogIfDebugBuild($"DEBUG: Attempting to Instantiate at {spawnPos}");

                        currentNetworkSwirl = Instantiate(TeleportSwirl, spawnPos, Quaternion.identity);

                        if (currentNetworkSwirl == null)
                        {
                            LogIfDebugBuild("DEBUG ERROR: Instantiate returned null! Unity failed to create the object.");
                        }
                        else
                        {
                            LogIfDebugBuild("DEBUG: Instantiate successful. Checking for NetworkObject component...");
                            NetworkObject netObj = currentNetworkSwirl.GetComponent<NetworkObject>();

                            if (netObj == null)
                            {
                                LogIfDebugBuild("DEBUG ERROR: No NetworkObject component found on the spawned swirl!");
                            }
                            else
                            {
                                LogIfDebugBuild($"DEBUG: NetworkObject found. IsSpawned before: {netObj.IsSpawned}. Calling Spawn()...");

                                try
                                {
                                    netObj.Spawn(true);
                                    LogIfDebugBuild($"DEBUG: Spawn called successfully. IsSpawned after: {netObj.IsSpawned}");
                                }
                                catch (System.Exception ex)
                                {
                                    LogIfDebugBuild($"DEBUG CRITICAL ERROR: Netcode threw an exception during Spawn()! {ex.Message}\n{ex.StackTrace}");
                                }
                            }
                        }
                    }
                    LogIfDebugBuild("--- DEBUG: END SWIRL SPAWN SEQUENCE ---");
                }
            }
            else
            {
                LogIfDebugBuild("No AI nodes found within 10 units. Teleport cancelled");
            }
        }

        private void ExecuteDelayedTeleport()
        {
            isPreparingTeleport = false;
            tpRollTimer = 0f;

            if (IsServer)
            {
                if (currentNetworkSwirl != null)
                {
                    currentNetworkSwirl.GetComponent<NetworkObject>().Despawn(true);
                    currentNetworkSwirl = null;
                }
            }

            if (pendingTeleportTarget == null || pendingTeleportTarget.isPlayerDead)
            {
                LogIfDebugBuild("Target lost, no tp for me :(");
                return;
            }
            float currentDistance = Vector3.Distance(pendingTeleportPosition, pendingTeleportTarget.transform.position);

            if (currentDistance <= maxDistanceAfterWarning && currentDistance > minDistanceAfterWarning)
            {
                LogIfDebugBuild($"Player is {currentDistance} units away (Cutoff is {maxDistanceAfterWarning}). I am tp'ing!!");
                if (IsServer)
                {
                    TeleportObakeClientRpc(pendingTeleportPosition);
                }
                else
                {
                    TeleportObakeServerRpc(pendingTeleportPosition);
                }
            }
            else
            {
                LogIfDebugBuild($"Player too far or too close to TP smoke! They are {currentDistance} units away. Teleport cancelled.");
            }
        }

        private void TeleportObakeLocally(Vector3 newPos)
        {
            agent.enabled = false;
            transform.position = newPos;
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(newPos);
            }
            serverPosition = newPos;

            if (creatureSFX != null && teleportSFX != null)
            {
                var clip = teleportSFX.Length > 0 ? teleportSFX[enemyRandom.Next(teleportSFX.Length)] : null;
                if (clip != null)
                {
                    creatureSFX.PlayOneShot(clip);
                }
            }

            if (creatureSFX != null && laughSFX != null)
            {
                creatureSFX.PlayOneShot(laughSFX);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TeleportObakeServerRpc(Vector3 newPos)
        {
            TeleportObakeClientRpc(newPos);
        }

        [ClientRpc]
        public void TeleportObakeClientRpc(Vector3 newPos)
        {
            TeleportObakeLocally(newPos);
        }

        private void SetVisibility()
        {
            float num = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, base.transform.position + Vector3.up * 0.7f);

            float alphaCutoff = (num - minDistance) / (maxDistance - minDistance);

            float clampedCutoff = Mathf.Clamp(alphaCutoff, 0.01f, 1f);

            for (int i = 0; i < thisMaterial.Length; i++)
            {
                if (thisMaterial[i] != null)
                {
                    thisMaterial[i].SetFloat("_AlphaCutoff", clampedCutoff);
                }
            }

            FadeTransparentParticle(mistParticles, clampedCutoff);
            FadeTransparentParticle(decayingMatterParticles, clampedCutoff);

            PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
            if (!localPlayerController.isPlayerDead && localPlayerController != null && num < 15f && num > maxDistance + 2f)
            {
                localPlayerController.IncreaseFearLevelOverTime(0.37f, 0.25f);
            }

            if (stunNormalizedTimer>0f)
        }

        private void FadeTransparentParticle(ParticleSystem ps, float fadeValue)
        {
            if (ps == null) return;
            ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();

            if (psRenderer != null && psRenderer.material != null)
            {
                if (psRenderer.material.HasProperty("_Color"))
                {
                    Color matColor = psRenderer.material.GetColor("_Color");
                    matColor.a = 1f - fadeValue;
                    psRenderer.material.SetColor("_Color", matColor);
                }
            }
        }

        public void HearGramophone(Vector3 gramophonePosition)
        {
            if (hasHeardGramophone) return;
            hasHeardGramophone = true;
            timesinceHearingGramophone = 0f;
            gramoDash = agent.speed * 1.5f;
            LogIfDebugBuild("The Obake hears the Gramophone!");

            SetDestinationToPosition(gramophonePosition);
        }

        public void DefeatObake()
        {
            if (isEnemyDead) return;
            LogIfDebugBuild("Gramophone fully wound. Obake defeated!");

            SpawnRapierLocally();
            base.KillEnemy(true);
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            if (!(timeSinceLastAttack < 2f))
            {
                PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
                if (playerControllerB != null)
                {
                    timeSinceLastAttack = 0f;
                    playerControllerB.DamagePlayer(40, hasDamageSFX: true, callRPC: true, CauseOfDeath.Stabbing);
                    HitPlayerServerRpc();
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1f);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void HitPlayerServerRpc()
        {
            HitPlayerClientRpc();
        }

        [ClientRpc]
        public void HitPlayerClientRpc()
        {
            if (!isEnemyDead)
            {
                creatureAnimator.SetTrigger("isAttacking");
                if (BloodParticles != null)
                {
                    ParticleSystem particles = BloodParticles.GetComponent<ParticleSystem>();
                    if (particles != null)
                    {
                        particles.Play();
                    }
                }
                creatureSFX.PlayOneShot(swingSFX);
            }
            if (base.IsOwner)
            {
                agent.speed = 2f;
            }
        }
    }
}