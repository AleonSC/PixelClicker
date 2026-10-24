// ---- System notes ----
// PixelUIRefresh.cs
//   Part of PixelUI (partial class): Refresh (the Currency list every frame): the rows (swatch, name, amount), the name / compact settings, the folded currency groups, the compact look and the list viewport. The system notes are in PixelUI.cs.
// ---------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public partial class PixelUI
{
    // ------------------------------------------------------------------
    // Refresh
    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Rows: a small coloured swatch, the name in plain light text, and the amount right-aligned in its own column.
    // ------------------------------------------------------------------

    // The row text is always this light colour (the scene's own 'Text Color' may be set to something else from when lines were coloured by tier).
    private static readonly Color RowTextColor = new Color(0.94f, 0.95f, 0.98f, 1f);

    private const string PrefCurrencyNames = "PixelClicker.Setting.InventoryNames";
    private static int currencyNamesCache = -1;

    /// <summary>Currency rows: true = the name beside its amount, false (the default) = just the swatch and the number (hover for the name; click to switch).</summary>
    public static bool CurrencyShowNames
    {
        get
        {
            if (currencyNamesCache < 0) currencyNamesCache = PixelPrefs.GetInt(PrefCurrencyNames, 0);
            return currencyNamesCache != 0;
        }
        set
        {
            currencyNamesCache = value ? 1 : 0;
            PixelPrefs.SetInt(PrefCurrencyNames, currencyNamesCache);
        }
    }

    private const string PrefCompact = "PixelClicker.Setting.InventoryCompact";
    private static int compactCache = -1;

    /// <summary>The pinned, simplified Inventory: just swatches and numbers (currency, then the consumables you can use with a click).</summary>
    public static bool Compact
    {
        get { return CompactMode != 0; }
        set { CompactMode = value ? 1 : 0; }
    }

    /// <summary>0 = off, 1 = the Currency tab pinned (swatches + numbers), 2 = the Consumables tab pinned.</summary>
    public static int CompactMode
    {
        get
        {
            // Read every time (not cached): a new game / loaded save puts its own value in the prefs, and a stale cache showed the pinned view.
            compactCache = PixelPrefs.GetInt(PrefCompact, 0);
            return compactCache;
        }
        set
        {
            compactCache = Mathf.Clamp(value, 0, 2);
            PixelPrefs.SetInt(PrefCompact, compactCache);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInventoryStatics() { currencyNamesCache = -1; compactCache = -1; }

    private struct RowParts { public TMP_Text amount; public Image swatch; }
    private readonly Dictionary<TMP_Text, RowParts> rowParts = new Dictionary<TMP_Text, RowParts>();

    /// <summary>The name without its "Pixels" suffix ("White Pixels" -> "White"): the Currency tab is about pixels already.</summary>
    private static string ShortName(string name)
    {
        if (name.EndsWith(" Pixels")) return name.Substring(0, name.Length - 7);
        if (name.EndsWith(" Pixel")) return name.Substring(0, name.Length - 6);
        return name;
    }

    private RowParts BuildRowParts(TMP_Text label)
    {
        float swatchSize = Mathf.Round(LinePitch * 0.8f);   // a clear colour chip
        float deltaSpace = fontSize * 1.5f;   // the "+N" indicator keeps the far right
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.margin = new Vector4(swatchSize + 18f, 0f, marginsCompact ? 8f : 110f, 0f);   // the name never runs under the amount (the pinned window has no amount column)
        label.enableAutoSizing = true;                                // a long name shrinks to fit on ONE line instead of wrapping
        label.fontSizeMax = fontSize;
        label.fontSizeMin = Mathf.Max(8f, fontSize * 0.3f);
        label.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        label.textWrappingMode = TextWrappingModes.NoWrap;
#else
        label.enableWordWrapping = false;
#endif

        GameObject sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        sw.transform.SetParent(label.transform, false);
        Image swImage = sw.GetComponent<Image>();
        swImage.raycastTarget = false;
        RectTransform sr = sw.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(swatchSize, swatchSize);
        sr.anchoredPosition = new Vector2(4f, 0f);
        PixelUIKit.StyleButtonLook(swImage);   // (the button shape only: the swatch shows the pixel's own colour)

        TMP_Text amount = MakeText(label.transform, "Amount", "", fontSize, TextAlignmentOptions.MidlineRight, FontStyles.Bold, RowTextColor);
        amount.raycastTarget = false;
        amount.enableAutoSizing = true;
        amount.fontSizeMax = fontSize;
        amount.fontSizeMin = Mathf.Max(8f, fontSize * 0.3f);
        amount.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        amount.textWrappingMode = TextWrappingModes.NoWrap;
#else
        amount.enableWordWrapping = false;
#endif
        RectTransform ar = amount.rectTransform;
        ar.anchorMin = new Vector2(0.55f, 0f);
        ar.anchorMax = Vector2.one;
        ar.offsetMin = Vector2.zero;
        ar.offsetMax = new Vector2(-deltaSpace, 0f);

        RowParts parts = new RowParts { amount = amount, swatch = swImage };
        rowParts[label] = parts;
        return parts;
    }

    /// <summary>Fills one row: name (left, light), amount (right) and the swatch colour.</summary>
    private void SetRow(TMP_Text label, string name, string amount, Color swatch, bool dim = false, bool animatedSwatch = false)
    {
        if (!rowParts.TryGetValue(label, out RowParts parts)) parts = BuildRowParts(label);
        PixelUIKit.SetText(label, name);
        PixelUIKit.SetText(parts.amount, amount);
        Color text = dim ? lockedColor : RowTextColor;
        label.color = text;
        parts.amount.color = text;
        if (animatedSwatch)
        {
            if (parts.swatch.GetComponent<PixelUltraSwatch>() == null) parts.swatch.gameObject.AddComponent<PixelUltraSwatch>();   // it colours itself
        }
        else parts.swatch.color = dim ? new Color(swatch.r, swatch.g, swatch.b, 0.35f) : swatch;
    }

    /// <summary>Writes the current amounts into the texts (and stacks visible lines in automatic mode).</summary>
    public void Refresh()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        if (autoMode)
        {
            if (buttonLabel != null) PixelUIKit.SetText(buttonLabel, inventoryButtonText);
            if (titleLabel != null) PixelUIKit.SetText(titleLabel, inventoryTitle);
        }
        if (autoMode) UpdateSubTabs();
        bool compact = autoMode && Compact;
        if (autoMode) ApplyCompactLook(compact);
        bool names = CurrencyShowNames && !compact;
        bool showCurrency = !autoMode || (compact ? CompactMode == 1 : inventoryTab == 0);
        float y = 0f; // lines are laid out inside the scrolling list (its top = just below the tabs)

        // The Ultra pixel row comes first, set apart from the pixel counts by a thin line.
        y = LayoutUltraRow(showCurrency, names, y);

        // Pass 1: which rows show, and what they say.
        bool[] shown = FillTierRows(tiers, showCurrency, names);

        // Pass 2 (automatic mode): lay the rows out in groups - Pure, Composite (one per skill), Exotic - each under a header you can fold.
        if (autoMode) y = LayoutCurrencyGroups(tiers, shown, compact, y);

        if (autoMode) y = RefreshTabContent(compact, y);

        // Box height follows the number of visible lines.
        if (autoMode && boxRect != null) SizeBox(compact, y);
    }

    // Refresh step: the Ultra pixel row at the top, set apart by a thin line; returns the next row's y.
    private float LayoutUltraRow(bool showCurrency, bool names, float y)
    {
        bool showUltra = autoMode && showCurrency && clicker.UltraEarned > 0;
        if (ultraLabel != null)
        {
            if (ultraLabel.gameObject.activeSelf != showUltra) ultraLabel.gameObject.SetActive(showUltra);
            if (ultraDivider.gameObject.activeSelf != showUltra) ultraDivider.gameObject.SetActive(showUltra);
        }
        if (showUltra && ultraLabel != null)
        {
            ultraLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            string ultraAmount = FormatAmount(clicker.TotalUltra);
            if (names) SetRow(ultraLabel, "Ultra", ultraAmount, Color.white, false, true);
            else SetRow(ultraLabel, ultraAmount, "", Color.white, false, true);
            y += LinePitch;
            ultraDivider.rectTransform.anchoredPosition = new Vector2(0f, -(y + 4f));
            y += 12f;
        }
        return y;
    }

    // Refresh step (pass 1): which tier rows show, and what they say.
    private bool[] FillTierRows(PixelClicker.PixelTier[] tiers, bool showCurrency, bool names)
    {
        bool[] shown = new bool[tierLabels.Length];
        for (int i = 0; i < tierLabels.Length && i < tiers.Length; i++)
        {
            TMP_Text label = tierLabels[i];
            if (label == null) continue;

            PixelClicker.PixelTier tier = tiers[i];
            bool holding = tier.count > 0d; // e.g. a Starting Amount on a tier that is still locked
            bool visible = (tier.unlocked || holding || (showLockedTiers && !tier.rareDrop)) && showCurrency;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            if (!visible) continue;
            shown[i] = true;

            string amount = (tier.unlocked || holding) ? FormatAmount(tier.count) : lockedText;
            if (names) SetRow(label, ShortName(tier.displayName), amount, tier.UIColor, !tier.unlocked && !holding);
            else SetRow(label, amount, "", tier.UIColor, !tier.unlocked && !holding);   // swatch + number; the name is in the hover tip
        }
        return shown;
    }

    // Refresh step (pass 2, automatic mode): lays the rows out in groups - Pure, Composite (one per skill), Exotic - each under a header you can fold; returns y.
    private float LayoutCurrencyGroups(PixelClicker.PixelTier[] tiers, bool[] shown, bool compact, float y)
    {
        List<string> groupOrder = new List<string> { "pure" };
        Dictionary<string, string> groupTitles = new Dictionary<string, string> { { "pure", "Pure" }, { "exotic", "Exotic" } };
        Dictionary<string, List<int>> groupRows = new Dictionary<string, List<int>> { { "pure", new List<int>() }, { "exotic", new List<int>() } };
        for (int i = 0; i < shown.Length && i < tiers.Length; i++)
        {
            if (!shown[i]) continue;
            string key = CurrencyGroup(tiers[i], out string title);
            if (!groupRows.ContainsKey(key)) { groupRows[key] = new List<int>(); groupTitles[key] = title; groupOrder.Add(key); }
            groupRows[key].Add(i);
        }
        groupOrder.Add("exotic");

        int header = 0;
        foreach (string key in groupOrder)
        {
            List<int> rows = groupRows[key];
            if (rows.Count == 0) continue;
            bool folded = !compact && CurrencyFolded(key);
            if (!compact)
            {
                TMP_Text h = CurrencyHeader(header++);
                h.gameObject.SetActive(true);
                PixelUIKit.SetText(h, (folded ? "+ " : "- ") + groupTitles[key]);
                h.rectTransform.anchoredPosition = new Vector2(0f, -y);
                currencyHeaderKeys[header - 1] = key;
                y += LinePitch * 0.85f;
            }
            foreach (int i in rows)
            {
                TMP_Text label = tierLabels[i];
                if (folded) { if (label.gameObject.activeSelf) label.gameObject.SetActive(false); continue; }
                label.rectTransform.anchoredPosition = new Vector2(0f, -y);
                y += LinePitch;
            }
        }
        for (int k = header; k < currencyHeaders.Count; k++) if (currencyHeaders[k].gameObject.activeSelf) currencyHeaders[k].gameObject.SetActive(false);
        return y;
    }

    // Refresh step (automatic mode): the Consumables / Materials tabs (or the compact view's pinned consumables); returns y.
    private float RefreshTabContent(bool compact, float y)
    {
        if (compact)
        {
            HideMaterialWidgets();
            HideConsumableWidgets(keepRows: CompactMode == 2);   // the pinned consumable rows stay active: switching them off and on each refresh would kill their hover and clicks
            if (CompactMode == 2) y = RefreshCompactConsumables(y);
        }
        else
        {
            if (inventoryTab == 1)
            {
                if (categoryDropdown != null)
                {
                    SetCategoryRowActive(true);
                    if (categoryDropdown.value != consumableSubTab) categoryDropdown.SetValueWithoutNotify(consumableSubTab);
                }
                y = RefreshConsumables(0f);
            }
            else HideConsumableWidgets();

            if (inventoryTab == 2) y = RefreshMaterials(0f);
            else HideMaterialWidgets();
        }
        return y;
    }

    // Refresh step (automatic mode): the box height follows the number of visible lines; the list scrolls when it is taller.
    private void SizeBox(bool compact, float y)
    {
        float boxHeight = compact ? (compactSmall ? CompactTop + panelPadding * 0.5f : Mathf.Min(maxPanelHeight, ContentTop + y + panelPadding + 4f))   // the simplified one is only as tall as its lines (minimised: just the button row)
                                  : maxPanelHeight;   // one fixed size on every tab, so the window never changes shape
        ApplyRowMargins(compact);
        boxRect.sizeDelta = new Vector2(compact ? FitCompactWidth() : panelWidth, boxHeight);

        // Keep the list just below the tabs (the header height can change after the box is built).
        listViewport.offsetMin = new Vector2(0f, panelPadding);
        listViewport.offsetMax = new Vector2(0f, -ContentTop);
        if (listViewport.gameObject.activeSelf == (compact && compactSmall)) listViewport.gameObject.SetActive(!(compact && compactSmall));

        // The list scrolls when it is taller than the room the box has for it.
        float viewHeight = boxHeight - ContentTop - panelPadding;
        listContent.sizeDelta = new Vector2(0f, Mathf.Max(y, 1f));
        bool scrolls = y > viewHeight + 0.5f;
        if (listBarObject != null && listBarObject.activeSelf != scrolls) listBarObject.SetActive(scrolls);
        if (!scrolls) listContent.anchoredPosition = Vector2.zero;
        ScrollRect sr = listViewport.GetComponent<ScrollRect>();
        if (sr != null) sr.enabled = !compact || scrolls;   // a pinned list that doesn't scroll lets the drag through, so the whole window can be moved
    }

    // ---- Currency groups: Pure (the base colours), Composite (a skill's mixed goods, one group per skill) and Exotic (everything else) ----

    private readonly List<TMP_Text> currencyHeaders = new List<TMP_Text>();
    private string[] currencyHeaderKeys = new string[16];
    private const string PrefFolded = "PixelClicker.Setting.InventoryFolded";
    private static HashSet<string> foldedGroups;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFoldedGroups() { foldedGroups = null; }

    private static bool CurrencyFolded(string key)
    {
        if (foldedGroups == null)
        {
            foldedGroups = new HashSet<string>();
            foreach (string part in PixelPrefs.GetString(PrefFolded, "").Split(',')) if (part.Length > 0) foldedGroups.Add(part);
        }
        return foldedGroups.Contains(key);
    }

    private static void ToggleCurrencyFolded(string key)
    {
        CurrencyFolded(key);   // loads the set
        if (!foldedGroups.Remove(key)) foldedGroups.Add(key);
        PixelPrefs.SetString(PrefFolded, string.Join(",", foldedGroups));
    }

    /// <summary>Which group a pixel type's row is listed under: 'pure' = White..Blue, 'composite.&lt;skill&gt;' = a skill pack's goods, else 'exotic'.</summary>
    private static string CurrencyGroup(PixelClicker.PixelTier tier, out string title)
    {
        PixelClicker.PixelType t = tier.type;
        title = "Pure";
        if (t >= PixelClicker.PixelType.White && t <= PixelClicker.PixelType.Blue) return "pure";
        PixelSkills skills = PixelSkills.Instance;
        PixelSkills.SkillPack pack = skills != null ? skills.PackOfType(t) : null;
        if (pack != null && pack.id != PixelSkills.StandardId)
        {
            title = "Composite: " + pack.displayName;
            return "composite." + pack.id;
        }
        title = "Exotic";
        return "exotic";
    }

    private TMP_Text CurrencyHeader(int index)
    {
        while (currencyHeaders.Count <= index)
        {
            int slot = currencyHeaders.Count;
            TMP_Text h = MakeText(ListParent, "Group Header " + slot, "", fontSize * 0.68f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, new Color(0.62f, 0.72f, 0.88f, 1f));
            PixelUIKit.Caps(h);
            RectTransform rt = h.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-panelPadding * 2f, LinePitch * 0.85f);
            h.margin = new Vector4(6f, 0f, 0f, 0f);
            h.raycastTarget = true;
            h.gameObject.AddComponent<PixelClickable>().onClick = () =>
            {
                if (slot < currencyHeaderKeys.Length && currencyHeaderKeys[slot] != null) { ToggleCurrencyFolded(currencyHeaderKeys[slot]); Refresh(); }
            };
            currencyHeaders.Add(h);
            if (currencyHeaderKeys.Length <= slot) System.Array.Resize(ref currencyHeaderKeys, slot + 8);
        }
        return currencyHeaders[index];
    }

    /// <summary>Hides / shows the parts the simplified Inventory drops (title, X, tabs, category list) and tints the toggle.</summary>
    private void ApplyCompactLook(bool compact)
    {
        if (titleLabel != null && titleLabel.gameObject.activeSelf == compact) titleLabel.gameObject.SetActive(!compact);
        if (closeButtonObject != null && closeButtonObject.activeSelf == compact) closeButtonObject.SetActive(!compact);
        foreach (GameObject tab in tabObjects) if (tab != null && tab.activeSelf == compact) tab.SetActive(!compact);
        if (compact) SetCategoryRowActive(false);
        if (compactSwitch != null)
        {
            if (compactSwitch.gameObject.activeSelf != compact) compactSwitch.gameObject.SetActive(compact);
            if (potionIcon != null) potionIcon.SetActive(CompactMode == 1);   // showing currency: the button offers the potions
            if (moneyIcon != null) moneyIcon.SetActive(CompactMode == 2);     // showing potions: the button offers the currency back
        }
        if (compactSmallButton != null && compactSmallButton.gameObject.activeSelf != compact) compactSmallButton.gameObject.SetActive(compact);
        if (compactStickButton != null && compactStickButton.gameObject.activeSelf != compact) compactStickButton.gameObject.SetActive(compact);
        if (compactButton != null && shownCompactOn != compact)   // (Refresh runs often: only re-role on a change)
        {
            shownCompactOn = compact;
            PixelUIKit.SetRole(compactButton, compact ? ButtonRole.Success : ButtonRole.Neutral);
        }
    }

    /// <summary>The place the currency lines and potion rows live: the scrolling list in automatic mode, else the box itself.</summary>
    private Transform ListParent => listContent != null ? (Transform)listContent : boxObject.transform;

    /// <summary>A masked scroll area under the tabs (mouse wheel + a thin scroll bar) that holds all the lines.</summary>
    private void BuildListViewport()
    {
        if (!autoMode) return;

        GameObject view = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        view.transform.SetParent(boxObject.transform, false);
        view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible, but catches the mouse wheel
        listViewport = view.GetComponent<RectTransform>();
        listViewport.anchorMin = Vector2.zero;
        listViewport.anchorMax = Vector2.one;
        listViewport.offsetMin = new Vector2(0f, panelPadding);
        listViewport.offsetMax = new Vector2(0f, -ContentTop);

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(view.transform, false);
        listContent = content.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.sizeDelta = Vector2.zero;
        listContent.anchoredPosition = Vector2.zero;

        listScroll = view.GetComponent<ScrollRect>();
        listScroll.gameObject.AddComponent<PixelScrollSound>();
        listScroll.viewport = listViewport;
        listScroll.content = listContent;
        listScroll.horizontal = false;
        listScroll.movementType = ScrollRect.MovementType.Clamped;
        listScroll.scrollSensitivity = LinePitch;

        listBarObject = new GameObject("Scroll Bar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        listBarObject.transform.SetParent(view.transform, false);
        listBarObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        RectTransform br = listBarObject.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 0f);
        br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(10f, 0f);
        br.anchoredPosition = new Vector2(-2f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(listBarObject.transform, false);
        Image handleImage = handle.GetComponent<Image>();
        PixelUIKit.StyleScrollBar(listBarObject.GetComponent<Image>(), handleImage);
        PixelUIKit.Stretch(handle.GetComponent<RectTransform>());

        Scrollbar bar = listBarObject.GetComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle.GetComponent<RectTransform>();
        bar.targetGraphic = handleImage;
        listScroll.verticalScrollbar = bar;
        listScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        listBarObject.SetActive(false);
    }

    /// <summary>Where the lines start: below the title bar and (in automatic mode) the two tab buttons.</summary>
    private float ContentTop => Compact && autoMode ? CompactTop : headerHeight + (autoMode ? subTabHeight + subTabGap : 0f)
                                + (autoMode && inventoryTab == 1 ? categoryDropdownHeight + subTabGap : 0f); // room for the category drop-down
}
