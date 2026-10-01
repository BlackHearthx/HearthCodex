using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace CreatureCodex
{
    internal static class CodexPortraits
    {
        // Bounded owned GPU resources. Failed renders are remembered until the UI is destroyed.
        private const int Capacity = 24;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly Queue<string> Order = new Queue<string>();
        private static readonly HashSet<string> Failed = new HashSet<string>(StringComparer.Ordinal);

        internal static Sprite Get(CreatureSpecies species, CreatureData data)
        {
            if (species == null || species.Knowledge == CodexKnowledge.Unknown || data?.Primary == null) return null;
            var id = data.Primary.PrefabId;
            if (Cache.TryGetValue(id, out var cached)) return cached;
            if (Failed.Contains(id)) return null;
            var scene = ZNetScene.instance;
            if (scene == null) return null;
            var prefab = scene.GetPrefab(id);
            if (prefab == null) return null;
            GameObject visual = null;
            Sprite sprite = null;
            try
            {
                visual = CodexVisualCopy.Create(prefab);
                if (visual != null)
                {
                    // Only Jotunn's clone belongs in the shot. Keep our active source outside
                    // its camera frustum; SpawnRenderClone resets the clone position to zero.
                    visual.transform.position = new Vector3(1000000f, 1000000f, 1000000f);
                    sprite = RenderManager.Instance.Render(new RenderManager.RenderRequest(visual)
                    {
                        Width = 256,
                        Height = 256,
                        Rotation = Quaternion.Euler(8f, 25f, 0f),
                        DistanceMultiplier = 0.65f,
                        ParticleSimulationTime = -1f,
                        UseCache = false
                    });
                }
                if (sprite == null || sprite.rect.width <= 0f)
                {
                    Failed.Add(id);
                    return null;
                }
                while (Order.Count >= Capacity)
                {
                    var oldest = Order.Dequeue();
                    Release(Cache[oldest]);
                    Cache.Remove(oldest);
                }
                Cache.Add(id, sprite);
                Order.Enqueue(id);
                return sprite;
            }
            catch (Exception ex)
            {
                Failed.Add(id);
                Release(sprite);
                Jotunn.Logger.LogWarning("Creature Codex: portrait unavailable for " + id + "; using trophy. " + ex.Message);
                return null;
            }
            finally
            {
                if (visual != null)
                {
                    visual.SetActive(false);
                    UnityEngine.Object.DestroyImmediate(visual);
                }
            }
        }

        internal static void Clear()
        {
            foreach (var sprite in Cache.Values) Release(sprite);
            Cache.Clear();
            Order.Clear();
            Failed.Clear();
        }

        private static void Release(Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            UnityEngine.Object.Destroy(sprite);
            if (texture != null && texture != Texture2D.whiteTexture) UnityEngine.Object.Destroy(texture);
        }
    }
}
