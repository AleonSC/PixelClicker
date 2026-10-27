// ---- System notes (moved here from CLAUDE.md) ----
// PixelSkillsMinigames.cs
//   Part of partial class PixelSkills: the minigame XP multiplier. AddXp multiplies every gain (except Standard's) by MinigameXpMultiplier = 1 +
//   EnabledMinigames x xpPerMinigame (0 = 0.1, i.e. +10% each), capped by maxMinigameXpBonus (0 = no cap), disableMinigameXpBonus.
//   EnabledMinigames counts PixelMinigames that are Running and not UserDisabled (Toggles > Minigames); visitor vendors don't count.
//   Recomputed once a second. Shown in the Skills window header (right side, gold 'Minigames: x1.3 XP (3 on)').
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Part of <see cref="PixelSkills"/>: the <b>minigame XP multiplier</b>. Every bought minigame that is switched on (Toggles &gt; Minigames) adds a
/// percentage to ALL skill XP gains, so running more minigames trains skills faster. Visitor vendors do not count.
/// </summary>
public partial class PixelSkills
{
    [Header("Minigame XP multiplier")]
    [Tooltip("Turns the minigame XP multiplier off (XP is never boosted by running minigames).")]
    [SerializeField] private bool disableMinigameXpBonus = false;

    [Min(0f)]
    [Tooltip("Extra XP (as a fraction) each enabled minigame adds to every skill gain: 0.1 = +10% per minigame. 0 = the coded default (0.1).")]
    [SerializeField] private float xpPerMinigame = 0f;

    [Min(0f)]
    [Tooltip("The most the minigames can add together, as a fraction (1.5 = +150%). 0 = no cap.")]
    [SerializeField] private float maxMinigameXpBonus = 0f;

    private float minigameBonusCache;
    private float minigameBonusCheckedAt = -10f;

    /// <summary>How many bought minigames are switched on (visitor vendors excluded).</summary>
    public static int EnabledMinigames
    {
        get
        {
            int n = 0;
            foreach (PixelMinigame m in PixelMinigame.All)
                if (m != null && !(m is PixelVisitorMinigame) && !m.UserDisabled && m.Running) n++;
            return n;
        }
    }

    /// <summary>The XP multiplier from running minigames: 1 + (enabled minigames x <c>xpPerMinigame</c>), capped.</summary>
    public float MinigameXpMultiplier
    {
        get
        {
            if (disableMinigameXpBonus) return 1f;
            if (Time.unscaledTime - minigameBonusCheckedAt > 1f)
            {
                minigameBonusCheckedAt = Time.unscaledTime;
                float bonus = EnabledMinigames * (xpPerMinigame > 0f ? xpPerMinigame : 0.1f);
                if (maxMinigameXpBonus > 0f) bonus = Mathf.Min(bonus, maxMinigameXpBonus);
                minigameBonusCache = bonus;
            }
            return 1f + minigameBonusCache;
        }
    }
}
