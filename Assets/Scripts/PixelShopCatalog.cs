// ---- System notes ----
// PixelShopCatalog.cs
//   Part of partial class PixelShop: the shop's layout since 2026-10-24 (the 'A + Saving up' design from the Shop Redesign canvas). The window
//   (panelWidth 1240) has, under the header (title left, search box, IOU / ? / X):
//   - no currency row: the PINNED Inventory opens beside the shop while it is open (PixelShopCurrency.cs),
//   - the CATEGORY RAIL on the left: group labels (Pixels, Consumables, Upgrades, Minigames) and their entries (RailKind: Toggleables, Skill
//     packs, Utilities, Potions, Seeds, Supplies, Features, Value, Operation, Minigames) with how many each lists; it replaced the four tabs, the
//     filter chips and the Upgrades sub-tabs (which used to scroll away with the list),
//   - the SAVING UP FOR card: the item you pinned ('Save up for this' in the detail panel; per save, key Shop.SavingGoal) - its name, a
//     progress bar (the limiting price line) and 'have / need'. Click it to jump to the item. Only shown while something is pinned: unpinned,
//     it hides and the heading row + grid move up into its space (PlaceBelowSavingCard).
//   - a heading row (the entry's name or 'Results for ...') with the 'Only what I can buy' switch,
//   - the CATALOG grid + DETAIL PANEL (PixelShopTilePage.cs). Every kind of thing is a tile now: packs, consumables (ItemBase + item), Value
//     upgrades (ValueBase + tier; bought in numbers like consumables: x1 / x5 / x10 / MAX), Operation devices (OperationBase + kind: Buy opens
//     their upgrades window). Ids by range as in PixelShopSearch.cs, which also lists the search results.
//   The old tab bar and scrolling row list are not built for the main window any more; the rows still serve the pack Upgrades window.
//   TutorialOpen(tab, subTab) / SelectTab map onto rail entries.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The shop's catalog layout: category rail, Saving up for card, the grid + detail panel. Part of <see cref="PixelShop"/>.</summary>
public partial class PixelShop
{
    /// <summary>One entry of the category rail.</summary>
    private enum RailKind { Toggleables, SkillPacks, Utilities, Potions, Seeds, Supplies, Features, Value, Operation, Minigames }

    private static readonly RailKind[] RailOrder =
    {
        RailKind.Toggleables, RailKind.SkillPacks,
        RailKind.Utilities, RailKind.Potions, RailKind.Seeds, RailKind.Supplies,
        RailKind.Features, RailKind.Value, RailKind.Operation,
        RailKind.Minigames,
    };

    /// <summary>The page 'entry' that lists the search results (after the rail entries).</summary>
    private static int SearchEntry => RailOrder.Length;

    private int railEntry;          // the open rail entry (index into RailOrder)
    private bool onlyAffordable;    // 'Only what I can buy'
    private Image[] railImages;
    private TMP_Text[] railCounts;
    private TMP_Text catalogHeading;
    private GameObject affordToggleFill;
    private RectTransform headingRow;
    private float savingCardSpace;   // the card's height + gap (the heading row and the grid move up by this while the card is hidden)
    private float catalogGridTop;    // the grid's top below the catalog area's top, with the card showing
    private Image affordToggleBox;
    private readonly List<int> scratchIds = new List<int>();

    // The Saving up for card.
    private GameObject savingCard;
    private TMP_Text savingName, savingProgress;
    private RectTransform savingFill;
    private int savingId = -1;       // the catalog id the card shows (-1 = none)
    private System.Func<string> savingKey;   // its picture key (made once when the goal changes, not every frame)
    private const string SavingGoalKey = "Shop.SavingGoal";

    // ---- layout (window units, from the window's top left) ----
    private float RailX => panelPadding * 0.8f;
    private float RailW => Mathf.Max(160f, railWidth);
    private float CatalogX => RailX + RailW + panelPadding * 0.8f;
    private float DetailWidth => Mathf.Max(240f, detailPanelWidth);
    private float CatalogTop => headerHeight + 10f;
    private float CatalogGridWidth => panelWidth - CatalogX - panelPadding - DetailGap - DetailWidth;
    private float HeadingHeight => 40f;
    private int[] AmountPresets => amountPresets != null && amountPresets.Length > 0 ? amountPresets : new[] { 1, 5, 10 };

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    /// <summary>Builds the rail, the Saving up card, the heading row and the catalog page (BuildPanel calls this instead of the tabs + list).</summary>
    private void BuildCatalog(Transform panel)
    {
        PixelPrefs.DeclarePerSave(SavingGoalKey, PixelPrefs.Kind.String);
        // Everything under the header lives in one body.
        RectTransform catalogBody = new GameObject("Catalog Body", typeof(RectTransform)).GetComponent<RectTransform>();
        catalogBody.SetParent(panel, false);
        catalogBody.anchorMin = Vector2.zero;
        catalogBody.anchorMax = Vector2.one;
        catalogBody.offsetMin = Vector2.zero;
        catalogBody.offsetMax = new Vector2(0f, -CatalogTop);
        panel = catalogBody;
        float top = 0f;
        BuildRail(panel, top);

        float cardH = Mathf.Max(70f, savingCardHeight);
        savingCardSpace = cardH + 10f;
        catalogGridTop = savingCardSpace + HeadingHeight + 6f;
        catalog = BuildCatalogPage(panel, CatalogX, top, top + catalogGridTop, CatalogGridWidth, RailOrder.Length + 1);
        BuildSavingCard(catalog.area.transform, CatalogGridWidth, cardH);
        BuildHeadingRow(catalog.area.transform, savingCardSpace, CatalogGridWidth);
        PlaceBelowSavingCard(false);   // (the card starts hidden: the heading and grid start in its space until something is pinned)

        catalog.collect = CollectEntry;
        catalog.tileName = SearchTileName;
        catalog.pictureKey = SearchPictureKey;
        catalog.description = SearchDescription;
        catalog.kindLine = KindLine;
        catalog.fillTile = (id, tile, selected) =>
        {
            switch (KindOf(id))
            {
                case SearchKind.Pack: FillPackTile(id, tile, selected); break;
                case SearchKind.Item: FillItemTile(id - ItemBase, tile, selected); break;
                case SearchKind.Value: FillValueTile(id - ValueBase, tile); break;
                default: FillOperationTile(id - OperationBase, tile); break;
            }
        };
        catalog.canBuyNow = CanBuyNow;
        catalog.fillBar = id =>
        {
            switch (KindOf(id))
            {
                case SearchKind.Pack: FillPackBar(catalog, id); break;
                case SearchKind.Item: FillItemBar(catalog, id - ItemBase); break;
                case SearchKind.Value: FillValueBar(catalog, id - ValueBase); break;
                default: SetCostMessage(catalog.barCosts, "", textColor); SetPageBuy(catalog, true, upgradeText); break;
            }
        };
        catalog.purchase = (id, max) =>
        {
            switch (KindOf(id))
            {
                case SearchKind.Pack: if (OpensUpgrades(id)) OpenUpgradesWindow(id); else TryBuy(id); break;
                case SearchKind.Item: { int item = id - ItemBase; int n = max ? MaxAffordable(item) : catalog.count; if (n > 0) TryBuyItems(item, n); break; }
                case SearchKind.Value: { int t = id - ValueBase; TryBuyValueLevels(t, max ? ValueLevelsAffordable(t) : catalog.count); break; }
                default: OpenOperationWindow(id - OperationBase); break;
            }
            RefreshRows();
        };
        catalog.quickBuy = id =>
        {
            switch (KindOf(id))
            {
                case SearchKind.Pack: TryBuy(id); break;
                case SearchKind.Item: TryBuyItems(id - ItemBase, 1); break;
                case SearchKind.Value: TryBuyValueLevels(id - ValueBase, 1); break;
                default: OpenOperationWindow(id - OperationBase); break;
            }
        };
        catalog.hasAmount = id => KindOf(id) == SearchKind.Item || KindOf(id) == SearchKind.Value;
        catalog.maxAmount = id => KindOf(id) == SearchKind.Item ? MaxAffordable(id - ItemBase) : KindOf(id) == SearchKind.Value ? ValueLevelsAffordable(id - ValueBase) : 1;
        catalog.canPin = CanSaveFor;
    }

    private void BuildRail(Transform panel, float top)
    {
        GameObject rail = new GameObject("Category Rail", typeof(RectTransform));
        rail.transform.SetParent(panel, false);
        RectTransform rr = rail.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 0f);
        rr.anchorMax = new Vector2(0f, 1f);
        rr.pivot = new Vector2(0f, 1f);
        rr.offsetMin = new Vector2(RailX, panelPadding);
        rr.offsetMax = new Vector2(RailX + RailW, -top);

        railImages = new Image[RailOrder.Length];
        railCounts = new TMP_Text[RailOrder.Length];
        float y = 0f, entryH = 44f, groupH = 30f;
        string group = null;
        for (int e = 0; e < RailOrder.Length; e++)
        {
            string g = GroupTitle(RailOrder[e]);
            if (g != group)
            {
                group = g;
                if (e > 0) y += 8f;
                TMP_Text label = CreateText(rail.transform, "Group " + g, g, tabFontSize * 0.55f, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
                label.color = new Color(0.62f, 0.68f, 0.82f, 1f);
                label.characterSpacing = 6f;
                PixelUIKit.Caps(label);
                PlaceTopLeft(label.rectTransform, 6f, y, RailW - 12f, groupH);
                y += groupH + 2f;
            }
            int entry = e;
            Button b = CreateButton(rail.transform, "Rail " + RailOrder[e], EntryTitle(RailOrder[e]), Vector2.zero, ButtonRole.Neutral,
                                    textColor, tabFontSize * 0.68f, out TMP_Text bl, out railImages[e]);
            bl.alignment = TextAlignmentOptions.MidlineLeft;
            bl.margin = new Vector4(14f, 0f, 46f, 0f);
            PixelUIKit.FitOneLine(bl, 12f);
            PlaceTopLeft(b.GetComponent<RectTransform>(), 0f, y, RailW, entryH);
            b.onClick.AddListener(() => SelectRailEntry(entry));
            railCounts[e] = CreateText(b.transform, "Count", "", tabFontSize * 0.5f, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            railCounts[e].color = new Color(0.8f, 0.85f, 0.95f, 0.85f);
            railCounts[e].raycastTarget = false;
            RectTransform cr = railCounts[e].rectTransform;
            cr.anchorMin = new Vector2(1f, 0f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(1f, 0.5f);
            cr.sizeDelta = new Vector2(44f, 0f);
            cr.anchoredPosition = new Vector2(-10f, 0f);
            y += entryH + 4f;
        }
    }

    private string GroupTitle(RailKind k) =>
        k <= RailKind.SkillPacks ? pixelsTabText : k <= RailKind.Supplies ? consumablesTabText : k <= RailKind.Operation ? upgradesTabText : minigamesTabText;

    private string EntryTitle(RailKind k)
    {
        switch (k)
        {
            case RailKind.Toggleables: return pixelsToggleablesTitle;
            case RailKind.SkillPacks: return pixelsSkillPacksTitle;
            case RailKind.Utilities: return utilitiesCardTitle;
            case RailKind.Potions: return potionsCardTitle;
            case RailKind.Seeds: return seedsCardTitle;
            case RailKind.Supplies: return suppliesCardTitle;
            case RailKind.Features: return featuresSubTabText;
            case RailKind.Value: return valueSubTabText;
            case RailKind.Operation: return operationSubTabText;
            default: return allMinigamesText;
        }
    }

    /// <summary>The Saving up for card: picture, label, name, progress bar and 'have / need'. Click = jump to the item.</summary>
    private void BuildSavingCard(Transform area, float width, float height)
    {
        savingCard = new GameObject("Saving Up Card", typeof(RectTransform), typeof(Image));
        savingCard.transform.SetParent(area, false);
        Image bg = savingCard.GetComponent<Image>();
        bg.color = savingCardColor;
        PixelUIKit.StyleBox(bg);
        RectTransform cr = savingCard.GetComponent<RectTransform>();
        PlaceTopLeft(cr, 0f, 0f, width, height);
        PixelUIKit.MakeClickable(savingCard, () => { if (savingId >= 0) ShowInCatalog(savingId); }, true, false);

        float pic = height - 16f;
        if (!hidePictures) AddPictureSlot(cr, pic, new Vector2(8f, 8f), false, () => savingKey != null ? savingKey() : null, false);
        float x = pic + 22f, textW = width - x - 14f;

        TMP_Text label = CreateText(savingCard.transform, "Label", savingUpTitle, tileFontSize * 0.75f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        label.color = levelColor;
        label.characterSpacing = 6f;
        label.raycastTarget = false;
        PixelUIKit.Caps(label);
        PlaceTopLeft(label.rectTransform, x, 8f, textW, 20f);

        savingName = CreateText(savingCard.transform, "Name", "", tileFontSize * 1.15f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        PixelUIKit.FitOneLine(savingName, 12f);
        savingName.raycastTarget = false;
        PlaceTopLeft(savingName.rectTransform, x, 26f, textW, 28f);

        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(savingCard.transform, false);
        bar.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f, 1f);
        bar.GetComponent<Image>().raycastTarget = false;
        RectTransform br = bar.GetComponent<RectTransform>();
        PlaceTopLeft(br, x, 58f, textW, 10f);
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        fill.GetComponent<Image>().color = savingBarColor;
        fill.GetComponent<Image>().raycastTarget = false;
        savingFill = fill.GetComponent<RectTransform>();
        savingFill.anchorMin = new Vector2(0f, 0f);
        savingFill.anchorMax = new Vector2(0f, 1f);
        savingFill.pivot = new Vector2(0f, 0.5f);
        savingFill.sizeDelta = new Vector2(0f, 0f);

        savingProgress = CreateText(savingCard.transform, "Progress", "", tileFontSize * 0.8f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        savingProgress.richText = true;
        savingProgress.color = new Color(0.72f, 0.77f, 0.88f, 1f);
        PixelUIKit.FitOneLine(savingProgress, 11f);
        savingProgress.raycastTarget = false;
        PlaceTopLeft(savingProgress.rectTransform, x, 70f, textW, Mathf.Max(18f, height - 74f));
        savingCard.SetActive(false);
    }

    /// <summary>The row above the grid: the entry's name (or 'Results for ...') and the 'Only what I can buy' switch.</summary>
    private void BuildHeadingRow(Transform area, float y, float width)
    {
        headingRow = new GameObject("Heading Row", typeof(RectTransform)).GetComponent<RectTransform>();
        headingRow.SetParent(area, false);
        PlaceTopLeft(headingRow, 0f, y, width, HeadingHeight);

        catalogHeading = CreateText(headingRow, "Heading", "", tabFontSize * 0.75f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        PixelUIKit.FitOneLine(catalogHeading, 12f);
        PlaceTopLeft(catalogHeading.rectTransform, 4f, 0f, width * 0.55f, HeadingHeight);

        float tw = width * 0.45f;
        GameObject toggle = new GameObject("Only Affordable", typeof(RectTransform), typeof(Image));
        toggle.transform.SetParent(headingRow, false);
        toggle.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // (an invisible hit area for the whole switch)
        PlaceTopLeft(toggle.GetComponent<RectTransform>(), width - tw, 0f, tw, HeadingHeight);
        PixelUIKit.MakeClickable(toggle, () => { onlyAffordable = !onlyAffordable; PixelAudio.Play("ui_click"); RefreshRows(); }, true, false);

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(toggle.transform, false);
        affordToggleBox = box.GetComponent<Image>();
        PixelUIKit.StyleBox(affordToggleBox, true);
        affordToggleBox.raycastTarget = false;
        RectTransform bxr = box.GetComponent<RectTransform>();
        bxr.anchorMin = bxr.anchorMax = bxr.pivot = new Vector2(1f, 0.5f);
        bxr.sizeDelta = new Vector2(26f, 26f);
        bxr.anchoredPosition = new Vector2(-4f, 0f);
        // (the 'on' mark is a filled square, not a tick character: the shared font may not have one)
        affordToggleFill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        affordToggleFill.transform.SetParent(box.transform, false);
        Image fill = affordToggleFill.GetComponent<Image>();
        fill.color = affordableColor;
        fill.raycastTarget = false;
        PixelUIKit.StyleBox(fill);
        RectTransform fr = affordToggleFill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(5f, 5f);
        fr.offsetMax = new Vector2(-5f, -5f);
        affordToggleFill.SetActive(false);

        TMP_Text label = CreateText(toggle.transform, "Label", onlyAffordableText, tileFontSize * 0.95f, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        PixelUIKit.FitOneLine(label, 11f);
        label.raycastTarget = false;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = new Vector2(-40f, 0f);
    }

    /// <summary>Moves the heading row and the grid up while the Saving up card is hidden (and back down when it shows).</summary>
    private void PlaceBelowSavingCard(bool cardShown)
    {
        float lift = cardShown ? 0f : savingCardSpace;
        if (headingRow != null) headingRow.anchoredPosition = new Vector2(0f, -(savingCardSpace - lift));
        if (catalog != null && catalog.gridRect != null)
        {
            float gridTop = catalogGridTop - lift;
            catalog.gridRect.sizeDelta = new Vector2(catalog.gridRect.sizeDelta.x, -gridTop);
            catalog.gridRect.anchoredPosition = new Vector2(0f, -gridTop);
        }
    }

    // ------------------------------------------------------------------
    // Using it
    // ------------------------------------------------------------------

    private void SelectRailEntry(int entry)
    {
        railEntry = Mathf.Clamp(entry, 0, RailOrder.Length - 1);
        currentTab = TabOfEntry(RailOrder[railEntry]);
        if (shopSearch.Length > 0) { shopSearch = ""; if (searchField != null) searchField.SetTextWithoutNotify(""); }
        if (catalog != null) { catalog.content.anchoredPosition = Vector2.zero; catalog.scroll.StopMovement(); }
        RefreshRows();
    }

    private static ShopTab TabOfEntry(RailKind k) =>
        k <= RailKind.SkillPacks ? ShopTab.Pixels : k <= RailKind.Supplies ? ShopTab.Consumables : k <= RailKind.Operation ? ShopTab.Upgrades : ShopTab.Minigames;

    /// <summary>The rail entry for a tab (+ sub-tab: Pixels / Consumables chip, Upgrades 0 Features / 1 Value / 2 Operation).</summary>
    private static int EntryOf(ShopTab tab, int sub)
    {
        RailKind k;
        switch (tab)
        {
            case ShopTab.Consumables: k = sub == 1 ? RailKind.Potions : sub == 2 ? RailKind.Seeds : sub == 3 ? RailKind.Supplies : RailKind.Utilities; break;
            case ShopTab.Upgrades: k = sub == 1 ? RailKind.Value : sub == 2 ? RailKind.Operation : RailKind.Features; break;
            case ShopTab.Minigames: k = RailKind.Minigames; break;
            default: k = sub == 1 ? RailKind.SkillPacks : RailKind.Toggleables; break;
        }
        return System.Array.IndexOf(RailOrder, k);
    }

    /// <summary>Opens the rail entry that lists 'id' and selects it there (the Saving up card's click).</summary>
    private void ShowInCatalog(int id)
    {
        for (int e = 0; e < RailOrder.Length; e++)
        {
            CollectEntry(e, scratchIds);
            if (!scratchIds.Contains(id)) continue;
            onlyAffordable = false;
            SelectRailEntry(e);
            catalog.selected[e] = id;
            RefreshRows();
            return;
        }
    }

    // ------------------------------------------------------------------
    // What each entry lists
    // ------------------------------------------------------------------

    /// <summary>The ids an entry lists, in shop order (the search entry: the search results).</summary>
    private void CollectEntry(int entry, List<int> list)
    {
        list.Clear();
        if (entry >= RailOrder.Length) { CollectSearchResults(list); return; }
        switch (RailOrder[entry])
        {
            case RailKind.Toggleables: CollectPixelPacks(0, list); break;
            case RailKind.SkillPacks: CollectPixelPacks(1, list); break;
            case RailKind.Utilities: CollectItems(1, list); break;
            case RailKind.Potions: CollectItems(0, list); break;
            case RailKind.Seeds: CollectItems(2, list); break;
            case RailKind.Supplies: CollectItems(3, list); break;
            case RailKind.Features: CollectTabPacks(ShopTab.Upgrades, list); break;
            case RailKind.Value:
                for (int t = 0; t < clicker.Tiers.Length; t++)
                {
                    PixelClicker.PixelTier tier = clicker.Tiers[t];
                    if (tier.unlocked && !PixelClicker.IsHazard(tier.type) && !(HidePurchased && ValueUpgradeMaxed(t))) list.Add(ValueBase + t);
                }
                break;
            case RailKind.Operation:
                if (consumables != null)
                    for (int k = 0; k < OperationKinds.Length; k++)
                        if (consumables.OperationListed(OperationKinds[k])) list.Add(OperationBase + k);
                break;
            default: CollectTabPacks(ShopTab.Minigames, list); break;
        }
    }

    private void CollectItems(int category, List<int> list)
    {
        CollectCategoryItems(category, list);
        for (int i = 0; i < list.Count; i++) list[i] += ItemBase;
    }

    /// <summary>The packs listed on a tab (Upgrades: the Pixel Bank right above Time Stop, as the old list had it), upgrade children left out.</summary>
    private void CollectTabPacks(ShopTab tab, List<int> list)
    {
        for (int i = 0; i < packs.Length && i < ItemBase; i++)
        {
            ShopPack pack = packs[i];
            if (pack == null || IsChild(i) || TabOf(pack) != tab) continue;
            if (!(IsPackRequirementMet(i) || showLockedPacks || HasPack(i)) || HiddenAsPurchased(i)) continue;
            list.Add(i);
        }
        if (tab == ShopTab.Upgrades)
        {
            int timeStop = System.Array.FindIndex(packs, p => p != null && p.unlocksTimeStop);
            float Key(int i) => timeStop >= 0 && packs[i] != null && packs[i].unlocksBank ? timeStop - 0.5f : i;
            list.Sort((a, b) => Key(a) != Key(b) ? Key(a).CompareTo(Key(b)) : a.CompareTo(b));
        }
    }

    /// <summary>'Pixels > Toggleables', 'Upgrades > Value'... (search results: the entry that lists it, found by its kind).</summary>
    private string KindLine(int id)
    {
        int entry = catalog != null && catalog.chip < RailOrder.Length ? catalog.chip : -1;
        if (entry < 0)
        {
            switch (KindOf(id))
            {
                case SearchKind.Value: entry = System.Array.IndexOf(RailOrder, RailKind.Value); break;
                case SearchKind.Operation: entry = System.Array.IndexOf(RailOrder, RailKind.Operation); break;
                case SearchKind.Item:
                {
                    int cat = consumables.CategoryOf(id - ItemBase);
                    entry = System.Array.IndexOf(RailOrder, cat == 0 ? RailKind.Potions : cat == 2 ? RailKind.Seeds : cat == 3 ? RailKind.Supplies : RailKind.Utilities);
                    break;
                }
                default:
                {
                    ShopTab tab = TabOf(packs[id]);
                    entry = EntryOf(tab, tab == ShopTab.Pixels && SectionOf(packs[id]) == 2 ? 1 : 0);
                    break;
                }
            }
        }
        if (entry < 0) return "";
        RailKind k = RailOrder[entry];
        string group = GroupTitle(k), title = EntryTitle(k);
        return group == title ? group : group + "  >  " + title;
    }

    /// <summary>Can this be bought right now (the tile's green bar, 'Only what I can buy')?</summary>
    private bool CanBuyNow(int id)
    {
        switch (KindOf(id))
        {
            case SearchKind.Pack: return !IsPurchased(id) && IsRequirementMet(id) && CanAfford(id);
            case SearchKind.Item:
            {
                int item = id - ItemBase;
                return !ItemLocked(item) && consumables.ItemRoom(item) > 0 && CanAffordCosts(consumables.ItemCosts(item));
            }
            case SearchKind.Value:
            {
                int t = id - ValueBase;
                return ValueLevelOpen(t) && clicker.CanAfford(clicker.Tiers[t].type, ValueUpgradeCost(t));
            }
            default: return false;
        }
    }

    /// <summary>The Buy label when the price can't be paid: 'Need more White' (the first price line you are short of).</summary>
    private string NeedMoreLabel(PackCost[] costs, string fallback)
    {
        if (costs != null)
            foreach (PackCost c in costs)
                if (!CanAffordCost(c)) return string.Format(needMoreFormat, ShortCostName(c));
        return fallback;
    }

    // ------------------------------------------------------------------
    // Value levels bought in numbers
    // ------------------------------------------------------------------

    /// <summary>How many more Value levels a pixel type can take (the max level and the owning skill's cap), at most maxPerPurchase.</summary>
    private int ValueLevelsLeft(int t)
    {
        PixelClicker.PixelTier tier = clicker.Tiers[t];
        int left = maxPerPurchase;
        if (valueMaxLevel > 0) left = Mathf.Min(left, valueMaxLevel - tier.valueLevel);
        if (valueCapBySkill && PixelSkills.TryGetOwnerLevel(tier.type, out _, out int skill))
            left = Mathf.Min(left, skill * Mathf.Max(1, valueLevelsPerSkillLevel) - tier.valueLevel);
        return Mathf.Max(0, left);
    }

    /// <summary>The price of the next 'n' Value levels of a pixel type (paid in that pixel).</summary>
    private double ValueLevelsCost(int t, int n)
    {
        PixelClicker.PixelTier tier = clicker.Tiers[t];
        double total = 0d;
        for (int k = 0; k < n; k++) total += ValueCostAtLevel(t, tier.valueLevel + k);
        return total;
    }

    /// <summary>How many Value levels you can afford right now (the MAX amount; at least 1 so the button shows a count).</summary>
    private int ValueLevelsAffordable(int t)
    {
        PixelClicker.PixelTier tier = clicker.Tiers[t];
        int left = ValueLevelsLeft(t);
        double held = tier.count, spent = 0d;
        int n = 0;
        while (n < left)
        {
            double next = ValueCostAtLevel(t, tier.valueLevel + n);
            if (!PixelClicker.InfiniteResources && spent + next > held) break;
            spent += next;
            n++;
        }
        return Mathf.Max(1, n);
    }

    /// <summary>Buys up to 'n' Value levels (stops at the first it can't pay or that is capped). Returns how many were bought.</summary>
    private int TryBuyValueLevels(int t, int n)
    {
        int bought = 0;
        while (bought < n && ValueLevelOpen(t) && clicker.TryBuyValueLevel(t, ValueUpgradeCost(t))) bought++;
        if (bought > 0) PlayPurchaseSound();
        return bought;
    }

    // ------------------------------------------------------------------
    // Saving up for
    // ------------------------------------------------------------------

    /// <summary>Packs and consumables can be saved for (not Value levels or Operation rows), while they are not bought / full.</summary>
    private bool CanSaveFor(int id)
    {
        switch (KindOf(id))
        {
            case SearchKind.Pack: return !IsPurchased(id) && !OpensUpgrades(id);
            case SearchKind.Item: return consumables.ItemRoom(id - ItemBase) > 0;
            default: return false;
        }
    }

    /// <summary>A stable name for an id (pack / item names survive list changes), stored per save.</summary>
    private string GoalKey(int id) =>
        KindOf(id) == SearchKind.Pack ? "pack:" + packs[id].displayName : KindOf(id) == SearchKind.Item ? "item:" + consumables.ItemName(id - ItemBase) : "";

    private int GoalFromKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return -1;
        if (key.StartsWith("pack:"))
        {
            string name = key.Substring(5);
            for (int i = 0; i < packs.Length && i < ItemBase; i++) if (packs[i] != null && packs[i].displayName == name) return i;
        }
        else if (key.StartsWith("item:") && consumables != null)
        {
            string name = key.Substring(5);
            for (int i = 0; i < consumables.ItemCount; i++) if (consumables.ItemName(i) == name) return ItemBase + i;
        }
        return -1;
    }

    private bool IsSavingGoal(int id) => PixelPrefs.GetString(SavingGoalKey, "") == GoalKey(id);

    private void ToggleSavingGoal(int id)
    {
        PixelPrefs.SetString(SavingGoalKey, IsSavingGoal(id) ? "" : GoalKey(id));
        PixelAudio.Play("ui_click");
        RefreshRows();
    }

    /// <summary>The id the card shows: the pinned goal while it can still be saved for, else none (-1: the card hides and the grid moves up into its space).</summary>
    private int PickSavingGoal()
    {
        int pinned = GoalFromKey(PixelPrefs.GetString(SavingGoalKey, ""));
        return pinned >= 0 && CanSaveFor(pinned) ? pinned : -1;
    }

    /// <summary>How far you are towards a price (0..1, the line you are furthest from); 'limit' = that line.</summary>
    private float Progress(PackCost[] costs, out int limit)
    {
        limit = -1;
        if (costs == null || costs.Length == 0) return 1f;
        float lowest = 2f;
        for (int c = 0; c < costs.Length; c++)
        {
            float f = costs[c].amount <= 0d ? 1f : Mathf.Clamp01((float)(CostHeld(costs[c]) / costs[c].amount));
            if (f < lowest) { lowest = f; limit = c; }
        }
        return Mathf.Clamp01(lowest);
    }

    private PackCost[] GoalCosts(int id) =>
        KindOf(id) == SearchKind.Pack ? CurrentCosts(packs[id]) : KindOf(id) == SearchKind.Item ? consumables.ItemCosts(id - ItemBase) : null;

    // ------------------------------------------------------------------
    // Refreshing the parts around the grid
    // ------------------------------------------------------------------

    /// <summary>Rail highlights and counts, the heading, the switch and the Saving up card.</summary>
    private void RefreshCatalogChrome(bool searching)
    {
        for (int e = 0; e < RailOrder.Length; e++)
        {
            SetShopRole(railImages[e], !searching && e == railEntry ? ButtonRole.Selected : ButtonRole.Neutral);
            CollectEntry(e, scratchIds);
            PixelUIKit.SetText(railCounts[e], scratchIds.Count.ToString());
        }
        PixelUIKit.SetText(catalogHeading, searching ? string.Format(resultsForFormat, searchField != null ? searchField.text : shopSearch) : EntryTitle(RailOrder[railEntry]));
        if (affordToggleFill.activeSelf != onlyAffordable) affordToggleFill.SetActive(onlyAffordable);
        PixelUIKit.SetText(catalog.empty, searching ? searchNoResultsText : onlyAffordable ? nothingAffordableText : noItemsText);
        affordToggleBox.color = new Color(0.07f, 0.07f, 0.1f, 1f);
        RefreshSavingCard();
    }

    private void RefreshSavingCard()
    {
        if (savingCard == null) return;
        int goal = PickSavingGoal();
        if (goal != savingId) { savingId = goal; savingKey = goal >= 0 ? SearchPictureKey(goal) : null; }
        bool show = savingId >= 0;
        if (savingCard.activeSelf != show) { savingCard.SetActive(show); PlaceBelowSavingCard(show); }
        if (!show) return;
        PackCost[] costs = GoalCosts(savingId);
        float p = Progress(costs, out int limit);
        PixelUIKit.SetText(savingName, SearchTileName(savingId));
        RectTransform bar = (RectTransform)savingFill.parent;
        savingFill.sizeDelta = new Vector2(bar.rect.width * p, 0f);
        string line;
        if (limit >= 0)
        {
            PackCost c = costs[limit];
            line = PixelClicker.FormatNumberShort(CostHeld(c)) + " / " + PixelClicker.FormatNumberShort(c.amount) + " " + ShortCostName(c) + "   -   " + Mathf.FloorToInt(p * 100f) + "%";
        }
        else line = Mathf.FloorToInt(p * 100f) + "%";
        PixelUIKit.SetText(savingProgress, line);
    }
}
