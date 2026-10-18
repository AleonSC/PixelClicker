using System.Text;
using UnityEngine;

/// <summary>
/// The explanations shown in the Pixel Guide's orange '!' boxes: what each pixel type does, in terms of the game's real rules. Numbers that come from a
/// pixel's own data (payout, clicks) are read from its tier; the rest describe the mechanics in words. A new pixel type gets its text on its entry in PixelBlockCatalog.cs (guide / oldPixel); the switches below only keep the texts that need this class's own helpers.
/// </summary>
public static class PixelGuideTexts
{
    private static PixelClicker cachedClicker;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cachedClicker = null; }

    /// <summary>The game's numbers for the bait text come from the real settings (PixelClicker's Fishing group), so the text can't drift from what the game does.</summary>
    public static void BaitNumbers(out int pellets, out int percent, out int seconds)
    {
        if (cachedClicker == null) cachedClicker = PixelFind.First<PixelClicker>();
        pellets = cachedClicker != null ? cachedClicker.BaitPellets : 6;
        percent = Mathf.RoundToInt((cachedClicker != null ? cachedClicker.BaitPerPellet : 0.35f) * 100f);
        seconds = Mathf.RoundToInt(cachedClicker != null ? cachedClicker.BaitLifetime : 60f);
    }

    public static string Describe(PixelClicker.PixelType type, PixelClicker.PixelTier t)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(Mechanics(type, t));

        // What every pixel shares, in short.
        if (t.clicksToCollect > 1 && !t.randomClicks && !PixelClicker.IsHazard(type))
            sb.Append("\n\n<b>Tough:</b> it takes ").Append(t.clicksToCollect).Append(" clicks; only the last click pays.");
        if (t.stallsHold) sb.Append("\n\n<b>Stall:</b> every click on it freezes your Block Hold meter for about ").Append((t.stallSeconds > 0f ? t.stallSeconds : 1.5f).ToString("0.#")).Append(" seconds and tops the meter up a little, so a held block keeps its bonus for as long as you keep working on this one.");
        sb.Append(PixelBlockRules.GuideNotes(type));   // special / hold role, from the rules
        sb.Append("\n\n<b>Husk:</b> ").Append(OldPixel(type));
        return sb.ToString();
    }

    /// <summary>What a block's husk does (the block's own text, then the Husk Crush sentence when it can be crushed).</summary>
    public static string OldPixel(PixelClicker.PixelType type) => OldPixelOwn(type) + PixelHuskCrush.GuideNote(type);

    private static string OldPixelOwn(PixelClicker.PixelType type)
    {
        string edited = PixelTextOverrides.Husk(type);   // the user's own text (Assets/Resources/PixelTexts.txt) wins
        if (edited != null) return edited;
        string fromCard = PixelBlockCatalog.OldPixelText(type);   // most texts live on the block's entry in PixelBlockCatalog.cs
        if (fromCard != null) return fromCard;
        switch (type)
        {
            case PixelClicker.PixelType.Bait:
            {
                BaitNumbers(out int pellets, out int percent, out int seconds);
                return "falls beside the cube like any other and does nothing more itself. It is the " + pellets + " pellets it dropped when you collected it that attract fish: each one makes Fish +" + percent + "% more common while it lies there (about " + seconds + " seconds).";
            }
            default: return "falls beside the cube and lies there for a while before it despawns.";
        }
    }

    private static string Mechanics(PixelClicker.PixelType type, PixelClicker.PixelTier t)
    {
        string edited = PixelTextOverrides.Guide(type, t);   // the user's own text (Assets/Resources/PixelTexts.txt) wins
        if (edited != null) return edited;
        System.Func<PixelClicker.PixelTier, string> fromCard = PixelBlockCatalog.GuideText(type);   // most texts live on the block's entry in PixelBlockCatalog.cs
        if (fromCard != null) return fromCard(t);
        switch (type)
        {
            case PixelClicker.PixelType.Bait:
            {
                BaitNumbers(out int pellets, out int percent, out int seconds);
                return "Fishing block: a lump of bait. One click, pays a little, and drops " + pellets + " bait pellets round the cube. While pellets lie there Fish show up much more often (+" + percent + "% each). They rot away after about " + seconds + " seconds, and a Jellyfish going off fries every one of them.";
            }
            default:
                return "A pixel with no special effects.";
        }
    }
}
