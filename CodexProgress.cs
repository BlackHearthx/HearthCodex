using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CreatureCodex
{
    /// <summary>
    /// Per-character codex progress, stored in one entry of Player.m_customData.
    ///
    /// Format (plain text, one line per species, tab separated):
    ///   CreatureCodex|1
    ///   $enemy_skeleton	Discovered	7
    /// Knowledge is written by enum name. Only species with progress are written,
    /// never the catalog. Tokens the catalog does not know are kept and written back.
    /// </summary>
    internal static class CodexProgress
    {
        // Keep the original development key permanently. Changing it with the public
        // HearthCodex name would make existing characters appear to lose progress.
        internal const string CustomDataKey = "blackhearthx.creaturecodex";
        private const string BackupKey = CustomDataKey + ".unreadable";
        private const string Header = "CreatureCodex";
        private const int FormatVersion = 1;

        private struct SavedEntry
        {
            public CodexKnowledge Knowledge;
            public int TimesSlain;
        }

        private static readonly Dictionary<string, SavedEntry> Entries =
            new Dictionary<string, SavedEntry>(StringComparer.Ordinal);

        /// <summary>Raised after progress is restored or a kill is recorded. The book refreshes if open.</summary>
        internal static event Action Changed;

        private static Player _boundPlayer;
        private static string _futureFormatRaw;
        private static bool _appliedToCatalog;

        internal static void OnProfileLoaded(Player player)
        {
            _boundPlayer = player;
            _appliedToCatalog = false;
            _futureFormatRaw = null;
            Entries.Clear();

            if (player.m_customData.TryGetValue(CustomDataKey, out var raw) && !string.IsNullOrEmpty(raw))
            {
                try
                {
                    Parse(player, raw);
                }
                catch (Exception ex)
                {
                    Entries.Clear();
                    Jotunn.Logger.LogWarning("Creature Codex: could not read saved progress, starting empty for this session. " + ex.Message);
                }
            }

            Jotunn.Logger.LogInfo("Creature Codex: Loaded codex progress: " + Entries.Count + " species entries");

            if (CreatureCatalog.IsBuilt)
            {
                ApplyToCatalog();
            }
        }

        internal static void OnCatalogBuilt()
        {
            if (_boundPlayer != null && !_appliedToCatalog)
            {
                ApplyToCatalog();
            }
        }

        internal static void OnProfileSaving(Player player)
        {
            if (player != _boundPlayer)
            {
                return;
            }

            if (_futureFormatRaw != null)
            {
                player.m_customData[CustomDataKey] = _futureFormatRaw;
                Jotunn.Logger.LogWarning("Creature Codex: saved progress is from a newer version of the mod, left untouched.");
                return;
            }

            if (_appliedToCatalog)
            {
                CaptureFromCatalog();
            }

            player.m_customData[CustomDataKey] = Serialize(out var written);
            Jotunn.Logger.LogInfo("Creature Codex: Saved codex progress: " + written + " species entries");

            if (PluginConfig.VerboseSaveLogging.Value)
            {
                LogEntries("saved");
            }
        }

        private static void Parse(Player player, string raw)
        {
            var lines = raw.Split('\n');
            var header = lines[0].Trim();
            var sep = header.IndexOf('|');

            if (sep <= 0
                || !string.Equals(header.Substring(0, sep), Header, StringComparison.Ordinal)
                || !int.TryParse(header.Substring(sep + 1), out var version)
                || version < 1)
            {
                player.m_customData[BackupKey] = raw;
                Jotunn.Logger.LogWarning("Creature Codex: saved progress has an unknown header, starting empty. The old text was kept under '" + BackupKey + "'.");
                return;
            }

            if (version > FormatVersion)
            {
                _futureFormatRaw = raw;
                Jotunn.Logger.LogWarning("Creature Codex: saved progress uses format " + version + ", this version reads " + FormatVersion + ". The codex starts empty and the save is kept as is.");
                return;
            }

            var skipped = 0;
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var parts = line.Split('\t');
                if (parts.Length < 3
                    || parts[0].Length < 2
                    || parts[0][0] != '$'
                    || !TryParseKnowledge(parts[1], out var knowledge)
                    || !int.TryParse(parts[2], out var slain)
                    || slain < 0)
                {
                    skipped++;
                    if (PluginConfig.VerboseSaveLogging.Value)
                    {
                        Jotunn.Logger.LogWarning("Creature Codex: skipped unreadable progress line " + i + ": '" + line + "'");
                    }

                    continue;
                }

                Merge(parts[0], knowledge, slain);
            }

            if (skipped > 0)
            {
                Jotunn.Logger.LogWarning("Creature Codex: skipped " + skipped + " unreadable progress line(s), kept the rest.");
            }
        }

        private static bool TryParseKnowledge(string text, out CodexKnowledge knowledge)
        {
            knowledge = CodexKnowledge.Unknown;
            if (string.IsNullOrEmpty(text) || char.IsDigit(text[0]) || text[0] == '-')
            {
                return false;
            }

            try
            {
                knowledge = (CodexKnowledge)Enum.Parse(typeof(CodexKnowledge), text, false);
                return Enum.IsDefined(typeof(CodexKnowledge), knowledge);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void Merge(string token, CodexKnowledge knowledge, int slain)
        {
            if (Entries.TryGetValue(token, out var existing))
            {
                existing.Knowledge = knowledge > existing.Knowledge ? knowledge : existing.Knowledge;
                existing.TimesSlain = SafeAdd(existing.TimesSlain, slain);
                Entries[token] = existing;
                return;
            }

            Entries[token] = new SavedEntry { Knowledge = knowledge, TimesSlain = slain };
        }

        private static void ApplyToCatalog()
        {
            foreach (var species in CreatureCatalog.Species)
            {
                species.ResetProgress();
            }

            var matched = 0;
            foreach (var pair in Entries)
            {
                if (!CreatureCatalog.TryGetSpecies(pair.Key, out var species))
                {
                    continue;
                }

                matched++;
                if (pair.Value.Knowledge > species.Knowledge)
                {
                    species.Knowledge = pair.Value.Knowledge;
                }

                species.TimesSlain = SafeAdd(species.TimesSlain, pair.Value.TimesSlain);
            }

            _appliedToCatalog = true;

            var orphans = Entries.Count - matched;
            if (orphans > 0)
            {
                Jotunn.Logger.LogInfo("Creature Codex: " + orphans + " saved entr" + (orphans == 1 ? "y is" : "ies are") + " not in the current catalog; kept in the save.");
            }

            if (PluginConfig.VerboseSaveLogging.Value)
            {
                LogEntries("loaded");
            }

            RaiseChanged();
        }

        private static void RaiseChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning("Creature Codex: the book could not refresh. " + ex.Message);
            }
        }

        /// <summary>
        /// Called on the client of a player the game credited with a kill (Game.RPC_RegisterKill).
        /// </summary>
        internal static void RecordKill(string enemyName, long sender, int bossNumber,
            int modifiers, int attackers, bool cheatsUsed)
        {
            var verbose = PluginConfig.VerboseKillLogging.Value;

            if (_boundPlayer == null || !_appliedToCatalog)
            {
                if (verbose)
                {
                    Jotunn.Logger.LogInfo("Creature Codex: Kill ignored: codex progress is not loaded yet (token " + enemyName + ")");
                }

                return;
            }

            if (!CreatureCatalog.TryGetSpecies(enemyName, out var species))
            {
                if (verbose)
                {
                    Jotunn.Logger.LogInfo("Creature Codex: Kill ignored: no species for token " + enemyName);
                }

                return;
            }

            var oldKnowledge = species.Knowledge;
            var oldSlain = species.TimesSlain < 0 ? 0 : species.TimesSlain;
            var newSlain = oldSlain;
            if (oldSlain == int.MaxValue)
            {
                Jotunn.Logger.LogWarning("Creature Codex: " + species.DisplayToken + " slain count is at its limit and stays there.");
            }
            else
            {
                newSlain = oldSlain + 1;
            }

            species.TimesSlain = newSlain;
            species.Knowledge = CodexKnowledge.Studied;
            WriteSpeciesEntry(species);

            if (oldKnowledge != CodexKnowledge.Studied)
            {
                Jotunn.Logger.LogInfo("Creature Codex: Studied: " + species.DisplayName + " (TimesSlain = " + newSlain + ")");
            }
            else
            {
                Jotunn.Logger.LogInfo("Creature Codex: " + species.DisplayName + " slain again (TimesSlain = " + newSlain + ")");
            }

            if (verbose)
            {
                var playerName = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "<no local player>";
                Jotunn.Logger.LogInfo(
                    "Creature Codex: kill detail: m_name = " + enemyName
                    + ", species = " + species.DisplayToken
                    + ", credited = " + playerName
                    + ", sender = " + sender
                    + ", bossNumber = " + bossNumber
                    + ", modifiers = " + modifiers
                    + ", attackers = " + attackers
                    + ", cheats = " + cheatsUsed
                    + ", Knowledge " + oldKnowledge + " -> " + species.Knowledge
                    + ", TimesSlain " + oldSlain + " -> " + newSlain);
            }

            RaiseChanged();
        }

        /// <summary>Discover only the boss named by a Vegvisir, for the interacting local character.</summary>
        internal static void DiscoverBoss(Player player, string token)
        {
            Discover(player, token, true);
        }

        internal static void DiscoverObservedCreature(Player player, string token)
        {
            Discover(player, token, false);
        }

        private static void Discover(Player player, string token, bool boss)
        {
            if (player == null || player != Player.m_localPlayer || player != _boundPlayer
                || !_appliedToCatalog || !CreatureCatalog.TryGetSpecies(token, out var species)
                || species.IsBoss != boss || species.Knowledge != CodexKnowledge.Unknown)
            {
                return;
            }

            species.Knowledge = CodexKnowledge.Discovered;
            WriteSpeciesEntry(species);
            Jotunn.Logger.LogInfo("Creature Codex: Discovered through " + (boss ? "Vegvisir: " : "observation: ") + species.DisplayName);
            RaiseChanged();
        }

        private static void CaptureFromCatalog()
        {
            foreach (var species in CreatureCatalog.Species)
            {
                WriteSpeciesEntry(species);
            }
        }

        // A species can own several tokens (FrozenKing phases). Its progress goes back
        // under its first token only; tokens the catalog does not know stay as they were.
        // Kills write here too, so a respawn reload between kill and save keeps the kill.
        private static void WriteSpeciesEntry(CreatureSpecies species)
        {
            foreach (var token in species.Tokens)
            {
                Entries.Remove(token);
            }

            if (species.Knowledge != CodexKnowledge.Unknown || species.TimesSlain > 0)
            {
                Entries[species.DisplayToken] = new SavedEntry
                {
                    Knowledge = species.Knowledge,
                    TimesSlain = species.TimesSlain
                };
            }
        }

        private static string Serialize(out int written)
        {
            written = 0;
            var sb = new StringBuilder();
            sb.Append(Header).Append('|').Append(FormatVersion);

            foreach (var pair in Entries.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (pair.Key.IndexOfAny(new[] { '\t', '\n', '\r' }) >= 0)
                {
                    Jotunn.Logger.LogWarning("Creature Codex: token '" + pair.Key.Replace("\t", " ").Replace("\n", " ") + "' has a tab or line break and was not saved.");
                    continue;
                }

                sb.Append('\n').Append(pair.Key)
                    .Append('\t').Append(pair.Value.Knowledge)
                    .Append('\t').Append(pair.Value.TimesSlain);
                written++;
            }

            return sb.ToString();
        }

        private static void LogEntries(string verb)
        {
            var sb = new StringBuilder();
            sb.Append("Creature Codex: ").Append(verb).Append(" entries (").Append(Entries.Count).Append("):");
            foreach (var pair in Entries.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var inCatalog = CreatureCatalog.TryGetSpecies(pair.Key, out _) ? string.Empty : " (not in catalog)";
                sb.Append("\n  ").Append(pair.Key)
                    .Append(" = ").Append(pair.Value.Knowledge)
                    .Append(", slain ").Append(pair.Value.TimesSlain)
                    .Append(inCatalog);
            }

            Jotunn.Logger.LogInfo(sb.ToString());
        }

        private static int SafeAdd(int a, int b)
        {
            var sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }
    }
}
