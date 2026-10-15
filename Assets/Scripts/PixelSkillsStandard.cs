// ---- System notes (moved here from CLAUDE.md) ----
// PixelSkillsStandard.cs
//   Part of partial class PixelSkills: Standard is a real skill. Every collected block gives it a FLAT standardXpPerBlock (1) XP - manual
//   or auto, any block, whatever skill / Offline is trained, no multipliers (AddXpFlat); popups only when Standard is the one trained and the Skills tab
//   exists; level-up log lines only once AnyTrainable). Its perks apply in EVERY rotation: Lingering pixels (StandardLifetimeBonus =
//   (level-1) x lifetimePerLevel 5 s (old pixels last oldPixelLifetime 15 s by default, so Standard level 2 = 20 s; the field was
//   renamed from fallingCopyLifetime so the old saved 6 s is dropped), added in PixelClicker.FallingCopyLifetime, which all old-pixel
//   despawn timers read) and Roomy floor (StandardCapacityBonus = (level-3) x capacityPerLevel 5 from level 4, added to
//   PixelClicker.OldPixelCap), Steady grip (level 3) and Firm grip (level 5): Block Hold's meter starts at half length and each adds 10% of that
//   (PixelBlockHold.GripFactor reads the Standard level); 'Sharper clicks' (level 7) is still 'coming soon'. Standard shows level / XP bars in the Skills window
//   and the tracker like any skill, has no bonus bank, filler, hazard or tree and does not count towards Mastery. Minigame bonus XP
//   still skips Standard.
//   Standard's XP popups and level-up log lines show from the very FIRST click (no longer only once a skill pack exists). Its level 2 perk carries the
//   intro popup (StandardLevel2Notice, the perk's noticeText: 'you have been training a skill all along...'), shown once through the perk notices.
//   The level 2 popup opens the Skills window on Standard, sits small to its right (PixelNotice beside the window) and frames the Standard card with
//   the shared pulsing gold frame (PixelHighlight) until Got it (Perk.noticeInWindow, any perk can use it; perkWindowVersion 1 switched it on).
//   The Skills window has a '?' button next to the X that plays Cubie's skills intro again (StartTutorial).
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Part of <see cref="PixelSkills"/>: <b>Standard is a real skill</b>. It levels from the very first click (every collected block gives it a flat
/// <c>standardXpPerBlock</c>, 1 XP, manual or auto, whatever skill is trained) and its perks are the base
/// bonuses that apply in EVERY rotation: old pixels last longer (<c>lifetimePerLevel</c> seconds a level), the floor holds more old pixels from
/// level 4 on (<c>capacityPerLevel</c> a level). It does not count towards Mastery and has no bonus bank, filler or hazard.
/// </summary>
public partial class PixelSkills
{
    [Header("Standard skill")]
    [Min(0f)]
    [Tooltip("Standard XP for every collected block - manual or auto, any block, whatever skill is trained, no multipliers. 0 = the coded default (1).")]
    [SerializeField] private float standardXpPerBlock = 0f;

    [Tooltip("Data version of the perk popups' 'show beside the Skills window' setting (1 = the Standard level 2 popup was switched to it). Raised by code; lower it to apply the default again.")]
    [SerializeField] private int perkWindowVersion = 0;

    [Min(0f)]
    [Tooltip("Perk 'Lingering pixels': seconds every old pixel lasts longer for each Standard level above 1. 0 = the coded default (5: level 2 = 20 s with the 15 s base).")]
    [SerializeField] private float lifetimePerLevel = 0f;

    [Min(0)]
    [Tooltip("Perk 'Roomy floor': extra old pixels allowed on the floor for each Standard level above 3. 0 = the coded default (5).")]
    [SerializeField] private int capacityPerLevel = 0;

    private double stdXpCache = -1d;
    private int stdLevelCache = 1;

    /// <summary>Standard's level (cached, because old pixels ask for the perks every time one spawns).</summary>
    private int StandardLevel
    {
        get
        {
            SkillPack std = Find(StandardId);
            if (std == null) return 1;
            double xp = Xp(std);
            if (xp != stdXpCache) { stdXpCache = xp; stdLevelCache = LevelOf(std); }
            return stdLevelCache;
        }
    }

    /// <summary>Extra seconds every old pixel lasts (Standard perk 'Lingering pixels').</summary>
    public static float StandardLifetimeBonus
    {
        get
        {
            PixelSkills s = Instance;
            if (s == null || s.disableSkills) return 0f;
            return (s.StandardLevel - 1) * (s.lifetimePerLevel > 0f ? s.lifetimePerLevel : 5f);
        }
    }

    /// <summary>Extra old pixels the floor can hold (Standard perk 'Roomy floor', from level 4).</summary>
    public static int StandardCapacityBonus
    {
        get
        {
            PixelSkills s = Instance;
            if (s == null || s.disableSkills) return 0;
            return Mathf.Max(0, s.StandardLevel - 3) * (s.capacityPerLevel > 0 ? s.capacityPerLevel : 5);
        }
    }

    private double StandardXpPerBlock => standardXpPerBlock > 0f ? standardXpPerBlock : 1d;

    /// <summary>The popup at Standard level 2: it introduces skills (the perk's noticeText, editable on the Standard pack's level 2 perk).</summary>
    private const string StandardLevel2Notice = "Standard level 2! Surprise: you have been training a skill all along. Every block you collect gives 1 Standard XP, and every level unlocks a perk.\n\nYour first perk, Lingering pixels: husks on the floor last 5 seconds longer for every Standard level, so you have more time to use them.\n\nLater, skill packs from the shop add new skills with blocks of their own.";

    private static Perk[] StandardPerks() => new[]
    {
        new Perk { level = 2, text = "Lingering pixels: old pixels last 5 seconds longer for every level", implemented = true, noticeText = StandardLevel2Notice },
        new Perk { level = 3, text = "Steady grip: Block Hold lasts 10% longer", implemented = true },
        new Perk { level = 4, text = "Roomy floor: room for 5 more old pixels for every level", implemented = true },
        new Perk { level = 5, text = "Firm grip: Block Hold lasts another 10% longer", implemented = true },
        new Perk { level = 7, text = "Sharper clicks: a small bonus to every pixel" },
    };
}
