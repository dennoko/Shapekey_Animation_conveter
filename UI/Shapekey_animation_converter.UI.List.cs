using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    // Time-based throttling (50ms) with coalescing during slider drags
    const double APPLY_INTERVAL_SEC = 0.05; // 50ms
    bool _throttleActive = false;           // update loop subscribed
    bool _hasPending = false;               // have a value waiting to apply
    int _pendingIndex = -1;                 // latest index to apply
    float _pendingValue = 0f;               // latest value to apply
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
        _hasPending = false;
        _pendingIndex = -1;
    }

    void QueuePendingApply(int index, float value)
    {
        _pendingIndex = index;
        _pendingValue = value;
        _hasPending = true;
        EnsureThrottleUpdate();
    }

    void OnEditorUpdateApply()
    {
        if (targetSkinnedMesh == null)
        {
            StopThrottleUpdate();
            return;
        }
        if (!_hasPending) return;
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastApplyTime >= APPLY_INTERVAL_SEC)
        {
            if (_pendingIndex >= 0)
            {
                targetSkinnedMesh.SetBlendShapeWeight(_pendingIndex, _pendingValue);
                _lastApplyTime = now;
            }
        }
    }

    void DrawShapeList()
    {
        // List (framed)
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
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

            // Use cached visible indices for rendering
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
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
}
