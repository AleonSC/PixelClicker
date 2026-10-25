// ---- System notes (moved here from CLAUDE.md) ----
// PixelSkillsMastery.cs
//   Part of partial class PixelSkills: Mastery, one bar for the progress of ALL skills together, along the bottom of the Skills window
//   (BuildMasteryBar, RefreshMastery, gold fill, 'Mastery 12.5% - x / y XP', tooltip). Every skill has a level cap
//   (SkillPack.levelCap, 0 = defaultLevelCap on the component, 0 = 30; the caps only measure mastery for now - a skill keeps earning
//   XP past its cap) worth CapXp(pack) = the XP from level 1 to the cap (baseXp x xpGrowth^n summed); MasteryProgress = sum over EVERY
//   pack except Standard (Mining, Farming, Electrician, Offline Progression and any pack added later; skills not unlocked yet count as
//   0) of min(skill XP, its cap XP) / sum of all cap XP, so it only reaches 100% when every skill is at its cap. An event-log line at
//   every 10% (counter mastery.step, saved) and 'Mastery complete!' at 100%. Not shown on the pinned tracker yet.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Part of <see cref="PixelSkills"/>: <b>Mastery</b>, one overarching bar for the progress of ALL your skills together. Every skill has a level cap
/// (<c>SkillPack.levelCap</c>, 0 = <c>defaultLevelCap</c>) worth a fixed amount of XP (the XP to climb from level 1 to the cap); a skill's share of
/// mastery is its XP so far (never more than its cap) out of that. Mastery is the sum over EVERY skill (Standard aside, skills you have not unlocked
/// yet count as zero) divided by the sum of all the caps, so the bar only reaches 100% once every skill has reached its cap. The caps are a
/// placeholder for now: they only measure mastery, a skill keeps earning XP past its cap.
/// </summary>
public partial class PixelSkills
{
    [Header("Mastery")]
    [Min(0)]
    [Tooltip("The level a skill counts as 'maxed' for Mastery when it has no cap of its own. 0 = 30. (Nothing stops a skill earning XP past it yet.)")]
    [SerializeField] private int defaultLevelCap = 0;

    private const string MasteryStepKey = "mastery.step";
    private TMP_Text masteryLabel;
    private RectTransform masteryFill;

    private int CapOf(SkillPack pack) => pack != null && pack.levelCap > 1 ? pack.levelCap : (defaultLevelCap > 1 ? defaultLevelCap : 30);

    /// <summary>The XP a skill needs from level 1 to its level cap.</summary>
    public double CapXp(SkillPack pack)
    {
        if (pack == null) return 0d;
        double total = 0d, need = BaseXpOf(pack);
        int cap = CapOf(pack);
        for (int level = 1; level < cap; level++)
        {
            total += need;
            need *= Math.Max(1.05f, pack.xpGrowth);
        }
        return total;
    }

    /// <summary>Dev tools 'Max XP' widget: fills one skill to its level cap (max) or takes all its XP away (back to level 1). No popups or perk notices.</summary>
    public void DevSetSkillXp(SkillPack p, bool max)
    {
        if (p == null) return;
        PixelStats.Clear(p.XpKey);
        if (max) { double cap = CapXp(p); if (cap > 0d) PixelStats.Count(p.XpKey, cap); }
        Changed?.Invoke();
    }

    /// <summary>Dev tools: gives every skill (except Standard) the XP up to its level cap. No popups or perk notices.</summary>
    [ContextMenu("Dev: Max All Skills")]
    public void DevMaxAllSkills()
    {
        foreach (SkillPack p in packs)
        {
            if (p == null || p.id == StandardId) continue;
            double missing = CapXp(p) - Xp(p);
            if (missing > 0d) PixelStats.Count(p.XpKey, missing);
        }
        Changed?.Invoke();
    }

    /// <summary>Mastery progress 0..1 (1 only when every skill is at its cap), plus the XP counted and the XP needed in all.</summary>
    public double MasteryProgress(out double xp, out double totalXp)
    {
        xp = 0d;
        totalXp = 0d;
        foreach (SkillPack p in packs)
        {
            if (p == null || p.id == StandardId) continue;   // Standard is no skill
            double cap = CapXp(p);
            totalXp += cap;
            xp += Math.Min(Xp(p), cap);
        }
        return totalXp > 0d ? Math.Min(1d, xp / totalXp) : 0d;
    }

    /// <summary>Mastery as a percentage (0-100).</summary>
    public double MasteryPercent => MasteryProgress(out _, out _) * 100d;

    /// <summary>Builds the bar along the bottom of the Skills window.</summary>
    private void BuildMasteryBar()
    {
        Image trackImage = PixelUIKit.CreateBottomBar(windowObject.transform, "Mastery Bar", new Color(1f, 0.78f, 0.25f, 1f), 26f, 22f, 42f, out masteryFill);
        GameObject track = trackImage.gameObject;
        masteryLabel = PixelUIKit.CreateText(Font, track.transform, "Text", "", 26f, TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        masteryLabel.raycastTarget = false;
        PixelUIKit.Caps(masteryLabel);
        PixelUIKit.Stretch(masteryLabel.rectTransform);

        PixelHoverTip tip = track.AddComponent<PixelHoverTip>();
        System.Func<string> source = () => "Mastery: your progress across every skill together. It only reaches 100% once all your skills are at their level cap.";
        tip.onEnter = () => hoverTip = source;
        tip.onExit = () => { if (hoverTip == source) hoverTip = null; };
        trackImage.raycastTarget = true;   // so the tooltip works
    }

    private void RefreshMastery()
    {
        if (masteryLabel == null) return;
        double progress = MasteryProgress(out double xp, out double total);
        PixelUIKit.SetText(masteryLabel, "Mastery  " + (progress * 100d).ToString("0.#") + "%   -   " + PixelClicker.FormatNumberShort(Math.Floor(xp)) + " / " + PixelClicker.FormatNumberShort(Math.Ceiling(total)) + " XP");
        masteryFill.anchorMax = new Vector2(Mathf.Clamp01((float)progress), 1f);
        masteryFill.offsetMax = new Vector2(-3f, -3f);

        // A line in the event log at every 10% (once each, remembered in the saved counters).
        int step = (int)Math.Floor(progress * 10d + 1e-9);
        if (step > PixelStats.Total(MasteryStepKey))
        {
            PixelStats.Best(MasteryStepKey, step);
            if (step >= 10) PixelHints.Announce("Mastery complete!", "Every skill is at its level cap: your Mastery is at 100%.");
            else if (step > 0) PixelHints.Announce("Mastery " + step * 10 + "%", "Your Mastery (progress across all skills) reached " + step * 10 + "%.");
        }
    }
}
