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
            // Debug: Reset debug flags (commented out)
            // _lsDescFound = false;
            // _lsLipSyncPropFound = false;
            // _lsLipSyncMode = null;
            // _lsIsVisemeBlendshape = false;
            // _lsSmrPropFound = false;
            // _lsSmrName = null;
            // _lsNamesPropFound = false;
            // _lsNamesCount = 0;
            // _lsNamesSample.Clear();
            // Find VRC_AvatarDescriptor along the parent chain (root-most first hit)
            Component descriptor = null;
            var tr = targetSkinnedMesh.transform;
            while (tr != null && descriptor == null)
            {
                var comps = tr.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var t = c.GetType();
                    var name = t.Name;
                    var full = t.FullName;
                    if (name == "VRC_AvatarDescriptor" || name == "VRCAvatarDescriptor" || (full != null && (full.Contains("VRC_AvatarDescriptor") || full.Contains("VRCAvatarDescriptor"))))
                    {
                        descriptor = c; break;
                    }
                }
                tr = tr.parent;
            }
            if (descriptor == null) return;
            // Debug: mark descriptor found
            // _lsDescFound = true;

            var dtype = descriptor.GetType();
            // lipSync style property enum; when "VisemeBlendShape" or similar, a SkinnedMeshRenderer and names are used
            var lipSyncStyleProp = dtype.GetProperty("lipSync");
            var lipSyncStyleField = dtype.GetField("lipSync");
            object lipSyncVal = null;
            if (lipSyncStyleProp != null) lipSyncVal = lipSyncStyleProp.GetValue(descriptor, null);
            else if (lipSyncStyleField != null) lipSyncVal = lipSyncStyleField.GetValue(descriptor);
            // Debug: record lipSync prop/field detection
            // _lsLipSyncPropFound = (lipSyncStyleProp != null) || (lipSyncStyleField != null);
            string lipSyncStyleName = lipSyncVal != null ? lipSyncVal.ToString() : null;
            // Debug: record lipSync mode string
            // _lsLipSyncMode = lipSyncStyleName;
            if (string.IsNullOrEmpty(lipSyncStyleName)) return;
            if (!lipSyncStyleName.Contains("VisemeBlendShape")) return; // only relevant when using blendshape visemes
            // Debug: record viseme mode flag
            // _lsIsVisemeBlendshape = true;

            // Fetch visemeSkinnedMesh and visemeBlendShapes string array
            var smrProp = dtype.GetProperty("VisemeSkinnedMesh");
            var smrField = dtype.GetField("VisemeSkinnedMesh");
            var namesProp = dtype.GetProperty("VisemeBlendShapes");
            var namesField = dtype.GetField("VisemeBlendShapes");
            if (smrProp == null && smrField == null) { /* Debug: _lsSmrPropFound = false; */ return; }
            if (namesProp == null && namesField == null) { /* Debug: _lsNamesPropFound = false; */ return; }
            // Debug: record prop/field presence
            // _lsSmrPropFound = (smrProp != null) || (smrField != null);
            // _lsNamesPropFound = (namesProp != null) || (namesField != null);
            var visemeSmrObj = smrProp != null ? smrProp.GetValue(descriptor, null) : smrField.GetValue(descriptor);
            var visemeSmr = visemeSmrObj as SkinnedMeshRenderer;
            // Debug: record SMR name
            // _lsSmrName = visemeSmr ? visemeSmr.name : null;
            var namesObj = namesProp != null ? namesProp.GetValue(descriptor, null) : namesField.GetValue(descriptor);
            var names = namesObj as string[];
            if (names == null || names.Length == 0) return;
            // Debug: record names count and sample
            // _lsNamesCount = names.Length;
            // for (int si = 0; si < names.Length && si < 10; si++)
            // {
            //     if (!string.IsNullOrEmpty(names[si])) _lsNamesSample.Add(names[si]);
            // }
            // Relaxed condition: exclude by name match regardless of which SMR is assigned
            var set = new HashSet<string>(names);
            for (int i = 0; i < blendNames.Count; i++)
            {
                if (!string.IsNullOrEmpty(blendNames[i]) && set.Contains(blendNames[i]))
                {
                    isLipSyncShapeCache[i] = true;
                }
            }
        }
        catch { }
    }

    // Symmetry helpers
    enum LRSide { None, L, R }

    static bool TryParseLRSuffix(string name, out string baseName, out LRSide side)
    {
        baseName = name;
        side = LRSide.None;
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.Trim();
        // common suffix patterns: _L/_R, .L/.R, -L/-R, ' L'/' R', '(L)'/'(R)'
        string[,] patterns = new string[,] { {"_L", "_R"}, {".L", ".R"}, {"-L", "-R"}, {" L", " R"} };
        // Parenthesis pattern
        if (n.EndsWith("(L)", StringComparison.OrdinalIgnoreCase)) { baseName = n.Substring(0, n.Length - 3).TrimEnd(); side = LRSide.L; return true; }
        if (n.EndsWith("(R)", StringComparison.OrdinalIgnoreCase)) { baseName = n.Substring(0, n.Length - 3).TrimEnd(); side = LRSide.R; return true; }
        for (int i = 0; i < patterns.GetLength(0); i++)
        {
            string l = patterns[i,0];
            string r = patterns[i,1];
            if (n.EndsWith(l, StringComparison.OrdinalIgnoreCase)) { baseName = n.Substring(0, n.Length - l.Length); side = LRSide.L; return true; }
            if (n.EndsWith(r, StringComparison.OrdinalIgnoreCase)) { baseName = n.Substring(0, n.Length - r.Length); side = LRSide.R; return true; }
        }
        return false;
    }

    static string GetSymmetryDisplayName(string name)
    {
        if (TryParseLRSuffix(name, out var baseName, out var side))
        {
            if (side != LRSide.None) return baseName + "_LR";
        }
        return name;
    }
}
