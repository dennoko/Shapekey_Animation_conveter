// UI partial for Shapekey_animation_converter
// - OnGUI layout and interactions
// - Drag & drop handling

using System;
using UnityEditor;
using UnityEngine;

public partial class Shapekey_animation_converter
{
    void OnGUI()
    {
        // Auto clear status if needed
        TickStatusAutoClear();
        DrawHeader();

        // Basic settings section
        if (!DrawBasicSettings())
        {
            // Basic block early-exited; still draw status bar
            DrawStatusBar();
            return;
        }

        DrawSnapshotAndSearch();

        DrawShapeList();

        DrawFooter();

        // Status bar at the bottom
        DrawStatusBar();
    }

    // BuildSearchTokens, MatchesAllTokens moved to State/Shapekey_animation_converter.State.Filter.cs

    // HandleDragAndDrop moved to a UI partial (to be created)
}

// DrawStatusBar moved to UI/Shapekey_animation_converter.UI.FooterAndStatus.cs
