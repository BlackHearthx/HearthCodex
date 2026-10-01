using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CreatureCodex
{
    /// <summary>
    /// The list of creatures the codex knows about, built once from ZNetScene.
    ///
    /// Candidate: every name returned by ZNetScene.GetPrefabNames().
    /// Included: the prefab has a Character on its root, a stable prefab id,
    /// a faction that belongs to wild creatures, and an m_name that is a translated $ token
    /// that is not a boss aspect. Included prefabs are grouped into species by localized name;
    /// a species is one book entry.
    ///
    /// Read only. Nothing on any prefab is changed. Only strings are kept,
    /// so later phases look the prefab up again through ZNetScene.GetPrefab(id).
    /// </summary>
    internal static class CreatureCatalog
    {
        internal const string ReasonMissingPrefab = "prefab lookup returned null";
        internal const string ReasonLookupMismatch = "prefab lookup returned a different object";
        internal const string ReasonEmptyId = "empty prefab ID";
        internal const string ReasonUnstableId = "prefab ID changes under Utils.GetPrefabName";
        internal const string ReasonDuplicateId = "duplicate prefab ID";
        internal const string ReasonMissingCharacter = "missing Character";
        internal const string ReasonPlayers = "Players";
        internal const string ReasonPlayerSpawned = "PlayerSpawned";
        internal const string ReasonDummy = "dummy (TrainingDummy)";
        internal const string ReasonUnsupportedFaction = "unsupported faction";
        internal const string ReasonNoToken = "m_name has no $ token";
        internal const string ReasonAspect = "boss aspect";
        internal const string ReasonUntranslated = "token has no translation";

        private const string AspectTokenPrefix = "$enemy_aspect_";
        private const string AspectPrefabPrefix = "Aspect_";

        private static readonly HashSet<Character.Faction> WildFactions = new HashSet<Character.Faction>
        {
            Character.Faction.AnimalsVeg,
            Character.Faction.ForestMonsters,
            Character.Faction.Undead,
            Character.Faction.Demon,
            Character.Faction.MountainMonsters,
            Character.Faction.SeaMonsters,
            Character.Faction.PlainsMonsters,
            Character.Faction.Boss,
            Character.Faction.MistlandsMonsters,
            Character.Faction.Dverger,
            Character.Faction.DeepNorth
        };

        private static readonly Dictionary<string, CreaturePrefab> ById =
            new Dictionary<string, CreaturePrefab>(StringComparer.Ordinal);

        private static readonly List<CreaturePrefab> Ordered = new List<CreaturePrefab>();

        private static readonly Dictionary<string, CreatureSpecies> SpeciesByToken =
            new Dictionary<string, CreatureSpecies>(StringComparer.Ordinal);

        private static readonly List<CreatureSpecies> SpeciesOrdered = new List<CreatureSpecies>();

        internal static bool IsBuilt { get; private set; }

        internal static int Count => Ordered.Count;

        internal static IReadOnlyList<CreaturePrefab> All => Ordered;

        internal static int SpeciesCount => SpeciesOrdered.Count;

        internal static IReadOnlyList<CreatureSpecies> Species => SpeciesOrdered;

        internal static bool TryGetSpecies(string displayToken, out CreatureSpecies species)
        {
            if (string.IsNullOrEmpty(displayToken))
            {
                species = null;
                return false;
            }

            return SpeciesByToken.TryGetValue(displayToken, out species);
        }

        internal static bool TryGet(string prefabId, out CreaturePrefab data)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                data = null;
                return false;
            }

            return ById.TryGetValue(prefabId, out data);
        }

        /// <summary>
        /// Builds the catalog the first time ZNetScene is ready. Later calls do nothing.
        /// </summary>
        internal static void BuildOnce(ZNetScene scene, bool verbose)
        {
            if (IsBuilt)
            {
                return;
            }

            if (scene == null)
            {
                Jotunn.Logger.LogWarning("Creature Codex: ZNetScene is not available, catalog not built.");
                return;
            }

            Jotunn.Logger.LogInfo("Creature Codex: scanning creature catalog...");

            List<string> names;
            try
            {
                names = scene.GetPrefabNames();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError("Creature Codex: GetPrefabNames failed, catalog not built. " + ex);
                return;
            }

            ById.Clear();
            Ordered.Clear();
            SpeciesByToken.Clear();
            SpeciesOrdered.Clear();

            var reasonCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var detail = new List<string>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var faultCount = 0;

            foreach (var rawName in names)
            {
                string reason;
                string id = null;
                Character character = null;

                try
                {
                    reason = Evaluate(scene, rawName, seenIds, out id, out character);
                }
                catch (Exception ex)
                {
                    faultCount++;
                    reason = "error while reading prefab (" + ex.GetType().Name + ")";
                    if (verbose)
                    {
                        Jotunn.Logger.LogWarning("Creature Codex: error reading '" + rawName + "': " + ex.Message);
                    }
                }

                if (reason == null)
                {
                    seenIds.Add(id);
                    var data = new CreaturePrefab(id, character.m_name, character.m_faction, character.m_boss);
                    ById[id] = data;
                    Ordered.Add(data);
                    continue;
                }

                if (!string.IsNullOrEmpty(id))
                {
                    seenIds.Add(id);
                }

                reasonCounts.TryGetValue(reason, out var n);
                reasonCounts[reason] = n + 1;

                if (verbose)
                {
                    detail.Add(DescribeExcluded(rawName, character, reason));
                }
            }

            Ordered.Sort((a, b) => string.CompareOrdinal(a.PrefabId, b.PrefabId));
            BuildSpecies();
            IsBuilt = true;

            var excluded = names.Count - Ordered.Count;
            Jotunn.Logger.LogInfo("Creature Codex: candidates = " + names.Count);
            Jotunn.Logger.LogInfo("Creature Codex: included = " + Ordered.Count);
            Jotunn.Logger.LogInfo("Creature Codex: species (book entries) = " + SpeciesOrdered.Count);
            Jotunn.Logger.LogInfo("Creature Codex: excluded = " + excluded);
            Jotunn.Logger.LogInfo("Creature Codex: excluded by reason:" + FormatReasons(reasonCounts));

            if (faultCount > 0)
            {
                Jotunn.Logger.LogWarning("Creature Codex: " + faultCount + " prefab(s) threw while being read and were skipped.");
            }

            if (verbose)
            {
                LogVerbose(detail);
            }
        }

        private static void BuildSpecies()
        {
            var byName = new Dictionary<string, CreatureSpecies>(StringComparer.Ordinal);
            foreach (var data in Ordered)
            {
                var nameKey = Localize(data.DisplayToken).Trim();
                if (nameKey.Length == 0)
                {
                    nameKey = data.DisplayToken;
                }

                if (!byName.TryGetValue(nameKey, out var species))
                {
                    species = new CreatureSpecies(nameKey);
                    byName[nameKey] = species;
                    SpeciesOrdered.Add(species);
                }

                species.Add(data);
                SpeciesByToken[data.DisplayToken] = species;
            }

            foreach (var species in SpeciesOrdered)
            {
                species.Sort();
            }

            SpeciesOrdered.Sort((a, b) => string.CompareOrdinal(a.DisplayToken, b.DisplayToken));
        }

        /// <summary>
        /// Null means included. Otherwise returns the exclusion reason.
        /// Identity is the prefab id, never Character.m_name.
        /// </summary>
        private static string Evaluate(
            ZNetScene scene,
            string rawName,
            HashSet<string> seenIds,
            out string id,
            out Character character)
        {
            id = null;
            character = null;

            if (string.IsNullOrEmpty(rawName))
            {
                return ReasonEmptyId;
            }

            var prefab = scene.GetPrefab(rawName);
            if (prefab == null)
            {
                return ReasonMissingPrefab;
            }

            if (!string.Equals(prefab.name, rawName, StringComparison.Ordinal))
            {
                return ReasonLookupMismatch;
            }

            id = rawName;

            // ZNetView.GetPrefabName() goes through Utils.GetPrefabName(gameObject).
            // A later death hook will see that form, so the stored id must already match it.
            if (!string.Equals(Utils.GetPrefabName(prefab), id, StringComparison.Ordinal))
            {
                return ReasonUnstableId;
            }

            if (seenIds.Contains(id))
            {
                return ReasonDuplicateId;
            }

            character = prefab.GetComponent<Character>();
            if (character == null)
            {
                return ReasonMissingCharacter;
            }

            switch (character.m_faction)
            {
                case Character.Faction.Players:
                    return ReasonPlayers;
                case Character.Faction.PlayerSpawned:
                    return ReasonPlayerSpawned;
                case Character.Faction.TrainingDummy:
                    return ReasonDummy;
            }

            if (!WildFactions.Contains(character.m_faction))
            {
                return ReasonUnsupportedFaction;
            }

            // Real creatures name themselves through a localization token.
            // Test and scenery prefabs (TrainingDummy, Tendril, Root) use plain text.
            if (string.IsNullOrEmpty(character.m_name) || character.m_name[0] != '$')
            {
                return ReasonNoToken;
            }

            // Boss aspects are summoned during boss fights, not creatures of the world.
            if (character.m_name.StartsWith(AspectTokenPrefix, StringComparison.Ordinal)
                || id.StartsWith(AspectPrefabPrefix, StringComparison.Ordinal))
            {
                return ReasonAspect;
            }

            // Localization returns "[key]" only when no loaded table (English is the fallback) has the token.
            if (Localization.instance != null
                && Localization.instance.Localize(character.m_name) == "[" + character.m_name.Substring(1) + "]")
            {
                return ReasonUntranslated;
            }

            return null;
        }

        private static string DescribeExcluded(string rawName, Character character, string reason)
        {
            var name = string.IsNullOrEmpty(rawName) ? "<empty>" : rawName;
            if (character == null)
            {
                return name + " -> " + reason;
            }

            return name + " [" + character.m_faction + ", " + Safe(character.m_name) + "] -> " + reason;
        }

        private static string FormatReasons(Dictionary<string, int> counts)
        {
            if (counts.Count == 0)
            {
                return " none";
            }

            var sb = new StringBuilder();
            foreach (var pair in counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                sb.Append("\n  ").Append(pair.Key).Append(" = ").Append(pair.Value);
            }

            return sb.ToString();
        }

        private static void LogVerbose(List<string> excludedLines)
        {
            var sb = new StringBuilder();
            sb.Append("Creature Codex: included creatures (").Append(Ordered.Count).Append("):");
            foreach (var c in Ordered)
            {
                sb.Append("\n  ").Append(c.PrefabId)
                    .Append(" [").Append(c.Faction)
                    .Append(c.IsBoss ? ", boss" : string.Empty)
                    .Append("] ").Append(Safe(c.DisplayToken));
            }

            Jotunn.Logger.LogInfo(sb.ToString());

            sb.Length = 0;
            sb.Append("Creature Codex: species (").Append(SpeciesOrdered.Count).Append("):");
            foreach (var s in SpeciesOrdered)
            {
                sb.Append("\n  ").Append(s.DisplayName)
                    .Append(s.IsBoss ? " [boss]" : string.Empty)
                    .Append(" {").Append(string.Join(", ", s.Tokens.ToArray())).Append("}")
                    .Append(" <- ")
                    .Append(string.Join(", ", s.Members.Select(m => m.PrefabId).ToArray()));
            }

            Jotunn.Logger.LogInfo(sb.ToString());

            // Prefabs with no Character are the bulk of the list (rocks, pieces, items).
            // They are written last so the lines that matter stay readable.
            var withCharacter = excludedLines.Where(l => !l.EndsWith(" -> " + ReasonMissingCharacter, StringComparison.Ordinal)).ToList();
            var withoutCharacter = excludedLines.Where(l => l.EndsWith(" -> " + ReasonMissingCharacter, StringComparison.Ordinal)).ToList();

            sb.Length = 0;
            sb.Append("Creature Codex: excluded with a Character or a bad id (").Append(withCharacter.Count).Append("):");
            foreach (var line in withCharacter)
            {
                sb.Append("\n  ").Append(line);
            }

            Jotunn.Logger.LogInfo(sb.ToString());

            sb.Length = 0;
            sb.Append("Creature Codex: excluded, missing Character (").Append(withoutCharacter.Count).Append("):");
            foreach (var line in withoutCharacter)
            {
                sb.Append("\n  ").Append(line);
            }

            Jotunn.Logger.LogInfo(sb.ToString());
        }

        private static string Safe(string s)
        {
            return string.IsNullOrEmpty(s) ? "<no m_name>" : s;
        }

        private static string Localize(string token)
        {
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }
}
