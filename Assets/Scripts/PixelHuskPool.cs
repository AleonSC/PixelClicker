// ---- System notes ----
// PixelHuskPool.cs
//   HUSK POOLING: a husk whose despawn finishes (OldPixelDespawn's swell-and-shrink end) is switched off and kept, and the next husk that drops
//   reuses it instead of a new GameObject + Rigidbody + MeshFilter + MeshRenderer. Switch: PixelClicker 'poolHusks' (+ 'huskPoolSize').
//   THE RULES that keep a reused husk from coming back into someone's list as a different block:
//   - Release(go) fires HuskReleasing(body) FIRST. Every system that keeps references to loose husks across frames (links, zone effects,
//     magnets, belts, grab, joints, caches...) listens and drops the body there and then. Pooling or not, the event always fires, so a
//     holder cannot tell the difference. A NEW system that keeps husk references must listen too (or only keep husks it claimed).
//   - Only husks nobody CLAIMED are pooled. ReleaseOldPixel / ClaimOldPixel / AbsorbOldPixel mark the husk (OldPixelInfo.claimed): its new
//     owner (a device, the Bank, a minigame) destroys it the normal way. Husks taken off the list by the floor cap are not claimed.
//   - On release the husk is switched off and every component except Transform / Rigidbody / MeshFilter / MeshRenderer and every child
//     object is removed at once (DestroyImmediate, so GetComponent never finds the old ones and their OnDestroy cleanup - a gravity well's
//     ring, a mold shell's material - runs now, not when it is reused); the despawn that called Release goes at the end of the frame.
//     On reuse (Take) it is cleaned again (anything left over), the collider is made new by the spawn code (that also clears any
//     Physics.IgnoreCollision pairs of the old collider) and the rigidbody / renderer are put back to a fresh state.
//   Restart: the pooled objects go with the scene; the list drops them (and is cleared by PixelStatics).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PixelHuskPool
{
    /// <summary>A loose husk is about to go (pooled or destroyed): drop every reference to it NOW. Fires before it is switched off.</summary>
    public static event Action<Rigidbody> HuskReleasing;

    /// <summary>Pool husks at all (PixelClicker 'poolHusks').</summary>
    public static bool Enabled { get; set; }
    /// <summary>The most switched-off husks kept for reuse (PixelClicker 'huskPoolSize').</summary>
    public static int MaxPooled { get; set; } = 64;

    private static readonly List<GameObject> pooled = new List<GameObject>();
    private static Transform root;
    private static readonly List<Component> scratch = new List<Component>();

    /// <summary>How many switched-off husks wait for reuse (dev tools).</summary>
    public static int PooledCount => pooled.Count;
    /// <summary>Husks reused / made new since the start (dev tools: how much the pool saves).</summary>
    public static int Reused { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pooled.Clear(); root = null; Reused = 0; HuskReleasing = null;
        PixelStatics.ResetOnRestart(OnRestart);
    }

    // (Restart reloads the scene: the pooled objects go with it. The event's listeners unsubscribe themselves in OnDisable.)
    private static void OnRestart() { pooled.Clear(); root = null; }

    /// <summary>Tells every listener this husk is going (without pooling it): for code that destroys a loose husk itself.</summary>
    public static void Announce(Rigidbody body)
    {
        if (body == null || HuskReleasing == null) return;
        foreach (Delegate d in HuskReleasing.GetInvocationList())
        {
            try { ((Action<Rigidbody>)d)(body); }
            catch (Exception e) { Debug.LogException(e); }   // (one broken listener must not stop the others from letting go)
        }
    }

    /// <summary>A husk's life ended (its despawn finished): announce it, take it off the field, then pool it (or destroy it).</summary>
    public static void Release(GameObject go)
    {
        if (go == null) return;
        Rigidbody body = go.GetComponent<Rigidbody>();
        OldPixelInfo info = go.GetComponent<OldPixelInfo>();
        Announce(body);
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker != null && body != null) clicker.ForgetOldPixel(body);
        PixelHuskQuery.Forget(body);

        bool poolable = Enabled && body != null && info != null && !info.claimed && pooled.Count < Mathf.Max(0, MaxPooled);
        if (!poolable) { UnityEngine.Object.Destroy(go); return; }
        go.SetActive(false);   // (its helpers leave the ticker in OnDisable)
        Strip(go, true);
        go.transform.SetParent(Root(), false);
        pooled.Add(go);
    }

    /// <summary>A clean, active husk object ready to be set up as a new husk (only Transform, Rigidbody, MeshFilter, MeshRenderer left), or null when the pool is empty.</summary>
    public static GameObject Take()
    {
        if (!Enabled) return null;
        while (pooled.Count > 0)
        {
            GameObject go = pooled[pooled.Count - 1];
            pooled.RemoveAt(pooled.Count - 1);
            if (go == null) continue;
            Clean(go);
            go.SetActive(true);
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                rb.angularVelocity = Vector3.zero;
            }
            Reused++;
            return go;
        }
        return null;
    }

    private static Transform Root()
    {
        if (root == null)
        {
            GameObject r = new GameObject("Husk Pool");
            r.SetActive(false);   // (keeps the pooled husks switched off whatever happens to them)
            root = r.transform;
        }
        return root;
    }

    /// <summary>Back to a bare object: no extra components or children, a fresh rigidbody and renderer.</summary>
    /// <summary>
    /// Removes every child object and every component except Transform / Rigidbody / MeshFilter / MeshRenderer.
    /// 'releasing' = called from inside the husk's own OldPixelDespawn (which is still running): that one goes with a normal Destroy.
    /// </summary>
    private static void Strip(GameObject go, bool releasing)
    {
        Transform t = go.transform;
        for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(t.GetChild(i).gameObject);
        scratch.Clear();
        go.GetComponents(scratch);
        // Joints first (they need the rigidbody), then everything else that isn't one of the four kept parts.
        foreach (Component c in scratch) if (c is Joint) UnityEngine.Object.DestroyImmediate(c);
        foreach (Component c in scratch)
        {
            if (c == null || c is Transform || c is Rigidbody || c is MeshFilter || c is MeshRenderer) continue;
            if (releasing && c is OldPixelDespawn) UnityEngine.Object.Destroy(c);
            else UnityEngine.Object.DestroyImmediate(c);
        }
        scratch.Clear();
    }

    private static void Clean(GameObject go)
    {
        Strip(go, false);
        Transform t = go.transform;

        t.SetParent(null, false);
        t.localScale = Vector3.one;
        t.rotation = Quaternion.identity;
        go.name = "OldPixel";
        go.layer = 0;

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.mass = 1f;
            rb.sleepThreshold = Physics.sleepThreshold;
            rb.maxAngularVelocity = 7f;
            rb.maxDepenetrationVelocity = Physics.defaultMaxDepenetrationVelocity;   // (Pixel Grabbing changes it)
            rb.ResetCenterOfMass();
            rb.ResetInertiaTensor();
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
#else
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
#endif
        }
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.SetPropertyBlock(null);
            mr.sharedMaterials = new Material[0];
            mr.enabled = true;
            mr.forceRenderingOff = false;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = null;
    }
}
