// State & Prefs partial for Shapekey_animation_converter
// - Holds fields, constants, enums
// - Lifecycle (OnEnable/OnDisable)
// - Snapshot & Pref helpers
// - Target refresh helpers and utility methods

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    // Pref keys
    const string PREF_SAVE_FOLDER = "ShapekeyConverter_SaveFolder";
    const string PREF_LAST_TARGET = "ShapekeyConverter_LastTarget";
    const string PREF_SEARCH_MODE = "ShapekeyConverter_SearchMode";
    const string PREF_SEARCH_TEXT = "ShapekeyConverter_SearchText";
    const string PREF_SNAPSHOT = "ShapekeyConverter_Snapshot";
    const string PREF_ALIGN_TO_CLIP = "ShapekeyConverter_AlignToClip";

    // State fields
    string saveFolder = "Assets/Generated_Animations";
    GameObject targetObject;
    SkinnedMeshRenderer targetSkinnedMesh;
    Vector2 scroll;
    List<string> blendNames = new List<string>();
    List<float> blendValues = new List<float>();
    List<bool> includeFlags = new List<bool>();
    string searchText = string.Empty;
    enum SearchMode { Prefix = 0, Contains = 1 }
    SearchMode searchMode = SearchMode.Prefix;
    List<float> snapshotValues = null;
    AnimationClip loadedClip = null; // for applying values to mesh
    AnimationClip baseAlignClip = null; // for aligning save keys (shown only when option enabled)
    // Options
    bool alignToExistingClipKeys = false; // When saving, include only keys found in loadedClip; disables excludeZero
    

    void OnEnable()
    {
        saveFolder = EditorPrefs.GetString(PREF_SAVE_FOLDER, saveFolder);
        searchMode = (SearchMode)EditorPrefs.GetInt(PREF_SEARCH_MODE, (int)SearchMode.Prefix);
        searchText = EditorPrefs.GetString(PREF_SEARCH_TEXT, string.Empty);
        alignToExistingClipKeys = EditorPrefs.GetBool(PREF_ALIGN_TO_CLIP, false);
        var last = EditorPrefs.GetString(PREF_LAST_TARGET, string.Empty);
        if (!string.IsNullOrEmpty(last))
        {
            var lastObj = EditorUtility.InstanceIDToObject(Convert.ToInt32(last)) as SkinnedMeshRenderer;
            if (lastObj != null)
            {
                targetSkinnedMesh = lastObj;
                targetObject = targetSkinnedMesh.gameObject; // internal convenience
                RefreshBlendList();
            }
        }
    }

    void OnDisable()
    {
        if (targetSkinnedMesh) EditorPrefs.SetString(PREF_LAST_TARGET, targetSkinnedMesh.GetInstanceID().ToString());
        EditorPrefs.SetString(PREF_SAVE_FOLDER, saveFolder);
        EditorPrefs.SetInt(PREF_SEARCH_MODE, (int)searchMode);
        EditorPrefs.SetString(PREF_SEARCH_TEXT, searchText);
        EditorPrefs.SetBool(PREF_ALIGN_TO_CLIP, alignToExistingClipKeys);
        // persist snapshot if exists
        if (snapshotValues != null && snapshotValues.Count > 0)
        {
            var parts = new string[snapshotValues.Count];
            for (int i = 0; i < snapshotValues.Count; i++) parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            EditorPrefs.SetString(PREF_SNAPSHOT, string.Join(",", parts));
        }
        SaveBlendValuesPrefs();
    }

    void RefreshTargetFromObject()
    {
        if (targetObject == null)
        {
            targetSkinnedMesh = null;
            blendNames.Clear();
            blendValues.Clear();
            return;
        }
        targetSkinnedMesh = targetObject.GetComponentInChildren<SkinnedMeshRenderer>();
        RefreshBlendList();
    }

    void RefreshBlendList()
    {
        blendNames.Clear();
        blendValues.Clear();
        includeFlags.Clear();
        if (targetSkinnedMesh == null || targetSkinnedMesh.sharedMesh == null) return;
        var mesh = targetSkinnedMesh.sharedMesh;
        int count = mesh.blendShapeCount;
        for (int i = 0; i < count; i++)
        {
            blendNames.Add(mesh.GetBlendShapeName(i));
            blendValues.Add(targetSkinnedMesh.GetBlendShapeWeight(i));
            includeFlags.Add(true); // default include
        }
        // Try to restore saved values for this mesh/instance
        LoadBlendValuesPrefs();
        // Try to restore include flags for this mesh/instance
        LoadIncludeFlagsPrefs();
        // Ensure includeFlags length matches
        if (includeFlags.Count != blendNames.Count)
        {
            var old = includeFlags;
            includeFlags = new List<bool>(blendNames.Count);
            for (int i = 0; i < blendNames.Count; i++)
            {
                bool val = i < old.Count ? old[i] : true;
                includeFlags.Add(val);
            }
        }
        // auto create snapshot at load
        CreateSnapshot(loadTime: true);
    }

    void CreateSnapshot(bool loadTime = false)
    {
        if (blendValues == null || blendValues.Count == 0) return;
        snapshotValues = new List<float>(blendValues);
        if (!loadTime)
        {
            try
            {
                var parts = new string[snapshotValues.Count];
                for (int i = 0; i < snapshotValues.Count; i++) parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                EditorPrefs.SetString(PREF_SNAPSHOT, string.Join(",", parts));
            }
            catch { }
        }
    }

    void RestoreSnapshot()
    {
        if (snapshotValues == null || snapshotValues.Count == 0)
        {
            if (EditorPrefs.HasKey(PREF_SNAPSHOT))
            {
                var s = EditorPrefs.GetString(PREF_SNAPSHOT);
                var parts = s.Split(',');
                snapshotValues = new List<float>();
                for (int i = 0; i < parts.Length; i++)
                {
                    if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f)) snapshotValues.Add(f);
                    else snapshotValues.Add(0f);
                }
            }
        }
        if (snapshotValues == null) return;
        int n = Math.Min(snapshotValues.Count, blendValues.Count);
        for (int i = 0; i < n; i++)
        {
            blendValues[i] = snapshotValues[i];
            if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, snapshotValues[i]);
        }
        SaveBlendValuesPrefs();
    }

    string GetBlendPrefsKey()
    {
        if (targetSkinnedMesh == null || targetSkinnedMesh.sharedMesh == null) return null;
        string scene = targetObject ? targetObject.scene.name : "";
        string rel = GetRelativePath(targetSkinnedMesh.transform, targetObject ? targetObject.transform : targetSkinnedMesh.transform);
        string meshName = targetSkinnedMesh.sharedMesh.name;
        return $"ShapekeyConverter_Values|{scene}|{rel}|{meshName}";
    }

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
        try
        {
            string key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            key += "|IncludeFlags";
            var parts = new string[includeFlags.Count];
            for (int i = 0; i < includeFlags.Count; i++) parts[i] = includeFlags[i] ? "1" : "0";
            EditorPrefs.SetString(key, string.Join(",", parts));
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
                includeFlags[i] = parts[i] == "1" || parts[i].Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
    }

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
}
