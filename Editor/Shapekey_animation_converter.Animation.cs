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
        if (targetSkinnedMesh == null) { EditorUtility.DisplayDialog("Error", "No target SkinnedMeshRenderer selected.", "OK"); return; }

        if (!Directory.Exists(saveFolder))
        {
            try { Directory.CreateDirectory(saveFolder); } catch { }
        }

        string defaultName = targetObject ? targetObject.name + "_blendshape" : "blendshape_anim";
        string path = EditorUtility.SaveFilePanelInProject("Save Animation", defaultName + ".anim", "anim", "Save generated animation", saveFolder);
        if (string.IsNullOrEmpty(path)) return;

        var clip = new AnimationClip();
        clip.frameRate = 60;

        // Determine indices to include based on options
        var includeIndices = new List<int>();
        for (int i = 0; i < blendNames.Count; i++) includeIndices.Add(i);

        // Always exclude VRChat control shapekeys from saving
        includeIndices.RemoveAll(idx => IsVrcShapeName(blendNames[idx]));

        // Filter by search
        if (excludeNonSearchMatches && !string.IsNullOrEmpty(searchText))
        {
            Predicate<string> match = (name) =>
            {
                if (string.IsNullOrEmpty(searchText)) return true;
                return searchMode == SearchMode.Prefix
                    ? name.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)
                    : name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            includeIndices.RemoveAll(idx => !match(blendNames[idx] ?? string.Empty));
        }

        // Align to existing clip key set
        if (alignToExistingClipKeys)
        {
            if (baseAlignClip == null)
            {
                EditorUtility.DisplayDialog("警告", "既存アニメーションに揃えるオプションが有効ですが、ベースアニメーションクリップが選択されていません。全てのキーを対象にします。", "OK");
            }
            else
            {
                var clipBindings = AnimationUtility.GetCurveBindings(baseAlignClip);
                var namesInClip = new HashSet<string>();
                foreach (var b in clipBindings)
                {
                    if (b.type != typeof(SkinnedMeshRenderer)) continue;
                    if (!b.propertyName.StartsWith("blendShape.")) continue;
                    string shapeName = b.propertyName.Substring("blendShape.".Length);
                    if (!string.IsNullOrEmpty(shapeName)) namesInClip.Add(shapeName);
                }
                includeIndices.RemoveAll(idx => !namesInClip.Contains(blendNames[idx]));
            }
        }

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
        EditorUtility.DisplayDialog("保存完了", "アニメーションを保存しました: " + path, "OK");
    }

    void ApplyAnimationToMesh(AnimationClip clip)
    {
        if (clip == null)
        {
            EditorUtility.DisplayDialog("エラー", "アニメーションが選択されていません。", "OK");
            return;
        }
        if (targetSkinnedMesh == null)
        {
            EditorUtility.DisplayDialog("エラー", "対象の SkinnedMeshRenderer が選択されていません。", "OK");
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
            EditorUtility.DisplayDialog("適用完了", "アニメーションのシェイプキー値をメッシュに適用しました。", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("情報", "アニメーションに適用できるブレンドシェイプが見つかりませんでした。", "OK");
        }
    }
}
