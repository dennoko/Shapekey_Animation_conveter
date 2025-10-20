using UnityEngine;

public partial class Shapekey_animation_converter
{
    // Undo/Redo callback: sync blendValues from SkinnedMeshRenderer
    void OnUndoRedo()
    {
        if (targetSkinnedMesh == null || targetSkinnedMesh.sharedMesh == null) return;

        // Only sync if we have blend shapes
        int count = Mathf.Min(blendValues.Count, targetSkinnedMesh.sharedMesh.blendShapeCount);
        if (count == 0) return;

        // Sync blend shape values from the mesh to our internal list
        for (int i = 0; i < count; i++)
        {
            blendValues[i] = targetSkinnedMesh.GetBlendShapeWeight(i);
        }

        // Force repaint to update UI
        Repaint();
    }
}
