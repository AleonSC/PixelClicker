// ---- System notes ----
// PixelSortingHopperWindow.cs
//   The Sorting Hopper's FILTER PICKER (2026-10-18): opened by the 'Select filter' button the hopper adds to the shared device panel
//   (PixelDeviceCard: right-click tap a hopper; PixelDeviceInfo.AddAction). A standard Fixed window (PixelUIKit.CreateWindow, its own canvas,
//   Escape closes it: PixelWindows 60) with a scrolling grid of every UNLOCKED husk type (not fly-aways, each with its PixelTierPicture and name)
//   plus 'None' (= the hopper just holds); the current filter is highlighted. Clicking one sets it (PixelSortingHopper.SetFilter) and closes the
//   window. The hopper calls Changed(this) when its filter changes (the highlight redraws) and Forget(this) when it is destroyed (the window closes).
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelSortingHopperWindow : MonoBehaviour
{
    private static PixelSortingHopperWindow instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private PixelSortingHopper hopper;
    private PixelClicker clicker;
    private TMP_FontAsset font;
    private GameObject panel;
    private RectTransform gridContent;
    private ScrollRect scroll;
    private GameObject scrollBar;
    private float viewHeight;
    private bool dirty;

    private const float Width = 690f, Height = 720f, Pad = 30f, TileW = 118f, TileH = 132f, Gap = 8f;
    private const int Columns = 5;

    /// <summary>Is the picker open?</summary>
    public static bool IsOpen => instance != null && instance.panel != null && instance.panel.activeSelf;

    /// <summary>Opens the picker for this hopper (or switches the open picker to it).</summary>
    public static void Open(PixelSortingHopper h)
    {
        if (h == null) return;
        if (instance == null)
        {
            PixelClicker c = h.Clicker != null ? h.Clicker : PixelFind.Cached<PixelClicker>();
            if (c == null || c.UIFont == null) return;
            PixelUIKit.EnsureEventSystem();
            GameObject canvas = PixelUIKit.CreateCanvas("Hopper Filter Picker", 660, new Vector2(1920f, 1080f), true);   // (above the device panel, 650)
            instance = canvas.AddComponent<PixelSortingHopperWindow>();
            instance.clicker = c;
            instance.font = c.UIFont;
            instance.Build();
            PixelWindows.Register(instance, 60, () => IsOpen, Close);
        }
        instance.hopper = h;
        instance.panel.SetActive(true);
        instance.Refresh();
    }

    /// <summary>Closes the picker.</summary>
    public static void Close()
    {
        if (instance == null || instance.panel == null) return;
        instance.panel.SetActive(false);
        instance.hopper = null;
    }

    /// <summary>The hopper's filter changed: redraw the highlight if the picker shows it.</summary>
    public static void Changed(PixelSortingHopper h) { if (instance != null && h != null && instance.hopper == h) instance.dirty = true; }

    /// <summary>The hopper is going away: close the picker if it shows it.</summary>
    public static void Forget(PixelSortingHopper h) { if (instance != null && instance.hopper == h) Close(); }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (instance == this) instance = null;
    }

    private void Build()
    {
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, transform, "Panel", "Select filter", new Vector2(Width, Height), PixelUIKit.WindowKind.Fixed, Close);
        panel = parts.Root;
        TMP_Text hint = PixelUIKit.CreateText(font, panel.transform, "Hint", "This type goes into the Recycler; every other type drops out of the side tube.",
                                              22f, TextAlignmentOptions.Center, FontStyles.Italic, PixelUITheme.DimTextColor);
        Place(hint.rectTransform, new Vector2(Pad, -parts.HeaderHeight - 4f), new Vector2(Width - 2f * Pad, 56f));
        float top = parts.HeaderHeight + 70f;
        viewHeight = Height - top - Pad;
        scroll = PixelUIKit.CreateScrollView(panel.transform, "Types", Color.gray, PixelUIKit.ScrollBarWidth, 30f, out gridContent, out scrollBar);
        Place(scroll.GetComponent<RectTransform>(), new Vector2(Pad, -top), new Vector2(Width - 2f * Pad, viewHeight));
        panel.SetActive(false);
    }

    private static void Place(RectTransform r, Vector2 topLeft, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = topLeft;
        r.sizeDelta = size;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (hopper == null || PixelCalm.InMenu) { Close(); return; }
        if (dirty) Refresh();
    }

    private void Refresh()
    {
        dirty = false;
        if (hopper == null || clicker == null) return;
        for (int i = gridContent.childCount - 1; i >= 0; i--) Destroy(gridContent.GetChild(i).gameObject);
        int n = 0;
        Tile(n++, -1, "None");
        for (int t = 0; t < clicker.Tiers.Length; t++)
        {
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            if (tier == null || !tier.unlocked || tier.flyAway) continue;
            Tile(n++, t, tier.displayName.Replace(" Pixels", ""));
        }
        PixelUIKit.UpdateScrollView(scroll, scrollBar, ((n + Columns - 1) / Columns) * (TileH + Gap), viewHeight);
    }

    /// <summary>A clickable tile at grid slot 'index': the block's picture (none for None) and its name; the current filter is highlighted.</summary>
    private void Tile(int index, int tier, string label)
    {
        GameObject go = new GameObject("Tile", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(gridContent, false);
        Image bg = go.GetComponent<Image>();
        bool selected = tier == hopper.FilterTier;
        bg.color = selected ? PixelUITheme.ColorOf(ButtonRole.Selected) : new Color(0f, 0f, 0f, 0.25f);
        PixelUIKit.StyleBox(bg, true);
        Place(go.GetComponent<RectTransform>(), new Vector2((index % Columns) * (TileW + Gap), -(index / Columns) * (TileH + Gap)), new Vector2(TileW, TileH));
        if (tier >= 0)
        {
            GameObject pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            pic.transform.SetParent(go.transform, false);
            RawImage img = pic.GetComponent<RawImage>();
            img.texture = PixelTierPicture.Get(clicker, tier, 128);
            img.raycastTarget = false;
            Place(pic.GetComponent<RectTransform>(), new Vector2((TileW - 84f) * 0.5f, -6f), new Vector2(84f, 84f));
            PixelTierPicture.Render(tier, 30f);
        }
        TMP_Text t = PixelUIKit.CreateText(font, go.transform, "Label", label, 18f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        Place(t.rectTransform, new Vector2(4f, tier >= 0 ? -92f : -(TileH - 34f) * 0.5f), new Vector2(TileW - 8f, 34f));
        PixelUIKit.FitOneLine(t, 11f);
        int pick = tier;
        PixelUIKit.MakeClickable(go, () => { if (hopper != null) hopper.SetFilter(pick); Close(); }, true, false);
    }
}
