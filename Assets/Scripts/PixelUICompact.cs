// ---- System notes ----
// PixelUICompact.cs
//   Part of PixelUI (partial class): the pinned (compact) Inventory: its toggle, prefs, drag / minimise / padlock buttons, placement, the currency <-> consumables switch, the Inventory key cycle and its consumable swatches. The system notes are in PixelUI.cs.
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
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetKeyCycle() => keyCycled = false;   // (a fresh play mode start: the Inventory key has not switched tabs yet)

    /// <summary>The little button at the window's top left that switches the simplified (pinned) Inventory on and off.</summary>
    private void BuildCompactToggle()
    {
        if (!autoMode) return;
        float size = CompactToggleSize;
        compactButton = windowParts.AddHeaderButton("Compact Toggle", "", true);   // the standard left header slot

        PixelUIKit.AddPinIcon(compactButton, size * 0.57f);   // the same pushpin as the Skills window's pin button

        BuildCompactSwitch(size);
        BuildCompactWindowButtons(size);

        PixelHoverTip tip = compactButton.gameObject.AddComponent<PixelHoverTip>();
        tip.onEnter = () => tooltipCustomText = Compact ? compactOffTip : pinTip;
        tip.onExit = HideTooltip;
        compactButton.onClick.AddListener(() =>
        {
            CompactMode = Compact ? 0 : (inventoryTab == 1 ? 2 : 1);   // pins the tab you are looking at
            if (listContent != null) listContent.anchoredPosition = Vector2.zero;
            Refresh();
        });
    }

    // ---- Movable pinned window: drag anywhere, minimise, stick in place ----
    private const string PrefCompactX = "PixelClicker.Inventory.CompactX";
    private const string PrefCompactY = "PixelClicker.Inventory.CompactY";
    private const string PrefCompactSmall = "PixelClicker.Inventory.CompactSmall";
    private const string PrefCompactStuck = "PixelClicker.Inventory.CompactStuck";
    private Button compactSmallButton, compactStickButton;
    private TMP_Text compactSmallLabel;
    private Image compactStickImage;
    private bool? shownCompactOn, shownStuck;   // the compact toggle's / lock's last shown state (null = not set yet)
    private int shownSubTab = -1;               // the Inventory tab last shown as Selected
    private GameObject compactLockClosed, compactLockOpen;
    private PixelPopoutDrag compactDrag;
    private bool compactSmall, compactStuck, compactPrefsLoaded, compactPlaced;
    private Vector2 compactDesired;   // where the player put the window; the real position is this pushed back on screen, so resizing never loses it
    private Vector2 normalBoxPosition;

    private void LoadCompactPrefs()
    {
        if (compactPrefsLoaded) return;
        compactPrefsLoaded = true;
        compactSmall = PixelPrefs.GetInt(PrefCompactSmall, 0) == 1;
        compactStuck = PixelPrefs.GetInt(PrefCompactStuck, 0) == 1;
    }

    /// <summary>Two small buttons stacked at the pinned window's top right: minimise (- / +) and stick (a padlock), plus the drag handler on the whole box.</summary>
    private void BuildCompactWindowButtons(float size)
    {
        LoadCompactPrefs();
        normalBoxPosition = boxRect.anchoredPosition;
        compactDrag = boxObject.AddComponent<PixelPopoutDrag>();
        compactDrag.target = boxRect;
        compactDrag.canvas = autoRoot.GetComponent<Canvas>();
        compactDrag.onMoved = () => { KeepCompactOnScreen(); compactDesired = boxRect.anchoredPosition; SaveCompactPosition(); };

        float small = size * 0.5f;
        compactSmallButton = MakeButton(boxObject.transform, "Compact Minimise", "", new Vector2(small, small), PixelUITheme.ColorOf(ButtonRole.Neutral), Color.white, small * 0.7f);
        PixelUIKit.SetRole(compactSmallButton, ButtonRole.Neutral);
        RectTransform a = compactSmallButton.GetComponent<RectTransform>();
        a.anchorMin = a.anchorMax = a.pivot = new Vector2(1f, 1f);
        a.anchoredPosition = new Vector2(-panelPadding, -panelPadding * 0.5f);
        compactSmallLabel = compactSmallButton.GetComponentInChildren<TMP_Text>();
        compactSmallButton.onClick.AddListener(() =>
        {
            compactSmall = !compactSmall;
            PixelPrefs.SetInt(PrefCompactSmall, compactSmall ? 1 : 0);
            Refresh();
        });
        PixelHoverTip t1 = compactSmallButton.gameObject.AddComponent<PixelHoverTip>();
        t1.onEnter = () => tooltipCustomText = compactSmall ? "Show the whole list" : "Minimise to just the buttons";
        t1.onExit = HideTooltip;

        compactStickButton = MakeButton(boxObject.transform, "Compact Stick", "", new Vector2(small, small), PixelUITheme.ColorOf(ButtonRole.Neutral), Color.white, small * 0.7f);
        PixelUIKit.SetRole(compactStickButton, ButtonRole.Neutral);
        RectTransform b = compactStickButton.GetComponent<RectTransform>();
        b.anchorMin = b.anchorMax = b.pivot = new Vector2(1f, 1f);
        b.anchoredPosition = new Vector2(-panelPadding, -panelPadding * 0.5f - small - 4f);
        compactStickImage = compactStickButton.GetComponent<Image>();
        compactLockClosed = BuildCompactLock(compactStickButton.transform, false, small);
        compactLockOpen = BuildCompactLock(compactStickButton.transform, true, small);
        compactStickButton.onClick.AddListener(() =>
        {
            compactStuck = !compactStuck;
            PixelPrefs.SetInt(PrefCompactStuck, compactStuck ? 1 : 0);
            UpdatePinnedPlacement();
        });
        PixelHoverTip t2 = compactStickButton.gameObject.AddComponent<PixelHoverTip>();
        t2.onEnter = () => tooltipCustomText = compactStuck ? "Stuck in place - click to let it move again" : "Stick it in place so it can't be moved";
        t2.onExit = HideTooltip;

        compactSmallButton.gameObject.SetActive(false);
        compactStickButton.gameObject.SetActive(false);
    }

    /// <summary>A little padlock from rectangles (closed = shackle in the body, open = lifted).</summary>
    private GameObject BuildCompactLock(Transform parent, bool open, float size)
    {
        GameObject root = new GameObject(open ? "Lock Open" : "Lock Closed", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(size, size);
        float u = size / 24f;
        void Part(Vector2 sz, Vector2 at, Color c)
        {
            GameObject g = new GameObject("Part", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(root.transform, false);
            Image im = g.GetComponent<Image>();
            im.color = c;
            im.raycastTarget = false;
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = sz * u;
            r.anchoredPosition = at * u;
        }
        Color metal = new Color(0.85f, 0.88f, 0.95f), body = new Color(0.95f, 0.75f, 0.25f);
        Part(new Vector2(16f, 10f), new Vector2(0f, -4f), body);
        if (!open)
        {
            Part(new Vector2(3f, 8f), new Vector2(-5f, 5f), metal);
            Part(new Vector2(3f, 8f), new Vector2(5f, 5f), metal);
            Part(new Vector2(13f, 3f), new Vector2(0f, 9f), metal);
        }
        else
        {
            Part(new Vector2(3f, 6f), new Vector2(-5f, 4f), metal);
            Part(new Vector2(3f, 8f), new Vector2(5f, 8f), metal);
            Part(new Vector2(13f, 3f), new Vector2(0f, 12f), metal);
        }
        return root;
    }

    private void SaveCompactPosition()
    {
        PixelPrefs.SetFloat(PrefCompactX, compactDesired.x);
        PixelPrefs.SetFloat(PrefCompactY, compactDesired.y);
    }

    /// <summary>Keeps the pinned window fully on screen, between the black bars.</summary>
    private void KeepCompactOnScreen()
    {
        if (boxRect == null || autoRoot == null) return;
        RectTransform canvasRect = autoRoot.GetComponent<RectTransform>();
        float bars = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 canvasSize = canvasRect.rect.size, size = boxRect.sizeDelta;
        Vector2 an = boxRect.anchorMin, pv = boxRect.pivot;
        Vector2 origin = new Vector2((an.x - 0.5f) * canvasSize.x, (an.y - 0.5f) * canvasSize.y);
        Vector2 offset = new Vector2((0.5f - pv.x) * size.x, (0.5f - pv.y) * size.y);
        Vector2 c = origin + boxRect.anchoredPosition + offset;
        Vector2 half = canvasSize * 0.5f, h = size * 0.5f;
        c.x = Mathf.Clamp(c.x, -half.x + h.x, Mathf.Max(-half.x + h.x, half.x - h.x));
        c.y = Mathf.Clamp(c.y, -half.y + bars + h.y, Mathf.Max(-half.y + bars + h.y, half.y - bars - h.y));
        boxRect.anchoredPosition = c - origin - offset;
    }

    /// <summary>While pinned: apply the saved spot, the stuck padlock and the minimise sign; when un-pinned the window goes back to its normal place.</summary>
    private void UpdatePinnedPlacement()
    {
        if (!autoMode || boxRect == null || compactDrag == null) return;
        LoadCompactPrefs();
        bool compact = Compact;
        if (compact && !compactPlaced)
        {
            compactPlaced = true;
            compactDesired = PixelPrefs.HasKey(PrefCompactX)
                ? new Vector2(PixelPrefs.GetFloat(PrefCompactX), PixelPrefs.GetFloat(PrefCompactY))
                : boxRect.anchoredPosition;
        }
        else if (!compact && compactPlaced)
        {
            compactPlaced = false;
            boxRect.anchoredPosition = normalBoxPosition;
        }
        compactDrag.locked = !compact || compactStuck;
        if (!compact) return;
        boxRect.anchoredPosition = compactDesired;   // always back to the chosen spot (maximising may have pushed it up to fit on screen)
        KeepCompactOnScreen();
        if (compactStickImage != null && shownStuck != compactStuck)   // (runs often: only re-role on a change)
        {
            shownStuck = compactStuck;
            PixelUIKit.SetRole(compactStickImage, compactStuck ? ButtonRole.Success : ButtonRole.Neutral);
        }
        if (compactLockClosed != null && compactLockClosed.activeSelf != compactStuck) compactLockClosed.SetActive(compactStuck);
        if (compactLockOpen != null && compactLockOpen.activeSelf == compactStuck) compactLockOpen.SetActive(!compactStuck);
        if (compactSmallLabel != null) PixelUIKit.SetText(compactSmallLabel, compactSmall ? "+" : "-");
    }

    private Button compactSwitch;
    private GameObject potionIcon, moneyIcon;

    /// <summary>A second button beside the toggle, only while the simplified view is on: swaps between the currency list (shows a potion) and the potion list (shows a coin).</summary>
    private void BuildCompactSwitch(float size)
    {
        compactSwitch = MakeButton(boxObject.transform, "Compact Switch", "", new Vector2(size, size), PixelUITheme.ColorOf(ButtonRole.Neutral), Color.white, size * 0.5f);
        PixelUIKit.SetRole(compactSwitch, ButtonRole.Neutral);
        RectTransform rt = compactSwitch.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = compactButton.GetComponent<RectTransform>().anchoredPosition + new Vector2(size + 8f, 0f);   // just right of the pin

        // Potion: a cork, a thin neck and a round-ish body, drawn from little rectangles.
        potionIcon = new GameObject("Potion Icon", typeof(RectTransform));
        potionIcon.transform.SetParent(compactSwitch.transform, false);
        PixelUIKit.Stretch(potionIcon.GetComponent<RectTransform>());
        IconRect(potionIcon.transform, new Color(0.65f, 0.45f, 0.25f, 1f), size * 0.16f, size * 0.1f, 0f, size * 0.32f);   // cork
        IconRect(potionIcon.transform, new Color(0.85f, 0.9f, 0.95f, 1f), size * 0.12f, size * 0.16f, 0f, size * 0.2f);    // neck
        IconRect(potionIcon.transform, new Color(0.9f, 0.3f, 0.35f, 1f), size * 0.4f, size * 0.3f, 0f, -size * 0.02f);      // body
        IconRect(potionIcon.transform, new Color(0.9f, 0.3f, 0.35f, 1f), size * 0.28f, size * 0.1f, 0f, -size * 0.24f);     // rounded bottom
        IconRect(potionIcon.transform, new Color(1f, 1f, 1f, 0.5f), size * 0.07f, size * 0.16f, -size * 0.1f, -size * 0.02f); // glint

        // Money: a gold coin with a "$".
        moneyIcon = new GameObject("Money Icon", typeof(RectTransform));
        moneyIcon.transform.SetParent(compactSwitch.transform, false);
        PixelUIKit.Stretch(moneyIcon.GetComponent<RectTransform>());
        IconRect(moneyIcon.transform, new Color(0.95f, 0.75f, 0.2f, 1f), size * 0.5f, size * 0.5f, 0f, 0f);
        IconRect(moneyIcon.transform, new Color(0.75f, 0.55f, 0.1f, 1f), size * 0.38f, size * 0.38f, 0f, 0f);
        TMP_Text dollar = MakeText(moneyIcon.transform, "Dollar", "$", size * 0.4f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.92f, 0.5f, 1f));
        dollar.raycastTarget = false;
        PixelUIKit.Stretch(dollar.rectTransform);

        compactSwitch.onClick.AddListener(() =>
        {
            CompactMode = CompactMode == 1 ? 2 : 1;
            if (listContent != null) listContent.anchoredPosition = Vector2.zero;
            Refresh();
        });
        PixelHoverTip tip = compactSwitch.gameObject.AddComponent<PixelHoverTip>();
        tip.onEnter = () => tooltipCustomText = CompactMode == 1 ? switchToPotionsTip : switchToCurrencyTip;
        tip.onExit = HideTooltip;
        compactSwitch.gameObject.SetActive(false);
    }

    /// <summary>The Inventory key in the pinned view: the first press after it opened switches to the other tab (currency <-> consumables); the next press closes it. Returns true when it handled the press.</summary>
    private static bool keyCycled;

    public static bool InventoryKeyCycle()
    {
        PixelUI ui = inventoryInstance;
        if (ui == null || !Compact || !InventoryOpen) { keyCycled = false; return false; }
        if (keyCycled) { keyCycled = false; return false; }   // already switched once since it opened: this press closes it
        keyCycled = true;
        CompactMode = CompactMode == 1 ? 2 : 1;
        if (ui.listContent != null) ui.listContent.anchoredPosition = Vector2.zero;
        ui.Refresh();
        return true;
    }

    private static void IconRect(Transform parent, Color color, float w, float h, float x, float y)
    {
        GameObject go = new GameObject("Part", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image im = go.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>The simplified view's consumables: every potion / device / seed you hold as a swatch + count; a click uses it. Returns the y below.</summary>
    private float RefreshCompactConsumables(float y)
    {
        if (consumables == null) return y;
        if (potionRowObjects == null || potionRowObjects.Length != consumables.ItemCount) BuildPotionRows();
        bool first = true;
        int count = potionRowObjects.Length;
        for (int i = 0; i < count; i++)
        {
            int owned = consumables.ItemOwned(i);
            bool visible = consumables.ShownInInventory(i);
            if (potionRowObjects[i] == null)
            {
                if (!visible) continue;
                CreatePotionRow(i);
            }
            if (potionRowObjects[i].activeSelf != visible) potionRowObjects[i].SetActive(visible);
            if (!visible) continue;

            if (first) { if (y > 0.5f) y += 10f; first = false; }
            potionRowObjects[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
            int tierIndex = clicker.IndexOf(consumables.ItemRequiredType(i));
            SetRow(potionLabels[i], "x" + (owned >= 99999 ? PixelConsumables.OwnedText(owned) : FormatAmount(owned)), "",
                   tierIndex >= 0 ? clicker.Tiers[tierIndex].UIColor : RowTextColor);
        }
        return y;
    }
}
