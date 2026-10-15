using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Settings that belong to a save file. The player's settings (Settings screen, key bindings, volumes) and the dev tools' remembered
/// choices normally live in PlayerPrefs, shared by every save. This copies them into each save file when it is written
/// (<see cref="Collect"/>) and puts them back when a save is loaded (<see cref="Apply"/>), then tells every system that caches
/// a setting to read it again. Display settings of the machine (resolution, fullscreen, VSync, graphics quality, UI scale) and the
/// "seen this tip" flags stay global on purpose. Older saves without settings leave the current ones alone.
/// </summary>
public static class PixelSettingsSync
{
    [Serializable]
    public class Entry
    {
        public string key;
        public int kind;      // 0 = int, 1 = float, 2 = string
        public string value;
    }

    private struct Known
    {
        public string key;
        public int kind;
        public Known(string key, int kind) { this.key = key; this.kind = kind; }
    }

    private static List<Known> known;

    private static List<Known> Known_()
    {
        if (known != null) return known;
        known = new List<Known>();
        // Player settings. The look of the interface (UI colour, button colour, neon bar glow + its colours, bar colour, clicker aura) is NOT listed here on purpose: it belongs to the
        // player's machine, not to a save. Kept per save it was overwritten by the last save's copy on every start (anything changed after the last save was lost).
        foreach (string k in new[]
        {
            "Rotation", "Pulsing", "RunInBackground", "AbbreviateNumbers", "CameraIntro", "Tips", "EventLog", "EventLogSize",
            "PauseStopsGame", "AutoSave", "AutoSaveMessage", "HidePurchased", "Popups", "PopupSize", "ColorBlind", "DropGuide", "FlatView", "ViewKind", "ViewAutoOrbit", "ViewScrollInvert", "ButtonMode", "DayNight", "DayNightLength", "Prospector", "SkillOffline", "ClickFx", "BlockTips", "BlockTipsAlt", "BlockTipsMode", "Filter", "NextUp", "TalkMuted", "FancyMenu", "DidYouKnow", 
        }) known.Add(new Known("PixelClicker.Setting." + k, 0));
        // Floats and strings (they used to be listed as ints, which stored 0 and wiped the picked glow colour, the hues and the skill pack on every load).
        foreach (string k in new[] { "ViewBaseZoom", "ViewAutoSpeed", "FilterStrength" }) known.Add(new Known("PixelClicker.Setting." + k, 1));
        foreach (string k in new[] { "SkillPack", "SkillGuests", "OptInOff", "MyWindow", "PetOutfits", "CustomKeys" }) known.Add(new Known("PixelClicker.Setting." + k, 2));
        // Volumes.
        known.Add(new Known("PixelAudio.Master", 1));
        known.Add(new Known("PixelAudio.Effects", 1));
        known.Add(new Known("PixelAudio.Music", 1));
        known.Add(new Known("PixelAudio.Muted", 0));
        known.Add(new Known("PixelAudio.UiClicks", 0));
        // Music, per save: the music mute, the custom playlist (unticked songs) and the chosen song.
        known.Add(new Known("PixelClicker.Setting.MusicMuted", 0));
        known.Add(new Known("PixelClicker.Setting.MusicShuffle", 0));
        known.Add(new Known("PixelClicker.Setting.PlaylistOff", 2));
        known.Add(new Known("PixelClicker.Setting.Song", 2));
        known.Add(new Known("PixelClicker.Setting.LogoFavorite", 0));   // the favourite main menu logo style (+ 1)
        // Key bindings.
        foreach (string name in Enum.GetNames(typeof(PixelAction))) known.Add(new Known("PixelClicker.Key." + name, 0));
        // Dev tools.
        foreach (string k in new[] { "ClearKey", "Infinite", "Selector", "NoDespawn", "NoLimit", "FarmerSeeds", "God", "PixelIndex", "SelPixel", "SelMinigame", "GodChoice", "ClickCount" })
            known.Add(new Known("PixelClicker.Dev." + k, 0));
        known.Add(new Known("PixelClicker.Dev.Amount", 2));
        known.Add(new Known("PixelClicker.Dev.CustomText", 2));   // the Dev Hub's edited label texts (PixelDevTextEdit)
        return known;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { known = null; baseline = null; }

    private static string baseline;

    /// <summary>The current settings as one string (to tell whether the player changed any since a profile's settings were put in place).</summary>
    private static string Snapshot()
    {
        List<string> parts = new List<string>();
        foreach (Entry e in Collect()) parts.Add(e.key + "=" + e.value);
        parts.Sort(StringComparer.Ordinal);
        return string.Join("|", parts);
    }

    /// <summary>Remembers the settings as they are now (just applied from a save, or reset for a new game).</summary>
    private static void MarkBaseline() { baseline = Snapshot(); }

    /// <summary>Has the player changed a setting (on the main menu, say) since the last Apply / ResetToDefaults? Then Continue keeps their choice instead of overwriting it with the save's.</summary>
    public static bool ChangedSinceBaseline() => baseline != null && Snapshot() != baseline;

    /// <summary>The current settings, ready to be stored in a save file.</summary>
    public static Entry[] Collect()
    {
        List<Entry> list = new List<Entry>();
        foreach (Known k in Known_())
        {
            if (!PlayerPrefs.HasKey(k.key)) continue;
            string value = k.kind == 0 ? PlayerPrefs.GetInt(k.key).ToString()
                         : k.kind == 1 ? PlayerPrefs.GetFloat(k.key).ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                         : PlayerPrefs.GetString(k.key);
            list.Add(new Entry { key = k.key, kind = k.kind, value = value });
        }
        return list.ToArray();
    }

    /// <summary>Puts a save file's settings back (null = an older save without settings: nothing changes) and refreshes the game.</summary>
    public static void Apply(Entry[] entries)
    {
        if (entries == null) return;

        Dictionary<string, Entry> byKey = new Dictionary<string, Entry>();
        foreach (Entry e in entries) if (e != null && !string.IsNullOrEmpty(e.key)) byKey[e.key] = e;

        foreach (Known k in Known_())
        {
            if (!byKey.TryGetValue(k.key, out Entry e) || e.kind != k.kind) { PlayerPrefs.DeleteKey(k.key); continue; } // not stored (or stored in the old, wrong form) = this file uses the default
            try
            {
                if (k.kind == 0) PlayerPrefs.SetInt(k.key, int.Parse(e.value));
                else if (k.kind == 1) PlayerPrefs.SetFloat(k.key, float.Parse(e.value, System.Globalization.CultureInfo.InvariantCulture));
                else PlayerPrefs.SetString(k.key, e.value ?? "");
            }
            catch (FormatException) { PlayerPrefs.DeleteKey(k.key); }
        }
        PlayerPrefs.Save();

        Reload();
        MarkBaseline();
    }

    /// <summary>A brand-new game: every per-save setting goes back to its default (key bindings and volumes are the player's own and stay).</summary>
    public static void ResetToDefaults()
    {
        foreach (Known k in Known_())
        {
            if (k.key.StartsWith("PixelClicker.Key.") || k.key.StartsWith("PixelAudio.")) continue;
            PlayerPrefs.DeleteKey(k.key);
        }
        PlayerPrefs.Save();
        Reload();
        MarkBaseline();
    }

    /// <summary>Tells everything that keeps a setting in memory to read it from PlayerPrefs again.</summary>
    public static void Reload()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker != null) clicker.ReloadSettingsFromPrefs();
        PixelShop.ReloadSettingsFromPrefs();
        PixelKeys.ReloadFromPrefs();
        PixelAudio.ReloadFromPrefs();
        PixelLogoView.ReloadFavorite();
        PixelCustomKeys.ReloadFromPrefs();
        PixelDisplaySettings.ReloadFromPrefs();
        PixelPauseMenu menu = PixelFind.First<PixelPauseMenu>();
        if (menu != null) menu.ReloadSettingsFromPrefs();
        PixelHints.RefreshLog();
        PixelDevTools.ReloadFromPrefs();
        PixelDevTextEdit.ReloadFromPrefs();
        PixelTalk.ReloadFromPrefs();
        PixelOrthoView.ReloadFromPrefs();
        PixelViewWindow.ReloadFromPrefs();
        PixelHud.ReloadGlowFromPrefs();
        PixelUIKit.ReloadColorFromPrefs();
        PixelUIKit.ReloadButtonColorFromPrefs();
        PixelProspector.ReloadSettingsFromPrefs();
        PixelSkills.ReloadSettingsFromPrefs();
        PixelBasicFx.ReloadFromPrefs();
        PixelMyWindow.ReloadFromPrefs();
        PixelPets.ReloadOutfitsFromPrefs();
        PixelDayNight.ReloadFromPrefs();
        PixelFilters.ReloadFromPrefs();
        PixelNextUp.ReloadFromPrefs();
        PixelDidYouKnow.ReloadFromPrefs();
        PixelMenuFx.ReloadFromPrefs();
        PixelClickerAura.ReloadFromPrefs();
    }
}
