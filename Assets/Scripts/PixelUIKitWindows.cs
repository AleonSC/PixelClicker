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
//   Extra header buttons go in the standard slots with parts.AddHeaderButton(name, label, left, colour) (same size as the X: left slots
//   from the top-left corner, right slots just left of the X); anything else (an info badge, an icon) with parts.PlaceInHeaderSlot(rect, left).
//   The title's room grows with them (FitTitle), so a title never runs under a header item. headerHeight override (only when the header must hold more) and styled: false
//   (a plain background, the Collection Log's book) are the two allowed exceptions.
//   Every window gets StyleWindow, a PixelPop (open / close animation; pop: false for windows rebuilt / re-shown often or that scale themselves) and its X wired to the given onClose. The owner still
//   registers with PixelWindows (Escape) and builds its own content below HeaderHeight. Dialogs that close with their own
//   Done / Continue / Next button pass closeButton: false (no X).
//   Windows moved onto it so far: View (PixelViewWindow).
//   FitBesideShop(window, shop, aloneSize): docks a right-edge window beside the open shop, mirroring the Currency panel (Pixel Guide, Toggles).
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

    internal TMP_FontAsset Font;
    internal bool Tool;
    private int leftButtons, rightButtons;

    /// <summary>
    /// An extra header button in the standard slots (same size as the X): left = from the top-left corner inwards (pin, book, hose, info, expand...),
    /// right = just left of the X (and of a Tool window's minimize), going leftwards (a ? help button...). Label "" = an icon button (add the icon yourself).
    /// </summary>
    public Button AddHeaderButton(string objectName, string label, bool left, ButtonRole role = ButtonRole.Neutral)
    {
        Button b = AddHeaderButton(objectName, label, left, PixelUITheme.ColorOf(role));
        PixelUIKit.SetRole(b, role);
        return b;
    }

    /// <summary>AddHeaderButton with its own colour (role Custom) - for a button whose colour shows a state of its own (the Bank's hose on / off).</summary>
    public Button AddHeaderButton(string objectName, string label, bool left, Color color)
    {
        float size = Tool ? PixelUIKit.ToolCloseSize : PixelUIKit.WindowCloseSize;
        Button b = PixelUIKit.CreateButton(Font, Rect, objectName, label, new Vector2(size, size), color, Color.white, size * 0.4f);
        PlaceInHeaderSlot(b.GetComponent<RectTransform>(), left);
        return b;
    }

    /// <summary>
    /// Puts any header item (a button made elsewhere, an info badge, an icon) into the next standard slot, sized like the X; the title's
    /// room shrinks so it never runs under it. AddHeaderButton uses this too.
    /// </summary>
    public void PlaceInHeaderSlot(RectTransform r, bool left)
    {
        float size = Tool ? PixelUIKit.ToolCloseSize : PixelUIKit.WindowCloseSize;
        Vector2 at = Tool ? PixelUIKit.ToolCloseOffset : PixelUIKit.WindowCloseOffset;
        r.SetParent(Rect, false);
        r.sizeDelta = new Vector2(size, size);
        if (left)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(at.x + leftButtons++ * (size + 8f), -at.y);
        }
        else
        {
            int taken = (Close != null ? 1 : 0) + (Chrome != null ? 1 : 0) + rightButtons++;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-(at.x + taken * (size + 8f)), -at.y);
        }
        if (Chrome != null) Chrome.Keep(r.gameObject);
        FitTitle();
    }

    /// <summary>Keeps the centred title clear of every header item on both sides (same room left and right so it stays centred).</summary>
    internal void FitTitle()
    {
        if (Title == null) return;
        float size = Tool ? PixelUIKit.ToolCloseSize : PixelUIKit.WindowCloseSize;
        Vector2 at = Tool ? PixelUIKit.ToolCloseOffset : PixelUIKit.WindowCloseOffset;
        int right = (Close != null ? 1 : 0) + (Chrome != null ? 1 : 0) + rightButtons;
        int most = Mathf.Max(leftButtons, right);
        float room = most > 0 ? at.x + most * (size + 8f) : 16f;
        RectTransform tr = Title.rectTransform;
        tr.offsetMin = new Vector2(room, tr.offsetMin.y);
        tr.offsetMax = new Vector2(-room, tr.offsetMax.y);
    }

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

    // Titles made by CreateWindow, recoloured live when the theme's title colour changes (a title given its own colour afterwards is left alone).
    private static readonly System.Collections.Generic.List<TMP_Text> titles = new System.Collections.Generic.List<TMP_Text>();
    private static Color titleColorShown = Color.white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTitleStatics()   // (a fresh play mode start: no titles yet, the start colour; play mode may skip the domain reload)
    {
        titles.Clear();
        titleColorShown = Color.white;
    }

    /// <summary>Re-applies PixelUITheme's title colour to every window title still in the theme colour.</summary>
    public static void RefreshTitles()
    {
        Color now = PixelUITheme.TitleColor;
        for (int i = titles.Count - 1; i >= 0; i--)
        {
            TMP_Text t = titles[i];
            if (t == null) { titles.RemoveAt(i); continue; }
            Color c = t.color;
            if (Mathf.Abs(c.r - titleColorShown.r) + Mathf.Abs(c.g - titleColorShown.g) + Mathf.Abs(c.b - titleColorShown.b) < 0.02f) t.color = now;
        }
        titleColorShown = now;
    }

    /// <summary>
    /// CreateWindow in the THEME's window colour (Fixed windows and dialogs: PixelUITheme's window colour, Tool windows: its tool window colour),
    /// following the theme live. Prefer this; the overload with a colour is for windows with a look of their own (the leather Collection Log, Cubie's teal).
    /// </summary>
    public static PixelWindowParts CreateWindow(TMP_FontAsset font, Transform parent, string objectName, string title, Vector2 size,
                                                WindowKind kind, System.Action onClose, string prefKey = null, bool closeButton = true,
                                                float headerHeight = 0f, bool pop = true)
    {
        WindowShade shade = kind == WindowKind.Tool ? WindowShade.ToolWindow : WindowShade.Window;
        PixelWindowParts w = CreateWindow(font, parent, objectName, title, size, PixelUITheme.ColorOf(shade), kind, onClose, prefKey, closeButton, headerHeight, false, pop);
        StyleWindow(w.Root.GetComponent<Image>(), shade);
        return w;
    }

    // ---- The standard (docs/systems/WindowInventory.md, chosen 2026-10-18). One place: change a number here and every window follows. ----
    public const float WindowHeaderHeight = 90f, WindowTitleSize = 48f, WindowCloseSize = 70f;
    public static readonly Vector2 WindowCloseOffset = new Vector2(14f, 12f);   // from the top-right corner
    public const float ToolHeaderHeight = 56f, ToolTitleSize = 30f, ToolCloseSize = 44f;
    public static readonly Vector2 ToolCloseOffset = new Vector2(8f, 6f);
    /// <summary>The Neutral button colour (the X and header buttons are role Neutral; see PixelUITheme). Kept for code that needs the colour itself.</summary>
    public static Color WindowButtonColor => PixelUITheme.ColorOf(ButtonRole.Neutral);

    /// <summary>
    /// A window in the standard look (see the header of PixelUIKitWindows.cs). 'parent' is a full-screen canvas. Tool windows need a
    /// prefKey (where their position / minimized / docked state is saved). 'onClose' runs when the X is clicked.
    /// </summary>
    /// <param name="headerHeight">0 = the standard; only for a window whose header must hold more (a search box, two rows) - say why at the call.</param>
    /// <param name="styled">false = keep a plain background in 'background' instead of StyleWindow (the Collection Log's leather book).</param>
    /// <param name="pop">false = no PixelPop (no grow-in / open sound): a window that is rebuilt or re-shown often (the UFO's pages, Next up after the title
    /// screen) or that scales itself (the Dev Hub's resize grip, which PixelPop would keep undoing).</param>
    public static PixelWindowParts CreateWindow(TMP_FontAsset font, Transform parent, string objectName, string title, Vector2 size, Color background,
                                                WindowKind kind, System.Action onClose, string prefKey = null, bool closeButton = true,
                                                float headerHeight = 0f, bool styled = true, bool pop = true)
    {
        bool tool = kind == WindowKind.Tool;
        PixelWindowParts w = new PixelWindowParts { HeaderHeight = headerHeight > 0f ? headerHeight : tool ? ToolHeaderHeight : WindowHeaderHeight, Font = font, Tool = tool };

        w.Root = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        if (pop) w.Root.AddComponent<PixelPop>();
        w.Root.transform.SetParent(parent, false);
        if (styled) StyleWindow(w.Root.GetComponent<Image>(), background);
        else w.Root.GetComponent<Image>().color = background;
        w.Rect = w.Root.GetComponent<RectTransform>();
        Vector2 anchor = tool ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);   // (Tool: top left, for PixelWindowChrome)
        w.Rect.anchorMin = w.Rect.anchorMax = w.Rect.pivot = anchor;
        w.Rect.sizeDelta = size;

        float closeSize = tool ? ToolCloseSize : WindowCloseSize;
        Vector2 closeAt = tool ? ToolCloseOffset : WindowCloseOffset;
        float sideRoom = closeButton ? closeSize + closeAt.x + 8f : 16f;
        if (tool) sideRoom += ToolCloseSize + 6f;   // (room for the minimize button too)

        w.Title = CreateText(font, w.Root.transform, "Title", title, tool ? ToolTitleSize : WindowTitleSize, TextAlignmentOptions.Center, FontStyles.Bold, PixelUITheme.TitleColor);
        if (titles.Count > 400) titles.RemoveAll(t => t == null);
        titles.Add(w.Title);
        Caps(w.Title);
        w.Title.raycastTarget = false;
        w.Title.enableAutoSizing = true;
        w.Title.fontSizeMax = tool ? ToolTitleSize : WindowTitleSize;
        w.Title.fontSizeMin = 14f;
        RectTransform tr = w.Title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(sideRoom, -Mathf.Min(w.HeaderHeight, tool ? ToolHeaderHeight : WindowHeaderHeight));   // centred in the standard header strip, clear of the buttons on both sides
        tr.offsetMax = new Vector2(-sideRoom, 0f);

        if (closeButton)
        {
            w.Close = CreateButton(font, w.Root.transform, "Close", "X", new Vector2(closeSize, closeSize), ButtonRole.Neutral, closeSize * 0.4f);
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
            foreach (string chromeButton in new[] { "Chrome Minimize", "Chrome Undock" })   // (the chrome's own buttons are Neutral too)
            {
                Transform t = w.Rect.Find(chromeButton);
                if (t != null) SetRole(t.GetComponent<Button>(), ButtonRole.Neutral);
            }
        }
        w.FitTitle();
        return w;
    }

    /// <summary>
    /// Docks a right-edge window BESIDE THE OPEN SHOP (Pixel Guide, Toggles): as tall as the shop and as wide as the Currency panel, flush against
    /// the shop's right side with the Currency panel's gap (mirroring it). With the shop closed (or 'shop' null) the window gets 'aloneSize' at the
    /// right edge, just in from the HUD's side bar. The window must be anchored at the middle of the right edge. Returns true when it is beside the shop.
    /// </summary>
    public static bool FitBesideShop(RectTransform window, PixelShop shop, Vector2 aloneSize)
    {
        RectTransform shopRect = shop != null ? shop.PanelRect : null;
        Canvas canvas = window.GetComponentInParent<Canvas>();
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor) : 1f;
        PixelHud hud = PixelHud.Instance;
        float inset = hud != null ? hud.SideShownInset : 0f;

        if (shopRect == null || !shopRect.gameObject.activeInHierarchy)
        {
            window.sizeDelta = aloneSize;
            window.anchoredPosition = new Vector2(-inset, 0f);
            return false;
        }
        Vector3[] c = new Vector3[4];
        shopRect.GetWorldCorners(c);   // screen pixels (overlay canvas)
        float shopRight = c[2].x, shopTop = c[2].y, shopBottom = c[0].y;
        float width = shop.CurrencyPanelWidth;
        float gap = shop.CurrencyPanelGap;
        float rightEdge = (Screen.width - shopRight) / scale - gap - width;
        window.sizeDelta = new Vector2(width, (shopTop - shopBottom) / scale);
        window.anchoredPosition = new Vector2(-Mathf.Max(0f, rightEdge), ((shopTop + shopBottom) * 0.5f - Screen.height * 0.5f) / scale);
        return true;
    }
}
