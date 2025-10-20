using UnityEditor;

public partial class Shapekey_animation_converter
{
    // Status helpers
    void SetStatus(string msg, StatusLevel level = StatusLevel.Info, double autoClearSec = 3.0)
    {
        statusMessage = msg;
        statusLevel = level;
        statusSetAt = EditorApplication.timeSinceStartup;
        statusAutoClearSec = autoClearSec;
        Repaint();
    }

    void TickStatusAutoClear()
    {
        if (statusAutoClearSec <= 0) return;
        if (!string.IsNullOrEmpty(statusMessage) && EditorApplication.timeSinceStartup - statusSetAt > statusAutoClearSec)
        {
            statusMessage = null;
            statusAutoClearSec = 0;
            Repaint();
        }
    }
}
