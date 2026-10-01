using System;
using System.Collections.Generic;

namespace CreatureCodex
{
    internal sealed class PrimaryCandidate
    {
        internal string PrefabId { get; }
        internal string DisplayToken { get; }
        internal bool IsBoss { get; }
        internal int SpawnEntries { get; }

        internal PrimaryCandidate(string prefabId, string displayToken, bool isBoss, int spawnEntries)
        {
            PrefabId = prefabId ?? string.Empty;
            DisplayToken = displayToken ?? string.Empty;
            IsBoss = isBoss;
            SpawnEntries = Math.Max(0, spawnEntries);
        }
    }

    /// <summary>
    /// Chooses the representative prefab from evidence authored by the game. No creature-specific
    /// names are embedded here: identity token match and actual spawn registration outrank ID shape.
    /// </summary>
    internal static class CreaturePrimarySelector
    {
        internal static string SelectId(IReadOnlyList<PrimaryCandidate> candidates, bool speciesIsBoss)
        {
            if (candidates == null || candidates.Count == 0) return null;
            var best = candidates[0];
            for (var i = 1; i < candidates.Count; i++)
            {
                if (Compare(candidates[i], best, speciesIsBoss) < 0) best = candidates[i];
            }
            return best.PrefabId;
        }

        private static int Compare(PrimaryCandidate left, PrimaryCandidate right, bool speciesIsBoss)
        {
            var result = BoolRank(left.IsBoss == speciesIsBoss, right.IsBoss == speciesIsBoss);
            if (result != 0) return result;

            var leftAffinity = TokenAffinity(left.PrefabId, left.DisplayToken);
            var rightAffinity = TokenAffinity(right.PrefabId, right.DisplayToken);
            result = BoolRank(leftAffinity == 3, rightAffinity == 3);
            if (result != 0) return result;

            result = BoolRank(left.SpawnEntries > 0, right.SpawnEntries > 0);
            if (result != 0) return result;

            result = right.SpawnEntries.CompareTo(left.SpawnEntries);
            if (result != 0) return result;

            result = rightAffinity.CompareTo(leftAffinity);
            if (result != 0) return result;

            result = SeparatorCount(left.PrefabId).CompareTo(SeparatorCount(right.PrefabId));
            if (result != 0) return result;

            result = left.PrefabId.Length.CompareTo(right.PrefabId.Length);
            return result != 0 ? result : string.CompareOrdinal(left.PrefabId, right.PrefabId);
        }

        private static int BoolRank(bool left, bool right)
        {
            return left == right ? 0 : left ? -1 : 1;
        }

        internal static int TokenAffinity(string prefabId, string displayToken)
        {
            var prefab = Normalize(prefabId);
            if (prefab.Length == 0) return 0;

            var token = (displayToken ?? string.Empty).TrimStart('$');
            var parts = token.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            var best = 0;
            for (var start = 0; start < parts.Length; start++)
            {
                var suffix = string.Empty;
                for (var i = start; i < parts.Length; i++) suffix += parts[i];
                suffix = Normalize(suffix);
                if (suffix.Length == 0) continue;
                if (prefab == suffix) return 3;
                if (prefab.StartsWith(suffix, StringComparison.Ordinal)
                    || suffix.StartsWith(prefab, StringComparison.Ordinal)) best = Math.Max(best, 2);
                else if (prefab.Contains(suffix) || suffix.Contains(prefab)) best = Math.Max(best, 1);
            }
            return best;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var chars = new char[value.Length];
            var length = 0;
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c)) chars[length++] = char.ToLowerInvariant(c);
            }
            return new string(chars, 0, length);
        }

        private static int SeparatorCount(string value)
        {
            var count = 0;
            foreach (var c in value ?? string.Empty)
            {
                if (c == '_' || c == '-' || char.IsWhiteSpace(c)) count++;
            }
            return count;
        }
    }
}
