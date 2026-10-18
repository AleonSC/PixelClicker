// ---- System notes ----
// PixelTextOverrides.cs
//   The player-facing texts of every pixel type in ONE text file the user edits himself: Assets/Resources/PixelTexts.txt (built 2026-10-18 from
//   the code's names, PixelBlockCatalog's guide texts and husk texts). One section per pixel type, keyed by its PixelType name:
//       [Sap]
//       name: Sap Pixels
//       guide: Woodcutting block: a blob of sticky sap...      (the hover card / Pixel Guide explanation)
//       husk: ...                                              (what its husk does; the 'Husk:' line)
//   A key that is missing or left empty keeps the game's own text. '\n' in a text = a new line; rich text (<b>, <color=...>) works.
//   Placeholders filled with the tier's live numbers: {clicks} {clicksMin} {clicksMax} {payMin} {payMax}; Bait: {pellets} {percent} {seconds}.
//   Hooks: PixelGuideTexts.Mechanics (guide), PixelGuideTexts.OldPixelOwn (husk), PixelClicker.Awake + EnsureTier (name, ApplyName).
//   Read through the shared PixelTextFile (Resources 'PixelTexts'); Reload() reads it again (Dev Hub > Events > 'Reload pixel texts', which also renames the live tiers).
//   IN THE EDITOR a pixel type missing from the file is appended to it at start (AppendMissing) with the game's current texts, so the file
//   always lists every type (new ones included); nothing already in the file is touched.
// ---------------------------------------------------
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using PixelType = PixelClicker.PixelType;
using PixelTier = PixelClicker.PixelTier;

public static class PixelTextOverrides
{
    public const string ResourceName = "PixelTexts";

    private static readonly PixelTextFile File = new PixelTextFile(ResourceName, "# PIXEL TEXTS - the names and hover-card texts of every pixel type (see the notes in PixelTextOverrides.cs).");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { File.Reload(); }

    /// <summary>Reads the file again (edits made while playing) - names are re-applied by the caller (PixelClicker.ReapplyTextOverrides).</summary>
    public static void Reload() => File.Reload();

    /// <summary>The file's name for this type, or null (keep the game's).</summary>
    public static string Name(PixelType type) => File.Get(type.ToString(), "name");

    /// <summary>Gives the tier the file's name (if the file has one for it).</summary>
    public static void ApplyName(PixelTier tier)
    {
        if (tier == null) return;
        string n = Name(tier.type);
        if (n != null) tier.displayName = n;
    }

    /// <summary>The file's guide text with the tier's numbers filled in, or null (keep the game's).</summary>
    public static string Guide(PixelType type, PixelTier tier)
    {
        string g = File.Get(type.ToString(), "guide");
        return g != null ? Fill(g, tier) : null;
    }

    /// <summary>The file's husk text, or null (keep the game's).</summary>
    public static string Husk(PixelType type)
    {
        string h = File.Get(type.ToString(), "husk");
        return h != null ? Fill(h, null) : null;
    }

    private static string Fill(string text, PixelTier t)
    {
        if (text.IndexOf('{') < 0) return text;
        if (t != null)
        {
            text = text.Replace("{clicks}", t.clicksToCollect.ToString()).Replace("{clicksMin}", t.randomClicksMin.ToString()).Replace("{clicksMax}", t.randomClicksMax.ToString())
                       .Replace("{payMin}", t.payoutMin.ToString()).Replace("{payMax}", t.payoutMax.ToString());
        }
        if (text.Contains("{pellets}") || text.Contains("{percent}") || text.Contains("{seconds}"))
        {
            PixelGuideTexts.BaitNumbers(out int pellets, out int percent, out int seconds);
            text = text.Replace("{pellets}", pellets.ToString()).Replace("{percent}", percent.ToString()).Replace("{seconds}", seconds.ToString());
        }
        return text;
    }

#if UNITY_EDITOR
    /// <summary>Editor only: lists every pixel type the file does not have yet (with the game's current texts) at the end of the file. Called once at start.</summary>
    public static void AppendMissing(PixelClicker clicker)
    {
        if (clicker == null) return;
        foreach (PixelType type in System.Enum.GetValues(typeof(PixelType)))
        {
            string section = type.ToString();
            if (File.HasSection(section)) continue;
            int index = clicker.IndexOf(type);
            File.Text(section, "name", index >= 0 ? clicker.Tiers[index].displayName : section);
            System.Func<PixelTier, string> guide = PixelBlockCatalog.GuideText(type);
            File.Text(section, "guide", guide != null ? Placeholders(guide) : "");
            File.Text(section, "husk", PixelBlockCatalog.OldPixelText(type) ?? "");
        }
        File.Flush();
    }

    /// <summary>A guide text with marker numbers in place of the tier's numbers, turned back into {placeholders}.</summary>
    private static string Placeholders(System.Func<PixelTier, string> guide)
    {
        PixelTier marker = new PixelTier { clicksToCollect = 90901, randomClicksMin = 90902, randomClicksMax = 90903, payoutMin = 90904, payoutMax = 90905 };
        return guide(marker).Replace("90901", "{clicks}").Replace("90902", "{clicksMin}").Replace("90903", "{clicksMax}").Replace("90904", "{payMin}").Replace("90905", "{payMax}");
    }
#endif
}
