using System;
using HarmonyLib;

namespace CreatureCodex
{
    /// <summary>
    /// Game.SpawnPlayer calls SetLocalPlayer() and then PlayerProfile.LoadPlayerData(player),
    /// which runs Player.Load only when the profile has data (a new character gets default items instead).
    /// Game.SavePlayerProfile and Game._RequestRespawn call PlayerProfile.SavePlayerData(Player.m_localPlayer),
    /// which serializes Player.m_customData through Player.Save.
    /// The main menu uses both methods on a preview Player that is never the local player.
    /// </summary>
    [HarmonyPatch]
    internal static class CodexSavePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.LoadPlayerData))]
        private static void LoadPlayerDataPostfix(Player player)
        {
            if (player == null || player != Player.m_localPlayer)
            {
                return;
            }

            try
            {
                CodexProgress.OnProfileLoaded(player);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning("Creature Codex: progress load failed, the character loads normally. " + ex);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.SavePlayerData))]
        private static void SavePlayerDataPrefix(Player player)
        {
            if (player == null || player != Player.m_localPlayer)
            {
                return;
            }

            try
            {
                CodexProgress.OnProfileSaving(player);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning("Creature Codex: progress save failed, the rest of the character still saves. " + ex);
            }
        }
    }
}
