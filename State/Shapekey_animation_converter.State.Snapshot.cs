using System;
using System.Collections.Generic;
using UnityEditor;

public partial class Shapekey_animation_converter
{
    void CreateSnapshot(bool loadTime = false)
    {
        if (blendValues == null || blendValues.Count == 0) return;
        snapshotValues = new List<float>(blendValues);
        if (!loadTime)
        {
            try
            {
                var parts = new string[snapshotValues.Count];
                for (int i = 0; i < snapshotValues.Count; i++) parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                DenEmoProjectPrefs.SetString(PREF_SNAPSHOT, string.Join(",", parts));
            }
            catch { }
        }
    }

    void RestoreSnapshot()
    {
        if (snapshotValues == null || snapshotValues.Count == 0)
        {
            if (DenEmoProjectPrefs.HasKey(PREF_SNAPSHOT))
            {
                var s = DenEmoProjectPrefs.GetString(PREF_SNAPSHOT);
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
        int n = Math.Min(snapshotValues.Count, blendValues.Count);
        for (int i = 0; i < n; i++)
        {
            blendValues[i] = snapshotValues[i];
            if (targetSkinnedMesh) targetSkinnedMesh.SetBlendShapeWeight(i, snapshotValues[i]);
        }
        SaveBlendValuesPrefs();
    }
}
