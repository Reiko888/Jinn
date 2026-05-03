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
        enum State
        {
            InvisibleSearch,
            Chase,
            Manifest
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
            ChangeBehaviorState((int)State.InvisibleSearch);
            StartSearch(transform.position);
            foreach (SkinnedMeshRenderer smr in skinnedMeshRenderers)
            {
                allObakeMaterials.AddRange(smr.materials);
            }
            foreach (MeshRenderer mr in meshRenderers)
            {
                allObakeMaterials.AddRange(mr.materials);
            }
        }

        public void ChangeBehaviorState(int stateIndex)
        {
            currentBehaviourStateIndex = stateIndex;
            if (IsServer) SyncStateClientRpc(stateIndex);
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

                case (int)State.Chase:
                    if (attackCooldown > 0f)
                    {
                        agent.speed = 4.0f;
                    }
                    else if (canAttack == true && hasManifested == true)
                    {
                        agent.speed = 6f;
                    }

                    playerInSight = TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: true);

                    if (playerInSight && targetPlayer != null)
                    {
                        lostSightTimer = 10f;
                        SetMovingTowardsTargetPlayer(targetPlayer);
                    }
                    else if (isManifesting == false)
                    {
                        lostSightTimer -= AIIntervalTime;

                        if (lostSightTimer <= 0f)
                        {
                            LogIfDebugBuild("10 seconds passed. Demanifesting.");
                            movingTowardsTargetPlayer = false;
                            if (creatureVoice != null)
                            {
                                creatureVoice.Play();
                            }
                            isManifesting = false;
                            canAttack = false;
                            hasManifested = false;
                            ChangeBehaviorState((int)State.InvisibleSearch);
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
            //animation flashes every 2 seconds when manifesting. animation is triggered in search and has exit time for each animation state, so this just toggles the mesh on and off to create a flashing effect while manifesting
            DoAnimationClientRpc("isManifesting");
            EnableEnemyMesh(true, false, true);
            yield return new WaitForSeconds(1.5f);
            EnableEnemyMesh(false, false, true);
            yield return new WaitForSeconds(1.0f);
            EnableEnemyMesh(true, false, true);
            DoAnimationClientRpc("manifest2");
            yield return new WaitForSeconds(1.5f);
            EnableEnemyMesh(false, false, true);
            yield return new WaitForSeconds(1.0f);
            EnableEnemyMesh(true, false, true);
            DoAnimationClientRpc("manifest3");
            yield return new WaitForSeconds(1.0f);
            EnableEnemyMesh(false, false, true);
            yield return new WaitForSeconds(1.0f);
            EnableEnemyMesh(true, false, true);
            canAttack = true;
            LogIfDebugBuild("I can now attack");
            DoAnimationClientRpc("hasManifested");
            hasManifested = true;
            isManifesting = false;
        }
        private void TriggerAttack(PlayerControllerB victim)
        {
            attackCooldown = 2.0f;
            if (IsServer)
            {
                DoAnimationClientRpc("isAttacking");
            }

            if ((victim == GameNetworkManager.Instance.localPlayerController) && canAttack == true)
            {
                LogIfDebugBuild("Obake hit the local player! Sending damage to Server...");
                victim.DamagePlayer(25, hasDamageSFX: true, callRPC: true, CauseOfDeath.Stabbing);
                canAttack = false;
            }
            else if (IsServer && canAttack==true)
            {
                victim.DamagePlayer(25, hasDamageSFX: true, callRPC: true, CauseOfDeath.Stabbing);
                canAttack = false;
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
    }
}