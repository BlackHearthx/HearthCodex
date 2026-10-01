using System;
using HarmonyLib;

namespace CreatureCodex
{
    [HarmonyPatch(typeof(Vegvisir), nameof(Vegvisir.Interact))]
    internal static class CodexVegvisirPatch
    {
        // Read the actual pin tokens instead of guessing a boss from the object's name.
        // A repeated interaction is valid even if its map marker was already discovered.
        [HarmonyPostfix]
        private static void InteractPostfix(Vegvisir __instance, Humanoid character, bool hold, bool __result)
        {
            if (hold || !__result || !(character is Player player) || player != Player.m_localPlayer)
            {
                return;
            }

            try
            {
                if (__instance.m_locations == null)
                {
                    return;
                }

                foreach (var location in __instance.m_locations)
                {
                    if (location != null)
                    {
                        CodexProgress.DiscoverBoss(player, location.m_pinName);
                    }
                }
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning("Creature Codex: could not record Vegvisir discovery. " + ex);
            }
        }
    }
}
