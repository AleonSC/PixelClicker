using UnityEngine;

/// <summary>
/// Something the player can restyle by stepping through looks with arrows (floor styles, skyboxes). The Settings row
/// and the title screen's small pickers only talk to this, so a new kind of look plugs straight into both.
/// </summary>
public interface IPixelLookSource
{
    /// <summary>False when there is nothing to restyle (e.g. no floor in the scene); the picker greys out.</summary>
    bool Usable { get; }

    /// <summary>Index of the look in use.</summary>
    int Current { get; }

    /// <summary>How many looks there are (the picker's drop-down lists them all).</summary>
    int StyleCount { get; }

    /// <summary>Use look 'index' (applied and remembered right away).</summary>
    void SetStyle(int index);

    string StyleName(int index);

    /// <summary>A picture of look 'index' for the picker's icon (may be null: then only <see cref="PreviewColor"/> shows).</summary>
    Texture PreviewTexture(int index);

    /// <summary>Tint of the icon (white for a full-colour picture).</summary>
    Color PreviewColor(int index);

    /// <summary>The part of the preview picture the icon shows (uv rect).</summary>
    Rect PreviewRect { get; }

    /// <summary>Next (+1) / previous (-1) look; applied and remembered right away.</summary>
    void Step(int direction);
}
