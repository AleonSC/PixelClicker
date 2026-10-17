// ---- System notes ----
// PixelHuskQuery.cs
//   ONE place to look at the husks lying on the field (PixelClicker.OldPixels) without every system repeating the same work:
//   - Get(body): the husk's OldPixelInfo / OldPixelDespawn, fetched ONCE per husk and cached (GetComponent every check was the old way). The cache prunes
//     itself when it holds far more entries than husks exist (checked by Near, and by Get every 256 new husks, so it also stays small when no zone
//     system calls Near), and is cleared on Restart.
//   - Husk.Tier / TypeOf / Fading (vanishing or already gone) / HeldByPlayer.
//   - Near(clicker, centre, radius, results, flat): every husk within a radius (flat = horizontal distance only), alive ones only unless asked.
//   - CountOfType(clicker, type): how many husks of a pixel type are lying out (e.g. Acorns for the Great Oak chance).
//   - NearestOnScreen(clicker, screen, marginPx, accept): the husk whose EDGE on the screen is nearest the pointer, within marginPx screen pixels
//     (its own on-screen size counts, so a far, small or moving husk is as easy to hit as a near one). Husk Crush uses it when the exact ray misses.
//   Used by the zone-style systems (Sap fields, Mold, the Great Oak boost, the Magnet, Nuclear radiation) and - since the 2026-10-17 device pass - every
//   device / container script (Recycler, Storage Crate, Bank hose, belts, sorters, the Sweeper, electric links, Quantum Organizer, Funnel, hoses...).
//   A card missing at the first look (a husk still being made) is looked for again. A NEW zone effect should use Near instead of its own loop.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class PixelHuskQuery
{
    /// <summary>One husk with its components (fetched once and cached).</summary>
    public struct Husk
    {
        public Rigidbody body;
        public OldPixelInfo info;
        public OldPixelDespawn despawn;

        public bool Valid => body != null;
        public int Tier => info != null ? info.tierIndex : -1;
        /// <summary>It is vanishing (or already destroyed): leave it alone.</summary>
        public bool Fading => body == null || (despawn != null && despawn.IsDespawning);
        public bool HeldByPlayer => info != null && info.heldByPlayer;
    }

    private static readonly Dictionary<int, Husk> cache = new Dictionary<int, Husk>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache.Clear(); PixelStatics.ResetOnRestart(OnRestart); }
    private static void OnRestart() { cache.Clear(); insertsSincePrune = 0; }

    private static int insertsSincePrune;
    private static readonly List<int> deadScratch = new List<int>();

    /// <summary>The husk's components (cached after the first look).</summary>
    public static Husk Get(Rigidbody body)
    {
        if (body == null) return default;
        int id = body.GetInstanceID();
        if (cache.TryGetValue(id, out Husk h) && h.body == body)
        {
            // A husk gets its cards while it is being made: a card that was missing at the first look is looked for again (found ones are kept).
            if (h.info == null || h.despawn == null)
            {
                if (h.info == null) h.info = body.GetComponent<OldPixelInfo>();
                if (h.despawn == null) h.despawn = body.GetComponent<OldPixelDespawn>();
                cache[id] = h;
            }
            return h;
        }
        h = new Husk { body = body, info = body.GetComponent<OldPixelInfo>(), despawn = body.GetComponent<OldPixelDespawn>() };
        cache[id] = h;
        if (++insertsSincePrune >= 256) { insertsSincePrune = 0; RemoveDead(); }   // (every new husk adds an entry: drop the ones of husks that are gone)
        return h;
    }

    /// <summary>The pixel type of a husk (false if it has none / is not a husk).</summary>
    public static bool TypeOf(PixelClicker clicker, Husk h, out PixelClicker.PixelType type)
    {
        type = default;
        if (clicker == null || !clicker.IsValidTierIndex(h.Tier)) return false;
        type = clicker.Tiers[h.Tier].type;
        return true;
    }

    /// <summary>Drops cached entries of husks that are gone (when the cache has grown well past the number of husks).</summary>
    private static void Prune(PixelClicker clicker)
    {
        int alive = clicker != null ? clicker.OldPixels.Count : 0;
        if (cache.Count < 64 || cache.Count < alive * 3) return;
        RemoveDead();
    }

    private static void RemoveDead()
    {
        deadScratch.Clear();
        foreach (KeyValuePair<int, Husk> kv in cache) if (kv.Value.body == null) deadScratch.Add(kv.Key);
        foreach (int k in deadScratch) cache.Remove(k);
        deadScratch.Clear();
    }

    /// <summary>
    /// Fills 'results' with the husks within 'radius' of 'centre' (flat = only the horizontal distance counts) and returns how many.
    /// Vanishing husks are skipped unless includeFading.
    /// </summary>
    public static int Near(PixelClicker clicker, Vector3 centre, float radius, List<Husk> results, bool flat = true, bool includeFading = false)
    {
        results.Clear();
        if (clicker == null) return 0;
        Prune(clicker);
        float r2 = radius * radius;
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody b = list[i];
            if (b == null) continue;
            Vector3 d = b.position - centre;
            if (flat) d.y = 0f;
            if (d.sqrMagnitude > r2) continue;
            Husk h = Get(b);
            if (!includeFading && h.Fading) continue;
            results.Add(h);
        }
        return results.Count;
    }

    /// <summary>
    /// The husk nearest the screen point, measured from the edge of its on-screen size (a husk under the point is at 0), if it is within
    /// marginPx screen pixels and 'accept' says yes; null when none is. A forgiving pick for small, far-away or moving husks.
    /// </summary>
    public static Rigidbody NearestOnScreen(PixelClicker clicker, Vector2 screen, float marginPx, System.Func<PixelClicker, Rigidbody, bool> accept)
    {
        Camera cam = clicker != null ? clicker.ViewCamera : null;
        if (cam == null) return null;
        Rigidbody best = null;
        float bestEdge = Mathf.Max(0f, marginPx);
        Vector3 up = cam.transform.up;
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody b = list[i];
            if (b == null) continue;
            Vector3 centre = cam.WorldToScreenPoint(b.position);
            if (centre.z <= 0f) continue;   // behind the camera
            float half = b.transform.lossyScale.x * 0.5f;
            float radius = (cam.WorldToScreenPoint(b.position + up * half) - centre).magnitude;   // its on-screen half size
            float edge = Vector2.Distance(screen, centre) - radius;
            if (edge > bestEdge || (accept != null && !accept(clicker, b))) continue;
            best = b;
            bestEdge = edge;
        }
        return best;
    }

    /// <summary>How many husks of this pixel type are lying out (vanishing ones too).</summary>
    public static int CountOfType(PixelClicker clicker, PixelClicker.PixelType type)
    {
        if (clicker == null) return 0;
        int count = 0;
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Husk h = Get(list[i]);
            if (TypeOf(clicker, h, out PixelClicker.PixelType t) && t == type) count++;
        }
        return count;
    }
}
