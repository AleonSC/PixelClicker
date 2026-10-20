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
//   MYSTERY BAR (2026-10-18): while PixelSkills.StandardHidden (Standard below level 2) every entry whose name or text says 'Standard' is undiscovered.
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
                e.discovered = clicker.IsUnlocked(t.type) || t.totalCollected > 0d;
                e.tierIndex = System.Array.IndexOf(clicker.Tiers, t);
                e.pinType = "currency"; e.pinKey = t.type.ToString();
                pixels[t.type] = e;
            }
        Entry PixelOf(PixelClicker.PixelType type) => pixels.TryGetValue(type, out Entry e) ? e : null;

        // ---- Minigames and vendors ----
        Dictionary<string, Entry> minigames = new Dictionary<string, Entry>();
        List<KeyValuePair<Entry, List<string>>> vendorWares = new List<KeyValuePair<Entry, List<string>>>();
        foreach (PixelMinigame m in PixelMinigame.All)
        {
            if (m == null) continue;
            string desc = m.HasTracker ? Plain(m.TrackerDescription) : "";
            Entry mgEntry = Add("Minigame", m.DisplayName, (string.IsNullOrEmpty(desc) ? "" : desc + "\n") + "Id: " + m.Id + (m.HasTracker ? "\nHas a goal counter: " + Plain(m.TrackerTitle) : ""));
            minigames[m.Id] = mgEntry;
            if (m is PixelVisitorMinigame visitor)
            {
                List<string> ware = visitor.WareNames();
                if (ware.Count > 0) { vendorWares.Add(new KeyValuePair<Entry, List<string>>(mgEntry, ware)); mgEntry.detail += "\nSells: " + string.Join(", ", ware) + "."; }
            }
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
                Entry packEntry = Add("Shop pack", p.displayName, Plain(p.description) + "\nShop tab: " + p.tab + (p.purchased ? "\nBought." : ""));
                bool rewardKnown = false;
                if (p.rewardTiers != null && clicker != null) foreach (PixelClicker.PixelTier rt in p.rewardTiers) if (rt != null && clicker.IsUnlocked(rt.type)) rewardKnown = true;
                packEntry.discovered = p.purchased || rewardKnown;
                packEntries.Add(packEntry);
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
                se.discovered = skills.IsAvailable(s);
                se.pinType = "skill"; se.pinKey = s.id;
                skillEntries[s.id] = se;
                if (s.types != null) foreach (PixelClicker.PixelType t in s.types) Link(se, "Pixel block", PixelOf(t), "Block of skill");
                if (Enum.TryParse(s.fillerType, out PixelClicker.PixelType filler)) Link(se, "Filler block", PixelOf(filler), "Filler block of skill");
                if (Enum.TryParse(s.hazardType, out PixelClicker.PixelType hazard)) Link(se, "Hazard block", PixelOf(hazard), "Hazard block of skill");
            }
        // ---- Skill tree nodes (each its own entry, linked to its skill, the node it needs and the block it unlocks) ----
        if (skills != null)
            foreach (PixelSkills.SkillPack s in skills.Packs)
            {
                if (s == null || s.nodes == null || !skillEntries.TryGetValue(s.id, out Entry owner)) continue;
                Dictionary<string, Entry> nodeEntries = new Dictionary<string, Entry>();
                foreach (PixelSkills.TreeNode n in s.nodes)
                {
                    if (n == null || string.IsNullOrEmpty(n.displayName)) continue;
                    string text = Plain(n.description) + "\nCosts " + n.cost + " skill point" + (n.cost == 1 ? "" : "s") + " (a skill earns one per level above 1)." + (n.minLevel > 1 ? "\nNeeds " + s.displayName + " level " + n.minLevel + "." : "") + (n.implemented ? "" : "\nComing soon: the effect is not built yet.");
                    Entry ne = Add("Skill node", s.displayName + ": " + n.displayName, text);
                    ne.discovered = owner.discovered;
                    nodeEntries[n.id] = ne;
                    Link(ne, "Node of skill", owner, "Tree node");
                    if (Enum.TryParse(n.guestType, out PixelClicker.PixelType guest)) Link(ne, "Unlocks the block toggle for", PixelOf(guest), "Toggle unlocked by tree node");
                }
                foreach (PixelSkills.TreeNode n in s.nodes)
                    if (n != null && !string.IsNullOrEmpty(n.requires) && nodeEntries.TryGetValue(n.id, out Entry mine))
                        foreach (string req in n.requires.Split(','))
                            if (nodeEntries.TryGetValue(req.Trim(), out Entry needed)) Link(mine, "Needs node", needed, "Needed by node");
            }

        // ---- Special and hold blocks (the rules from PixelBlockRules) ----
        PixelBlockRules blockRules = PixelBlockRules.Instance;
        if (blockRules != null)
        {
            foreach (PixelBlockRules.SpecialRule r in blockRules.SpecialRules)
            {
                if (r == null || r.tier == null) continue;
                Entry px = PixelOf(r.tier.type);
                string blockName = px != null ? px.name : r.tier.type.ToString();
                Entry re = Add("Block rule", blockName + ": special block", Plain(PixelBlockRules.GuideNotes(r.tier.type)).Trim());
                re.discovered = px != null && px.discovered;
                Link(re, "Special block", px, "Has a special-block rule");
                if (r.replaces != null) foreach (PixelClicker.PixelType t in r.replaces) Link(re, "Replaces", PixelOf(t), "Can be replaced by a special block");
                if (r.hasBoost) Link(re, "Raised by old pixels of", PixelOf(r.boostBlock), "Raises a special block's chance");
                if (!string.IsNullOrEmpty(r.skillId) && skillEntries.TryGetValue(r.skillId, out Entry sk)) Link(re, "Skill", sk, "Special block of skill");
            }
            foreach (PixelBlockRules.HoldRule r in blockRules.HoldRules)
            {
                if (r == null) continue;
                Entry px = PixelOf(r.type);
                string blockName = px != null ? px.name : r.type.ToString();
                Entry re = Add("Block rule", blockName + ": hold bonus", Plain(PixelBlockRules.GuideNotes(r.type)).Trim());
                re.discovered = px != null && px.discovered;
                Link(re, "Hold block", px, "Has a hold bonus");
                if (r.clearsHazard) Link(re, "Clears hazard", PixelOf(r.hazardType), "Cleared by a held block");
                if (!string.IsNullOrEmpty(r.skillId) && skillEntries.TryGetValue(r.skillId, out Entry sk)) Link(re, "Skill", sk, "Hold bonus of skill");
            }
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
                // Only items a player can meet: the thousands of craft-only combo potions / attuned devices show up once you hold one.
                if (!consumables.ItemListable(i)) continue;
                try
                {
                    string name = consumables.ItemName(i);
                    if (string.IsNullOrEmpty(name)) continue;
                    Entry ie = Add("Consumable", name, Plain(consumables.ItemDescription(i)));
                    ie.discovered = clicker != null && clicker.IsUnlocked(consumables.ItemRequiredType(i));
                    ie.pinType = "fav"; ie.pinKey = "item:" + name;
                    itemEntries[name] = ie;
                    PixelShop.PackCost[] costs = consumables.ItemCosts(i);
                    if (costs != null) foreach (PixelShop.PackCost c in costs) if (c != null && string.IsNullOrEmpty(c.minigameCurrency)) Link(ie, "Costs " + c.amount, PixelOf(c.type), "Pays for consumable");
                }
                catch (Exception e) { Debug.LogError("PixelDatabase: could not list consumable " + i + " - " + e.Message); }   // (shown, not hidden: a broken item should be noticed)
            }

        // ---- Vendors' wares (a vendor's entry lists what it sells; the consumable points back) ----
        foreach (KeyValuePair<Entry, List<string>> vw in vendorWares)
            foreach (string wareName in vw.Value)
                if (itemEntries.TryGetValue(wareName, out Entry wareEntry)) Link(vw.Key, "Sells", wareEntry, "Sold by");

        // ---- Floors and skies (the looks the Settings pickers list) ----
        System.Action<string, IPixelLookSource, string> addLooks = (cat, src, where) =>
        {
            if (src == null) return;
            for (int i = 0; i < src.StyleCount; i++)
            {
                string styleName = src.StyleName(i);
                if (string.IsNullOrEmpty(styleName)) continue;
                Add(cat, styleName, "A " + cat.ToLowerInvariant() + " style for the game. Pick it in Settings > Gameplay > " + where + "." + (src.Current == i ? "\nIn use now." : ""));
            }
        };
        addLooks("Floor", PixelFloor.Instance, "Floor style");
        addLooks("Sky", PixelSkybox.Instance, "Sky style");

        // ---- Settings: every row of the pause menu's Settings screen (labels, choices and current values read from the built rows) ----
        PixelPauseMenu pauseMenu = PixelFind.First<PixelPauseMenu>();
        if (pauseMenu != null)
            foreach (PixelPauseMenu.SettingInfo st in pauseMenu.SettingsCatalog())
            {
                string label = Plain(st.label).Trim();
                string where = "Settings > " + (string.IsNullOrEmpty(st.section) ? "" : Plain(st.section).Trim() + " > ") + label;
                string text = "Found in " + where + ". A " + st.kind.ToLowerInvariant() + ".";
                string lookCategory = st.look == null ? null : (ReferenceEquals(st.look, PixelSkybox.Instance) ? "Sky" : "Floor");
                if (st.choices != null && st.choices.Length > 0) text += "\nChoices: " + string.Join(", ", st.choices) + ".";
                if (lookCategory != null) text += "\nChoices: every " + lookCategory.ToLowerInvariant() + " style (each has its own entry under " + (lookCategory == "Sky" ? "Skies" : "Floors") + ").";
                if (!string.IsNullOrEmpty(st.current)) text += "\nNow: " + Plain(st.current) + ".";
                Entry se = Add("Setting", label, text);
                if (lookCategory != null)
                    for (int i = 0; i < st.look.StyleCount; i++) Link(se, "Lists", Find(lookCategory + "|" + st.look.StyleName(i)), "Picked in setting");
            }

        // ---- Pets: one per pixel type that can have one ----
        PixelPets petSystem = PixelPets.Instance;
        if (clicker != null && petSystem != null)
            foreach (PixelClicker.PixelTier t in clicker.Tiers)
            {
                if (t == null || !PixelPets.CanHavePet(t.type)) continue;
                string pixelName = PixelName(t);
                string special = "";
                switch (t.type.ToString())
                {
                    case "Seed": special = "\nThis pet plants itself in the ground and sows seeds."; break;
                    case "Ghost": special = "\nThis pet only hovers."; break;
                    case "Glass": special = "\nThis pet shatters now and then and pulls itself back together."; break;
                    case "Vacuum": special = "\nThis pet sucks up nearby old pixels and counts them."; break;
                }
                Entry pet = Add("Pet", pixelName + " Pet",
                    "A companion of the " + pixelName + " pixel. Every click has a small chance (" + (petSystem.FindChancePerClick * 100f).ToString("0.###") + "%) of finding it; automatic clicks count less, a Pet Treat helps. " +
                    "While it is out, " + pixelName + " comes up " + petSystem.SpawnWeightFactor.ToString("0.#") + "x as often and its old pixels last " + petSystem.DespawnSlowDown.ToString("0.#") + "x longer." + special);
                pet.discovered = petSystem.IsOwned(t.type);
                Link(pet, "Pet of pixel", PixelOf(t.type), "Has a pet");
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
                ae.pinType = "achievement"; ae.pinKey = achievements.GetId(i);
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
        foreach (Entry e in entries) if (e.category != "Tip" && e.category != "Achievement" && e.category != "Recipe" && e.category != "Floor" && e.category != "Sky" && e.category != "Pet" && e.category != "Block rule" && e.category != "Skill node" && e.name.Length >= 3) mentionable.Add(e);
        foreach (Entry t in texts)
            foreach (Entry target in mentionable)
                if (ContainsWord(t.detail, target.name) || ContainsWord(t.name, target.name)) Link(t, "Mentions", target, "Mentioned in tip / guide");

        // ---- Goals (PixelGoals' table: one entry per row, discovered once it has appeared in the Log's Goals tab) ----
        PixelGoals goalTable = PixelGoals.Instance;
        if (goalTable != null && goalTable.Defs != null)
            foreach (PixelGoals.GoalDef g in goalTable.Defs)
            {
                if (g == null || !g.enabled || string.IsNullOrEmpty(g.title)) continue;
                string reward = goalTable.RewardText(g);
                Entry ge = Add("Goal", g.title, g.description + "\nTarget: " + g.target.ToString("0.#") + (reward.Length > 0 ? "\nReward: " + reward : "") + (PixelGoals.Done(g) ? "\nDone." : ""));
                ge.discovered = PixelGoals.Shown(g);
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
