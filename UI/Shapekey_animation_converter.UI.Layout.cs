using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("DenEmo", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        var newLang = EditorGUILayout.ToggleLeft(DenEmoLoc.T("ui.lang.englishMode"), DenEmoLoc.EnglishMode, GUILayout.Width(140));
        if (newLang != DenEmoLoc.EnglishMode)
        {
            DenEmoLoc.EnglishMode = newLang;
            Repaint();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }
}
