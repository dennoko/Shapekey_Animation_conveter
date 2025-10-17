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

        // Direct assignment fields
        EditorGUILayout.LabelField("対象（直接割り当てかドラッグ＆ドロップ）:");
        EditorGUI.BeginChangeCheck();
        var newObj = EditorGUILayout.ObjectField("GameObject", targetObject, typeof(GameObject), true) as GameObject;
        var newSmr = EditorGUILayout.ObjectField("SkinnedMeshRenderer", targetSkinnedMesh, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        if (EditorGUI.EndChangeCheck())
        {
            if (newSmr != null)
            {
                targetSkinnedMesh = newSmr;
                targetObject = newSmr.gameObject;
            }
            else if (newObj != null)
            {
                targetObject = newObj;
                targetSkinnedMesh = targetObject.GetComponentInChildren<SkinnedMeshRenderer>();
            }
            else
            {
                targetObject = null;
                targetSkinnedMesh = null;
            }
            RefreshBlendList();
            Repaint();
        }

        EditorGUILayout.LabelField("または下に SkinnedMeshRenderer を含む GameObject をドラッグしてください:");
        var rect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(rect, targetObject ? targetObject.name : "Drag target here");
        HandleDragAndDrop(rect);

        if (targetSkinnedMesh == null)
        {
            if (targetObject)
            {
                EditorGUILayout.HelpBox("選択したオブジェクトに SkinnedMeshRenderer が見つかりません。SkinnedMeshRenderer を含む GameObject を渡してください。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("対象が選択されていません。", MessageType.Info);
            }
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
            for (int i = 0; i < blendNames.Count; i++)
            {
                // Always hide VRChat control shapekeys from the list without altering indices
                if (IsVrcShapeName(blendNames[i])) continue;
                // filter by search
                if (!string.IsNullOrEmpty(searchText))
                {
                    var name = blendNames[i] ?? string.Empty;
                    bool ok = searchMode == SearchMode.Prefix ? name.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) : name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!ok) continue;
                }
                float v = EditorGUILayout.Slider(blendNames[i], blendValues[i], 0f, 100f);
                if (Math.Abs(v - blendValues[i]) > 0.0001f)
                {
                    blendValues[i] = v;
                    if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, v);
                    SaveBlendValuesPrefs();
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
                        if (obj is GameObject go)
                        {
                            targetObject = go;
                            RefreshTargetFromObject();
                            Repaint();
                            break;
                        }
                        else if (obj is SkinnedMeshRenderer smr)
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
