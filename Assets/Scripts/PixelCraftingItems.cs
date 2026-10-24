// ---- System notes ----
// PixelCraftingItems.cs
//   Part of PixelCrafting (partial class): the item icons, the two boxes, dragging from the list and the Materials list (category headers, search, cells). The system notes are in PixelCrafting.cs.
// ---------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelCrafting
{
    // ------------------------------------------------------------------
    // Icons
    // ------------------------------------------------------------------

    private Color TierColor(PixelClicker.PixelType type)
    {
        int i = clicker.IndexOf(type);
        if (i < 0) return Color.white;
        PixelClicker.PixelTier t = clicker.Tiers[i];
        return t.translucent ? t.color : t.UIColor;
    }

    private string ItemName(Item item)
    {
        if (item.kind == ItemKind.Pixel)
        {
            int i = clicker.IndexOf(item.type);
            return i >= 0 ? clicker.Tiers[i].displayName : item.type.ToString();
        }
        if (item.kind == ItemKind.Device)
        {
            int dv = FindDevice(item.device);
            return dv >= 0 ? consumables.ItemName(dv) : item.device.ToString();
        }
        int p = PotionIndexOf(item);
        return p >= 0 ? consumables.ItemName(p) : (clicker != null ? clicker.DisplayNameOf(item.type) : PixelClicker.TypeName(item.type)) + " Potion";
    }

    /// <summary>The consumables index of a potion item (plain, 2-type or 3-type combo), or -1.</summary>
    private int PotionIndexOf(Item item)
    {
        if (consumables == null || item.kind != ItemKind.Potion) return -1;
        if (!item.combo) return FindPotion(item.type);
        return item.hasThird ? consumables.FindComboPotion(item.type, item.second, item.third) : consumables.FindComboPotion(item.type, item.second);
    }

    private int FindPotion(PixelClicker.PixelType type)
    {
        if (consumables == null) return -1;
        for (int i = 0; i < consumables.ItemCount && !consumables.IsDevice(i); i++)
            if (!consumables.ItemCraftOnly(i) && consumables.ItemRequiredType(i) == type) return i;
        return -1;
    }

    private int FindDevice(PixelConsumables.DeviceKind kind)
    {
        if (consumables == null) return -1;
        for (int i = 0; i < consumables.ItemCount; i++)
            if (consumables.IsDevice(i) && consumables.DeviceKindOf(i) == kind) return i;
        return -1;
    }

    private PixelCubeIcon MakeCube(Transform parent, string name, Color color, float fill)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        PixelCubeIcon icon = go.AddComponent<PixelCubeIcon>();
        icon.color = color;
        icon.fill = fill;
        icon.spinDegreesPerSecond = cubeSpin;
        PixelUIKit.Stretch(icon.rectTransform);
        return icon;
    }

    /// <summary>A pixel is a spinning cube; a potion is a glass cube with a smaller cube of its pixel inside.</summary>
    private GameObject BuildIcon(Transform parent, Item item, Vector2 size)
    {
        GameObject root = new GameObject("Icon", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rr = root.GetComponent<RectTransform>();
        if (size == Vector2.zero) PixelUIKit.Stretch(rr);
        else
        {
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = size;
        }

        Color c = TierColor(item.type);
        if (item.kind == ItemKind.Device)
        {
            int dv = FindDevice(item.device);
            Color dc = dv >= 0 ? consumables.DeviceColorOf(dv) : Color.grey;
            MakeCube(root.transform, "Cube", new Color(dc.r, dc.g, dc.b, 1f), 0.62f);
            MakeCube(root.transform, "Frame", new Color(0.95f, 0.8f, 0.3f, 0.35f), 0.7f);   // a gold rim marks it as a device, not a pixel
        }
        else if (item.kind == ItemKind.Pixel)
        {
            MakeCube(root.transform, "Cube", c, 0.62f);
        }
        else
        {
            if (item.combo)
            {
                // Two (or three) small cubes side by side inside the glass.
                int parts = item.hasThird ? 3 : 2;
                Color[] colours = { c, TierColor(item.second), TierColor(item.third) };
                for (int k = 0; k < parts; k++)
                {
                    GameObject half = new GameObject("Part " + k, typeof(RectTransform));
                    half.transform.SetParent(root.transform, false);
                    RectTransform hr = half.GetComponent<RectTransform>();
                    float left = 0.08f + (0.84f * k) / parts, right = 0.08f + (0.84f * (k + 1)) / parts;
                    hr.anchorMin = new Vector2(left, 0f);
                    hr.anchorMax = new Vector2(right, 1f);
                    hr.offsetMin = hr.offsetMax = Vector2.zero;
                    Color ck = colours[k];
                    MakeCube(half.transform, "Inner", new Color(ck.r, ck.g, ck.b, 1f), (parts == 3 ? 0.62f : 0.8f) * potionInnerScale * 1.2f);
                }
            }
            else
            {
                MakeCube(root.transform, "Inner", new Color(c.r, c.g, c.b, 1f), 0.62f * potionInnerScale);
            }
            MakeCube(root.transform, "Glass", glassColor, 0.62f);
        }
        return root;
    }

    // ------------------------------------------------------------------
    // The two boxes
    // ------------------------------------------------------------------

    private void SetSlot(int index, Item item, bool pinned = false)
    {
        Slot s = slots[index];
        if (s.icon != null) Destroy(s.icon);
        s.has = true;
        s.pinned = pinned;
        s.item = item;
        s.icon = BuildIcon(s.iconHost.transform, item, Vector2.zero);
        s.label.text = ItemName(item);
        Refresh();
    }

    private void ClearSlot(int index)
    {
        Slot s = slots[index];
        if (s.icon != null) Destroy(s.icon);
        s.icon = null;
        s.has = false;
        s.pinned = false;
        s.label.text = "";
        Refresh();
    }

    private void FillBoxes(Recipe r)
    {
        for (int i = 0; i < 2; i++) ClearSlotQuiet(i);
        SetSlot(0, ItemOf(r.a), true);
        SetSlot(1, ItemOf(r.b), true);
        PixelAudio.Play("ui_click");
    }

    private void ClearSlotQuiet(int index)
    {
        Slot s = slots[index];
        if (s.icon != null) Destroy(s.icon);
        s.icon = null;
        s.has = false;
        s.pinned = false;
        s.label.text = "";
    }

    /// <summary>Every way to craft this potion (a plain potion: its recipe; a 2-type combo: either potion + the other pixel; a 3-type combo: any pair potion + the last pixel).</summary>
    private List<Recipe> Decompositions(Item potion)
    {
        List<Recipe> list = new List<Recipe>();
        if (!potion.combo)
        {
            foreach (Recipe r in recipes)
                if (r != null && r.resultKind == ResultKind.Potion && r.resultPotion == potion.type) list.Add(ScaleRecipe(r));
            return list;
        }
        PixelClicker.PixelType[] types = potion.hasThird ? new[] { potion.type, potion.second, potion.third } : new[] { potion.type, potion.second };
        for (int k = 0; k < types.Length; k++)
        {
            PixelClicker.PixelType pixel = types[k];
            Item rest;
            if (potion.hasThird)
            {
                PixelClicker.PixelType x = types[(k + 1) % 3], y = types[(k + 2) % 3];
                rest = PairItem(x, y);
            }
            else rest = new Item(ItemKind.Potion, types[1 - k]);
            Recipe r = ScaleRecipe(FindComboRecipe(rest, new Item(ItemKind.Pixel, pixel)));
            if (r != null) list.Add(r);
        }
        return list;
    }

    /// <summary>The 2-type combo potion item for two pixel types.</summary>
    private Item PairItem(PixelClicker.PixelType a, PixelClicker.PixelType b)
    {
        int index = consumables != null ? consumables.FindComboPotion(a, b) : -1;
        return index >= 0 ? new Item(ItemKind.Potion, consumables.ItemRequiredType(index), true, consumables.ItemSecondType(index))
                          : new Item(ItemKind.Potion, a, true, b);
    }

    private void QuickPlace(Item item)
    {
        int target = !slots[0].has ? 0 : !slots[1].has ? 1 : 1; // both full: replace the second
        SetSlot(target, item);
    }

    // ------------------------------------------------------------------
    // Dragging from the list
    // ------------------------------------------------------------------

    private void BeginDrag(Item item, PointerEventData e)
    {
        dragItem = item;
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = BuildIcon(canvasRoot.transform, item, new Vector2(cellSize * 0.9f, cellSize * 0.9f));
        dragGhost.transform.SetAsLastSibling();
        dragGhost.transform.position = e.position;
    }

    private void Drag(PointerEventData e)
    {
        if (dragGhost != null) dragGhost.transform.position = e.position;
    }

    private void EndDrag(PointerEventData e)
    {
        if (dragGhost == null) return;   // the drag was refused (a greyed-out item)
        Destroy(dragGhost);
        dragGhost = null;
        for (int i = 0; i < slots.Length; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(slots[i].rect, e.position, null))
            {
                SetSlot(i, dragItem);
                return;
            }
    }

    // ------------------------------------------------------------------
    // Item list
    // ------------------------------------------------------------------

    private double Shown(Item item)
    {
        if (item.kind == ItemKind.Pixel) return Math.Floor(clicker.GetCount(item.type));
        return Have(item);
    }

    private double Have(Item item)
    {
        if (item.kind == ItemKind.Pixel) return PixelClicker.InfiniteResources ? 1e9 : Math.Floor(clicker.GetCount(item.type));
        if (item.kind == ItemKind.Device)
        {
            int dv = FindDevice(item.device);
            return dv >= 0 ? consumables.ItemOwned(dv) : 0;
        }
        int p = PotionIndexOf(item);
        return p >= 0 ? consumables.ItemOwned(p) : 0;
    }

    /// <summary>Sizes the window like the shop window (never taller than the space between the black bars); the body is centred a little in the free height.</summary>
    private void FitWindow()
    {
        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        float bars = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        float available = Mathf.Max(400f, canvasRect.rect.height - bars * 2f - 16f);
        PixelShop shop = PixelFind.First<PixelShop>();
        float width = shop != null ? shop.PanelSize.x : windowWidth;
        float height = shop != null ? shop.PanelSize.y : available;   // too tall for the screen (high UI scale)? PixelPop shrinks the whole window to fit
        float key = width * 10000f + height;
        if (Mathf.Approximately(key, fittedHeight)) return;
        fittedHeight = key;

        RectTransform wr = windowObject.GetComponent<RectTransform>();
        wr.sizeDelta = new Vector2(width, height);
        wr.anchoredPosition = Vector2.zero; // the bars are the same height top and bottom, so centred is between them

        float spare = Mathf.Max(0f, height - bodyHeight - 20f);
        bodyRect.anchoredPosition = new Vector2(0f, -spare * 0.35f);
    }

    /// <summary>The device kinds some recipe uses as an ingredient (only those are listed among the materials).</summary>
    private IEnumerable<PixelConsumables.DeviceKind> DeviceIngredientKinds()
    {
        HashSet<PixelConsumables.DeviceKind> kinds = new HashSet<PixelConsumables.DeviceKind>();
        foreach (Recipe r in recipes)
        {
            if (r == null) continue;
            if (r.a.kind == ItemKind.Device) kinds.Add(r.a.device);
            if (r.b.kind == ItemKind.Device) kinds.Add(r.b.device);
        }
        kinds.Add(PixelConsumables.DeviceKind.Vacuum);   // (attuning: a device + a potion)
        kinds.Add(PixelConsumables.DeviceKind.Fan);
        kinds.Add(PixelConsumables.DeviceKind.Magnet);
        return kinds;
    }

    private void RefreshList()
    {
        FitWindow();
        List<Item> items = new List<Item>();
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
            if (Math.Floor(t.count) >= 1d && !t.rareDrop) items.Add(new Item(ItemKind.Pixel, t.type)); // Dragon Cubes are for the wish, not crafting
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount && !consumables.IsDevice(i); i++)
                if (consumables.ItemCraftOnly(i) ? consumables.Get(i).owned > 0 : consumables.ItemOwned(i) > 0) // combo potions only when really held (Infinite resources would list all of them)
                    items.Add(consumables.ItemCraftOnly(i)
                        ? new Item(ItemKind.Potion, consumables.ItemRequiredType(i), true, consumables.ItemSecondType(i), consumables.ItemHasThird(i), consumables.ItemThirdType(i))
                        : new Item(ItemKind.Potion, consumables.ItemRequiredType(i)));

        if (consumables != null)
            foreach (PixelConsumables.DeviceKind kind in DeviceIngredientKinds())
            {
                int dv = FindDevice(kind);
                if (dv >= 0 && consumables.ItemOwned(dv) > 0) items.Add(new Item(ItemKind.Device, PixelClicker.PixelType.White) { device = kind });
            }

        // Search, then lay the rest out under foldable category headers (Pixels / Potions / Devices).
        bool searching = materialSearch.Length > 0;
        if (searching) items.RemoveAll(it => !ItemName(it).ToLowerInvariant().Contains(materialSearch));
        int[] totals = new int[3];
        foreach (Item it in items) totals[(int)it.kind]++;
        List<Item> shown = new List<Item>();
        List<float> shownY = new List<float>();
        float y = 0f;
        int headerCount = 0;
        for (int c = 0; c < 3; c++)
        {
            if (totals[c] == 0) continue;
            MaterialHeader h = GetHeader(headerCount++);
            h.category = c;
            h.rect.gameObject.SetActive(true);
            h.rect.anchoredPosition = new Vector2(0f, -y);
            bool open = searching || !materialsCollapsed[c];
            h.label.text = (open ? "- " : "+ ") + CategoryNames[c] + " (" + totals[c] + ")";
            y += HeaderHeight;
            if (!open) continue;
            foreach (Item it in items)
                if ((int)it.kind == c) { shown.Add(it); shownY.Add(y); y += RowHeight; }
        }
        for (int i = headerCount; i < headers.Count; i++) headers[i].rect.gameObject.SetActive(false);

        while (cells.Count < shown.Count) cells.Add(BuildCell());

        bool oneFilled = slots[0].has != slots[1].has;
        Item filledItem = slots[0].has ? slots[0].item : slots[1].item;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            bool used = i < shown.Count;
            if (cell.rect.gameObject.activeSelf != used) cell.rect.gameObject.SetActive(used);
            if (!used) continue;

            Item item = shown[i];
            if (!cell.built || !SameItem(cell.item, item))
            {
                if (cell.icon != null) Destroy(cell.icon);
                cell.icon = BuildIcon(cell.iconHost.transform, item, Vector2.zero);
                cell.item = item;
                cell.built = true;
                cell.nameLabel.text = ItemName(item);
            }
            cell.count.text = PixelClicker.FormatNumber(Shown(item)); // the real amount (Infinite resources only affects what can be spent)

            // With one box filled, only the items that make something with it stay bright.
            bool compatible = !oneFilled || FindRecipe(filledItem, item) != null;
            cell.disabled = !compatible;
            if (cell.group != null) cell.group.alpha = compatible ? 1f : 0.3f;

            cell.rect.anchoredPosition = new Vector2(0f, -shownY[i]);
        }

        float contentHeight = items.Count > 0 ? y : fontSize * 2.6f;
        emptyLabel.text = searching ? "Nothing matches your search." : emptyItemsText;
        emptyLabel.gameObject.SetActive(items.Count == 0);
        PixelUIKit.UpdateScrollView(listScroll, listBar, contentHeight, listScroll.GetComponent<RectTransform>().rect.height);
    }

    // ---- Materials list: category headers + search ----

    private static readonly string[] CategoryNames = { "PIXELS", "POTIONS", "DEVICES" };
    private readonly bool[] materialsCollapsed = new bool[3];
    private string materialSearch = "";
    private TMP_InputField materialSearchField;
    private readonly List<MaterialHeader> headers = new List<MaterialHeader>();
    private float HeaderHeight => RowHeight * 0.55f;

    private class MaterialHeader
    {
        public RectTransform rect;
        public TMP_Text label;
        public int category;
    }

    private MaterialHeader GetHeader(int index)
    {
        while (headers.Count <= index)
        {
            MaterialHeader h = new MaterialHeader();
            GameObject go = new GameObject("Category", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(listContent, false);
            Image im = go.GetComponent<Image>();
            im.color = new Color(cellColor.r, cellColor.g, cellColor.b, 0.55f);
            PixelUIKit.StyleBox(im);
            h.rect = go.GetComponent<RectTransform>();
            h.rect.anchorMin = new Vector2(0f, 1f);
            h.rect.anchorMax = new Vector2(1f, 1f);
            h.rect.pivot = new Vector2(0.5f, 1f);
            h.rect.sizeDelta = new Vector2(0f, HeaderHeight - 6f);
            h.label = MakeLabel(go.transform, "Text", "", fontSize * 0.6f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            h.label.raycastTarget = false;
            RectTransform lr = h.label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(14f, 0f);
            lr.offsetMax = new Vector2(-8f, 0f);
            // (a click, not a drag handler, so the list still scrolls when dragged)
            PixelUIKit.MakeClickable(go, () =>
            {
                materialsCollapsed[h.category] = !materialsCollapsed[h.category];
                RefreshList();
            }, pressTint: false);
            headers.Add(h);
        }
        return headers[index];
    }

    private Cell BuildCell()
    {
        Cell cell = new Cell();
        GameObject go = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(PixelCraftDrag));
        go.transform.SetParent(listContent, false);
        go.GetComponent<Image>().color = cellColor;
        PixelUIKit.StyleBox(go.GetComponent<Image>());   // the same rounded, softly shaded boxes as the shop's rows
        cell.group = go.AddComponent<CanvasGroup>();
        cell.rect = go.GetComponent<RectTransform>();
        cell.rect.anchorMin = new Vector2(0f, 1f);
        cell.rect.anchorMax = new Vector2(1f, 1f);
        cell.rect.pivot = new Vector2(0.5f, 1f);
        cell.rect.sizeDelta = new Vector2(0f, RowHeight - 8f);

        // The cube (or the potion's glass cube) on the left, like the shop's Currency rows.
        float icon = (RowHeight - 8f) * 0.86f;
        cell.iconHost = new GameObject("Icon Host", typeof(RectTransform));
        cell.iconHost.transform.SetParent(go.transform, false);
        RectTransform ir = cell.iconHost.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(icon, icon);
        ir.anchoredPosition = new Vector2(10f, 0f);

        // A small name on top (potions need one) and the amount underneath in the same bold style as the Currency list.
        cell.nameLabel = MakeLabel(go.transform, "Name", "", fontSize * 0.5f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        cell.nameLabel.enableAutoSizing = true;
        cell.nameLabel.fontSizeMax = fontSize * 0.5f;
        cell.nameLabel.fontSizeMin = 9f;
        cell.nameLabel.color = new Color(textColor.r, textColor.g, textColor.b, 0.75f);
        RectTransform nr = cell.nameLabel.rectTransform;
        nr.anchorMin = new Vector2(0f, 0.55f);
        nr.anchorMax = Vector2.one;
        nr.offsetMin = new Vector2(icon + 24f, 0f);
        nr.offsetMax = new Vector2(-8f, -3f);

        cell.count = MakeLabel(go.transform, "Count", "", fontSize * 0.95f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        cell.count.enableAutoSizing = true;
        cell.count.fontSizeMax = fontSize * 0.95f;
        cell.count.fontSizeMin = 12f;
        RectTransform cr = cell.count.rectTransform;
        cr.anchorMin = Vector2.zero;
        cr.anchorMax = new Vector2(1f, 0.58f);
        cr.offsetMin = new Vector2(icon + 24f, 3f);
        cr.offsetMax = new Vector2(-8f, 0f);

        PixelHoverTip cellHover = go.AddComponent<PixelHoverTip>();
        cellHover.onEnter = () => { if (!cell.disabled) craftTip = materialTipText; };
        cellHover.onExit = () => { if (craftTip == materialTipText) craftTip = null; };

        PixelCraftDrag drag = go.GetComponent<PixelCraftDrag>();
        drag.onBegin = e => { if (!cell.disabled) BeginDrag(cell.item, e); };
        drag.onDrag = Drag;
        drag.onEnd = EndDrag;
        drag.onClick = e =>
        {
            if (e.button != PointerEventData.InputButton.Left || cell.disabled) return;
            QuickPlace(cell.item);   // into the first free box
        };
        return cell;
    }
}
