using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreatureCodex
{
    internal sealed class WeaponRecommendation
    {
        internal ItemDrop.ItemData Weapon;
        internal ItemDrop.ItemData Ammo;
        internal float BaseDamage;
        internal float EffectiveDamage;
        internal bool ExploitsWeakness;
        internal HitData.DamageType WeaknessType;
    }

    internal static class CodexWeapons
    {
        // Read on detail refresh only; never mutate prefab quality or player inventory.
        internal static List<WeaponRecommendation> Recommend(Player player, CreatureResistances resistances)
        {
            var result = new List<WeaponRecommendation>();
            if (player == null || resistances == null || ObjectDB.instance == null) return result;
            var weapons = new List<ItemDrop.ItemData>();
            var ammo = new List<ItemDrop.ItemData>();
            var seen = new HashSet<ItemDrop>();
            foreach (var recipe in ObjectDB.instance.m_recipes)
            {
                if (recipe == null || !recipe.m_enabled || recipe.m_item == null || !seen.Add(recipe.m_item)) continue;
                var item = recipe.m_item.m_itemData;
                if (item == null || item.m_shared == null || !player.IsRecipeKnown(item.m_shared.m_name)) continue;
                var type = item.m_shared.m_itemType;
                if (type == ItemDrop.ItemData.ItemType.Ammo) ammo.Add(item);
                else if (type == ItemDrop.ItemData.ItemType.OneHandedWeapon
                    || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                    || type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft
                    || type == ItemDrop.ItemData.ItemType.Bow
                    || type == ItemDrop.ItemData.ItemType.Torch) weapons.Add(item);
            }

            return Rank(weapons, ammo, resistances);
        }

        internal static List<WeaponRecommendation> Rank(List<ItemDrop.ItemData> weapons,
            List<ItemDrop.ItemData> ammo, CreatureResistances resistances)
        {
            var result = new List<WeaponRecommendation>();
            foreach (var weapon in weapons)
            {
                WeaponRecommendation best = null;
                if (string.IsNullOrEmpty(weapon.m_shared.m_ammoType))
                {
                    best = Score(weapon, null, resistances);
                }
                else
                {
                    foreach (var projectile in ammo)
                    {
                        if (!string.Equals(projectile.m_shared.m_ammoType, weapon.m_shared.m_ammoType, StringComparison.Ordinal)) continue;
                        var candidate = Score(weapon, projectile, resistances);
                        if (candidate != null && (best == null || Compare(candidate, best) < 0)) best = candidate;
                    }
                }
                if (best != null) result.Add(best);
            }
            result.Sort(Compare);
            // Do not fill remaining slots with resistant/neutral weapons when a suitable
            // weakness match exists. Alternatives are only shown when there are no matches.
            if (result.Exists(entry => entry.ExploitsWeakness))
                result.RemoveAll(entry => !entry.ExploitsWeakness);

            // Give the player useful alternatives instead of letting one weapon family
            // occupy every slot (for example, three crossbows against a pierce weakness).
            // The list is already ordered by weakness suitability and effective damage,
            // so the first entry seen for each family is that family's best option.
            var recommendations = new List<WeaponRecommendation>();
            var families = new HashSet<string>();
            foreach (var entry in result)
            {
                if (!families.Add(FamilyKey(entry))) continue;
                recommendations.Add(entry);
                if (recommendations.Count == 3) return recommendations;
            }

            // If fewer than three families are available, retain the previous useful
            // behaviour by filling the remaining slots with the next strongest choices.
            foreach (var entry in result)
            {
                if (recommendations.Contains(entry)) continue;
                recommendations.Add(entry);
                if (recommendations.Count == 3) break;
            }
            return recommendations;
        }

        internal static WeaponRecommendation Score(ItemDrop.ItemData weapon, ItemDrop.ItemData ammo, CreatureResistances resistances)
        {
            // Attack rejects untamed targets for m_tamedOnly weapons (e.g. butcher knife).
            // The codex recommends combat equipment, not slaughter tools, for every species.
            if (weapon == null || weapon.m_shared == null || weapon.m_shared.m_tamedOnly
                || (ammo != null && (ammo.m_shared == null || ammo.m_shared.m_tamedOnly))) return null;
            var damage = weapon.GetDamage(1, 0f);
            if (ammo != null) damage.Add(ammo.GetDamage(1, 0f));
            // Utility damage to trees/rocks must not make an axe/pick look stronger in combat.
            damage.m_chop = 0f;
            damage.m_pickaxe = 0f;
            var baseline = damage.GetTotalDamage();
            var effective = resistances.Apply(damage).GetTotalDamage();
            if (float.IsNaN(baseline) || float.IsInfinity(baseline) || baseline <= 0f
                || float.IsNaN(effective) || float.IsInfinity(effective) || effective <= 0f) return null;
            var weakDamage = 0f;
            var strongestWeak = 0f;
            var mainDamage = Math.Max(damage.m_damage, damage.m_nonPlayer);
            var weakness = HitData.DamageType.Blunt;
            foreach (var type in CreatureResistances.ShownTypes)
            {
                var amount = Amount(damage, type);
                mainDamage = Math.Max(mainDamage, amount);
                var modifier = resistances.Get(type);
                if (modifier != HitData.DamageModifier.SlightlyWeak
                    && modifier != HitData.DamageModifier.Weak && modifier != HitData.DamageModifier.VeryWeak) continue;
                weakDamage += amount;
                if (amount > strongestWeak) { strongestWeak = amount; weakness = type; }
            }
            // A tiny elemental component must not qualify a mostly resisted weapon.
            // At least half its base damage must hit weaknesses, a largest damage
            // component must be weak, and gains must exceed all resistance losses.
            var exploits = strongestWeak > 0f && strongestWeak >= mainDamage
                && weakDamage >= baseline * 0.5f && effective > baseline;
            return new WeaponRecommendation { Weapon = weapon, Ammo = ammo, BaseDamage = baseline,
                EffectiveDamage = effective, ExploitsWeakness = exploits, WeaknessType = weakness };
        }

        private static float Amount(HitData.DamageTypes damage, HitData.DamageType type)
        {
            switch (type)
            {
                case HitData.DamageType.Blunt: return damage.m_blunt;
                case HitData.DamageType.Slash: return damage.m_slash;
                case HitData.DamageType.Pierce: return damage.m_pierce;
                case HitData.DamageType.Fire: return damage.m_fire;
                case HitData.DamageType.Frost: return damage.m_frost;
                case HitData.DamageType.Lightning: return damage.m_lightning;
                case HitData.DamageType.Poison: return damage.m_poison;
                case HitData.DamageType.Spirit: return damage.m_spirit;
                default: return 0f;
            }
        }

        private static int Compare(WeaponRecommendation a, WeaponRecommendation b)
        {
            var order = b.ExploitsWeakness.CompareTo(a.ExploitsWeakness);
            if (order != 0) return order;
            order = b.EffectiveDamage.CompareTo(a.EffectiveDamage);
            if (order != 0) return order;
            order = string.CompareOrdinal(a.Weapon.m_shared.m_name, b.Weapon.m_shared.m_name);
            return order != 0 ? order : string.CompareOrdinal(a.Ammo?.m_shared.m_name, b.Ammo?.m_shared.m_name);
        }

        private static string FamilyKey(WeaponRecommendation entry)
        {
            var shared = entry.Weapon.m_shared;
            if (shared.m_skillType != Skills.SkillType.None)
                return "skill:" + (int)shared.m_skillType;

            // Modded weapons occasionally omit a skill. Keep equivalent items together,
            // while separating ranged ammunition families from unrelated weapon types.
            return "item:" + (int)shared.m_itemType + "|ammo:" + (shared.m_ammoType ?? string.Empty);
        }
    }
}
