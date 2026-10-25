// ---- System notes ----
// PixelSweeper.cs
//   THE SWEEPER (PixelConsumables DeviceKind.Sweeper 25, Utilities; design: docs/GameDesign.md section 6 'The Sweeper'): an early stray-husk helper, NOT tied to
//   Electrician. Built from the old Tiny Robot (the tester's belt-feeding hover bot, same fetch loop) into a little wind-up CLOCKWORK CUBE with a broom: it rolls
//   out from its home pad, fetches husks that are CLOSE TO FADING (it sets off once RemainingReal <= fadingSeconds + headStartSeconds + the trip there; the most urgent one within rangeCubes of home first), carries
//   them on its head (kept alive on the way) and drops them into the NEAREST container with room that is an IPixelStrayTarget (Storage Crate,
//   Recycler - nearest to where the husk was picked up), through the container's one husk hook (IPixelSocketDevice.AcceptHusk; room = IPixelHuskRoom). So the
//   player's placement decides where strays go: a crate nearest = stored safe, a Recycler nearest = straight to recycling. It MOVES husks, never processes them.
//   After a drop it pauses pauseAfterDrop (0.35 s), then goes straight to the next stray; with none it rolls back to its pad (it looks every searchSeconds).
//   Refused (full) or no container: it puts the husk down where it stands. Husks held by the player, kinematic (riding / kept by a machine), flying, attached or
//   high above the floor are left alone; one the player grabs from it is let go.
//   WIND-UP: it runs runSeconds (60) after a wind, slowing to a stop over the last slowDownSeconds (the key on its back turns slower too); stopped, it shows
//   'click to wind' and a LEFT CLICK on it winds it again (PixelPointerClaim.ClaimButton: the cube and Pixel Grabbing leave that click alone). motorFitted is the
//   hook for the planned Electrician MOTOR (fitted through a future Power socket): with it on, it never runs down.
//   The placed device is the permanent PixelPermanentDevice stub (bought, kept, placed, saved, removed - hold the right button on it - like every device). Unlock:
//   vendorOnly + shopSkillId 'standard' / level 5 (on sale in the shop from Standard 5, pure currency). Saved: the wind left (PlacedState.extra 'W,seconds').
//   No carry handles: it roams away from its home pad, so it is moved by picking it up (removing) and placing it again. Stats sweeper.rescued.
//   DEVICE CARD (PixelDeviceCard, FillDeviceInfo): wind left as the bar, what it is doing; the panel adds range and this session's rescues.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>A container the Sweeper may drop stray husks into (it takes them through AcceptHusk and says its room through IPixelHuskRoom).</summary>
public interface IPixelStrayTarget { }

public class PixelSweeper : MonoBehaviour, IPixelDeviceInfo
{
    [Header("Sweeper")]
    [Min(1f)]
    [Tooltip("How far from its home pad it looks for strays, in husk widths.")]
    [SerializeField] private float rangeCubes = 12f;

    [Min(0.2f)]
    [Tooltip("How fast it rolls, in husk widths per second (at full wind).")]
    [SerializeField] private float speedCubes = 3.5f;

    [Min(5f)]
    [Tooltip("Seconds it runs after one wind.")]
    [SerializeField] private float runSeconds = 60f;

    [Min(0f)]
    [Tooltip("Over the last this many seconds of a wind it slows down to a stop.")]
    [SerializeField] private float slowDownSeconds = 12f;

    [Min(0.5f)]
    [Tooltip("'Close to fading': a husk with this many seconds of life left (or fewer) is a stray it rescues.")]
    [SerializeField] private float fadingSeconds = 4f;

    [Min(0f)]
    [Tooltip("It sets off this many seconds EARLIER than 'close to fading', plus the time the trip takes, so it reaches a husk before its last moments.")]
    [SerializeField] private float headStartSeconds = 2f;

    [Min(0f)]
    [Tooltip("After dropping a husk in, it waits this long, then goes straight to the next stray (or back to its pad when there is none).")]
    [SerializeField] private float pauseAfterDrop = 0.35f;

    [Min(0.05f)]
    [Tooltip("Seconds between two looks for a stray while it waits.")]
    [SerializeField] private float searchSeconds = 0.12f;

    [Tooltip("The planned Electrician MOTOR (fitted through a future Power socket): on = it never runs down and needs no winding. Off for now.")]
    [SerializeField] private bool motorFitted = false;

    [Header("Texts")]
    [Tooltip("The label over an unwound Sweeper.")] [SerializeField] private string windText = "click to wind";
    [Tooltip("Event log line when the Sweeper is fully wound.")] [SerializeField] private string woundText = "Sweeper wound up";

    private static readonly List<PixelSweeper> all = new List<PixelSweeper>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    public static IReadOnlyList<PixelSweeper> All => all;

    private enum State { Idle, ToHusk, ToContainer }

    private PixelClicker clicker;
    private float unit = 1f;
    private Transform bot, key, broom;
    private Renderer[] botParts;
    private TMPro.TextMeshPro windLabel;
    private State state = State.Idle;
    private Rigidbody target;
    private int sessionRescued;   // husks it brought to a container since it was placed / loaded (the device panel)
    private IPixelSocketDevice container;
    private float containerReach;
    private float windLeft, nextSearch, pauseUntil;
    private Vector3 botPos;

    /// <summary>Is the motor fitted (no winding)? The hook for the later Electrician upgrade.</summary>
    public bool Motorized => motorFitted;

    private float Wind01 => motorFitted ? 1f : Mathf.Clamp01(windLeft / Mathf.Max(0.1f, slowDownSeconds));
    private bool RunDown => !motorFitted && windLeft <= 0f;

    // ---------------- building ----------------

    private static float Unit(PixelClicker c) => c != null && c.OldPixelWorldSize > 0f ? c.OldPixelWorldSize : 0.5f;

    /// <summary>The shared device card / panel: wind left (the bar), what it is doing; the panel adds its range and this session's rescues.</summary>
    public void FillDeviceInfo(PixelDeviceInfo info)
    {
        if (motorFitted) info.Note("Motor fitted - never runs down");
        else if (RunDown) { info.Note("<color=#FFD27F>Run down - click it to wind it up</color>"); info.Progress = 0f; info.ProgressLabel = "no wind left"; }
        else { info.Progress = Mathf.Clamp01(windLeft / Mathf.Max(1f, runSeconds)); info.ProgressLabel = PixelDeviceInfo.Clock(windLeft) + " of wind"; }
        info.Add("Doing", state == State.ToHusk ? "fetching a fading husk" : state == State.ToContainer ? "carrying a husk to a container" : "waiting at home");
        if (!info.Full) return;
        info.Add("Range", rangeCubes.ToString("0") + " cubes from its pad");
        info.Add("Wind", PixelDeviceInfo.Clock(runSeconds) + " per wind-up");
        info.Heading("This session");
        info.Add("Husks rescued", sessionRescued.ToString());
    }

    /// <summary>A placed Sweeper with its home pad at 'point' (wound up and ready).</summary>
    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        GameObject root = Build(c, false);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        PixelSweeper s = root.AddComponent<PixelSweeper>();
        s.Init(c);
        return root;
    }

    public static GameObject CreateGhost(PixelClicker c) => Build(c, true);
    public static GameObject CreateDisplay(PixelClicker c) => Build(c, false);

    private static GameObject Build(PixelClicker c, bool ghost)
    {
        float u = Unit(c);
        PixelFactoryKit k = new PixelFactoryKit(c, ghost);
        GameObject root = new GameObject(ghost ? "Sweeper (Preview)" : "Sweeper");
        Transform t = root.transform;
        Color brass = new Color(0.86f, 0.66f, 0.3f, 1f), copper = new Color(0.72f, 0.42f, 0.25f, 1f), dark = new Color(0.16f, 0.15f, 0.14f, 1f);
        Color wood = new Color(0.55f, 0.38f, 0.22f, 1f), straw = new Color(0.9f, 0.78f, 0.42f, 1f), eye = new Color(0.4f, 0.95f, 1f, 1f);
        // Its home pad: a round dock it rests on and returns to.
        k.Cyl("Pad", t, new Vector3(0f, 0.03f * u, 0f), 0.75f * u, 0.06f * u, dark, default, 0.5f, 0.5f);
        k.Cyl("Pad Ring", t, new Vector3(0f, 0.065f * u, 0f), 0.62f * u, 0.015f * u, brass, default, 0.8f, 0.7f);
        // The clockwork cube itself (it rolls about, so it hangs under its own 'Bot' object).
        GameObject b = new GameObject("Bot");
        b.transform.SetParent(t, false);
        Transform bt = b.transform;
        k.Box("Body", bt, new Vector3(0f, 0.42f * u, 0f), new Vector3(0.62f * u, 0.56f * u, 0.62f * u), brass, default, false, 0.7f, 0.6f);
        k.Box("Band", bt, new Vector3(0f, 0.3f * u, 0f), new Vector3(0.66f * u, 0.08f * u, 0.66f * u), copper, default, false, 0.8f, 0.6f);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            k.Box("Eye", bt, new Vector3(sx * 0.13f * u, 0.52f * u, 0.315f * u), new Vector3(0.1f * u, 0.1f * u, 0.02f * u), eye, default, false, 0f, 0.9f, eye);
            for (int sz = -1; sz <= 1; sz += 2)   // four little wheels
                k.Cyl("Wheel", bt, new Vector3(sx * 0.33f * u, 0.1f * u, sz * 0.2f * u), 0.1f * u, 0.06f * u, dark, new Vector3(0f, 0f, 90f), 0.3f, 0.4f);
        }
        // The wind-up key on its back (it turns while the clockwork runs).
        GameObject keyPivot = new GameObject("Key");
        keyPivot.transform.SetParent(bt, false);
        keyPivot.transform.localPosition = new Vector3(0f, 0.5f * u, -0.36f * u);
        k.Cyl("Key Shaft", keyPivot.transform, new Vector3(0f, 0f, -0.06f * u), 0.03f * u, 0.12f * u, copper, new Vector3(90f, 0f, 0f), 0.8f, 0.6f);
        k.Box("Key Bow", keyPivot.transform, new Vector3(0f, 0f, -0.14f * u), new Vector3(0.3f * u, 0.12f * u, 0.03f * u), copper, default, false, 0.8f, 0.6f);
        // The broom in front: a stick angled down and a straw head that sweeps side to side.
        GameObject broomPivot = new GameObject("Broom");
        broomPivot.transform.SetParent(bt, false);
        broomPivot.transform.localPosition = new Vector3(0.2f * u, 0.45f * u, 0.3f * u);
        k.Cyl("Broom Stick", broomPivot.transform, new Vector3(0f, -0.12f * u, 0.18f * u), 0.025f * u, 0.5f * u, wood, new Vector3(55f, 0f, 0f), 0.1f, 0.3f);
        k.Box("Broom Head", broomPivot.transform, new Vector3(0f, -0.36f * u, 0.36f * u), new Vector3(0.36f * u, 0.12f * u, 0.08f * u), straw, default, false, 0f, 0.2f);
        return root;
    }

    private void Init(PixelClicker c)
    {
        clicker = c;
        unit = Unit(c);
        bot = transform.Find("Bot");
        key = bot != null ? bot.Find("Key") : null;
        broom = bot != null ? bot.Find("Broom") : null;
        botParts = bot != null ? bot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        windLeft = runSeconds;
        botPos = transform.position;
        GameObject lg = new GameObject("Wind Label");
        lg.transform.SetParent(transform, false);
        windLabel = lg.AddComponent<TMPro.TextMeshPro>();
        windLabel.text = windText;
        windLabel.fontSize = 2.2f;
        windLabel.alignment = TMPro.TextAlignmentOptions.Center;
        windLabel.color = new Color(1f, 0.85f, 0.45f, 1f);
        windLabel.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
        if (c != null && c.UIFont != null) windLabel.font = c.UIFont;
        windLabel.gameObject.SetActive(false);
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); }
    private void OnDestroy() { PixelHuskPool.HuskReleasing -= OnHuskReleasing; DropTarget(); }
    private void Awake() => PixelHuskPool.HuskReleasing += OnHuskReleasing;

    // (PixelHuskPool: the husk is going away and may be reused - let go of it; nothing to put down, it is gone)
    private void OnHuskReleasing(Rigidbody body)
    {
        if (body != target) return;
        target = null;
        state = State.Idle;
    }

    // ---------------- saving (PixelConsumables puts this in PlacedState.extra) ----------------

    public string ExportState() => "W," + windLeft.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public void ImportState(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        string[] f = data.Split(',');
        if (f.Length >= 2 && f[0] == "W" && float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w)) windLeft = Mathf.Clamp(w, 0f, runSeconds);
    }

    // ---------------- finding strays and containers ----------------

    private float FloorY => transform.position.y;

    /// <summary>A husk it may pick up: loose, not touched by anything else, not high above the floor.</summary>
    private bool Fetchable(Rigidbody rb)
    {
        if (!PixelHuskHolder.Takeable(clicker, rb) || PixelAttachment.IsAttached(rb)) return false;   // (one owner per husk: held, reserved or kept by another machine = not ours)
        if (PixelHuskQuery.Get(rb).despawn == null) return false;
        if (rb.position.y > FloorY + 2.5f * unit) return false;
        Vector3 c = rb.worldCenterOfMass;
        foreach (PixelConveyor b in PixelConveyor.All) if (b != null && !b.IsGhost && b.Carries(c)) return false;   // riding a belt (or about to be)
        return true;
    }

    /// <summary>
    /// Worth setting off for: a husk whose fade timer is running and that will be close to fading by the time it gets there - fadingSeconds, plus the trip
    /// from where the Sweeper is now, plus a head start. 'left' = the time it has left.
    /// </summary>
    private bool Fading(Rigidbody rb, out float left)
    {
        OldPixelDespawn d = PixelHuskQuery.Get(rb).despawn;
        left = d != null ? d.RemainingReal : -1f;
        if (d == null || d.Paused || left < 0f) return false;
        float trip = Flat(rb.position - botPos) / Mathf.Max(0.05f, speedCubes * unit);
        return left <= fadingSeconds + headStartSeconds + trip;
    }

    /// <summary>The most urgent stray within range of home (the least time left).</summary>
    private Rigidbody FindStray()
    {
        float reach = rangeCubes * unit, bestLeft = float.MaxValue;
        Rigidbody pick = null;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody rb = pixels[i];
            if (rb == null) continue;
            Vector3 d = rb.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > reach * reach || !Fading(rb, out float left) || !Fetchable(rb)) continue;
            if (left < bestLeft) { bestLeft = left; pick = rb; }
        }
        return pick;
    }

    /// <summary>The container with room nearest this point (a Storage Crate, Junction Box or Recycler).</summary>
    private static IPixelSocketDevice NearestContainer(Vector3 from)
    {
        IPixelSocketDevice best = null;
        float bestD = float.MaxValue;
        foreach (IPixelSocketDevice d in PixelCouplings.Devices)
        {
            if (!(d is IPixelStrayTarget) || (d is Object o && o == null) || d.SocketRoot == null) continue;
            PixelSocket intake = IntakeOf(d);
            if (intake == null || !(d is IPixelHuskRoom room) || room.HuskRoom(intake) <= 0) continue;
            float dist = (d.SocketRoot.position - from).sqrMagnitude;
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    private static PixelSocket IntakeOf(IPixelSocketDevice d)
    {
        foreach (PixelSocket s in d.Sockets) if (s != null && s.kind == PixelSocketKind.Husk && s.CanTakeIn) return s;
        return null;
    }

    /// <summary>How close it has to get to a container to drop a husk in (the container's footprint, from its model's bounds, plus a little).</summary>
    private float ReachOf(IPixelSocketDevice d)
    {
        Bounds bb = new Bounds(d.SocketRoot.position, Vector3.one * unit);
        bool any = false;
        foreach (MeshRenderer r in d.SocketRoot.GetComponentsInChildren<MeshRenderer>())
        {
            if (r == null || r.GetComponentInParent<OldPixelInfo>() != null) continue;
            if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds);
        }
        return Mathf.Max(bb.extents.x, bb.extents.z) + 0.6f * unit;
    }

    // ---------------- every frame ----------------

    private PixelPlacedDevice placedDevice;   // (this Sweeper's PixelPlacedDevice, fetched once it is there)

    private void Update()
    {
        if (clicker == null || bot == null) return;
        if (placedDevice == null) placedDevice = GetComponent<PixelPlacedDevice>();
        PixelPlacedDevice device = placedDevice;
        if (device != null && device.IsEnding) { DropTarget(); return; }
        if (PixelCalm.InMenu || Time.timeScale <= 0f) return;
        float dt = Time.deltaTime;

        HandleWinding();
        if (!motorFitted && windLeft > 0f) windLeft = Mathf.Max(0f, windLeft - dt);
        float pace = Wind01;
        if (RunDown && target != null) { DropTarget(); state = State.Idle; }   // (ran down while carrying: it puts the husk down)

        Vector3 home = transform.position, goal = home;
        switch (state)
        {
            case State.Idle:
                if (Time.time < pauseUntil) { goal = botPos; break; }   // (a short stop after dropping a husk in)
                if (!RunDown && Time.time >= nextSearch)
                {
                    nextSearch = Time.time + searchSeconds;
                    Rigidbody stray = FindStray();
                    if (stray != null && NearestContainer(stray.position) != null) { target = stray; state = State.ToHusk; goal = stray.position; break; }   // off it goes at once
                }
                break;   // nothing to fetch: back to its pad
            case State.ToHusk:
                if (!Fetchable(target)) { target = null; state = State.Idle; nextSearch = 0f; break; }   // (gone: look for the next one straight away)
                goal = target.position;
                if (Flat(goal - botPos) < 0.45f * unit)
                {
                    container = NearestContainer(target.position);
                    if (container == null) { target = null; state = State.Idle; break; }
                    containerReach = ReachOf(container);
                    PixelHuskHolder.Reserve(target);   // (kinematic, its fade timer held: no other machine takes it on the way)
                    state = State.ToContainer;
                    PixelAudio.Play("click", 1.4f);
                }
                break;
            case State.ToContainer:
                if (target == null || container == null || (container is Object co && co == null)) { DropTarget(); state = State.Idle; break; }
                if (PixelHuskQuery.Get(target).HeldByPlayer) { target = null; state = State.Idle; break; }   // the player grabbed it: theirs now
                goal = container.SocketRoot.position;
                if (Flat(goal - botPos) <= containerReach) Deliver();
                break;
        }

        // Roll towards the goal on the floor (slower as the wind runs out), turning to face the way it goes.
        goal.y = home.y;
        Vector3 before = botPos;
        botPos = Vector3.MoveTowards(botPos, goal, speedCubes * unit * pace * dt);
        botPos.y = home.y;
        bot.position = botPos;
        Vector3 move = botPos - before; move.y = 0f;
        bool moving = move.sqrMagnitude > 1e-8f;
        if (moving) bot.rotation = Quaternion.Slerp(bot.rotation, Quaternion.LookRotation(move.normalized, Vector3.up), 8f * dt);
        if (key != null) key.Rotate(0f, 0f, 220f * pace * (RunDown ? 0f : 1f) * dt, Space.Self);
        if (broom != null) broom.localRotation = Quaternion.Euler(0f, moving ? Mathf.Sin(Time.time * 10f) * 18f : 0f, 0f);
        if (target != null && state == State.ToContainer)
        {
            target.position = botPos + Vector3.up * 1.1f * unit;   // carried on its head
            OldPixelDespawn d = PixelHuskQuery.Get(target).despawn;
            if (d != null) d.Held = true;
        }
        UpdateLabel();
    }

    private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    /// <summary>At the container: the husk goes in through its one hook; refused, it is put down here.</summary>
    private void Deliver()
    {
        Rigidbody husk = target;
        target = null;
        state = State.Idle;
        pauseUntil = Time.time + pauseAfterDrop;   // a short stop, then straight on to the next stray (or home)
        nextSearch = pauseUntil;
        PixelSocket intake = IntakeOf(container);
        PixelHuskHolder.Unreserve(husk, Vector3.zero);
        husk.isKinematic = true;
        if (intake != null && container.AcceptHusk(intake, husk)) { PixelStats.Count("sweeper.rescued"); sessionRescued++; PixelAudio.Play("device_place", 1.2f, 0.5f); return; }
        husk.isKinematic = false;
        husk.position = botPos + Vector3.up * 0.8f * unit;
    }

    private void DropTarget()
    {
        if (target != null) PixelHuskHolder.Unreserve(target, Vector3.zero);   // (falls where it is, with a moment more life)
        target = null;
    }

    // ---------------- winding ----------------

    /// <summary>A left click on the clockwork cube winds it up again (the pointer on it claims the click).</summary>
    private void HandleWinding()
    {
        if (motorFitted || PixelInput.PointerOverUI() || PixelCarryHandles.Hot) return;
        Camera cam = clicker.ViewCamera;
        if (cam == null || !PointerOnBot(cam)) return;
        PixelPointerClaim.ClaimButton();
        if (!PixelInput.LeftPressed()) return;
        windLeft = runSeconds;
        PixelAudio.Play("ui_click", 0.8f);
        PixelHints.Announce(woundText);
    }

    private bool PointerOnBot(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        foreach (Renderer r in botParts) if (r != null && r.enabled && r.bounds.IntersectRay(ray)) return true;
        return false;
    }

    private void UpdateLabel()
    {
        if (windLabel == null) return;
        bool show = RunDown;
        if (windLabel.gameObject.activeSelf != show) windLabel.gameObject.SetActive(show);
        if (!show) return;
        windLabel.transform.position = botPos + Vector3.up * 1.4f * unit;
        Camera cam = clicker.ViewCamera;
        if (cam != null) windLabel.transform.rotation = Quaternion.LookRotation(windLabel.transform.position - cam.transform.position, cam.transform.up);
    }
}
