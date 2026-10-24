// ---- System notes ----
// PixelClickerNumbers.cs
//   Part of PixelClicker (partial class): the optional Inspector labels (NotifyChanged, ApplyUIFont, RefreshUI) and number formatting / parsing (AbbreviateNumbers, FormatNumber, FormatNumberShort, TryParseAmount). The system notes are in PixelClicker.cs.
// ---------------------------------------------------
using Unity.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public partial class PixelClicker
{
    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private void NotifyChanged()
    {
        RefreshUI();
        onCurrencyChanged?.Invoke();
        CurrencyChanged?.Invoke();
    }

    /// <summary>Applies the shared UI font to the labels assigned to this script.</summary>
    private void ApplyUIFont()
    {
        if (uiFont == null) return;
        if (summaryLabel != null) summaryLabel.font = uiFont;
        if (nextUnlockLabel != null) nextUnlockLabel.font = uiFont;
        foreach (PixelTier t in tiers)
            if (t.countLabel != null) t.countLabel.font = uiFont;
    }

    /// <summary>Refreshes every assigned label. Safe to call any time.</summary>
    public void RefreshUI()
    {
        if (tiers == null) return;

        // (this runs on every currency change - every click - and these labels are optional Inspector extras, normally all empty:
        //  only format the numbers a label actually shows)
        System.Text.StringBuilder sb = summaryLabel != null ? new System.Text.StringBuilder() : null;

        for (int i = 0; i < tiers.Length; i++)
        {
            PixelTier t = tiers[i];
            bool toSummary = sb != null && t.unlocked;
            if (t.countLabel == null && !toSummary) continue;
            string amount = FormatNumber(t.count);

            if (t.countLabel != null)
            {
                t.countLabel.gameObject.SetActive(t.unlocked);
                t.countLabel.text = string.Format(summaryLineFormat, t.displayName, amount);
            }

            if (toSummary)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.AppendFormat(summaryLineFormat, t.displayName, amount);
            }
        }

        if (summaryLabel != null) summaryLabel.text = sb.ToString();

        if (nextUnlockLabel != null)
        {
            int next = -1;
            for (int i = 1; i < tiers.Length; i++)
            {
                if (!tiers[i].unlocked && tiers[i].unlockMode != TierUnlockMode.ShopOnly) { next = i; break; }
            }

            PixelGoals.GoalDef byGoal = next >= 0 && tiers[next].unlockMode == TierUnlockMode.ByGoal ? PixelGoals.GoalUnlocking(tiers[next].type) : null;
            nextUnlockLabel.text = next < 0
                ? allUnlockedText
                : byGoal != null ? "Goal: " + byGoal.title + " - unlocks " + tiers[next].displayName   // (unlocked by a goal's reward, PixelGoals)
                : string.Format(nextUnlockFormat, FormatNumber(tiers[next].unlockThreshold),
                                tiers[next - 1].displayName, tiers[next].displayName);
        }
    }

    private const string PrefAbbreviate = "PixelClicker.Setting.AbbreviateNumbers";
    private static int abbreviateCache = -1; // -1 = not read yet

    /// <summary>Player setting: show big numbers as 1.2K / 3.4M (on) or in full as 1,200 (off). Remembered between sessions; used by every number display.</summary>
    public static bool AbbreviateNumbers
    {
        get
        {
            if (abbreviateCache < 0) abbreviateCache = PixelPrefs.GetInt(PrefAbbreviate, 1) != 0 ? 1 : 0;
            return abbreviateCache != 0;
        }
        set { abbreviateCache = value ? 1 : 0; PixelPrefs.SetInt(PrefAbbreviate, abbreviateCache); }
    }

    // Short-scale suffixes, one per factor of 1000: K, M, B, T, Qa, Qi, Sx, Sp, Oc, No, then Dc (1e33), UDc, DDc ...
    // up to UCe (1e306), which covers the whole range of a double.
    private static readonly string[] NumberSuffixes = BuildNumberSuffixes();

    private static string[] BuildNumberSuffixes()
    {
        var list = new List<string> { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No" };
        string[] units = { "", "U", "D", "T", "Qa", "Qi", "Sx", "Sp", "O", "N" };
        string[] tens = { "Dc", "Vg", "Tg", "Qag", "Qig", "Sxg", "Spg", "Ocg", "Nog", "Ce" };
        // 10^33 (Dc) .. 10^303 (Ce): unit prefix + tens name, e.g. UDc = 10^36, DVg = 10^69.
        for (int t = 0; t < tens.Length && list.Count < 103; t++)
            for (int u = 0; u < units.Length && list.Count < 103; u++)
                list.Add(units[u] + tens[t]);
        return list.ToArray();
    }

    /// <summary>
    /// Reads a typed amount: '1500', '1,500', '2.5K', '3M', '1B' (K / M / B = thousand / million / billion, any case). False for empty / bad text or a negative.
    /// The ONE parser for typed amounts (ShopText.txt prices, the Bill window's box).
    /// </summary>
    public static bool TryParseAmount(string s, out double amount)
    {
        amount = 0d;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace(",", "").Replace(" ", "");
        double scale = 1d;
        char last = char.ToUpperInvariant(s[s.Length - 1]);
        if (last == 'K') scale = 1e3; else if (last == 'M') scale = 1e6; else if (last == 'B') scale = 1e9;
        if (scale > 1d) s = s.Substring(0, s.Length - 1);
        bool ok = double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out amount);
        amount *= scale;
        return ok && amount >= 0d;
    }

    /// <summary>Always-abbreviated number formatting (1.2K, 3.4M ... from 1K up), whatever the "Abbreviate numbers" setting says. Used by the shop.</summary>
    public static string FormatNumberShort(double value)
    {
        if (value < 999.995) return value.ToString("0.##");
        int s = 0;
        while (value >= 999.995 && s < NumberSuffixes.Length - 1) { value /= 1000; s++; }
        return value.ToString("0.##") + NumberSuffixes[s];
    }

    /// <summary>Number formatting: compact (1.2K, 3.4M ...) or full with separators (1,200), depending on <see cref="AbbreviateNumbers"/>.</summary>
    public static string FormatNumber(double value)
    {
        if (!AbbreviateNumbers) return value.ToString("#,0.##");
        if (value < 999.995) return value.ToString("0.##"); // small amounts keep their decimals (e.g. 6.25)
        int s = 0;
        while (value >= 999.995 && s < NumberSuffixes.Length - 1) { value /= 1000; s++; }
        return value.ToString("0.##") + NumberSuffixes[s];
    }
}
