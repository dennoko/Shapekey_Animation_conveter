// Animation partial for Shapekey_animation_converter
// - SaveAnimationClip and ApplyAnimationToMesh
// - Respects options: align to existing clip keys, exclude non-search matches, exclude zero

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    void SaveAnimationClip()
    {
    if (targetSkinnedMesh == null) { EditorUtility.DisplayDialog(DenEmoLoc.T("dlg.error"), DenEmoLoc.T("dlg.apply.noTarget"), DenEmoLoc.T("dlg.ok")); return; }

        if (!Directory.Exists(saveFolder))
        {
            try { Directory.CreateDirectory(saveFolder); } catch { }
        }

    string defaultName = targetObject ? targetObject.name + "_blendshape" : DenEmoLoc.T("save.panel.defaultName");
    string path = EditorUtility.SaveFilePanelInProject(DenEmoLoc.T("save.panel.title"), defaultName + ".anim", "anim", DenEmoLoc.T("save.panel.hint"), saveFolder);
        if (string.IsNullOrEmpty(path)) return;

        var clip = new AnimationClip();
        clip.frameRate = 60;

        // Determine indices to include based on per-shape flags
        var includeIndices = new List<int>();
        for (int i = 0; i < blendNames.Count; i++)
        {
            if (i < includeFlags.Count && includeFlags[i]) includeIndices.Add(i);
        }

        // Always exclude VRChat control shapekeys from saving
        includeIndices.RemoveAll(idx => IsVrcShapeName(blendNames[idx]));

        // 検索一致による保存除外機能は削除しました

        // Align option no longer forces inclusion; use baseAlignClip only for path reuse and via the UI button

        // Optional: map shape -> paths from existing clip to reuse binding.path
        Dictionary<string, List<string>> shapeToPaths = null;
        string currentSmrPath = GetRelativePath(targetSkinnedMesh.transform, targetObject.transform);
        if (alignToExistingClipKeys && baseAlignClip != null)
        {
            shapeToPaths = new Dictionary<string, List<string>>();
            foreach (var b in AnimationUtility.GetCurveBindings(baseAlignClip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;
                var shape = b.propertyName.Substring("blendShape.".Length);
                if (string.IsNullOrEmpty(shape)) continue;
                if (!shapeToPaths.TryGetValue(shape, out var list))
                {
                    list = new List<string>();
                    shapeToPaths[shape] = list;
                }
                if (!list.Contains(b.path)) list.Add(b.path);
            }
        }

        foreach (var i in includeIndices)
        {
            float current = targetSkinnedMesh.GetBlendShapeWeight(i);

            string prop = "blendShape." + blendNames[i];
            var binding = new EditorCurveBinding();
            binding.type = typeof(SkinnedMeshRenderer);
            if (shapeToPaths != null && shapeToPaths.TryGetValue(blendNames[i], out var paths) && paths.Count > 0)
            {
                string chosen = paths.Contains(currentSmrPath) ? currentSmrPath : paths[0];
                binding.path = chosen ?? string.Empty;
            }
            else
            {
                binding.path = currentSmrPath;
            }
            binding.propertyName = prop;

            var key = new Keyframe[2];
            key[0] = new Keyframe(0f, current);
            key[1] = new Keyframe(0.0001f, current);
            var curve = new AnimationCurve(key);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();
        // Show the saved asset in the Project window
        var asset = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (asset != null)
        {
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
        EditorUtility.DisplayDialog(DenEmoLoc.T("dlg.save.done.title"), DenEmoLoc.Tf("dlg.save.done.msg", path), DenEmoLoc.T("dlg.ok"));
    }

    void ApplyAnimationToMesh(AnimationClip clip)
    {
        if (clip == null)
        {
            EditorUtility.DisplayDialog(DenEmoLoc.T("dlg.error"), DenEmoLoc.T("dlg.apply.noClip"), DenEmoLoc.T("dlg.ok"));
            return;
        }
        if (targetSkinnedMesh == null)
        {
            EditorUtility.DisplayDialog(DenEmoLoc.T("dlg.error"), DenEmoLoc.T("dlg.apply.noTarget"), DenEmoLoc.T("dlg.ok"));
            return;
        }

        var bindings = AnimationUtility.GetCurveBindings(clip);
        bool applied = false;
        foreach (var b in bindings)
        {
            if (b.type != typeof(SkinnedMeshRenderer)) continue;
            if (!b.propertyName.StartsWith("blendShape.")) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            float value = curve.Evaluate(0f);
            string shapeName = b.propertyName.Substring("blendShape.".Length);
            int idx = targetSkinnedMesh.sharedMesh.GetBlendShapeIndex(shapeName);
            if (idx >= 0)
            {
                targetSkinnedMesh.SetBlendShapeWeight(idx, value);
                if (idx < blendValues.Count) blendValues[idx] = value;
                applied = true;
            }
        }
        if (applied)
        {
            SaveBlendValuesPrefs();
            EditorUtility.DisplayDialog(DenEmoLoc.T("dlg.apply.done.title"), DenEmoLoc.T("dlg.apply.done.msg"), DenEmoLoc.T("dlg.ok"));
        }
        else
        {
            EditorUtility.DisplayDialog(DenEmoLoc.T("dlg.info"), DenEmoLoc.T("dlg.apply.noneFound"), DenEmoLoc.T("dlg.ok"));
        }
    }
}
