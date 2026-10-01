using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreatureCodex
{
    /// <summary>
    /// A creature's picture in the book is the icon of the trophy it drops: a drop whose item
    /// has ItemDrop.ItemData.SharedData.m_itemType == ItemType.Trophy. Species without one get null
    /// and the book draws its own placeholder. Looked up once per species and cached.
    /// </summary>
    internal static class CodexIcons
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        internal static Sprite GetTrophyIcon(CreatureData data)
        {
            if (data == null)
            {
                return null;
            }

            if (Cache.TryGetValue(data.DisplayToken, out var cached))
            {
                return cached;
            }

            var icon = FindTrophy(data);
            Cache[data.DisplayToken] = icon;
            return icon;
        }

        private static Sprite FindTrophy(CreatureData data)
        {
            var scene = ZNetScene.instance;
            if (scene == null)
            {
                return null;
            }

            var found = TrophyIn(scene, data.Primary);
            if (found != null)
            {
                return found;
            }

            // Do not reveal a special variant's trophy when only the species is known.
            return null;
        }

        private static Sprite TrophyIn(ZNetScene scene, CreatureVariantData variant)
        {
            foreach (var drop in variant.Drops)
            {
                if (drop.Icon == null)
                {
                    continue;
                }

                var prefab = scene.GetPrefab(drop.ItemPrefabId);
                var itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                var shared = itemDrop != null && itemDrop.m_itemData != null ? itemDrop.m_itemData.m_shared : null;
                if (shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy)
                {
                    return drop.Icon;
                }
            }

            return null;
        }
    }
}
