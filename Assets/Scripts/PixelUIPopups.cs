// ---- System notes ----
// PixelUIPopups.cs
//   Part of PixelUI (partial class): the popups canvas, the hover tooltips (Ultra / item), the cursor helpers, the click / hit / vacuum gain popups (+N, pooled) and the popup settings. The system notes are in PixelUI.cs.
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
    // Popups
    // ------------------------------------------------------------------

    private void BuildPopupCanvas()
    {
        // Separate overlay canvas so popups work in both automatic and manual mode.
        GameObject go = PixelUIKit.CreateCanvas("PixelUI Popups", popupCanvasOrder, referenceResolution, false);

        popupCanvasRect = go.GetComponent<RectTransform>();
        popupRoot = go;
        BuildTooltip();
    }

    private GameObject popupRoot;

    // Tooltip over a pixel's entry: its Ultra count.
    private GameObject tooltipRoot;
    private TMP_Text tooltipLabel;
    private RectTransform tooltipRect;
    private int tooltipTier = -1;
    private string tooltipCustomText;

    private void BuildTooltip()
    {
        tooltipRoot = new GameObject("Ultra Tooltip", typeof(RectTransform), typeof(Image));
        tooltipRoot.transform.SetParent(popupCanvasRect, false);
        Image bg = tooltipRoot.GetComponent<Image>();
        bg.color = tooltipColor;
        bg.raycastTarget = false;
        tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 1f);

        tooltipLabel = MakeText(tooltipRoot.transform, "Text", "", tooltipFontSize, TextAlignmentOptions.Center, FontStyles.Bold, tooltipTextColor);
        // The text's box is much wider than the tooltip, so it can never wrap (it is centred and simply overflows).
        RectTransform lr = tooltipLabel.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(-400f, 0f);
        lr.offsetMax = new Vector2(400f, 0f);
        tooltipLabel.overflowMode = TextOverflowModes.Overflow;
        tooltipRoot.SetActive(false);
    }

    private void ShowTooltip(int tierIndex)
    {
        tooltipTier = tierIndex;
    }

    private void HideTooltip() { tooltipTier = -1; tooltipCustomText = null; }

    private void ShowItemTooltip(int item)
    {
        if (Compact && consumables != null)
        {
            // The simplified Inventory has no names on its rows: the tip says what it is and what a click does.
            string verb = consumables.IsSeedItem(item) ? "plant" : consumables.IsSupplyItem(item) ? "use" : consumables.IsDevice(item) ? "place" : "drink";
            tooltipCustomText = consumables.ItemName(item) + "  -  click to " + verb;
            return;
        }
        string hint = consumables != null && consumables.IsSeedItem(item) ? seedHint : consumables != null && consumables.IsSupplyItem(item) ? supplyHint : consumables != null && consumables.IsDevice(item) ? deviceHint : potionHint;
        if (consumables == null) { tooltipCustomText = hint; return; }
        // What it is and what it does (the same description as the shop and the 3D window), then how to use it.
        string description = consumables.ItemDescription(item);
        tooltipCustomText = "<b>" + consumables.ItemName(item) + "</b>"
                          + (string.IsNullOrEmpty(description) ? "" : "\n<size=82%>" + description + "</size>")
                          + "\n<size=76%><i>" + hint + "</i></size>";
    }

    private string measuredTipText;
    private Vector2 measuredTipSize;

    private void UpdateTooltip()
    {
        if (tooltipRoot == null) return;
        bool custom = !string.IsNullOrEmpty(tooltipCustomText);
        bool show = custom || (tooltipTier >= 0 && tooltipTier < clicker.Tiers.Length);
        if (tooltipRoot.activeSelf != show) tooltipRoot.SetActive(show);
        if (!show) return;

        string text = custom ? tooltipCustomText
            : clicker.Tiers[tooltipTier].displayName;
        if (custom && text.IndexOf('\n') >= 0)
        {
            // A several-line tip (an item: name + description + how to use it): wraps at itemTipMaxWidth, measured only when the text changes.
            if (text != measuredTipText)
            {
                measuredTipText = text;
                tooltipLabel.textWrappingMode = TextWrappingModes.Normal;
                RectTransform lr = tooltipLabel.rectTransform;
                lr.offsetMin = new Vector2(14f, 6f);
                lr.offsetMax = new Vector2(-14f, -6f);
                tooltipLabel.text = text;
                float maxW = Mathf.Max(160f, itemTipMaxWidth);
                Vector2 pref = tooltipLabel.GetPreferredValues(text, maxW, 0f);
                float w = Mathf.Min(maxW, pref.x);
                pref = tooltipLabel.GetPreferredValues(text, w, 0f);
                measuredTipSize = new Vector2(w + 30f, pref.y + 16f);
            }
            tooltipRect.sizeDelta = measuredTipSize;
            PixelUIKit.PlaceTooltip(tooltipRect, popupCanvasRect, CursorLocal(), 20f);
            return;
        }
        if (measuredTipText != null)
        {
            // back to the one-line tip: its text box is much wider than the tooltip, so it never wraps (centred, it simply overflows)
            measuredTipText = null;
            tooltipLabel.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform lr = tooltipLabel.rectTransform;
            lr.offsetMin = new Vector2(-400f, 0f);
            lr.offsetMax = new Vector2(400f, 0f);
        }
        tooltipLabel.text = text;
        tooltipLabel.ForceMeshUpdate();
        float measured = tooltipLabel.preferredWidth;
        float width = Mathf.Max(measured, text.Length * tooltipFontSize * 0.55f) + 36f; // generous, so the text always fits
        tooltipRect.sizeDelta = new Vector2(width, tooltipFontSize * 1.8f);
        PixelUIKit.PlaceTooltip(tooltipRect, popupCanvasRect, CursorLocal(), 20f);   // bottom left of the pointer
    }

    private Vector2 CursorScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    /// <summary>Cursor position in the popup canvas's local space.</summary>
    private Vector2 CursorLocal()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(popupCanvasRect, CursorScreenPosition(), null, out Vector2 local);
        return local;
    }

    /// <summary>World position of the cube in the popup canvas's local space.</summary>
    private Vector2 CubeLocal()
    {
        Camera cam = clicker.ViewCamera;
        if (cam == null || clicker.PixelTransform == null) return lastCubeLocal;

        Vector3 screen = cam.WorldToScreenPoint(clicker.PixelTransform.position);
        if (screen.z < 0f) return lastCubeLocal; // behind the camera

        RectTransformUtility.ScreenPointToLocalPointInRectangle(popupCanvasRect, screen, null, out Vector2 local);
        lastCubeLocal = local;
        return local;
    }

    private Vector2 lastCubeLocal;

    /// <summary>A click that only damaged a tough pixel: shows how many hits it has taken.</summary>
    private void OnPixelHit(int tierIndex, int hits, int needed, bool automatic)
    {
        if (!PopupsEnabled || !showHitPopups || tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return;

        // Tough pixels are often dark, so lighten the tier colour to keep the counter readable.
        Color color = Color.Lerp(clicker.Tiers[tierIndex].UIColor, Color.white, 0.6f);
        string text = needed < 0 ? hits.ToString() : string.Format(hitPopupFormat, hits, needed);   // a treasure chest (needed < 0) doesn't say how many it needs
        Vector2 jitter = new Vector2(UnityEngine.Random.Range(-popupRandomX, popupRandomX), 0f);

        bool overCube = automatic && autoPopupsOverCube;
        ShowPopup(text, color, overCube ? PopupAnchor.Cube : PopupAnchor.Cursor, Vector2.zero, (overCube ? cubePopupStartOffset : popupStartOffset) + jitter, hitPopupSize, 0f);
    }

    /// <summary>The click that breaks a tough pixel: shows the full counter (e.g. 5/5) before the payout.</summary>
    private void OnPixelFinalHit(int tierIndex, int needed, bool automatic) => OnPixelHit(tierIndex, needed, needed, automatic);

    /// <summary>Called for every collected pixel; automatic clicks pop up over the cube, manual ones at the cursor.</summary>
    private void OnPixelCollected(int tierIndex, double amount, bool automatic)
    {
        AddGainDelta(tierIndex, amount);
        if (!PopupsEnabled || !showGainPopups || tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return;

        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];
        Color color = popupUsesTierColor ? tier.UIColor : popupColor;
        string text = string.Format(popupFormat, FormatAmount(amount));
        Vector2 jitter = new Vector2(UnityEngine.Random.Range(-popupRandomX, popupRandomX), 0f);

        PopupAnchor anchor;
        Vector2 fixedPoint = Vector2.zero, offset;
        if (automatic && autoPopupsOverCube)
        {
            anchor = cubePopupFollowsCube ? PopupAnchor.Cube : PopupAnchor.Fixed;
            if (anchor == PopupAnchor.Fixed) fixedPoint = CubeLocal();
            offset = cubePopupStartOffset;
        }
        else
        {
            anchor = followCursor ? PopupAnchor.Cursor : PopupAnchor.Fixed;
            if (anchor == PopupAnchor.Fixed) fixedPoint = CursorLocal();
            offset = popupStartOffset;
        }

        float delay = tier.clicksToCollect > 1 && showHitPopups && toughPayoutDelay > 0f ? toughPayoutDelay : 0f;   // tough blocks: after the "5/5"
        ShowPopup(text, color, anchor, fixedPoint, offset + jitter, 1f, delay);
    }

    /// <summary>
    /// Currency gained somewhere in the world that is NOT a click (a husk recycled in the Recycler): the "+X" next to that currency's entry adds up like a
    /// click's, and a "+X" popup rises from the world point (smaller than a click's). Nothing when popups are off.
    /// </summary>
    public static void ShowWorldGain(int tierIndex, double amount, Vector3 world, string note = null)
    {
        PixelUI ui = inventoryInstance;
        if (ui == null || ui.clicker == null || amount <= 0d || tierIndex < 0 || tierIndex >= ui.clicker.Tiers.Length) return;
        ui.AddGainDelta(tierIndex, amount);
        if (!PopupsEnabled || !ui.showGainPopups || ui.popupCanvasRect == null) return;
        Camera cam = ui.clicker.ViewCamera;
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z < 0f) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.popupCanvasRect, screen, null, out Vector2 local);
        PixelClicker.PixelTier tier = ui.clicker.Tiers[tierIndex];
        Color color = ui.popupUsesTierColor ? tier.UIColor : ui.popupColor;
        string text = string.Format(ui.popupFormat, ui.FormatAmount(amount));
        if (!string.IsNullOrEmpty(note)) text += " <size=75%><alpha=#CC>(" + note + ")</alpha></size>";   // (a small extra, e.g. the Recycler's streak 'x1.3')
        ui.ShowPopup(text, color, PopupAnchor.Fixed, local, Vector2.zero, 0.75f, 0f);
    }

    /// <summary>A small text popup rising from a world point (e.g. the Kiln's "+1 Charcoal"). Nothing when popups are off.</summary>
    public static void ShowWorldText(string text, Color color, Vector3 world)
    {
        PixelUI ui = inventoryInstance;
        if (ui == null || ui.clicker == null || string.IsNullOrEmpty(text)) return;
        if (!PopupsEnabled || !ui.showGainPopups || ui.popupCanvasRect == null) return;
        Camera cam = ui.clicker.ViewCamera;
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z < 0f) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.popupCanvasRect, screen, null, out Vector2 local);
        ui.ShowPopup(text, color, PopupAnchor.Fixed, local, Vector2.zero, 0.75f, 0f);
    }

    /// <summary>Shows (and adds up) "+X" next to a pixel's entry for each click's payout.</summary>
    private void AddGainDelta(int tierIndex, double amount)
    {
        if (!showGainDeltas || deltaLabels == null || tierIndex < 0 || tierIndex >= deltaLabels.Length) return;

        gainSums[tierIndex] = deltaTimers[tierIndex] > 0f && gainSums[tierIndex] > 0d ? gainSums[tierIndex] + amount : amount;
        deltaLabels[tierIndex].text = string.Format(deltaFormat, FormatAmount(gainSums[tierIndex]));
        deltaTimers[tierIndex] = deltaDuration;
        deltaLabels[tierIndex].gameObject.SetActive(true);
    }

    /// <summary>Starts a "+X" indicator next to every entry that the Vacuum just paid.</summary>
    private void OnVacuumBreakdown(double[] perTier)
    {
        if (!showVacuumDeltas || deltaLabels == null) return;

        for (int i = 0; i < perTier.Length && i < deltaLabels.Length; i++)
        {
            if (perTier[i] <= 0d) continue;
            deltaLabels[i].text = string.Format(deltaFormat, FormatAmount(perTier[i]));
            gainSums[i] = 0d; // the vacuum total replaces the click sum
            deltaTimers[i] = deltaDuration;
            deltaLabels[i].gameObject.SetActive(true);
        }
    }

    /// <summary>Fades and lifts active indicators, then hides them.</summary>
    private void TickDeltas()
    {
        if (deltaLabels == null) return;

        for (int i = 0; i < deltaLabels.Length; i++)
        {
            if (deltaTimers[i] <= 0f) continue;

            deltaTimers[i] -= Time.unscaledDeltaTime;
            float remaining = deltaDuration > 0f ? Mathf.Clamp01(deltaTimers[i] / deltaDuration) : 0f;
            float progress = 1f - remaining;

            Color c = deltaColor;
            c.a = deltaColor.a * remaining;
            deltaLabels[i].color = c;

            RectTransform dr = deltaLabels[i].rectTransform;
            dr.offsetMin = dr.offsetMax = new Vector2(0f, deltaRise * progress);

            if (deltaTimers[i] <= 0f)
            {
                deltaLabels[i].gameObject.SetActive(false);
                gainSums[i] = 0d;
            }
        }
    }

    /// <summary>"+X" popup over the cube totalling everything a Vacuum pixel just sucked up.</summary>
    private void OnPixelsVacuumed(int vacuumTierIndex, double total, int count)
    {
        if (!PopupsEnabled || !showVacuumPopup || count <= 0 || total <= 0d) return;   // (pixel rules: a vacuum pays XP, not currency, so total is 0)

        Color color = clicker.Tiers[vacuumTierIndex].UIColor;
        string text = string.Format(vacuumPopupFormat, FormatAmount(total));
        ShowPopup(text, color, PopupAnchor.Cube, Vector2.zero, cubePopupStartOffset + vacuumPopupExtraOffset, vacuumPopupSize, 0f);
    }

    // ---- player settings: the "+N" popups ----

    private const string PrefPopups = "PixelClicker.Setting.Popups";
    private const string PrefPopupSize = "PixelClicker.Setting.PopupSize";
    public static readonly string[] PopupSizeNames = { "Small", "Medium", "Large", "Extra large" };
    private static readonly float[] PopupSizeScale = { 0.7f, 1f, 1.4f, 1.8f };

    /// <summary>Show the "+N" popups at the cursor / over the cube? (The inventory's "+N" next to each pixel is not affected.)</summary>
    public static bool PopupsEnabled
    {
        get => PixelPrefs.GetInt(PrefPopups, 1) != 0;
        set { PixelPrefs.SetInt(PrefPopups, value ? 1 : 0); PixelPrefs.Save(); }
    }

    /// <summary>Popup size: 0 = small ... 3 = extra large; 1 (medium) is the size set in the Inspector.</summary>
    public static int PopupSizeChoice
    {
        get => Mathf.Clamp(PixelPrefs.GetInt(PrefPopupSize, 1), 0, PopupSizeNames.Length - 1);
        set { PixelPrefs.SetInt(PrefPopupSize, Mathf.Clamp(value, 0, PopupSizeNames.Length - 1)); PixelPrefs.Save(); }
    }

    private static float PopupScale => PopupSizeScale[PopupSizeChoice];

    // ---- the popup pool ----
    // Popups used to be a new GameObject + TextMeshProUGUI + coroutine each (and each one's outline made its own font material copy that was
    // never freed). Now up to 'maxPopups' labels are made once, reused oldest-first, and moved by one loop in Update (TickPopups).

    private enum PopupAnchor { Fixed, Cursor, Cube }

    private class Popup
    {
        public RectTransform rect;
        public TextMeshProUGUI label;
        public bool live;              // in use (waiting out its delay or showing)
        public float startTime;        // Time.unscaledTime it starts showing (later than now while delayed)
        public Color color;
        public PopupAnchor anchor;
        public Vector2 fixedPoint, offset;
        public float appliedOutline = -1f;   // (the outline is set once per label: setting it makes TMP copy the font material)
        public Color appliedOutlineColor;
    }

    private readonly List<Popup> popups = new List<Popup>();

    private void ShowPopup(string text, Color color, PopupAnchor anchor, Vector2 fixedPoint, Vector2 offset, float sizeMultiplier, float delay)
    {
        if (popupCanvasRect == null) return;
        for (int i = popups.Count - 1; i >= 0; i--) if (popups[i] == null || popups[i].rect == null) popups.RemoveAt(i);   // (the canvas was rebuilt)

        Popup p = null;
        for (int i = 0; i < popups.Count; i++) if (!popups[i].live) { p = popups[i]; break; }
        if (p == null && popups.Count < Mathf.Max(1, maxPopups)) { p = MakePopup(); popups.Add(p); }
        if (p == null)   // all in use: take the one that started first
        {
            p = popups[0];
            for (int i = 1; i < popups.Count; i++) if (popups[i].startTime < p.startTime) p = popups[i];
        }

        TextMeshProUGUI tmp = p.label;
        if (font != null && tmp.font != font) tmp.font = font;
        if (popupOutlineWidth > 0f && (p.appliedOutline != popupOutlineWidth || p.appliedOutlineColor != popupOutlineColor))
        {
            tmp.outlineColor = popupOutlineColor;
            tmp.outlineWidth = popupOutlineWidth;
            p.appliedOutline = popupOutlineWidth;
            p.appliedOutlineColor = popupOutlineColor;
        }
        tmp.text = text;
        tmp.fontSize = popupFontSize * sizeMultiplier * PopupScale;
        p.color = color;
        p.anchor = anchor;
        p.fixedPoint = fixedPoint;
        p.offset = offset;
        p.startTime = Time.unscaledTime + Mathf.Max(0f, delay);
        p.live = true;
        p.rect.SetAsLastSibling();   // (newest on top, as before)
        if (delay > 0f) tmp.enabled = false;
        else PlacePopup(p, 0f);
    }

    private Popup MakePopup()
    {
        GameObject go = new GameObject("GainPopup", typeof(RectTransform));
        go.transform.SetParent(popupCanvasRect, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600f, 150f); // wide enough that the text never needs to wrap

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        tmp.enabled = false;
        return new Popup { rect = rt, label = tmp };
    }

    /// <summary>Puts a showing popup where it belongs 'k' (0..1) of the way through its life.</summary>
    private void PlacePopup(Popup p, float k)
    {
        Vector2 anchor = p.anchor == PopupAnchor.Cube ? CubeLocal() : p.anchor == PopupAnchor.Cursor ? CursorLocal() : p.fixedPoint;
        p.rect.anchoredPosition = anchor + p.offset + Vector2.up * (popupRise * k);
        p.rect.localScale = Vector3.one * popupScale.Evaluate(k);
        Color c = p.color;
        c.a = p.color.a * popupAlpha.Evaluate(k);
        p.label.color = c;
        if (!p.label.enabled) p.label.enabled = true;
    }

    /// <summary>Moves and fades every popup in use (once per frame), and frees the finished ones.</summary>
    private void TickPopups()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < popups.Count; i++)
        {
            Popup p = popups[i];
            if (p == null || !p.live || p.rect == null) continue;
            if (now < p.startTime) continue;   // (still waiting out its delay)
            float t = now - p.startTime;
            if (t >= popupDuration) { p.live = false; p.label.enabled = false; continue; }
            PlacePopup(p, popupDuration > 0f ? Mathf.Clamp01(t / popupDuration) : 1f);
        }
    }
}
