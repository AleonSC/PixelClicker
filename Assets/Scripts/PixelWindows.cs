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
