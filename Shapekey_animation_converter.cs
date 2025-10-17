using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class Shapekey_animation_converter : EditorWindow
{
    const string PREF_SAVE_FOLDER = "ShapekeyConverter_SaveFolder";
    const string PREF_LAST_TARGET = "ShapekeyConverter_LastTarget";
    const string PREF_EXCLUDE_ZERO = "ShapekeyConverter_ExcludeZero";
    const string PREF_SEARCH_MODE = "ShapekeyConverter_SearchMode";
    const string PREF_SEARCH_TEXT = "ShapekeyConverter_SearchText";
    const string PREF_SNAPSHOT = "ShapekeyConverter_Snapshot";
    const string PREF_ALIGN_TO_CLIP = "ShapekeyConverter_AlignToClip";
    const string PREF_EXCLUDE_NON_SEARCH = "ShapekeyConverter_ExcludeNonSearch";

    string saveFolder = "Assets/Generated_Animations";
    GameObject targetObject;
    SkinnedMeshRenderer targetSkinnedMesh;
    Vector2 scroll;
    List<string> blendNames = new List<string>();
    List<float> blendValues = new List<float>();
    bool excludeZero = false;
    string searchText = string.Empty;
    enum SearchMode { Prefix = 0, Contains = 1 }
    SearchMode searchMode = SearchMode.Prefix;
    List<float> snapshotValues = null;
    AnimationClip loadedClip = null;
    // New options
    bool alignToExistingClipKeys = false; // When saving, include only keys found in loadedClip; disables excludeZero
    bool excludeNonSearchMatches = false; // When saving, exclude keys that do not match the current search filter

    [MenuItem("Tools/Blendshape -> Animation Converter")]
    public static void ShowWindow()
    {
    var w = GetWindow<Shapekey_animation_converter>("ブレンドシェイプ変換");
        w.minSize = new Vector2(350, 300);
    }

    void OnEnable()
    {
        saveFolder = EditorPrefs.GetString(PREF_SAVE_FOLDER, saveFolder);
    excludeZero = EditorPrefs.GetBool(PREF_EXCLUDE_ZERO, false);
    searchMode = (SearchMode)EditorPrefs.GetInt(PREF_SEARCH_MODE, (int)SearchMode.Prefix);
    searchText = EditorPrefs.GetString(PREF_SEARCH_TEXT, string.Empty);
    alignToExistingClipKeys = EditorPrefs.GetBool(PREF_ALIGN_TO_CLIP, false);
    excludeNonSearchMatches = EditorPrefs.GetBool(PREF_EXCLUDE_NON_SEARCH, false);
        var last = EditorPrefs.GetString(PREF_LAST_TARGET, string.Empty);
        if (!string.IsNullOrEmpty(last))
        {
            targetObject = EditorUtility.InstanceIDToObject(Convert.ToInt32(last)) as GameObject;
            RefreshTargetFromObject();
        }
    }

    void OnDisable()
    {
        if (targetObject) EditorPrefs.SetString(PREF_LAST_TARGET, targetObject.GetInstanceID().ToString());
        EditorPrefs.SetString(PREF_SAVE_FOLDER, saveFolder);
    EditorPrefs.SetBool(PREF_EXCLUDE_ZERO, excludeZero);
    EditorPrefs.SetInt(PREF_SEARCH_MODE, (int)searchMode);
    EditorPrefs.SetString(PREF_SEARCH_TEXT, searchText);
    EditorPrefs.SetBool(PREF_ALIGN_TO_CLIP, alignToExistingClipKeys);
    EditorPrefs.SetBool(PREF_EXCLUDE_NON_SEARCH, excludeNonSearchMatches);
        // persist snapshot if exists
        if (snapshotValues != null && snapshotValues.Count > 0)
        {
            var parts = new string[snapshotValues.Count];
            for (int i = 0; i < snapshotValues.Count; i++) parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            EditorPrefs.SetString(PREF_SNAPSHOT, string.Join(",", parts));
        }
    SaveBlendValuesPrefs();
    }

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
                // convert absolute to relative if inside project
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
            if (alignToExistingClipKeys && loadedClip == null)
            {
                EditorGUILayout.HelpBox("既存アニメーションを選択すると、そのアニメに含まれるブレンドシェイプのみを書き出します。", MessageType.Info);
            }

            // Exclude zero (disabled when aligning to clip)
            EditorGUI.BeginDisabledGroup(alignToExistingClipKeys);
            EditorGUILayout.BeginHorizontal();
            excludeZero = EditorGUILayout.ToggleLeft("値が0のシェイプをアニメーションから除外する", excludeZero);
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            // 検索 UI
            EditorGUILayout.BeginHorizontal();
            searchText = EditorGUILayout.TextField(searchText, GUILayout.ExpandWidth(true));
            searchMode = (SearchMode)EditorGUILayout.EnumPopup(searchMode, GUILayout.Width(100));
            if (GUILayout.Button("クリア", GUILayout.Width(60))) { searchText = string.Empty; }
            EditorGUILayout.EndHorizontal();

            // Option to exclude non-matching search keys when saving
            excludeNonSearchMatches = EditorGUILayout.ToggleLeft("検索に一致しないキーを保存から除外する", excludeNonSearchMatches);

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
            loadedClip = EditorGUILayout.ObjectField("読み込むアニメーション", loadedClip, typeof(AnimationClip), false) as AnimationClip;
            if (GUILayout.Button("アニメーションを適用", GUILayout.Width(120)))
            {
                ApplyAnimationToMesh(loadedClip);
            }
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < blendNames.Count; i++)
            {
                // filter by search
                if (!string.IsNullOrEmpty(searchText))
                {
                    var name = blendNames[i] ?? "";
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
        if (targetSkinnedMesh == null || targetSkinnedMesh.sharedMesh == null) return;
        var mesh = targetSkinnedMesh.sharedMesh;
        int count = mesh.blendShapeCount;
        for (int i = 0; i < count; i++)
        {
            blendNames.Add(mesh.GetBlendShapeName(i));
            blendValues.Add(targetSkinnedMesh.GetBlendShapeWeight(i));
        }
        // Try to restore saved values for this mesh/instance
        LoadBlendValuesPrefs();
        // auto create snapshot at load
        CreateSnapshot(loadTime: true);
    }

    void CreateSnapshot(bool loadTime = false)
    {
        if (blendValues == null || blendValues.Count == 0) return;
        snapshotValues = new List<float>(blendValues);
        // persist immediately unless this was a load-time call (we will persist on disable anyway)
        if (!loadTime)
        {
            try
            {
                var parts = new string[snapshotValues.Count];
                for (int i = 0; i < snapshotValues.Count; i++) parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                EditorPrefs.SetString(PREF_SNAPSHOT, string.Join(",", parts));
            }
            catch { }
        }
    }

    void RestoreSnapshot()
    {
        if (snapshotValues == null || snapshotValues.Count == 0)
        {
            // try loading persisted snapshot
            if (EditorPrefs.HasKey(PREF_SNAPSHOT))
            {
                var s = EditorPrefs.GetString(PREF_SNAPSHOT);
                var parts = s.Split(',');
                snapshotValues = new List<float>();
                for (int i = 0; i < parts.Length; i++)
                {
                    if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f)) snapshotValues.Add(f);
                    else snapshotValues.Add(0f);
                }
            }
        }
        if (snapshotValues == null) return;
        // apply snapshot to current list
        int n = Math.Min(snapshotValues.Count, blendValues.Count);
        for (int i = 0; i < n; i++)
        {
            blendValues[i] = snapshotValues[i];
            if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, snapshotValues[i]);
        }
        SaveBlendValuesPrefs();
    }

    string GetBlendPrefsKey()
    {
        if (targetSkinnedMesh == null || targetSkinnedMesh.sharedMesh == null) return null;
        string scene = targetObject ? targetObject.scene.name : "";
        string rel = GetRelativePath(targetSkinnedMesh.transform, targetObject ? targetObject.transform : targetSkinnedMesh.transform);
        string meshName = targetSkinnedMesh.sharedMesh.name;
        return $"ShapekeyConverter_Values|{scene}|{rel}|{meshName}";
    }

    void SaveBlendValuesPrefs()
    {
        try
        {
            string key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            // join by comma
            var parts = new string[blendValues.Count];
            for (int i = 0; i < blendValues.Count; i++) parts[i] = blendValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            EditorPrefs.SetString(key, string.Join(",", parts));
        }
        catch { }
    }

    void LoadBlendValuesPrefs()
    {
        try
        {
            string key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            if (!EditorPrefs.HasKey(key)) return;
            var s = EditorPrefs.GetString(key);
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split(',');
            for (int i = 0; i < parts.Length && i < blendValues.Count; i++)
            {
                if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                {
                    blendValues[i] = f;
                    if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, f);
                }
            }
        }
        catch { }
    }

    void SaveAnimationClip()
    {
        if (targetSkinnedMesh == null) { EditorUtility.DisplayDialog("Error", "No target SkinnedMeshRenderer selected.", "OK"); return; }

        if (!Directory.Exists(saveFolder))
        {
            // Attempt to create inside Assets if relative
            try
            {
                Directory.CreateDirectory(saveFolder);
            }
            catch { }
        }

        string defaultName = targetObject ? targetObject.name + "_blendshape" : "blendshape_anim";
        string path = EditorUtility.SaveFilePanelInProject("Save Animation", defaultName + ".anim", "anim", "Save generated animation", saveFolder);
        if (string.IsNullOrEmpty(path)) return;

        var clip = new AnimationClip();
        clip.frameRate = 60;

        // Determine indices to include based on options
        var includeIndices = new List<int>();
        for (int i = 0; i < blendNames.Count; i++) includeIndices.Add(i);

        // If excluding non-matching search keys, filter by current search
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

        // If aligning to an existing clip, intersect with keys present in the clip
        if (alignToExistingClipKeys)
        {
            if (loadedClip == null)
            {
                EditorUtility.DisplayDialog("警告", "既存アニメーションに揃えるオプションが有効ですが、アニメーションクリップが選択されていません。全てのキーを対象にします。", "OK");
            }
            else
            {
                var clipBindings = AnimationUtility.GetCurveBindings(loadedClip);
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

        // For each selected blendshape create an animation curve on SkinnedMeshRenderer's blend shape weight
        // Build optional map from shape name to binding path(s) from the loaded clip so we can reuse the same path structure
        Dictionary<string, List<string>> shapeToPaths = null;
        string currentSmrPath = GetRelativePath(targetSkinnedMesh.transform, targetObject.transform);
        if (alignToExistingClipKeys && loadedClip != null)
        {
            shapeToPaths = new Dictionary<string, List<string>>();
            foreach (var b in AnimationUtility.GetCurveBindings(loadedClip))
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
            // use current renderer weight rather than stored slider value to avoid FBX/mesh default zeros
            float current = targetSkinnedMesh.GetBlendShapeWeight(i);
            // Exclude zero only when NOT aligning to clip keys
            if (!alignToExistingClipKeys && excludeZero && Mathf.Approximately(current, 0f)) continue;

            string prop = "blendShape." + blendNames[i];
            // Property path must reference the SkinnedMeshRenderer component on the target object
            var binding = new EditorCurveBinding();
            binding.type = typeof(SkinnedMeshRenderer);
            // Prefer path from existing clip (to match its structure), fall back to current object's path
            if (shapeToPaths != null && shapeToPaths.TryGetValue(blendNames[i], out var paths) && paths != null && paths.Count > 0)
            {
                // If the loaded clip has multiple paths for the same shape, prefer the one matching currentSmrPath if present
                string chosen = paths.Contains(currentSmrPath) ? currentSmrPath : paths[0];
                binding.path = chosen ?? string.Empty;
            }
            else
            {
                binding.path = currentSmrPath;
            }
            binding.propertyName = prop;

            // create a curve that sets value at time 0 to current value
            var key = new Keyframe[2];
            // both keys have same value to avoid initial 0 from FBX/default
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

        // Get all curve bindings and apply those that match blendShape.<name>
        var bindings = AnimationUtility.GetCurveBindings(clip);
        bool applied = false;
        foreach (var b in bindings)
        {
            if (b.type != typeof(SkinnedMeshRenderer)) continue;
            if (!b.propertyName.StartsWith("blendShape.")) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            float value = curve.Evaluate(0f);
            // find blendshape index by name
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

    static string GetRelativePath(Transform target, Transform root)
    {
        if (target == root) return "";
        var parts = new List<string>();
        var t = target;
        while (t != null && t != root)
        {
            parts.Add(t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }
}
