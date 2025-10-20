using UnityEditor;

public partial class Shapekey_animation_converter
{
    // Status helpers
    void SetStatus(string msg, StatusLevel level = StatusLevel.Info, double autoClearSec = 3.0)
    {
        // For non-ready (non-Info) statuses, double the default display time
        if (level != StatusLevel.Info && autoClearSec == 3.0)
        {
            autoClearSec = 6.0;
        }
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
            // Reset color/style back to Info when returning to ready
            statusLevel = StatusLevel.Info;
            statusAutoClearSec = 0;
            Repaint();
        }
    }
}
