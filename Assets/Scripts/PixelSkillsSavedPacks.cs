// ---- System notes ----
// PixelSkillsSavedPacks.cs
//   Part of partial class PixelSkills: the fix-ups EnsureDefaults runs on every saved skill pack - step 3 FixSavedPackContents (the blocks, fillers,
//   hazards, perks and tree nodes added since the pack was saved) and step 4 FixSavedPackTexts (older wording). Each is cut into named steps that run
//   in the same order as before; a new fix goes into the step for its pack (or a new step called last). Moved out of PixelSkills.cs (no behaviour change).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PixelSkills
{
    /// <summary>EnsureDefaults, step 3: a saved pack gets the blocks, fillers, hazards, perks and tree nodes added since it was saved (the steps below, in this order).</summary>
    private void FixSavedPackContents(SkillPack pack)
    {
        FixSavedPackPixels(pack);
        FixSavedWoodcuttingPack(pack);
        FixSavedOtherSkillPacks(pack);
        AddMissingTreeNodes(pack);
    }

    /// <summary>Part of FixSavedPackContents: the Firemaking, Electrician, Mining and Farming pixel lists and fillers.</summary>
    private void FixSavedPackPixels(SkillPack pack)
    {
        if (pack.id == FiremakingId && pack.types != null && System.Array.IndexOf(pack.types, PixelClicker.PixelType.Charcoal) >= 0)   // Charcoal is its own shop pixel now (2026-10-24)
        {
            pack.types = System.Array.FindAll(pack.types, t => t != PixelClicker.PixelType.Charcoal);
            if (pack.spawns != null) pack.spawns = pack.spawns.Replace(", Charcoal", "");
            if (pack.description != null) pack.description = pack.description.Replace("flint, embers, flames and charcoal", "flint, embers and flames");
        }
        if (pack.id == ElectricianId && (pack.types == null || pack.types.Length < 8))   // a saved pack from when Electric was its only pixel
        {
            pack.types = new[] { PixelClicker.PixelType.Electric, PixelClicker.PixelType.Wire, PixelClicker.PixelType.Battery, PixelClicker.PixelType.Capacitor, PixelClicker.PixelType.Resistor, PixelClicker.PixelType.Transistor, PixelClicker.PixelType.Fuse, PixelClicker.PixelType.Bulb };
            pack.spawns = "Electric, Wire, Battery, Capacitor, Resistor, Transistor, Fuse, Bulb";
            pack.xpFrom = "Collecting electrical pixels";
        }
        if (pack.id == MiningId && (pack.types == null || pack.types.Length == 0))
            pack.types = new[] { PixelClicker.PixelType.Copper, PixelClicker.PixelType.Tin, PixelClicker.PixelType.Iron, PixelClicker.PixelType.Lead, PixelClicker.PixelType.Zinc, PixelClicker.PixelType.Nickel };
        // Stone (the old Rock block) is Mining's filler; Dirt is Farming's.
        if (pack.id == MiningId)
        {
            if (System.Array.IndexOf(pack.types, PixelClicker.PixelType.Rock) < 0)
            {
                System.Collections.Generic.List<PixelClicker.PixelType> withStone = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types) { PixelClicker.PixelType.Rock };
                pack.types = withStone.ToArray();
            }
            if (string.IsNullOrEmpty(pack.fillerType)) pack.fillerType = PixelClicker.PixelType.Rock.ToString();
            if (pack.spawns != null && !pack.spawns.Contains("Rock")) pack.spawns = "Rock, " + pack.spawns;
        }
        if (pack.id == FarmingId && string.IsNullOrEmpty(pack.fillerType)) pack.fillerType = PixelClicker.PixelType.Dirt.ToString();
        if (pack.id == FarmingId)
        {
            // A saved Farming pack could lack a farm block (Seed and Water used to be sold on their own): make sure every one is in the rotation.
            System.Collections.Generic.List<PixelClicker.PixelType> farm = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types ?? new PixelClicker.PixelType[0]);
            foreach (PixelClicker.PixelType t in new[] { PixelClicker.PixelType.Dirt, PixelClicker.PixelType.Seed, PixelClicker.PixelType.Water, PixelClicker.PixelType.Fertilizer,
                                                         PixelClicker.PixelType.Pollen, PixelClicker.PixelType.Wheat, PixelClicker.PixelType.Weevil, PixelClicker.PixelType.GoldenFruit })
                if (!farm.Contains(t)) farm.Add(t);
            pack.types = farm.ToArray();
        }
    }

    /// <summary>Part of FixSavedPackContents: the Woodcutting pack's blocks, fillers, hazard and perks.</summary>
    private void FixSavedWoodcuttingPack(SkillPack pack)
    {
        // Hazard blocks: Farming's Pest, Mining's Gas Pocket and Woodcutting's Hornet Nest (part of their packs' pixel lists).
        if (pack.id == WoodcuttingId)
        {
            // A saved Woodcutting pack could lack a block: make sure every one is in the rotation.
            System.Collections.Generic.List<PixelClicker.PixelType> wood = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types ?? new PixelClicker.PixelType[0]);
            foreach (PixelClicker.PixelType t in new[] { PixelClicker.PixelType.Wood, PixelClicker.PixelType.Bark, PixelClicker.PixelType.Sap, PixelClicker.PixelType.Leaf,
                                                         PixelClicker.PixelType.Heartwood, PixelClicker.PixelType.Twig, PixelClicker.PixelType.Pinecone, PixelClicker.PixelType.Moss, PixelClicker.PixelType.HornetNest, PixelClicker.PixelType.GreatOak, PixelClicker.PixelType.Whetstone, PixelClicker.PixelType.Acorn, PixelClicker.PixelType.Pesticide })
                if (!wood.Contains(t)) wood.Add(t);
            pack.types = wood.ToArray();
            if (string.IsNullOrEmpty(pack.fillerType)) pack.fillerType = PixelClicker.PixelType.Twig.ToString();
            if (string.IsNullOrEmpty(pack.extraFillerTypes)) pack.extraFillerTypes = "Pinecone,Moss";   // the filler family
            if (pack.fillerStartShare <= 0f && pack.fillerEndShare <= 0f) { pack.fillerStartShare = 0.75f; pack.fillerEndShare = 0.25f; }   // the fillers show up a little more (was 60% -> 10%)
            if (string.IsNullOrEmpty(pack.hazardType)) pack.hazardType = PixelClicker.PixelType.HornetNest.ToString();
            if (pack.hazardBaseShare <= 0f && pack.hazardPerLevelShare <= 0f) { pack.hazardBaseShare = 0.015f; pack.hazardPerLevelShare = 0.001f; }   // the Termite Hive: half as common as before (3% + 0.2%/level)
            // Perks: the old 'Sharp axe' level 2 perk becomes the Wood Chipper unlock; level 3 is new (a saved pack keeps any text you edited).
            System.Collections.Generic.List<Perk> woodPerks = new System.Collections.Generic.List<Perk>(pack.perks ?? new Perk[0]);
            woodPerks.RemoveAll(pk => pk != null && pk.text != null && pk.text.StartsWith("Sharp axe"));
            if (!woodPerks.Exists(pk => pk != null && pk.level == 2)) woodPerks.Add(new Perk { level = 2, text = WoodChipperPerkText, implemented = true, noticeText = WoodChipperNotice });
            if (!woodPerks.Exists(pk => pk != null && pk.level == 3)) woodPerks.Add(new Perk { level = 3, text = WoodLifespanPerkText, implemented = true, noticeText = WoodLifespanNotice });
            foreach (Perk pk in woodPerks) if (pk != null && pk.level == 2 && pk.text == WoodChipperPerkText) { pk.implemented = true; if (string.IsNullOrEmpty(pk.noticeText)) pk.noticeText = WoodChipperNotice; }
            foreach (Perk pk in woodPerks) if (pk != null && pk.level == 3 && pk.text == WoodLifespanPerkText) { pk.implemented = true; if (string.IsNullOrEmpty(pk.noticeText)) pk.noticeText = WoodLifespanNotice; }
            if (!woodPerks.Exists(pk => pk != null && pk.guestType == "Wood")) woodPerks.Add(new Perk { level = 5, text = WoodGuestPerkText, implemented = true, noticeText = WoodGuestNotice, guestType = "Wood" });
            woodPerks.Sort((a, b) => a.level.CompareTo(b.level));
            pack.perks = woodPerks.ToArray();
        }
    }

    /// <summary>Part of FixSavedPackContents: the Electrician's conveyor perks, the Fishing / Mining / Foraging / Firemaking lists and the Mining / Farming hazards.</summary>
    private void FixSavedOtherSkillPacks(SkillPack pack)
    {
        if (pack.id == ElectricianId)
        {
            // Level 5: the Conveyor Belt Kit goes on sale in the shop (PixelConsumables DeviceKind.ConveyorBelt, shopSkillId electrician / level 5).
            System.Collections.Generic.List<Perk> elPerks = new System.Collections.Generic.List<Perk>(pack.perks ?? new Perk[0]);
            bool changedPerks = false;
            if (!elPerks.Exists(pk => pk != null && pk.level == 5)) { elPerks.Add(new Perk { level = 5, text = ConveyorPerkText, implemented = true, noticeText = ConveyorNotice }); changedPerks = true; }
            if (!elPerks.Exists(pk => pk != null && pk.level == 10)) { elPerks.Add(new Perk { level = 10, text = ConveyorWidePerkText, implemented = true, noticeText = ConveyorWideNotice }); changedPerks = true; }
            if (!elPerks.Exists(pk => pk != null && pk.level == 20)) { elPerks.Add(new Perk { level = 20, text = ConveyorWidestPerkText, implemented = true, noticeText = ConveyorWidestNotice }); changedPerks = true; }
            if (changedPerks)
            {
                elPerks.Sort((a, b) => a.level.CompareTo(b.level));
                pack.perks = elPerks.ToArray();
            }
        }
        if (pack.id == FishingId)
        {
            System.Collections.Generic.List<PixelClicker.PixelType> fish = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types ?? new PixelClicker.PixelType[0]);
            foreach (PixelClicker.PixelType t in new[] { PixelClicker.PixelType.Fish, PixelClicker.PixelType.Seaweed, PixelClicker.PixelType.Shell,
                                                         PixelClicker.PixelType.Pearl, PixelClicker.PixelType.Water, PixelClicker.PixelType.Driftwood, PixelClicker.PixelType.Bait, PixelClicker.PixelType.Jellyfish,
                                                         PixelClicker.PixelType.Bottle, PixelClicker.PixelType.Whale })
                if (!fish.Contains(t)) fish.Add(t);
            pack.types = fish.ToArray();
            if (string.IsNullOrEmpty(pack.fillerType) || pack.fillerType == PixelClicker.PixelType.Driftwood.ToString())
            {
                pack.fillerType = PixelClicker.PixelType.Water.ToString();
                pack.fillerStartShare = 0.75f; pack.fillerEndShare = 0.5f;
                pack.spawns = "Water, Fish, Seaweed, Shell, Pearl";
                pack.description = "Swap the basic cubes for fished goods: very common Water, seaweed, shells, rare pearls and rare fish. Click a fish and a Weak Spots round starts: hit every spot to catch it, miss and it gets away. Water is a plain filler that gets rarer as your fishing level rises. Watch out for the stinging jellyfish. A cheap early skill that leads on towards cooking.";
            }
            if (string.IsNullOrEmpty(pack.hazardType)) pack.hazardType = PixelClicker.PixelType.Jellyfish.ToString();
        }
        if (pack.id == MiningId && pack.types != null)
        {
            // Every ore is in: Lead / Zinc / Nickel are back alongside Silver / Gold / Obsidian / Bedrock; the filler is called Rock.
            System.Collections.Generic.List<PixelClicker.PixelType> mine = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types);
            mine.RemoveAll(PixelClicker.IsRetiredOre);
            foreach (PixelClicker.PixelType t in MiningTypes())
                if (!mine.Contains(t)) mine.Add(t);
            pack.types = mine.ToArray();
            pack.spawns = MiningSpawns;
            pack.description = MiningDescription;
        }
        if (pack.id == ForagingId)
        {
            System.Collections.Generic.List<PixelClicker.PixelType> forage = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types ?? new PixelClicker.PixelType[0]);
            foreach (PixelClicker.PixelType t in new[] { PixelClicker.PixelType.Berry, PixelClicker.PixelType.Mushroom, PixelClicker.PixelType.Herb,
                                                         PixelClicker.PixelType.Truffle, PixelClicker.PixelType.Weeds, PixelClicker.PixelType.Mold, PixelClicker.PixelType.Toadstool })
                if (!forage.Contains(t)) forage.Add(t);   // (the Toadstool is back beside the Mold)
            pack.types = forage.ToArray();
            if (string.IsNullOrEmpty(pack.fillerType)) pack.fillerType = PixelClicker.PixelType.Weeds.ToString();
            pack.hazardType = "Mold,Toadstool";
            pack.uniformRotation = true;   // a truly random rotation: a pure reaction check
            pack.description = ForagingDescription;
            pack.spawns = ForagingSpawns;
        }
        if (pack.id == FiremakingId && (pack.types == null || pack.types.Length == 0)) pack.types = FiremakingTypes();
        if (pack.id == MiningId || pack.id == FarmingId)
        {
            PixelClicker.PixelType hazard = pack.id == MiningId ? PixelClicker.PixelType.GasPocket : PixelClicker.PixelType.Pest;
            if (pack.types != null && System.Array.IndexOf(pack.types, hazard) < 0)
            {
                System.Collections.Generic.List<PixelClicker.PixelType> withHazard = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types) { hazard };
                pack.types = withHazard.ToArray();
            }
            if (string.IsNullOrEmpty(pack.hazardType)) pack.hazardType = hazard.ToString();
        }
    }

    /// <summary>Part of FixSavedPackContents: built-in trees get any default node the saved pack is missing.</summary>
    private void AddMissingTreeNodes(SkillPack pack)
    {
        // Built-in trees: add any default node the saved pack is missing (ones you edited are kept).
        if (pack.id != MiningId && pack.id != OfflineId && pack.id != FarmingId && pack.id != ElectricianId && pack.id != WoodcuttingId && pack.id != StandardId) return;
        System.Collections.Generic.List<TreeNode> merged = new System.Collections.Generic.List<TreeNode>(pack.nodes ?? new TreeNode[0]);
        foreach (TreeNode def in DefaultNodes(pack.id))
            if (!merged.Exists(n => n.id == def.id)) merged.Add(def);
            else if (def.id == "hold" && pack.id == StandardId) { TreeNode saved = merged.Find(n => n.id == def.id); if (saved.description != null && saved.description.Contains("Holding a basic block puts a stake")) saved.description = def.description; }   // (2026-10-16: right-hold is each block's second action)
        pack.nodes = merged.ToArray();
    }

    /// <summary>EnsureDefaults, step 4: older wording in a saved pack (renamed packs, retuned XP, perk popups) is brought up to date.</summary>
    private void FixSavedPackTexts(SkillPack pack)
    {
        if (pack.unlockHint != null) pack.unlockHint = pack.unlockHint.Replace("Woodcutter's Pack", "Lumberjack's Pack");   // (renamed pack)
        if (pack.description != null) pack.description = pack.description.Replace("Woodcutter's Pack", "Lumberjack's Pack");
        if (pack.replaces != null) pack.replaces = pack.replaces.Replace("Monochrome, RGB, Glass, Luminescent", "Monochrome, RGB");
        if (pack.id == OfflineId)
        {
            if (pack.description != null && (pack.description.StartsWith("Pick this skill") || pack.description.StartsWith("A PASSIVE skill: it never trains while you play. While")))   // (2026-10-16: offline XP only comes from offline progress)
            { pack.description = "A PASSIVE skill: it never trains while you play. Pick what your character does while the game is closed: your Auto Clicker keeps working (offline progress), every offline click gives Offline Progression XP and a little XP to the skill you picked, shown in the Welcome back window. Its levels will make your time away pay more."; pack.replaces = "Nothing (passive)"; pack.spawns = "Nothing (passive)"; pack.xpFrom = "Offline progress only: each offline Auto Clicker click while the game is closed"; }
            if (pack.description != null && pack.description.Contains("You earn no XP for that pack"))
                pack.description = "Pick this skill, then pick which pack's pixels you play with. Every click trains Offline Progression, which will improve what you earn while you are away. The pack you assign still earns 50% of its normal XP. With no skill pack assigned (Standard), Offline Progression earns +50% XP.";
            if (pack.xpFrom == "Every click, whichever pack is in play")
                pack.xpFrom = "Every click (the assigned pack earns 50%; +50% for itself with no pack assigned)";
        }
        if (pack.id == StandardId)
        {
            FixSavedStandardPerks(pack);
            FixSavedStandardNotices(pack);
        }
    }

    /// <summary>Part of FixSavedPackTexts (the Standard pack): its perks, XP curve and the grip / crush / Double floor perks added later.</summary>
    private void FixSavedStandardPerks(SkillPack pack)
    {
        if (pack.perks == null || pack.perks.Length == 0) pack.perks = StandardPerks();
        if (Mathf.Approximately(pack.baseXp, 80f)) pack.baseXp = 380f;
        if (Mathf.Approximately(pack.baseXp, 380f)) pack.baseXp = 600f;   // (2026-10-17: the Recycler's streak XP + Husk Crush levelled Standard ~2x too fast - level 4 at 8:21 in the 10:50 report)
        if (pack.perks != null && !System.Array.Exists(pack.perks, pk => pk != null && pk.text != null && pk.text.StartsWith("Steady grip")))
        {
            // The grip perks (Block Hold starts at half length; level 3 and level 5 each add 10% of it): added to a saved Standard pack, kept in level order.
            System.Collections.Generic.List<Perk> withGrip = new System.Collections.Generic.List<Perk>(pack.perks);
            foreach (Perk def in StandardPerks())
                if (def.text.StartsWith("Steady grip") || def.text.StartsWith("Firm grip")) withGrip.Add(def);
            withGrip.Sort((x, y) => (x != null ? x.level : 0).CompareTo(y != null ? y.level : 0));
            pack.perks = withGrip.ToArray();
        }   // level 2 now takes the 380 XP level 4 used to (the whole curve x4.75, growth stays 1.5)
        if (pack.perks != null)
        {
            // Perks added later (2026-10-18: the crush refund perks, Double floor): any missing one is added to a saved Standard pack, kept in level order.
            foreach (Perk pk in pack.perks)   // (the first wording of Double floor, 2026-10-18, was about the husk limit)
                if (pk != null && pk.text == "Double floor: the floor holds twice as many husks") pk.text = DoubleFloorPerk;
            System.Collections.Generic.List<Perk> withLater = new System.Collections.Generic.List<Perk>(pack.perks);
            bool added = false;
            foreach (Perk def in StandardPerks())
                if ((def.text == CrushPerkPower || def.text == CrushPerkGrip || def.text == CrushPerkRhythm || def.text == DoubleFloorPerk)
                    && !withLater.Exists(pk => pk != null && pk.text == def.text)) { withLater.Add(def); added = true; }
            if (added)
            {
                withLater.Sort((x, y) => (x != null ? x.level : 0).CompareTo(y != null ? y.level : 0));
                pack.perks = withLater.ToArray();
            }
        }
    }

    /// <summary>Part of FixSavedPackTexts (the Standard pack): its level 2 / 3 perk popups, perk wording, description, XP and spawn texts.</summary>
    private void FixSavedStandardNotices(SkillPack pack)
    {
        if (pack.perks != null)   // the level 2 popup that introduces the skill
            foreach (Perk perk in pack.perks)
                if (perk != null && perk.level == 2 && (string.IsNullOrEmpty(perk.noticeText) || perk.noticeText.Contains("Every pixel you click gives Standard XP") || perk.noticeText.Contains("every level unlocks a perk.\n\nYour first perk") || perk.noticeText.Contains("Later points take the other one and branch further.") || perk.noticeText.StartsWith("Standard level 2! Surprise"))) perk.noticeText = StandardLevel2Notice;
        if (pack.perks != null)   // the level 3 popup: the second point is a choice - the skipped move or Aimed Clicks (2026-10-17)
            foreach (Perk perk in pack.perks)
                if (perk != null && perk.level == 3 && string.IsNullOrEmpty(perk.noticeText) && perk.text != null && perk.text.StartsWith("Steady grip")) { perk.noticeText = StandardLevel3Notice; perk.noticeInWindow = true; }
        if (perkWindowVersion < 1 && pack.perks != null)   // the level 2 popup opens the Skills window and frames the Standard card
            foreach (Perk perk in pack.perks)
                if (perk != null && perk.level == 2) perk.noticeInWindow = true;
        if (pack.perks != null)
            foreach (Perk perk in pack.perks)
                if (perk != null && perk.text != null && perk.text.Contains("last 2 seconds longer")) perk.text = perk.text.Replace("last 2 seconds longer", "last 5 seconds longer");
        if (pack.description != null && pack.description.Contains("No skill is trained")) pack.description = StandardDescription;
        if (pack.xpFrom == "Nothing") pack.xpFrom = "1 XP for every block you collect, whatever skill is trained";
        if (pack.description != null && pack.description.StartsWith("The usual rotation: monochrome, RGB, Glass and Luminescent"))
            pack.description = "The usual rotation: the six monochrome and RGB pixels (Glass and Luminescent join in once bought, and have their own switches in Toggles). No skill is trained.";
        if (pack.spawns == "White, Gray, Black, Red, Green, Blue, Glass, Luminescent")
            pack.spawns = "White, Gray, Black, Red, Green, Blue (+ Glass, Luminescent once bought)";
    }
}
