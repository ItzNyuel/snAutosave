﻿using System;
using HarmonyLib;

namespace SubnauticaAutosave
{
    public static class HarmonyPatches
    {
        private static bool Patch_ManualSaveGame_Prefix()
        {
            AutosaveController controller = Player.main?.GetComponent<AutosaveController>();

            if (controller != null)
            {
                if (ModPlugin.ConfigComprehensiveSaves.Value)
                {
                    controller.DoSaveLoadManagerDateHack();
                }

                controller.SetMainSlotIfAutosave();
            }

            return true;
        }

        private static void Patch_UpdateLoadButtonState_Postfix(MainMenuLoadButton lb)
        {
            if (ModPlugin.ConfigShowSaveNames.Value && SaveLoadManager.main.GetGameInfo(lb.saveGame) != null)
            {
                string slotNamePrefix = string.Empty;

                if (lb.saveGame.Contains("auto"))
                {
                    slotNamePrefix = "[Auto] ";
                }

                lb.saveGameLengthText.text += "\n\n" + slotNamePrefix + lb.saveGame;
            }
        }

        private static void Patch_Player_Awake_Postfix(Player __instance)
        {
            __instance.gameObject.AddComponent<AutosaveController>();
        }

        private static void Patch_ReportStageDurations_Postfix()
        {
#if DEBUG
            ModPlugin.LogMessage("Main scene loading, scheduling first autosave.");
#endif

            Player.main?.GetComponent<AutosaveController>()?.ScheduleAutosave(ModPlugin.ConfigMinutesBetweenAutosaves.Value);
        }

        // Untested //
        private static void Patch_Bed_OnHandClick_Postfix()
        {
            if (ModPlugin.ConfigAutosaveOnSleep.Value)
            {
                Player player = Player.main;

                if (player != null)
                {
                    /* [Bed.cs] private float kSleepInterval = 600f, added to timeLastSleep when sleep screen ends
                     * If player did indeed sleep recently, we can trigger a save after the bed click
                     * Could instead transpile into OnHandClick if(isValidHandTarget), but no reason to complicate things... */
                    if (player.timeLastSleep + 200f > DayNightCycle.main.timePassedAsFloat)
                    {
#if DEBUG
                        ModPlugin.LogMessage("Player clicked on bed. Executing save on sleep.");
#endif

                        player.GetComponent<AutosaveController>()?.TryExecuteAutosave(); // Might want a scheduled save instead
                    }
                }
            }
        }

        private static void Patch_Subroot_PlayerEnteredOrExited_Postfix()
        {
#if DEBUG
            ModPlugin.LogMessage("Player entered or exited sub. Delaying autosave.");
#endif

            Player.main?.GetComponent<AutosaveController>()?.DelayAutosave();
        }

        private static void Patch_SetCurrentLanguage_Postfix()
        {
            Translation.ReloadLanguage();
        }

        internal static void InitializeHarmony()
        {
            Harmony harmony = new Harmony("Dingo.Harmony.SubnauticaAutosave");

            /* When saving manually, set to main slot if loaded from autosave */
            // Patch: IngameMenu.SaveGame
            harmony.Patch(original: AccessTools.Method(typeof(IngameMenu), nameof(IngameMenu.SaveGame)),
                          prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Patch_ManualSaveGame_Prefix)));

            /* Show save names in menu panel */
            // Patch: MainMenuLoadPanel.UpdateLoadButtonState(MainMenuLoadButton lb)
            harmony.Patch(original: AccessTools.Method(typeof(MainMenuLoadPanel), "UpdateLoadButtonState", new Type[] { typeof(MainMenuLoadButton) }),
                          postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Patch_UpdateLoadButtonState_Postfix)));

            /* Autosave Controller initialization */
            // Patch: Player.Awake
            harmony.Patch(original: AccessTools.Method(typeof(Player), nameof(Player.Awake)),
                          postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Patch_Player_Awake_Postfix)));
            // Patch: WaitScreen.ReportStageDurations, called last when loading saved games
            harmony.Patch(original: AccessTools.Method(typeof(WaitScreen), nameof(WaitScreen.ReportStageDurations)),
                          postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Patch_ReportStageDurations_Postfix)));

            /* Save on player sleep */
            // Patch: Bed.OnHandClick
            harmony.Patch(original: AccessTools.Method(typeof(Bed), nameof(Bed.OnHandClick)),
                          postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Patch_Bed_OnHandClick_Postfix)));

            /* Delay autosave if player has entered or exited a base or vehicle */
            HarmonyMethod delayAutosavePatch = new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Patch_Subroot_PlayerEnteredOrExited_Postfix));
            // Patch: SubRoot.OnPlayerEntered
            harmony.Patch(original: AccessTools.Method(typeof(SubRoot), nameof(SubRoot.OnPlayerEntered)),
                          postfix: delayAutosavePatch);
            // Patch: SubRoot.OnPlayerExited
            harmony.Patch(original: AccessTools.Method(typeof(SubRoot), nameof(SubRoot.OnPlayerExited)),
                          postfix: delayAutosavePatch);

            /* Reset language cache upon language change */
            // Patch: Language.SetCurrentLanguage
            harmony.Patch(original: AccessTools.Method(typeof(Language), nameof(Language.SetCurrentLanguage)),
                          postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.Patch_SetCurrentLanguage_Postfix)));
        }
    }
}
