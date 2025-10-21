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
    if (targetSkinnedMesh == null) { SetStatus(DenEmoLoc.T("dlg.apply.noTarget"), StatusLevel.Error); return; }

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

    // Always exclude VRChat control shapekeys and lip-sync visemes from saving
    includeIndices.RemoveAll(idx => IsVrcShapeName(blendNames[idx]) || (idx < isLipSyncShapeCache.Count && isLipSyncShapeCache[idx]));

        // 検索一致による保存除外機能は削除しました

        // Align option no longer forces inclusion; use baseAlignClip only for path reuse and via the UI button

        // Resolve path to the current SMR from the scene root to ensure path is embedded in saved clip
        string currentSmrPath = GetRelativePath(targetSkinnedMesh.transform, targetSkinnedMesh.transform.root);

        foreach (var i in includeIndices)
        {
            float current = targetSkinnedMesh.GetBlendShapeWeight(i);

            string prop = "blendShape." + blendNames[i];
            var binding = new EditorCurveBinding();
            binding.type = typeof(SkinnedMeshRenderer);
            // Always prefer binding to the currently selected SMR path
            binding.path = currentSmrPath;
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
        SetStatus(DenEmoLoc.Tf("dlg.save.done.msg", path), StatusLevel.Success);
    }

    void ApplyAnimationToMesh(AnimationClip clip)
    {
        if (clip == null) { SetStatus(DenEmoLoc.T("dlg.apply.noClip"), StatusLevel.Error); return; }
        if (targetSkinnedMesh == null) { SetStatus(DenEmoLoc.T("dlg.apply.noTarget"), StatusLevel.Error); return; }

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
            SetStatus(DenEmoLoc.T("dlg.apply.done.msg"), StatusLevel.Success);
        }
        else
        {
            SetStatus(DenEmoLoc.T("dlg.apply.noneFound"), StatusLevel.Warning);
        }
    }
}
