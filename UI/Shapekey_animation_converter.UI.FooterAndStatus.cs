using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    void DrawStatusBar()
    {
        GUILayout.FlexibleSpace();
        var rect = GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            // Background color per level
            var bg = EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.90f, 0.90f, 0.90f);
            switch (statusLevel)
            {
                case StatusLevel.Success: bg = new Color(0.20f, 0.45f, 0.20f, 1f); break;
                case StatusLevel.Warning: bg = new Color(0.55f, 0.45f, 0.15f, 1f); break;
                case StatusLevel.Error: bg = new Color(0.55f, 0.20f, 0.20f, 1f); break;
            }
            EditorGUI.DrawRect(rect, bg);
        }
        var style = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white },
            padding = new RectOffset(8, 8, 0, 0)
        };
        string text = string.IsNullOrEmpty(statusMessage) ? DenEmoLoc.T("status.ready") : statusMessage;
        GUI.Label(rect, text, style);
    }
}
