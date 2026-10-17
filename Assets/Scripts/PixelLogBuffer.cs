// ---- System notes ----
// PixelLogBuffer.cs
//   ONE in-memory record of the game's log (every Debug.Log / warning / error / exception, from any thread) for the dev Console tab (PixelDevConsole):
//   the last Capacity entries with time, type, message and stack trace; the same message straight after itself is counted (x3) instead of added again.
//   Starts listening before the first scene loads, so errors from startup are there too. Not cleared by Settings > Restart (it is a dev record, not
//   game state). PixelCrashLog keeps its own short line list for the report files.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PixelLogBuffer
{
    public sealed class Entry
    {
        public int id, count = 1;
        public DateTime time;
        public LogType type;
        public string message, stack;
        public bool IsError => type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
    }

    public const int Capacity = 1000;

    private static readonly object gate = new object();
    private static readonly List<Entry> entries = new List<Entry>();
    private static int nextId, errors, warnings, logs, unseenErrors;
    private static volatile int version;
    private static bool listening;

    /// <summary>Goes up with every new log line (UI rebuilds only when it changed).</summary>
    public static int Version => version;
    public static int Errors => errors;
    public static int Warnings => warnings;
    public static int Logs => logs;
    /// <summary>Errors since the Console tab was last looked at (the on-screen error badge).</summary>
    public static int UnseenErrors => unseenErrors;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Application.logMessageReceivedThreaded -= OnLog;   // (with domain reload off the old subscription would still be there)
        listening = false;
        Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Listen()
    {
        if (listening) return;
        Application.logMessageReceivedThreaded += OnLog;
        listening = true;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        lock (gate)
        {
            Entry last = entries.Count > 0 ? entries[entries.Count - 1] : null;
            if (last != null && last.type == type && last.message == message && last.stack == stack)
            {
                last.count++;
                last.time = DateTime.Now;
            }
            else
            {
                entries.Add(new Entry { id = ++nextId, time = DateTime.Now, type = type, message = message ?? "", stack = stack ?? "" });
                if (entries.Count > Capacity) entries.RemoveRange(0, entries.Count - Capacity);
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; unseenErrors++; }
            else if (type == LogType.Warning) warnings++;
            else logs++;
            version++;
        }
    }

    /// <summary>Copies the current entries (oldest first) into 'into'.</summary>
    public static void Snapshot(List<Entry> into)
    {
        into.Clear();
        lock (gate) into.AddRange(entries);
    }

    public static void MarkSeen() { unseenErrors = 0; }

    public static void Clear()
    {
        lock (gate)
        {
            entries.Clear();
            errors = warnings = logs = unseenErrors = 0;
            version++;
        }
    }
}
