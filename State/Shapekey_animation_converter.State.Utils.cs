using System;
using System.Collections.Generic;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    static string GetRelativePath(Transform target, Transform root)
    {
        if (target == root) return "";
        var parts = new List<string>();
        var t = target;
        while (t != null && t != root)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    // Helper: detect VRChat control shapekeys that should always be ignored/hidden
    static bool IsVrcShapeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = name.Trim();
        // Hide names starting with "vrc." or ".vrc" (case-insensitive)
        if (name.StartsWith("vrc.", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith(".vrc", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string GetGroupKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Other";
        name = name.Trim();
        // 1) delimiter-based first token
        int idx = IndexOfAny(name, new char[] { ' ', '_', '-', '/' , '.' , '0' , '1' , '2' , '3' , '4' , '5' , '6' , '7' , '8' , '9' });
        string token;
        if (idx > 0)
        {
            token = name.Substring(0, idx);
        }
        else
        {
            // 2) CamelCase: split at first upper following a lower
            int cut = -1;
            for (int i = 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && char.IsLetter(name[i - 1]) && char.IsLower(name[i - 1]))
                {
                    cut = i; break;
                }
            }
            token = cut > 0 ? name.Substring(0, cut) : name;
        }
        token = token.Trim();
        if (token.Length == 0) return "Other";
        return token;
    }

    static int IndexOfAny(string s, char[] chars)
    {
        int best = -1;
        for (int i = 0; i < chars.Length; i++)
        {
            int p = s.IndexOf(chars[i]);
            if (p >= 0 && (best < 0 || p < best)) best = p;
        }
        return best;
    }

    // Build cache for lip sync blendshapes to exclude, using VRC Avatar Descriptor if present
    void BuildLipSyncExclusionCache()
    {
        isLipSyncShapeCache.Clear();
        for (int i = 0; i < blendNames.Count; i++) isLipSyncShapeCache.Add(false);
        try
        {
            if (targetSkinnedMesh == null) return;
            var avatarRoot = targetSkinnedMesh.transform.root;
            if (avatarRoot == null) return;
            // Find a component named "VRC_AvatarDescriptor" via reflection (no hard reference)
            var comps = avatarRoot.GetComponents<Component>();
            Component descriptor = null;
            foreach (var c in comps)
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t.Name == "VRC_AvatarDescriptor") { descriptor = c; break; }
            }
            if (descriptor == null) return;

            var dtype = descriptor.GetType();
            // lipSync style property enum; when "VisemeBlendShape" or similar, a SkinnedMeshRenderer and names are used
            var lipSyncStyleProp = dtype.GetProperty("lipSync");
            var lipSyncVal = lipSyncStyleProp != null ? lipSyncStyleProp.GetValue(descriptor, null) : null;
            string lipSyncStyleName = lipSyncVal != null ? lipSyncVal.ToString() : null;
            if (string.IsNullOrEmpty(lipSyncStyleName)) return;
            if (!lipSyncStyleName.Contains("VisemeBlendShape")) return; // only relevant when using blendshape visemes

            // Fetch visemeSkinnedMesh and visemeBlendShapes string array
            var smrProp = dtype.GetProperty("VisemeSkinnedMesh");
            var namesProp = dtype.GetProperty("VisemeBlendShapes");
            if (smrProp == null || namesProp == null) return;
            var visemeSmr = smrProp.GetValue(descriptor, null) as SkinnedMeshRenderer;
            var names = namesProp.GetValue(descriptor, null) as string[];
            if (names == null || names.Length == 0) return;

            // If the descriptor points to the same SMR as our target, mark those names for exclusion
            if (visemeSmr == targetSkinnedMesh)
            {
                var set = new HashSet<string>(names);
                for (int i = 0; i < blendNames.Count; i++)
                {
                    if (!string.IsNullOrEmpty(blendNames[i]) && set.Contains(blendNames[i]))
                    {
                        isLipSyncShapeCache[i] = true;
                    }
                }
            }
        }
        catch { }
    }
}
