using GameNetcodeStuff;
using HarmonyLib;
using System;
using UnityEngine.Video;

namespace Jinn
{
    [HarmonyPatch(typeof(Terminal))]
    internal class TerminalVideoInjectorPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void InjectVideo(Terminal __instance)
        {
            if (JinnContentHandler.Instance.jinnAssets.Jinn == null) return;
            JinnTerminalAssets container = JinnContentHandler.Instance.jinnAssets.Jinn.GetComponent<JinnTerminalAssets>();
            if (container == null || container.bestiaryVideo == null) return;
            foreach (TerminalNode node in __instance.enemyFiles)
            {
                if (node != null && node.name == "JinnBestiaryNode")
                {
                    node.displayVideo = container.bestiaryVideo;
                    return;
                }
            }

            Plugin.Logger.LogWarning("Could not find a TerminalNode named 'JinnBestiaryNode' in the Bestiary.");
        }
    }


    [HarmonyPatch]
    public static class JinnEventManager
    {
        public static event Action OnShipLeft;
        public static event Action<PlayerControllerB> OnPlayerDied;
        public static event Action<PlayerControllerB> OnPlayerDisconnect;

        [HarmonyPatch(typeof(StartOfRound), "ShipLeave")]
        [HarmonyPostfix]
        private static void TriggerShipLeave()
        {
            OnShipLeft?.Invoke();
        }
    }
}
