// ---- System notes (moved here from CLAUDE.md) ----
// PixelSkillsBonus.cs
//   Part of partial class PixelSkills: the bonus XP bank. Per skill, stat counter skill.<id>.bonus (starts 0, floored at 0).
//   PixelSkills.OldPixelConsumed(tierIndex) is called when an old pixel is CONSUMED (PixelClicker.Vacuum and AbsorbOldPixel = Vacuum
//   pixel / Vacuum Device, the black hole swallowing, the Pet Vacuum; pixel rules: they also give husk XP, see PixelSkills.AddHuskXp) and works on the pack in play
//   (none for Standard): a GOOD pixel (IsGood: Mining = ores, Farming = plants, Electrician = conductors; goodTypeOverrides
//   'packId:Type,Type') adds goodBonusXp (0 = 3) x a rarity factor (1..4, from 1/sqrt(spawn weight)); every other pixel (junk AND
//   neutral) takes badBonusXp (0 = 1) off. In AddXp, DrawBonus DOUBLES each gain while the bank holds anything - the extra comes out
//   of the bank (after the hazard penalty). Shown as a gold 'Bonus N' after the XP on the skill tracker; disableBonusXp. Not on the
//   Skills window yet; other consumers (magnet sorter, quantum organizer, bank, UFO) don't count.
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Part of <see cref="PixelSkills"/>: the <b>bonus XP bank</b>. Every skill has its own bank, starting at 0 and never below 0. Old pixels that are
/// CONSUMED while a skill pack is in play (sucked up by a Vacuum Device / Vacuum pixel, swallowed by a black hole, eaten by a Pet Vacuum) fill or
/// drain the bank of that pack: a GOOD pixel (the pack's own valuable ones - ores for Mining, plants for Farming, conductors for Electrician) adds,
/// anything else (junk like Stone and Dirt, but also every neutral pixel) takes some away. While the bank holds anything, every XP gain of that skill
/// is DOUBLED, the extra half coming out of the bank. The consumed pixels still pay their currency as before - this only adds the XP layer.
/// The bank is the stat counter <c>skill.&lt;id&gt;.bonus</c> (saved generically).
/// </summary>
public partial class PixelSkills
{
    [Header("Bonus XP bank")]
    [Tooltip("Turns the whole bonus XP bank off (consumed old pixels no longer change it and XP is never doubled).")]
    [SerializeField] private bool disableBonusXp = false;

    [Min(0f)]
    [Tooltip("Bonus XP one GOOD pixel adds (rarer pixels add more, up to 4x). 0 = the coded default (3).")]
    [SerializeField] private float goodBonusXp = 0f;

    [Min(0f)]
    [Tooltip("Bonus XP one BAD or neutral pixel takes away. 0 = the coded default (1).")]
    [SerializeField] private float badBonusXp = 0f;

    [Tooltip("Pixel types that count as GOOD for a pack, as 'packId:Type,Type' entries (e.g. 'mining:Copper,Tin'). Packs with no entry use the coded default (Mining = ores, Farming = plants, Electrician = conductors).")]
    [SerializeField] private string[] goodTypeOverrides = new string[0];

    private static string BonusKey(SkillPack pack) => "skill." + pack.id + ".bonus";

    /// <summary>The bonus XP waiting in a skill's bank.</summary>
    public double Bonus(SkillPack pack) => pack == null ? 0d : PixelStats.Total(BonusKey(pack));

    private void SetBonus(SkillPack pack, double value)
    {
        string key = BonusKey(pack);
        PixelStats.Clear(key);
        if (value > 0d) PixelStats.Count(key, value);
    }

    /// <summary>Does this pixel type count as GOOD for the pack (its consumption adds bonus XP)?</summary>
    public bool IsGood(SkillPack pack, PixelClicker.PixelType type)
    {
        if (pack == null) return false;
        if (goodTypeOverrides != null)
        {
            foreach (string entry in goodTypeOverrides)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                int colon = entry.IndexOf(':');
                if (colon <= 0 || entry.Substring(0, colon).Trim() != pack.id) continue;
                foreach (string part in entry.Substring(colon + 1).Split(','))
                    if (part.Trim() == type.ToString()) return true;
                return false;
            }
        }
        switch (pack.id)
        {
            case MiningId: return PixelClicker.IsOre(type) || type == PixelClicker.PixelType.Obsidian;
            case FishingId: return PixelClicker.IsFishBlock(type) && type != PixelClicker.PixelType.Driftwood;
            case FarmingId: return PixelClicker.IsPlant(type);
            case WoodcuttingId: return PixelClicker.IsWoodBlock(type) && type != PixelClicker.PixelType.Twig;
            case ForagingId: return PixelClicker.IsForageBlock(type) && type != PixelClicker.PixelType.Weeds;
            case ElectricianId: return PixelClicker.IsConductorType(type);
            default: return false;
        }
    }

    /// <summary>Tooltip text for the bank: what fills and drains it.</summary>
    public string BonusHelp(SkillPack pack) =>
        "Bonus XP bank: consume good pixels (ores for Mining, plants for Farming, conductors for Electrician) to fill it, any other pixel drains it. " +
        "While it holds anything, every XP you earn is doubled and the extra comes out of the bank.";

    /// <summary>An old pixel of this tier was consumed (vacuumed, swallowed...). Adds to or drains the bonus bank of the pack in play.</summary>
    public static double OldPixelConsumed(int tierIndex)
    {
        PixelSkills s = Instance;
        if (s == null || s.disableBonusXp || s.clicker == null || tierIndex < 0 || tierIndex >= s.clicker.Tiers.Length) return 0d;
        SkillPack pack = s.Find(s.ActiveId);
        if (pack == null || pack.id == StandardId) return 0d;
        double before = s.Bonus(pack);

        PixelClicker.PixelTier tier = s.clicker.Tiers[tierIndex];
        double bank = s.Bonus(pack);
        if (s.IsGood(pack, tier.type))
        {
            float rarity = Mathf.Clamp(Mathf.Sqrt(1f / Mathf.Max(0.05f, tier.spawnWeight)), 1f, 4f);
            bank += (s.goodBonusXp > 0f ? s.goodBonusXp : 3f) * rarity;
        }
        else
        {
            bank -= s.badBonusXp > 0f ? s.badBonusXp : 1f;
        }
        s.SetBonus(pack, System.Math.Max(0d, bank));
        return System.Math.Max(0d, bank) - before;   // how much the bank changed (negative = it drained)
    }

    /// <summary>Adds a flat amount to the bonus bank of the pack in play (the woodchipper's Twig). Returns what was added.</summary>
    public static double AddBonusXp(double amount)
    {
        PixelSkills s = Instance;
        if (s == null || s.disableBonusXp || s.clicker == null || amount <= 0d) return 0d;
        SkillPack pack = s.Find(s.ActiveId);
        if (pack == null || pack.id == StandardId) return 0d;
        s.SetBonus(pack, s.Bonus(pack) + amount);
        return amount;
    }

    /// <summary>Doubling: returns the extra XP to add to 'xp' (taken out of the pack's bank).</summary>
    private double DrawBonus(SkillPack pack, double xp)
    {
        if (disableBonusXp || pack.id == StandardId) return 0d;
        double bank = Bonus(pack);
        if (bank <= 0d) return 0d;
        double extra = System.Math.Min(xp, bank);
        SetBonus(pack, bank - extra);
        return extra;
    }
}
