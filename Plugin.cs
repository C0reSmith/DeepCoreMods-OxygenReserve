using BepInEx;
using BepInEx.Logging;

namespace DeepCoreMods.OxygenReserve
{
    [BepInPlugin(
        PluginGuid,
        PluginName,
        PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "DeepCoreMods.OxygenReserve";

        public const string PluginName =
            "DeepCore Mods - Oxygen Reserve";

        public const string PluginVersion =
            "1.0.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            Log.LogInfo(
                $"{PluginName} v{PluginVersion} loaded.");

            Log.LogInfo(
                "Engineering Better Gameplay.");

            // Register the Stationeers controls entry and learning key.
            StationeersKeybind.Register();

            // Start the background Oxygen Reserve monitoring worker.
            OxygenReserveTimer.Start();
        }
      
    }
}