//using BepInEx.Configuration;

//namespace Jinn;

//public class JinnConfig
//{
//    public ConfigEntry<bool> ConfigDropRapierOnDeath { get; private set; }
//    public ConfigEntry<bool> ConfigDropGramoOnDeath { get; private set; }
//    public ConfigEntry<int> ConfigJinnBaseSpeed { get; private set; }
//    public ConfigEntry<int> ConfigJinnAttackCooldownSlow { get; private set; }
//    public ConfigEntry<float> ConfigJinnMinVisibleDist { get; private set; }
//    public ConfigEntry<float> ConfigJinnMaxVisibleDist { get; private set; }
//    public ConfigEntry<bool> ConfigJinnCanTeleport { get; private set; }
//    public ConfigEntry<float> ConfigJinnTPWarnDelay { get; private set; }
//    public ConfigEntry<float> ConfigJinnMaxDistAfterDelay { get; private set; }
//    public ConfigEntry<float> ConfigJinnMinnDistAfterDelay { get; private set; }
//    public ConfigEntry<bool> ConfigJinnCanBeBurned { get; private set; }
//    public ConfigEntry<bool> ConfigJinnBurnLights { get; private set; }
//    public ConfigEntry<int> ConfigJinnFlashlightConsumption { get; private set; }
//    public ConfigEntry<int> ConfigJinnBurnSlowdown { get; private set; }
//    public ConfigEntry<int> ConfigJinnAttackDamage { get; private set; }
//    public ConfigEntry<int> ConfigJinnGramoHintDist { get; private set; }
//    public ConfigEntry<int> ConfigJinnSearchMusicVolume { get; private set; }
//    public ConfigEntry<int> ConfigJinnSearchMusicDistance { get; private set; }
//    public ConfigEntry<int> ConfigGramophoneVolume { get; private set; }
//    public ConfigEntry<int> ConfigGramophoneDistance { get; private set; }
//    public ConfigEntry<int> ConfigRapierDamage { get; private set; }

//    public void JinnConf(ConfigFile configfile)
//    {
//        // --- DROPS ---
//        //ConfigDropRapierOnDeath = configfile.Bind("Jinn Drops",
//        //    "Drop Rapier",
//        //    true,
//        //    "Does the Jinn drop the Rapier weapon on death?");

//        //ConfigDropGramoOnDeath = configfile.Bind("Jinn Drops",
//        //    "Drop Gramophone",
//        //    true,
//        //    "Does the Gramophone turn into scrap on Jinn death?");

//        //// --- STATS ---
//        //ConfigJinnBaseSpeed = configfile.Bind("Jinn Stats",
//        //    "Base Chase Speed",
//        //    7,
//        //    "The standard movement speed of the Jinn when chasing a player.");

//        //ConfigJinnAttackCooldownSlow = configfile.Bind("Jinn Stats",
//        //    "Attack Cooldown Speed",
//        //    2,
//        //    "The slowed speed of the Jinn for 2 seconds immediately after landing a stab.");

//        //ConfigJinnAttackDamage = configfile.Bind("Jinn Stats",
//        //    "Attack Damage",
//        //    40,
//        //    "How much damage the Jinn deals per stab (100 is instant kill).");

//        //// --- VISIBILITY ---
//        //ConfigJinnMaxVisibleDist = configfile.Bind("Jinn Visibility",
//        //    "Max Visible Distance",
//        //    15f,
//        //    "The distance at which the Jinn begins to manifest out of the mist.");

//        //ConfigJinnMinVisibleDist = configfile.Bind("Jinn Visibility",
//        //    "Min Visible Distance",
//        //    5f,
//        //    "The distance at which the Jinn is 100% fully visible.");

//        //// --- TELEPORT ---
//        //ConfigJinnCanTeleport = configfile.Bind("Jinn Teleport",
//        //    "Can Teleport",
//        //    true,
//        //    "Is the Jinn allowed to use its teleport ability?");

//        //ConfigJinnTPWarnDelay = configfile.Bind("Jinn Teleport",
//        //    "Teleport Warning Delay",
//        //    6f,
//        //    "How many seconds the smoke swirl warns the player before the Jinn teleports to it.");

//        //ConfigJinnMaxDistAfterDelay = configfile.Bind("Jinn Teleport",
//        //    "Max Teleport Distance",
//        //    18f,
//        //    "The maximum distance the player can be from the smoke for the teleport to trigger.");

//        //ConfigJinnMinnDistAfterDelay = configfile.Bind("Jinn Teleport",
//        //    "Min Teleport Distance",
//        //    0f,
//        //    "The minimum distance the player must be from the smoke for it to trigger.");

//        //// --- FLASHLIGHT BURN ---
//        //ConfigJinnCanBeBurned = configfile.Bind("Jinn Flashlight",
//        //    "Can Be Burned",
//        //    true,
//        //    "Can the Jinn be slowed down and stunned by flashlights?");

//        //ConfigJinnBurnSlowdown = configfile.Bind("Jinn Flashlight",
//        //    "Burn Slowdown Speed",
//        //    1,
//        //    "The speed the Jinn is reduced to while actively being burned by a flashlight.");

//        //ConfigJinnFlashlightConsumption = configfile.Bind("Jinn Flashlight",
//        //    "Aura Flashlight Drain",
//        //    15,
//        //    "The percentage of flashlight battery drained per second when near the Jinn.");

//        //ConfigJinnBurnLights = configfile.Bind("Jinn Flashlight",
//        //    "Flicker Facility Lights",
//        //    true,
//        //    "Do the facility lights violently flicker when the Jinn is being burned?");

//        //// --- GRAMOPHONE & AUDIO ---
//        //ConfigJinnGramoHintDist = configfile.Bind("Jinn Gramophone",
//        //    "Gramophone Hint Distance",
//        //    15,
//        //    "The distance at which the Gramophone gives a hint to the players.");

//        //ConfigJinnSearchMusicVolume = configfile.Bind("Jinn Audio",
//        //    "Search Music Volume",
//        //    100,
//        //    "The volume level (0-100) of the Jinn's ambient search music.");

//        //ConfigJinnSearchMusicDistance = configfile.Bind("Jinn Audio",
//        //    "Search Music Distance",
//        //    20,
//        //    "How far away players can hear the Jinn's ambient search music.");

//        //ConfigGramophoneVolume = configfile.Bind("Jinn Gramophone",
//        //    "Winding Volume",
//        //    100,
//        //    "The volume level (0-100) of the Gramophone's winding sound.");

//        //ConfigGramophoneDistance = configfile.Bind("Jinn Gramophone",
//        //    "Winding Hearing Distance",
//        //    15,
//        //    "How far away players can hear the Gramophone being wound.");

//        //// --- RAPIER ---

//        //ConfigRapierDamage = configfile.Bind("Jinn Stats",
//        //    "Attack Damage (Hit force)",
//        //    1,
//        //    "How much damage the Rapier deals per swing");
//    }
//}