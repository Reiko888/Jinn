using BepInEx;
using BepInEx.Logging;
using Dawn;
using Dusk;
using HarmonyLib;
using System.Reflection;

namespace Obake
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(DawnLib.PLUGIN_GUID)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger = null!;

        private void Awake()
        {
            Harmony harmony = new Harmony("Reiko888.Obake");
            Logger = base.Logger;
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("Obake Plugin has loaded!");
        }
    }
}