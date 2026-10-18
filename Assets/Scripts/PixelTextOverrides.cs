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
//   Read once (Resources 'PixelTexts'); Reload() reads it again (Dev Hub > Events > 'Reload pixel texts', which also renames the live tiers).
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

    private sealed class Entry { public string name, guide, husk; }

    private static Dictionary<string, Entry> entries;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { entries = null; }

    private static Dictionary<string, Entry> Entries
    {
        get
        {
            if (entries == null) Load();
            return entries;
        }
    }

    /// <summary>Reads the file again (edits made while playing) - names are re-applied by the caller (PixelClicker.ReapplyTextOverrides).</summary>
    public static void Reload() => Load();

    private static void Load()
    {
        entries = new Dictionary<string, Entry>();
        TextAsset asset = Resources.Load<TextAsset>(ResourceName);
        string text = asset != null ? asset.text : null;
#if UNITY_EDITOR
        // In the editor read the file itself, so edits saved while the game runs count after a Reload (Resources keeps the imported copy).
        string path = FilePath;
        if (System.IO.File.Exists(path)) text = System.IO.File.ReadAllText(path);
#endif
        if (string.IsNullOrEmpty(text)) return;
        Entry current = null;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                string key = line.Substring(1, line.Length - 2).Trim();
                current = new Entry();
                entries[key] = current;
                continue;
            }
            if (current == null) continue;
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            string field = line.Substring(0, colon).Trim().ToLowerInvariant();
            string value = line.Substring(colon + 1).Trim().Replace("\\n", "\n");
            if (value.Length == 0) continue;
            if (field == "name") current.name = value;
            else if (field == "guide") current.guide = value;
            else if (field == "husk") current.husk = value;
        }
    }

    private static Entry Get(PixelType type) => Entries.TryGetValue(type.ToString(), out Entry e) ? e : null;

    /// <summary>The file's name for this type, or null (keep the game's).</summary>
    public static string Name(PixelType type) => Get(type)?.name;

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
        string g = Get(type)?.guide;
        return g != null ? Fill(g, tier) : null;
    }

    /// <summary>The file's husk text, or null (keep the game's).</summary>
    public static string Husk(PixelType type)
    {
        string h = Get(type)?.husk;
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
    private static string FilePath => System.IO.Path.Combine(Application.dataPath, "Resources", ResourceName + ".txt");

    /// <summary>Editor only: appends a section for every pixel type the file does not list yet (with the game's current texts). Called once at start.</summary>
    public static void AppendMissing(PixelClicker clicker)
    {
        if (clicker == null) return;
        Dictionary<string, Entry> have = Entries;
        StringBuilder sb = new StringBuilder();
        foreach (PixelType type in System.Enum.GetValues(typeof(PixelType)))
        {
            if (have.ContainsKey(type.ToString())) continue;
            int index = clicker.IndexOf(type);
            PixelTier tier = index >= 0 ? clicker.Tiers[index] : null;
            sb.Append("\n[").Append(type).Append("]\n");
            sb.Append("name: ").Append(tier != null ? tier.displayName : type.ToString()).Append('\n');
            System.Func<PixelTier, string> guide = PixelBlockCatalog.GuideText(type);
            sb.Append("guide: ").Append(guide != null ? Placeholders(guide) : "").Append('\n');
            sb.Append("husk: ").Append((PixelBlockCatalog.OldPixelText(type) ?? "").Replace("\n", "\\n")).Append('\n');
            have[type.ToString()] = new Entry();
        }
        if (sb.Length == 0) return;
        try { System.IO.File.AppendAllText(FilePath, sb.ToString()); }
        catch (System.Exception e) { Debug.LogWarning("PixelTexts.txt: could not add the new pixel types (" + e.Message + ")"); }
    }

    /// <summary>A guide text with marker numbers in place of the tier's numbers, turned back into {placeholders}.</summary>
    private static string Placeholders(System.Func<PixelTier, string> guide)
    {
        PixelTier marker = new PixelTier { clicksToCollect = 90901, randomClicksMin = 90902, randomClicksMax = 90903, payoutMin = 90904, payoutMax = 90905 };
        string s = guide(marker);
        return s.Replace("90901", "{clicks}").Replace("90902", "{clicksMin}").Replace("90903", "{clicksMax}").Replace("90904", "{payMin}").Replace("90905", "{payMax}").Replace("\n", "\\n");
    }
#endif
}
