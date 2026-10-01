using System;
using System.Globalization;

namespace CreatureCodex
{
    internal enum CodexStateFilter { All, Unknown, Discovered, Studied }

    internal static class CodexFilters
    {
        internal static bool MatchesBiome(CodexKnowledge knowledge, bool allBiomes, bool dataBiomeMatches)
        {
            // Biome tabs mirror the complete catalogue. Identity, portrait, and details are
            // still hidden by the row/page presentation until the species is discovered.
            return allBiomes || dataBiomeMatches;
        }

        internal static int CompareForDisplay(CodexKnowledge leftKnowledge, string leftName, string leftId,
            CodexKnowledge rightKnowledge, string rightName, string rightId)
        {
            var leftKnown = leftKnowledge != CodexKnowledge.Unknown;
            var rightKnown = rightKnowledge != CodexKnowledge.Unknown;
            if (leftKnown != rightKnown) return leftKnown ? -1 : 1;

            if (leftKnown)
            {
                var named = string.Compare(leftName ?? string.Empty, rightName ?? string.Empty,
                    StringComparison.CurrentCultureIgnoreCase);
                if (named != 0) return named;
            }
            else
            {
                // A stable non-semantic order keeps every "???" identical without arranging
                // those rows by the hidden localized name or prefab/token spelling.
                var opaque = UnknownOrderKey(leftId).CompareTo(UnknownOrderKey(rightId));
                if (opaque != 0) return opaque;
            }

            return string.CompareOrdinal(leftId ?? string.Empty, rightId ?? string.Empty);
        }

        private static uint UnknownOrderKey(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in value ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        internal static bool Matches(CodexStateFilter state, CodexKnowledge knowledge,
            bool biomeMatches, string knownName, string query, string hiddenName)
        {
            if (!biomeMatches) return false;
            if ((state == CodexStateFilter.Unknown && knowledge != CodexKnowledge.Unknown)
                || (state == CodexStateFilter.Discovered && knowledge != CodexKnowledge.Discovered)
                || (state == CodexStateFilter.Studied && knowledge != CodexKnowledge.Studied)) return false;
            if (string.IsNullOrEmpty(query)) return true;
            var shown = knowledge == CodexKnowledge.Unknown ? hiddenName : knownName;
            return CultureInfo.CurrentCulture.CompareInfo.IndexOf(shown ?? string.Empty, query,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }
    }
}
