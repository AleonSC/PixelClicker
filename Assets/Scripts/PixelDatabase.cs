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
//   wares. A new kind of game data should be added to PixelDatabase.Build.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

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
        public string key;    // the entry this reference points to (null = just text)
    }

    public class Entry
    {
        public string category, name, detail = "";
        public readonly List<Ref> refs = new List<Ref>();
        public string blob;   // lower-case text the search looks through
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
        from.refs.Add(new Ref { text = text, key = to.Key });
    }

    /// <summary>Two-way link: 'a' lists b under labelA, b lists a under labelB.</summary>
    private static void Link(Entry a, string labelA, Entry b, string labelB)
    {
        Point(a, labelA, b);
        Point(b, labelB, a);
    }

    private static string PixelName(PixelClicker.PixelTier t)
    {
        string n = t.displayName;
        return n != null && n.EndsWith(" Pixels") ? n.Substring(0, n.Length - 7) : n;
    }

    private static string Plain(string s) => string.IsNullOrEmpty(s) ? "" : System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

    public static void Build()
    {
        entries = new List<Entry>();
        byKey = new Dictionary<string, Entry>();

        PixelClicker clicker = PixelFind.First<PixelClicker>();
        PixelShop shop = PixelFind.First<PixelShop>();
        PixelSkills skills = PixelSkills.Instance;
        PixelConsumables consumables = shop != null ? shop.Consumables : PixelFind.First<PixelConsumables>();
        PixelCrafting crafting = PixelFind.First<PixelCrafting>();
        PixelAchievements achievements = PixelFind.First<PixelAchievements>();
        PixelHints hints = PixelFind.First<PixelHints>();

        // ---- Pixels ----
        Dictionary<PixelClicker.PixelType, Entry> pixels = new Dictionary<PixelClicker.PixelType, Entry>();
        if (clicker != null)
            foreach (PixelClicker.PixelTier t in clicker.Tiers)
            {
                if (t == null) continue;
                string desc;
                try { desc = Plain(PixelGuideTexts.Describe(t.type, t)); } catch (Exception) { desc = ""; }
                Entry e = Add("Pixel", PixelName(t), desc + "\nPays " + t.amountPerClick + " per collect, " + Mathf.Max(1, t.clicksToCollect) + " click" + (t.clicksToCollect > 1 ? "s" : "") + ", spawn weight " + t.spawnWeight + ". Internal type: " + t.type + ".");
                pixels[t.type] = e;
            }
        Entry PixelOf(PixelClicker.PixelType type) => pixels.TryGetValue(type, out Entry e) ? e : null;

        // ---- Minigames and vendors ----
        Dictionary<string, Entry> minigames = new Dictionary<string, Entry>();
        foreach (PixelMinigame m in PixelMinigame.All)
        {
            if (m == null) continue;
            string desc = m.HasTracker ? Plain(m.TrackerDescription) : "";
            minigames[m.Id] = Add("Minigame", m.DisplayName, (string.IsNullOrEmpty(desc) ? "" : desc + "\n") + "Id: " + m.Id + (m.HasTracker ? "\nHas a goal counter: " + Plain(m.TrackerTitle) : ""));
        }

        // ---- Shop packs ----
        List<Entry> packEntries = new List<Entry>();
        if (shop != null)
        {
            IReadOnlyList<PixelShop.ShopPack> packs = shop.AllPacks;
            for (int i = 0; i < packs.Count; i++)
            {
                PixelShop.ShopPack p = packs[i];
                if (p == null) { packEntries.Add(null); continue; }
                packEntries.Add(Add("Shop pack", p.displayName, Plain(p.description) + "\nShop tab: " + p.tab + (p.purchased ? "\nBought." : "")));
            }
            for (int i = 0; i < packs.Count; i++)
            {
                PixelShop.ShopPack p = packs[i];
                Entry pe = packEntries[i];
                if (p == null || pe == null) continue;
                if (p.rewardTiers != null)
                    foreach (PixelClicker.PixelTier t in p.rewardTiers) Link(pe, "Rewards pixel", PixelOf(t.type), "Rewarded by shop pack");
                if (p.costs != null)
                    foreach (PixelShop.PackCost c in p.costs)
                    {
                        if (!string.IsNullOrEmpty(c.minigameCurrency)) { minigames.TryGetValue(c.minigameCurrency, out Entry mg); Link(pe, "Costs " + c.amount + " of minigame currency", mg, "Spent on shop pack"); }
                        else Link(pe, "Costs " + c.amount, PixelOf(c.type), "Pays for shop pack");
                    }
                if (p.requirements != null)
                    foreach (PixelShop.PackRequirement r in p.requirements)
                    {
                        if (r.kind == PixelShop.RequirementKind.Pack && r.packIndex >= 0 && r.packIndex < packEntries.Count) Link(pe, "Requires pack", packEntries[r.packIndex], "Required by pack");
                        else if (r.kind != PixelShop.RequirementKind.Pack && minigames.TryGetValue(r.minigameId ?? "", out Entry mg)) Link(pe, "Requires goal of", mg, "Goal needed for shop pack");
                    }
                if (!string.IsNullOrEmpty(p.unlocksMinigame) && minigames.TryGetValue(p.unlocksMinigame, out Entry unlocked)) Link(pe, "Unlocks minigame", unlocked, "Unlocked by shop pack");
            }
        }

        // ---- Skills ----
        Dictionary<string, Entry> skillEntries = new Dictionary<string, Entry>();
        if (skills != null)
            foreach (PixelSkills.SkillPack s in skills.Packs)
            {
                if (s == null) continue;
                System.Text.StringBuilder sb = new System.Text.StringBuilder(Plain(s.description));
                if (s.perks != null) foreach (PixelSkills.Perk perk in s.perks) sb.Append("\nLevel ").Append(perk.level).Append(": ").Append(Plain(perk.text));
                if (s.nodes != null) foreach (PixelSkills.TreeNode n in s.nodes) sb.Append("\nSkill tree - ").Append(n.displayName).Append(": ").Append(Plain(n.description));
                Entry se = Add("Skill", s.displayName, sb.ToString());
                skillEntries[s.id] = se;
                if (s.types != null) foreach (PixelClicker.PixelType t in s.types) Link(se, "Pixel block", PixelOf(t), "Block of skill");
                if (Enum.TryParse(s.fillerType, out PixelClicker.PixelType filler)) Link(se, "Filler block", PixelOf(filler), "Filler block of skill");
                if (Enum.TryParse(s.hazardType, out PixelClicker.PixelType hazard)) Link(se, "Hazard block", PixelOf(hazard), "Hazard block of skill");
            }
        if (shop != null)
        {
            IReadOnlyList<PixelShop.ShopPack> packs = shop.AllPacks;
            for (int i = 0; i < packs.Count; i++)
                if (packs[i] != null && !string.IsNullOrEmpty(packs[i].skillId) && skillEntries.TryGetValue(packs[i].skillId, out Entry se))
                    Link(packEntries[i], "Unlocks skill", se, "Unlocked by shop pack");
        }

        // ---- Consumables ----
        Dictionary<string, Entry> itemEntries = new Dictionary<string, Entry>();
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount; i++)
            {
                string name;
                try { name = consumables.ItemName(i); } catch (Exception) { continue; }
                if (string.IsNullOrEmpty(name)) continue;
                string desc = "";
                try { desc = Plain(consumables.ItemDescription(i)); } catch (Exception) { }
                Entry ie = Add("Consumable", name, desc);
                itemEntries[name] = ie;
                try
                {
                    PixelShop.PackCost[] costs = consumables.ItemCosts(i);
                    if (costs != null) foreach (PixelShop.PackCost c in costs) if (string.IsNullOrEmpty(c.minigameCurrency)) Link(ie, "Costs " + c.amount, PixelOf(c.type), "Pays for consumable");
                }
                catch (Exception) { }
            }

        // ---- Recipes ----
        if (crafting != null)
            foreach (PixelCrafting.Recipe r in crafting.DefinedRecipes)
            {
                if (r == null) continue;
                Entry re = Add("Recipe", string.IsNullOrEmpty(r.label) ? "Recipe" : r.label, "Crafting: makes a " + r.resultKind + (r.resultKind == PixelCrafting.ResultKind.Device ? "" : " (" + r.resultPotion + ")") + ".");
                foreach (PixelCrafting.Ingredient ing in new[] { r.a, r.b })
                    if (ing != null && ing.kind != PixelCrafting.ItemKind.Device) Link(re, (ing.kind == PixelCrafting.ItemKind.Potion ? "Uses potion of" : "Uses pixel"), PixelOf(ing.type), "Ingredient in recipe");
                if (r.resultKind == PixelCrafting.ResultKind.Potion || r.resultKind == PixelCrafting.ResultKind.Combo || r.resultKind == PixelCrafting.ResultKind.Triple) Link(re, "Makes potion of", PixelOf(r.resultPotion), "Potion made by recipe");
            }

        // ---- Achievements ----
        if (achievements != null)
            for (int i = 0; i < achievements.Count; i++)
            {
                PixelAchievements.Achievement a = achievements.Get(i);
                if (a == null) continue;
                Entry ae = Add("Achievement", Plain(a.title), Plain(a.description));
                if (a.kind == PixelAchievements.Kind.CollectPixelType || a.kind == PixelAchievements.Kind.ClickPixelType) Link(ae, "About pixel", PixelOf(a.pixelType), "Achievement");
            }

        // ---- Tips and the in-game guides: text that MENTIONS other entries ----
        List<Entry> texts = new List<Entry>();
        if (hints != null)
            foreach (PixelHints.Hint h in hints.AllHints)
                if (h != null && !string.IsNullOrEmpty(h.text)) texts.Add(Add("Tip", h.id, Plain(h.text)));
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
                texts.Add(Add("Tip", title, line));
            }
        }
        List<Entry> mentionable = new List<Entry>();
        foreach (Entry e in entries) if (e.category != "Tip" && e.category != "Achievement" && e.category != "Recipe" && e.name.Length >= 3) mentionable.Add(e);
        foreach (Entry t in texts)
            foreach (Entry target in mentionable)
                if (ContainsWord(t.detail, target.name) || ContainsWord(t.name, target.name)) Link(t, "Mentions", target, "Mentioned in tip / guide");

        foreach (Entry e in entries)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(e.name).Append('\n').Append(e.category).Append('\n').Append(e.detail);
            foreach (Ref r in e.refs) sb.Append('\n').Append(r.text);
            e.blob = sb.ToString().ToLowerInvariant();
        }
        entries.Sort((a, b) => { int c = string.CompareOrdinal(a.category, b.category); return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase); });
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

    /// <summary>Entries matching a search (every word must appear somewhere), best matches first. An empty query lists everything.</summary>
    public static List<Entry> Search(string query, int max)
    {
        List<Entry> result = new List<Entry>();
        string q = (query ?? "").Trim().ToLowerInvariant();
        if (q.Length == 0)
        {
            for (int i = 0; i < entries.Count && i < max; i++) result.Add(entries[i]);
            return result;
        }
        string[] words = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        List<KeyValuePair<int, Entry>> scored = new List<KeyValuePair<int, Entry>>();
        foreach (Entry e in entries)
        {
            bool all = true;
            foreach (string w in words) if (e.blob.IndexOf(w, StringComparison.Ordinal) < 0) { all = false; break; }
            if (!all) continue;
            string name = e.name.ToLowerInvariant();
            int score = 10;
            if (name == q) score = 100;
            else if (name.StartsWith(q, StringComparison.Ordinal)) score = 80;
            else if (name.Contains(q)) score = 60;
            else if (e.category.ToLowerInvariant().Contains(q)) score = 40;
            else if (e.detail.ToLowerInvariant().Contains(q)) score = 20;
            if (e.category == "Tip") score -= 5;
            scored.Add(new KeyValuePair<int, Entry>(score, e));
        }
        scored.Sort((a, b) => b.Key != a.Key ? b.Key.CompareTo(a.Key) : string.Compare(a.Value.name, b.Value.name, StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < scored.Count && i < max; i++) result.Add(scored[i].Value);
        return result;
    }
}
