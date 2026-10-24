// ---- System notes ----
// PixelBystanders.cs
//   ONE place for the 'bystander' husks of a takeover round (Snake, Sorting Race, Breakout; 2026-10-24: was a copied block in each):
//   the husks on the floor that are NOT part of the round shrink away while it runs and grow back afterwards, so every husk you can
//   still see is one the round uses. A plain helper object (not a component): a minigame keeps one in a field.
//   - Shrink(clicker, skip): freezes every husk on the floor except the ones in 'skip' (the round's own), flying ones and ones that are
//     despawning or held: stopped, kinematic, colliders and the gravity-well / float behaviours off.
//   - Tick(roundRunning, seconds): call every frame. Scales them towards 0 while the round runs and back to full size after it
//     ('seconds' for the whole way); once they are full size again they get their physics back and the list empties.
//   - Restore(): puts them back at once (OnDestroy).
//   - Forget(body): drop one (PixelHuskPool.HuskReleasing: the husk is going away and may be reused).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelBystanders
{
    private class Bystander { public Rigidbody body; public Transform tf; public Vector3 baseScale; public Collider[] colliders; public Behaviour[] paused; }

    private readonly List<Bystander> list = new List<Bystander>();
    private float factor = 1f;   // 1 = full size, 0 = shrunk away

    /// <summary>Freezes and starts shrinking every husk on the floor that is not in 'skip' (null = none skipped).</summary>
    public void Shrink(PixelClicker clicker, HashSet<Rigidbody> skip)
    {
        list.Clear();
        factor = 1f;
        if (clicker == null) return;
        var all = clicker.OldPixels;
        for (int i = 0; i < all.Count; i++)
        {
            Rigidbody rb = all[i];
            if (rb == null || (skip != null && skip.Contains(rb)) || clicker.IsFlyingPixel(rb)) continue;
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null && (d.IsDespawning || d.Held)) continue;
            list.Add(new Bystander
            {
                body = rb, tf = rb.transform, baseScale = rb.transform.localScale,
                colliders = rb.GetComponents<Collider>(),
                paused = new Behaviour[] { rb.GetComponent<OldPixelGravityWell>(), rb.GetComponent<OldPixelFloat>() },
            });
        }
        foreach (Bystander b in list)
        {
            PixelBody.Steer(b.body, Vector3.zero, true);
            b.body.isKinematic = true;
            foreach (Collider c in b.colliders) if (c != null) c.enabled = false;
            foreach (Behaviour x in b.paused) if (x != null) x.enabled = false;
        }
    }

    /// <summary>Every frame: shrink while the round runs, grow back after it (over 'seconds'), then give the husks their physics back.</summary>
    public void Tick(bool roundRunning, float seconds)
    {
        if (list.Count == 0) return;
        factor = Mathf.MoveTowards(factor, roundRunning ? 0f : 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, seconds));
        Apply(!roundRunning && factor >= 1f);
    }

    /// <summary>Puts every bystander back at full size with its physics at once.</summary>
    public void Restore()
    {
        factor = 1f;
        Apply(true);
    }

    /// <summary>Drops one husk from the list (it is going away).</summary>
    public void Forget(Rigidbody body)
    {
        for (int i = list.Count - 1; i >= 0; i--) if (list[i].body == body) list.RemoveAt(i);
    }

    private void Apply(bool done)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Bystander b = list[i];
            if (b.body == null || b.tf == null) { list.RemoveAt(i); continue; }
            b.tf.localScale = b.baseScale * Mathf.Max(0.001f, factor);
            if (done)
            {
                foreach (Collider c in b.colliders) if (c != null) c.enabled = true;
                foreach (Behaviour x in b.paused) if (x != null) x.enabled = true;
                b.body.isKinematic = false;
            }
        }
        if (done) list.Clear();
    }
}
