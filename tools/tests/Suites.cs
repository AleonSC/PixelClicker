using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

// All suites. NOTE: classes with Unity static constructors (PixelClicker uses a native ProfilerMarker) cannot be loaded here - test only plain static classes (e.g. PixelBlockCatalog). Logic suites call game code directly (statics only - no Unity scene); data suites check the Resources / docs text files.
public static class Suites
{
    public static void Register()
    {
        Catalog(); TextFiles(); Docs();
    }

    // ---- Block catalog (PixelBlockCatalog.cs): every pixel type needs a card ----
    static void Catalog()
    {
        var types = Enum.GetValues(typeof(PixelClicker.PixelType)).Cast<PixelClicker.PixelType>().ToArray();
        T.Add("Catalog", "every PixelType has a card", () =>
        {
            var missing = types.Where(t => PixelBlockCatalog.Card(t) == null).Select(t => t.ToString()).ToArray();
            T.True(missing.Length == 0, "no card for: " + string.Join(", ", missing));
        });
        T.Add("Catalog", "no two cards share a type", () =>
        {
            var cards = (Array)typeof(PixelBlockCatalog).GetField("Cards", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var dup = cards.Cast<PixelBlockCard>().GroupBy(c => c.type).Where(g => g.Count() > 1).Select(g => g.Key.ToString()).ToArray();
            T.True(dup.Length == 0, "duplicate cards: " + string.Join(", ", dup));
        });
        T.Add("Catalog", "enum values are unique numbers", () =>
        {
            var dup = types.GroupBy(t => (int)t).Where(g => g.Count() > 1).Select(g => g.Key.ToString()).ToArray();
            T.True(dup.Length == 0, "duplicate enum numbers: " + string.Join(", ", dup));
        });
    }

    // ---- Resources text files ----
    static void TextFiles()
    {
        T.Add("TextFiles", "Changelog lines are dated", () =>
        {
            var re = new Regex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}( UTC)?\] \S");
            var bad = File.ReadAllLines(T.Path("Assets/Resources/Changelog.txt")).Select((l, i) => (l, i + 1))
                .Where(x => x.l.Trim().Length > 0 && !re.IsMatch(x.l)).Take(5).Select(x => "line " + x.Item2).ToArray();
            T.True(bad.Length == 0, "badly formed: " + string.Join(", ", bad));
        });
        T.Add("TextFiles", "Todo lines are items, headings or comments", () =>
        {
            var bad = File.ReadAllLines(T.Path("Assets/Resources/Todo.txt")).Select((l, i) => (l, i + 1))
                .Where(x => { string s = x.l.Trim(); return s.Length > 0 && !(s.StartsWith("[ ] ") || s.StartsWith("[x] ") || s.StartsWith("#") || s.StartsWith("//")); })
                .Take(5).Select(x => "line " + x.Item2).ToArray();
            T.True(bad.Length == 0, "unknown line type: " + string.Join(", ", bad));
        });
        T.Add("TextFiles", "Dialogue sections are unique", () =>
        {
            var heads = File.ReadAllLines(T.Path("Assets/Resources/Dialogue.txt")).Where(l => l.StartsWith("[") && l.EndsWith("]")).ToArray();
            var dup = heads.GroupBy(h => h).Where(g => g.Count() > 1).Select(g => g.Key).Take(5).ToArray();
            T.True(dup.Length == 0, "duplicate sections: " + string.Join(", ", dup));
        });
        T.Add("TextFiles", "every Dialogue section has an ID in the map", () =>
        {
            string ids = File.ReadAllText(T.Path("tools/dialogue-map/ids.txt"));
            var heads = File.ReadAllLines(T.Path("Assets/Resources/Dialogue.txt")).Where(l => l.StartsWith("[") && l.EndsWith("]")).Select(l => l.Trim('[', ']')).ToArray();
            var missing = heads.Where(h => !ids.Contains(h)).Take(5).ToArray();
            T.True(missing.Length == 0, "no ID (run tools/dialogue-map/build.py): " + string.Join(" | ", missing));
        });
    }

    // ---- Docs rules from CLAUDE.md ----
    static void Docs()
    {
        T.Add("Docs", "every script has a line in docs/Index.md", () =>
        {
            string index = File.ReadAllText(T.Path("docs/Index.md"));
            var missing = Directory.GetFiles(T.Path("Assets/Scripts"), "*.cs", SearchOption.AllDirectories)
                .Select(Path.GetFileName).Where(f => !index.Contains(f)).Take(10).ToArray();
            T.True(missing.Length == 0, missing.Length + "+ not in Index: " + string.Join(", ", missing));
        });
        T.Add("Docs", "scripts stay under 1,500 lines", () =>
        {
            var big = Directory.GetFiles(T.Path("Assets/Scripts"), "*.cs", SearchOption.AllDirectories)
                .Where(f => File.ReadLines(f).Count() > 1500).Select(Path.GetFileName).ToArray();
            T.True(big.Length == 0, "too long: " + string.Join(", ", big));
        });
        T.Add("Docs", "no PlayerPrefs outside PixelPrefs", () =>
        {
            var bad = Directory.GetFiles(T.Path("Assets/Scripts"), "*.cs", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f) != "PixelPrefs.cs" && File.ReadAllLines(f).Any(l => !l.TrimStart().StartsWith("//") && l.Contains("PlayerPrefs."))).Select(Path.GetFileName).Take(10).ToArray();
            T.True(bad.Length == 0, "uses PlayerPrefs: " + string.Join(", ", bad));
        });
    }
}
