using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    void DrawSnapshotAndSearch()
    {
        // Filter toggle
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        var newShowOnly = EditorGUILayout.ToggleLeft(new GUIContent(DenEmoLoc.T("ui.filter.showIncluded"), DenEmoLoc.T("ui.filter.showIncluded.tip")), showOnlyIncluded);
        if (newShowOnly != showOnlyIncluded)
        {
            showOnlyIncluded = newShowOnly;
            DenEmoProjectPrefs.SetBool("ShapekeyConverter_ShowOnlyIncluded", showOnlyIncluded);
        }
        EditorGUILayout.EndHorizontal();

        // Snapshot controls
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(DenEmoLoc.T("ui.snapshot.create"), GUILayout.Height(22)))
        {
            CreateSnapshot();
        }
        if (GUILayout.Button(DenEmoLoc.T("ui.snapshot.restore"), GUILayout.Height(22)))
        {
            RestoreSnapshot();
        }
        EditorGUILayout.EndHorizontal();

        // Search UI
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(DenEmoLoc.T("ui.section.search"), EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.SetNextControlName("SearchField");
        searchText = EditorGUILayout.TextField(searchText, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(DenEmoLoc.T("ui.search.clear"), GUILayout.Width(60)))
        {
            searchText = string.Empty;
            // Persist immediately (optional)
            DenEmoProjectPrefs.SetString("ShapekeyConverter_SearchText", searchText);
            // Remove focus so the TextField updates visually in the same repaint
            GUI.FocusControl(null);
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        // Build AND-search tokens
        var searchTokens = BuildSearchTokens(searchText);

        // Check if filter cache needs rebuild
        if (filterCacheDirty || searchText != lastSearchText || showOnlyIncluded != lastShowOnlyIncluded)
        {
            RebuildVisibleIndicesCache(searchTokens);
        }

        // Delayed save of include flags (after 0.5 seconds of inactivity)
        if (includeFlagsDirty && EditorApplication.timeSinceStartup - lastIncludeFlagsChangeTime > 0.5)
        {
            SaveIncludeFlagsPrefsImmediate();
        }
    }
}
