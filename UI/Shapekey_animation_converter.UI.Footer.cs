using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    void DrawFooter()
    {
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(DenEmoLoc.T("ui.footer.saveAnim"), GUILayout.Height(30)))
        {
            SetStatus(DenEmoLoc.T("status.saving"), StatusLevel.Info, 0);
            SaveAnimationClip();
        }
        if (GUILayout.Button(DenEmoLoc.T("ui.footer.refresh"), GUILayout.Width(80), GUILayout.Height(30)))
        {
            RefreshTargetFromObject();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(DenEmoLoc.T("ui.footer.saveTo"), GUILayout.Width(100));
        saveFolder = EditorGUILayout.TextField(saveFolder);
        if (GUILayout.Button(DenEmoLoc.T("ui.footer.browse"), GUILayout.Width(80)))
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
    }
}
