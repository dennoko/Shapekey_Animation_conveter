using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    // Time-based throttling (50ms) with coalescing during slider drags
    const double APPLY_INTERVAL_SEC = 0.05; // 50ms
    bool _throttleActive = false;           // update loop subscribed
    System.Collections.Generic.Dictionary<int, float> _pendingApplies = new System.Collections.Generic.Dictionary<int, float>();
    double _lastApplyTime = 0;              // last time we actually applied to mesh

    void EnsureThrottleUpdate()
    {
        if (!_throttleActive)
        {
            _throttleActive = true;
            EditorApplication.update += OnEditorUpdateApply;
        }
    }

    void StopThrottleUpdate()
    {
        if (_throttleActive)
        {
            EditorApplication.update -= OnEditorUpdateApply;
            _throttleActive = false;
        }
        _pendingApplies.Clear();
    }

    void QueuePendingApply(int index, float value)
    {
        _pendingApplies[index] = value; // coalesce: latest wins per index
        EnsureThrottleUpdate();
    }

    void OnEditorUpdateApply()
    {
        if (targetSkinnedMesh == null)
        {
            StopThrottleUpdate();
            return;
        }
        if (_pendingApplies.Count == 0) return;
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastApplyTime >= APPLY_INTERVAL_SEC)
        {
            foreach (var kv in _pendingApplies)
            {
                if (kv.Key >= 0) targetSkinnedMesh.SetBlendShapeWeight(kv.Key, kv.Value);
            }
            _pendingApplies.Clear();
            _lastApplyTime = now;
        }
    }

    void DrawShapeList()
    {
        // List (framed)
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        // Debug: LipSync 検出サマリと一覧（コメントアウト）
        // int lipCount = 0;
        // for (int di = 0; di < isLipSyncShapeCache.Count && di < blendNames.Count; di++)
        // {
        //     if (isLipSyncShapeCache[di]) lipCount++;
        // }
        // EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        // EditorGUILayout.LabelField("Debug: LipSync 検出サマリ", EditorStyles.boldLabel);
        // EditorGUILayout.LabelField($"Descriptor: {(_lsDescFound ? "Found" : "Not Found")}", EditorStyles.miniLabel);
        // EditorGUILayout.LabelField($"lipSync prop: {(_lsLipSyncPropFound ? "Found" : "Not Found")}", EditorStyles.miniLabel);
        // EditorGUILayout.LabelField($"lipSync mode: {(_lsLipSyncMode ?? "null")}", EditorStyles.miniLabel);
        // EditorGUILayout.LabelField($"is VisemeBlendShape: {(_lsIsVisemeBlendshape ? "Yes" : "No")}", EditorStyles.miniLabel);
        // EditorGUILayout.LabelField($"Viseme SMR: {(_lsSmrPropFound ? (_lsSmrName ?? "<null>") : "prop/field-missing")}", EditorStyles.miniLabel);
        // EditorGUILayout.LabelField($"Viseme names: {(_lsNamesPropFound ? _lsNamesCount.ToString() : "prop/field-missing")}", EditorStyles.miniLabel);
        // if (_lsNamesSample != null && _lsNamesSample.Count > 0)
        // {
        //     string sample = string.Join(", ", _lsNamesSample.ToArray());
        //     EditorGUILayout.LabelField($"Sample: {sample}", EditorStyles.miniLabel);
        // }
        // _debugLipSyncFoldout = EditorGUILayout.Foldout(_debugLipSyncFoldout, $"リップシンク検出一覧 ({lipCount})", true);
        // if (_debugLipSyncFoldout && lipCount > 0)
        // {
        //     int shown = 0;
        //     for (int di = 0; di < isLipSyncShapeCache.Count && di < blendNames.Count; di++)
        //     {
        //         if (!isLipSyncShapeCache[di]) continue;
        //         EditorGUILayout.LabelField($"- {blendNames[di]}", EditorStyles.miniLabel);
        //         shown++;
        //         if (shown >= 50)
        //         {
        //             EditorGUILayout.LabelField("…(省略)", EditorStyles.miniLabel);
        //             break;
        //         }
        //     }
        // }
        // EditorGUILayout.EndVertical();
        // EditorGUILayout.Space(4);
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

                // Fold toggle button left of group name
                bool collapsed = IsGroupCollapsed(seg.key);
                string foldIcon = collapsed ? "\u25B6" : "\u25BC"; // ▶ / ▼
                if (GUILayout.Button(foldIcon, EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    SetGroupCollapsed(seg.key, !collapsed);
                }

                // Exclude group checkbox from Tab focus navigation
                int groupCheckboxId = GUIUtility.GetControlID(FocusType.Passive);
                Rect groupCheckboxRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
                newGroupVal = GUI.Toggle(groupCheckboxRect, groupAllOn, GUIContent.none);

                using (new EditorGUI.DisabledGroupScope(true))
                {
                    string suffix = groupAllOn ? DenEmoLoc.T("ui.group.all") : groupAllOff ? DenEmoLoc.T("ui.group.none") : DenEmoLoc.T("ui.group.some");
                    EditorGUILayout.LabelField($"{seg.key}  {suffix}  [{enabledCount}/{visibleCount}]", EditorStyles.boldLabel);
                }
                EditorGUILayout.EndHorizontal();
                if (newGroupVal != groupAllOn)
                {
                    for (int i = start; i < end; i++)
                    {
                        if ((i < isVrcShapeCache.Count && isVrcShapeCache[i]) || (i < isLipSyncShapeCache.Count && isLipSyncShapeCache[i])) continue;
                        includeFlags[i] = newGroupVal;
                    }
                    filterCacheDirty = true; // Mark cache dirty when include flags change
                    SaveIncludeFlagsPrefs();
                }
            }

            // Rendering
            if (symmetryMode)
            {
                // Build LR maps within this segment using cached global pairs and parse results
                var segBases = new System.Collections.Generic.HashSet<string>();
                var nonLR = new System.Collections.Generic.List<int>();
                for (int i = start; i < end; i++)
                {
                    if ((i < isVrcShapeCache.Count && isVrcShapeCache[i]) || (i < isLipSyncShapeCache.Count && isLipSyncShapeCache[i])) continue;
                    var name = blendNames[i];
                    // Use cached parse
                    if (lrParseCache.TryGetValue(name, out var parsed))
                    {
                        if (parsed.side == LRSide.L || parsed.side == LRSide.R)
                        {
                            segBases.Add(parsed.baseName);
                        }
                        else
                        {
                            nonLR.Add(i);
                        }
                    }
                    else
                    {
                        nonLR.Add(i);
                    }
                }

                // Render merged LR rows over the segment-relevant base names
                foreach (var baseName in segBases)
                {
                    int li = baseToLIndex.ContainsKey(baseName) ? baseToLIndex[baseName] : -1;
                    int ri = baseToRIndex.ContainsKey(baseName) ? baseToRIndex[baseName] : -1;
                    // Constrain to segment bounds
                    if (li >= 0 && (li < start || li >= end)) li = -1;
                    if (ri >= 0 && (ri < start || ri >= end)) ri = -1;
                    bool leftVis = li >= 0 && (li < visibleFlags.Count && visibleFlags[li]);
                    bool rightVis = ri >= 0 && (ri < visibleFlags.Count && visibleFlags[ri]);
                    if (!leftVis && !rightVis) continue; // only show if either side visible
                    bool both = li >= 0 && ri >= 0;

                    EditorGUILayout.BeginHorizontal();
                    if (treatAsGroup) GUILayout.Space(24);

                    // Checkbox reflects BOTH included when both exist; mixed shows unchecked but toggling applies to both
                    bool currentInc = true;
                    if (both)
                    {
                        bool il = li < includeFlags.Count && includeFlags[li];
                        bool ir = ri < includeFlags.Count && includeFlags[ri];
                        currentInc = il && ir;
                    }
                    else
                    {
                        int pi = li >= 0 ? li : ri;
                        currentInc = pi < includeFlags.Count && includeFlags[pi];
                    }
                    int checkboxId = GUIUtility.GetControlID(FocusType.Passive);
                    Rect checkboxRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
                    bool newInc = GUI.Toggle(checkboxRect, currentInc, GUIContent.none);
                    if (newInc != currentInc)
                    {
                        if (li >= 0) includeFlags[li] = newInc;
                        if (ri >= 0) includeFlags[ri] = newInc;
                        filterCacheDirty = true;
                        SaveIncludeFlagsPrefs();
                    }

                    // Label
                    float nameWidth = Mathf.Min(220f, EditorGUIUtility.currentViewWidth * 0.35f);
                    string displayName = both ? (baseName + "_LR") : (li >= 0 ? blendNames[li] : blendNames[ri]);
                    EditorGUILayout.LabelField(displayName, GUILayout.Width(nameWidth));

                    // Zero button
                    if (GUILayout.Button("0", EditorStyles.miniButton, GUILayout.Width(22)))
                    {
                        if (targetSkinnedMesh != null) Undo.RecordObject(targetSkinnedMesh, "Reset Shape Key to 0");
                        if (li >= 0 && blendValues[li] != 0f)
                        {
                            blendValues[li] = 0f; if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(li, 0f);
                        }
                        if (ri >= 0 && blendValues[ri] != 0f)
                        {
                            blendValues[ri] = 0f; if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(ri, 0f);
                        }
                    }

                    // Slider (one control for both)
                    int primary = li >= 0 ? li : ri;
                    float oldValue = blendValues[primary];
                    int sliderId = GUIUtility.GetControlID(FocusType.Passive);
                    EditorGUI.BeginChangeCheck();
                    float newValue = EditorGUILayout.Slider(oldValue, 0f, 100f);
                    bool valueChanged = EditorGUI.EndChangeCheck();
                    bool isThisSliderHot = (GUIUtility.hotControl == sliderId || GUIUtility.hotControl == sliderId + 1);

                    if (valueChanged && isThisSliderHot && (!isSliderDragging || currentDraggingIndex != primary))
                    {
                        if (targetSkinnedMesh != null) Undo.RecordObject(targetSkinnedMesh, "Change Shape Key Value");
                        isSliderDragging = true;
                        currentDraggingIndex = primary;
                    }

                    if (valueChanged)
                    {
                        if (!isThisSliderHot && !isSliderDragging && targetSkinnedMesh != null)
                        {
                            Undo.RecordObject(targetSkinnedMesh, "Change Shape Key Value");
                        }
                        // Set both sides
                        if (li >= 0) { blendValues[li] = newValue; }
                        if (ri >= 0) { blendValues[ri] = newValue; }
                        if (targetSkinnedMesh)
                        {
                            if (isThisSliderHot || isSliderDragging)
                            {
                                if (li >= 0) QueuePendingApply(li, newValue);
                                if (ri >= 0) QueuePendingApply(ri, newValue);
                            }
                            else
                            {
                                if (li >= 0) targetSkinnedMesh.SetBlendShapeWeight(li, newValue);
                                if (ri >= 0) targetSkinnedMesh.SetBlendShapeWeight(ri, newValue);
                            }
                        }
                    }

                    if (isSliderDragging && currentDraggingIndex == primary && !isThisSliderHot)
                    {
                        isSliderDragging = false;
                        currentDraggingIndex = -1;
                        if (targetSkinnedMesh)
                        {
                            if (li >= 0) targetSkinnedMesh.SetBlendShapeWeight(li, blendValues[li]);
                            if (ri >= 0) targetSkinnedMesh.SetBlendShapeWeight(ri, blendValues[ri]);
                        }
                        StopThrottleUpdate();
                    }

                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(3f);
                }

                // Render non-LR singles
                foreach (int i in nonLR)
                {
                    if (!(i < visibleFlags.Count && visibleFlags[i])) continue;
                    if (treatAsGroup && IsGroupCollapsed(seg.key)) continue;
                    // singles: reuse existing single-row code via inline

                    EditorGUILayout.BeginHorizontal();
                    if (treatAsGroup) GUILayout.Space(24);

                    int checkboxId2 = GUIUtility.GetControlID(FocusType.Passive);
                    Rect checkboxRect2 = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
                    bool newInc2 = GUI.Toggle(checkboxRect2, includeFlags[i], GUIContent.none);
                    if (newInc2 != includeFlags[i])
                    {
                        includeFlags[i] = newInc2;
                        filterCacheDirty = true;
                        SaveIncludeFlagsPrefs();
                    }

                    float nameWidth2 = Mathf.Min(220f, EditorGUIUtility.currentViewWidth * 0.35f);
                    EditorGUILayout.LabelField(blendNames[i], GUILayout.Width(nameWidth2));

                    if (GUILayout.Button("0", EditorStyles.miniButton, GUILayout.Width(22)))
                    {
                        if (blendValues[i] != 0f)
                        {
                            if (targetSkinnedMesh != null) Undo.RecordObject(targetSkinnedMesh, "Reset Shape Key to 0");
                            blendValues[i] = 0f;
                            if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, 0f);
                        }
                    }

                    float oldValue2 = blendValues[i];
                    int sliderId2 = GUIUtility.GetControlID(FocusType.Passive);
                    EditorGUI.BeginChangeCheck();
                    float newValue2 = EditorGUILayout.Slider(oldValue2, 0f, 100f);
                    bool valueChanged2 = EditorGUI.EndChangeCheck();
                    bool isThisSliderHot2 = (GUIUtility.hotControl == sliderId2 || GUIUtility.hotControl == sliderId2 + 1);
                    if (valueChanged2 && isThisSliderHot2 && (!isSliderDragging || currentDraggingIndex != i))
                    {
                        if (targetSkinnedMesh != null) Undo.RecordObject(targetSkinnedMesh, "Change Shape Key Value");
                        isSliderDragging = true;
                        currentDraggingIndex = i;
                    }
                    if (valueChanged2)
                    {
                        if (!isThisSliderHot2 && !isSliderDragging && targetSkinnedMesh != null)
                        {
                            Undo.RecordObject(targetSkinnedMesh, "Change Shape Key Value");
                        }
                        blendValues[i] = newValue2;
                        if (targetSkinnedMesh)
                        {
                            if (isThisSliderHot2 || isSliderDragging) QueuePendingApply(i, newValue2);
                            else targetSkinnedMesh.SetBlendShapeWeight(i, newValue2);
                        }
                    }
                    if (isSliderDragging && currentDraggingIndex == i && !isThisSliderHot2)
                    {
                        isSliderDragging = false;
                        currentDraggingIndex = -1;
                        if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, blendValues[i]);
                        StopThrottleUpdate();
                    }
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(3f);
                }
            }
            else
            {
                // Original per-index rendering when symmetry is off
                foreach (int i in visibleIndices)
                {
                    if (i < start || i >= end) continue; // Only render indices in this segment
                    if (treatAsGroup && IsGroupCollapsed(seg.key)) continue; // Skip children when group collapsed
                    // Skip lip-sync reserved shapes from being toggled/edited
                    if ((i < isLipSyncShapeCache.Count && isLipSyncShapeCache[i])) continue;

                    EditorGUILayout.BeginHorizontal();
                    if (treatAsGroup) GUILayout.Space(24);

                    // Exclude checkbox from Tab focus navigation
                    int checkboxId = GUIUtility.GetControlID(FocusType.Passive);
                    Rect checkboxRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
                    bool newInc = GUI.Toggle(checkboxRect, includeFlags[i], GUIContent.none);
                    if (newInc != includeFlags[i])
                    {
                        includeFlags[i] = newInc;
                        filterCacheDirty = true; // Mark cache dirty
                        SaveIncludeFlagsPrefs();
                    }

                    // Label (shape name) on the left
                    float nameWidth = Mathf.Min(220f, EditorGUIUtility.currentViewWidth * 0.35f);
                    EditorGUILayout.LabelField(blendNames[i], GUILayout.Width(nameWidth));

                    // Reset-to-zero compact button (just left of slider)
                    if (GUILayout.Button("0", EditorStyles.miniButton, GUILayout.Width(22)))
                    {
                        // Only act if not already zero to avoid unnecessary work
                        if (blendValues[i] != 0f)
                        {
                            if (targetSkinnedMesh != null)
                            {
                                Undo.RecordObject(targetSkinnedMesh, "Reset Shape Key to 0");
                            }
                            blendValues[i] = 0f;
                            if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, 0f);
                        }
                    }

                    // Slider with Undo support (no label)
                    float oldValue = blendValues[i];
                    // Get control ID before the slider
                    int sliderId = GUIUtility.GetControlID(FocusType.Passive);

                    EditorGUI.BeginChangeCheck();
                    float newValue = EditorGUILayout.Slider(oldValue, 0f, 100f);
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
                        if (targetSkinnedMesh)
                        {
                            // During drag: time-based throttling with coalescing; direct input applies immediately
                            if (isThisSliderHot || isSliderDragging)
                            {
                                QueuePendingApply(i, newValue);
                            }
                            else
                            {
                                targetSkinnedMesh.SetBlendShapeWeight(i, newValue);
                            }
                        }
                    }

                    // Detect end of drag - when this slider was hot but now isn't
                    if (isSliderDragging && currentDraggingIndex == i && !isThisSliderHot)
                    {
                        isSliderDragging = false;
                        currentDraggingIndex = -1;
                        // Flush final value and stop throttling loop
                        if (targetSkinnedMesh)
                        {
                            targetSkinnedMesh.SetBlendShapeWeight(i, blendValues[i]);
                        }
                        StopThrottleUpdate();
                    }

                    EditorGUILayout.EndHorizontal();
                    // Add small vertical padding between items (~3px)
                    GUILayout.Space(3f);
                }
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
}
