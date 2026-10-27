// ---- System notes ----
// PixelHuskTicker.cs
//   ONE loop for the per-husk helpers in PixelHusks.cs. ScaledGravity, OldPixelImpact (physics step), OldPixelPopIn and OldPixelDespawn (frame)
//   used to each have their own Update / FixedUpdate: with ~150 husks that was ~600 separate calls from the engine every frame. Now each one
//   registers itself here while it is enabled (OnEnable / OnDisable, so 'enabled = false' still pauses it, e.g. a held husk's gravity) and a single
//   hidden runner calls their Tick methods. Order between husks is the same as before (none promised).
//   Safe to change the lists while ticking: a helper removed mid-loop is skipped, one added mid-loop starts ticking next frame.
//   An error in one husk's Tick is logged and the loop carries on with the others (as when each had its own Update).
//   Run(coroutine): the same runner also runs short husk animations that must finish even if the device that started them is destroyed (the
//   containers' FlyIn, the Quantum Organizer's GlitchOut): a coroutine on the device would stop with it and leave the husk frozen in the scene.
//   The runner is made on first use (DontDestroyOnLoad, like the save-shot runner) - it is not a game system, so it is not in PixelSystems.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Something the husk ticker calls once per frame (Update) or once per physics step (FixedUpdate).</summary>
public interface IHuskTick
{
    void Tick();
}

public static class PixelHuskTicker
{
    /// <summary>A list that may be changed while it is being walked.</summary>
    private class TickList
    {
        private readonly List<IHuskTick> items = new List<IHuskTick>();
        private bool walking, holes;

        public int Count => items.Count;

        public void Add(IHuskTick t) { if (t != null) items.Add(t); }

        public void Remove(IHuskTick t)
        {
            int i = items.IndexOf(t);
            if (i < 0) return;
            if (walking) { items[i] = null; holes = true; }   // (filled in after the walk)
            else items.RemoveAt(i);
        }

        public void Walk()
        {
            walking = true;
            int n = items.Count;   // (ones added during the walk wait for the next one)
            for (int i = 0; i < n; i++)
            {
                IHuskTick t = items[i];
                if (t == null || (t is UnityEngine.Object o && o == null)) { items[i] = null; holes = true; continue; }   // (removed, or destroyed without OnDisable)
                try { t.Tick(); }
                catch (Exception e) { Debug.LogException(e, t as UnityEngine.Object); }
            }
            walking = false;
            if (holes) { items.RemoveAll(x => x == null || (x is UnityEngine.Object o && o == null)); holes = false; }
        }

        public void Clear() { items.Clear(); holes = false; walking = false; }
    }

    private static readonly TickList frame = new TickList(), physics = new TickList();
    private static Runner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { frame.Clear(); physics.Clear(); runner = null; }

    /// <summary>Husks ticked every frame / every physics step right now (for the dev tools).</summary>
    public static int FrameCount => frame.Count;
    public static int PhysicsCount => physics.Count;

    /// <summary>Call Tick once per frame (from OnEnable; pair with RemoveFrame in OnDisable).</summary>
    public static void AddFrame(IHuskTick t) { EnsureRunner(); frame.Add(t); }
    public static void RemoveFrame(IHuskTick t) => frame.Remove(t);

    /// <summary>Call Tick once per physics step (from OnEnable; pair with RemovePhysics in OnDisable).</summary>
    public static void AddPhysics(IHuskTick t) { EnsureRunner(); physics.Add(t); }
    public static void RemovePhysics(IHuskTick t) => physics.Remove(t);

    /// <summary>Runs a coroutine on the persistent runner (not on a device that may be destroyed first): the husk fly-in / glitch-out animations that
    /// must always finish and destroy their husk (PixelHuskHolder.FlyIn, the Quantum Organizer's GlitchOut).</summary>
    public static Coroutine Run(System.Collections.IEnumerator routine)
    {
        if (routine == null) return null;
        EnsureRunner();
        return runner.StartCoroutine(routine);
    }

    private static void EnsureRunner()
    {
        if (runner != null) return;
        GameObject go = new GameObject("Husk Ticker");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private void Update() => frame.Walk();
        private void FixedUpdate() => physics.Walk();
        private void OnDestroy() { if (runner == this) runner = null; }
    }
}
