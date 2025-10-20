using System;
using UnityEditor;

public partial class Shapekey_animation_converter
{
    void SaveBlendValuesPrefs()
    {
        try
        {
            string key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            var parts = new string[blendValues.Count];
            for (int i = 0; i < blendValues.Count; i++) parts[i] = blendValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            EditorPrefs.SetString(key, string.Join(",", parts));
        }
        catch { }
    }

    void LoadBlendValuesPrefs()
    {
        try
        {
            string key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            if (!EditorPrefs.HasKey(key)) return;
            var s = EditorPrefs.GetString(key);
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split(',');
            for (int i = 0; i < parts.Length && i < blendValues.Count; i++)
            {
                if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                {
                    blendValues[i] = f;
                    if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, f);
                }
            }
        }
        catch { }
    }

    void SaveIncludeFlagsPrefs()
    {
        // Mark as dirty instead of saving immediately
        includeFlagsDirty = true;
        lastIncludeFlagsChangeTime = EditorApplication.timeSinceStartup;
    }

    void SaveIncludeFlagsPrefsImmediate()
    {
        try
        {
            string key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            key += "|IncludeFlags";
            var parts = new string[includeFlags.Count];
            for (int i = 0; i < includeFlags.Count; i++) parts[i] = includeFlags[i] ? "1" : "0";
            EditorPrefs.SetString(key, string.Join(",", parts));
            includeFlagsDirty = false;
        }
        catch { }
    }

    void LoadIncludeFlagsPrefs()
    {
        try
        {
            string key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            key += "|IncludeFlags";
            if (!EditorPrefs.HasKey(key)) return;
            var s = EditorPrefs.GetString(key);
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split(',');
            for (int i = 0; i < parts.Length && i < includeFlags.Count; i++)
            {
                includeFlags[i] = parts[i] == "1" || parts[i].Equals("true", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
    }

    string GetBlendPrefsKey()
    {
        if (targetSkinnedMesh == null || targetSkinnedMesh.sharedMesh == null) return null;
        string scene = targetObject ? targetObject.scene.name : "";
        string rel = GetRelativePath(targetSkinnedMesh.transform, targetObject ? targetObject.transform : targetSkinnedMesh.transform);
        string meshName = targetSkinnedMesh.sharedMesh.name;
        return $"ShapekeyConverter_Values|{scene}|{rel}|{meshName}";
    }
}
