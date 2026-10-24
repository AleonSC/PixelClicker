// ---- System notes ----
// PixelUIInventoryTabs.cs
//   Part of PixelUI (partial class): the Inventory tabs (Currency / Consumables / Materials), the category drop-down + search, and the Consumables and Materials lists. The system notes are in PixelUI.cs.
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
    // Inventory tabs / consumables
    // ------------------------------------------------------------------

    private void BuildInventoryTabs(Vector2 anchor)
    {
        string[] names = { currencyTabText, consumablesTabText, materialsTabText };
        subTabImages = new Image[names.Length];
        shownSubTab = -1;

        float innerWidth = panelWidth - panelPadding * 2f;
        float tabWidth = (innerWidth - 8f * (names.Length - 1)) / names.Length;
        float tabTop = headerHeight;

        for (int i = 0; i < names.Length; i++)
        {
            Button tab = MakeButton(boxObject.transform, "Tab " + names[i], names[i],
                                    new Vector2(tabWidth, subTabHeight), PixelUITheme.ColorOf(ButtonRole.Neutral), subTabTextColor,
                                    subTabFontSize > 0f ? subTabFontSize : 28f);
            PixelUIKit.Caps(tab);
            subTabImages[i] = tab.GetComponent<Image>();
            tabObjects.Add(tab.gameObject);

            RectTransform rt = tab.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(tabWidth, subTabHeight);
            rt.anchoredPosition = new Vector2((i - (names.Length - 1) * 0.5f) * (tabWidth + 8f), -tabTop);

            TMP_Text label = tab.GetComponentInChildren<TMP_Text>();
            label.text = names[i];
            label.color = subTabTextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true; // three tabs share the width
            label.fontSizeMax = subTabFontSize > 0f ? subTabFontSize : 28f;
            label.fontSizeMin = 8f;
            label.margin = new Vector4(8f, 0f, 8f, 0f); // keeps "CONSUMABLES" off the button's edges
            label.overflowMode = TextOverflowModes.Overflow;

            int captured = i;
            tab.onClick.AddListener(() =>
            {
                inventoryTab = captured;
                if (listContent != null) listContent.anchoredPosition = Vector2.zero; // new tab starts at the top
                Refresh();
            });
        }

        // The kind-of-consumable drop-down, under the tabs (only on the Consumables tab). It sits on the box itself, not in
        // the scrolling list, so its pop-up list is never cut off.
        categoryDropdown = PixelUIKit.CreateDropdown(font, boxObject.transform, "Category Dropdown",
                                                     new Vector2(innerWidth, categoryDropdownHeight), subTabInactiveColor,
                                                     new Color(0.12f, 0.12f, 0.16f, 1f), subTabTextColor,
                                                     subTabFontSize > 0f ? subTabFontSize * 0.9f : 26f);
        RectTransform dr = categoryDropdown.GetComponent<RectTransform>();
        dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0.5f, 1f);
        dr.sizeDelta = new Vector2(innerWidth, categoryDropdownHeight);
        dr.anchoredPosition = new Vector2(0f, -(tabTop + subTabHeight + subTabGap));
        categoryDropdown.options.Clear();
        foreach (string category in consumableCategories) categoryDropdown.options.Add(new TMP_Dropdown.OptionData(category == "Devices" ? "Utilities" : category));   // (one name for the category everywhere: the shop calls it Utilities)
        if (categoryDropdown.options.Count < 3) categoryDropdown.options.Add(new TMP_Dropdown.OptionData("Seeds")); // a scene saved before seeds existed has only two names
        if (categoryDropdown.options.Count < 4) categoryDropdown.options.Add(new TMP_Dropdown.OptionData("Supplies"));
        categoryDropdown.SetValueWithoutNotify(0);
        categoryDropdown.RefreshShownValue();
        categoryDropdown.onValueChanged.AddListener(value =>
        {
            consumableSubTab = value;
            if (listContent != null) listContent.anchoredPosition = Vector2.zero;
            Refresh();
        });
        categoryDropdown.gameObject.SetActive(false);

        // The drop-down on the left, a search box on the right (searches every kind of consumable you hold) and, between them, a square
        // menu button that opens the 3D consumables window (PixelConsumableGallery).
        float gapBetween = 8f, square = categoryDropdownHeight, half = (innerWidth - gapBetween * 2f - square) * 0.5f;
        dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1f);
        dr.pivot = new Vector2(0f, 1f);
        dr.sizeDelta = new Vector2(half, categoryDropdownHeight);
        dr.anchoredPosition = new Vector2(-innerWidth * 0.5f, -(tabTop + subTabHeight + subTabGap));
        consumableSearchField = PixelUIKit.CreateInputField(font, boxObject.transform, "Consumable Search", new Vector2(half, categoryDropdownHeight),
                                                            subTabInactiveColor, subTabTextColor, subTabFontSize > 0f ? subTabFontSize * 0.9f : 26f, consumableSearchPlaceholder);
        PixelUIKit.AddSearchIcon(consumableSearchField);
        RectTransform csr = consumableSearchField.GetComponent<RectTransform>();
        csr.anchorMin = csr.anchorMax = new Vector2(0.5f, 1f);
        csr.pivot = new Vector2(1f, 1f);
        csr.sizeDelta = new Vector2(half, categoryDropdownHeight);
        csr.anchoredPosition = new Vector2(innerWidth * 0.5f, -(tabTop + subTabHeight + subTabGap));
        consumableSearchField.onValueChanged.AddListener(text =>
        {
            consumableSearch = (text ?? "").Trim().ToLowerInvariant();
            if (listContent != null) listContent.anchoredPosition = Vector2.zero;
            Refresh();
        });
        consumableSearchField.gameObject.SetActive(false);

        galleryButton = PixelUIKit.CreateButton(font, boxObject.transform, "3D View Button", "", new Vector2(square, square), ButtonRole.Neutral, 20f);
        RectTransform gbr = galleryButton.GetComponent<RectTransform>();
        gbr.anchorMin = gbr.anchorMax = new Vector2(0.5f, 1f);
        gbr.pivot = new Vector2(0.5f, 1f);
        gbr.sizeDelta = new Vector2(square, square);
        gbr.anchoredPosition = new Vector2(-innerWidth * 0.5f + half + gapBetween + square * 0.5f, -(tabTop + subTabHeight + subTabGap));
        GameObject icon = new GameObject("Menu Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(galleryButton.transform, false);
        Image iconImage = icon.GetComponent<Image>();
        iconImage.sprite = PixelUIKit.MenuSprite();
        iconImage.raycastTarget = false;
        RectTransform ir = iconImage.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.sizeDelta = new Vector2(square * 0.62f, square * 0.62f);
        galleryButton.onClick.AddListener(PixelConsumableGallery.Toggle);
        PixelHoverTip galleryTip = galleryButton.gameObject.AddComponent<PixelHoverTip>();
        galleryTip.onEnter = () => tooltipCustomText = galleryButtonTip;
        galleryTip.onExit = HideTooltip;
        galleryButton.gameObject.SetActive(false);
    }

    private Button galleryButton;

    private TMP_InputField consumableSearchField;
    private string consumableSearch = "";

    /// <summary>Shows / hides the category drop-down and the search box beside it together.</summary>
    private void SetCategoryRowActive(bool on)
    {
        if (categoryDropdown != null && categoryDropdown.gameObject.activeSelf != on) categoryDropdown.gameObject.SetActive(on);
        if (consumableSearchField != null && consumableSearchField.gameObject.activeSelf != on) consumableSearchField.gameObject.SetActive(on);
        if (galleryButton != null && galleryButton.gameObject.activeSelf != on) galleryButton.gameObject.SetActive(on);
    }

    private void UpdateSubTabs()
    {
        if (subTabImages == null || shownSubTab == inventoryTab) return;   // (Refresh runs often: only re-role on a change)
        shownSubTab = inventoryTab;
        for (int i = 0; i < subTabImages.Length; i++)
            PixelUIKit.SetRole(subTabImages[i], i == inventoryTab ? ButtonRole.Selected : ButtonRole.Neutral);
    }

    /// <summary>Builds one clickable line per potion kind (shown only while you own some), plus the status and hint texts.</summary>
    private void BuildConsumableList(Vector2 anchor)
    {
        TextAlignmentOptions align = anchor.x > 0.5f ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;

        activeLabel = MakeText(ListParent, "Active Potion", "", fontSize * 0.85f, align, FontStyles.Bold, textColor);
        PlaceLine(activeLabel.rectTransform);
        activeLabel.gameObject.SetActive(false);

        consumableAlign = align;
        BuildPotionRows();

        noConsumablesLabel = MakeText(ListParent, "No Consumables", noConsumablesText.ToUpperInvariant(), fontSize,
                                      TextAlignmentOptions.Center, FontStyles.Bold, emptyMessageColor); // capitals, centred in the window
        PlaceLine(noConsumablesLabel.rectTransform);
        noConsumablesLabel.gameObject.SetActive(false);

        hintLabel = MakeText(ListParent, "Hint", potionHint, hintFontSize, align, FontStyles.Bold, hintColor);
        hintLabel.enableAutoSizing = true; // shrinks to fit the box instead of wrapping
        hintLabel.fontSizeMax = hintFontSize;
        hintLabel.fontSizeMin = 12f;
        hintLabel.overflowMode = TextOverflowModes.Truncate;
        PlaceLine(hintLabel.rectTransform);
        hintLabel.gameObject.SetActive(false);
    }

    private TextAlignmentOptions consumableAlign;

    /// <summary>(Re)creates one clickable line per potion kind. Lines are only shown while you own that potion.</summary>
    /// <summary>Makes the inventory row of one consumable the first time it is shown.</summary>
    private void CreatePotionRow(int i)
    {
        TextAlignmentOptions align = consumableAlign;
        GameObject row = new GameObject("Potion " + i, typeof(RectTransform), typeof(Image), typeof(PotionRowClick));
        row.transform.SetParent(ListParent, false);
        Image image = row.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        PlaceLine(row.GetComponent<RectTransform>());

        PotionRowClick click = row.GetComponent<PotionRowClick>();
        click.highlight = image;
        click.hoverColor = potionHoverColor;
        int captured = i;
        PixelHoverTip itemTip = row.AddComponent<PixelHoverTip>();
        itemTip.onEnter = () => ShowItemTooltip(captured);
        itemTip.onExit = HideTooltip;
        click.onRightClick = () => { if (consumables != null && consumables.TryUseItem(captured)) Refresh(); };
        click.onLeftClick = () => { if (Compact && consumables != null && consumables.TryUseItem(captured)) Refresh(); };   // the simplified Inventory: a plain click uses it

        TMP_Text label = MakeText(row.transform, "Label", "", fontSize, align, FontStyles.Normal, textColor);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        potionLabels[i] = label;
        potionRowObjects[i] = row;
        row.SetActive(false);
    }

    private void BuildPotionRows()
    {
        if (potionRowObjects != null)
            foreach (GameObject old in potionRowObjects)
                if (old != null) Destroy(old);

        int count = consumables != null ? consumables.ItemCount : 0;
        potionLabels = new TMP_Text[count];
        potionRowObjects = new GameObject[count];   // the rows themselves are made when an item is first held (there are over a thousand combo potions)

        PixelDebug.Info("PixelUI: inventory knows " + count + " potion kinds (PixelConsumables on '" +
                  (consumables != null ? consumables.gameObject.name : "none") + "').", this);
    }

    /// <summary>Stretches a line across the box's width, anchored to the top (Refresh sets the y position).</summary>
    private void PlaceLine(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-panelPadding * 2f, LinePitch);
    }

    /// <summary>Lays out the Materials tab: one line per material you hold (PixelMaterials: Charcoal...) and per minigame material (Bomb Parts...).</summary>
    private float RefreshMaterials(float top)
    {
        float y = top;
        int shown = 0;
        void Line(string name, double amount, Color colour)
        {
            while (materialLabels.Count <= shown)
            {
                TMP_Text made = MakeText(ListParent, "Material " + materialLabels.Count, "", fontSize, consumableAlign, FontStyles.Normal, textColor);
                PlaceLine(made.rectTransform);
                materialLabels.Add(made);
            }
            TMP_Text label = materialLabels[shown];
            label.gameObject.SetActive(true);
            label.rectTransform.anchoredPosition = new Vector2(0f, -y);
            SetRow(label, name, FormatAmount(amount), colour);
            y += LinePitch;
            shown++;
        }
        foreach (PixelMaterials.MaterialDef d in PixelMaterials.All())
        {
            double held = d != null ? PixelMaterials.Amount(d.id) : 0d;
            if (held > 0d) Line(d.displayName, held, d.color);
        }
        foreach (PixelMinigame m in PixelMinigame.All)
        {
            if (m == null || string.IsNullOrEmpty(m.MaterialName) || m.MaterialAmount <= 0d) continue;
            Line(m.MaterialName, m.MaterialAmount, m.MaterialColor);
        }
        for (int i = shown; i < materialLabels.Count; i++) materialLabels[i].gameObject.SetActive(false);

        if (noMaterialsLabel == null)
        {
            noMaterialsLabel = MakeText(ListParent, "No Materials", noMaterialsText.ToUpperInvariant(), fontSize,
                                        TextAlignmentOptions.Center, FontStyles.Bold, emptyMessageColor);
            PlaceLine(noMaterialsLabel.rectTransform);
        }
        noMaterialsLabel.gameObject.SetActive(shown == 0);
        if (shown == 0)
        {
            noMaterialsLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
        }
        return y;
    }

    private void HideMaterialWidgets()
    {
        foreach (TMP_Text label in materialLabels) if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
        if (noMaterialsLabel != null && noMaterialsLabel.gameObject.activeSelf) noMaterialsLabel.gameObject.SetActive(false);
    }

    /// <summary>Does this item belong to the chosen Consumables category (Potions / Devices / Seeds)?</summary>
    private bool InConsumableCategory(int item, bool onDevices, bool onSeeds)
    {
        if (consumableSearch.Length > 0) return consumables.ItemName(item).ToLowerInvariant().Contains(consumableSearch);   // searching: every kind, by name
        return consumables.CategoryOf(item) == consumableSubTab;   // 0 potions, 1 devices, 2 seeds, 3 supplies
    }

    /// <summary>Hides everything that belongs to the Consumables tab.</summary>
    private void HideConsumableWidgets(bool keepRows = false)
    {
        if (activeLabel != null) activeLabel.gameObject.SetActive(false);
        SetCategoryRowActive(false);
        if (noConsumablesLabel != null) noConsumablesLabel.gameObject.SetActive(false);
        if (hintLabel != null) hintLabel.gameObject.SetActive(false);
        if (potionRowObjects != null && !keepRows)
            foreach (GameObject row in potionRowObjects)
                if (row != null) row.SetActive(false);
    }

    /// <summary>Lays out the Consumables tab starting at 'top'. Returns the y below its last line.</summary>
    private float RefreshConsumables(float top)
    {
        float y = top;

        bool onDevices = consumableSubTab == 1;
        bool onSeeds = consumableSubTab == 2;
        bool onSupplies = consumableSubTab == 3;

        // The hint comes first, so it is always in view.
        bool anyOwned = false;
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount && !anyOwned; i++)
                anyOwned = consumables.ItemOwned(i) > 0 && InConsumableCategory(i, onDevices, onSeeds);
        hintLabel.text = onSeeds ? seedHint : onSupplies ? supplyHint : onDevices ? deviceHint : potionHint;
        bool showHint = false; // the hint is now a hover tooltip on each item
        hintLabel.gameObject.SetActive(showHint);
        if (showHint)
        {
            hintLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            hintLabel.rectTransform.sizeDelta = new Vector2(-panelPadding * 2f, hintFontSize * 1.5f);
            y += hintFontSize * 1.5f + 6f;
        }

        bool active = consumables != null && consumables.IsActive && !onDevices && !onSeeds && !onSupplies;
        activeLabel.gameObject.SetActive(active);
        if (active)
        {
            activeLabel.text = string.Format(activePotionFormat, consumables.Get(consumables.ActiveIndex).displayName,
                                             Mathf.CeilToInt(consumables.Remaining));
            activeLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
        }

        if (consumables != null && (potionRowObjects == null || potionRowObjects.Length != consumables.ItemCount))
            BuildPotionRows();

        int shown = 0;
        int count = potionRowObjects != null ? potionRowObjects.Length : 0;
        for (int i = 0; i < count; i++)
        {
            int owned = consumables.ItemOwned(i);
            // Combo potions only count when really held (Infinite resources would otherwise list over a thousand).
            bool visible = owned > 0 && (!consumables.ItemCraftOnly(i) || consumables.Get(i).owned > 0) && (!consumables.ItemAttuned(i) || consumables.GetDevice(i - consumables.Count).owned > 0) && InConsumableCategory(i, onDevices, onSeeds); // this sub-tab's kind only
            if (potionRowObjects[i] == null)
            {
                if (!visible) continue;
                CreatePotionRow(i);
            }
            if (potionRowObjects[i].activeSelf != visible) potionRowObjects[i].SetActive(visible);
            if (!visible) continue;

            potionRowObjects[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
            shown++;

            int tierIndex = clicker.IndexOf(consumables.ItemRequiredType(i));
            SetRow(potionLabels[i], consumables.ItemName(i), owned >= 99999 ? PixelConsumables.OwnedText(owned) : "x" + FormatAmount(owned),
                   tierIndex >= 0 ? clicker.Tiers[tierIndex].UIColor : RowTextColor);
        }

        noConsumablesLabel.gameObject.SetActive(shown == 0);
        if (shown == 0)
        {
            noConsumablesLabel.text = (onSeeds ? noSeedsText : onSupplies ? noSuppliesText : onDevices ? noDevicesText : noConsumablesText).ToUpperInvariant();
            noConsumablesLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
        }

        return y;
    }
}
