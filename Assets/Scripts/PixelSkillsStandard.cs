using UnityEngine;

/// <summary>
/// Part of <see cref="PixelSkills"/>: <b>Standard is a real skill</b>. It levels from the very first click (every collected pixel gives it XP while
/// it is the pack in play; while another skill is trained it still gets a tiny <c>standardSideXp</c> share, 5%) and its perks are the base
/// bonuses that apply in EVERY rotation: old pixels last longer (<c>lifetimePerLevel</c> seconds a level), the floor holds more old pixels from
/// level 4 on (<c>capacityPerLevel</c> a level). It does not count towards Mastery and has no bonus bank, filler or hazard.
/// </summary>
public partial class PixelSkills
{
    [Header("Standard skill")]
    [Range(0f, 0.5f)]
    [Tooltip("While another skill is trained, Standard still earns this share of the XP (0.05 = 5%). 0 = the coded default (5%).")]
    [SerializeField] private float standardSideXp = 0f;

    [Min(0f)]
    [Tooltip("Perk 'Lingering pixels': seconds every old pixel lasts longer for each Standard level above 1. 0 = the coded default (2).")]
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
            return (s.StandardLevel - 1) * (s.lifetimePerLevel > 0f ? s.lifetimePerLevel : 2f);
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

    private float StandardSideRate => standardSideXp > 0f ? standardSideXp : 0.05f;

    private static Perk[] StandardPerks() => new[]
    {
        new Perk { level = 2, text = "Lingering pixels: old pixels last 2 seconds longer for every level", implemented = true },
        new Perk { level = 4, text = "Roomy floor: room for 5 more old pixels for every level", implemented = true },
        new Perk { level = 7, text = "Sharper clicks: a small bonus to every pixel" },
    };
}
