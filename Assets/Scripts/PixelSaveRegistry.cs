// ---- System notes ----
// PixelSaveRegistry.cs
//   The list of systems that SAVE THEMSELVES. A system implements IPixelSaved (SaveId, SaveOrder, Export, Import), calls
//   PixelSaveRegistry.Register(this) in Awake and Unregister(this) in OnDestroy, and PixelSaveGame writes its Export() into the save file as
//   one named part (SaveData.parts) and hands it back to Import() on load - PixelSaveGame no longer needs a field and two hand-written
//   blocks for every system. A NEW thing to save: implement IPixelSaved on its system (usually a small [Serializable] class + PixelSaveRegistry.ToJson /
//   FromJson), nothing to add in PixelSaveGame.
//   Import(null) = the save has no part for it (a new game, or a save from before the system existed): go back to the starting state.
//   Load order (SaveOrder, lower first). PixelSaveGame's own core steps sit between these bands:
//     < SettingsBand (200): before the save's settings are applied (toggles, auto clicker, pets, UFO, event log)
//     < MinigamesBand (300): after the settings, before the minigame counters (potion / device inventory)
//     < WorldBand (500): after the minigames, before FinishLoad (achievements, stats)
//     >= WorldBand: after FinishLoad and the husks are back on the field (Bank, placed devices + running potion)
//   An error in one part is logged and the others still load / save.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A system that writes its own part of the save file (see PixelSaveRegistry).</summary>
public interface IPixelSaved
{
    /// <summary>The part's name in the save file. Never change it once used (old saves are matched by it).</summary>
    string SaveId { get; }
    /// <summary>When it loads (lower first; see the bands in PixelSaveRegistry).</summary>
    int SaveOrder { get; }
    /// <summary>The state as text (usually JSON), or null for nothing to save.</summary>
    string Export();
    /// <summary>Puts the state back. Null = the save has no part for this system: go back to the starting state.</summary>
    void Import(string data);
}

public static class PixelSaveRegistry
{
    public const int SettingsBand = 200, MinigamesBand = 300, WorldBand = 500;

    /// <summary>One part of a save file.</summary>
    [Serializable]
    public class Part
    {
        public string id;
        public string data;
    }

    private static readonly List<IPixelSaved> parts = new List<IPixelSaved>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => parts.Clear();   // (the systems register again in their Awake; Restart destroys them, which unregisters them)

    public static void Register(IPixelSaved p)
    {
        if (p == null || parts.Contains(p)) return;
        foreach (IPixelSaved other in parts)
            if (other != null && !(other is UnityEngine.Object o && o == null) && other.SaveId == p.SaveId)
                Debug.LogWarning("PixelSaveRegistry: two systems save under the id '" + p.SaveId + "' - only the first one's part will load.");
        parts.Add(p);
    }

    public static void Unregister(IPixelSaved p) => parts.Remove(p);

    /// <summary>Every registered system's part, in load order.</summary>
    public static Part[] ExportAll()
    {
        List<Part> list = new List<Part>();
        foreach (IPixelSaved p in Sorted())
        {
            try
            {
                string data = p.Export();
                if (data != null) list.Add(new Part { id = p.SaveId, data = data });
            }
            catch (Exception e) { Debug.LogError("PixelSaveRegistry: could not save '" + p.SaveId + "' - " + e.Message); }
        }
        return list.ToArray();
    }

    /// <summary>Loads the parts of the systems whose SaveOrder is in [fromOrder, toOrder). Systems with no part in the file get Import(null).</summary>
    public static void ImportRange(Part[] saved, int fromOrder, int toOrder)
    {
        foreach (IPixelSaved p in Sorted())
        {
            if (p.SaveOrder < fromOrder || p.SaveOrder >= toOrder) continue;
            string data = null;
            if (saved != null) foreach (Part part in saved) if (part != null && part.id == p.SaveId) { data = part.data; break; }
            try { p.Import(data); }
            catch (Exception e) { Debug.LogError("PixelSaveRegistry: could not load '" + p.SaveId + "' - " + e.Message); }
        }
    }

    private static List<IPixelSaved> Sorted()
    {
        parts.RemoveAll(p => p == null || (p is UnityEngine.Object o && o == null));   // (destroyed without unregistering)
        List<IPixelSaved> list = new List<IPixelSaved>(parts);
        list.Sort((a, b) => a.SaveOrder.CompareTo(b.SaveOrder));   // (List.Sort is not stable: give systems distinct orders when their order matters)
        return list;
    }

    // ---- small helpers for the parts ----

    /// <summary>A [Serializable] object as JSON (for Export).</summary>
    public static string ToJson(object o) => o != null ? JsonUtility.ToJson(o) : null;

    /// <summary>A part's JSON back into an object; null when there is no data or it can't be read (for Import).</summary>
    public static T FromJson<T>(string data) where T : class
    {
        if (string.IsNullOrEmpty(data)) return null;
        try { return JsonUtility.FromJson<T>(data); }
        catch (Exception e) { Debug.LogWarning("PixelSaveRegistry: could not read a saved " + typeof(T).Name + " - " + e.Message); return null; }
    }
}
