using System;
using HarmonyLib;

namespace CreatureCodex
{
    /// <summary>
    /// Character.OnDeath runs its kill credit only on the ZDO owner. For every player marked
    /// in the ZDO as an attacker (Character.RPC_Damage sets s_attackers + player name) it calls
    /// Game.RPC_RegisterKill directly for the local player, or Game.RegisterKill, which routes
    /// "RPC_RegisterKill" to that player's peer. The current RPC also supplies kill modifiers,
    /// participant count and the cheat flag. This postfix runs once per credited player, on that
    /// player's own client, with Character.m_name. Vanilla adds its kill stat here too.
    /// OnDeath ends with ZNetScene.Destroy, so one death does not reach this twice.
    /// </summary>
    [HarmonyPatch]
    internal static class CodexKillPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game), nameof(Game.RPC_RegisterKill))]
        private static void RegisterKillPostfix(long sender, string enemyName, int bossNumber,
            int modifiers, int attackers, bool cheatsUsed)
        {
            try
            {
                CodexProgress.RecordKill(enemyName, sender, bossNumber, modifiers, attackers, cheatsUsed);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning("Creature Codex: could not record kill of " + enemyName + ". " + ex);
            }
        }
    }
}
