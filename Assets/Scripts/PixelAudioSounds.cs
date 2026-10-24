// ---- System notes ----
// PixelAudioSounds.cs
//   Part of PixelAudio (partial class): the built-in sound table (CreateDefaultSounds) and which code-made PixelSynth clips fill a sound that has no clips (FillSynthesizedClips / SynthFor). The system notes are in PixelAudio.cs.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PixelAudio
{
    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    private static Sound Make(string id, float cooldown = 0.03f, float pitchMin = 0.97f, float pitchMax = 1.03f)
    {
        return new Sound { id = id, cooldown = cooldown, pitchMin = pitchMin, pitchMax = pitchMax };
    }

    private static List<Sound> CreateDefaultSounds()
    {
        List<Sound> list = new List<Sound>
        {
            Make("click", 0.02f, 0.92f, 1.08f),
            Make("pixel_land", 0.02f, 0.88f, 1.12f),
            Make("pixel_bounce_electric", 0.05f, 0.9f, 1.15f),
            Make("overcharge", 0.3f, 1f, 1f),
            Make("dragon_summon", 1f, 1f, 1f),
            Make("glass_shatter", 0.04f, 0.9f, 1.1f),
            Make("auto_click", 0.06f, 0.92f, 1.08f),
            Make("hit", 0.03f, 0.92f, 1.08f),
            Make("husk_crush", 0.03f, 0.94f, 1.06f),
            Make("vacuum", 0.2f),
            Make("combo", 0.02f, 1f, 1f),
            Make("purchase", 0.08f),
            Make("craft", 0.1f),
            Make("drink", 0.1f),
            Make("device_place", 0.1f),
            Make("achievement", 0.2f),
            Make("ui_click", 0.05f),
            new Sound { id = "menu_open", cooldown = 0.12f, pitchMin = 0.94f, pitchMax = 1.08f, volume = 0.6f },
            Make("grab", 0.05f),
            Make("drop", 0.05f),
            Make("ui_scroll", 0.03f, 0.95f, 1.05f),
            Make("ghost_spawn", 0.2f),
            Make("ghost_click", 0.1f),
            Make("meteor_spawn", 0.2f),
            Make("meteor_click", 0.03f, 0.9f, 1.1f),
            Make("meteor_hit", 0.05f, 0.9f, 1.12f),
            new Sound { id = "god_slifer", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "god_ra", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "god_obelisk", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "stone_space", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "stone_mind", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "stone_reality", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "stone_power", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "stone_time", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "stone_soul", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "stone_cubelette", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "pixel_vanish", cooldown = 0.04f, pitchMin = 0.88f, pitchMax = 1.3f, volume = 0.4f },
            new Sound { id = "meteor_rumble", cooldown = 0f, volume = 0.45f },
            new Sound { id = "studio_whoosh", cooldown = 0f, pitchMin = 1f, pitchMax = 1f },
            new Sound { id = "studio_gameover", cooldown = 0f, pitchMin = 1f, pitchMax = 1f, volume = 0.6f },
            new Sound { id = "studio_hum", cooldown = 0f, pitchMin = 1f, pitchMax = 1f, volume = 0.3f },
            new Sound { id = "studio_coin", cooldown = 0f, pitchMin = 1f, pitchMax = 1f, volume = 0.5f },
            new Sound { id = "studio_start", cooldown = 0f, pitchMin = 1f, pitchMax = 1f, volume = 0.5f },
            Make("blackhole_spawn", 0.2f),
            Make("pad_spawn", 0.2f),
            Make("pad_click", 0.05f, 0.92f, 1.08f),
            Make("pad_wrong", 0.05f),
            Make("pad_ultra", 0.2f, 1f, 1f),
            Make("ultra_gain", 0.2f, 1f, 1f),
            Make("time_stop", 0.3f, 1f, 1f),
            Make("device_remove", 0.2f, 1f, 1f),
            Make("hazard_warn", 0.4f, 1f, 1f),
            Make("termite_chitter", 0.5f, 0.95f, 1.05f),
            Make("axe_swing", 0.1f, 0.95f, 1.08f),
            Make("axe_chop", 0.05f, 0.92f, 1.08f),
            Make("nest_fall", 0.1f, 0.95f, 1.1f),
            Make("tree_creak", 0.4f, 0.95f, 1.05f),   // a tree's last blocks: the trunk creaks (PixelTreeMode.LiveBlocksLeft)
            Make("nest_catch", 0.1f, 0.97f, 1.05f),
            Make("remove_buildup", 0.05f, 1f, 1f),   // the rising whine while the right mouse button is held on a placed device (PixelHold)
            Make("hose_toggle", 0.2f, 1f, 1f),
            Make("bank_suck", 0.1f, 1f, 1f),
            Make("bank_spit", 0.05f, 1f, 1f),
            Make("bank_select", 0.05f, 1f, 1f),
            Make("bank_full", 0.3f, 1f, 1f),
            Make("bank_empty", 0.3f, 1f, 1f),
            Make("time_resume", 0.3f, 1f, 1f),
            Make("time_stop_loop", 0f, 1f, 1f),
            Make("game_start", 0.2f, 1f, 1f),
            Make("bomb_spawn", 0.2f),
            Make("bomb_click", 0.1f),
            Make("bomb_tick", 0.1f, 1f, 1f),
            Make("bomb_explode", 0.2f),
            Make("seed_dig", 0.04f, 0.9f, 1.1f),
            Make("seed_plant", 0.05f, 0.9f, 1.1f),
            Make("water_splash", 0.05f, 0.9f, 1.1f),
            Make("jelly_sting", 0.1f, 0.95f, 1.05f),
            Make("fire_burst", 0.1f, 0.92f, 1.08f),
            Make("dragon_find", 0.1f, 1f, 1f),
            Make("chest_hit", 0.1f, 0.92f, 1.08f),
            Make("chest_open", 0.1f, 1f, 1f),
            Make("dragon_stash", 0.1f, 1f, 1f),
            Make("fire_crackle", 0.15f, 0.85f, 1.15f),
            Make("ore_chip", 0.04f, 0.8f, 1.3f),
            Make("ore_gleam", 0.3f, 0.95f, 1.05f),
            Make("stardust_fall", 0.3f, 0.97f, 1.03f),
            Make("pixel_bounce_ghost", 0.05f, 0.9f, 1.15f),
            Make("ghost_float", 0.3f, 0.95f, 1.05f),
            Make("ufo_beam", 0.3f, 1f, 1f),
            Make("talk_blip", 0.035f, 0.92f, 1.08f),
            Make("stardust_collect", 0.1f, 0.97f, 1.04f),
            Make("farm_hit", 0.04f, 0.85f, 1.2f),
            Make("wood_chop", 0.04f, 0.9f, 1.15f),
            Make("forage_pick", 0.04f, 0.9f, 1.15f),
            Make("fish_splash", 0.04f, 0.9f, 1.15f),
            Make("chipper_feed", 0.08f, 0.95f, 1.05f),
            Make("recycler_shake", 0.5f, 1f, 1f),
            Make("snitch_appear", 0.5f, 1f, 1f),
            Make("snitch_buzz", 0.05f, 0.94f, 1.06f),
            Make("snitch_ready", 0.3f, 1f, 1f),
            Make("snitch_catch", 0.5f, 1f, 1f),
            Make("snitch_miss", 0.2f, 0.95f, 1.05f),
            Make("rack_store", 0.05f, 0.9f, 1.1f),
            Make("farm_gleam", 0.3f, 0.95f, 1.05f),
            Make("traffic_start", 0.2f, 1f, 1f),
            Make("traffic_green", 0.2f, 1f, 1f),
            Make("traffic_yellow", 0.2f, 1f, 1f),
            Make("traffic_red", 0.2f, 1f, 1f),
            Make("traffic_tick", 0.08f, 0.97f, 1.03f),
            Make("traffic_good", 0.04f, 0.97f, 1.05f),
            Make("traffic_bad", 0.06f, 0.95f, 1.02f),
            Make("traffic_win", 0.3f, 1f, 1f),
            Make("traffic_lose", 0.3f, 1f, 1f),
        };
        foreach (string oceanId in PixelOceanSynth.Ids) list.Add(Make(oceanId, 0.05f, 0.95f, 1.05f));   // the bottle / treasure map / whale sounds
        foreach (PixelClicker.PixelType ore in Enum.GetValues(typeof(PixelClicker.PixelType)))
            if (PixelClicker.IsOre(ore)) list.Add(Make("pixel_bounce_" + ore.ToString().ToLowerInvariant(), 0.04f, 0.9f, 1.15f)); // ore clinks when an old ore lands
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
            list.Add(Make("pixel_" + type.ToString().ToLowerInvariant(), 0.03f, 0.92f, 1.08f));
        return list;
    }

    /// <summary>
    /// Sounds that ship without audio files get a clip synthesized in code (only while their clip list is empty, so putting your own
    /// clips in the Inspector replaces them): the Electric pixel's bounce is a short crackling zap.
    /// </summary>
    private void FillSynthesizedClips()
    {
        if (sounds == null) return;
        foreach (Sound s in sounds)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            AudioClip[] made = SynthFor(s.id);
            if (made == null) continue;
            if (s.clips != null && s.clips.Length > 0 && s.clips[0] != null) continue;
            s.clips = made;
        }
    }

    /// <summary>The code-made clips of a sound id (null = this sound has none).</summary>
    private static AudioClip[] SynthFor(string id)
    {
        AudioClip[] ocean = PixelOceanSynth.For(id);
        if (ocean != null) return ocean;
        AudioClip[] godSounds = PixelGodSynth.For(id);
        if (godSounds != null) return godSounds;
        AudioClip[] meteorSounds = PixelMeteorSynth.For(id);
        if (meteorSounds != null) return meteorSounds;
        AudioClip[] studio = PixelStudioSynth.For(id);   // (the Galactic Arcade studio intro)
        if (studio != null) return studio;
        AudioClip[] axe = PixelAxeSynth.For(id);
        if (axe != null) return axe;
        AudioClip[] hazard = PixelHazardSynth.For(id);
        if (hazard != null) return hazard;
        switch (id)
        {
            case "pixel_bounce_electric": return new[] { PixelSynth.Zap(1), PixelSynth.Zap(2), PixelSynth.Zap(3) };
            case "overcharge": return new[] { PixelSynth.Charge() };
            case "menu_open": return new[] { PixelSynth.MenuBloop() };
            case "remove_buildup": return new[] { PixelSynth.Buildup() };
            case "dragon_summon": return new[] { PixelSynth.Summon() };
            case "seed_dig": return new[] { PixelSynth.Dig(1), PixelSynth.Dig(2), PixelSynth.Dig(3) };
            case "seed_plant": return new[] { PixelSynth.Crunch(1), PixelSynth.Crunch(2), PixelSynth.Crunch(3) };
            case "husk_crush": return new[] { PixelSynth.HuskCrush(1), PixelSynth.HuskCrush(2), PixelSynth.HuskCrush(3), PixelSynth.HuskCrush(4) };
            case "pixel_water": return new[] { PixelSynth.WaterClick(1), PixelSynth.WaterClick(2), PixelSynth.WaterClick(3), PixelSynth.WaterClick(4), PixelSynth.WaterClick(5), PixelSynth.WaterClick(6), PixelSynth.WaterClick(7), PixelSynth.WaterClick(8), PixelSynth.WaterClick(9), PixelSynth.WaterClick(10), PixelSynth.WaterClick(11), PixelSynth.WaterClick(12) };   // a pool: every click sounds different
            case "water_splash": return new[] { PixelSynth.Splash(1), PixelSynth.Splash(2), PixelSynth.Splash(3) };
            case "chest_hit": return new[] { PixelSynth.ChestHit(1), PixelSynth.ChestHit(2), PixelSynth.ChestHit(3) };
            case "chest_open": return new[] { PixelSynth.ChestOpen() };
            case "dragon_find": return new[] { PixelSynth.DragonFind() };
            case "dragon_stash": return new[] { PixelSynth.DragonStash() };
            case "fire_burst": return new[] { PixelSynth.FireBurst(1), PixelSynth.FireBurst(2), PixelSynth.FireBurst(3) };
            case "fire_crackle": return new[] { PixelSynth.FireCrackle(1), PixelSynth.FireCrackle(2), PixelSynth.FireCrackle(3) };
            case "ore_chip": return new[] { PixelSynth.OreChip(1), PixelSynth.OreChip(2), PixelSynth.OreChip(3), PixelSynth.OreChip(4) };
            case "ore_gleam": return new[] { PixelSynth.OreGleam() };
            case "pixel_ghost": return new[] { PixelSynth.GhostClick(1), PixelSynth.GhostClick(2), PixelSynth.GhostClick(3) };
            case "pixel_bounce_ghost": return new[] { PixelSynth.GhostBounce(1), PixelSynth.GhostBounce(2), PixelSynth.GhostBounce(3) };
            case "ghost_float": return new[] { PixelSynth.GhostFloat() };
            case "ufo_beam": return new[] { PixelSynth.UfoBeam() };
            case "pixel_vanish": return new[] { PixelSynth.Bloop(1), PixelSynth.Bloop(2), PixelSynth.Bloop(3) };
            case "talk_blip": return new[] { PixelSynth.TalkBlip(1), PixelSynth.TalkBlip(2), PixelSynth.TalkBlip(3), PixelSynth.TalkBlip(4), PixelSynth.TalkBlip(5), PixelSynth.TalkBlip(6) };
            case "stardust_fall": return new[] { PixelSynth.StarFall(1), PixelSynth.StarFall(2) };
            case "stardust_collect": return new[] { PixelSynth.StarChime() };
            case "pixel_wire": return new[] { PixelSynth.Elec(0, 1), PixelSynth.Elec(0, 2), PixelSynth.Elec(0, 3) };
            case "pixel_battery": return new[] { PixelSynth.Elec(1, 1), PixelSynth.Elec(1, 2), PixelSynth.Elec(1, 3) };
            case "pixel_resistor": return new[] { PixelSynth.Elec(3, 1), PixelSynth.Elec(3, 2), PixelSynth.Elec(3, 3) };
            case "pixel_transistor": return new[] { PixelSynth.Elec(4, 1), PixelSynth.Elec(4, 2), PixelSynth.Elec(4, 3) };
            case "pixel_fuse": return new[] { PixelSynth.Elec(5, 1), PixelSynth.Elec(5, 2) };
            case "pixel_bulb": return new[] { PixelSynth.Elec(6, 1), PixelSynth.Elec(6, 2), PixelSynth.Elec(6, 3) };
            case "pixel_capacitor": return new[] { PixelSynth.Elec(2, 1), PixelSynth.Elec(2, 2) };
            case "pixel_obsidian": return new[] { PixelSynth.ObsidianPing(1), PixelSynth.ObsidianPing(2) };
            case "chipper_feed": return new[] { PixelSynth.Chipper(1), PixelSynth.Chipper(2), PixelSynth.Chipper(3) };
            case "recycler_shake": return new[] { PixelSynth.RecyclerShake(2f) };
            case "snitch_appear": return new[] { PixelSnitchSynth.Appear() };
            case "snitch_buzz": return new[] { PixelSnitchSynth.Buzz() };
            case "snitch_ready": return new[] { PixelSnitchSynth.Ready() };
            case "snitch_catch": return new[] { PixelSnitchSynth.Catch() };
            case "snitch_miss": return new[] { PixelSnitchSynth.Miss() };
            case "rack_store": return new[] { PixelAxeSynth.WoodChop(1, 1f, 0.5f, 1f), PixelAxeSynth.WoodChop(8, 0.72f, 0.6f, 1.8f) };
            case "fish_splash": return new[] { PixelSynth.Fishing(0, 1), PixelSynth.Fishing(0, 2), PixelSynth.Fishing(0, 3) };
            case "pixel_fish": return new[] { PixelSynth.Fishing(0, 4), PixelSynth.Fishing(0, 5), PixelSynth.Fishing(0, 6) };
            case "pixel_seaweed": return new[] { PixelSynth.Fishing(1, 1), PixelSynth.Fishing(1, 2), PixelSynth.Fishing(1, 3) };
            case "pixel_shell": return new[] { PixelSynth.Fishing(2, 1), PixelSynth.Fishing(2, 2), PixelSynth.Fishing(2, 3) };
            case "pixel_pearl": return new[] { PixelSynth.Fishing(3, 1), PixelSynth.Fishing(3, 2) };
            case "pixel_driftwood": return new[] { PixelSynth.Fishing(4, 1), PixelSynth.Fishing(4, 2), PixelSynth.Fishing(4, 3) };
            case "pixel_jellyfish": return new[] { PixelSynth.Zap(4), PixelSynth.Zap(5), PixelSynth.Zap(6) };
            case "jelly_sting": return new[] { PixelSynth.Zap(7), PixelSynth.Charge() };
            case "pixel_bait": return new[] { PixelSynth.Farm(4, 1), PixelSynth.Farm(4, 2), PixelSynth.Farm(4, 3) };
            case "forage_pick": return new[] { PixelSynth.Forage(2, 1), PixelSynth.Forage(2, 2), PixelSynth.Forage(2, 3) };
            case "pixel_berry": return new[] { PixelSynth.Forage(0, 1), PixelSynth.Forage(0, 2), PixelSynth.Forage(0, 3) };
            case "pixel_mushroom": return new[] { PixelSynth.Forage(1, 1), PixelSynth.Forage(1, 2), PixelSynth.Forage(1, 3) };
            case "pixel_herb": return new[] { PixelSynth.Forage(2, 1), PixelSynth.Forage(2, 2), PixelSynth.Forage(2, 3) };
            case "pixel_truffle": return new[] { PixelSynth.Forage(3, 1), PixelSynth.Forage(3, 2) };
            case "pixel_weeds": return new[] { PixelSynth.Forage(4, 1), PixelSynth.Forage(4, 2), PixelSynth.Forage(4, 3) };
            case "pixel_mold":
            case "pixel_toadstool": return new[] { PixelSynth.Forage(5, 1), PixelSynth.Forage(5, 2) };
            case "wood_chop": return new[] { PixelAxeSynth.WoodChop(1, 1f, 1f, 1f), PixelAxeSynth.WoodChop(2, 1f, 1f, 1f), PixelAxeSynth.WoodChop(3, 1f, 1f, 1f) };
            case "pixel_wood": return new[] { PixelAxeSynth.WoodChop(1, 1f, 1f, 1f), PixelAxeSynth.WoodChop(2, 1f, 1f, 1f), PixelAxeSynth.WoodChop(3, 1f, 1f, 1f) };   // an axe biting into a log
            case "pixel_bark": return new[] { PixelAxeSynth.WoodChop(4, 1.15f, 2.6f, 0.7f), PixelAxeSynth.WoodChop(5, 1.15f, 2.6f, 0.7f), PixelAxeSynth.WoodChop(6, 1.15f, 2.6f, 0.7f) };   // drier, crackly, shorter
            case "pixel_heartwood": return new[] { PixelAxeSynth.WoodChop(7, 0.72f, 0.6f, 1.8f), PixelAxeSynth.WoodChop(8, 0.72f, 0.6f, 1.8f) };   // deep, dense, rings longer
            case "pixel_sap": return new[] { PixelSynth.Wood(2, 1), PixelSynth.Wood(2, 2), PixelSynth.Wood(2, 3) };
            case "pixel_leaf": return new[] { PixelSynth.Wood(3, 1), PixelSynth.Wood(3, 2), PixelSynth.Wood(3, 3) };
            case "pixel_twig": return new[] { PixelSynth.Wood(5, 1), PixelSynth.Wood(5, 2), PixelSynth.Wood(5, 3) };
            case "pixel_hornetnest": return new[] { PixelSynth.Wood(6, 1), PixelSynth.Wood(6, 2) };
            case "farm_hit": return new[] { PixelSynth.Farm(0, 1), PixelSynth.Farm(0, 2), PixelSynth.Farm(0, 3) };
            case "farm_gleam": return new[] { PixelSynth.Farm(9, 1) };
            case "traffic_start": return new[] { PixelTrafficSynth.Start() };
            case "traffic_green": return new[] { PixelTrafficSynth.Green() };
            case "traffic_yellow": return new[] { PixelTrafficSynth.Yellow() };
            case "traffic_red": return new[] { PixelTrafficSynth.Red() };
            case "traffic_tick": return new[] { PixelTrafficSynth.Tick() };
            case "traffic_good": return new[] { PixelTrafficSynth.Good(1), PixelTrafficSynth.Good(2) };
            case "traffic_bad": return new[] { PixelTrafficSynth.Bad() };
            case "traffic_win": return new[] { PixelTrafficSynth.Win() };
            case "traffic_lose": return new[] { PixelTrafficSynth.Lose() };
            case "pixel_dirt": return new[] { PixelSynth.Farm(1, 1), PixelSynth.Farm(1, 2), PixelSynth.Farm(1, 3) };
            case "pixel_fertilizer": return new[] { PixelSynth.Farm(2, 1), PixelSynth.Farm(2, 2), PixelSynth.Farm(2, 3) };
            case "pixel_pollen": return new[] { PixelSynth.Farm(3, 1), PixelSynth.Farm(3, 2), PixelSynth.Farm(3, 3) };
            case "pixel_wheat": return new[] { PixelSynth.Farm(4, 1), PixelSynth.Farm(4, 2), PixelSynth.Farm(4, 3) };
            case "pixel_weevil": return new[] { PixelSynth.Farm(5, 1), PixelSynth.Farm(5, 2), PixelSynth.Farm(5, 3) };
            case "pixel_goldenfruit": return new[] { PixelSynth.Farm(6, 1), PixelSynth.Farm(6, 2) };
        }
        // Each ore's own click is the sound of it breaking; its landing is a metallic clink.
        foreach (PixelClicker.PixelType ore in System.Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (!PixelClicker.IsOre(ore)) continue;
            string name = ore.ToString().ToLowerInvariant();
            int variant = (int)ore - (int)PixelClicker.PixelType.Copper;
            if (id == "pixel_" + name) return new[] { PixelSynth.OreBreak(1 + variant), PixelSynth.OreBreak(7 + variant) };
            if (id == "pixel_bounce_" + name) return new[] { PixelSynth.OreClink(1 + variant), PixelSynth.OreClink(11 + variant), PixelSynth.OreClink(21 + variant) };
        }
        return null;
    }
}
