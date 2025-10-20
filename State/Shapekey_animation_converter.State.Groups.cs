using UnityEditor;
using System.Collections.Generic;

public partial class Shapekey_animation_converter
{
    // Collapsed groups helpers (persist per project)
    void LoadCollapsedGroupsPrefs()
    {
        collapsedGroups.Clear();
        var s = DenEmoProjectPrefs.GetString(PREF_GROUPS_COLLAPSED, string.Empty);
        if (string.IsNullOrEmpty(s)) return;
        var parts = s.Split(new char[] { '\n', '\r', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var key = p.Trim();
            if (key.Length > 0) collapsedGroups.Add(key);
        }
    }

    void SaveCollapsedGroupsPrefs()
    {
        if (collapsedGroups == null || collapsedGroups.Count == 0)
        {
            DenEmoProjectPrefs.SetString(PREF_GROUPS_COLLAPSED, string.Empty);
            return;
        }
        var arr = new List<string>(collapsedGroups);
        DenEmoProjectPrefs.SetString(PREF_GROUPS_COLLAPSED, string.Join(",", arr));
    }

    bool IsGroupCollapsed(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return collapsedGroups.Contains(key);
    }

    void SetGroupCollapsed(string key, bool collapsed)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (collapsed)
            collapsedGroups.Add(key);
        else
            collapsedGroups.Remove(key);
        SaveCollapsedGroupsPrefs();
    }
}
