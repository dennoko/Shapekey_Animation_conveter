using System;
using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    // Draws Basic Settings block (mesh selection, align, apply). Returns false if early exit is needed.
    bool DrawBasicSettings()
    {
        // Basic Settings
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(DenEmoLoc.T("ui.section.basic"), EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // Mesh field
        EditorGUI.BeginChangeCheck();
        var newSmr = EditorGUILayout.ObjectField(new GUIContent(DenEmoLoc.T("ui.mesh.label"), DenEmoLoc.T("ui.mesh.tooltip")), targetSkinnedMesh, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        if (EditorGUI.EndChangeCheck())
        {
            // Stop any pending throttled applies from previous target
            StopThrottleUpdate();
            targetSkinnedMesh = newSmr;
            targetObject = targetSkinnedMesh ? targetSkinnedMesh.gameObject : null;
            RefreshBlendList();
            // 自動スナップショット（現在のメッシュ状態を初期値として保持）
            if (targetSkinnedMesh != null)
            {
                CreateSnapshot(loadTime: false); // 永続化も行う
            }
            // Reflect mesh -> tool (clarify with status)
            if (targetSkinnedMesh != null)
                SetStatus(DenEmoLoc.T("status.ready"), StatusLevel.Info, 0);
            Repaint();
        }

        if (targetSkinnedMesh == null)
        {
            EditorGUILayout.HelpBox(DenEmoLoc.T("ui.mesh.missing"), MessageType.Info);
            EditorGUILayout.EndVertical();
            return false;
        }

        // Warn if the selected mesh is inactive/disabled
        if (!targetSkinnedMesh.gameObject.activeInHierarchy || !targetSkinnedMesh.enabled)
        {
            EditorGUILayout.HelpBox(DenEmoLoc.T("ui.mesh.inactive.warn"), MessageType.Warning);
        }

        if (blendNames == null || blendNames.Count == 0)
        {
            EditorGUILayout.HelpBox(DenEmoLoc.T("ui.mesh.noShapes"), MessageType.Info);
            EditorGUILayout.EndVertical();
            return false;
        }

        // Align toggle (row 1)
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        alignToExistingClipKeys = EditorGUILayout.ToggleLeft(
            new GUIContent(
                DenEmoLoc.T("ui.align.toggle"),
                DenEmoLoc.T("ui.align.toggle.tip")
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
                    DenEmoLoc.T("ui.align.base.label"),
                    DenEmoLoc.T("ui.align.base.tip")
                ),
                GUILayout.Width(110)
            );
            baseAlignClip = EditorGUILayout.ObjectField(GUIContent.none, baseAlignClip, typeof(AnimationClip), false) as AnimationClip;
            using (new EditorGUI.DisabledGroupScope(baseAlignClip == null))
            {
                if (GUILayout.Button(new GUIContent(DenEmoLoc.T("ui.align.apply.button"), DenEmoLoc.T("ui.align.apply.tip")), GUILayout.Width(60)))
                {
                    // Build set of (path, shape) pairs from the base clip
                    var pairs = new System.Collections.Generic.HashSet<string>();
                    string currentSmrPath = GetRelativePath(targetSkinnedMesh.transform, targetSkinnedMesh.transform.root);
                    foreach (var b in AnimationUtility.GetCurveBindings(baseAlignClip))
                    {
                        if (b.type != typeof(SkinnedMeshRenderer)) continue;
                        if (!b.propertyName.StartsWith("blendShape.")) continue;
                        var shape = b.propertyName.Substring("blendShape.".Length);
                        if (string.IsNullOrEmpty(shape)) continue;
                        // Only consider bindings that match the current SMR path
                        if (string.Equals(b.path, currentSmrPath, StringComparison.Ordinal))
                        {
                            pairs.Add(currentSmrPath + "\n" + shape);
                        }
                    }

                    for (int i = 0; i < blendNames.Count; i++)
                    {
                        if (IsVrcShapeName(blendNames[i])) { includeFlags[i] = false; continue; }
                        bool match = pairs.Contains(currentSmrPath + "\n" + blendNames[i]);
                        includeFlags[i] = match;
                    }
                    filterCacheDirty = true;
                    SaveIncludeFlagsPrefs();
                    SetStatus(DenEmoLoc.T("status.alignedSavedTargets"), StatusLevel.Success);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // Apply animation row
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            new GUIContent(
                DenEmoLoc.T("ui.applyAnim.label"),
                DenEmoLoc.T("ui.applyAnim.tip")
            ),
            GUILayout.Width(120)
        );
        loadedClip = EditorGUILayout.ObjectField(GUIContent.none, loadedClip, typeof(AnimationClip), false) as AnimationClip;
        using (new EditorGUI.DisabledGroupScope(loadedClip == null))
        {
            if (GUILayout.Button(new GUIContent(DenEmoLoc.T("ui.applyAnim.button"), DenEmoLoc.T("ui.applyAnim.button.tip")), GUILayout.Width(60)))
            {
                SetStatus(DenEmoLoc.T("status.applying"), StatusLevel.Info, 0);
                ApplyAnimationToMesh(loadedClip);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        return true;
    }
}
