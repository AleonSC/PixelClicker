// ---- System notes ----
// PixelCardBoard.cs
//   ONE draggable / foldable CARD BOARD, used by the Dev Hub's Main tab widgets (PixelDevTools) and Pocket's Pinned tab (PixelMyWindow) - they used
//   to have a copy each (PixelWidgetBoard / Pocket's own cards, merged 2026-10-24, to-do 62).
//   - The OWNER builds each card (a top-left anchored RectTransform with its own background and body) and hands it to Add(id, rect, height, title):
//     the board adds the HEADER strip you drag the card by (PixelCardGrip: the others slide out of the way, Layout), a grip icon, the title and a
//     '-' / '+' button that FOLDS the card up to its header (every child of the card except the header parts is hidden; KeepInHeader adds an owner's
//     own header button, e.g. Pocket's hold-to-remove x).
//   - Look per board: Style (header height, top padding, gap between cards, title insets, fold button spot, the Dev Hub's tall side grip).
//   - SAVING, two ways:
//     * PrefKey set (Dev Hub): the board saves the order, the folded cards (.Min) and the favorites (.Fav) in PixelPrefs under that key itself, and
//       Finish() puts them back (a card missing from the saved order goes right after the card built before it).
//     * PrefKey empty (Pocket): the owner keeps order / folds in its own data: OrderChanged (after a drag) and FoldChanged(card) tell it, and it
//       folds a card on build with SetFolded(card, true, false). Card.tag holds the owner's data for the card.
//   - Optional extras: Favorites (hold the RIGHT button on a card: it turns gold and stays above the others), SearchTab (each card is registered
//     with the Dev Hub's search bar; Reveal unfolds, scrolls to and flashes it), MinimizeChanged + AllMinimized / SetAllMinimized ('Collapse all').
//   onHeight(total) lets the owner size its scroll content (and place things under the last card).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PixelCardBoard
{
    /// <summary>The header height of a default card (the Dev Hub's widgets lay their controls out under it).</summary>
    public const float DefaultHeaderH = 30f;

    /// <summary>How a board's cards look (the two boards differ in a few pixels).</summary>
    public sealed class Style
    {
        public float headerHeight = DefaultHeaderH;   // the header strip: all a folded card shows
        public float topPad;                 // space above the first card
        public float gap = 8f;               // space between cards
        public float handleAlpha = 0.02f;    // the header strip's faint tint
        public float titleLeft = 44f, titleRight = 48f;   // the title's insets in the header (room for the grip / the buttons on the right)
        public Vector2 foldPos = new Vector2(-8f, -3f);   // the fold button, from the card's top-right corner
        public bool sideGrip;                // the Dev Hub's grip: a tall grip centred on the card's left edge you can drag by too (else a small grip in the header)
        public Color titleColor = new Color(0.85f, 0.9f, 1f, 0.9f);
        public Color cardColor = new Color(1f, 1f, 1f, 0.05f);   // the normal card background (used for the favorite / flash looks)
        public Color? foldColor;             // the fold button's own colour (null = the theme's Neutral button)
    }

    public sealed class Card
    {
        public string id, title;
        public RectTransform rect, grip;
        public float height, fullHeight;
        public bool folded, favorite;
        public Image bg;
        public TMP_Text foldLabel, titleLabel;
        public object tag;   // the owner's data for this card
        internal readonly List<GameObject> header = new List<GameObject>();   // what stays shown when folded
        internal readonly List<GameObject> hidden = new List<GameObject>();
    }

    private static readonly Color FavoriteColor = new Color(1f, 0.82f, 0.25f, 0.16f), FavoriteText = new Color(1f, 0.88f, 0.4f, 1f);
    private const float FavoriteHoldSeconds = 0.7f;

    private readonly RectTransform content, viewport;
    private readonly Style style;
    private readonly Action<float> onHeight;
    private readonly List<Card> cards = new List<Card>();
    private readonly PixelCardBoardDriver driver;
    private Card dragging;
    private float dragTop, total;
    private Card flashCard;
    private float flashUntil;

    /// <summary>When set, the board saves the order, the folded cards and the favorites in PixelPrefs under this key (else the owner saves them).</summary>
    public string PrefKey;
    /// <summary>Hold the right button on a card to make it a favorite (it stays above the others).</summary>
    public bool Favorites;
    /// <summary>The Dev Hub tab these cards live in: each card is registered with the hub's search bar (PixelDevHub.RegisterSearch).</summary>
    public string SearchTab;
    /// <summary>After a drag moved a card (the new order is Cards).</summary>
    public Action OrderChanged;
    /// <summary>After the player folded or unfolded a card.</summary>
    public Action<Card> FoldChanged;
    /// <summary>After any card folds or unfolds (the 'collapse all' button follows it).</summary>
    public Action MinimizeChanged;

    public IReadOnlyList<Card> Cards => cards;

    /// <summary>'viewport' = the visible window of the scrolling list the cards live in (hovering a card scrolled out of view does not count).</summary>
    public PixelCardBoard(RectTransform content, Style style, Action<float> onHeight, RectTransform viewport = null)
    {
        this.content = content; this.style = style ?? new Style(); this.onHeight = onHeight; this.viewport = viewport;
        driver = content.gameObject.AddComponent<PixelCardBoardDriver>();
        driver.board = this;
    }

    /// <summary>Forgets every card (the owner destroys their objects; it rebuilds them with Add).</summary>
    public void Clear()
    {
        cards.Clear();
        dragging = null;
        flashCard = null;
    }

    /// <summary>Adds a card (top-left anchored, 'height' tall) and gives it its header: drag strip, grip, title and fold button.</summary>
    public Card Add(string id, RectTransform rect, float height, string title = null)
    {
        Card card = new Card { id = id, rect = rect, height = height, fullHeight = height, title = string.IsNullOrEmpty(title) ? id : title };
        cards.Add(card);
        card.bg = rect.GetComponent<Image>();
        float H = style.headerHeight;
        PixelClicker owner = PixelFind.Cached<PixelClicker>();
        TMP_FontAsset font = owner != null ? owner.UIFont : null;

        // The whole header strip is the handle you drag the card by (a wide, easy target).
        GameObject hgo = new GameObject("Header Handle", typeof(RectTransform), typeof(Image), typeof(PixelCardGrip));
        hgo.transform.SetParent(rect, false);
        hgo.GetComponent<Image>().color = new Color(1f, 1f, 1f, style.handleAlpha);
        RectTransform hr = hgo.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(0f, H);
        hr.anchoredPosition = Vector2.zero;
        PixelCardGrip handle = hgo.GetComponent<PixelCardGrip>();
        handle.board = this; handle.card = card;
        card.header.Add(hgo);

        // The grip icon: tall on the card's left edge (and draggable there too) on the Dev Hub, a small one in the header on Pocket.
        GameObject g = new GameObject("Grip", typeof(RectTransform), typeof(Image));
        Image gim = g.GetComponent<Image>();
        gim.sprite = PixelUIKit.GripSprite();
        gim.color = new Color(1f, 1f, 1f, 0.45f);
        RectTransform gr = g.GetComponent<RectTransform>();
        gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(0f, 0.5f);
        if (style.sideGrip)
        {
            g.transform.SetParent(rect, false);
            gr.sizeDelta = new Vector2(26f, Mathf.Min(40f, height - 6f));
            PixelCardGrip grip = g.AddComponent<PixelCardGrip>();
            grip.board = this; grip.card = card;
            card.header.Add(g);
        }
        else
        {
            g.transform.SetParent(hr, false);
            gim.raycastTarget = false;
            gr.sizeDelta = new Vector2(24f, 26f);
        }
        gr.anchoredPosition = new Vector2(8f, 0f);
        card.grip = gr;

        // The title (always shown) and the '-' / '+' fold button at the header's right end.
        TMP_Text label = PixelUIKit.CreateText(font, rect, "Title", card.title.ToUpperInvariant(), 20f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, style.titleColor);
        label.raycastTarget = false;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0f, 1f);
        lr.offsetMin = new Vector2(style.titleLeft, -H); lr.offsetMax = new Vector2(-style.titleRight, 0f);
        card.titleLabel = label;
        card.header.Add(label.gameObject);
        Button fold = style.foldColor.HasValue
            ? PixelUIKit.CreateButton(font, rect, "Minimize", "-", new Vector2(30f, 24f), style.foldColor.Value, Color.white, 20f)
            : PixelUIKit.CreateButton(font, rect, "Minimize", "-", new Vector2(30f, 24f), ButtonRole.Neutral, 20f);
        RectTransform fr = fold.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(1f, 1f);
        fr.anchoredPosition = style.foldPos;
        card.foldLabel = fold.GetComponentInChildren<TMP_Text>();
        fold.onClick.AddListener(() => SetFolded(card, !card.folded, true));
        card.header.Add(fold.gameObject);

        if (!string.IsNullOrEmpty(SearchTab)) PixelDevHub.RegisterSearch("Widget: " + card.title, SearchTab, () => Reveal(card), card.id);
        return card;
    }

    /// <summary>An owner's own header part (a button in the header strip): it stays shown when the card is folded.</summary>
    public void KeepInHeader(Card card, GameObject go) { if (card != null && go != null && !card.header.Contains(go)) card.header.Add(go); }

    /// <summary>Search result picked: unfold the card, scroll it into view and flash it.</summary>
    public void Reveal(Card card)
    {
        if (card == null) return;
        if (card.folded) SetFolded(card, false, true);
        PixelUIKit.ScrollTo(content.GetComponentInParent<ScrollRect>(), card.rect);
        flashCard = card;
        flashUntil = Time.unscaledTime + 1.6f;
    }

    /// <summary>Are all the cards folded up?</summary>
    public bool AllMinimized { get { foreach (Card c in cards) if (!c.folded) return false; return cards.Count > 0; } }

    /// <summary>Folds every card up to its header (or unfolds them all).</summary>
    public void SetAllMinimized(bool on)
    {
        foreach (Card c in cards) if (SetFoldedCore(c, on)) FoldChanged?.Invoke(c);
        Layout();
        SaveFolded();
        MinimizeChanged?.Invoke();
    }

    /// <summary>Folds / unfolds a card. 'notify' = the player did it: save (PrefKey) and tell the owner (FoldChanged / MinimizeChanged); false = setting it up on build.</summary>
    public void SetFolded(Card card, bool on, bool notify)
    {
        if (!SetFoldedCore(card, on)) return;
        Layout();
        if (!notify) return;
        SaveFolded();
        FoldChanged?.Invoke(card);
        MinimizeChanged?.Invoke();
    }

    private bool SetFoldedCore(Card card, bool on)
    {
        if (card == null || card.folded == on) return false;
        card.folded = on;
        if (on)
        {
            card.hidden.Clear();
            foreach (Transform child in card.rect)
                if (child.gameObject.activeSelf && !card.header.Contains(child.gameObject)) { card.hidden.Add(child.gameObject); child.gameObject.SetActive(false); }
            card.height = style.headerHeight;
        }
        else
        {
            foreach (GameObject g in card.hidden) if (g != null) g.SetActive(true);
            card.hidden.Clear();
            card.height = card.fullHeight;
        }
        if (card.foldLabel != null) card.foldLabel.text = on ? "+" : "-";
        card.rect.sizeDelta = new Vector2(card.rect.sizeDelta.x, card.height);
        if (style.sideGrip) card.grip.sizeDelta = new Vector2(26f, Mathf.Min(40f, card.height - 6f));
        return true;
    }

    /// <summary>Puts back the saved order / folds / favorites (PrefKey boards) and lays everything out. Call once after adding every card.</summary>
    public void Finish()
    {
        if (!string.IsNullOrEmpty(PrefKey))
        {
            string saved = PixelPrefs.GetString(PrefKey, "");
            if (saved.Length > 0)
            {
                List<Card> ordered = new List<Card>();
                foreach (string id in saved.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    Card c = cards.Find(x => x.id == id);
                    if (c != null && !ordered.Contains(c)) ordered.Add(c);
                }
                // A card that is not in the saved order (a widget added since) goes right after the card built before it, not at the very end of a long list.
                for (int i = 0; i < cards.Count; i++)
                {
                    Card c = cards[i];
                    if (ordered.Contains(c)) continue;
                    int at = 0;
                    for (int k = i - 1; k >= 0; k--) { int p = ordered.IndexOf(cards[k]); if (p >= 0) { at = p + 1; break; } }
                    ordered.Insert(at, c);
                }
                cards.Clear();
                cards.AddRange(ordered);
            }
            foreach (string id in PixelPrefs.GetString(PrefKey + ".Min", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                SetFoldedCore(cards.Find(x => x.id == id), true);
            if (Favorites)
                foreach (string id in PixelPrefs.GetString(PrefKey + ".Fav", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    Card c = cards.Find(x => x.id == id);
                    if (c != null) { c.favorite = true; ApplyFavoriteLook(c); }
                }
        }
        Partition();
        Layout();
    }

    /// <summary>Puts every card in its place (top to bottom) and reports the total height.</summary>
    public void Layout()
    {
        float y = style.topPad;
        foreach (Card c in cards)
        {
            if (c != dragging) c.rect.anchoredPosition = new Vector2(0f, -y);
            y += c.height + style.gap;
        }
        total = y;
        onHeight?.Invoke(total);
    }

    private float CanvasScale { get { float s = content.lossyScale.x; return s > 0.0001f ? s : 1f; } }   // screen pixels per unit

    internal void BeginDrag(Card card)
    {
        dragging = card;
        dragTop = -card.rect.anchoredPosition.y;
        card.rect.SetAsLastSibling();
    }

    internal void Drag(Card card, float screenDeltaY)
    {
        if (dragging != card) return;
        dragTop = Mathf.Clamp(dragTop - screenDeltaY / CanvasScale, 0f, Mathf.Max(0f, total - style.gap - card.height));
        card.rect.anchoredPosition = new Vector2(0f, -dragTop);
        // The slot it would take: how many of the others sit above its centre.
        float centre = dragTop + card.height * 0.5f, y = style.topPad;
        int index = 0;
        foreach (Card other in cards)
        {
            if (other == card) continue;
            if (y + other.height * 0.5f < centre) index++;
            y += other.height + style.gap;
        }
        int from = cards.IndexOf(card);
        if (index != from) { cards.RemoveAt(from); cards.Insert(index, card); }
        Layout();
    }

    internal void EndDrag(Card card)
    {
        if (dragging != card) return;
        dragging = null;
        Partition();   // favorites always stay above the rest
        Layout();
        SaveOrder();
        OrderChanged?.Invoke();
    }

    private void SaveOrder()
    {
        if (string.IsNullOrEmpty(PrefKey)) return;
        List<string> ids = new List<string>();
        foreach (Card c in cards) ids.Add(c.id);
        PixelPrefs.SetString(PrefKey, string.Join(",", ids));
    }

    private void SaveFolded()
    {
        if (string.IsNullOrEmpty(PrefKey)) return;
        List<string> ids = new List<string>();
        foreach (Card c in cards) if (c.folded) ids.Add(c.id);
        PixelPrefs.SetString(PrefKey + ".Min", string.Join(",", ids));
    }

    /// <summary>Favorites first (keeping their order), then the others (keeping theirs).</summary>
    private void Partition()
    {
        if (!Favorites) return;
        List<Card> favs = cards.FindAll(x => x.favorite), rest = cards.FindAll(x => !x.favorite);
        cards.Clear();
        cards.AddRange(favs);
        cards.AddRange(rest);
    }

    private void ApplyFavoriteLook(Card card)
    {
        if (card.bg != null) card.bg.color = card.favorite ? FavoriteColor : style.cardColor;
        if (card.titleLabel != null) card.titleLabel.color = card.favorite ? FavoriteText : style.titleColor;
    }

    /// <summary>Hold the right button on a card: it becomes a favorite and snaps to the top (or, if it already was one, goes back to the top of the others).</summary>
    private void ToggleFavorite(Card card)
    {
        card.favorite = !card.favorite;
        ApplyFavoriteLook(card);
        cards.Remove(card);
        if (card.favorite) cards.Insert(0, card);
        else cards.Insert(cards.FindAll(x => x.favorite).Count, card);
        Layout();
        SaveOrder();
        OrderChanged?.Invoke();
        if (!string.IsNullOrEmpty(PrefKey))
        {
            List<string> favs = new List<string>();
            foreach (Card c in cards) if (c.favorite) favs.Add(c.id);
            PixelPrefs.SetString(PrefKey + ".Fav", string.Join(",", favs));
        }
        PixelAudio.Play("ui_click");
    }

    /// <summary>The card under the pointer (inside the visible part of the list), or null.</summary>
    public Card CardUnderPointer()
    {
        Vector2 pointer = PixelInput.PointerPosition();
        if (viewport != null && !RectTransformUtility.RectangleContainsScreenPoint(viewport, pointer, null)) return null;
        foreach (Card c in cards)
            if (c.rect != null && c.rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(c.rect, pointer, null)) return c;
        return null;
    }

    internal void Tick()
    {
        if (flashCard != null)
        {
            if (Time.unscaledTime >= flashUntil) { ApplyFavoriteLook(flashCard); flashCard = null; }
            else if (flashCard.bg != null) flashCard.bg.color = Color.Lerp(flashCard.favorite ? FavoriteColor : style.cardColor, new Color(0.4f, 0.8f, 1f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f));
        }
        if (!Favorites) return;
        Card target = PixelInput.RightPressed() || PixelInput.RightHeld() ? CardUnderPointer() : null;
        if (PixelHold.Update(driver, target, FavoriteHoldSeconds, "", FavoriteText, true, null) && target != null) ToggleFavorite(target);
    }
}

/// <summary>Runs a card board's per-frame checks (the right-hold for a favorite, the search flash); the board itself is a plain class.</summary>
public class PixelCardBoardDriver : MonoBehaviour
{
    [System.NonSerialized] public PixelCardBoard board;   // (set in code at run time; never saved)
    private void Update() { if (board != null && gameObject.activeInHierarchy) board.Tick(); }
}

/// <summary>A card's header strip (and the Dev Hub's side grip): dragging it slides the card up or down.</summary>
public class PixelCardGrip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [System.NonSerialized] public PixelCardBoard board;   // (set in code at run time; never saved)
    [System.NonSerialized] public PixelCardBoard.Card card;
    public void OnBeginDrag(PointerEventData e) { board.BeginDrag(card); }
    public void OnDrag(PointerEventData e) { board.Drag(card, e.delta.y); }
    public void OnEndDrag(PointerEventData e) { board.EndDrag(card); }
}
