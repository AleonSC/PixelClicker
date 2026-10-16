// ---- System notes ----
// PixelClickerTitlePreview.cs
//   Part of partial class PixelClicker: a LOOK-ONLY preview of any pixel type on the cube, for the main menu (PixelTitleScreen cycles every type on the
//   cube). ShowLookPreview(tierIndex) plays the materialize pop with that type's look WITHOUT changing the current block or raising PixelMaterialized
//   (so the Golden Snitch, hazard hints, stakes etc. never react to it); EndLookPreview() brings the real current block back (a normal Materialize).
// ---------------------------------------------------
public partial class PixelClicker
{
    private bool lookPreview;

    /// <summary>Is the cube showing a main-menu preview look instead of the real current block?</summary>
    public bool LookPreviewActive => lookPreview;

    /// <summary>Shows this pixel type's look on the cube (with the materialize pop) without changing the game: no events, no new block.</summary>
    public void ShowLookPreview(int tierIndex)
    {
        if (!IsValidTier(tierIndex) || pixelTransform == null) return;
        lookPreview = true;
        if (materializeRoutine != null) StopCoroutine(materializeRoutine);
        materializeRoutine = StartCoroutine(MaterializeRoutine(tiers[tierIndex]));
    }

    /// <summary>Ends the preview: the real current block materializes again.</summary>
    public void EndLookPreview()
    {
        if (!lookPreview) return;
        lookPreview = false;
        Materialize(GetClickTier());
    }
}
