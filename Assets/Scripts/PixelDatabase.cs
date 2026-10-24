// ---- System notes (moved here from CLAUDE.md) ----
// PixelDatabase.cs / PixelDatabaseWindow.cs
//   Game Database (opened by the Database tab of the pause menu's Game Guide screen, PixelPauseMenu.BuildGuideScreen ->
//   PixelDatabaseWindow.Toggle(); the window's canvas (520) sits above the pause menu and Escape closes it first, CloseIfOpen): a
//   search engine over the live game data. PixelDatabase.Build() (run each time the window opens) makes one Entry per pixel (name +
//   the Pixel Guide text + pay / clicks / weight), shop pack (PixelShop.AllPacks), skill (perks + tree nodes), consumable
//   (PixelConsumables.ItemName/ItemDescription/ItemCosts), defined recipe (PixelCrafting.DefinedRecipes), minigame / vendor
//   (PixelMinigame.All), achievement and tip (PixelHints.AllHints + the lines of Resources/HowToPlay.txt and Controls.txt), and TWO-
//   WAY References between them (pack rewards / costs / requirements / unlocks, skill blocks / filler / hazard, recipe ingredients,
//   achievement pixel, and 'mentioned in tip' by whole-word name match). PixelDatabase.Search(query, max): every word must appear in
//   the entry's name / category / description / reference text; ranked name-exact > starts-with > contains > category > text. The
//   window (canvas 108, PixelWindows 16, PixelPop): search box + results list on the left, the selected entry's description and its
//   references on the right (click a reference to jump to it, '< Back' returns). Not covered yet: floors / skies / pets / vendors'
//   wares.
//   SOURCES (2026-10-24): Build no longer holds one long hand-written list. Every system component that implements IPixelDatabaseSource (found in the
//   scene each time, one per component type) hands in STEPS with an order (PixelDatabase.PixelsOrder ... GoalsOrder); the steps run in that order on one
//   Builder (Add / Link + the lookups earlier steps filled: Pixels by type, Minigames by id, Packs by index, Skills by id, Items by name, VendorWares,
//   Texts). Owners: PixelClicker (pixels, PixelClickerDatabase.cs), PixelShop (packs, pack->skill links, Operation 'Upgrade' entries, PixelShopDatabase.cs),
//   PixelSkills (skills + tree nodes, PixelSkillsDatabase.cs), PixelMinigame.AddToDatabase (each minigame; PixelVisitorMinigame adds its wares),
//   PixelBlockRules, PixelPets (PixelPetsDatabase.cs), PixelCrafting (recipes), PixelAchievements, PixelGoals, and the 'Special' entries: PixelWhaleFight
//   (whale fight), PixelTreasureMap (treasure map), PixelDragonWish (the wish + every wish, from its AllWishes). Kept here as built-in steps: the minigame
//   loop, consumables, vendors' wares links, floor / sky looks, settings, and tips + guide lines + the 'mentions' pass (TipsOrder: everything added
//   earlier can be mentioned, except the NotMentioned categories). A NEW kind of game data: implement IPixelDatabaseSource on its system (pick an order
//   after whatever it links to; before TipsOrder if tips should mention it) and, for a new category, add its colour in CategoryColor and a tab in
//   PixelDatabaseWindow if it needs one. A step that throws is logged with its source and the others still run.
//   BROWSE / PICTURES / DISCOVERY: the window has category tabs under the search box (All, Pixels, Packs, Skills, Items, Recipes, Minigames, Awards, Tips; PixelDatabase.Search(query, max,
//   category, showAll)), a turning 3D picture of the pixel (PixelTierPicture, Entry.tierIndex) beside a pixel's description, and a 'Show ???' switch. Entry.discovered: pixel = unlocked or
//   collected, shop pack = bought or a reward pixel unlocked, skill = PixelSkills.IsAvailable, consumable = its required pixel unlocked; recipes, minigames, achievements and tips are always
//   shown. An undiscovered entry is listed as '???' (name, text, picture and references hidden, searchable only by its category) unless 'Show ???' is on; other entries' references to it
//   read 'Label: ???'. MORE ENTRIES: Pet (one per pixel type that can have a pet; discovered = owned; numbers from PixelPets.SpawnWeightFactor / DespawnSlowDown / FindChancePerClick),
//   Floor and Sky (the looks of PixelFloor / PixelSkybox, IPixelLookSource) and vendors' wares (PixelVisitorMinigame.WareNames -> 'Sells' / 'Sold by' links on minigame + consumable entries).
//   RULES / NODES: 'Block rule' entries (PixelBlockRules special-block and hold-block rules; text = PixelBlockRules.GuideNotes, links to the pixel, the blocks it replaces, the boost block, the hazard it clears and the skill) and
//   'Skill node' entries (every PixelSkills.TreeNode: description, cost, 'coming soon', links to its skill, the node it needs and the block toggle it unlocks); both are discovered when their pixel / skill is.
//   SETTINGS: 'Setting' entries = every row of the pause menu's Settings screen (PixelPauseMenu.SettingsCatalog reads the BUILT rows: section, label, tick box / slider / drop-down / look picker /
//   button, drop-down choices, current value), so screen filters, day / night length, floor off modes and every other option can be found by searching; the Floor / Sky rows link to each look.
//   PIN: Entry.pinType / pinKey (pixel = currency row, skill, achievement, listable consumable = favorite) drive the window's 'Pin to My Window' button (PixelMyWindow.CanPin / IsPinned / TogglePin).
//   Known leak: a discovered entry's reference text still contains the hidden name, so searching for that exact name finds the discovered entry.
//   CHARACTERS (2026-10-23): every visitor (PixelVisitorMinigame) is a 'Character' entry under its own name (who they are, when they visit, a greeting line,
//   what they sell; discovered once started), and Cubie adds his own (PixelGuideVendor, ExtrasOrder: visits, his bill, links to the Recycler and the visitors). Tab 'People'.
//   MYSTERY BAR (2026-10-18): while PixelSkills.StandardHidden (Standard below level 2) every entry whose name or text says 'Standard' is undiscovered.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A system that puts its own entries into the Game Database: it hands in one or more steps (PixelDatabase.Steps.Add(order, step)); each step adds entries
/// with the builder and links them to entries of earlier steps (see the PixelDatabase.*Order constants). Any system COMPONENT in the scene that implements
/// this is found automatically each time the database is built - a new feature shows up in the database without touching PixelDatabase.
/// </summary>
public interface IPixelDatabaseSource
{
    void DatabaseSteps(PixelDatabase.Steps steps);
}

/// <summary>
/// The game database: one searchable entry for every pixel type, shop pack, skill, consumable, recipe, minigame / vendor, achievement, tip and line of
/// the in-game guides, read from the live game data (nothing is typed in twice). Each entry carries REFERENCES - where else in the game it turns up
/// (which pack rewards or costs it, which skill it belongs to, which recipe uses it, which tip mentions it...), both ways, so a search for 'Water'
/// shows the pixel and everything that points at it. Built by <see cref="Build"/> each time the window opens.
/// </summary>
public static class PixelDatabase
{
    public class Ref
    {
        public string text;
        public string label;  // the part before the target's name ('Rewards pixel'): a reference to something undiscovered shows 'label: ???'
        public string key;    // the entry this reference points to (null = just text)
    }

    public class Entry
    {
        public string category, name, detail = "";
        public readonly List<Ref> refs = new List<Ref>();
        public string blob;   // lower-case text the search looks through
        public string maskedBlob;   // what it is searchable by while undiscovered entries are hidden (just '???' and the category)
        public bool discovered = true;   // pixels / packs / skills / consumables the player has not met yet are marked '???' (the window can show everything)
        public int tierIndex = -1;       // pixel entries: the tier's index (the window draws its turning 3D picture)
        public string pinType, pinKey;   // what 'Pin to My Window' adds (a My Window row type + key; null = this entry cannot be pinned)
        public string Key => category + "|" + name;
    }

    private static List<Entry> entries = new List<Entry>();
    private static Dictionary<string, Entry> byKey = new Dictionary<string, Entry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { entries = new List<Entry>(); byKey = new Dictionary<string, Entry>(); }

    public static IReadOnlyList<Entry> Entries => entries;

    public static Entry Find(string key) => key != null && byKey.TryGetValue(key, out Entry e) ? e : null;

    /// <summary>Colour of a category's tag.</summary>
    public static Color CategoryColor(string category)
    {
        switch (category)
        {
            case "Pixel": return new Color(0.45f, 0.8f, 1f);
            case "Shop pack": return new Color(1f, 0.8f, 0.35f);
            case "Skill": return new Color(0.5f, 1f, 0.55f);
            case "Consumable": return new Color(0.95f, 0.55f, 1f);
            case "Recipe": return new Color(1f, 0.6f, 0.4f);
            case "Minigame": return new Color(1f, 0.5f, 0.55f);
            case "Achievement": return new Color(1f, 0.9f, 0.4f);
            case "Tip": return new Color(0.7f, 0.75f, 0.85f);
            case "Floor": return new Color(0.55f, 0.85f, 0.8f);
            case "Sky": return new Color(0.6f, 0.7f, 1f);
            case "Pet": return new Color(1f, 0.75f, 0.85f);
            case "Block rule": return new Color(0.85f, 0.7f, 1f);
            case "Skill node": return new Color(0.7f, 0.95f, 0.6f);
            case "Setting": return new Color(0.75f, 0.8f, 0.6f);
            case "Upgrade": return new Color(1f, 0.65f, 0.3f);
            case "Special": return new Color(0.4f, 0.9f, 0.95f);
            case "Character": return new Color(0.45f, 0.95f, 0.85f);
            default: return new Color(0.6f, 0.65f, 0.7f);
        }
    }

    private static Entry Add(string category, string name, string detail)
    {
        string key = category + "|" + name;
        if (byKey.TryGetValue(key, out Entry existing))
        {
            if (!string.IsNullOrEmpty(detail) && string.IsNullOrEmpty(existing.detail)) existing.detail = detail;
            return existing;
        }
        Entry e = new Entry { category = category, name = name, detail = detail ?? "" };
        entries.Add(e);
        byKey[key] = e;
        return e;
    }

    private static void Point(Entry from, string label, Entry to)
    {
        if (from == null || to == null || from == to) return;
        string text = label + ": " + to.name + "  (" + to.category + ")";
        foreach (Ref r in from.refs) if (r.key == to.Key && r.text == text) return;
        from.refs.Add(new Ref { text = text, label = label, key = to.Key });
    }

    /// <summary>Two-way link: 'a' lists b under labelA, b lists a under labelB.</summary>
    private static void Link(Entry a, string labelA, Entry b, string labelB)
    {
        Point(a, labelA, b);
        Point(b, labelB, a);
    }

    // ------------------------------------------------------------------
    // Building: every system adds its own entries (IPixelDatabaseSource), in ORDER steps so links can point at what an earlier step added.
    // ------------------------------------------------------------------

    /// <summary>When each group of entries is added (lower first). A step can link only to entries of earlier steps; tips come late so they can mention everything.</summary>
    public const int PixelsOrder = 100, MinigamesOrder = 200, PacksOrder = 300, SkillsOrder = 400, SkillNodesOrder = 410, BlockRulesOrder = 500, PackSkillLinksOrder = 510,
                     ConsumablesOrder = 600, VendorWaresOrder = 610, LooksOrder = 700, SettingsOrder = 710, PetsOrder = 800, RecipesOrder = 900, AchievementsOrder = 1000,
                     ExtrasOrder = 1050, TipsOrder = 1100, GoalsOrder = 1200;

    /// <summary>Entry categories that tips / guide lines never 'mention' by name (texts themselves, or names too generic to match safely).</summary>
    private static readonly HashSet<string> NotMentioned = new HashSet<string> { "Tip", "Achievement", "Recipe", "Floor", "Sky", "Pet", "Block rule", "Skill node", "Upgrade", "Goal" };

    /// <summary>The steps the sources hand in (Build sorts them by order; the same order = the order they were handed in).</summary>
    public sealed class Steps
    {
        internal sealed class Step { public int order, seq; public Action<Builder> run; public string source; }
        internal readonly List<Step> list = new List<Step>();
        internal string current;

        /// <summary>Adds a step: 'run' is called with the builder when Build reaches 'order' (see the *Order constants).</summary>
        public void Add(int order, Action<Builder> run)
        {
            if (run != null) list.Add(new Step { order = order, seq = list.Count, run = run, source = current });
        }
    }

    /// <summary>What a step gets: Add / Link plus the lookups earlier steps filled (pixels by type, minigames by id, packs by index, skills by id, items by name).</summary>
    public sealed class Builder
    {
        public readonly PixelClicker Clicker;
        public readonly Dictionary<PixelClicker.PixelType, Entry> Pixels = new Dictionary<PixelClicker.PixelType, Entry>();
        public readonly Dictionary<string, Entry> Minigames = new Dictionary<string, Entry>();
        public readonly List<Entry> Packs = new List<Entry>();   // by shop pack index (null for an empty slot)
        public readonly Dictionary<string, Entry> Skills = new Dictionary<string, Entry>();
        public readonly Dictionary<string, Entry> Items = new Dictionary<string, Entry>();
        public readonly List<KeyValuePair<Entry, List<string>>> VendorWares = new List<KeyValuePair<Entry, List<string>>>();   // a vendor's entry + the item names it sells
        public readonly List<Entry> Texts = new List<Entry>();   // tip / guide entries whose text is searched for mentions of other entries

        internal Builder(PixelClicker clicker) { Clicker = clicker; }

        /// <summary>The entry of category + name (made if new; an existing one keeps its text unless it had none).</summary>
        public Entry Add(string category, string name, string detail) => PixelDatabase.Add(category, name, detail);

        /// <summary>Two-way link: 'a' lists b under labelA, b lists a under labelB (nulls are ignored).</summary>
        public void Link(Entry a, string labelA, Entry b, string labelB) => PixelDatabase.Link(a, labelA, b, labelB);

        /// <summary>The pixel entry of a type (null when there is none).</summary>
        public Entry Pixel(PixelClicker.PixelType type) => Pixels.TryGetValue(type, out Entry e) ? e : null;

        /// <summary>An entry made by an earlier step, by category + name (null if none).</summary>
        public Entry Find(string category, string name) => PixelDatabase.Find(category + "|" + name);

        public IReadOnlyList<Entry> Entries => entries;
    }

    /// <summary>Plain text: rich-text tags taken out.</summary>
    public static string Plain(string s) => string.IsNullOrEmpty(s) ? "" : System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

    /// <summary>A pixel's name without a trailing ' Pixels'.</summary>
    public static string PixelName(PixelClicker.PixelTier t)
    {
        string n = t.displayName;
        return n != null && n.EndsWith(" Pixels") ? n.Substring(0, n.Length - 7) : n;
    }

    public static void Build()
    {
        entries = new List<Entry>();
        byKey = new Dictionary<string, Entry>();

        // Collect the steps: every system component in the scene that is a source, plus the providers kept here (data with no natural owner file).
        Steps steps = new Steps();
        HashSet<Type> seen = new HashSet<Type>();   // (one source per component type, like the single-system lookups it replaced)
        foreach (MonoBehaviour mb in PixelFind.All<MonoBehaviour>())
            if (mb is IPixelDatabaseSource src && seen.Add(mb.GetType()))
            {
                steps.current = mb.GetType().Name;
                try { src.DatabaseSteps(steps); }
                catch (Exception e) { Debug.LogError("PixelDatabase: " + steps.current + " could not hand in its entries - " + e.Message); }
            }
        steps.current = "PixelDatabase";
        BuiltInSteps(steps);
        steps.list.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.seq.CompareTo(b.seq));

        Builder builder = new Builder(PixelFind.First<PixelClicker>());
        foreach (Steps.Step st in steps.list)
        {
            try { st.run(builder); }
            catch (Exception e) { Debug.LogError("PixelDatabase: a step of " + st.source + " (order " + st.order + ") failed - " + e.Message); }   // (shown, not hidden: a broken entry should be noticed)
        }

        // The mystery bar (PixelSkills.StandardHidden): until Standard level 2 every entry that names it (the skill, its tree nodes, tips and guide
        // lines about it) is undiscovered, so the database never spoils the name.
        if (PixelSkills.StandardHidden)
            foreach (Entry e in entries)
                if (ContainsWord(e.name, "Standard") || ContainsWord(e.detail, "Standard")) e.discovered = false;

        foreach (Entry e in entries)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(e.name).Append('\n').Append(e.category).Append('\n').Append(e.detail);
            foreach (Ref r in e.refs) sb.Append('\n').Append(r.text);
            e.blob = sb.ToString().ToLowerInvariant();
            e.maskedBlob = ("???\n" + e.category).ToLowerInvariant();
        }
        entries.Sort((a, b) => { int c = string.CompareOrdinal(a.category, b.category); return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase); });
    }

    // ------------------------------------------------------------------
    // Providers kept here: groups whose owner is a very large file with no natural place (PixelConsumables, PixelPauseMenu, PixelHints) or that work
    // over several systems at once (the minigame list, vendors' wares, the look pickers, the Resources guide texts and the 'mentions' pass).
    // ------------------------------------------------------------------

    private static void BuiltInSteps(Steps steps)
    {
        // ---- Minigames and vendors: each minigame describes itself (PixelMinigame.AddToDatabase; a vendor also lists its wares) ----
        steps.Add(MinigamesOrder, AddMinigameEntries);

        // ---- Consumables ----
        steps.Add(ConsumablesOrder, AddConsumableEntries);

        // ---- Vendors' wares (a vendor's entry lists what it sells; the consumable points back) ----
        steps.Add(VendorWaresOrder, LinkVendorWares);

        // ---- Floors and skies (the looks the Settings pickers list) ----
        steps.Add(LooksOrder, AddLookEntries);

        // ---- Settings: every row of the pause menu's Settings screen (labels, choices and current values read from the built rows) ----
        steps.Add(SettingsOrder, AddSettingEntries);

        // ---- Tips and the in-game guides: text that MENTIONS other entries (everything added before this step can be mentioned) ----
        steps.Add(TipsOrder, AddTipEntriesAndMentions);
    }

    /// <summary>Built-in database step (BuiltInSteps): Minigames and vendors: each minigame describes itself (PixelMinigame.AddToDatabase; a vendor also lists its wares).</summary>
    private static void AddMinigameEntries(Builder b)
    {
        foreach (PixelMinigame m in PixelMinigame.All) if (m != null) m.AddToDatabase(b);
    }

    /// <summary>Built-in database step (BuiltInSteps): Consumables.</summary>
    private static void AddConsumableEntries(Builder b)
    {
        PixelShop shop = PixelFind.First<PixelShop>();
        PixelConsumables consumables = shop != null ? shop.Consumables : PixelFind.First<PixelConsumables>();
        if (consumables == null) return;
        for (int i = 0; i < consumables.ItemCount; i++)
        {
            // Only items a player can meet: the thousands of craft-only combo potions / attuned devices show up once you hold one.
            if (!consumables.ItemListable(i)) continue;
            try
            {
                string name = consumables.ItemName(i);
                if (string.IsNullOrEmpty(name)) continue;
                Entry ie = b.Add("Consumable", name, Plain(consumables.ItemDescription(i)));
                ie.discovered = b.Clicker != null && b.Clicker.IsUnlocked(consumables.ItemRequiredType(i));
                ie.pinType = "fav"; ie.pinKey = "item:" + name;
                b.Items[name] = ie;
                PixelShop.PackCost[] costs = consumables.ItemCosts(i);
                if (costs != null) foreach (PixelShop.PackCost c in costs) if (c != null && string.IsNullOrEmpty(c.minigameCurrency)) b.Link(ie, "Costs " + c.amount, b.Pixel(c.type), "Pays for consumable");
            }
            catch (Exception e) { Debug.LogError("PixelDatabase: could not list consumable " + i + " - " + e.Message); }   // (shown, not hidden: a broken item should be noticed)
        }
    }

    /// <summary>Built-in database step (BuiltInSteps): Vendors' wares (a vendor's entry lists what it sells; the consumable points back).</summary>
    private static void LinkVendorWares(Builder b)
    {
        foreach (KeyValuePair<Entry, List<string>> vw in b.VendorWares)
            foreach (string wareName in vw.Value)
                if (b.Items.TryGetValue(wareName, out Entry wareEntry)) b.Link(vw.Key, "Sells", wareEntry, "Sold by");
    }

    /// <summary>Built-in database step (BuiltInSteps): Floors and skies (the looks the Settings pickers list).</summary>
    private static void AddLookEntries(Builder b)
    {
        System.Action<string, IPixelLookSource, string> addLooks = (cat, src, where) =>
        {
            if (src == null) return;
            for (int i = 0; i < src.StyleCount; i++)
            {
                string styleName = src.StyleName(i);
                if (string.IsNullOrEmpty(styleName)) continue;
                b.Add(cat, styleName, "A " + cat.ToLowerInvariant() + " style for the game. Pick it in Settings > Gameplay > " + where + "." + (src.Current == i ? "\nIn use now." : ""));
            }
        };
        addLooks("Floor", PixelFloor.Instance, "Floor style");
        addLooks("Sky", PixelSkybox.Instance, "Sky style");
    }

    /// <summary>Built-in database step (BuiltInSteps): Settings: every row of the pause menu's Settings screen (labels, choices and current values read from the built rows).</summary>
    private static void AddSettingEntries(Builder b)
    {
        PixelPauseMenu pauseMenu = PixelFind.First<PixelPauseMenu>();
        if (pauseMenu == null) return;
        foreach (PixelPauseMenu.SettingInfo st in pauseMenu.SettingsCatalog())
        {
            string label = Plain(st.label).Trim();
            string where = "Settings > " + (string.IsNullOrEmpty(st.section) ? "" : Plain(st.section).Trim() + " > ") + label;
            string text = "Found in " + where + ". A " + st.kind.ToLowerInvariant() + ".";
            string lookCategory = st.look == null ? null : (ReferenceEquals(st.look, PixelSkybox.Instance) ? "Sky" : "Floor");
            if (st.choices != null && st.choices.Length > 0) text += "\nChoices: " + string.Join(", ", st.choices) + ".";
            if (lookCategory != null) text += "\nChoices: every " + lookCategory.ToLowerInvariant() + " style (each has its own entry under " + (lookCategory == "Sky" ? "Skies" : "Floors") + ").";
            if (!string.IsNullOrEmpty(st.current)) text += "\nNow: " + Plain(st.current) + ".";
            Entry se = b.Add("Setting", label, text);
            if (lookCategory != null)
                for (int i = 0; i < st.look.StyleCount; i++) b.Link(se, "Lists", b.Find(lookCategory, st.look.StyleName(i)), "Picked in setting");
        }
    }

    /// <summary>Built-in database step (BuiltInSteps): Tips and the in-game guides: text that MENTIONS other entries (everything added before this step can be mentioned).</summary>
    private static void AddTipEntriesAndMentions(Builder b)
    {
        PixelHints hints = PixelFind.First<PixelHints>();
        if (hints != null)
            foreach (PixelHints.Hint h in hints.AllHints)
                if (h != null && !string.IsNullOrEmpty(h.text)) b.Texts.Add(b.Add("Tip", h.id, Plain(h.text)));
        foreach (string resource in new[] { "HowToPlay", "Controls" })
        {
            TextAsset asset = Resources.Load<TextAsset>(resource);
            if (asset == null) continue;
            foreach (string raw in asset.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length < 8 || line.StartsWith("//") || line.StartsWith("#")) continue;
                line = Plain(line).Replace(" | ", ":  ");
                string title = (resource == "HowToPlay" ? "Guide: " : "Controls: ") + (line.Length > 46 ? line.Substring(0, 46) + "..." : line);
                b.Texts.Add(b.Add("Tip", title, line));
            }
        }
        List<Entry> mentionable = new List<Entry>();
        foreach (Entry e in entries) if (!NotMentioned.Contains(e.category) && e.name.Length >= 3) mentionable.Add(e);
        foreach (Entry t in b.Texts)
            foreach (Entry target in mentionable)
                if (ContainsWord(t.detail, target.name) || ContainsWord(t.name, target.name)) b.Link(t, "Mentions", target, "Mentioned in tip / guide");
    }

    private static bool ContainsWord(string text, string word)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word)) return false;
        int at = 0;
        while ((at = text.IndexOf(word, at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            bool before = at == 0 || !char.IsLetterOrDigit(text[at - 1]);
            int end = at + word.Length;
            bool after = end >= text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after) return true;
            at = end;
        }
        return false;
    }

    /// <summary>What the lists show as the entry's name: the real one, or '???' for something undiscovered while those are hidden.</summary>
    public static string ShownName(Entry e, bool showAll) => e.discovered || showAll ? e.name : "???";

    /// <summary>Entries matching a search (every word must appear somewhere), best matches first. An empty query lists everything. 'category' (null = all) narrows it to one kind;
    /// with showAll off an undiscovered entry only matches its category (its name and text stay secret).</summary>
    public static List<Entry> Search(string query, int max, string category = null, bool showAll = true)
    {
        List<Entry> result = new List<Entry>();
        string q = (query ?? "").Trim().ToLowerInvariant();
        string[] words = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        List<KeyValuePair<int, Entry>> scored = new List<KeyValuePair<int, Entry>>();
        foreach (Entry e in entries)
        {
            if (category != null && e.category != category) continue;
            bool open = e.discovered || showAll;
            if (q.Length == 0) { scored.Add(new KeyValuePair<int, Entry>(open ? 1 : 0, e)); continue; }
            string blob = open ? e.blob : e.maskedBlob;
            bool all = true;
            foreach (string w in words) if (blob.IndexOf(w, StringComparison.Ordinal) < 0) { all = false; break; }
            if (!all) continue;
            string name = open ? e.name.ToLowerInvariant() : "???";
            int score = 10;
            if (name == q) score = 100;
            else if (name.StartsWith(q, StringComparison.Ordinal)) score = 80;
            else if (name.Contains(q)) score = 60;
            else if (e.category.ToLowerInvariant().Contains(q)) score = 40;
            else if (open && e.detail.ToLowerInvariant().Contains(q)) score = 20;
            if (e.category == "Tip") score -= 5;
            scored.Add(new KeyValuePair<int, Entry>(score, e));
        }
        if (q.Length == 0) scored.Sort((a, b) =>   // discovered first, then by category and name (List.Sort is not stable, so the tie-break is spelled out)
        {
            if (b.Key != a.Key) return b.Key.CompareTo(a.Key);
            int c = string.CompareOrdinal(a.Value.category, b.Value.category);
            return c != 0 ? c : string.Compare(a.Value.name, b.Value.name, StringComparison.OrdinalIgnoreCase);
        });
        else scored.Sort((a, b) => b.Key != a.Key ? b.Key.CompareTo(a.Key) : string.Compare(a.Value.name, b.Value.name, StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < scored.Count && i < max; i++) result.Add(scored[i].Value);
        return result;
    }
}
