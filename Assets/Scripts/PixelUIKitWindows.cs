// ---- System notes ----
// PixelUIKitWindows.cs
//   Part of static partial class PixelUIKit: the WINDOW BUILDER (UI unification step 5; the standard is in docs/systems/WindowInventory.md).
//   PixelUIKit.CreateWindow(font, parent, name, title, size, background, kind, prefKey) builds a window in the one standard look and
//   returns a PixelWindowParts (Root / Rect / Title / Close / Chrome / HeaderHeight / SetHeight):
//   * Kind.Fixed (the big HUD windows: Shop, Skills, Log, Inventory, Crafting, Bank, Toggles, Database, Guide...): centred where it opens,
//     header WindowHeaderHeight 90, title 48 centred in capitals, a grey X 70 at (-14, -12). Not draggable.
//   * Kind.Tool (small tool windows: View, Pocket, tracker, Next up, Dev Hub): header 56, title 30 centred in capitals, a grey X 44 at
//     (-8, -6), and PixelWindowChrome: dragged by the header (kept between the black bars), minimize '-', dock to a screen edge,
//     position saved under prefKey (+ .X / .Y / .Min / .Dock / .DockY). Anchored at the TOP LEFT (PixelWindowChrome works that way).
//   Every window gets StyleWindow, a PixelPop (open / close animation) and its X wired to the given onClose. The owner still
//   registers with PixelWindows (Escape) and builds its own content below HeaderHeight. Dialogs that close with their own
//   Done / Continue / Next button pass closeButton: false (no X).
//   Windows moved onto it so far: View (PixelViewWindow).
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The pieces of a window made by <see cref="PixelUIKit.CreateWindow"/>.</summary>
public sealed class PixelWindowParts
{
    public GameObject Root;
    public RectTransform Rect;
    public TMP_Text Title;
    /// <summary>The X (null for a dialog without one).</summary>
    public Button Close;
    /// <summary>Drag / minimize / dock (Tool windows only, else null).</summary>
    public PixelWindowChrome Chrome;
    /// <summary>Height of the header strip: content starts below it.</summary>
    public float HeaderHeight;

    /// <summary>Sets the window's full height (use this instead of Rect.sizeDelta: a minimized Tool window remembers it for when it opens up again).</summary>
    public void SetHeight(float height)
    {
        if (Chrome != null) Chrome.SetFullHeight(height);
        else Rect.sizeDelta = new Vector2(Rect.sizeDelta.x, height);
    }
}

public static partial class PixelUIKit
{
    public enum WindowKind { Fixed, Tool }

    // ---- The standard (docs/systems/WindowInventory.md, chosen 2026-10-18). One place: change a number here and every window follows. ----
    public const float WindowHeaderHeight = 90f, WindowTitleSize = 48f, WindowCloseSize = 70f;
    public static readonly Vector2 WindowCloseOffset = new Vector2(14f, 12f);   // from the top-right corner
    public const float ToolHeaderHeight = 56f, ToolTitleSize = 30f, ToolCloseSize = 44f;
    public static readonly Vector2 ToolCloseOffset = new Vector2(8f, 6f);
    public static readonly Color WindowButtonColor = new Color(0.3f, 0.3f, 0.35f, 1f);   // the X and the chrome buttons (grey, never red)

    /// <summary>
    /// A window in the standard look (see the header of PixelUIKitWindows.cs). 'parent' is a full-screen canvas. Tool windows need a
    /// prefKey (where their position / minimized / docked state is saved). 'onClose' runs when the X is clicked.
    /// </summary>
    public static PixelWindowParts CreateWindow(TMP_FontAsset font, Transform parent, string objectName, string title, Vector2 size, Color background,
                                                WindowKind kind, System.Action onClose, string prefKey = null, bool closeButton = true)
    {
        bool tool = kind == WindowKind.Tool;
        PixelWindowParts w = new PixelWindowParts { HeaderHeight = tool ? ToolHeaderHeight : WindowHeaderHeight };

        w.Root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(PixelPop));
        w.Root.transform.SetParent(parent, false);
        StyleWindow(w.Root.GetComponent<Image>(), background);
        w.Rect = w.Root.GetComponent<RectTransform>();
        Vector2 anchor = tool ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);   // (Tool: top left, for PixelWindowChrome)
        w.Rect.anchorMin = w.Rect.anchorMax = w.Rect.pivot = anchor;
        w.Rect.sizeDelta = size;

        float closeSize = tool ? ToolCloseSize : WindowCloseSize;
        Vector2 closeAt = tool ? ToolCloseOffset : WindowCloseOffset;
        float sideRoom = closeButton ? closeSize + closeAt.x + 8f : 16f;
        if (tool) sideRoom += ToolCloseSize + 6f;   // (room for the minimize button too)

        w.Title = CreateText(font, w.Root.transform, "Title", title, tool ? ToolTitleSize : WindowTitleSize, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        Caps(w.Title);
        w.Title.raycastTarget = false;
        w.Title.enableAutoSizing = true;
        w.Title.fontSizeMax = tool ? ToolTitleSize : WindowTitleSize;
        w.Title.fontSizeMin = 14f;
        RectTransform tr = w.Title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(sideRoom, -w.HeaderHeight);   // centred, clear of the buttons on both sides
        tr.offsetMax = new Vector2(-sideRoom, 0f);

        if (closeButton)
        {
            w.Close = CreateButton(font, w.Root.transform, "Close", "X", new Vector2(closeSize, closeSize), WindowButtonColor, Color.white, closeSize * 0.4f);
            RectTransform cr = w.Close.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-closeAt.x, -closeAt.y);
            if (onClose != null) w.Close.onClick.AddListener(() => onClose());
        }

        if (tool)
        {
            float firstRight = closeButton ? closeAt.x + closeSize + 6f : closeAt.x;   // the minimize button sits left of the X
            w.Chrome = PixelWindowChrome.Attach(w.Rect, font, string.IsNullOrEmpty(prefKey) ? "PixelClicker.Window." + objectName : prefKey,
                                                w.HeaderHeight, firstRight, closeAt.y, ToolCloseSize, WindowButtonColor, Color.white, ToolCloseSize * 0.5f);
            if (w.Close != null) w.Chrome.Keep(w.Close.gameObject);
            w.Chrome.Keep(w.Title.gameObject);
        }
        return w;
    }
}
