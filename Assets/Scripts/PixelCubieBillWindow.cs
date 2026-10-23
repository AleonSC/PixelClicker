// ---- System notes ----
// PixelCubieBillWindow.cs
//   The BILL WINDOW of Cubie's bill (PixelCubieBill, 2026-10-23): a standard Fixed window (PixelUIKit.CreateWindow, its own canvas above the
//   shop, Escape closes it: PixelWindows 45). Opened at the end of Cubie's reveal visit and by the shop's 'IOU' header button.
//   On top: the absurd total owed per colour (the joke) and the trust tier. Then one row per pure colour: name, a bar showing progress to
//   the NEXT milestone (never to the absurd total, which would sit at 0% for hours), 'paid / needed', then the payment controls (2026-10-23):
//   a SLIDER (0..100% of what you can pay: what you hold, capped at what is left of the bill) and an ENTRY BOX kept in step with it (type an
//   amount like 1500, 1,500, 2.5K or 3M - PixelClicker.TryParseAmount; Enter pays), a 'Pay' button that pays the box's amount, and 'Pay max'
//   (up to the next milestone's share, or the rest of the bill once every milestone is met). After a payment the slider and box reset.
//   At the bottom: what the next tier opens and its condition. Refreshed on PixelCubieBill.Paid / TierUnlocked and twice a second while open
//   (the held amounts change as you play) - no per-frame text building.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelCubieBillWindow : MonoBehaviour
{
    private static PixelCubieBillWindow instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private class Row
    {
        public TMP_Text name, amounts;
        public RectTransform fill;
        public Slider slider;
        public TMP_InputField amount;
        public Button pay, payMax;
    }

    private PixelClicker clicker;
    private TMP_FontAsset font;
    private GameObject panel;
    private TMP_Text total, trust, next;
    private readonly List<Row> rows = new List<Row>();
    private float refreshTimer;

    private const float Width = 1460f, Pad = 34f, RowH = 78f, Gap = 10f, TopBlock = 120f, BottomBlock = 110f;

    /// <summary>Is the Bill window open?</summary>
    public static bool IsOpen => instance != null && instance.panel != null && PixelPop.IsOpen(instance.panel);

    /// <summary>Opens the Bill window (only once the bill is revealed).</summary>
    public static void Open()
    {
        PixelCubieBill bill = PixelCubieBill.Instance;
        if (bill == null || !PixelCubieBill.IsRevealed) return;
        if (instance == null)
        {
            PixelClicker clicker = PixelFind.Cached<PixelClicker>();
            if (clicker == null || clicker.UIFont == null) return;
            PixelUIKit.EnsureEventSystem();
            GameObject canvas = PixelUIKit.CreateCanvas("Cubie Bill Canvas", 660, new Vector2(1920f, 1080f), true);   // (above the shop)
            instance = canvas.AddComponent<PixelCubieBillWindow>();
            instance.clicker = clicker;
            instance.font = clicker.UIFont;
            instance.Build(bill);
            PixelWindows.Register(instance, 45, () => IsOpen, Close);
            PixelCubieBill.Paid += instance.Refresh;
            PixelCubieBill.TierUnlocked += instance.OnTier;
        }
        PixelPop.Show(instance.panel);
        instance.Refresh();
    }

    /// <summary>Opens or closes it (the shop's IOU button).</summary>
    public static void Toggle() { if (IsOpen) Close(); else Open(); }

    /// <summary>Closes the Bill window.</summary>
    public static void Close()
    {
        if (instance == null || instance.panel == null) return;
        PixelPop.Hide(instance.panel);
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        PixelCubieBill.Paid -= Refresh;
        PixelCubieBill.TierUnlocked -= OnTier;
        if (instance == this) instance = null;
    }

    private void OnTier(int tier) => Refresh();

    private void Build(PixelCubieBill bill)
    {
        int n = bill.Colours.Count;
        float height = WindowHeight(n);
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, transform, "Panel", "Cubie's bill", new Vector2(Width, height), PixelUIKit.WindowKind.Fixed, Close);
        panel = parts.Root;
        float y = parts.HeaderHeight;
        float inner = Width - 2f * Pad;

        total = PixelUIKit.CreateText(font, panel.transform, "Total", "", 26f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.83f, 0.35f, 1f));
        Place(total.rectTransform, new Vector2(Pad, -y), new Vector2(inner, 44f));
        trust = PixelUIKit.CreateText(font, panel.transform, "Trust", "", 22f, TextAlignmentOptions.Center, FontStyles.Italic, PixelUITheme.DimTextColor);
        Place(trust.rectTransform, new Vector2(Pad, -y - 52f), new Vector2(inner, 60f));
        y += TopBlock;

        float nameW = 130f, amountsW = 220f, sliderW = 250f, fieldW = 170f, payW = 110f, maxW = 130f;
        float barW = inner - nameW - amountsW - sliderW - fieldW - payW - maxW - 6f * Gap;
        for (int i = 0; i < n; i++)
        {
            int colour = i;
            Row r = new Row();
            float ry = -(y + i * (RowH + Gap));
            r.name = PixelUIKit.CreateText(font, panel.transform, "Colour", "", 26f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
            Place(r.name.rectTransform, new Vector2(Pad, ry), new Vector2(nameW, RowH));
            PixelUIKit.FitOneLine(r.name, 14f);

            GameObject track = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(panel.transform, false);
            Image trackImg = track.GetComponent<Image>();
            trackImg.color = new Color(0f, 0f, 0f, 0.35f);
            trackImg.raycastTarget = false;
            PixelUIKit.StyleBox(trackImg, true);
            Place(track.GetComponent<RectTransform>(), new Vector2(Pad + nameW + Gap, ry - RowH * 0.3f), new Vector2(barW, RowH * 0.4f));
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            Image fillImg = fill.GetComponent<Image>();
            fillImg.raycastTarget = false;
            int t = clicker.IndexOf(bill.Colours[i]);
            fillImg.color = t >= 0 ? clicker.Tiers[t].UIColor : Color.white;
            r.fill = fill.GetComponent<RectTransform>();
            r.fill.anchorMin = Vector2.zero; r.fill.anchorMax = new Vector2(0f, 1f);
            r.fill.offsetMin = new Vector2(3f, 3f); r.fill.offsetMax = new Vector2(-3f, -3f);

            r.amounts = PixelUIKit.CreateText(font, panel.transform, "Amounts", "", 21f, TextAlignmentOptions.Center, FontStyles.Normal, PixelUITheme.TextColor);
            Place(r.amounts.rectTransform, new Vector2(Pad + nameW + Gap + barW + Gap, ry), new Vector2(amountsW, RowH));
            PixelUIKit.FitOneLine(r.amounts, 12f);

            // Payment controls: slider (share of what you can pay) <-> entry box, then Pay and Pay max.
            float bx = Pad + nameW + barW + amountsW + 3f * Gap;
            float ch = RowH * 0.62f, cy = ry - (RowH - ch) * 0.5f;
            r.slider = PixelUIKit.CreateSlider(panel.transform, "Pay Slider", new Color(0f, 0f, 0f, 0.45f), fillImg.color, Color.white);
            Place(r.slider.GetComponent<RectTransform>(), new Vector2(bx, cy), new Vector2(sliderW, ch));
            r.amount = PixelUIKit.CreateInputField(font, panel.transform, "Pay Amount", new Vector2(fieldW, ch), new Color(0f, 0f, 0f, 0.4f), Color.white, 22f, "amount");
            Place(r.amount.GetComponent<RectTransform>(), new Vector2(bx + sliderW + Gap, cy), new Vector2(fieldW, ch));
            r.amount.contentType = TMP_InputField.ContentType.Standard;
            r.amount.characterLimit = 16;
            Row row = r;
            r.slider.onValueChanged.AddListener(v =>
            {
                string text = v > 0f ? System.Math.Floor(Payable(colour) * v).ToString("#,0") : "";
                row.amount.SetTextWithoutNotify(text);
                if (row.amount.placeholder != null) row.amount.placeholder.gameObject.SetActive(text.Length == 0);   // (the box's hint hides while it shows an amount)
            });
            r.amount.onValueChanged.AddListener(text =>
            {
                double can = Payable(colour);
                row.slider.SetValueWithoutNotify(PixelClicker.TryParseAmount(text, out double a) && can > 0d ? Mathf.Clamp01((float)(a / can)) : 0f);
            });
            r.amount.onSubmit.AddListener(_ => PayTyped(colour));
            r.pay = PixelUIKit.CreateButton(font, panel.transform, "Pay", "Pay", new Vector2(payW, ch), ButtonRole.Primary, 22f);
            Place(r.pay.GetComponent<RectTransform>(), new Vector2(bx + sliderW + fieldW + 2f * Gap, cy), new Vector2(payW, ch));
            r.pay.onClick.AddListener(() => PayTyped(colour));
            r.payMax = PixelUIKit.CreateButton(font, panel.transform, "Pay Max", "Pay max", new Vector2(maxW, ch), ButtonRole.Success, 22f);
            Place(r.payMax.GetComponent<RectTransform>(), new Vector2(bx + sliderW + fieldW + payW + 3f * Gap, cy), new Vector2(maxW, ch));
            r.payMax.onClick.AddListener(() => PayMax(colour));
            rows.Add(r);
        }
        y += n * (RowH + Gap) + 10f;

        next = PixelUIKit.CreateText(font, panel.transform, "Next", "", 22f, TextAlignmentOptions.Center, FontStyles.Normal, PixelUITheme.TextColor);
        Place(next.rectTransform, new Vector2(Pad, -y), new Vector2(inner, BottomBlock - 20f));
        panel.SetActive(false);
    }

    private static float WindowHeight(int rowsCount) => PixelUIKit.WindowHeaderHeight + TopBlock + rowsCount * (RowH + Gap) + BottomBlock + Pad;

    private static void Place(RectTransform r, Vector2 topLeft, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = topLeft;
        r.sizeDelta = size;
    }

    private double Held(PixelCubieBill bill, int colour)
    {
        int t = clicker.IndexOf(bill.Colours[colour]);
        return t >= 0 ? clicker.Tiers[t].count : 0d;
    }

    /// <summary>The most this colour can take right now: what you hold, capped at what is left of the bill (the slider's 100%).</summary>
    private double Payable(int colour)
    {
        PixelCubieBill bill = PixelCubieBill.Instance;
        if (bill == null) return 0d;
        return System.Math.Floor(System.Math.Max(0d, System.Math.Min(Held(bill, colour), bill.BillPerColour - bill.PaidOf(colour))));
    }

    /// <summary>Pays the amount in the row's entry box (Pay button / Enter), then resets the slider and box.</summary>
    private void PayTyped(int colour)
    {
        PixelCubieBill bill = PixelCubieBill.Instance;
        if (bill == null || colour < 0 || colour >= rows.Count) return;
        Row r = rows[colour];
        if (!PixelClicker.TryParseAmount(r.amount.text, out double amount) || amount < 1d) return;
        bill.Pay(colour, amount);   // (Pay caps it at what you hold and what is left of the bill)
        ResetControls(r);
    }

    private static void ResetControls(Row r)
    {
        r.slider.SetValueWithoutNotify(0f);
        r.amount.SetTextWithoutNotify("");
        if (r.amount.placeholder != null) r.amount.placeholder.gameObject.SetActive(true);
    }

    private void PayMax(int colour)
    {
        PixelCubieBill bill = PixelCubieBill.Instance;
        if (bill == null) return;
        double left = bill.LeftForNext(colour);
        bill.Pay(colour, left > 0d ? left : bill.BillPerColour);   // (every milestone met for this colour: towards the rest of the bill)
        if (colour >= 0 && colour < rows.Count) ResetControls(rows[colour]);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (PixelCalm.InMenu) { Close(); return; }
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f) Refresh();
    }

    private void Refresh()
    {
        refreshTimer = 0.5f;
        PixelCubieBill bill = PixelCubieBill.Instance;
        if (bill == null || panel == null) return;
        PixelUIKit.SetText(total, "Amount owed: " + PixelClicker.FormatNumber(bill.BillPerColour) + " of every pure colour");
        PixelCubieBill.Milestone m = bill.Next;
        IReadOnlyList<PixelCubieBill.Milestone> all = bill.Milestones;
        string tierName = bill.Tier > 0 && bill.Tier <= all.Count ? all[bill.Tier - 1].name : "Customer";
        PixelUIKit.SetText(trust, "Cubie's trust: <b>Tier " + bill.Tier + " - " + tierName + "</b>\n<size=80%>Interest compounds. Allegedly.</size>");
        for (int i = 0; i < rows.Count && i < bill.Colours.Count; i++)
        {
            Row r = rows[i];
            int t = clicker.IndexOf(bill.Colours[i]);
            PixelUIKit.SetText(r.name, t >= 0 ? clicker.Tiers[t].displayName.Replace(" Pixels", "") : bill.Colours[i].ToString());
            double paid = bill.PaidOf(i);
            double target = m != null ? m.perColour : bill.BillPerColour;
            float k = target > 0d ? Mathf.Clamp01((float)(paid / target)) : 1f;
            r.fill.anchorMax = new Vector2(k, 1f);
            PixelUIKit.SetText(r.amounts, PixelClicker.FormatNumberShort(paid) + " / " + PixelClicker.FormatNumberShort(target)
                                          + "\n<size=75%><color=#9FB0CC>you hold " + PixelClicker.FormatNumberShort(Held(bill, i)) + "</color></size>");
            bool canPay = Held(bill, i) >= 1d && paid < bill.BillPerColour;
            r.slider.interactable = canPay;
            r.amount.interactable = canPay;
            r.pay.interactable = canPay;
            r.payMax.interactable = canPay;
        }
        if (m == null) PixelUIKit.SetText(next, "The bill is settled. Cubie is speechless. Nearly.");
        else
        {
            string cond = string.IsNullOrEmpty(m.conditionSkillId) || m.conditionLevel <= 0 ? ""
                        : "\nAlso needs " + m.conditionSkillId + " level " + m.conditionLevel + (bill.ConditionMet(m) ? " (done)" : "");
            PixelUIKit.SetText(next, "Next: <b>Tier " + (bill.Tier + 1) + " - " + m.name + "</b> at " + PixelClicker.FormatNumberShort(m.perColour) + " of each colour"
                                     + (string.IsNullOrEmpty(m.opens) ? "" : "\nOpens: " + m.opens) + cond);
        }
    }
}
