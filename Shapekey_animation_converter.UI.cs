// UI partial for Shapekey_animation_converter
// - OnGUI layout and interactions
// - Drag & drop handling

using System;
using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    void OnGUI()
    {
        EditorGUILayout.LabelField("DenEmo", EditorStyles.boldLabel);
        EditorGUILayout.Space();

    // Basic Settings
    EditorGUILayout.Space();
        EditorGUILayout.LabelField("基本設定", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // Mesh field
        EditorGUI.BeginChangeCheck();
        var newSmr = EditorGUILayout.ObjectField(new GUIContent("メッシュ", "SkinnedMeshRenderer コンポーネントを指定します。"), targetSkinnedMesh, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        if (EditorGUI.EndChangeCheck())
        {
            targetSkinnedMesh = newSmr;
            targetObject = targetSkinnedMesh ? targetSkinnedMesh.gameObject : null;
            RefreshBlendList();
            Repaint();
        }

        if (targetSkinnedMesh == null)
        {
            EditorGUILayout.HelpBox("対象のメッシュが選択されていません。SkinnedMeshRenderer を指定してください。", MessageType.Info);
            return;
        }

        if (blendNames == null || blendNames.Count == 0)
        {
            EditorGUILayout.HelpBox("このメッシュにはシェイプキーがありません。", MessageType.Info);
            return;
        }

        // Align toggle (row 1)
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        alignToExistingClipKeys = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "保存するキーを既存のアニメーションに揃える",
                "保存時、ここで指定したベースアニメーションに含まれるブレンドシェイプのキーだけを書き出します。未選択時は有効な全シェイプを保存します。"
            ),
            alignToExistingClipKeys
        );
        EditorGUILayout.EndHorizontal();

        // Base clip + apply (row 2)
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledGroupScope(!alignToExistingClipKeys))
        {
            EditorGUILayout.LabelField(
                new GUIContent(
                    "ベースアニメーション",
                    "保存対象のシェイプを選別するために参照するAnimationClipです。『適用』を押すと、このクリップに含まれるブレンドシェイプのみを保存対象に切り替えます。"
                ),
                GUILayout.Width(110)
            );
            baseAlignClip = EditorGUILayout.ObjectField(GUIContent.none, baseAlignClip, typeof(AnimationClip), false) as AnimationClip;
            using (new EditorGUI.DisabledGroupScope(baseAlignClip == null))
            {
                if (GUILayout.Button(new GUIContent("適用", "ベースアニメーションに含まれるブレンドシェイプのみ保存対象（チェック）にします。vrc.* 系は除外されます。"), GUILayout.Width(60)))
                {
                    var names = new System.Collections.Generic.HashSet<string>();
                    foreach (var b in AnimationUtility.GetCurveBindings(baseAlignClip))
                    {
                        if (b.type != typeof(SkinnedMeshRenderer)) continue;
                        if (!b.propertyName.StartsWith("blendShape.")) continue;
                        var shape = b.propertyName.Substring("blendShape.".Length);
                        if (!string.IsNullOrEmpty(shape)) names.Add(shape);
                    }
                    for (int i = 0; i < blendNames.Count; i++)
                    {
                        if (IsVrcShapeName(blendNames[i])) { includeFlags[i] = false; continue; }
                        includeFlags[i] = names.Contains(blendNames[i]);
                    }
                    SaveIncludeFlagsPrefs();
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // Apply animation row
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            new GUIContent(
                "アニメーションを適用",
                "選択したアニメーションクリップのブレンドシェイプ値（時刻0秒）を現在のメッシュに反映します。"
            ),
            GUILayout.Width(120)
        );
        loadedClip = EditorGUILayout.ObjectField(GUIContent.none, loadedClip, typeof(AnimationClip), false) as AnimationClip;
        using (new EditorGUI.DisabledGroupScope(loadedClip == null))
        {
            if (GUILayout.Button(new GUIContent("適用", "アニメーションの値をメッシュへ反映します（一致するシェイプのみ）。Undo対応。"), GUILayout.Width(60)))
            {
                ApplyAnimationToMesh(loadedClip);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // Filter toggle
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        var newShowOnly = EditorGUILayout.ToggleLeft(new GUIContent("有効なシェイプのみ表示", "チェックが入っている（保存対象の）シェイプだけを一覧に表示します。"), showOnlyIncluded);
        if (newShowOnly != showOnlyIncluded)
        {
            showOnlyIncluded = newShowOnly;
            EditorPrefs.SetBool("ShapekeyConverter_ShowOnlyIncluded", showOnlyIncluded);
        }
        EditorGUILayout.EndHorizontal();

        // Snapshot controls
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("一時保存（スナップショット）", GUILayout.Height(22)))
        {
            CreateSnapshot();
        }
        if (GUILayout.Button("スナップショットにリセット", GUILayout.Height(22)))
        {
            RestoreSnapshot();
        }
        EditorGUILayout.EndHorizontal();

    // Search UI
    EditorGUILayout.Space();
        EditorGUILayout.LabelField("シェイプキー検索", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.SetNextControlName("SearchField");
        searchText = EditorGUILayout.TextField(searchText, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("クリア", GUILayout.Width(60)))
        {
            searchText = string.Empty;
            // Persist immediately (optional)
            EditorPrefs.SetString("ShapekeyConverter_SearchText", searchText);
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

        // List
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int s = 0; s < groupSegments.Count; s++)
        {
            var seg = groupSegments[s];
            int start = seg.start;
            int end = seg.start + seg.length;

            int enabledCount = 0;
            int visibleCount = 0;
               // Use cached visible indices for counting
               foreach (int i in visibleIndices)
            {
                   if (i < start || i >= end) continue; // Only count indices in this segment
                visibleCount++;
                if (i < includeFlags.Count && includeFlags[i]) enabledCount++;
            }

            bool treatAsGroup = seg.length > 3;
            if (treatAsGroup && visibleCount > 0)
            {
                bool groupAllOn = enabledCount == visibleCount && visibleCount > 0;
                bool groupAllOff = enabledCount == 0;
                bool newGroupVal = groupAllOn;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                newGroupVal = EditorGUILayout.Toggle(newGroupVal, GUILayout.Width(18));
                using (new EditorGUI.DisabledGroupScope(true))
                {
                    string suffix = groupAllOn ? "(全選択)" : groupAllOff ? "(全解除)" : "(一部)";
                    EditorGUILayout.LabelField($"{seg.key}  {suffix}  [{enabledCount}/{visibleCount}]", EditorStyles.boldLabel);
                }
                EditorGUILayout.EndHorizontal();
                if (newGroupVal != groupAllOn)
                {
                    for (int i = start; i < end; i++)
                    {
                           if (i < isVrcShapeCache.Count && isVrcShapeCache[i]) continue;
                        includeFlags[i] = newGroupVal;
                    }
                       filterCacheDirty = true; // Mark cache dirty when include flags change
                    SaveIncludeFlagsPrefs();
                }
            }

               // Use cached visible indices for rendering
               foreach (int i in visibleIndices)
            {
                   if (i < start || i >= end) continue; // Only render indices in this segment
               
                EditorGUILayout.BeginHorizontal();
                if (treatAsGroup) GUILayout.Space(24);
                bool newInc = EditorGUILayout.Toggle(includeFlags[i], GUILayout.Width(18));
                   if (newInc != includeFlags[i])
                   {
                       includeFlags[i] = newInc;
                       filterCacheDirty = true; // Mark cache dirty
                       SaveIncludeFlagsPrefs();
                   }
                
                // Slider with Undo support
                float oldValue = blendValues[i];
                
                // Get control ID before the slider
                int sliderId = GUIUtility.GetControlID(FocusType.Passive);
                
                EditorGUI.BeginChangeCheck();
                float newValue = EditorGUILayout.Slider(blendNames[i], oldValue, 0f, 100f);
                bool valueChanged = EditorGUI.EndChangeCheck();
                
                // Check if this slider is currently being interacted with
                bool isThisSliderHot = (GUIUtility.hotControl == sliderId || GUIUtility.hotControl == sliderId + 1);
                
                // Record undo at the start of interaction (first change while not dragging)
                if (valueChanged && isThisSliderHot && (!isSliderDragging || currentDraggingIndex != i))
                {
                    if (targetSkinnedMesh != null)
                    {
                        Undo.RecordObject(targetSkinnedMesh, "Change Shape Key Value");
                    }
                    isSliderDragging = true;
                    currentDraggingIndex = i;
                }
                
                // Apply value change immediately
                if (valueChanged)
                {
                    // If changed but no hot control, it's a direct input (not drag) - record undo
                    if (!isThisSliderHot && !isSliderDragging && targetSkinnedMesh != null)
                    {
                        Undo.RecordObject(targetSkinnedMesh, "Change Shape Key Value");
                    }
                    
                    blendValues[i] = newValue;
                    if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, newValue);
                }
                
                // Detect end of drag - when this slider was hot but now isn't
                if (isSliderDragging && currentDraggingIndex == i && !isThisSliderHot)
                {
                    isSliderDragging = false;
                    currentDraggingIndex = -1;
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Animation", GUILayout.Height(30)))
        {
            SaveAnimationClip();
        }
        if (GUILayout.Button("Refresh", GUILayout.Width(80), GUILayout.Height(30)))
        {
            RefreshTargetFromObject();
        }
        EditorGUILayout.EndHorizontal();

    EditorGUILayout.Space();
    EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("保存先 (既定):", GUILayout.Width(100));
        saveFolder = EditorGUILayout.TextField(saveFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(80)))
        {
            var newPath = EditorUtility.OpenFolderPanel("フォルダを選択", Application.dataPath, "");
            if (!string.IsNullOrEmpty(newPath))
            {
                if (newPath.StartsWith(Application.dataPath))
                    saveFolder = "Assets" + newPath.Substring(Application.dataPath.Length);
                else
                    saveFolder = newPath;
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // Helper methods for AND search
    static string[] BuildSearchTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        var tokens = text.Split(new char[] { ' ', '\t', '\u3000' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens;
    }

    static bool MatchesAllTokens(string name, string[] tokens)
    {
        if (tokens == null || tokens.Length == 0) return true;
           if (string.IsNullOrEmpty(name)) return false;
       
           // Convert to lower case once for performance
           var nmLower = name.ToLowerInvariant();
       
        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (string.IsNullOrEmpty(t)) continue;
           
               // Use pre-lowercased string for faster comparison
               if (nmLower.IndexOf(t.ToLowerInvariant(), StringComparison.Ordinal) < 0) return false;
        }
        return true;
    }

    void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition)) return;
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is SkinnedMeshRenderer smr)
                        {
                            targetSkinnedMesh = smr;
                            targetObject = smr.gameObject;
                            RefreshBlendList();
                            Repaint();
                            break;
                        }
                    }
                }
                evt.Use();
                break;
        }
    }
}
