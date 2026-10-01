using System.Collections.Generic;
using UnityEngine;

namespace CreatureCodex
{
    /// <summary>
    /// A copy of Character.m_damageModifiers (or WeakSpot.m_damageModifiers) as authored on the prefab.
    /// Runtime Character.GetDamageModifiers() adds status-effect mods on top; this does not.
    /// Only Player overrides ApplyArmorDamageMods, so creatures have no armor layer to miss.
    /// </summary>
    internal sealed class CreatureResistances
    {
        /// <summary>The ten damage types the codex shows, in display order.</summary>
        internal static readonly HitData.DamageType[] ShownTypes =
        {
            HitData.DamageType.Blunt,
            HitData.DamageType.Slash,
            HitData.DamageType.Pierce,
            HitData.DamageType.Chop,
            HitData.DamageType.Pickaxe,
            HitData.DamageType.Fire,
            HitData.DamageType.Frost,
            HitData.DamageType.Lightning,
            HitData.DamageType.Poison,
            HitData.DamageType.Spirit
        };

        private readonly HitData.DamageModifiers _mods;

        internal CreatureResistances(HitData.DamageModifiers mods)
        {
            _mods = mods;
        }

        public HitData.DamageModifier Get(HitData.DamageType type)
        {
            return _mods.GetModifier(type);
        }

        internal HitData.DamageTypes Apply(HitData.DamageTypes damage)
        {
            var hit = new HitData { m_damage = damage };
            hit.ApplyResistance(_mods, out _);
            return hit.m_damage;
        }

        internal float Multiplier(HitData.DamageType type)
        {
            var unit = new HitData.DamageTypes();
            switch (type)
            {
                case HitData.DamageType.Blunt: unit.m_blunt = 1f; break;
                case HitData.DamageType.Slash: unit.m_slash = 1f; break;
                case HitData.DamageType.Pierce: unit.m_pierce = 1f; break;
                case HitData.DamageType.Chop: unit.m_chop = 1f; break;
                case HitData.DamageType.Pickaxe: unit.m_pickaxe = 1f; break;
                case HitData.DamageType.Fire: unit.m_fire = 1f; break;
                case HitData.DamageType.Frost: unit.m_frost = 1f; break;
                case HitData.DamageType.Lightning: unit.m_lightning = 1f; break;
                case HitData.DamageType.Poison: unit.m_poison = 1f; break;
                case HitData.DamageType.Spirit: unit.m_spirit = 1f; break;
                default: return 1f;
            }

            return Apply(unit).GetTotalDamage();
        }
    }

    internal sealed class CreatureWeakSpot
    {
        public string Name { get; }
        public CreatureResistances Resistances { get; }

        internal CreatureWeakSpot(string name, CreatureResistances resistances)
        {
            Name = name ?? string.Empty;
            Resistances = resistances;
        }
    }

    /// <summary>
    /// One CharacterDrop.Drop, values as authored. CharacterDrop.GenerateDropList at the default
    /// resource rate rolls Random.Range(AmountMin, AmountMax) on ints, which never returns AmountMax
    /// when AmountMax > AmountMin; EffectiveMax is that result. With LevelMultiplier, chance and amount
    /// are multiplied by 2^(level-1). Chances of 0.3 or less use the game's pseudo-random drop counter.
    /// </summary>
    internal sealed class CreatureDrop
    {
        public string ItemPrefabId { get; }
        public string ItemToken { get; }
        public Sprite Icon { get; }
        public int AmountMin { get; }
        public int AmountMax { get; }
        public int EffectiveMax => AmountMax > AmountMin ? AmountMax - 1 : AmountMin;
        public float Chance { get; }
        public bool OnePerPlayer { get; }
        public bool LevelMultiplier { get; }
        public bool DontScale { get; }

        internal CreatureDrop(string itemPrefabId, string itemToken, Sprite icon, int amountMin, int amountMax,
            float chance, bool onePerPlayer, bool levelMultiplier, bool dontScale)
        {
            ItemPrefabId = itemPrefabId ?? string.Empty;
            ItemToken = itemToken ?? string.Empty;
            Icon = icon;
            AmountMin = amountMin;
            AmountMax = amountMax;
            Chance = chance;
            OnePerPlayer = onePerPlayer;
            LevelMultiplier = levelMultiplier;
            DontScale = dontScale;
        }
    }

    /// <summary>
    /// Static data of one prefab of a species. BaseHealth is Character.m_health on the prefab,
    /// before world level (GetMaxHealthBase) and star level (SetupMaxHealth multiplies by level).
    /// </summary>
    internal sealed class CreatureVariantData
    {
        public string PrefabId { get; }
        public string DisplayToken { get; }
        public bool IsBoss { get; }
        public float BaseHealth { get; }
        public Heightmap.Biome Biomes { get; }
        public int SpawnEntries { get; }
        public CreatureResistances Resistances { get; }
        public IReadOnlyList<CreatureWeakSpot> WeakSpots { get; }
        public IReadOnlyList<CreatureDrop> Drops { get; }

        internal CreatureVariantData(string prefabId, string displayToken, bool isBoss, float baseHealth,
            Heightmap.Biome biomes, int spawnEntries, CreatureResistances resistances,
            List<CreatureWeakSpot> weakSpots, List<CreatureDrop> drops)
        {
            PrefabId = prefabId;
            DisplayToken = displayToken ?? string.Empty;
            IsBoss = isBoss;
            BaseHealth = baseHealth;
            Biomes = biomes;
            SpawnEntries = spawnEntries;
            Resistances = resistances;
            WeakSpots = weakSpots.AsReadOnly();
            Drops = drops.AsReadOnly();
        }
    }

    /// <summary>
    /// Static gameplay data of one species, read once from prefabs. No player progress here.
    /// Primary is selected from token identity, boss compatibility and actual spawn registration;
    /// the other variants stay available because their values can differ.
    /// Biomes come from the primary prefab only, so hidden variants cannot add spoiler locations.
    /// </summary>
    internal sealed class CreatureData
    {
        public string DisplayToken { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> PrefabIds { get; }
        public bool IsBoss { get; }
        public CreatureVariantData Primary { get; }
        public IReadOnlyList<CreatureVariantData> Variants { get; }
        public Heightmap.Biome Biomes { get; }

        public float BaseHealth => Primary.BaseHealth;
        public CreatureResistances Resistances => Primary.Resistances;
        public IReadOnlyList<CreatureWeakSpot> WeakSpots => Primary.WeakSpots;
        public IReadOnlyList<CreatureDrop> Drops => Primary.Drops;

        internal CreatureData(CreatureSpecies species, CreatureVariantData primary, List<CreatureVariantData> variants)
        {
            DisplayToken = species.DisplayToken;
            DisplayName = species.DisplayName;
            PrefabIds = species.PrefabIds;
            IsBoss = species.IsBoss;
            Primary = primary;
            Variants = variants.AsReadOnly();
            Biomes = primary.Biomes;
        }
    }
}
