// ---- System notes ----
// PixelPathCarrier.cs
//   Shared helper for machines that CARRY old pixels along a path (users: the Funnel and the Conveyor Elevator; the belts have their own rider system). Add(rb) locks a loose old pixel (kinematic, no velocity); Step(...) each FixedUpdate draws new ones to the path's start (approachSeconds), then moves every
//   pixel along the polyline at 'speed' keeping 'gap' behind the one ahead, keeps them from despawning, lets go of ones that were grabbed or destroyed, and calls onEnd(rb) when one reaches
//   the end (optional canExit gate: while it returns false the pixel waits at the end and the ones behind queue up) (the caller then puts it back into the world with ReleaseAt). ReleaseAll() lets everything go (the machine was removed).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PixelPathCarrier
{
    private sealed class Item { public Rigidbody rb; public Vector3 from; public float approach, d; }

    private static readonly HashSet<Rigidbody> carriedAll = new HashSet<Rigidbody>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { carriedAll.Clear(); carriers.Clear(); listening = false; }

    // Every carrier made (weakly held: a machine that is removed just drops its carrier), so one listener can take a husk out of all of them.
    private static readonly List<WeakReference<PixelPathCarrier>> carriers = new List<WeakReference<PixelPathCarrier>>();
    private static bool listening;

    public PixelPathCarrier()
    {
        carriers.Add(new WeakReference<PixelPathCarrier>(this));
        if (!listening) { listening = true; PixelHuskPool.HuskReleasing += OnHuskReleasing; }
    }

    // (PixelHuskPool: the husk is going away and may be reused - every carrier lets go of it, or a tube would carry the next husk made from its object)
    private static void OnHuskReleasing(Rigidbody body)
    {
        if (body == null) return;
        carriedAll.Remove(body);
        for (int i = carriers.Count - 1; i >= 0; i--)
        {
            if (!carriers[i].TryGetTarget(out PixelPathCarrier c)) { carriers.RemoveAt(i); continue; }
            if (!c.set.Remove(body)) continue;
            for (int k = c.items.Count - 1; k >= 0; k--) if (c.items[k].rb == body) c.items.RemoveAt(k);
        }
    }

    /// <summary>Is this pixel inside any carrier (a Funnel tube, an Elevator shaft, a Splitter, a Container Hose)? Such pixels cannot be grabbed with the mouse.</summary>
    /// <summary>Marks a pixel as held by some machine (it cannot be grabbed with the mouse) without it being on a path (the Funnel's pile). Unmark when it is let go.</summary>
    public static void MarkCarried(Rigidbody rb) { if (rb != null) carriedAll.Add(rb); }
    public static void Unmark(Rigidbody rb) { if (rb != null) carriedAll.Remove(rb); }

    public static bool IsCarried(Rigidbody rb) => rb != null && carriedAll.Contains(rb);

    private readonly List<Item> items = new List<Item>();
    private readonly HashSet<Rigidbody> set = new HashSet<Rigidbody>();

    public int Count => items.Count;
    public bool Contains(Rigidbody rb) => rb != null && set.Contains(rb);

    public static void SetVelocity(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
        rb.angularVelocity = Vector3.zero;
    }

    public static float[] Cumulative(Vector3[] path)
    {
        float[] cum = new float[path.Length];
        for (int i = 1; i < path.Length; i++) cum[i] = cum[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        return cum;
    }

    public static Vector3 PointAt(Vector3[] path, float[] cum, float d)
    {
        d = Mathf.Clamp(d, 0f, cum[cum.Length - 1]);
        for (int i = 1; i < path.Length; i++)
            if (cum[i] >= d) return Vector3.Lerp(path[i - 1], path[i], (d - cum[i - 1]) / Mathf.Max(0.0001f, cum[i] - cum[i - 1]));
        return path[path.Length - 1];
    }

    /// <summary>Locks the pixel (it is drawn to the path's start by the next Step calls).</summary>
    /// <summary>Locks the pixel. startD >= 0: it is already on the path at that distance (no approach to the start first).</summary>
    public void Add(Rigidbody rb, float startD = -1f)
    {
        if (rb == null || !set.Add(rb)) return;
        carriedAll.Add(rb);
        SetVelocity(rb, Vector3.zero);
        rb.isKinematic = true;
        items.Add(startD >= 0f ? new Item { rb = rb, from = rb.position, approach = 1f, d = startD } : new Item { rb = rb, from = rb.position });
    }

    public void Step(Vector3[] path, float[] cum, float speed, float gap, float dt, Action<Rigidbody> onEnd, float approachSeconds = 0.25f, Func<bool> canExit = null)
    {
        float len = cum[cum.Length - 1];
        for (int i = items.Count - 1; i >= 0; i--)
        {
            Item c = items[i];
            OldPixelInfo info = c.rb != null ? c.rb.GetComponent<OldPixelInfo>() : null;
            if (c.rb == null || (info != null && info.heldByPlayer))   // gone, or grabbed (the grab's business now)
            {
                if (c.rb != null) { set.Remove(c.rb); carriedAll.Remove(c.rb); }
                items.RemoveAt(i);
                continue;
            }
            OldPixelDespawn despawn = c.rb.GetComponent<OldPixelDespawn>();
            if (despawn != null) despawn.KeepAlive();

            if (c.approach < 1f)
            {
                c.approach = Mathf.Min(1f, c.approach + dt / Mathf.Max(0.01f, approachSeconds));
                float e = c.approach * c.approach * (3f - 2f * c.approach);
                c.rb.MovePosition(Vector3.Lerp(c.from, path[0], e));
                continue;
            }
            float limit = len;
            foreach (Item o in items) if (o != c && o.approach >= 1f && o.d > c.d) limit = Mathf.Min(limit, o.d - gap);
            c.d = Mathf.Max(c.d, Mathf.Min(c.d + speed * dt, limit));
            if (c.d >= len - 0.001f && canExit != null && !canExit())
            {
                c.d = len - 0.002f;   // the way out is blocked: wait at the end (the ones behind queue up)
                c.rb.MovePosition(Vector3.Lerp(c.rb.position, PointAt(path, cum, c.d), 0.6f));
                continue;
            }
            if (c.d >= len - 0.001f)
            {
                Rigidbody rb = c.rb;
                set.Remove(rb);
                carriedAll.Remove(rb);
                items.RemoveAt(i);
                rb.isKinematic = false;
                onEnd?.Invoke(rb);
                continue;
            }
            c.rb.MovePosition(Vector3.Lerp(c.rb.position, PointAt(path, cum, c.d), 0.6f));
        }
    }

    public void ReleaseAll()
    {
        foreach (Item c in items) if (c.rb != null) { c.rb.isKinematic = false; carriedAll.Remove(c.rb); }
        items.Clear();
        set.Clear();
    }
}
