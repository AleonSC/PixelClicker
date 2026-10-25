// ---- System notes ----
// PixelBeltSorter.cs
//   BELT SORTER (a Dev Hub > Tester feature, PixelFeatureTester 'Belt Sorter'): a low gantry that straddles a conveyor belt anywhere along it and kicks every pixel of ITS TYPE off the side. Placing: the ghost snaps to the belt under the mouse (TrySnap) and the
//   chute is on whichever side of the belt the mouse is (the snapped yaw is turned round for the left side, the chute is always the model's +X). RIGHT CLICK (a quick tap; holding deletes it) turns the sorter round: it swaps to the other side of the belt
//   and its chute / output swaps with it. FILTER SLOT: a tray on top of the beam holds ONE block, shown spinning above it (like a Storage Crate section): let go of a pixel you are dragging over the tray (or throw / drop one onto it: PixelGrab's 'recently held' tag) and it
//   goes into the slot (a block already there is handed back); the slot's type is the filter. LEFT CLICK the shown block to take it out again (it jumps into your hand; PixelBeltSorter.ClaimsPointer makes PixelGrab step aside). With an empty slot the sorter lets everything
//   pass. While a pixel of the filter type rides under the beam, PixelConveyorSystem asks PixelBeltSorter.TryDivert (called from its rider loop): the pixel leaves the belt (a paddle sweeps across), and is thrown out of the side - or put onto a belt that STARTS at the chute's
//   mouth through its built-in bare 'chute' socket (PixelCouplings.Emit, within ~1.3 cubes), so a second belt can be fed from the side. Other types are untouched.
//   AUTO CHUTE (2026-10-20): the chute is only BUILT while something takes husks beside the sorter - a belt starting there, a machine's In coupling, an
//   elevator's base (Connected, checked twice a second, RefreshChute); the sorter turns round to face it. Nothing beside it = no chute, kicked husks fall
//   to the floor. JOINT (2026-10-20): placed in the middle of a belt, it SPLITS the belt there (PixelConveyor.SplitAt) and sits at the start of the second
//   piece; its joint cuts the line into sections for the Control Levers once a line has two or more sorters (JointsOn, PixelConveyor.CollectSection).
//   A sorter whose belt was split (another sorter's joint) carries on on the piece under it. REMOVING a sorter (hold right, or the tester's Clear) HEALS its
//   joint (HealJoint -> PixelConveyor.JoinAt): the two pieces become one belt again when they run straight on with the same speed / size and no other
//   sorter stands there. SECTIONS: one sorter is enough to cut a line in two when both of the line's ends are connected (a coupling, an elevator). Its spot is measured along the belt from where the belt
//   starts NOW (Spot, like the Combiner), so reversing the belt leaves it where it stands; saved with the tester save (spot, yaw, slot type + amount).
//   A removed belt takes the sorter with it: the sorter goes back into the backpack (refunded) and its filter block is handed back (LoseBelt).
//   It catches a rider anywhere on the stretch the rider crosses this physics step, so fast belts can't skip it (TryDivert). When the belt fed by its
//   chute is full, a husk of its type rides on along the main belt instead of being thrown on the floor.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelBeltSorter : MonoBehaviour, IPixelSocketDevice
{
    [Min(0f)]
    [Tooltip("How hard a kicked pixel is thrown out of the side, in cube widths per second. 0 = the coded default (3).")]
    [SerializeField] private float kickSpeedCubes = 0f;

    [Header("Texts")]
    [Tooltip("Event log line when the sorter's belt is removed and the sorter goes back into your backpack.")] [SerializeField] private string beltLostText = "Its belt was removed: the Belt Sorter went back into your backpack.";

    private float Kick => kickSpeedCubes > 0f ? kickSpeedCubes : 3f;

    private static readonly List<PixelBeltSorter> all = new List<PixelBeltSorter>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 1f, beltWidth;
    private PixelConveyor belt;
    private float along;

    /// <summary>Where the sorter stands along its belt, measured from the belt's start NOW (so a reversed belt doesn't move it).</summary>
    private float Spot => belt != null ? Mathf.Clamp(belt.Along(transform.position), 0f, belt.Length) : along;
    private Vector3 sideDir = Vector3.right;
    private bool hasFilter;
    private PixelClicker.PixelType filter;
    private double slotAmount;
    private Transform slotModel;
    private float beamTopY;
    private readonly PixelRightTap flipTap = new PixelRightTap();
    private Transform paddle;
    private float kickT = -1f;
    private int placedFrame;

    // ---------------- placing ----------------

    /// <summary>The model alone, for the shop / inventory pictures (no working parts: PixelConsumables.DisplayOnly strips them).</summary>
    public static GameObject CreateDisplay(PixelClicker c) => PixelConsumables.DisplayOnly(Build(c, false, "Belt Sorter (Display)", 1.35f * Mathf.Max(0.05f, c.OldPixelWorldSize)));
    public static GameObject CreateGhost(PixelClicker c) { return Build(c, true, "Belt Sorter (Ghost)", 1.35f * Mathf.Max(0.05f, c.OldPixelWorldSize)); }

    public static bool TrySnap(PixelClicker c, Vector3 point, float yaw, GameObject ghostRoot, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        if (!PixelConveyor.NearestOn(c, point, out PixelConveyor belt, out float s, 1.6f)) return false;
        Vector3 p = belt.PointAt(s), d = belt.Direction, right = new Vector3(d.z, 0f, -d.x);
        snappedPoint = new Vector3(p.x, p.y + belt.SurfaceHeight, p.z);
        float lateral = Vector3.Dot(new Vector3(point.x - p.x, 0f, point.z - p.z), right);
        float beltYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        snappedYaw = lateral < 0f ? beltYaw + 180f : beltYaw;   // (the chute is the model's +X: turned round it points to the belt's left)
        if (ghostRoot != null) { PixelBeltSorter g = ghostRoot.GetComponent<PixelBeltSorter>(); if (g != null && !Mathf.Approximately(g.beltWidth, belt.Width)) g.Rebuild(belt.Width); }
        return true;
    }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw) { return PlaceWith(c, point, yaw, false, default, 0d, true); }

    private static GameObject PlaceWith(PixelClicker c, Vector3 point, float yaw, bool withFilter, PixelClicker.PixelType type, double amount, bool makeJoint)
    {
        if (!PixelConveyor.NearestOn(c, point, out PixelConveyor belt, out float s, 1.6f)) { PixelHints.Announce(PixelDialogue.File.Text("Device placing", "belt sorter spot", "Click a spot on a conveyor belt for the sorter.")); return null; }
        if (makeJoint)
        {
            // A JOINT: placed mid-belt, the belt is split in two right here and the sorter sits at the start of the second piece - the line's power
            // can be switched section by section between sorters (PixelConveyor.CollectSection, Control Levers).
            PixelConveyor second = belt.SplitAt(s);
            if (second != null) { belt = second; s = JointAlong * Mathf.Max(0.05f, c.OldPixelWorldSize); }
        }
        GameObject root = Build(c, false, "Belt Sorter", belt.Width);
        PixelBeltSorter b = root.GetComponent<PixelBeltSorter>();
        b.belt = belt; b.along = s;
        b.placedFrame = Time.frameCount;
        Vector3 p = belt.PointAt(s);
        root.transform.SetPositionAndRotation(new Vector3(p.x, p.y + belt.SurfaceHeight, p.z), Quaternion.Euler(0f, yaw, 0f));
        b.sideDir = root.transform.right;
        // Socket (PixelSockets.cs, built in and bare): the CHUTE's mouth, an Out socket on the model's +X side (it turns with the sorter when it swaps sides).
        b.sockets = new[] { new PixelSocket("chute", new Vector3(belt.Width * 0.5f + 1.0f * b.unit, 0f, 0f), Vector3.right, PixelSocketFlow.Output, true) { bare = true } };
        PixelCoupling.FitBuiltIn(b);
        if (withFilter) b.SetFilter(c.IndexOf(type), amount);
        b.RefreshChute(true);
        PixelAudio.Play("device_place");
        return root;
    }

    private const float JointAlong = 0.05f;   // (cube widths: a sorter on a joint sits just past the start of the second piece)

    // ---------------- the chute: it forms towards whatever takes husks beside it ----------------

    private GameObject[] chuteParts = new GameObject[0];
    private float nextChuteCheck;

    /// <summary>The chute's mouth on one side of the belt (world): +1 = the model's +X (where the chute and its socket are), -1 = the other side.</summary>
    private Vector3 Mouth(int side) => transform.TransformPoint(new Vector3(side * (beltWidth * 0.5f + 1.0f * unit), 0f, 0f));

    /// <summary>Does something take husks at this side's mouth: a belt STARTING there (not the sorter's own belt), a machine's In coupling, an elevator's base?</summary>
    private bool Connected(int side)
    {
        Vector3 m = Mouth(side), facing = transform.right * side;
        foreach (PixelConveyor c in PixelConveyor.All)
        {
            if (c == null || c.IsGhost || c == belt) continue;
            Vector3 d = c.StartPoint - m;
            if (new Vector3(d.x, 0f, d.z).magnitude <= 1.3f * unit && Mathf.Abs(d.y) <= 1.2f * unit && Vector3.Dot(c.Direction, facing) > 0.3f) return true;
        }
        if (PixelCouplings.IntakeAt(m + facing * 0.5f * unit, unit, this) != null) return true;
        return PixelElevator.AnyBaseNear(m);
    }

    /// <summary>
    /// The chute only exists while something is connected beside the sorter: on the connected side (the sorter turns round to face it, unless the
    /// other side is connected too); with nothing there it has no chute and kicks husks off to the floor.
    /// </summary>
    private void RefreshChute(bool force = false)
    {
        if (ghost || (!force && Time.time < nextChuteCheck)) return;
        nextChuteCheck = Time.time + 0.5f;
        bool here = Connected(1), there = !here && Connected(-1);
        if (there) { Flip(); here = true; }
        foreach (GameObject g in chuteParts) if (g != null && g.activeSelf != here) g.SetActive(here);
    }

    private static GameObject Build(PixelClicker c, bool ghostMode, string name, float width)
    {
        GameObject root = new GameObject(name);
        PixelBeltSorter b = root.AddComponent<PixelBeltSorter>();
        b.clicker = c; b.ghost = ghostMode; b.unit = Mathf.Max(0.05f, c.OldPixelWorldSize);
        b.Rebuild(width);
        return root;
    }

    private void Rebuild(float width)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) { GameObject old = transform.GetChild(i).gameObject; old.SetActive(false); Destroy(old); }
        beltWidth = width;
        float u = unit, half = width * 0.5f, post = 0.18f * u, beamY = 1.5f * u;
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color steel = new Color(0.55f, 0.58f, 0.64f, 1f), dark = new Color(0.2f, 0.23f, 0.3f, 1f), accent = new Color(1f, 0.78f, 0.15f, 1f), teal = new Color(0.2f, 0.85f, 0.8f, 1f);
        // The gantry: a post each side of the belt (outside the rails), a beam across the top, a hazard strip along the beam.
        foreach (float sx in new[] { -1f, 1f })
            k.Box("Post", transform, new Vector3(sx * (half + 0.2f * u), beamY * 0.5f, 0f), new Vector3(post, beamY, post * 1.6f), steel, default, false, 0.7f, 0.5f);
        k.Box("Beam", transform, new Vector3(0f, beamY, 0f), new Vector3(width + 0.6f * u, 0.24f * u, 0.4f * u), dark, default, true, 0.4f, 0.5f);   // (the one collider: click it to learn)
        k.Box("Strip", transform, new Vector3(0f, beamY + 0.13f * u, 0f), new Vector3(width + 0.5f * u, 0.03f * u, 0.16f * u), accent);
        // The paddle that sweeps pixels across (hangs from the beam), and the chute on the +X side.
        paddle = k.Box("Paddle", transform, new Vector3(-half + 0.1f * u, 0.55f * u, 0f), new Vector3(0.1f * u, 1.0f * u, 0.5f * u), teal, default, false, 0.3f, 0.6f).transform;
        chuteParts = new[]
        {
            k.Box("Chute", transform, new Vector3(half + 0.55f * u, 0.12f * u, 0f), new Vector3(1.1f * u, 0.07f * u, 0.9f * u), dark, new Vector3(0f, 0f, -8f), false, 0.4f, 0.4f),
            k.Box("Chute Wall F", transform, new Vector3(half + 0.55f * u, 0.3f * u, 0.46f * u), new Vector3(1.1f * u, 0.3f * u, 0.07f * u), steel, default, false, 0.7f, 0.5f),
            k.Box("Chute Wall B", transform, new Vector3(half + 0.55f * u, 0.3f * u, -0.46f * u), new Vector3(1.1f * u, 0.3f * u, 0.07f * u), steel, default, false, 0.7f, 0.5f),
        };
        if (!ghost) foreach (GameObject g in chuteParts) g.SetActive(false);   // (no chute until something is connected beside it; the placing ghost shows where it would go)
        // The filter slot: a tray on top of the beam (a block placed in it is shown spinning above the tray).
        beamTopY = beamY + 0.13f * u;
        k.Box("Tray", transform, new Vector3(0f, beamTopY + 0.04f * u, 0f), new Vector3(0.8f * u, 0.07f * u, 0.8f * u), dark, default, false, 0.4f, 0.5f);
        k.Box("Tray Rim", transform, new Vector3(0f, beamTopY + 0.085f * u, 0f), new Vector3(0.86f * u, 0.02f * u, 0.86f * u), teal);
        slotModel = null;
        if (!ghost && hasFilter) MakeModel();
    }

    // ---------------- behaviour ----------------

    private void Awake() => PixelHuskPool.HuskReleasing += OnHuskReleasing;
    private void OnDestroy() => PixelHuskPool.HuskReleasing -= OnHuskReleasing;
    // (PixelHuskPool: the husk is going away and may be reused - let go of it)
    private void OnHuskReleasing(Rigidbody body) => ignoreUntil.Remove(body);

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); PixelCouplings.Register(this); }
    private void OnDisable() { all.Remove(this); PixelCouplings.Unregister(this); }

    // ---- Socket ----
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => new Color(0.55f, 0.58f, 0.64f, 1f);
    public bool AcceptHusk(PixelSocket socket, Rigidbody husk) => false;   // (output only)

    private Vector3 SlotWorld() => transform.TransformPoint(new Vector3(0f, beamTopY + 0.6f * unit, 0f));

    private void MakeModel()
    {
        int idx = clicker.IndexOf(filter);
        if (idx < 0) return;
        GameObject model = clicker.CreateDisplayPixel(idx, transform, 0.62f * unit);
        if (model == null) return;
        model.transform.localPosition = new Vector3(0f, beamTopY + 0.6f * unit, 0f);
        slotModel = model.transform;
    }

    /// <summary>Puts a block in the slot (tier index; -1 clears it).</summary>
    private void SetFilter(int tierIndex, double amount)
    {
        if (slotModel != null) Destroy(slotModel.gameObject);
        slotModel = null;
        hasFilter = clicker.IsValidTierIndex(tierIndex);
        slotAmount = hasFilter ? amount : 0d;
        if (hasFilter) { filter = clicker.Tiers[tierIndex].type; MakeModel(); }
    }

    private void Update()
    {
        if (ghost || clicker == null) return;
        if (belt == null)
        {
            // Its belt was split at another sorter's joint: carry on on the piece under it; a removed belt takes the sorter with it.
            if (PixelConveyor.NearestOn(clicker, transform.position, out PixelConveyor again, out float s2, 0.3f)) { belt = again; along = s2; }
            else { LoseBelt(); return; }
        }
        if (!Mathf.Approximately(beltWidth, belt.Width)) { Rebuild(belt.Width); if (sockets.Length > 0) sockets[0].localPosition = new Vector3(belt.Width * 0.5f + 1.0f * unit, 0f, 0f); RefreshChute(true); }   // (its belt was made wider / narrower: the gantry follows)
        RefreshChute();
        along = Spot;   // (from where it stands: a reversed belt doesn't move it, a raised / sloped belt still carries it up)
        Vector3 p = belt.PointAt(along);
        transform.position = new Vector3(p.x, p.y + belt.SurfaceHeight, p.z);
        if (slotModel != null) slotModel.Rotate(0f, 35f * Time.deltaTime, 0f, Space.Self);
        if (paddle != null)
        {
            float half = beltWidth * 0.5f;
            float k = 0f;
            if (kickT >= 0f) { kickT += Time.deltaTime; k = kickT < 0.12f ? kickT / 0.12f : Mathf.Max(0f, 1f - (kickT - 0.12f) / 0.2f); if (kickT > 0.32f) kickT = -1f; }
            paddle.localPosition = new Vector3(Mathf.Lerp(-half + 0.1f * unit, half - 0.1f * unit, k), 0.55f * unit, 0f);
        }
        if (Time.frameCount != placedFrame)
        {
            if (PixelInput.LeftPressed() && SlotPicked()) TakeOut();
            if (PixelInput.RightPressed() || PixelInput.RightHeld() || flipTap.Pending)
            {
                bool on = !PixelFirstPerson.Active && !PixelInput.PointerOverUI() && PointerOnMe();
                if (flipTap.Update(on ? this : null)) Flip();
            }
        }
        if (Time.time >= nextCapture) { nextCapture = Time.time + 0.1f; CaptureIntoSlot(); }
    }

    private float nextCapture;

    /// <summary>Its belt was removed: the sorter goes back into the backpack and its filter block is handed back (nothing is lost).</summary>
    private void LoseBelt()
    {
        if (hasFilter)
        {
            int tier = clicker.IndexOf(filter);
            if (tier >= 0) clicker.SpawnStoredPixel(tier, slotAmount, transform.position + Vector3.up * (beamTopY + 0.5f * unit), Vector3.up * unit);
            hasFilter = false;
        }
        PixelConsumables inv = PixelFind.Cached<PixelConsumables>();
        if (inv != null && inv.GiveDevice(PixelConsumables.DeviceKind.BeltSorter)) PixelHints.Announce(beltLostText);
        PixelFeatureTester.RemovePlaced(gameObject);
    }

    private Ray PointerRay(out bool ok)
    {
        ok = true;
        if (PixelFirstPerson.Active) { ok = PixelFirstPerson.TryGetViewRay(out Ray r); return r; }
        Camera cam = clicker.ViewCamera;
        if (cam == null) { ok = false; return new Ray(); }
        return cam.ScreenPointToRay(PixelInput.PointerPosition());
    }

    private bool PointerOnMe()
    {
        Ray ray = PointerRay(out bool ok);
        if (!ok) return false;
        int n = Physics.RaycastNonAlloc(ray, pointerHits, 500f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (pointerHits[i].transform.IsChildOf(transform)) return true;
        return false;
    }

    private static readonly RaycastHit[] pointerHits = new RaycastHit[32];   // (reused every frame by PointerOnMe)

    /// <summary>The sorter turns round: it moves to the other side of the belt and its chute (the output side) goes with it.</summary>
    private void Flip()
    {
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f);
        sideDir = transform.right;
        PixelAudio.Play("device_place", 1.2f);
    }

    private bool SlotPicked()
    {
        if (slotModel == null || PixelInput.PointerOverUI()) return false;
        Camera cam = clicker.ViewCamera;
        if (cam == null || PixelFirstPerson.Active) return false;
        Vector3 sp = cam.WorldToScreenPoint(slotModel.position);
        if (sp.z <= 0f) return false;
        Vector3 edge = cam.WorldToScreenPoint(slotModel.position + cam.transform.right * 0.4f * unit);
        float radius = Mathf.Max(18f, Mathf.Abs(edge.x - sp.x));
        return Vector2.Distance(PixelInput.PointerPosition(), new Vector2(sp.x, sp.y)) <= radius;
    }

    /// <summary>Would a left click at the pointer take a block out of a sorter's slot? Pixel Grabbing steps aside then.</summary>
    public static bool ClaimsPointer()
    {
        foreach (PixelBeltSorter b in all) if (b != null && !b.ghost && b.clicker != null && b.SlotPicked()) return true;
        return false;
    }

    private void TakeOut()
    {
        if (!hasFilter || slotModel == null) return;
        Vector3 at = slotModel.position;
        int idx = clicker.IndexOf(filter);
        double amount = slotAmount;
        SetFilter(-1, 0d);
        if (idx < 0 || !clicker.SpawnStoredPixel(idx, amount, at, Vector3.zero)) return;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        Rigidbody body = pixels.Count > 0 ? pixels[pixels.Count - 1] : null;
        if (body == null) return;
        ignoreUntil[body] = Time.time + 1.5f;
        PixelGrab grab = PixelFind.First<PixelGrab>();
        if (grab != null) grab.BeginHold(body);
        PixelAudio.Play("ui_click");
    }

    private readonly Dictionary<Rigidbody, float> ignoreUntil = new Dictionary<Rigidbody, float>();

    /// <summary>A pixel the player let go of over the tray goes into the slot (a block already there is handed back).</summary>
    private void CaptureIntoSlot()
    {
        float window = PixelGrab.RecentlyHeldSeconds;
        Vector3 slot = SlotWorld();
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic) continue;
            OldPixelInfo info = PixelHuskQuery.Get(body).info;
            if (info == null || info.heldByPlayer || !info.RecentlyHeld(window)) continue;
            if (ignoreUntil.TryGetValue(body, out float until) && Time.time < until) continue;
            if (!clicker.IsValidTierIndex(info.tierIndex) || clicker.Tiers[info.tierIndex].flyAway || clicker.IsFlyingPixel(body)) continue;
            Vector3 d = body.position - slot; d.y = 0f;
            float up = body.position.y - slot.y;
            if (d.magnitude > 0.9f * unit || up < -0.9f * unit || up > 3f * unit) continue;
            int tier = info.tierIndex; double amount = info.amount; GameObject go = body.gameObject;
            if (!clicker.ReleaseOldPixel(body, false)) continue;
            if (hasFilter) { int old = clicker.IndexOf(filter); double oldAmount = slotAmount; if (old >= 0) clicker.SpawnStoredPixel(old, oldAmount, slot + Vector3.up * 0.5f * unit, Vector3.up * unit); }
            Destroy(go);
            SetFilter(tier, amount);
            PixelAudio.Play("device_place", 1.3f);
            return;
        }
    }

    /// <summary>
    /// The sorter is being REMOVED by the player (the tester's delete): its joint heals - the two belt pieces it split become one belt again
    /// (PixelConveyor.JoinAt), unless another sorter also stands on that joint or the pieces don't run straight on.
    /// </summary>
    public void HealJoint()
    {
        if (ghost || belt == null || along > 1.2f * unit) return;   // (not on a joint: it stood in the middle of a belt)
        foreach (PixelBeltSorter other in all)
            if (other != null && other != this && !other.ghost && other.belt == belt && other.along <= 1.2f * other.unit) return;   // (another sorter keeps the joint)
        PixelConveyor second = belt;
        belt = null;   // (it lets go first, so it does not follow the joined belt)
        PixelConveyor.JoinAt(second);
    }

    /// <summary>
    /// The JOINTS of the sorters standing on these belts (Control Lever sections, PixelConveyor.CollectSection): the end of its belt a sorter sits on
    /// (within about a cube of it). A sorter in the middle of an unsplit belt is no joint.
    /// </summary>
    public static void JointsOn(List<PixelConveyor> belts, List<Vector3> joints)
    {
        foreach (PixelBeltSorter b in all)
        {
            if (b == null || b.ghost || b.belt == null || !belts.Contains(b.belt)) continue;
            float reach = 1.2f * b.unit, spot = b.Spot;
            if (spot <= reach) joints.Add(b.belt.StartPoint);
            else if (spot >= b.belt.Length - reach) joints.Add(b.belt.ExitPoint);
        }
    }

    /// <summary>
    /// Called by the belt system for every riding pixel BEFORE it moves this physics step ('s' = where it is now): true when a sorter took it off the
    /// belt (the system then forgets it). A sorter anywhere on the stretch the pixel is about to cross catches it, so a fast belt can't skip one.
    /// </summary>
    public static bool TryDivert(PixelClicker c, PixelConveyor belt, float s, Rigidbody rb)
    {
        float step = belt != null ? belt.RunSpeed * Time.fixedDeltaTime : 0f;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            PixelBeltSorter b = all[i];
            if (b == null || b.ghost || b.belt != belt) continue;
            float spot = b.Spot;
            if (spot < s - 0.2f * b.unit || spot > s + step + 0.05f * b.unit) continue;
            OldPixelInfo info = PixelHuskQuery.Get(rb).info;
            if (info == null || !c.IsValidTierIndex(info.tierIndex)) continue;
            PixelClicker.PixelType type = c.Tiers[info.tierIndex].type;
            if (!b.hasFilter || type != b.filter) continue;   // (an empty slot lets everything pass)
            if (PixelCouplings.OutputConnected(b, "chute") && !PixelCouplings.OutputHasRoom(b, "chute")) continue;   // (the belt off the chute is full: it rides on rather than being thrown on the floor)
            b.Eject(rb);
            return true;
        }
        return false;
    }

    private void Eject(Rigidbody rb)
    {
        kickT = 0f;
        PixelAudio.Play("click", 0.8f);
        float u = unit, oldSize = clicker.OldPixelWorldSize;
        rb.isKinematic = false;
        if (PixelCouplings.Emit(this, "chute", rb)) return;   // a belt that starts at the chute's mouth (its built-in coupling) takes it
        rb.position = transform.position + sideDir * (beltWidth * 0.5f + 0.2f * u) + Vector3.up * (oldSize * 0.6f);
        PixelPathCarrier.SetVelocity(rb, sideDir * Kick * u + Vector3.up * (0.6f * u));
    }

    // ---------------- saving ----------------

    [System.Serializable]
    private class Save { public Vector3 spot; public float yaw; public bool has; public string filter; public double amount; }

    public static string ExportState(GameObject root)
    {
        PixelBeltSorter b = root != null ? root.GetComponent<PixelBeltSorter>() : null;
        return b == null || b.belt == null ? null : JsonUtility.ToJson(new Save { spot = root.transform.position, yaw = root.transform.eulerAngles.y, has = b.hasFilter, filter = b.filter.ToString(), amount = b.slotAmount });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        PixelClicker.PixelType type = default;
        bool has = s.has && System.Enum.TryParse(s.filter, out type);
        return PlaceWith(c, s.spot, s.yaw, has, type, s.amount, false);   // (the saved belts are already split at its joint)
    }
}
