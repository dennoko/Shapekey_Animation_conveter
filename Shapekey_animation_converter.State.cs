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
    const string PREF_SEARCH_TEXT = "ShapekeyConverter_SearchText";
    const string PREF_SNAPSHOT = "ShapekeyConverter_Snapshot";
    const string PREF_ALIGN_TO_CLIP = "ShapekeyConverter_AlignToClip";
    const string PREF_SHOW_ONLY_INCLUDED = "ShapekeyConverter_ShowOnlyIncluded";

    // State fields
    string saveFolder = "Assets/Generated_Animations";
    GameObject targetObject;
    SkinnedMeshRenderer targetSkinnedMesh;
    Vector2 scroll;
    List<string> blendNames = new List<string>();
    List<float> blendValues = new List<float>();
    List<bool> includeFlags = new List<bool>();
    // Grouping structures
    // - name -> indices (legacy/general use)
    Dictionary<string, List<int>> groupToIndices = new Dictionary<string, List<int>>();
    List<string> groupOrder = new List<string>();
    // - contiguous segments to preserve original order and allow thresholds
    class GroupSegment { public string key; public int start; public int length; }
    List<GroupSegment> groupSegments = new List<GroupSegment>();
    string searchText = string.Empty;
    // Undo tracking for slider drag operations
    bool isSliderDragging = false;
    int currentDraggingIndex = -1;
        // Performance optimization: cached filter results and flags
        List<bool> isVrcShapeCache = new List<bool>();
        List<int> visibleIndices = new List<int>(); // Cached list of indices that pass all filters
        string lastSearchText = null;
        bool lastShowOnlyIncluded = false;
        bool filterCacheDirty = true;
        // Delayed EditorPrefs save
        bool includeFlagsDirty = false;
        double lastIncludeFlagsChangeTime = 0;
    List<float> snapshotValues = null;
    AnimationClip loadedClip = null; // for applying values to mesh
    AnimationClip baseAlignClip = null; // for aligning save keys (shown only when option enabled)
    // Options
    bool alignToExistingClipKeys = false; // When saving, include only keys found in loadedClip; disables excludeZero
    bool showOnlyIncluded = false; // Filter UI to show only shapes currently included

    // Status bar state
    enum StatusLevel { Info, Success, Warning, Error }
    string statusMessage = null;
    StatusLevel statusLevel = StatusLevel.Info;
    double statusSetAt = 0;
    double statusAutoClearSec = 0; // 0=don't auto clear

    // UI: collapsed groups
    const string PREF_GROUPS_COLLAPSED = "ShapekeyConverter_GroupsCollapsed";
    System.Collections.Generic.HashSet<string> collapsedGroups = new System.Collections.Generic.HashSet<string>();
    

    void OnEnable()
    {
        DenEmoLoc.LoadPrefs();
        saveFolder = DenEmoProjectPrefs.GetString(PREF_SAVE_FOLDER, saveFolder);
        searchText = DenEmoProjectPrefs.GetString(PREF_SEARCH_TEXT, string.Empty);
        alignToExistingClipKeys = DenEmoProjectPrefs.GetBool(PREF_ALIGN_TO_CLIP, false);
        showOnlyIncluded = DenEmoProjectPrefs.GetBool(PREF_SHOW_ONLY_INCLUDED, false);
        var last = DenEmoProjectPrefs.GetString(PREF_LAST_TARGET, string.Empty);
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
        
        // Register undo callback to sync blendValues when undo/redo occurs
        Undo.undoRedoPerformed += OnUndoRedo;

        // Initial status
        SetStatus(DenEmoLoc.T("status.ready"), StatusLevel.Info, 0);

        // Load collapsed groups per project
        LoadCollapsedGroupsPrefs();
    }

    void OnDisable()
    {
        // Unregister undo callback
        Undo.undoRedoPerformed -= OnUndoRedo;
        // Stop throttling update loop if active
        StopThrottleUpdate();
        
           // Save any pending include flags changes
           if (includeFlagsDirty)
           {
               SaveIncludeFlagsPrefsImmediate();
           }
       
        if (targetSkinnedMesh) DenEmoProjectPrefs.SetString(PREF_LAST_TARGET, targetSkinnedMesh.GetInstanceID().ToString());
        DenEmoProjectPrefs.SetString(PREF_SAVE_FOLDER, saveFolder);
        DenEmoProjectPrefs.SetString(PREF_SEARCH_TEXT, searchText);
        DenEmoProjectPrefs.SetBool(PREF_ALIGN_TO_CLIP, alignToExistingClipKeys);
        DenEmoProjectPrefs.SetBool(PREF_SHOW_ONLY_INCLUDED, showOnlyIncluded);
        // persist snapshot if exists
        if (snapshotValues != null && snapshotValues.Count > 0)
        {
            var parts = new string[snapshotValues.Count];
            for (int i = 0; i < snapshotValues.Count; i++) parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            DenEmoProjectPrefs.SetString(PREF_SNAPSHOT, string.Join(",", parts));
        }
        SaveBlendValuesPrefs();
        SaveCollapsedGroupsPrefs();
    }

        // Status helpers moved to State/Shapekey_animation_converter.State.Status.cs

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
        isVrcShapeCache.Clear();
        groupToIndices.Clear();
        groupOrder.Clear();
        groupSegments.Clear();
        if (targetSkinnedMesh == null || targetSkinnedMesh.sharedMesh == null) return;
        var mesh = targetSkinnedMesh.sharedMesh;
        int count = mesh.blendShapeCount;
        for (int i = 0; i < count; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            blendNames.Add(name);
            blendValues.Add(targetSkinnedMesh.GetBlendShapeWeight(i));
            includeFlags.Add(true); // default include
            isVrcShapeCache.Add(IsVrcShapeName(name)); // Cache VRC check
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
        // build groups/segments based on current blend names
        BuildGroups();
        // Mark filter cache as dirty
        filterCacheDirty = true;
    }

    // Snapshot and Pref helpers moved to State/Shapekey_animation_converter.State.Snapshot.cs and .Prefs.cs

    // Grouping: build groups based on first delimiter/CamelCase token
    void BuildGroups()
    {
        groupToIndices.Clear();
        groupOrder.Clear();
        groupSegments.Clear();

        // Build contiguous segments (excluding vrc.* names)
        string curKey = null;
        int segStart = -1;
        int segLen = 0;
        Action flush = () =>
        {
            if (segLen > 0)
            {
                var seg = new GroupSegment { key = curKey ?? "Other", start = segStart, length = segLen };
                groupSegments.Add(seg);
            }
            curKey = null; segStart = -1; segLen = 0;
        };

        for (int i = 0; i < blendNames.Count; i++)
        {
            var name = blendNames[i] ?? string.Empty;
               if (i < isVrcShapeCache.Count && isVrcShapeCache[i])
            {
                // break segment at vrc.*
                flush();
                continue;
            }
            string key = GetGroupKey(name);
            if (string.IsNullOrEmpty(key)) key = "Other";
            if (curKey == null)
            {
                curKey = key; segStart = i; segLen = 1;
            }
            else if (key == curKey)
            {
                segLen++;
            }
            else
            {
                flush();
                curKey = key; segStart = i; segLen = 1;
            }
        }
        flush();

        // Also populate name->indices and order for generic use if needed
        for (int s = 0; s < groupSegments.Count; s++)
        {
            var seg = groupSegments[s];
            if (!groupToIndices.TryGetValue(seg.key, out var list))
            {
                list = new List<int>();
                groupToIndices[seg.key] = list;
                groupOrder.Add(seg.key);
            }
            for (int i = 0; i < seg.length; i++) list.Add(seg.start + i);
        }
    }

    // GetGroupKey, IndexOfAny moved to State/Shapekey_animation_converter.State.Utils.cs

    // Undo/Redo handler moved to State/Shapekey_animation_converter.State.Undo.cs

    // Rebuild visible indices cache based on current filters
    void RebuildVisibleIndicesCache(string[] searchTokens)
    {
        visibleIndices.Clear();
        for (int i = 0; i < blendNames.Count; i++)
        {
            // Skip VRC shapes (cached)
            if (i < isVrcShapeCache.Count && isVrcShapeCache[i]) continue;
        
            // Check search filter
            if (!MatchesAllTokens(blendNames[i], searchTokens)) continue;
        
            // Check include filter
            if (showOnlyIncluded && !(i < includeFlags.Count && includeFlags[i])) continue;
        
            visibleIndices.Add(i);
        }
        filterCacheDirty = false;
        lastSearchText = searchText;
        lastShowOnlyIncluded = showOnlyIncluded;
    }
}

