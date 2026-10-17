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
//   and the tracker like any skill, has no bonus bank, filler or hazard and does not count towards Mastery.
//   XP CURVE RETUNE (2026-10-17, from the 10:50 session report: level 4 at 8:21, plan ~20 min; Recycler streak XP 2,620 out-earned click XP 2,357):
//   Standard's baseXp 380 -> 600 (x xpCurveScale 2 = 1,200 XP to level 2; growth stays 1.5) AND the Recycler gives a pure husk only
//   standardXpShare (0.5) of its XP (PixelRecycler; skill husks keep it all). Modelled at the report's pace (clicks ~218 XP/min + half the Recycler's
//   ~242 + some crushing ~ 360/min): levels 2-7 at about 3 / 8 / 16 / 27 / 44 / 69 min - Auto Clicker upgrades raise the pace later, pulling 6-7
//   towards the 45 / 60 min targets. Check the next report's Standard timeline and nudge baseXp / standardXpShare from there.
//   SKILL TREE (2026-10-16, DefaultNodes(StandardId), PixelSkills.HasStandardNode): the first point (level 2) goes on 'Block Hold' (hold; Block Hold
//   is no longer available from the start - PixelBlockHold.requireTreeNode) or 'Power Click' (power, PixelPowerClick.requireTreeNode); branches
//   hold -> lucky (+10% stake win) -> highroller (cost 2, stakes grow 50% faster) and power -> quick (charge 40% faster) -> aftershock (cost 2,
//   cooldown a third shorter). 'What's Next' (nextup, row 1) needs BOTH hold and power ('requires' takes several comma-separated ids now: ParentsOf,
//   all must be owned, the window draws a line from each) and shows the Next up window; 'Look Further' (nextup2, cost 2) and 'Foresight' (nextup3, cost 2)
//   each show one more block ahead in it. 'Shuffle Combo' (shuffle, after highroller) unlocks PixelShuffleBonus (the filler tap combo). The bottom
//   row (row 3) is the HUSK branch (2026-10-17, PixelHuskCrush - crushing itself is free): 'Chain Crush' (chaincrush, minLevel 4, column 1 = beside
//   the first twists) -> 'Clean Break' (cleanbreak, minLevel 6, cost 2) -> 'Hard Hands' (hardhands, minLevel 9, cost 2); TreeNode.minLevel holds a node back.
//   'Aimed Clicks' (aimed, row 1 column 0, minLevel 3, 2026-10-17): edge-click aiming (PixelBurstPop.AimingOwned) - the alternative to the second move;
//   level 3's perk popup (StandardLevel3Notice) points at that choice.
//   Row 4: 'Charge Crush' (chargecrush, needs chaincrush, minLevel 7, cost 2): hold right on a husk to charge a ring, let go to arc-crush its type inside it. The level 2 popup now points the player to the skill point and the tree. Minigame bonus XP
//   still skips Standard.
//   Standard's XP popups and level-up log lines show from the very FIRST click (no longer only once a skill pack exists). Its level 2 perk carries the
//   intro popup (StandardLevel2Notice, the perk's noticeText: 'you have been training a skill all along...'), shown once through the perk notices.
//   The level 2 popup opens the Skills window on Standard, sits small to its right (PixelNotice beside the window, time frozen until Got it) and frames the Standard card with
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
    private const string StandardLevel2Notice = "Standard level 2! Surprise: you have been training a skill all along. Every block you collect gives 1 Standard XP, and every level gives a perk AND a skill point.\n\nSpend your point in Standard's skill tree (the tree button on its card): Block Hold or Power Click - two new ways to click. At level 3 your next point can take the other one - or Aimed Clicks, to throw husks where you want them. Later points branch further.\n\nYour first perk, Lingering pixels: husks on the floor last 5 seconds longer for every Standard level.";

    /// <summary>The popup at Standard level 3: the second point is a choice between the move you skipped and Aimed Clicks.</summary>
    private const string StandardLevel3Notice = "Standard level 3 - another skill point! Take the move you skipped (Block Hold or Power Click), or Aimed Clicks: click near the cube's edge and its husk flies out of that side, so you can send husks to a Recycler or a clear spot. Until then every husk scatters at random.\n\nYour perk, Steady grip: Block Hold lasts 10% longer.";

    private static Perk[] StandardPerks() => new[]
    {
        new Perk { level = 2, text = "Lingering pixels: old pixels last 5 seconds longer for every level", implemented = true, noticeText = StandardLevel2Notice },
        new Perk { level = 3, text = "Steady grip: Block Hold lasts 10% longer", implemented = true, noticeText = StandardLevel3Notice, noticeInWindow = true },
        new Perk { level = 4, text = "Roomy floor: room for 5 more old pixels for every level", implemented = true },
        new Perk { level = 5, text = "Firm grip: Block Hold lasts another 10% longer", implemented = true },
        new Perk { level = 7, text = "Sharper clicks: a small bonus to every pixel" },
    };
}
