using System;
using GameNetcodeStuff;
using HarmonyLib;

namespace Obake
{
    [HarmonyPatch]
    public static class ObakeEventManager
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
