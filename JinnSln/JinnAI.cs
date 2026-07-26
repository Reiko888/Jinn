using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using Unity.Services.Authentication.Generated;
using UnityEngine;

namespace Jinn
{
    class JinnAI : EnemyAI
    {
        System.Random enemyRandom = null!;
        private int baseSpeed;
        private int attackCooldownSlow;
        private float minDistance;
        private float maxDistance;
        private bool canTeleport;
        private float teleportWarningDelay;
        private float maxDistanceAfterWarning;
        private float minDistanceAfterWarning;
        private bool canBeBurned;
        private bool burnLights;
        private int flashlightDrainPercentage;
        private int burnSlowdown;
        private int attackDamage;
        public SkinnedMeshRenderer skin;
        public Material[] thisMaterial;
        public GameObject BloodParticles;
        public GameObject TeleportSwirl;
        private GameObject currentNetworkSwirl;
        public GameObject GramophonePropPrefab;
        private GameObject spawnedGramophone;
        private bool hasHeardGramophone = false;
        private Vector3 lastPosition;
        private bool hasLOS;
        public float chaseTimer = 0f;
        private bool isPreparingTeleport = false;
        private float teleportWarningTimer = 0f;
        private Vector3 pendingTeleportPosition;
        private PlayerControllerB pendingTeleportTarget;
        private bool hasAttemptedLastDitchTp = false;
        private bool isManifesting = false;
        private bool isFlickeringLights = false;
        private bool wasBeingBurned = false;
        public bool isCurrentlyBurning = false;
        private bool isChasingPitch = false;
        private bool isCloseToTarget = false;
        public Renderer[] rapierRenderers;
        private float gramoDash;
        private float timeSinceLastAttack;
        private float timeSinceSeen;
        private float timesinceHearingGramophone;
        private float tpRollTimer;
        private float continuousChaseTimer = 0f;
        public float teleportCooldown = 15f;
        private float teleportCooldownTimer = 0f;
        private float flyingAudioCooldownTimer = 0f;
        private int playerWinding;
        private DoorLock[] cachedDoors;
        private Dictionary<DoorLock, float> doorSlamCooldowns = new Dictionary<DoorLock, float>();

        //AUDIO
        public AudioSource movementAudio;
        public AudioSource flyingAudio;
        public AudioSource whooshAudio;
        public AudioClip whooshSFX;
        public AudioClip[] flyingClips;
        public AudioClip unsheathSFX;
        public AudioClip laughSFX;
        public AudioClip swingSFX;
        public AudioClip[] teleportSFX;
        public AudioClip chargeUpTPSFX;
        public AudioClip creaturePainSFX;

        //PARTICLES
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

        public override void Start()
        {
            base.Start();
            baseSpeed = JinnContentHandler.Instance.jinnAssets.GetConfig<int>("ConfigJinnBaseSpeed").Value;
            attackCooldownSlow = JinnContentHandler.Instance.jinnAssets.GetConfig<int>("ConfigJinnAttackCooldownSlow").Value;
            minDistance = JinnContentHandler.Instance.jinnAssets.GetConfig<float>("ConfigJinnMinVisibleDist").Value;
            maxDistance = JinnContentHandler.Instance.jinnAssets.GetConfig<float>("ConfigJinnMaxVisibleDist").Value;
            canTeleport = JinnContentHandler.Instance.jinnAssets.GetConfig<bool>("ConfigJinnCanTeleport").Value;
            teleportWarningDelay = JinnContentHandler.Instance.jinnAssets.GetConfig<float>("ConfigJinnTPWarnDelay").Value;
            maxDistanceAfterWarning = JinnContentHandler.Instance.jinnAssets.GetConfig<float>("ConfigJinnMaxDistAfterDelay").Value;
            minDistanceAfterWarning = JinnContentHandler.Instance.jinnAssets.GetConfig<float>("ConfigJinnMinnDistAfterDelay").Value;
            canBeBurned = JinnContentHandler.Instance.jinnAssets.GetConfig<bool>("ConfigJinnCanBeBurned").Value;
            burnLights = JinnContentHandler.Instance.jinnAssets.GetConfig<bool>("ConfigJinnBurnLights").Value;
            flashlightDrainPercentage = JinnContentHandler.Instance.jinnAssets.GetConfig<int>("ConfigJinnFlashlightConsumption").Value;
            burnSlowdown = JinnContentHandler.Instance.jinnAssets.GetConfig<int>("ConfigJinnBurnSlowdown").Value;
            attackDamage = JinnContentHandler.Instance.jinnAssets.GetConfig<int>("ConfigJinnAttackDamage").Value;

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
            cachedDoors = UnityEngine.Object.FindObjectsOfType<DoorLock>();

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

            if (IsServer && spawnedGramophone != null)
            {
                NetworkObject netObj = spawnedGramophone.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
            }

            if (IsServer && currentNetworkSwirl != null)
            {
                NetworkObject netObj = currentNetworkSwirl.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
            }

            KillEnemy(true);
        }

        private Vector3 GetFloorPosition(Vector3 startPos)
        {
            Vector3 rayStart = startPos + (Vector3.up * 0.5f);

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return startPos;
        }
        public void SpawnCursedGramophoneLocally()
        {
            if (!IsServer) return;
            bool enableGramophone = JinnContentHandler.Instance.jinnAssets.GetConfig<bool>("ConfigCanSpawnGramo").Value;
            if (!enableGramophone) return;

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

            safeSpawnPos = GetFloorPosition(safeSpawnPos);
            spawnedGramophone = Instantiate(GramophonePropPrefab, safeSpawnPos, Quaternion.identity);
            spawnedGramophone.GetComponent<NetworkObject>().Spawn();

            LogIfDebugBuild("Dropped Gramophone at random node");
        }

        public void SpawnRapierLocally()
        {
            if (!IsServer) return;
            bool enableRapier = JinnContentHandler.Instance.jinnAssets.GetConfig<bool>("ConfigDropRapierOnDeath").Value;
            if (!enableRapier) return;
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

            GameObject rapierDrop = Instantiate(rapierItem.spawnPrefab, safeSpawnPos, Quaternion.identity, StartOfRound.Instance.propsContainer);
            GrabbableObject rapierGrabbable = rapierDrop.GetComponent<GrabbableObject>();

            rapierGrabbable.fallTime = 0f;
            rapierGrabbable.targetFloorPosition = rapierGrabbable.GetItemFloorPosition(safeSpawnPos);

            int scrapValue = (int)(UnityEngine.Random.Range(rapierItem.minValue, rapierItem.maxValue));
            rapierGrabbable.SetScrapValue(scrapValue);

            NetworkObject netObj = rapierDrop.GetComponent<NetworkObject>();
            netObj.Spawn();
            SyncScrapValueClientRpc(netObj, scrapValue);

            rapierGrabbable.EnableItemMeshes(true);

            LogIfDebugBuild("Dropped Rapier item upon Jinn defeat");
        }

        [ClientRpc]
        public void SyncScrapValueClientRpc(NetworkObjectReference netObjRef, int scrapValue)
        {
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.StartCoroutine(DelayedScrapValueSync(netObjRef, scrapValue));
            }
            else
            {

                StartCoroutine(DelayedScrapValueSync(netObjRef, scrapValue));
            }
        }

        private IEnumerator DelayedScrapValueSync(NetworkObjectReference netObjRef, int scrapValue)
        {
            yield return new WaitForSeconds(1f);

            if (netObjRef.TryGet(out NetworkObject netObj))
            {
                GrabbableObject grabObj = netObj.GetComponent<GrabbableObject>();
                if (grabObj != null)
                {
                    grabObj.SetScrapValue(scrapValue);
                }
            }
        }

        public override void Update()
        {
            base.Update();
            if (creatureAnimator != null)
            {
                creatureAnimator.SetBool("isFlying", hasHeardGramophone);
            }
            if (flyingAudioCooldownTimer > 0f)
            {
                flyingAudioCooldownTimer -= Time.deltaTime;
            }
            if (flyingAudio != null && flyingClips != null && flyingClips.Length > 0)
            {
                if (hasHeardGramophone)
                {
                    if (!flyingAudio.isPlaying && flyingAudioCooldownTimer <= 0f)
                    {
                        AudioClip clip = flyingClips[enemyRandom.Next(0, flyingClips.Length)];
                        if (clip != null)
                        {
                            flyingAudio.clip = clip;
                            flyingAudio.Play();
                            flyingAudioCooldownTimer = 15f;
                        }
                    }
                }
                else if (!hasHeardGramophone && flyingAudio.isPlaying)
                {
                    flyingAudio.Stop();
                }
            }
            if (whooshAudio != null && whooshSFX != null)
            {
                if (hasHeardGramophone)
                {
                    if (!whooshAudio.isPlaying)
                    {
                        whooshAudio.clip = whooshSFX;
                        whooshAudio.Play();
                    }
                }
                else if (!hasHeardGramophone && whooshAudio.isPlaying)
                {
                    whooshAudio.Stop();
                }
            }
            timeSinceLastAttack += Time.deltaTime;
            if (isCurrentlyBurning)
            {
                stunNormalizedTimer = 1f;
            }

            SetVisibility();

            DrainLocalFlashlightBattery();

            if (creatureVoice != null)
            {
                float targetPitch = isChasingPitch ? 0.8f : 1.0f;
                creatureVoice.pitch = Mathf.Lerp(creatureVoice.pitch, targetPitch, Time.deltaTime * 0.8f);
                if (isCurrentlyBurning || hasHeardGramophone)
                {
                    if (creatureVoice.isPlaying)
                    {
                        creatureVoice.Pause();
                    }
                }
                else
                {
                    if (!creatureVoice.isPlaying && !isEnemyDead)
                    {
                        creatureVoice.Play();
                    }
                }
            }

            if (isEnemyDead) return;
            if (StartOfRound.Instance.allPlayersDead) return;

            if (!hasLOS && chaseTimer > 0f)
            {
                chaseTimer -= Time.deltaTime;
            }

            if (!base.IsOwner) return;

            SlamNearbyDoorsCheck();

            timeSinceSeen += Time.deltaTime;
            timesinceHearingGramophone += Time.deltaTime;

            bool isBurnedNow = IsBeingBurnedByFlashlight();

            if (isBurnedNow)
            {

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

            if (targetPlayer != null)
            {
                float distToTarget = Vector3.Distance(transform.position, targetPlayer.transform.position);
                if (distToTarget <= maxDistance && !isManifesting)
                {
                    isManifesting = true;
                    SetManifestingStateServerRpc(true);
                }
                else if (distToTarget > maxDistance + 5f && isManifesting)
                {
                    isManifesting = false;
                }
                isCloseToTarget = distToTarget <= 10f;
            }
            else
            {
                isManifesting = false;
            }

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
                if (isCurrentlyBurning)
                {
                    agent.speed = burnSlowdown;
                }
                else if (hasHeardGramophone)
                {
                    agent.speed = gramoDash;

                    if (isCloseToTarget || timesinceHearingGramophone > 35f)
                    {
                        isCloseToTarget = false;
                        CancelGramophoneServerRpc();
                    }
                }
                else if (timeSinceLastAttack < 2f)
                {
                    agent.speed = attackCooldownSlow;
                }
                else
                {
                    agent.speed = baseSpeed;
                }

                syncMovementSpeed = agent.speed;
            }
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;
            if (targetPlayer != null && !hasHeardGramophone)
            {
                if (CheckLineOfSightForPosition(targetPlayer.gameplayCamera.transform.position, 120f, 60))
                {
                    hasLOS = true;
                    if (!isChasingPitch) SetPitchStateServerRpc(true);
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
                        if (isChasingPitch) SetPitchStateServerRpc(false);
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
            else if (hasHeardGramophone)
            {
                if (targetPlayer != null && !targetPlayer.isPlayerDead)
                {
                    if (currentSearch != null && currentSearch.inProgress) StopSearch(currentSearch);
                    SetDestinationToPosition(targetPlayer.transform.position);
                }
                else
                {
                    hasHeardGramophone = false;
                    CancelGramophoneServerRpc();
                }
            }
            else
            {
                hasLOS = false;

                if (TargetClosestPlayer(5f, requireLineOfSight: true, 120f))
                {
                    hasLOS = true;
                    if (!isChasingPitch) SetPitchStateServerRpc(true);
                    timeSinceSeen = 0f;
                    chaseTimer = 10f;
                    lastPosition = targetPlayer.transform.position;
                    hasAttemptedLastDitchTp = false;

                    if (currentSearch != null && currentSearch.inProgress) StopSearch(currentSearch);
                    SetMovingTowardsTargetPlayer(targetPlayer);
                }
                else if (currentSearch == null || !currentSearch.inProgress)
                {
                    if (isChasingPitch) SetPitchStateServerRpc(false);
                    StartSearch(base.transform.position);
                }
            }
            if (IsServer) UpdateJinnOwnership();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            ResetAllFlashlightInterference();

            if (creatureVoice != null) creatureVoice.Stop();

            if (IsServer)
            {
                if (spawnedGramophone != null)
                {
                    NetworkObject netObj = spawnedGramophone.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
                }

                if (currentNetworkSwirl != null)
                {
                    NetworkObject netObj = currentNetworkSwirl.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
                }
            }

            LogIfDebugBuild("OnDestroy successful.");
        }

        private void ResetAllFlashlightInterference()
        {
            if (GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null) return;

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

            if (localPlayer.pocketedFlashlight != null && localPlayer.pocketedFlashlight is FlashlightItem pocketLight)
            {
                pocketLight.flashlightInterferenceLevel = 0;
            }

            if (localPlayer.currentlyHeldObjectServer != null && localPlayer.currentlyHeldObjectServer is FlashlightItem heldLight)
            {
                heldLight.flashlightInterferenceLevel = 0;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPitchStateServerRpc(bool chasing)
        {
            SetPitchStateClientRpc(chasing);
        }

        [ClientRpc]
        public void SetPitchStateClientRpc(bool chasing)
        {
            isChasingPitch = chasing;
        }

        private void CheckAndFireLastDitchTeleport()
        {
            if (hasHeardGramophone) return;
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
            if (hasHeardGramophone) return;
            if (!canTeleport) return;

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
            if (!canBeBurned) return false;

            PlayerControllerB[] visiblePlayers = GetAllPlayersInLineOfSight(360, 15);

            if (visiblePlayers == null || visiblePlayers.Length == 0) return false;

            foreach (PlayerControllerB player in visiblePlayers)
            {
                Vector3 directionToObake = transform.position - player.transform.position;
                float viewAngle = Vector3.Angle(player.transform.forward, directionToObake);

                if (Mathf.Abs(viewAngle) > 30f) continue;

                if (player.pocketedFlashlight != null && player.pocketedFlashlight.isBeingUsed)
                {
                    if (player.pocketedFlashlight.insertedBattery != null && player.pocketedFlashlight.insertedBattery.charge > 0f)
                    {
                        return true;
                    }
                }

                GrabbableObject heldItem = player.currentlyHeldObject;
                if (player.isHoldingObject && heldItem != null && heldItem is FlashlightItem flashlight)
                {
                    if (flashlight.isBeingUsed && flashlight.insertedBattery != null && flashlight.insertedBattery.charge > 0f)
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

                if (burnLights && !isFlickeringLights && !isEnemyDead)
                {
                    StartCoroutine(FlickerLightsDuringStun());
                }
            }
            else
            {
                if (creatureVoice != null) creatureVoice.Play();
                ResetAllFlashlightInterference();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetManifestingStateServerRpc(bool manifesting)
        {
            SetManifestingStateClientRpc(manifesting);
        }

        [ClientRpc]
        public void SetManifestingStateClientRpc(bool manifesting)
        {
            creatureAnimator.SetTrigger("isManifesting");
            creatureSFX.PlayOneShot(unsheathSFX);
        }

        private IEnumerator FlickerLightsDuringStun()
        {
            isFlickeringLights = true;

            List<FlashlightItem> flashlightsNearJinn = new List<FlashlightItem>();
            FlashlightItem[] allFlashlights = UnityEngine.Object.FindObjectsOfType<FlashlightItem>();
            if (allFlashlights != null)
            {
                for (int i = 0; i < allFlashlights.Length; i++)
                {
                    FlashlightItem fl = allFlashlights[i];
                    if (fl != null && fl.playerHeldBy != null && Vector3.Distance(fl.playerHeldBy.transform.position, transform.position) <= 15f)
                    {
                        flashlightsNearJinn.Add(fl);
                    }
                }
            }

            for (int i = 0; i < flashlightsNearJinn.Count; i++)
            {
                FlashlightItem fl = flashlightsNearJinn[i];
                if (fl != null)
                {
                    if (fl.flashlightAudio != null && fl.flashlightFlicker != null)
                    {
                        fl.flashlightAudio.PlayOneShot(fl.flashlightFlicker);
                        WalkieTalkie.TransmitOneShotAudio(fl.flashlightAudio, fl.flashlightFlicker, 0.8f);
                    }
                    if (fl.playerHeldBy != null && fl.playerHeldBy.isInsideFactory)
                    {
                        fl.flashlightInterferenceLevel = 2;
                    }
                }
            }

            List<Animator> lightsNearJinn = new List<Animator>();
            if (RoundManager.Instance != null && RoundManager.Instance.allPoweredLightsAnimators != null)
            {
                for (int i = 0; i < RoundManager.Instance.allPoweredLightsAnimators.Count; i++)
                {
                    Animator animator = RoundManager.Instance.allPoweredLightsAnimators[i];
                    if (animator != null && Vector3.Distance(animator.transform.position, transform.position) <= 15f)
                    {
                        lightsNearJinn.Add(animator);
                    }
                }
            }

            if (lightsNearJinn.Count > 0)
            {
                int loopCount = 0;
                int b = 4;
                while (b > 0 && b != 0)
                {
                    int limit = lightsNearJinn.Count / b;
                    for (int j = loopCount; j < limit; j++)
                    {
                        if (j < lightsNearJinn.Count && lightsNearJinn[j] != null)
                        {
                            lightsNearJinn[j].SetTrigger("Flicker");
                        }
                        loopCount++;
                    }
                    yield return new WaitForSeconds(0.05f);
                    b--;
                }
            }

            yield return new WaitForSeconds(0.3f);

            for (int i = 0; i < flashlightsNearJinn.Count; i++)
            {
                FlashlightItem fl = flashlightsNearJinn[i];
                if (fl != null)
                {
                    fl.flashlightInterferenceLevel = 0;
                }
            }

            isFlickeringLights = false;
        }

        private void TpNearestNode(PlayerControllerB targetPlayer)
        {
            if (hasHeardGramophone) return;
            if (!canTeleport) return;

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
                    if (TeleportSwirl != null)
                    {
                        Vector3 spawnPos = pendingTeleportPosition + (Vector3.up * 1.5f);
                        currentNetworkSwirl = Instantiate(TeleportSwirl, spawnPos, Quaternion.identity);

                        if (currentNetworkSwirl != null)
                        {
                            NetworkObject netObj = currentNetworkSwirl.GetComponent<NetworkObject>();
                            if (netObj != null)
                            {
                                try
                                {
                                    netObj.Spawn(true);
                                }
                                catch (System.Exception ex)
                                {
                                    LogIfDebugBuild($"DEBUG CRITICAL ERROR: Netcode threw an exception during Spawn()! {ex.Message}\n{ex.StackTrace}");
                                }
                            }
                        }
                    }
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

            if (swirlToPlayerDist <= maxDistanceAfterWarning && swirlToPlayerDist >= minDistanceAfterWarning)
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
            if (GameNetworkManager.Instance == null) return;

            if (isEnemyDead) return;

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer == null || localPlayer.isPlayerDead) return;

            float dist = Vector3.Distance(transform.position, localPlayer.transform.position);

            FlashlightItem activeFlashlight = null;

            if (localPlayer.pocketedFlashlight != null && localPlayer.pocketedFlashlight is FlashlightItem pocketLight)
            {
                activeFlashlight = pocketLight;
            }
            else if (localPlayer.currentlyHeldObject != null && localPlayer.currentlyHeldObject is FlashlightItem heldLight)
            {
                activeFlashlight = heldLight;
            }

            if (activeFlashlight != null)
            {
                Vector3 directionToObake = transform.position - localPlayer.transform.position;
                float viewAngle = Vector3.Angle(localPlayer.transform.forward, directionToObake);
                if (dist > 30f || !activeFlashlight.isBeingUsed || Mathf.Abs(viewAngle) > 30f)
                {
                    if (activeFlashlight.flashlightInterferenceLevel == 1)
                    {
                        activeFlashlight.flashlightInterferenceLevel = 0;
                    }
                    return;
                }

                if (activeFlashlight.insertedBattery != null && activeFlashlight.insertedBattery.charge > 0f)
                {
                    float drainRate = flashlightDrainPercentage / 100f;
                    activeFlashlight.insertedBattery.charge -= (Time.deltaTime * drainRate);

                    activeFlashlight.flashlightInterferenceLevel = 1;

                    LogIfDebugBuild($"Distance: {dist}. Battery now at: {activeFlashlight.insertedBattery.charge}");

                    if (activeFlashlight.insertedBattery.charge <= 0f)
                    {
                        activeFlashlight.insertedBattery.charge = 0f;
                        activeFlashlight.insertedBattery.empty = true;
                        activeFlashlight.UseUpBatteries();
                        activeFlashlight.flashlightInterferenceLevel = 0;

                        activeFlashlight.SyncBatteryServerRpc(0);
                    }
                }
            }
        }

        private void SlamNearbyDoorsCheck()
        {
            if (cachedDoors == null) return;

            foreach (DoorLock door in cachedDoors)
            {
                if (door == null) continue;

                if (Vector3.Distance(door.transform.position, transform.position) < 3f)
                {
                    if (!door.isDoorOpened && (!doorSlamCooldowns.ContainsKey(door) || (Time.time - doorSlamCooldowns[door] >= 5f)))
                    {
                        if (door.isLocked)
                        {
                            door.UnlockDoorServerRpc();
                        }
                        AnimatedObjectTrigger component = door.GetComponent<AnimatedObjectTrigger>();
                        if (component != null)
                        {
                            component.TriggerAnimationNonPlayer(true, true, false);
                        }
                        door.OpenDoorAsEnemyServerRpc();
                        doorSlamCooldowns[door] = Time.time;
                    }
                }
            }
        }

        private void UpdateJinnOwnership()
        {
            if (!IsServer) return;
            if (isEnemyDead) return;

            ulong targetOwnerId = 0UL;
            if (hasHeardGramophone && targetPlayer != null)
            {
                targetOwnerId = targetPlayer.actualClientId;
            }
            else if (targetPlayer != null)
            {
                targetOwnerId = targetPlayer.actualClientId;
            }

            if (NetworkObject.OwnerClientId != targetOwnerId)
            {
                ChangeOwnershipOfEnemy(targetOwnerId);
            }
        }

        private void SetVisibility()
        {
            if (StartOfRound.Instance == null || GameNetworkManager.Instance == null) return;

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

            if (localPlayerController != null && !localPlayerController.isPlayerDead && num < 15f && num > maxDistance + 2f)
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

        [ServerRpc(RequireOwnership = false)]
        public void HearGramophoneServerRpc(int playerClientId)
        {
            HearGramophoneClientRpc(playerClientId);
        }

        [ClientRpc]
        public void HearGramophoneClientRpc(int playerClientId)
        {
            if (hasHeardGramophone) return;
            hasHeardGramophone = true;
            timesinceHearingGramophone = 0f;
            gramoDash = baseSpeed * 2.5f;
            playerWinding = playerClientId;
            PlayerControllerB windingPlayer = StartOfRound.Instance.allPlayerScripts[playerClientId];
            Plugin.Logger.LogInfo($"HearGramophoneClientRpc: playerClientId={playerClientId}, windingPlayer={windingPlayer?.playerUsername}, flyingAudio={(flyingAudio != null)}, flyingClips={(flyingClips != null ? flyingClips.Length.ToString() : "null")}, whooshAudio={(whooshAudio != null)}, whooshSFX={(whooshSFX != null ? whooshSFX.name : "null")}");
            if (windingPlayer != null && !windingPlayer.isPlayerDead)
            {
                targetPlayer = windingPlayer;
                SetDestinationToPosition(windingPlayer.transform.position);
            }
            LogIfDebugBuild("The Jinn hears the Gramophone");
            if (IsServer) UpdateJinnOwnership();
        }

        [ServerRpc(RequireOwnership = false)]
        public void CancelGramophoneServerRpc()
        {
            CancelGramophoneClientRpc();
        }

        [ClientRpc]
        public void CancelGramophoneClientRpc()
        {
            if (hasHeardGramophone)
            {
                hasHeardGramophone = false;
                LogIfDebugBuild("Gramophone reached");
                if (IsServer) UpdateJinnOwnership();
            }
        }

        public void DefeatObake()
        {
            if (isEnemyDead) return;
            LogIfDebugBuild("Gramophone fully wound");

            DefeatObakeServerRpc();
        }

        public override void KillEnemy(bool destroy = false)
        {
            base.KillEnemy(destroy);
            ResetAllFlashlightInterference();
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
            if (IsServer)
            {
                ChangeOwnershipOfEnemy(0UL);
            }
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

                    playerControllerB.DamagePlayer(attackDamage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Stabbing);
                    HitPlayerServerRpc();

                    if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController != null)
                    {
                        GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1f);
                    }
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
                agent.speed = attackCooldownSlow;
            }
        }
    }
}