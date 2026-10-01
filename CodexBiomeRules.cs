using System.Collections.Generic;

namespace CreatureCodex
{
    internal sealed class DungeonBiomeRule
    {
        internal Room.Theme Themes;
        internal Heightmap.Biome Biomes;
    }

    internal static class CodexBiomeRules
    {
        internal static Heightmap.Biome MatchRoomBiomes(Room.Theme roomTheme,
            IEnumerable<DungeonBiomeRule> sources)
        {
            var result = Heightmap.Biome.None;
            if (roomTheme == Room.Theme.None || sources == null) return result;
            foreach (var source in sources)
            {
                if (source != null && (roomTheme & source.Themes) != Room.Theme.None)
                    result |= source.Biomes;
            }
            return result;
        }
    }
}
