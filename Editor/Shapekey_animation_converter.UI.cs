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

        // メッシュ入力を最上段へ配置

        // Direct assignment fields (Mesh only)
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
        }

        if (blendNames.Count > 0)
        {
            // 基本設定グループ
            EditorGUILayout.LabelField("基本設定", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            // 1) 既存アニメに揃える（行1: トグルのみ）
            EditorGUILayout.BeginHorizontal();
            alignToExistingClipKeys = EditorGUILayout.ToggleLeft("保存するキーを既存のアニメーションに揃える", alignToExistingClipKeys);
            EditorGUILayout.EndHorizontal();

            // 1b) ベースアニメーション + 適用（行2: 入力欄＋ボタン）
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledGroupScope(!alignToExistingClipKeys))
            {
                EditorGUILayout.LabelField("ベースアニメーション", GUILayout.Width(110));
                baseAlignClip = EditorGUILayout.ObjectField(GUIContent.none, baseAlignClip, typeof(AnimationClip), false) as AnimationClip;
                using (new EditorGUI.DisabledGroupScope(baseAlignClip == null))
                {
                    if (GUILayout.Button("適用", GUILayout.Width(60)))
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

            // 2) アニメーションを適用 + 入力欄 + 適用（横並び）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("アニメーションを適用", GUILayout.Width(120));
            loadedClip = EditorGUILayout.ObjectField(GUIContent.none, loadedClip, typeof(AnimationClip), false) as AnimationClip;
            using (new EditorGUI.DisabledGroupScope(loadedClip == null))
            {
                if (GUILayout.Button("適用", GUILayout.Width(60)))
                {
                    ApplyAnimationToMesh(loadedClip);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // 表示フィルタ: 含まれる（チェック済み）のみ表示
            EditorGUILayout.BeginHorizontal();
            var newShowOnly = EditorGUILayout.ToggleLeft(new GUIContent("有効なシェイプのみ表示", "チェックが入っている（保存対象の）シェイプだけを一覧に表示します。"), showOnlyIncluded);
            if (newShowOnly != showOnlyIncluded)
            {
                showOnlyIncluded = newShowOnly;
                // すぐに保持したい場合はPrefsへも反映
                EditorPrefs.SetBool("ShapekeyConverter_ShowOnlyIncluded", showOnlyIncluded);
            }
            EditorGUILayout.EndHorizontal();

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

            // 検索 UI
            EditorGUILayout.BeginHorizontal();
            searchText = EditorGUILayout.TextField(searchText, GUILayout.ExpandWidth(true));
            searchMode = (SearchMode)EditorGUILayout.EnumPopup(searchMode, GUILayout.Width(100));
            if (GUILayout.Button("クリア", GUILayout.Width(60))) { searchText = string.Empty; }
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
                    if (showOnlyIncluded && !(i < includeFlags.Count && includeFlags[i])) continue;
                    visibleCount++;
                    if (i < includeFlags.Count && includeFlags[i]) enabledCount++;
                }

                bool treatAsGroup = seg.length > 3; // 指定数以下はグループ化しない
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
                            // グループ操作はフィルタに関わらずグループ全体へ適用（隠れている要素も対象）
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
                    if (showOnlyIncluded && !(i < includeFlags.Count && includeFlags[i])) continue;
                    EditorGUILayout.BeginHorizontal();
                    // If this segment is treated as a group, add a small left indent for its items
                    if (treatAsGroup) GUILayout.Space(24);
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

            // 保存先（既定）を Save の直下に配置。保存時に意識しやすく、通常時は邪魔にならない。
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
