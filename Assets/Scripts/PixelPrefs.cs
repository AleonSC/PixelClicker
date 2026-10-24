// ---- System notes ----
// PixelPrefs.cs
//   The one door to PlayerPrefs: every script calls PixelPrefs.GetInt / SetInt / GetFloat / SetFloat / GetString / SetString / GetBool / SetBool
//   / HasKey / DeleteKey / Save instead of PlayerPrefs directly (same behaviour, nothing is saved to disk until Save, like before).
//   Why: PlayerPrefs can't tell what TYPE a key holds, so reading a float key with GetInt quietly gives 0 - the per-save settings list once did
//   exactly that and wiped the picked glow colour, the hues and the skill pack on every load. In the Editor / dev builds PixelPrefs remembers
//   the type each key is written or declared with and logs a warning the first time a key is used as another type.
//   Per-save settings: PixelSettingsSync copies the keys declared with PixelPrefs.DeclarePerSave(key, kind) into each save file (its own
//   built-in list declares itself the same way); a new per-save setting declares its key once, from its class's ResetStatics or Awake,
//   next to the code that reads it, with the right kind - instead of being added by hand to a list in another file.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class PixelPrefs
{
    /// <summary>What a key holds (PlayerPrefs itself does not know).</summary>
    public enum Kind { Int = 0, Float = 1, String = 2 }

    private static readonly Dictionary<string, Kind> perSave = new Dictionary<string, Kind>();
    private static readonly List<string> perSaveOrder = new List<string>();

    /// <summary>Marks a key as a per-save setting (PixelSettingsSync stores it in each save file). Declaring it again is fine.</summary>
    public static void DeclarePerSave(string key, Kind kind)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!perSave.ContainsKey(key)) perSaveOrder.Add(key);
        perSave[key] = kind;
        Note(key, kind);
    }

    /// <summary>Every per-save key with its kind, in the order declared.</summary>
    public static IEnumerable<KeyValuePair<string, Kind>> PerSaveKeys()
    {
        foreach (string k in perSaveOrder) yield return new KeyValuePair<string, Kind>(k, perSave[k]);
    }

    public static bool HasKey(string key) => PlayerPrefs.HasKey(key);
    public static void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
    public static void Save() => PlayerPrefs.Save();

    public static int GetInt(string key, int fallback = 0) { Note(key, Kind.Int); return PlayerPrefs.GetInt(key, fallback); }
    public static void SetInt(string key, int value) { Note(key, Kind.Int); PlayerPrefs.SetInt(key, value); }
    public static float GetFloat(string key, float fallback = 0f) { Note(key, Kind.Float); return PlayerPrefs.GetFloat(key, fallback); }
    public static void SetFloat(string key, float value) { Note(key, Kind.Float); PlayerPrefs.SetFloat(key, value); }
    public static string GetString(string key, string fallback = "") { Note(key, Kind.String); return PlayerPrefs.GetString(key, fallback); }
    public static void SetString(string key, string value) { Note(key, Kind.String); PlayerPrefs.SetString(key, value); }

    /// <summary>A tick-box setting stored as 1 / 0.</summary>
    public static bool GetBool(string key, bool fallback) => GetInt(key, fallback ? 1 : 0) != 0;
    public static void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);

#if UNITY_EDITOR || DEBUG
    private static readonly Dictionary<string, Kind> seen = new Dictionary<string, Kind>();
    private static readonly HashSet<string> warned = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSeen() { seen.Clear(); warned.Clear(); }
#endif

    /// <summary>Editor / dev builds: remembers each key's type and warns once when a key is used as a different type.</summary>
    private static void Note(string key, Kind kind)
    {
#if UNITY_EDITOR || DEBUG
        if (string.IsNullOrEmpty(key)) return;
        if (!seen.TryGetValue(key, out Kind was)) { seen[key] = kind; return; }
        if (was != kind && warned.Add(key))
            Debug.LogWarning("PixelPrefs: '" + key + "' is used as " + kind + " but was first used as " + was + " - PlayerPrefs would read the wrong value.");
#endif
    }
}
