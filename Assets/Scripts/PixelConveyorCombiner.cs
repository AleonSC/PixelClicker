// ---- System notes ----
// PixelConveyorCombiner.cs
//   CONVEYOR COMBINER (2026-10-19, a Dev Hub > Tester feature 'Conveyor Combiner'): a box that sits ON a conveyor belt anywhere along it (snaps to the belt
//   under the mouse, like the Belt Sorter). Husks riding the belt pass straight through it. Each SIDE has an inlet: a belt that ENDS at an inlet (belts snap
//   there: SnapPoints, so the feeding belt comes in square to the main one) hands its husks in (IPixelAutoIntake.CatchNear, via PixelCouplings.HandIn) and they
//   are MERGED onto the main belt at the box (PixelConveyorSystem.TryMerge) as soon as there is a gap there - the two feeds become one. While there is no gap
//   they wait at the inlet (up to queueSize; kept alive), then the feeding belt's husks are thrown off its end as at any full device.
//   SHOP (2026-10-20): sold in Utilities (PixelConsumables DeviceKind.ConveyorCombiner, a PixelInstantItems tool: right-click starts this placing, one used up per box).
//   The spot is a distance along the belt (it follows the belt; the box goes when its belt goes). Saved with the tester save (position, turn).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelConveyorCombiner : MonoBehaviour, IPixelAutoIntake
{
    [Min(0)]
    [Tooltip("How many husks may wait at the inlets for a gap on the main belt (more = the feeding belt throws them off its end).")]
    [SerializeField] private int queueSize = 6;

    private static readonly List<PixelConveyorCombiner> all = new List<PixelConveyorCombiner>();
    public static IReadOnlyList<PixelConveyorCombiner> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 0.5f, beltWidth;
    private PixelConveyor belt;
    private float along;
    private readonly List<Rigidbody> waiting = new List<Rigidbody>();

    public bool IsGhost => ghost;

    /// <summary>Where the box sits along its belt, measured from the belt's start NOW (so a reversed belt still merges at the box).</summary>
    private float Spot => belt != null ? belt.Along(transform.position) : along;

    // ---------------- placing ----------------

    /// <summary>The model alone, for the shop / inventory pictures (no working parts: PixelConsumables.DisplayOnly strips them).</summary>
    public static GameObject CreateDisplay(PixelClicker c) => PixelConsumables.DisplayOnly(Build(c, false, 1.35f * Mathf.Max(0.05f, c.OldPixelWorldSize)));
    public static GameObject CreateGhost(PixelClicker c) => Build(c, true, 1.35f * Mathf.Max(0.05f, c.OldPixelWorldSize));

    /// <summary>Snaps the ghost to the belt under the mouse, turned the belt's way.</summary>
    public static bool TrySnap(PixelClicker c, Vector3 point, float yaw, GameObject ghostRoot, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        if (!PixelConveyor.NearestOn(c, point, out PixelConveyor belt, out float s, 1.6f)) return false;
        Vector3 p = belt.PointAt(s), d = belt.Direction;
        snappedPoint = p;
        snappedYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        if (ghostRoot != null) { PixelConveyorCombiner g = ghostRoot.GetComponent<PixelConveyorCombiner>(); if (g != null && !Mathf.Approximately(g.beltWidth, belt.Width)) g.Rebuild(belt.Width); }
        return true;
    }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        if (!PixelConveyor.NearestOn(c, point, out PixelConveyor belt, out float s, 1.6f)) { PixelHints.Announce("Click a spot on a conveyor belt for the combiner."); return null; }
        GameObject root = Build(c, false, belt.Width);
        PixelConveyorCombiner b = root.GetComponent<PixelConveyorCombiner>();
        b.belt = belt; b.along = s;
        Vector3 d = belt.Direction;
        root.transform.SetPositionAndRotation(belt.PointAt(s), Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f));
        PixelAudio.Play("device_place");
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghostMode, float width)
    {
        GameObject root = new GameObject(ghostMode ? "Conveyor Combiner (Ghost)" : "Conveyor Combiner");
        PixelConveyorCombiner b = root.AddComponent<PixelConveyorCombiner>();
        b.clicker = c; b.ghost = ghostMode; b.unit = Mathf.Max(0.05f, c.OldPixelWorldSize);
        b.Rebuild(width);
        return root;
    }

    private void Rebuild(float width)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) { GameObject old = transform.GetChild(i).gameObject; old.SetActive(false); Destroy(old); }
        beltWidth = width;
        float u = unit, half = width * 0.5f + 0.25f * u, h = 1.3f * u, len = 1.6f * u, t = 0.12f * u;
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color body = new Color(0.24f, 0.3f, 0.36f, 1f), steel = new Color(0.55f, 0.58f, 0.64f, 1f), dark = new Color(0.06f, 0.06f, 0.07f, 1f), accent = new Color(1f, 0.78f, 0.15f, 1f), merge = new Color(0.35f, 1f, 0.55f, 1f);
        // The hood over the belt: a roof and two side walls, open front and back (the main belt runs straight through).
        k.Box("Roof", transform, new Vector3(0f, h + t * 0.5f, 0f), new Vector3(half * 2f + 2f * t, t, len), body, default, false, 0.4f, 0.5f);
        k.Box("Roof Strip", transform, new Vector3(0f, h + t + 0.01f * u, 0f), new Vector3(half * 2f, 0.02f * u, len * 0.3f), accent);
        for (int s = -1; s <= 1; s += 2)
        {
            // Each side wall has an INLET: a dark opening low down with a little sloped lip, and a green arrow pointing in.
            float x = s * (half + t * 0.5f), holeH = 0.9f * u, holeW = 1.2f * u;
            k.Box("Wall Top", transform, new Vector3(x, (holeH + h) * 0.5f, 0f), new Vector3(t, h - holeH, len), body, default, false, 0.4f, 0.5f);
            for (int e = -1; e <= 1; e += 2)
                k.Box("Wall Side", transform, new Vector3(x, holeH * 0.5f, e * (holeW + len) * 0.25f), new Vector3(t, holeH, (len - holeW) * 0.5f), body, default, false, 0.4f, 0.5f);
            k.Box("Inlet Frame", transform, new Vector3(x + s * t * 0.6f, holeH, 0f), new Vector3(t * 0.4f, t * 0.6f, holeW + t), steel, default, false, 0.8f, 0.6f);
            k.Box("Inlet Lip", transform, new Vector3(s * (half + 0.45f * u), 0.12f * u, 0f), new Vector3(0.9f * u, 0.06f * u, holeW), dark, new Vector3(0f, 0f, s * 6f), false, 0.3f, 0.4f);
            for (int a = -1; a <= 1; a += 2)
                k.Box("Inlet Arrow", transform, new Vector3(x + s * t * 0.55f + s * 0.08f * u, h * 0.82f, a * 0.1f * u), new Vector3(0.02f * u, 0.07f * u, 0.32f * u), merge, new Vector3(0f, s * a * 35f, 0f), false, 0f, 0.6f, merge * 0.6f);
        }
        foreach (float z in new[] { -len * 0.5f, len * 0.5f })   // steel frames round the open ends
        {
            k.Box("End Frame", transform, new Vector3(0f, h, z), new Vector3(half * 2f + 2f * t, t * 0.8f, t * 0.6f), steel, default, false, 0.8f, 0.6f);
            for (int s = -1; s <= 1; s += 2)
                k.Box("End Post", transform, new Vector3(s * (half + t * 0.5f), h * 0.5f, z), new Vector3(t * 1.2f, h, t * 0.6f), steel, default, false, 0.8f, 0.6f);
        }
    }

    // ---------------- inlets ----------------

    /// <summary>The two inlet ports (world): just off each side at the belt's underside height - a feeding belt ENDS here.</summary>
    private Vector3 Inlet(int side) => transform.TransformPoint(new Vector3(side * (beltWidth * 0.5f + 1.1f * unit), 0f, 0f));

    /// <summary>Where belts snap (placing): both inlets.</summary>
    public void SnapPoints(System.Action<Vector3> visit) { if (!ghost) { visit(Inlet(-1)); visit(Inlet(1)); } }

    /// <summary>A husk handed in at an inlet (the end of a feeding belt): merged onto the main belt now, or it waits for a gap.</summary>
    public bool CatchNear(Vector3 at, Rigidbody husk)
    {
        if (ghost || belt == null || husk == null) return false;
        int side = 0;
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 d = at - Inlet(s);
            if (Mathf.Abs(d.y) <= 0.8f * unit && new Vector2(d.x, d.z).magnitude <= 0.9f * unit) side = s;
        }
        if (side == 0) return false;
        if (waiting.Count == 0 && PixelConveyorSystem.TryMerge(belt, husk, Spot)) return true;
        if (waiting.Count >= Mathf.Max(0, queueSize)) return false;
        husk.isKinematic = true;
        husk.position = transform.TransformPoint(new Vector3(side * (beltWidth * 0.5f + 0.45f * unit), 0.6f * unit, 0f));   // (waits on the inlet lip)
        waiting.Add(husk);
        return true;
    }

    // ---------------- every frame ----------------

    private void OnEnable() { PixelCouplings.RegisterAutoIntake(this); if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { PixelCouplings.UnregisterAutoIntake(this); all.Remove(this); }

    private void Awake() => PixelHuskPool.HuskReleasing += OnHuskReleasing;

    private void OnDestroy()
    {
        PixelHuskPool.HuskReleasing -= OnHuskReleasing;
        foreach (Rigidbody rb in waiting) if (rb != null) rb.isKinematic = false;   // (let them drop)
        waiting.Clear();
    }

    // (PixelHuskPool: the husk is going away and may be reused - take it out of the queue)
    private void OnHuskReleasing(Rigidbody body) => waiting.Remove(body);

    private void FixedUpdate()
    {
        if (ghost) return;
        if (belt == null)
        {
            // Its belt was split at a Belt Sorter's joint: carry on on the piece under it; a removed belt takes the box with it.
            if (PixelConveyor.NearestOn(clicker, transform.position, out PixelConveyor again, out float s2, 0.3f)) { belt = again; along = s2; }
            else { Destroy(gameObject); return; }
        }
        if (!Mathf.Approximately(beltWidth, belt.Width)) Rebuild(belt.Width);   // (its belt was made wider / narrower: the hood follows)
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = waiting[i];
            OldPixelInfo info = rb != null ? PixelHuskQuery.Get(rb).info : null;
            if (rb == null || (info != null && info.heldByPlayer)) { waiting.RemoveAt(i); continue; }   // (gone, or grabbed by the player)
            OldPixelDespawn life = PixelHuskQuery.Get(rb).despawn;
            if (life != null && !life.IsDespawning) life.KeepAlive();   // (waiting at the inlet never fades)
        }
        if (waiting.Count > 0 && waiting[0] != null && PixelConveyorSystem.TryMerge(belt, waiting[0], Spot)) waiting.RemoveAt(0);
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw; }

    public static string ExportState(GameObject root) => root == null ? null : JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y });

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        return s == null ? null : Place(c, s.position, s.yaw);
    }
}
