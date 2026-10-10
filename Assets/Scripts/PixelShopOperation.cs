using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shop's Upgrades &gt; Operation sub-tab: one row per consumable with a rotating 3D picture of it and an orange arrow that opens an upgrade
/// window for it (the shared upgrades window). Every upgrade has the orange '!' box whose tooltip shows its statistics. Part of
/// <see cref="PixelShop"/>; the data is in <see cref="PixelOperationUpgrade"/>, the effects are applied by <see cref="PixelConsumables"/>.
/// </summary>
public partial class PixelShop
{
    // ------------------------------------------------------------------
    // Data and levels
    // ------------------------------------------------------------------

    private static readonly PixelConsumables.DeviceKind[] OperationKinds =
    {
        PixelConsumables.DeviceKind.Vacuum, PixelConsumables.DeviceKind.Fan, PixelConsumables.DeviceKind.Sorter,
        PixelConsumables.DeviceKind.ChargeBooster, PixelConsumables.DeviceKind.LightningRod,
        PixelConsumables.DeviceKind.ComboFuel, PixelConsumables.DeviceKind.GhostBait, PixelConsumables.DeviceKind.PetTreat,
    };

    /// <summary>Adds any built-in Operation upgrade missing from the list (by id). Returns true if something was added.</summary>
    private bool EnsureDefaultOperationUpgrades()
    {
        if (operationUpgrades == null) operationUpgrades = new List<PixelOperationUpgrade>();
        bool changed = false;
        foreach (PixelOperationUpgrade def in PixelOperationUpgrade.CreateDefaults())
        {
            if (operationUpgrades.Exists(u => u != null && u.id == def.id)) continue;
            operationUpgrades.Add(def);
            changed = true;
        }
        return changed;
    }

    private static string OperationKey(PixelOperationUpgrade u) => "op." + u.id;

    /// <summary>The level bought of an upgrade (kept as a saved stat counter).</summary>
    public int OperationLevel(PixelOperationUpgrade u) => u == null ? 0 : Mathf.Clamp((int)PixelStats.Total(OperationKey(u)), 0, u.maxLevel);

    /// <summary>The total change the bought upgrades make to one stat of a consumable.</summary>
    public float OperationBonus(PixelConsumables.DeviceKind kind, PixelOperationStat stat)
    {
        float total = 0f;
        if (operationUpgrades == null) return 0f;
        foreach (PixelOperationUpgrade u in operationUpgrades)
            if (u != null && u.device == kind && u.stat == stat) total += OperationLevel(u) * u.perLevel;
        return total;
    }

    /// <summary>Price of the next level of an upgrade.</summary>
    public double OperationCost(PixelOperationUpgrade u) => System.Math.Ceiling(u.baseCost * System.Math.Pow(u.costGrowth, OperationLevel(u)));

    /// <summary>Buys the next level of an upgrade. False if it is maxed or you can't pay.</summary>
    public bool TryBuyOperation(PixelOperationUpgrade u)
    {
        if (u == null || consumables == null || OperationLevel(u) >= u.maxLevel) return false;
        if (!clicker.TrySpend(consumables.OperationCostType(u.device), OperationCost(u))) return false;
        PixelStats.Best(OperationKey(u), OperationLevel(u) + 1);
        PixelAudio.Play("purchase");
        PixelHints.Announce(consumables.OperationName(u.device) + ": " + u.displayName + " level " + OperationLevel(u));
        return true;
    }

    private string OperationStatText(PixelOperationUpgrade u)
    {
        int level = OperationLevel(u);
        float baseValue = consumables.OperationBase(u.device, u.stat);
        float now = baseValue + level * u.perLevel, next = baseValue + (level + 1) * u.perLevel, max = baseValue + u.maxLevel * u.perLevel;
        string unit = u.unit ?? "";
        string line = "<b>" + u.displayName + "</b>   Level " + level + "/" + u.maxLevel + "\n";
        if (!string.IsNullOrEmpty(u.description)) line += u.description + "\n";
        line += "Now: " + now.ToString("0.##") + unit;
        if (level < u.maxLevel) line += "\nNext level: " + next.ToString("0.##") + unit + "  (" + (u.perLevel >= 0f ? "+" : "") + u.perLevel.ToString("0.##") + unit + ")";
        line += "\nAt max: " + max.ToString("0.##") + unit;
        return line;
    }

    // ------------------------------------------------------------------
    // UI: the consumable rows (Upgrades > Operation) and the upgrade window
    // ------------------------------------------------------------------

    private class OperationRow
    {
        public PackRow row;
        public RawImage icon;
        public Button arrow;
        public PixelConsumables.DeviceKind kind;
        public int studioIndex;
    }

    private class OperationStudio
    {
        public GameObject root;
        public Transform pivot;
        public Camera cam;
        public RenderTexture rt;
    }

    private OperationRow[] operationRows;
    private PackRow[] operationUpgradeRows;
    private OperationStudio[] operationStudios;
    private int openOperation = -1;   // index into OperationKinds of the consumable whose upgrade window is open (-1 = none)
    private const float OperationStudioHeight = -3700f;

    private void EnsureOperationRows()
    {
        if (consumables == null) return;
        if (operationRows == null)
        {
            operationRows = new OperationRow[OperationKinds.Length];
            operationStudios = new OperationStudio[OperationKinds.Length];
            for (int i = 0; i < OperationKinds.Length; i++)
            {
                OperationRow o = new OperationRow { kind = OperationKinds[i], studioIndex = i };
                o.row = BuildPlainRow(contentRect, "Operation " + i);
                o.row.buyButton.gameObject.SetActive(false);
                float picture = Mathf.Max(60f, rowHeight - 24f);

                // The rotating picture on the left, framed.
                GameObject frame = new GameObject("Picture", typeof(RectTransform), typeof(Image));
                frame.transform.SetParent(o.row.rect, false);
                frame.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
                PixelUIKit.StyleBox(frame.GetComponent<Image>(), true);
                frame.GetComponent<Image>().raycastTarget = false;
                RectTransform fr = frame.GetComponent<RectTransform>();
                fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 0.5f);
                fr.sizeDelta = new Vector2(picture, picture);
                fr.anchoredPosition = new Vector2(14f, 0f);
                GameObject imageGo = new GameObject("Model", typeof(RectTransform), typeof(RawImage));
                imageGo.transform.SetParent(frame.transform, false);
                o.icon = imageGo.GetComponent<RawImage>();
                o.icon.raycastTarget = false;
                PixelUIKit.Stretch(imageGo.GetComponent<RectTransform>());

                // Everything else moves right of the picture.
                float shift = picture + 14f;
                o.row.nameLabel.rectTransform.offsetMin += new Vector2(shift, 0f);
                o.row.costLabel.rectTransform.offsetMin += new Vector2(shift, 0f);
                Transform info = o.row.rect.Find("Info");
                if (info != null) info.GetComponent<RectTransform>().anchoredPosition += new Vector2(shift, 0f);

                // The orange upgrade arrow on the right.
                o.arrow = CreateButton(o.row.rect, "Upgrades Arrow", upgradesArrowText, upgradesArrowSize, upgradesArrowColor, textColor, buyFontSize, out _, out _);
                PixelUIKit.UseRightTriangle(o.arrow.gameObject, o.arrow.GetComponentInChildren<TMP_Text>(true), textColor, upgradesArrowSize);
                RectTransform ar = o.arrow.GetComponent<RectTransform>();
                ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(1f, 0.5f);
                ar.anchoredPosition = new Vector2(-20f, 0f);
                int captured = i;
                o.arrow.onClick.AddListener(() => OpenOperationWindow(captured));

                operationRows[i] = o;
            }
        }

        if (operationUpgradeRows == null || operationUpgradeRows.Length != operationUpgrades.Count)
        {
            if (operationUpgradeRows != null)
                foreach (PackRow old in operationUpgradeRows) if (old != null && old.rect != null) Destroy(old.rect.gameObject);
            operationUpgradeRows = new PackRow[operationUpgrades.Count];
            for (int i = 0; i < operationUpgradeRows.Length; i++)
            {
                PackRow row = BuildPlainRow(subContentRect, "Operation Upgrade " + i);
                PixelOperationUpgrade u = operationUpgrades[i];
                row.buyButton.onClick.AddListener(() => { if (TryBuyOperation(u)) RefreshRows(); });
                operationUpgradeRows[i] = row;
            }
        }
    }

    /// <summary>Opens the upgrade window of the consumable at this index of the Operation list.</summary>
    private void OpenOperationWindow(int kindIndex)
    {
        openOperation = kindIndex;
        openParent = -1;
        subContentRect.anchoredPosition = Vector2.zero;
        subScrollRect.StopMovement();
        subPanelObject.SetActive(true);
        RefreshRows();
    }

    /// <summary>Called from RefreshRows: places the Operation rows / upgrade rows and fills their texts.</summary>
    private void RefreshOperation(bool onUpgrades, ref float y, ref int visibleCount, ref float subY, ref int subVisibleCount)
    {
        EnsureOperationRows();
        if (operationRows == null) return;

        bool showKinds = onUpgrades && upgradesSubTab == 2;
        for (int i = 0; i < operationRows.Length; i++)
        {
            OperationRow o = operationRows[i];
            bool visible = showKinds && consumables.OperationListed(o.kind);
            o.row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            o.row.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowHeight + rowSpacing;
            visibleCount++;

            int bought = 0, total = 0;
            foreach (PixelOperationUpgrade u in operationUpgrades)
                if (u != null && u.device == o.kind) { bought += OperationLevel(u); total += u.maxLevel; }
            PixelUIKit.SetText(o.row.nameLabel, consumables.OperationName(o.kind));
            PixelUIKit.SetText(o.row.descLabel, consumables.OperationDescription(o.kind));
            PixelUIKit.SetText(o.row.costLabel, "<color=#" + ColorUtility.ToHtmlStringRGB(levelColor) + ">" + string.Format(operationSummaryFormat, bought, total) + "</color>");
            if (o.arrow.gameObject.activeSelf != (total > 0)) o.arrow.gameObject.SetActive(total > 0);
        }

        // The upgrade window of one consumable.
        if (openOperation >= 0 && openOperation < OperationKinds.Length && !consumables.OperationListed(OperationKinds[openOperation])) CloseUpgradesWindow();
        PixelConsumables.DeviceKind openKind = openOperation >= 0 ? OperationKinds[openOperation] : PixelConsumables.DeviceKind.Seed;
        for (int i = 0; i < operationUpgradeRows.Length; i++)
        {
            PackRow row = operationUpgradeRows[i];
            PixelOperationUpgrade u = operationUpgrades[i];
            bool visible = openOperation >= 0 && u != null && u.device == openKind;
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            row.rect.anchoredPosition = new Vector2(0f, -subY);
            subY += rowHeight + rowSpacing;
            subVisibleCount++;

            int level = OperationLevel(u);
            bool maxed = level >= u.maxLevel;
            PixelClicker.PixelType costType = consumables.OperationCostType(u.device);
            int tierIndex = clicker.IndexOf(costType);
            string pixelName = tierIndex >= 0 ? clicker.Tiers[tierIndex].displayName : costType.ToString();
            double cost = OperationCost(u), have = tierIndex >= 0 ? clicker.Tiers[tierIndex].count : 0d;
            bool enough = PixelClicker.InfiniteResources || have >= cost;

            row.nameLabel.text = u.displayName;
            ApplyLevelWidgets(row, level, u.maxLevel);
            row.descLabel.text = OperationStatText(u);   // the orange '!' box shows this as its tooltip
            row.costLabel.text = maxed ? "" : "<color=#" + ColorUtility.ToHtmlStringRGB(enough ? affordableColor : unaffordableColor) + ">" +
                                 string.Format(NoHave(operationCostFormat), PixelClicker.FormatNumberShort(cost), pixelName, PixelClicker.FormatNumberShort(have)) + "</color>";
            bool canBuy = !maxed && enough;
            row.buyButton.interactable = canBuy;
            row.buyLabel.text = maxed ? maxedText : upgradeText;
            row.buyImage.color = canBuy ? buyColor : disabledColor;
        }
        if (openOperation >= 0) PixelUIKit.SetText(subTitle, string.Format(operationWindowTitle, consumables.OperationName(openKind)));
    }

    // ------------------------------------------------------------------
    // The rotating 3D pictures
    // ------------------------------------------------------------------

    private OperationStudio BuildOperationStudio(int index)
    {
        GameObject model = consumables.CreateDisplayModel(OperationKinds[index]);
        if (model == null) return null;

        GameObject root = new GameObject("Operation Studio " + index);
        root.transform.position = new Vector3(index * 90f, OperationStudioHeight, 0f);
        GameObject pivot = new GameObject("Pivot");
        pivot.transform.SetParent(root.transform, false);
        model.transform.SetParent(pivot.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);

        // Centre the model on the pivot and fit the camera to it.
        Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (r.name == "Range" || r.name == "Blow Area" || r.name == "Bend Cone") { r.enabled = false; continue; }
            if (r.GetComponent<TMP_Text>() != null) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        model.transform.position += root.transform.position - bounds.center;
        float radius = Mathf.Max(0.3f, bounds.extents.magnitude);

        OperationStudio st = new OperationStudio { root = root, pivot = pivot.transform };
        st.rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32) { name = "Operation Picture " + index };
        GameObject camGo = new GameObject("Camera");
        camGo.transform.SetParent(root.transform, false);
        st.cam = camGo.AddComponent<Camera>();
        st.cam.enabled = false;
        st.cam.clearFlags = CameraClearFlags.SolidColor;
        st.cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        st.cam.orthographic = true;
        st.cam.orthographicSize = radius * 1.05f;
        st.cam.nearClipPlane = 0.1f;
        st.cam.farClipPlane = 60f;
        st.cam.allowHDR = false;
        st.cam.targetTexture = st.rt;
        camGo.transform.localPosition = new Vector3(0f, radius * 0.55f, -radius * 2.6f - 4f);
        camGo.transform.LookAt(root.transform.position);

        GameObject lightGo = new GameObject("Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(2f * radius, 3f * radius, -5f * radius);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 40f * radius;
        light.intensity = 7f;
        light.shadows = LightShadows.None;
        return st;
    }

    /// <summary>Turns and re-renders the pictures of the consumable rows that are on screen.</summary>
    private void UpdateOperationPictures()
    {
        if (operationRows == null || currentTab != ShopTab.Upgrades || upgradesSubTab != 2 || !panelObject.activeSelf) return;
        for (int i = 0; i < operationRows.Length; i++)
        {
            OperationRow o = operationRows[i];
            if (!o.row.rect.gameObject.activeInHierarchy) continue;
            if (operationStudios[i] == null) operationStudios[i] = BuildOperationStudio(i);
            OperationStudio st = operationStudios[i];
            if (st == null || st.rt == null) continue;
            if (o.icon.texture != st.rt) o.icon.texture = st.rt;
            st.pivot.localRotation = Quaternion.Euler(0f, Time.unscaledTime * 45f + i * 40f, 0f);
            st.cam.Render();
        }
    }

    private void OnDestroyOperation()
    {
        if (operationStudios == null) return;
        foreach (OperationStudio st in operationStudios)
        {
            if (st == null) continue;
            if (st.rt != null) st.rt.Release();
            if (st.root != null) Destroy(st.root);
        }
    }
}
