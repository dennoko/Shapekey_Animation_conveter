// Core entry file: keeps the menu entry and defines the partial class shell.
using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter : EditorWindow
{
    [MenuItem("Tools/DenEmo")]
    public static void ShowWindow()
    {
        var w = GetWindow<Shapekey_animation_converter>("ブレンドシェイプ変換");
        w.minSize = new Vector2(350, 300);
    }
}
