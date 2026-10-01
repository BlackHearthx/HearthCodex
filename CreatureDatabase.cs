using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CreatureCodex
{
    /// <summary>
    /// Static creature data per species, read once from prefabs after CreatureCatalog is built.
    /// Read only: nothing on any prefab, drop, or spawn list is changed.
    /// </summary>
    internal static class CreatureDatabase
    {
        private sealed class SpawnEvidence
        {
            internal Heightmap.Biome Biomes;
            internal int Entries;
        }

        private static readonly Dictionary<string, CreatureData> ByToken =
            new Dictionary<string, CreatureData>(StringComparer.Ordinal);

        private static readonly string[] SampleTokens =
        {
            "$enemy_skeleton", "$enemy_troll", "$enemy_greydwarf", "$enemy_draugr",
            "$enemy_wolf", "$enemy_serpent", "$enemy_eikthyr"
        };

        private static readonly Heightmap.Biome[] BiomeOrder =
        {
            Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp,
            Heightmap.Biome.Mountain, Heightmap.Biome.Plains, Heightmap.Biome.Mistlands,
            Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth, Heightmap.Biome.Ocean
        };

        internal static bool IsBuilt { get; private set; }

        internal static bool TryGet(string displayToken, out CreatureData data)
        {
            if (string.IsNullOrEmpty(displayToken))
            {
                data = null;
                return false;
            }

            if (ByToken.TryGetValue(displayToken, out data))
            {
                return true;
            }

            // Any token of a merged species (FrozenKing_p3) finds the same entry.
            if (CreatureCatalog.TryGetSpecies(displayToken, out var species))
            {
                return ByToken.TryGetValue(species.DisplayToken, out data);
            }

            return false;
        }

        internal static IEnumerable<Heightmap.Biome> SplitBiomes(Heightmap.Biome biomes)
        {
            foreach (var b in BiomeOrder)
            {
                if ((biomes & b) != 0)
                {
                    yield return b;
                }
            }
        }

        internal static void BuildOnce(ZNetScene scene, bool verbose)
        {
            if (IsBuilt || !CreatureCatalog.IsBuilt || scene == null)
            {
                return;
            }

            ByToken.Clear();

            var buildTimer = Stopwatch.StartNew();
            var biomesByPrefab = CollectSpawnBiomes(scene, out var listCount, out var entryCount,
                out var locationCount, out var roomCount, out var locationEntryCount);
            var evidenceMilliseconds = buildTimer.ElapsedMilliseconds;
            var failed = 0;
            var withBiome = 0;
            var withDrops = 0;
            var changedPrimary = 0;

            foreach (var species in CreatureCatalog.Species)
            {
                try
                {
                    var variants = new List<CreatureVariantData>();
                    foreach (var member in species.Members)
                    {
                        var prefab = scene.GetPrefab(member.PrefabId);
                        var character = prefab != null ? prefab.GetComponent<Character>() : null;
                        if (character == null)
                        {
                            continue;
                        }

                        biomesByPrefab.TryGetValue(member.PrefabId, out var evidence);
                        variants.Add(ReadVariant(member, prefab, character,
                            evidence != null ? evidence.Biomes : Heightmap.Biome.None,
                            evidence != null ? evidence.Entries : 0));
                    }

                    if (variants.Count == 0)
                    {
                        failed++;
                        continue;
                    }

                    var legacyPrimary = variants
                        .OrderBy(v => v.PrefabId.Length)
                        .ThenBy(v => v.PrefabId, StringComparer.Ordinal)
                        .First();
                    var choices = variants.Select(v => new PrimaryCandidate(
                        v.PrefabId, v.DisplayToken, v.IsBoss, v.SpawnEntries)).ToList();
                    var primaryId = CreaturePrimarySelector.SelectId(choices, species.IsBoss);
                    var primary = variants.First(v => string.Equals(v.PrefabId, primaryId, StringComparison.Ordinal));
                    if (!string.Equals(primary.PrefabId, legacyPrimary.PrefabId, StringComparison.Ordinal))
                    {
                        changedPrimary++;
                        if (verbose)
                        {
                            Jotunn.Logger.LogInfo("Creature Codex: primary changed for " + species.DisplayToken
                                + ": " + legacyPrimary.PrefabId + " -> " + primary.PrefabId + ".");
                        }
                    }

                    var data = new CreatureData(species, primary, variants);
                    ByToken[species.DisplayToken] = data;

                    if (data.Biomes != Heightmap.Biome.None)
                    {
                        withBiome++;
                    }

                    if (data.Drops.Count > 0)
                    {
                        withDrops++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Jotunn.Logger.LogWarning("Creature Codex: could not read data for " + species.DisplayToken + ": " + ex.Message);
                }
            }

            IsBuilt = true;

            Jotunn.Logger.LogInfo(
                "Creature Codex: creature data for " + ByToken.Count + " of " + CreatureCatalog.SpeciesCount
                + " species (with biome " + withBiome + ", with drops " + withDrops + ", failed " + failed
                + ", improved primary " + changedPrimary + "). Spawn lists " + listCount
                + ", spawn entries " + entryCount + "; location assets " + locationCount
                + ", dungeon rooms " + roomCount + ", location spawn entries " + locationEntryCount
                + ". Built in " + buildTimer.ElapsedMilliseconds + " ms (biome evidence "
                + evidenceMilliseconds + " ms).");

            if (verbose)
            {
                LogSample();
            }
        }

        private static CreatureVariantData ReadVariant(CreaturePrefab member, GameObject prefab, Character character,
            Heightmap.Biome biomes, int spawnEntries)
        {
            var weakSpots = new List<CreatureWeakSpot>();
            if (character.m_weakSpots != null)
            {
                foreach (var ws in character.m_weakSpots)
                {
                    if (ws != null)
                    {
                        weakSpots.Add(new CreatureWeakSpot(ws.name, new CreatureResistances(ws.m_damageModifiers)));
                    }
                }
            }

            var drops = new List<CreatureDrop>();
            var characterDrop = prefab.GetComponent<CharacterDrop>();
            if (characterDrop != null && characterDrop.m_drops != null)
            {
                foreach (var d in characterDrop.m_drops)
                {
                    if (d == null || d.m_prefab == null)
                    {
                        continue;
                    }

                    string token = null;
                    Sprite icon = null;
                    var itemDrop = d.m_prefab.GetComponent<ItemDrop>();
                    var shared = itemDrop != null && itemDrop.m_itemData != null ? itemDrop.m_itemData.m_shared : null;
                    if (shared != null)
                    {
                        token = shared.m_name;
                        var icons = shared.m_icons;
                        if (icons != null && icons.Length > 0)
                        {
                            var variant = itemDrop.m_itemData.m_variant;
                            icon = icons[variant >= 0 && variant < icons.Length ? variant : 0];
                        }
                    }

                    drops.Add(new CreatureDrop(
                        d.m_prefab.name, token, icon, d.m_amountMin, d.m_amountMax,
                        d.m_chance, d.m_onePerPlayer, d.m_levelMultiplier, d.m_dontScale));
                }
            }

            return new CreatureVariantData(
                member.PrefabId, member.DisplayToken, member.IsBoss, character.m_health, biomes, spawnEntries,
                new CreatureResistances(character.m_damageModifiers), weakSpots, drops);
        }

        /// <summary>
        /// Vanilla SpawnSystemLists are referenced by SpawnSystem components on ZNetScene prefabs;
        /// Jötunn keeps its own list in DontDestroyOnLoad and adds it to each SpawnSystem when it wakes.
        /// Both are found here, once. Fixed locations and dungeon rooms contribute their authored
        /// ZoneLocation biome through CreatureSpawner, TriggerSpawner, SpawnArea and boss altars. Assets are loaded
        /// only while inspected and immediately released. Raids (RandomEvent) are left out: they say
        /// where a raid happens, not where a creature lives.
        /// </summary>
        private static Dictionary<string, SpawnEvidence> CollectSpawnBiomes(ZNetScene scene, out int listCount,
            out int entryCount, out int locationCount, out int roomCount, out int locationEntryCount)
        {
            var lists = new HashSet<SpawnSystemList>();
            var sourceErrors = 0;

            foreach (var name in scene.GetPrefabNames())
            {
                try
                {
                    var prefab = scene.GetPrefab(name);
                    var system = prefab != null ? prefab.GetComponent<SpawnSystem>() : null;
                    if (system == null || system.m_spawnLists == null)
                    {
                        continue;
                    }

                    foreach (var list in system.m_spawnLists)
                    {
                        if (list != null)
                        {
                            lists.Add(list);
                        }
                    }
                }
                catch
                {
                    sourceErrors++;
                }
            }

            foreach (var list in Resources.FindObjectsOfTypeAll<SpawnSystemList>())
            {
                if (list != null)
                {
                    lists.Add(list);
                }
            }

            var result = new Dictionary<string, SpawnEvidence>(StringComparer.Ordinal);
            entryCount = 0;

            foreach (var list in lists)
            {
                if (list.m_spawners == null)
                {
                    continue;
                }

                foreach (var spawn in list.m_spawners)
                {
                    if (spawn == null || !spawn.m_enabled || spawn.m_prefab == null || spawn.m_biome == Heightmap.Biome.None)
                    {
                        continue;
                    }

                    entryCount++;
                    var id = spawn.m_prefab.name;
                    if (!result.TryGetValue(id, out var evidence))
                    {
                        evidence = new SpawnEvidence();
                        result[id] = evidence;
                    }
                    evidence.Biomes |= spawn.m_biome;
                    evidence.Entries++;
                }
            }

            listCount = lists.Count;
            try
            {
                CollectLocationBiomes(result, out locationCount, out roomCount, out locationEntryCount);
            }
            catch (Exception ex)
            {
                locationCount = 0;
                roomCount = 0;
                locationEntryCount = 0;
                sourceErrors++;
                Jotunn.Logger.LogWarning("Creature Codex: location biome scan stopped early; global spawn data remains available. " + ex.Message);
            }
            if (sourceErrors > 0)
            {
                Jotunn.Logger.LogWarning("Creature Codex: skipped " + sourceErrors
                    + " incompatible biome source(s); the remaining catalogue is available.");
            }
            return result;
        }

        private static void CollectLocationBiomes(Dictionary<string, SpawnEvidence> result,
            out int locationCount, out int roomCount, out int entryCount)
        {
            locationCount = 0;
            roomCount = 0;
            entryCount = 0;
            var dungeonBiomes = new List<DungeonBiomeRule>();
            var zone = ZoneSystem.instance;

            if (zone != null && zone.m_locations != null)
            {
                var assets = new Dictionary<string, ZoneSystem.ZoneLocation>(StringComparer.Ordinal);
                var assetBiomes = new Dictionary<string, Heightmap.Biome>(StringComparer.Ordinal);
                foreach (var location in zone.m_locations)
                {
                    if (location == null || !location.m_enable || location.m_biome == Heightmap.Biome.None
                        || !location.m_prefab.IsValid) continue;
                    var key = location.m_prefab.Name;
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!assets.ContainsKey(key)) assets[key] = location;
                    assetBiomes.TryGetValue(key, out var knownBiomes);
                    assetBiomes[key] = knownBiomes | location.m_biome;
                }

                foreach (var pair in assets)
                {
                    var location = pair.Value;
                    var biomes = assetBiomes[pair.Key];
                    var loaded = false;
                    try
                    {
                        location.m_prefab.Load();
                        loaded = true;
                        var asset = location.m_prefab.Asset;
                        if (asset == null) continue;
                        locationCount++;
                        entryCount += CollectAssetSpawns(asset, biomes, result);

                        foreach (var generator in asset.GetComponentsInChildren<DungeonGenerator>(true))
                        {
                            if (generator != null && generator.m_themes != Room.Theme.None)
                            {
                                dungeonBiomes.Add(new DungeonBiomeRule
                                {
                                    Themes = generator.m_themes,
                                    Biomes = biomes
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Jotunn.Logger.LogWarning("Creature Codex: could not inspect location "
                            + location.m_prefab.Name + ": " + ex.Message);
                    }
                    finally
                    {
                        if (loaded) location.m_prefab.Release();
                    }
                }
            }

            if (DungeonDB.instance == null || dungeonBiomes.Count == 0) return;
            var rooms = new Dictionary<string, DungeonDB.RoomData>(StringComparer.Ordinal);
            var roomBiomes = new Dictionary<string, Heightmap.Biome>(StringComparer.Ordinal);
            foreach (var room in DungeonDB.GetRooms())
            {
                if (room == null || !room.m_enabled || !room.m_prefab.IsValid) continue;
                var biomes = CodexBiomeRules.MatchRoomBiomes(room.m_theme, dungeonBiomes);
                if (biomes == Heightmap.Biome.None) continue;
                var key = room.m_prefab.Name;
                if (string.IsNullOrEmpty(key)) continue;
                if (!rooms.ContainsKey(key)) rooms[key] = room;
                roomBiomes.TryGetValue(key, out var knownBiomes);
                roomBiomes[key] = knownBiomes | biomes;
            }

            foreach (var pair in rooms)
            {
                var room = pair.Value;
                var biomes = roomBiomes[pair.Key];
                var loaded = false;
                try
                {
                    room.m_prefab.Load();
                    loaded = true;
                    var asset = room.m_prefab.Asset;
                    if (asset == null) continue;
                    roomCount++;
                    entryCount += CollectAssetSpawns(asset, biomes, result);
                }
                catch (Exception ex)
                {
                    Jotunn.Logger.LogWarning("Creature Codex: could not inspect dungeon room "
                        + room.m_prefab.Name + ": " + ex.Message);
                }
                finally
                {
                    if (loaded) room.m_prefab.Release();
                }
            }
        }

        private static int CollectAssetSpawns(GameObject asset, Heightmap.Biome biomes,
            Dictionary<string, SpawnEvidence> result)
        {
            var count = 0;
            foreach (var spawner in asset.GetComponentsInChildren<CreatureSpawner>(true))
            {
                if (spawner != null && spawner.enabled && AddSpawnEvidence(spawner.m_creaturePrefab, biomes, result)) count++;
            }

            foreach (var spawner in asset.GetComponentsInChildren<TriggerSpawner>(true))
            {
                if (spawner == null || !spawner.enabled || spawner.m_spawnChance <= 0f
                    || spawner.m_creaturePrefabs == null) continue;
                foreach (var prefab in spawner.m_creaturePrefabs)
                {
                    if (AddSpawnEvidence(prefab, biomes, result)) count++;
                }
            }

            foreach (var area in asset.GetComponentsInChildren<SpawnArea>(true))
            {
                if (area == null || !area.enabled || area.m_prefabs == null) continue;
                foreach (var spawn in area.m_prefabs)
                {
                    if (spawn != null && spawn.m_weight > 0f && AddSpawnEvidence(spawn.m_prefab, biomes, result)) count++;
                }
            }

            foreach (var altar in asset.GetComponentsInChildren<OfferingBowl>(true))
            {
                if (altar != null && altar.enabled && AddSpawnEvidence(altar.m_bossPrefab, biomes, result)) count++;
            }
            return count;
        }

        private static bool AddSpawnEvidence(GameObject prefab, Heightmap.Biome biomes,
            Dictionary<string, SpawnEvidence> result)
        {
            if (prefab == null || biomes == Heightmap.Biome.None || prefab.GetComponent<Character>() == null)
                return false;
            var id = prefab.name;
            if (string.IsNullOrEmpty(id)) return false;
            if (!result.TryGetValue(id, out var evidence))
            {
                evidence = new SpawnEvidence();
                result[id] = evidence;
            }
            evidence.Biomes |= biomes;
            evidence.Entries++;
            return true;
        }

        private static void LogSample()
        {
            var sb = new StringBuilder();
            sb.Append("Creature Codex: creature data sample:");

            foreach (var token in SampleTokens)
            {
                if (!TryGet(token, out var d))
                {
                    sb.Append("\n  ").Append(token).Append(": not in the catalog");
                    continue;
                }

                sb.Append("\n  ").Append(d.DisplayToken).Append(" (").Append(d.DisplayName).Append(")")
                    .Append(d.IsBoss ? " [boss]" : string.Empty);
                sb.Append("\n    prefabs: ").Append(string.Join(", ", d.PrefabIds.ToArray()))
                    .Append(" (primary ").Append(d.Primary.PrefabId).Append(")");
                sb.Append("\n    base HP: ").Append(d.BaseHealth);

                var otherHp = d.Variants.Where(v => v.BaseHealth != d.BaseHealth)
                    .Select(v => v.PrefabId + " " + v.BaseHealth).ToArray();
                if (otherHp.Length > 0)
                {
                    sb.Append(" (other variants: ").Append(string.Join(", ", otherHp)).Append(")");
                }

                sb.Append("\n    modifiers: ").Append(FormatResistances(d.Resistances));

                foreach (var ws in d.WeakSpots)
                {
                    sb.Append("\n    weak spot ").Append(ws.Name).Append(": ").Append(FormatResistances(ws.Resistances));
                }

                sb.Append("\n    biomes: ");
                var biomes = SplitBiomes(d.Biomes).Select(b => b.ToString()).ToArray();
                sb.Append(biomes.Length > 0 ? string.Join(", ", biomes) : "none found in spawn lists");

                if (d.Drops.Count == 0)
                {
                    sb.Append("\n    drops: none");
                }

                foreach (var drop in d.Drops)
                {
                    sb.Append("\n    drop ").Append(drop.ItemPrefabId)
                        .Append(" [").Append(string.IsNullOrEmpty(drop.ItemToken) ? "no ItemDrop" : drop.ItemToken).Append("]")
                        .Append(" amount ").Append(drop.AmountMin).Append("-").Append(drop.AmountMax)
                        .Append(" (effective ").Append(drop.AmountMin).Append("-").Append(drop.EffectiveMax).Append(")")
                        .Append(", chance ").Append(drop.Chance)
                        .Append(drop.LevelMultiplier ? ", x level" : string.Empty)
                        .Append(drop.OnePerPlayer ? ", one per player" : string.Empty)
                        .Append(drop.DontScale ? ", not scaled" : string.Empty)
                        .Append(drop.Icon != null ? ", icon" : ", no icon");
                }
            }

            Jotunn.Logger.LogInfo(sb.ToString());
        }

        private static string FormatResistances(CreatureResistances r)
        {
            var parts = new List<string>();
            foreach (var type in CreatureResistances.ShownTypes)
            {
                var mod = r.Get(type);
                if (mod != HitData.DamageModifier.Normal)
                {
                    parts.Add(type + " " + mod);
                }
            }

            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "all Normal";
        }
    }
}
