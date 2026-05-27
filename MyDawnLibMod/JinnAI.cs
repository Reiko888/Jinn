using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;

namespace Jinn
{
    class JinnAI : EnemyAI
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

        public GameObject GramophonePropPrefab;

        private bool hasHeardGramophone = false;

        private Vector3 lastPosition;

        private bool hasLOS;

        public float chaseTimer = 0f;

        private bool isPreparingTeleport = false;

        private float teleportWarningTimer = 0f;

        private Vector3 pendingTeleportPosition;

        private PlayerControllerB pendingTeleportTarget;

        private bool hasAttemptedLastDitchTp = false;

        public float teleportWarningDelay = 6f;

        public float maxDistanceAfterWarning = 18f;

        public float minDistanceAfterWarning = 8f;

        private bool isFlickeringLights = false;

        private bool wasBeingBurned = false;

        public bool isCurrentlyBurning = false;

        public Renderer[] rapierRenderers;

        private float gramoDash;

        private float timeSinceLastAttack;

        private float timeSinceSeen;

        private float timesinceHearingGramophone;

        private float tpRollTimer;

        private float continuousChaseTimer = 0f;

        public float teleportCooldown = 15f;

        private float teleportCooldownTimer = 0f;

        public AudioSource movementAudio;

        public AudioClip unsheathSFX;

        public AudioClip laughSFX;

        public AudioClip swingSFX;

        public AudioClip[] teleportSFX;

        public AudioClip chargeUpTPSFX;

        public AudioClip creaturePainSFX; 

        public ParticleSystem mistParticles;

        public ParticleSystem decayingMatterParticles;



        [Conditional("DEBUG")]
        void LogIfDebugBuild(string text)
        {
            if (Plugin.Logger != null)
            {
                Plugin.Logger.LogInfo(text);
            }
            else
            {

                UnityEngine.Debug.Log($"[Jinn] {text}");
            }
        }

        public override void Awake()
        {
            base.Awake();
        }

        public override void Start()
        {
            base.Start();
            List<Material> combinedMaterials = new List<Material>();

            if (skin != null)
            {
                combinedMaterials.AddRange(skin.materials);
            }
            if (rapierRenderers != null)
            {
                foreach (Renderer r in rapierRenderers)
                {
                    if (r != null)
                    {
                        combinedMaterials.AddRange(r.materials);
                    }
                }
            }
            thisMaterial = combinedMaterials.ToArray();
            base.GetAINodes();

            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            if (creatureVoice != null)
            {
                creatureVoice.Play();
            }

            LogIfDebugBuild("Jinn Spawned");
            SpawnCursedGramophoneLocally();
        }

        private void OnEnable()
        {
            JinnEventManager.OnShipLeft += HandleShipLeft;
        }

        private void OnDisable()
        {
            JinnEventManager.OnShipLeft -= HandleShipLeft;
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

            if (GramophonePropPrefab == null)
            {
                LogIfDebugBuild("GramophonePropPrefab is missing! Cannot spawn the prop.");
                return;
            }

            Vector3 safeSpawnPos = transform.position + (Vector3.up * 1.5f);
            bool foundValidSpawn = false;

            RandomScrapSpawn[] allScrapSpawns = FindObjectsOfType<RandomScrapSpawn>();

            if (allScrapSpawns != null && allScrapSpawns.Length > 0 && allAINodes != null && allAINodes.Length > 0)
            {
                for (int i = 0; i < 15; i++)
                {
                    RandomScrapSpawn chosenScrapNode = allScrapSpawns[enemyRandom.Next(0, allScrapSpawns.Length)];

                    bool nearAINode = false;
                    foreach (GameObject aiNode in allAINodes)
                    {
                        if (aiNode != null && Vector3.Distance(chosenScrapNode.transform.position, aiNode.transform.position) <= 15f)
                        {
                            nearAINode = true;
                            break;
                        }
                    }

                    if (nearAINode)
                    {
                        Vector3 testPos = RoundManager.Instance.GetNavMeshPosition(chosenScrapNode.transform.position, RoundManager.Instance.navHit, 3f, -1);

                        if (RoundManager.Instance.GotNavMeshPositionResult)
                        {
                            safeSpawnPos = testPos;
                            foundValidSpawn = true;
                            break;
                        }
                    }
                }
            }

            if (!foundValidSpawn && allAINodes != null && allAINodes.Length > 0)
            {
                GameObject fallbackNode = allAINodes[enemyRandom.Next(0, allAINodes.Length)];
                if (fallbackNode != null)
                {
                    safeSpawnPos = fallbackNode.transform.position;
                    LogIfDebugBuild("Failed to find valid scrap node. Falling back to an AI Node");
                }
            }

            safeSpawnPos.y += 1.0f;
            GameObject gramophoneProp = Instantiate(GramophonePropPrefab, safeSpawnPos, Quaternion.identity);
            gramophoneProp.GetComponent<NetworkObject>().Spawn();

            LogIfDebugBuild("Dropped Gramophone PROP at valid node!");
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

            LogIfDebugBuild("Dropped Rapier item upon Jinn defeat");
        }

        public override void Update()
        {
            base.Update();
            timeSinceLastAttack += Time.deltaTime;
            if (isCurrentlyBurning)
            {
                stunNormalizedTimer = 1f;
            }

            SetVisibility();

            DrainLocalFlashlightBattery();

            if (isEnemyDead) return;
            if (StartOfRound.Instance.allPlayersDead) return;

            if (!hasLOS && chaseTimer > 0f)
            {
                chaseTimer -= Time.deltaTime;
            }
            if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;

            if (!base.IsOwner) return;

            timeSinceSeen += Time.deltaTime;
            timesinceHearingGramophone += Time.deltaTime;

            bool isBurnedNow = IsBeingBurnedByFlashlight();

            if (isBurnedNow)
            {
                agent.speed = 1f; 

                if (!wasBeingBurned)
                {
                    wasBeingBurned = true;
                    SetBurningStateClientRpc(true);
                }
            }
            else
            {
                if (wasBeingBurned)
                {
                    wasBeingBurned = false;
                    SetBurningStateClientRpc(false);
                }
            }

            if (stunNormalizedTimer > 0f && !isFlickeringLights && !isEnemyDead)
            {
                StartCoroutine(FlickerLightsDuringStun());
            }

            if (isPreparingTeleport)
            {
                teleportWarningTimer -= Time.deltaTime;
                if (teleportWarningTimer <= 0f)
                {
                    ExecuteDelayedTeleport();
                }
            }

            //if(hasLOS==true)
            //{
            //    creatureVoice.Pitch(0.5f);
            //}

            if (isEnemyDead)
            {
                agent.speed = 0f;
            }
            else
            {
                if (teleportCooldownTimer > 0f)
                {
                    teleportCooldownTimer -= Time.deltaTime;
                }

                if (targetPlayer != null)
                {
                    continuousChaseTimer += Time.deltaTime;

                    if (continuousChaseTimer >= 5f && !isPreparingTeleport && teleportCooldownTimer <= 0f)
                    {
                        tpRollTimer += Time.deltaTime;

                        if (tpRollTimer >= 5f)
                        {
                            tpRollTimer = 0f;

                            if (UnityEngine.Random.value <= 0.80f)
                            {
                                LogIfDebugBuild("Teleport has been accepted");
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
                }
                else if (timeSinceLastAttack < 2f)
                {
                    agent.speed = 2f;
                }
                else
                {
                    agent.speed = 7f;
                }
            }
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

            if (targetPlayer != null)
            {
                if (CheckLineOfSightForPosition(targetPlayer.gameplayCamera.transform.position, 120f, 60))
                {
                    hasLOS = true;
                    timeSinceSeen = 0f;
                    chaseTimer = 10f;
                    lastPosition = targetPlayer.transform.position;
                    hasAttemptedLastDitchTp = false; 

                    if (currentSearch != null && currentSearch.inProgress) StopSearch(currentSearch);
                    SetMovingTowardsTargetPlayer(targetPlayer);
                }
                else
                {
                    hasLOS = false;

                    if (chaseTimer <= 0f)
                    {
                        CheckAndFireLastDitchTeleport();

                        targetPlayer = null;
                        if (currentSearch == null || !currentSearch.inProgress) StartSearch(base.transform.position);
                    }
                    else
                    {
                        SetDestinationToPosition(lastPosition);

                        if (Vector3.Distance(transform.position, lastPosition) <= 2f)
                        {
                            CheckAndFireLastDitchTeleport();
                            chaseTimer = 0f;
                        }
                        else if (chaseTimer <= 7f)
                        {
                            CheckAndFireLastDitchTeleport();
                        }
                    }
                }
            }
            else
            {
                hasLOS = false;

                if (TargetClosestPlayer(5f, requireLineOfSight: true, 120f))
                {
                    hasLOS = true;
                    timeSinceSeen = 0f;
                    chaseTimer = 10f;
                    lastPosition = targetPlayer.transform.position;
                    hasAttemptedLastDitchTp = false;

                    if (currentSearch != null && currentSearch.inProgress) StopSearch(currentSearch);
                    SetMovingTowardsTargetPlayer(targetPlayer);
                }
                else if (currentSearch == null || !currentSearch.inProgress)
                {
                    StartSearch(base.transform.position);
                }
            }
        }

        private void CheckAndFireLastDitchTeleport()
        {
            if (!hasAttemptedLastDitchTp && !isPreparingTeleport && targetPlayer != null && teleportCooldownTimer <= 0f)
            {
                hasAttemptedLastDitchTp = true;

                if (UnityEngine.Random.value <= 0.75f)
                {
                    LogIfDebugBuild("Lost target, attempting TP.");
                    TpToCutoffNode(targetPlayer);
                }
            }
        }

        private void TpToCutoffNode(PlayerControllerB targetPlayer)
        {
            if (targetPlayer == null || targetPlayer.isPlayerDead || allAINodes == null || allAINodes.Length == 0) return;

            Vector3 playerVelocity = targetPlayer.thisController.velocity;

            if (playerVelocity.sqrMagnitude < 1f)
            {
                LogIfDebugBuild("Player is standing still. Falling back to normal LDTp.");
                TpNearestNode(targetPlayer);
                return;
            }

            Vector3 predictedPosition = targetPlayer.transform.position + (playerVelocity.normalized * 15f);

            List<GameObject> validNodes = new List<GameObject>();
            float cutoffRadius = 12f; 

            foreach (GameObject node in allAINodes)
            {
                if (node == null) continue;

                if (Vector3.Distance(node.transform.position, predictedPosition) <= cutoffRadius)
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

                if (IsServer && TeleportSwirl != null)
                {
                    Vector3 spawnPos = pendingTeleportPosition + (Vector3.up * 1.5f);
                    currentNetworkSwirl = Instantiate(TeleportSwirl, spawnPos, Quaternion.identity);
                    if (currentNetworkSwirl != null)
                    {
                        NetworkObject netObj = currentNetworkSwirl.GetComponent<NetworkObject>();
                        if (netObj != null) netObj.Spawn(true);
                    }
                }
            }
            else
            {
                LogIfDebugBuild("No nodes found ahead of the player. Falling back to normal LDTp.");
                TpNearestNode(targetPlayer);
            }
        }

        private bool IsBeingBurnedByFlashlight()
        {
            PlayerControllerB[] visiblePlayers = GetAllPlayersInLineOfSight(360, 15);

            if (visiblePlayers == null || visiblePlayers.Length == 0) return false;

            foreach (PlayerControllerB player in visiblePlayers)
            {
                Vector3 directionToObake = transform.position - player.transform.position;
                float viewAngle = Vector3.Angle(player.transform.forward, directionToObake);

                // Flashlights have roughly a 30-degree cone. 
                // If the angle is larger, they are facing away.
                if (Mathf.Abs(viewAngle) > 30f) continue;

                if (player.pocketedFlashlight != null && player.pocketedFlashlight.isBeingUsed)
                {
                    return true;
                }

                GrabbableObject heldItem = player.currentlyHeldObjectServer;
                if (player.isHoldingObject && heldItem != null)
                {
                    if (heldItem is FlashlightItem flashlight && flashlight.isBeingUsed)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [ClientRpc]
        public void SetBurningStateClientRpc(bool isBurning)
        {
            isCurrentlyBurning = isBurning;
            creatureAnimator.SetBool("isBeingBurned", isBurning);

            if (isBurning)
            {
                LogIfDebugBuild("Jinn is being burned");
                if (creatureVoice != null) creatureVoice.Pause();
                if (creatureSFX != null && creaturePainSFX != null) creatureSFX.PlayOneShot(creaturePainSFX);

                if (!isFlickeringLights && !isEnemyDead)
                {
                    StartCoroutine(FlickerLightsDuringStun());
                }
            }
            else
            {

                if (creatureVoice != null) creatureVoice.Play();
            }
        }

        private IEnumerator FlickerLightsDuringStun()
        {
            isFlickeringLights = true;

            Light[] allLights = FindObjectsOfType<Light>();
            Dictionary<Light, float> originalIntensities = new Dictionary<Light, float>();
            List<Light> nearbyLights = new List<Light>();

            float radiusSq = 10f * 10f;

            foreach (Light l in allLights)
            {
                if (l == null || l.type == LightType.Directional) continue;

                string nameLower = l.gameObject.name.ToLower();
                if (nameLower.Contains("helmet") || nameLower.Contains("visor") || nameLower.Contains("sun")) continue;

                if ((l.transform.position - transform.position).sqrMagnitude <= radiusSq)
                {
                    nearbyLights.Add(l);
                    originalIntensities[l] = l.intensity;
                }
            }

            while (stunNormalizedTimer > 0f && !isEnemyDead)
            {
                foreach (Light l in nearbyLights)
                {
                    if (l != null && originalIntensities.ContainsKey(l))
                    {
                        float rand = UnityEngine.Random.value;
                        if (rand > 0.8f) l.intensity = originalIntensities[l] * 2.5f; // Intense flash
                        else if (rand > 0.5f) l.intensity = originalIntensities[l] * 0.1f; // Dim out
                        else l.intensity = originalIntensities[l]; // Normal
                    }
                }

                yield return new WaitForSeconds(0.1f);
            }

            foreach (var kvp in originalIntensities)
            {
                if (kvp.Key != null) kvp.Key.intensity = kvp.Value;
            }

            isFlickeringLights = false;
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
                teleportCooldownTimer = teleportCooldown;

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

            float obakeToPlayerDist = Vector3.Distance(transform.position, pendingTeleportTarget.transform.position);
            float swirlToPlayerDist = Vector3.Distance(pendingTeleportPosition, pendingTeleportTarget.transform.position);

            if (obakeToPlayerDist <= 4f)
            {
                LogIfDebugBuild($"Player is only {obakeToPlayerDist} units away from Obake. Canceling teleport to attack");
                return;
            }

            if (obakeToPlayerDist <= swirlToPlayerDist)
            {
                LogIfDebugBuild($"Obake ({obakeToPlayerDist}u) is closer than smoke ({swirlToPlayerDist}u). Cancelling teleport");
                return;
            }

            if (swirlToPlayerDist <= maxDistanceAfterWarning)
            {
                LogIfDebugBuild($"Player is {swirlToPlayerDist} units away from Swirl. I am tp'ing!!");
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
                LogIfDebugBuild($"Player too far from TP smoke! They are {swirlToPlayerDist} units away from Swirl.");
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

        private void DrainLocalFlashlightBattery()
        {
            if (isEnemyDead) return;

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer == null || localPlayer.isPlayerDead) return;

            float dist = Vector3.Distance(transform.position, localPlayer.transform.position);

            FlashlightItem activeFlashlight = null;

            if (localPlayer.pocketedFlashlight != null && localPlayer.pocketedFlashlight is FlashlightItem pocketLight)
            {
                activeFlashlight = pocketLight;
            }
            else if (localPlayer.currentlyHeldObjectServer != null && localPlayer.currentlyHeldObjectServer is FlashlightItem heldLight)
            {
                activeFlashlight = heldLight;
            }

            if (activeFlashlight != null)
            {

                if (dist > 30f || !activeFlashlight.isBeingUsed)
                {
                    if (activeFlashlight.flashlightInterferenceLevel == 1)
                    {
                        activeFlashlight.flashlightInterferenceLevel = 0;
                    }
                    return; // Stop draining
                }

                if (activeFlashlight.insertedBattery != null && activeFlashlight.insertedBattery.charge > 0f)
                {
                    activeFlashlight.insertedBattery.charge -= (Time.deltaTime * 0.15f);

                    activeFlashlight.flashlightInterferenceLevel = 1;

                    LogIfDebugBuild($"Distance: {dist}. Battery now at: {activeFlashlight.insertedBattery.charge}");

                    if (activeFlashlight.insertedBattery.charge <= 0f)
                    {
                        activeFlashlight.insertedBattery.charge = 0f;
                        activeFlashlight.insertedBattery.empty = true;
                        activeFlashlight.UseUpBatteries();
                        activeFlashlight.flashlightInterferenceLevel = 0;
                    }
                }
            }
        }

        private void SetVisibility()
        {
            float num = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, base.transform.position + Vector3.up * 0.7f);

            float alphaCutoff = (num - minDistance) / (maxDistance - minDistance);

            float clampedCutoff = Mathf.Clamp(alphaCutoff, 0.01f, 1f);

            if (stunNormalizedTimer > 0f)
            {
                float flickerInstabilitySpeed = 10f;
                clampedCutoff = Mathf.Repeat(Time.time * flickerInstabilitySpeed, 1f) > 0.5f ? 1f : 0.01f;
            }

            for (int i = 0; i < thisMaterial.Length; i++)
            {
                if (thisMaterial[i] != null)
                {
                    if (thisMaterial[i].HasProperty("_AlphaCutoff"))
                    {
                        thisMaterial[i].SetFloat("_AlphaCutoff", clampedCutoff);
                    }
                }
            }

            FadeTransparentParticle(mistParticles, clampedCutoff);
            FadeTransparentParticle(decayingMatterParticles, clampedCutoff);

            PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
            if (!localPlayerController.isPlayerDead && localPlayerController != null && num < 15f && num > maxDistance + 2f)
            {
                localPlayerController.IncreaseFearLevelOverTime(0.37f, 0.25f);
            }
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
            LogIfDebugBuild("The Jinn hears the Gramophone");

            SetDestinationToPosition(gramophonePosition);
        }

        public void DefeatObake()
        {
            if (isEnemyDead) return;
            LogIfDebugBuild("Gramophone fully wound");

            DefeatObakeServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void DefeatObakeServerRpc()
        {
            DefeatObakeClientRpc();
        }

        [ClientRpc]
        public void DefeatObakeClientRpc()
        {
            if (isEnemyDead) return;
            LogIfDebugBuild("Jinn defeated");

            if (IsServer && currentNetworkSwirl != null)
            {
                NetworkObject netObj = currentNetworkSwirl.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
            }

            if (creatureVoice != null)
            {
                creatureVoice.Stop();
            }
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