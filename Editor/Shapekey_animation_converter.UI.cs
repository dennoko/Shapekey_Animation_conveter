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
        EditorGUILayout.LabelField("ブレンドシェイプ → アニメーション変換ツール", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("保存フォルダ:", GUILayout.Width(80));
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

        EditorGUILayout.Space();

        // Direct assignment fields (Mesh only)
        EditorGUILayout.LabelField("対象（直接割り当てかドラッグ＆ドロップ）:");
        EditorGUI.BeginChangeCheck();
        var newSmr = EditorGUILayout.ObjectField(new GUIContent("メッシュ", "SkinnedMeshRenderer コンポーネントを指定します。"), targetSkinnedMesh, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        if (EditorGUI.EndChangeCheck())
        {
            targetSkinnedMesh = newSmr;
            targetObject = targetSkinnedMesh ? targetSkinnedMesh.gameObject : null;
            RefreshBlendList();
            Repaint();
        }

        EditorGUILayout.LabelField("または下にメッシュをドラッグしてください:");
        var rect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(rect, targetObject ? targetObject.name : "Drag target here");
        HandleDragAndDrop(rect);

        if (targetSkinnedMesh == null)
        {
            EditorGUILayout.HelpBox("対象のメッシュが選択されていません。SkinnedMeshRenderer を指定してください。", MessageType.Info);
        }

        if (blendNames.Count > 0)
        {
            // Alignment option
            EditorGUILayout.BeginHorizontal();
            alignToExistingClipKeys = EditorGUILayout.ToggleLeft("保存するキーを既存アニメーションに揃える", alignToExistingClipKeys);
            EditorGUILayout.EndHorizontal();
            if (alignToExistingClipKeys)
            {
                baseAlignClip = EditorGUILayout.ObjectField("ベース(揃え用)アニメーション", baseAlignClip, typeof(AnimationClip), false) as AnimationClip;
                if (baseAlignClip == null)
                {
                    EditorGUILayout.HelpBox("ベースアニメーションを選択すると、そのアニメに含まれるブレンドシェイプのみを書き出します。", MessageType.Info);
                }
                else
                {
                    if (GUILayout.Button("ベースアニメのキーを反映", GUILayout.Height(22)))
                    {
                        // Build set of names included in baseAlignClip
                        var names = new System.Collections.Generic.HashSet<string>();
                        foreach (var b in AnimationUtility.GetCurveBindings(baseAlignClip))
                        {
                            if (b.type != typeof(SkinnedMeshRenderer)) continue;
                            if (!b.propertyName.StartsWith("blendShape.")) continue;
                            var shape = b.propertyName.Substring("blendShape.".Length);
                            if (!string.IsNullOrEmpty(shape)) names.Add(shape);
                        }
                        // Apply to includeFlags: included => true; others => false, but keep vrc.* always false
                        for (int i = 0; i < blendNames.Count; i++)
                        {
                            if (IsVrcShapeName(blendNames[i])) { includeFlags[i] = false; continue; }
                            includeFlags[i] = names.Contains(blendNames[i]);
                        }
                        SaveIncludeFlagsPrefs();
                    }
                }
            }

            // 値0除外機能はオミットしました

            // 検索 UI
            EditorGUILayout.BeginHorizontal();
            searchText = EditorGUILayout.TextField(searchText, GUILayout.ExpandWidth(true));
            searchMode = (SearchMode)EditorGUILayout.EnumPopup(searchMode, GUILayout.Width(100));
            if (GUILayout.Button("クリア", GUILayout.Width(60))) { searchText = string.Empty; }
            EditorGUILayout.EndHorizontal();

            // 検索に一致しないキーの保存除外オプションは削除しました

            EditorGUILayout.LabelField("シェイプ一覧", EditorStyles.boldLabel);

            // Snapshot / Reset buttons (スクロール外)
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

            // Animation clip loader / applier
            EditorGUILayout.BeginHorizontal();
            loadedClip = EditorGUILayout.ObjectField("適用用アニメーション (Apply)", loadedClip, typeof(AnimationClip), false) as AnimationClip;
            if (GUILayout.Button("アニメーションを適用", GUILayout.Width(120)))
            {
                ApplyAnimationToMesh(loadedClip);
            }
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            // Render by contiguous segments to preserve original order
            for (int s = 0; s < groupSegments.Count; s++)
            {
                var seg = groupSegments[s];
                int start = seg.start;
                int end = seg.start + seg.length; // exclusive

                // Count visible items in this segment (respect search)
                int enabledCount = 0;
                int visibleCount = 0;
                for (int i = start; i < end; i++)
                {
                    if (IsVrcShapeName(blendNames[i])) continue; // safety
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        var nm = blendNames[i] ?? string.Empty;
                        bool ok = searchMode == SearchMode.Prefix ? nm.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) : nm.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!ok) continue;
                    }
                    visibleCount++;
                    if (i < includeFlags.Count && includeFlags[i]) enabledCount++;
                }

                bool treatAsGroup = seg.length > 5; // 指定数以下はグループ化しない
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
                            if (IsVrcShapeName(blendNames[i])) continue;
                            if (!string.IsNullOrEmpty(searchText))
                            {
                                var nm = blendNames[i] ?? string.Empty;
                                bool ok = searchMode == SearchMode.Prefix ? nm.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) : nm.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                                if (!ok) continue;
                            }
                            includeFlags[i] = newGroupVal;
                        }
                        SaveIncludeFlagsPrefs();
                    }
                }

                // Render items in this segment (preserving original order)
                for (int i = start; i < end; i++)
                {
                    if (IsVrcShapeName(blendNames[i])) continue;
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        var nm = blendNames[i] ?? string.Empty;
                        bool ok = searchMode == SearchMode.Prefix ? nm.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) : nm.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!ok) continue;
                    }
                    EditorGUILayout.BeginHorizontal();
                    bool newInc = EditorGUILayout.Toggle(includeFlags[i], GUILayout.Width(18));
                    if (newInc != includeFlags[i]) { includeFlags[i] = newInc; SaveIncludeFlagsPrefs(); }
                    float v = EditorGUILayout.Slider(blendNames[i], blendValues[i], 0f, 100f);
                    EditorGUILayout.EndHorizontal();
                    if (Math.Abs(v - blendValues[i]) > 0.0001f)
                    {
                        blendValues[i] = v;
                        if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, v);
                        SaveBlendValuesPrefs();
                    }
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
        }
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
