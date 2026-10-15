// ---- System notes (moved here from CLAUDE.md) ----
// PixelWindows.cs
//   CloseAllExcept(owner) closes every open window with priority < 100 (Crafting calls it when it opens, and closes itself when
//   AnyOpenExcept(this) turns true; the event log fades out whenever any registered window opens, see PixelHints.Update). Static
//   registry of closable windows (placing a device 100, shop upgrades window 40, shop 30, log 20, inventory 10). Escape closes the
//   topmost one; the pause menu only opens on Escape when none is open. New windows should PixelWindows.Register(this, priority,
//   isOpen, close) and Unregister(this) in OnDestroy. Entries whose owner was destroyed are dropped on every scene unload (Restart).
// ---------------------------------------------------
using System;
using System.Collections.Generic;

/// <summary>
/// Keeps track of the game's closable windows (inventory, log, shop...) so the Escape key can close them
/// one at a time - the topmost first - before the pause menu opens.
///
/// A window registers itself with an open-check and a close action. Higher priority closes first.
/// </summary>
public static class PixelWindows
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        entries.Clear();
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    // Restart reloads the scene: windows whose owner was destroyed without Unregister would stay listed, and an open-check that reads
    // a plain field of the dead owner (no exception) kept AnyOpen() true in the new game. Only destroyed owners are dropped.
    private static void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
    {
        entries.RemoveAll(e => e.owner is UnityEngine.Object o && o == null);
    }

    private class Entry
    {
        public object owner;
        public int priority;
        public Func<bool> isOpen;
        public Action close;
    }

    private static readonly List<Entry> entries = new List<Entry>();

    /// <summary>Registers a window. 'owner' is used to unregister it (usually the script that built it).</summary>
    public static void Register(object owner, int priority, Func<bool> isOpen, Action close)
    {
        entries.Add(new Entry { owner = owner, priority = priority, isOpen = isOpen, close = close });
    }

    /// <summary>Removes every window registered by this owner.</summary>
    public static void Unregister(object owner)
    {
        entries.RemoveAll(e => e.owner == owner);
    }

    /// <summary>
    /// Closes every open window except the ones registered by 'keep' (used by windows that open on their own, like Crafting).
    /// Windows with a priority of 100 or more (placing a device, dev tools, the title screen) are left alone.
    /// </summary>
    public static void CloseAllExcept(object keep)
    {
        foreach (Entry e in entries.ToArray())
            if (e.owner != keep && e.priority < 100 && IsOpen(e)) e.close();
    }

    /// <summary>True if any window other than the ones registered by 'except' is open (device placing, dev tools and the title screen don't count).</summary>
    public static bool AnyOpenExcept(object except)
    {
        foreach (Entry e in entries)
            if (e.owner != except && e.priority < 100 && IsOpen(e)) return true;
        return false;
    }

    /// <summary>True if any registered window is open.</summary>
    public static bool AnyOpen()
    {
        foreach (Entry e in entries)
            if (IsOpen(e)) return true;
        return false;
    }

    /// <summary>Closes the open window with the highest priority. Returns false if none was open.</summary>
    public static bool CloseTopmost()
    {
        Entry top = null;
        foreach (Entry e in entries)
            if (IsOpen(e) && (top == null || e.priority > top.priority)) top = e;

        if (top == null) return false;
        top.close();
        return true;
    }

    private static bool IsOpen(Entry e)
    {
        try { return e.isOpen(); }
        catch (Exception) { return false; } // the window was destroyed (e.g. a scene reload)
    }
}
