using GameNetcodeStuff;
using Obake;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

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

        public GameObject VoidFace;

        private bool hasHeardGramophone = false;

        //private float footstepTimer = 0f;

        private bool hasLOS;

        public AISearchRoutine searchRoutine;

        public float chaseTimer = 0f;


        private float gramoDash;

        private float timeSinceLastAttack;

        private float timeSinceSeen;

        private float manifestAnimCooldown;

        public AudioClip[] customFootstepSounds;

        public AudioSource movementAudio;

        public AudioClip emergeSFX;

        public AudioClip laughSFX;

        public AudioClip swingSFX;

        public AudioClip hitSFX;

        public AudioSource CreatureMiscSFX;

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
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            if (creatureVoice != null)
            {
                creatureVoice.Play();
            }

            LogIfDebugBuild("Obake Spawned");
            SpawnCursedGramophoneLocally();
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

            timeSinceLastAttack += Time.deltaTime;

            if (!base.IsOwner)
            {
                return;
            }
            timeSinceSeen += Time.deltaTime;
            manifestAnimCooldown += Time.deltaTime;

            if (isEnemyDead)
            {
                agent.speed = 0f;
            }
            else if (!isEnemyDead)
            {
                if (timeSinceSeen > 3f)
                {
                    agent.speed = 6f;
                }
                else
                {
                    agent.speed = 5f;
                }
                if (timeSinceSeen <= 1f && manifestAnimCooldown >= 20f)
                {
                    manifestAnimCooldown = 0f;
                    DoManifestOnOwnerClient();
                }
                if (hasHeardGramophone==true)
                {
                    agent.speed= gramoDash;
                }
                if (timeSinceLastAttack < 2f)
                {
                    agent.speed = 2f;
                }
            }
        }

        private void Manifesting()
        {
            StartCoroutine(ManifestFX());
            creatureVoice.PlayOneShot(emergeSFX);
            creatureVoice.PlayOneShot(laughSFX);
            creatureAnimator.SetTrigger("isManifesting");
        }

        IEnumerator ManifestFX()
        {
            yield return new WaitForSeconds(1f);
            if (VoidFace != null) VoidFace.SetActive(true);
            yield return new WaitForSeconds(1f);
            if (VoidFace != null) VoidFace.SetActive(false);
        }

        private void DoManifestOnOwnerClient()
        {
            Manifesting();

            if (base.IsServer)
            {
                ManifestClientRpc();
            }
            else
            {
                DoManifestServerRpc();
            }
        }

        [ServerRpc]
        public void DoManifestServerRpc()
        {
            ManifestClientRpc();
        }

        [ClientRpc]
        public void ManifestClientRpc()
        {
            if (!base.IsOwner)
            {
                Manifesting();
            }
        }

        //private void HandleCustomFootsteps()
        //{
        //    float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        //    if (distanceMoved > 0.02f)
        //    {
        //        footstepTimer += Time.deltaTime;
        //        if (footstepTimer > 0.6f)
        //        {
        //            footstepTimer = 0f;
        //            if (customFootstepSounds != null && customFootstepSounds.Length > 0 && movementAudio != null)
        //            {
        //                int randomIndex = UnityEngine.Random.Range(0, customFootstepSounds.Length);
        //                movementAudio.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
        //                movementAudio.PlayOneShot(customFootstepSounds[randomIndex], 1f);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        footstepTimer = 0f;
        //    }
        //    lastPosition = transform.position;
        //}

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

        private void SetVisibility()
        {
            float num = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, base.transform.position + Vector3.up * 0.7f);
            float alphaCutoff = (num - minDistance) / (maxDistance - minDistance);

            for (int i = 0; i < thisMaterial.Length; i++)
            {
                if (thisMaterial[i] != null)
                {
                    thisMaterial[i].SetFloat("_AlphaCutoff", alphaCutoff);
                }
            }
            PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
            if (!localPlayerController.isPlayerDead && localPlayerController != null && num < 15f && num > maxDistance + 2f)
            {
                localPlayerController.IncreaseFearLevelOverTime(0.37f, 0.25f);
            }
        }


        public void HearGramophone(Vector3 gramophonePosition)
        {
            if (hasHeardGramophone) return;
            hasHeardGramophone = true;
            gramoDash = agent.speed * 1.5f;
            LogIfDebugBuild("The Obake hears the Gramophone!");

            SetDestinationToPosition(gramophonePosition);
        }

        public void DefeatObake()
        {
            if (isEnemyDead) return;
            LogIfDebugBuild("Gramophone fully wound. Obake defeated!");

            SpawnRapierLocally();

            KillEnemyOnOwnerClient(true);
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            if (!(timeSinceLastAttack < 0.65f))
            {
                PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
                if (playerControllerB != null)
                {
                    timeSinceLastAttack = 0f;
                    playerControllerB.DamagePlayer(40, hasDamageSFX: true, callRPC: true, CauseOfDeath.Stabbing);
                    HitPlayerServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1f);
                }
            }

        }

        [ServerRpc(RequireOwnership = false)]
        public void HitPlayerServerRpc(int playerId)
        {
            HitPlayerClientRpc(playerId);
        }

        [ClientRpc]
        public void HitPlayerClientRpc(int playerId)
        {
            timeSinceLastAttack = 0f;
            creatureAnimator.SetTrigger("isAttacking");
            creatureVoice.PlayOneShot(swingSFX);
        }
    }
}