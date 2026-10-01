using System.Collections.Generic;
using UnityEngine;

namespace CreatureCodex
{
    /// <summary>
    /// Copies transforms and render data, never a prefab or a component containing game scripts.
    /// No Instantiate, Animator, collider, Character, AI or ZNetView is used.
    /// The caller owns the returned root; meshes/materials remain shared, read-only assets.
    /// </summary>
    internal static class CodexVisualCopy
    {
        internal static GameObject Create(GameObject source)
        {
            var root = new GameObject("CodexVisual");
            root.SetActive(false);
            try
            {
                var transforms = new Dictionary<Transform, Transform>();
                CopyTransforms(source.transform, root.transform, transforms, true);
                var excluded = new HashSet<Renderer>();
                foreach (var group in source.GetComponentsInChildren<LODGroup>(true))
                {
                    var levels = group.GetLODs();
                    var first = new HashSet<Renderer>();
                    if (levels.Length > 0)
                        foreach (var renderer in levels[0].renderers) first.Add(renderer);
                    for (var i = 1; i < levels.Length; i++)
                        foreach (var renderer in levels[i].renderers)
                            if (!first.Contains(renderer)) excluded.Add(renderer);
                }

                var count = 0;
                foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled || excluded.Contains(renderer) || !VisibleBranch(renderer.transform, source.transform)) continue;
                    var target = transforms[renderer.transform].gameObject;
                    Renderer copy;
                    if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    {
                        var bones = skin.bones;
                        var mapped = new Transform[bones.Length];
                        var valid = true;
                        for (var i = 0; i < bones.Length; i++)
                        {
                            if (bones[i] != null && !transforms.TryGetValue(bones[i], out mapped[i])) valid = false;
                        }
                        if (!valid || (skin.rootBone != null && !transforms.ContainsKey(skin.rootBone))) continue;
                        var skinned = target.AddComponent<SkinnedMeshRenderer>();
                        skinned.sharedMesh = skin.sharedMesh;
                        skinned.bones = mapped;
                        skinned.rootBone = skin.rootBone == null ? null : transforms[skin.rootBone];
                        skinned.localBounds = skin.localBounds;
                        skinned.updateWhenOffscreen = true;
                        skinned.quality = skin.quality;
                        for (var i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                            skinned.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                        copy = skinned;
                    }
                    else if (renderer is MeshRenderer)
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null) continue;
                        target.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                        copy = target.AddComponent<MeshRenderer>();
                    }
                    else continue; // No particles, trails, lights or other runtime effects.

                    copy.sharedMaterials = renderer.sharedMaterials;
                    copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    copy.receiveShadows = false;
                    count++;
                }

                if (count == 0)
                {
                    Object.DestroyImmediate(root);
                    return null;
                }
                // Jotunn checks active visuals before cloning. Layer 31 is its rendering layer.
                // This temporary tree is disabled/destroyed synchronously by the caller.
                root.SetActive(true);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        private static void CopyTransforms(Transform source, Transform target,
            Dictionary<Transform, Transform> map, bool root)
        {
            map.Add(source, target);
            target.gameObject.layer = 31;
            target.localPosition = root ? Vector3.zero : source.localPosition;
            target.localRotation = root ? Quaternion.identity : source.localRotation;
            target.localScale = source.localScale;
            foreach (Transform child in source)
            {
                var copied = new GameObject(child.name).transform;
                copied.SetParent(target, false);
                CopyTransforms(child, copied, map, false);
                copied.gameObject.SetActive(child.gameObject.activeSelf);
            }
        }

        private static bool VisibleBranch(Transform node, Transform root)
        {
            for (; node != root; node = node.parent)
                if (!node.gameObject.activeSelf) return false;
            return true; // Prefab roots may be inactive; child visibility still matters.
        }
    }
}
