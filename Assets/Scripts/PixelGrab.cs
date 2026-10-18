// ---- System notes (moved here from CLAUDE.md) ----
// PixelGrab.cs
//   Pixel Grabbing (unlocked from the start - Awake sets grabbingActive; the shop pack was removed by MigrateOldPackData; it keeps its
//   on / off switch in Toggles > Upgrades, on by default): hold left mouse on an old pixel (sphere-cast, grabRadius) to drag it on the
//   camera-facing plane through it (rigidbody velocity follows the mouse, so it still collides), release to throw (throwStrength,
//   maxThrowSpeed); a held pixel's OldPixelDespawn.Held freezes its lifetime (a held glass pixel can't shatter; releasing slower than
//   gentleReleaseSpeed = a soft set-down: no break, speed damped, gentleLifetimeBonus seconds added via OldPixelDespawn.AddLifetime)
//   and its ScaledGravity is paused. While time is slowed a still-flying meteor pixel (OldPixelFlight, trigger collider) can be
//   grabbed too: PixelClicker.LandFlyingPixel turns it into an ordinary old pixel (solid, gravity, view bounds, normal lifetime);
//   grabbing / throwing divide / multiply velocities by PixelTimeStop.SlowFactor so it still follows the mouse normally. Gentle
//   collisions: a carried pixel is light (carriedMassFactor 0 = 0.2 of its mass, stays light lightSecondsAfterThrow 1.5 s after the
//   throw) and every old pixel within ~1.7 widths of a light one has its speed capped to hitReactionSpeedLimit (0 = 5) for 0.7 s
//   (LimitReactions, MakeLight), so piles react less and nothing flies far; physics still applies (disableGentleHits). Sets
//   PixelClicker.GrabEnabled so clicks on old pixels no longer pass through to the cube. Sounds grab/drop. Steady carrying (2026-10-16, held husks
//   used to jitter / bounce against the floor and piles): while held a husk is interpolated, uses continuous collisions, can't tumble
//   (freezeSpinWhileHeld), is pushed out of things gently (heldDepenetrationSpeed 2) and is lifted heldAutoLift (0.35) widths off what it
//   rested on; all of it is put back on release. PixelShop finds/adds it.
//   TUMBLING WHEN THROWN (2026-10-17, thrown husks used to skid flat): on release StartTumble gives a husk thrown faster than
//   minTumbleSpeed sideways a roll along the throw (axis = up x sideways velocity, speed = travel / half height x throwSpin, capped
//   maxThrowSpin - maxAngularVelocity is raised for it, Unity's default 7 rad/s was too low - plus spinWobble on the axis) and for
//   thrownGripSeconds swaps its collider to a grippy copy of its own material (friction >= thrownGrip, combined by Maximum; husk materials
//   normally combine friction by Minimum so corners never caught). EndThrownGrip puts the original back (time over, or grabbed again).
//   Tags: OldPixelInfo.heldByPlayer / lastHeldTime / RecentlyHeld(seconds) = 'recently held by the player' (held now, or let go within recentlyHeldSeconds 0 = 6, Inspector); the Storage Crate
//   stores a tagged pixel that lands in its footprint from any height.
//   GROUND CLAMP (2026-10-16): a held husk is never pushed into the surface under it (TryGroundUnder: highest still collider straight below, husks skipped);
//   lowering it with the scroll wheel stops at the ground (it used to sink in, crawl after the mouse and replay the landing sound).
//   Accessibility 'Drop guide for held pixels' (Settings, PixelDisplaySettings.DropGuide, on by default): while holding a pixel LateUpdate / UpdateDropGuide draws a faint line straight down
//   from it to the first surface a sphere of its size would land on (ignores itself, triggers and the live cube) and a ring on that surface = where it drops if let go standing still.
// ---------------------------------------------------
using UnityEngine;
using static PixelInput;

/// <summary>
/// Pixel Grabbing (bought as an upgrade in the shop). With it, you can press the left mouse button on an old pixel,
/// drag it around the screen (it keeps colliding with other old pixels and the floor) and let go to drop or throw it.
/// A grabbed pixel doesn't despawn while you hold it. A click on an old pixel no longer falls through to the cube behind.
///
/// Add this to any GameObject. PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelGrab : MonoBehaviour, IPixelSaved
{
    // ---- save: this system writes its own part of the save file (PixelSaveRegistry) ----
    [System.Serializable] private class SaveState { public bool disabled; }
    string IPixelSaved.SaveId => "grab";
    int IPixelSaved.SaveOrder => 120;
    string IPixelSaved.Export() => PixelSaveRegistry.ToJson(new SaveState { disabled = UserDisabled });
    void IPixelSaved.Import(string data) { SaveState s = PixelSaveRegistry.FromJson<SaveState>(data); UserDisabled = s != null && s.disabled; }

    [Header("References")]
    [Tooltip("The PixelClicker whose old pixels can be grabbed. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Is grabbing available? (The shop turns this on when the Pixel Grabbing upgrade is bought. Tick it to test.)")]
    [SerializeField] private bool grabbingActive = false;

    [Header("Grabbing")]
    [Min(0f)]
    [Tooltip("How forgiving the click is: old pixels are small, so the click can be this far (world units) off a pixel and still grab it.")]
    [SerializeField] private float grabRadius = 0.25f;

    [Min(0f)]
    [Tooltip("An old pixel can't be grabbed for this many seconds after it appears (it is still inside the cube's click area, so it would be grabbed straight through the cube). 0 = the default (0.8).")]
    [SerializeField] private float grabDelaySeconds = 0f;

    [Min(0f)]
    [Tooltip("How long (seconds) after the player lets go of an old pixel it still counts as 'recently held by the player' (OldPixelInfo.RecentlyHeld). A Storage Crate stores such a pixel when it lands on it, even if it was dropped from the very top. 0 = the default (6).")]
    [SerializeField] private float recentlyHeldSeconds = 0f;

    /// <summary>How long a let-go pixel stays tagged 'recently held by the player'.</summary>
    public static float RecentlyHeldSeconds => instance != null && instance.recentlyHeldSeconds > 0f ? instance.recentlyHeldSeconds : 6f;

    private static PixelGrab instance;

    [Min(1f)]
    [Tooltip("How tightly a held pixel follows the mouse (higher = snappier, lower = floaty).")]
    [SerializeField] private float followSharpness = 18f;

    [Min(1f)]
    [Tooltip("Fastest a held pixel can move (world units per second), so it can't smash through things.")]
    [SerializeField] private float maxFollowSpeed = 40f;

    [Range(0f, 1f)]
    [Tooltip("How quickly a held pixel stops spinning (0 = keeps spinning, 1 = stops at once).")]
    [SerializeField] private float spinDamping = 0.2f;

    [Header("Steady carrying (stops held husks freaking out)")]
    [Min(0f)]
    [Tooltip("A grabbed husk is lifted this many husk widths off what it was resting on, so it doesn't scrape along the floor / other husks while dragged (scraping made it bounce). The scroll wheel still raises / lowers it.")]
    [SerializeField] private float heldAutoLift = 0.35f;
    [Tooltip("Stop a held husk from tumbling while it is carried (its edges catching on the floor and other husks made it jitter). It spins freely again when let go.")]
    [SerializeField] private bool freezeSpinWhileHeld = true;
    [Min(0.1f)]
    [Tooltip("How fast the physics may push a held husk out of things it is pressed into (Unity's default is 10, which made it jump). Lower = calmer when pushed against a pile.")]
    [SerializeField] private float heldDepenetrationSpeed = 2f;

    // The held husk's own physics settings, put back when it is let go.
    private RigidbodyInterpolation heldWasInterpolation;
    private CollisionDetectionMode heldWasDetection;
    private bool heldWasFreezeRotation;
    private float heldWasDepenetration;

    [Header("Letting Go")]
    [Range(0f, 2f)]
    [Min(0f)]
    [Tooltip("Letting go of a fragile pixel (glass) slower than this (world units per second) sets it down gently: it won't shatter when it lands, it settles, and it lasts longer.")]
    [SerializeField] private float gentleReleaseSpeed = 3f;

    [Min(0f)]
    [Tooltip("Seconds added to a fragile pixel's despawn timer when you set it down gently.")]
    [SerializeField] private float gentleLifetimeBonus = 12f;

    [Tooltip("How much of the mouse's speed a pixel keeps when you let go (0 = it just drops, 1 = a natural throw).")]
    [SerializeField] private float throwStrength = 1f;

    [Min(0f)]
    [Tooltip("Fastest a thrown pixel can leave your hand (world units per second).")]
    [SerializeField] private float maxThrowSpeed = 25f;

    [Header("Tumbling when thrown")]
    [Range(0f, 2f)]
    [Tooltip("Spin a thrown husk gets when you let go, so it tumbles end over end instead of skidding flat. 1 = it rolls exactly as fast as it travels (like a die rolled across a table), 0 = no spin (the old skid).")]
    [SerializeField] private float throwSpin = 0.8f;
    [Min(0f)]
    [Tooltip("Fastest a thrown husk may spin (turns per second x 6.28, radians per second). Unity's own cap is 7, which made hard throws look like slides.")]
    [SerializeField] private float maxThrowSpin = 25f;
    [Min(0f)]
    [Tooltip("A husk let go slower than this sideways (world units per second) gets no spin: a set-down stays a set-down.")]
    [SerializeField] private float minTumbleSpeed = 1.5f;
    [Range(0f, 1f)]
    [Tooltip("Random wobble added to the spin axis (0 = every throw rolls perfectly straight, 1 = quite wild). A little makes throws look natural.")]
    [SerializeField] private float spinWobble = 0.25f;
    [Range(0f, 1.5f)]
    [Tooltip("Grip of a thrown husk for a moment after the throw (0 = slippery, 1 = rubbery), combined by Maximum so it wins against the floor. Keep it above the resting husk friction (PixelClicker friction 0.65) so corners catch and tip a thrown husk over instead of sliding. 0 = keep its normal grip.")]
    [SerializeField] private float thrownGrip = 0.85f;
    [Min(0f)]
    [Tooltip("Seconds a thrown husk keeps that extra grip, then it goes back to its normal material (so piles and machines behave as before).")]
    [SerializeField] private float thrownGripSeconds = 2.5f;

    [Header("Gentle collisions")]
    [Tooltip("Turns off the gentle-collision rules below (a carried / thrown pixel hits the others with its full weight and they fly as far as physics says).")]
    [SerializeField] private bool disableGentleHits = false;

    [Range(0f, 1f)]
    [Tooltip("How heavy a pixel you carry or throw is compared to normal (0.2 = a fifth of the weight, so it only nudges the others). 0 = the coded default (0.2).")]
    [SerializeField] private float carriedMassFactor = 0f;

    [Min(0f)]
    [Tooltip("Seconds a thrown pixel stays light after you let go. 0 = the coded default (1.5).")]
    [SerializeField] private float lightSecondsAfterThrow = 0f;

    [Min(0f)]
    [Tooltip("Fastest a pixel that was hit by a carried / thrown pixel may fly away (world units per second). 0 = the coded default (5).")]
    [SerializeField] private float hitReactionSpeedLimit = 0f;

    [Min(0f)]
    [Tooltip("How far one notch of the mouse wheel raises or lowers a pixel you are holding (world units). 0 = the coded default (0.4).")]
    [SerializeField] private float wheelLift = 0f;

    [Min(0f)]
    [Tooltip("Highest the wheel can lift a held pixel above where you grabbed it (world units). 0 = the coded default (6).")]
    [SerializeField] private float maxWheelLift = 0f;

    private float liftOffset;
    private Rigidbody held;
    private OldPixelDespawn heldDespawn;
    private ScaledGravity heldGravity;
    private Plane dragPlane;
    private Vector3 grabOffset;
    private Camera cam;

    /// <summary>Is grabbing unlocked (bought)?</summary>
    public bool Active => grabbingActive;

    /// <summary>The player switched grabbing off with the Toggles window. Saved.</summary>
    public bool UserDisabled
    {
        get => userDisabled;
        set
        {
            userDisabled = value;
            if (userDisabled) Release();
            ApplyToClicker();
        }
    }

    private bool userDisabled;

    private bool Working => grabbingActive && !userDisabled;

    /// <summary>Is grabbing bought and switched on? (The ghost minigame lets you grab its falling potion only then.)</summary>
    public bool CanGrab => Working;

    /// <summary>Called by the shop when the upgrade is bought.</summary>
    public void Activate()
    {
        grabbingActive = true;
        ApplyToClicker();
    }

    /// <summary>Called by the shop (e.g. when a save without the upgrade is loaded).</summary>
    public void Deactivate()
    {
        grabbingActive = false;
        Release();
        ApplyToClicker();
    }

    private void Awake()
    {
        PixelSaveRegistry.Register(this);
        instance = this;
        if (Mathf.Approximately(thrownGrip, 0.6f)) thrownGrip = 0.85f;   // (2026-10-17) above the new resting husk friction 0.65, so a throw still grips harder than a heap
        grabbingActive = true;   // Pixel Grabbing is unlocked from the start now (it still has its switch in the Toggles window)
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelGrab: no PixelClicker found in the scene.", this);
            enabled = false;
        }
    }

    private void Start() => ApplyToClicker();

    private void OnDestroy()
    {
        PixelSaveRegistry.Unregister(this);
        if (instance == this) instance = null;
        Release();
        if (clicker != null) clicker.GrabEnabled = false;
    }

    private void ApplyToClicker()
    {
        if (clicker != null) clicker.GrabEnabled = Working; // old pixels now catch clicks instead of letting them through
    }

    private void Update()
    {
        if (!Working || clicker == null) return;
        ApplyToClicker();

        if (held != null)
        {
            if (!LeftHeld() || Time.timeScale <= 0f) { Release(); return; }
            float wheel = PixelInput.ControlHeld() ? 0f : PixelInput.ScrollY();   // (Ctrl + wheel zooms the camera instead)
            if (wheel != 0f)
            {
                float cap = maxWheelLift > 0f ? maxWheelLift : 6f;
                liftOffset = Mathf.Clamp(liftOffset - Mathf.Sign(wheel) * Mathf.Min(Mathf.Abs(wheel), 3f) * (wheelLift > 0f ? wheelLift : 0.4f), -cap, cap);
            }
            return;
        }

        if (Time.timeScale <= 0f || PixelPauseMenu.IsPaused || PixelBank.HoseOn || PixelRobotWorker.Piloting || PixelMinigame.TakeoverActive || PixelClicker.GodMode || PixelPets.HoveringPet || !LeftPressed() || PointerOverUI() || PixelStorageCrate.ClaimsPointer() || PixelRecycler.ButtonHot || PixelPointerClaim.ButtonClaimed || PixelCarryHandles.Hot || PixelBeltSorter.ClaimsPointer() || PixelBaitBucket.ClaimsPointer()) return;
        TryGrab();
    }

    /// <summary>The old pixel being dragged right now (null when none).</summary>
    public Rigidbody HeldBody => held;

    /// <summary>Puts an old pixel straight into the mouse's hand (the left button must be held), as if it had just been grabbed. Used by the Storage Crate.</summary>
    public void BeginHold(Rigidbody body)
    {
        if (!Working || clicker == null || held != null || body == null) return;
        cam = clicker.ViewCamera;
        if (cam == null) return;
        Grab(body, body.GetComponent<OldPixelDespawn>(), cam.ScreenPointToRay(PointerPosition()));
    }

    private void TryGrab()
    {
        cam = clicker.ViewCamera;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(PointerPosition());
        RaycastHit[] hits = Physics.SphereCastAll(ray, grabRadius, 1000f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // Where does the exact line under the cursor hit? (The fat grab sphere reaches the cube long before the cursor is on it,
        // which used to make pixels next to the cube impossible to pick up.)
        RaycastHit[] exact = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
        float cubeExact = float.PositiveInfinity;
        if (clicker.PixelTransform != null)
            foreach (RaycastHit h in exact)
                if (h.transform.IsChildOf(clicker.PixelTransform) && h.distance < cubeExact) cubeExact = h.distance;

        foreach (RaycastHit hit in hits)
        {
            Rigidbody body = hit.rigidbody;
            if (body == null) continue;
            bool inRecycler = PixelRecycler.Holds(body);   // a husk kept in a Recycler can be picked back out
            if (!inRecycler && !IsOldPixel(body)) continue;
            if (PixelPathCarrier.IsCarried(body)) continue;   // inside a Funnel tube / Elevator / Splitter: not to be dragged out
            if (clicker.IsFlyingPixel(body) && !PixelTimeStop.IsSlowed) continue; // a flying meteor can only be caught while time is slowed

            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && despawn.IsDespawning) continue; // already vanishing
            OldPixelInfo bornInfo = body.GetComponent<OldPixelInfo>();
            if (bornInfo != null && Time.unscaledTime - bornInfo.bornTime < (grabDelaySeconds > 0f ? grabDelaySeconds : 0.8f)) continue;   // too new: it has only just come out of the cube, so it can't be grabbed through it

            // The cube only wins when the cursor is really on it and it is in front of this pixel under the cursor.
            // A pixel beside the cube (the cursor not on the cube) is always grabbable, even right up against it.
            float pixelExact = float.PositiveInfinity;
            foreach (RaycastHit h in exact)
                if (h.rigidbody == body && h.distance < pixelExact) pixelExact = h.distance;
            if (cubeExact < pixelExact) return;
            if (inRecycler && !PixelRecycler.TakeOut(body)) continue;   // back on the floor's old-pixel list, loose and steerable
            Grab(body, despawn, ray);
            return;
        }
    }

    private bool IsOldPixel(Rigidbody body)
    {
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
            if (list[i] == body) return true;
        return false;
    }

    private bool HitsCubeFirst(RaycastHit[] sorted, float distance)
    {
        foreach (RaycastHit h in sorted)
        {
            if (h.distance >= distance) return false;
            if (clicker.PixelTransform != null && h.transform.IsChildOf(clicker.PixelTransform)) return true;
        }
        return false;
    }

    private void Grab(Rigidbody body, OldPixelDespawn despawn, Ray ray)
    {
        PixelStats.Count("old.grabbed");
        PixelConveyorSystem.FreeRider(body);   // a pixel riding a belt is locked (kinematic): free it, or it would stay stuck where it is let go
        clicker.LandFlyingPixel(body); // a caught meteor becomes an ordinary old pixel (solid, falls when let go)
        held = body;
        OldPixelInfo heldInfo = body.GetComponent<OldPixelInfo>();
        if (heldInfo != null) heldInfo.heldByPlayer = true;   // tagged 'recently held by the player' from now on (until recentlyHeldSeconds after it is let go)
        liftOffset = 0f;
        heldDespawn = despawn;
        if (heldDespawn != null) heldDespawn.Held = true; // frozen lifetime while held

        heldGravity = body.GetComponent<ScaledGravity>();
        if (heldGravity != null) heldGravity.enabled = false;

        // Drag on the plane facing the camera that passes through the pixel.
        dragPlane = new Plane(-cam.transform.forward, body.position);
        grabOffset = dragPlane.Raycast(ray, out float enter) ? body.position - ray.GetPoint(enter) : Vector3.zero;
        // A tilted camera (orbital, free cam) drags on the HORIZONTAL plane through the pixel instead, so moving the mouse slides it over the floor rather than up / down through the world.
        dragLevelY = body.position.y;
        Plane level = new Plane(Vector3.up, new Vector3(0f, dragLevelY, 0f));
        levelOffset = level.Raycast(ray, out float enterLevel) ? body.position - ray.GetPoint(enterLevel) : Vector3.zero;
        levelOffset.y = 0f;

        heldShatter = body.GetComponent<OldPixelShatter>();
        if (heldShatter != null) heldShatter.Grabbed(); // can't break while carried

        if (!disableGentleHits) MakeLight(body);

        // Steady carrying: smooth movement (interpolated), no tunnelling at speed (continuous collisions), no tumbling, a gentle push-out
        // when pressed into things, and a small lift so it doesn't scrape along the floor. All put back on release.
        heldWasInterpolation = body.interpolation;
        heldWasDetection = body.collisionDetectionMode;
        heldWasFreezeRotation = body.freezeRotation;
        heldWasDepenetration = body.maxDepenetrationVelocity;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        if (!body.isKinematic) body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        EndThrownGrip(body);   // re-grabbed while still grippy from a throw: put its own material back first
        if (freezeSpinWhileHeld) { body.freezeRotation = true; if (!body.isKinematic) body.angularVelocity = Vector3.zero; }
        body.maxDepenetrationVelocity = heldDepenetrationSpeed;
        Collider heldCol = body.GetComponent<Collider>();
        if (heldCol != null) liftOffset = heldAutoLift * heldCol.bounds.size.y;

        PixelAudio.Play("grab");
    }

    private OldPixelShatter heldShatter;
    private float dragLevelY;
    private Vector3 levelOffset;

    /// <summary>Drag on the floor-parallel plane when the camera looks down at more than about 20 degrees (top down and orbital views); near the horizon the camera-facing plane is steadier.</summary>
    private const float LevelDragMinDown = 0.35f;

    // ---- Gentle collisions: a carried / thrown pixel is light, and anything it hits is kept from flying far ----
    private readonly System.Collections.Generic.Dictionary<Rigidbody, float> normalMass = new System.Collections.Generic.Dictionary<Rigidbody, float>();
    private readonly System.Collections.Generic.Dictionary<Rigidbody, float> lightUntil = new System.Collections.Generic.Dictionary<Rigidbody, float>();
    private readonly System.Collections.Generic.Dictionary<Rigidbody, float> limitedUntil = new System.Collections.Generic.Dictionary<Rigidbody, float>();
    private readonly System.Collections.Generic.List<Rigidbody> scratch = new System.Collections.Generic.List<Rigidbody>();

    private void MakeLight(Rigidbody body)
    {
        if (body == null) return;
        if (!normalMass.ContainsKey(body)) normalMass[body] = body.mass;
        body.mass = Mathf.Max(0.0001f, normalMass[body] * (carriedMassFactor > 0f ? carriedMassFactor : 0.2f));
        lightUntil[body] = float.MaxValue;   // until it is let go
    }

    /// <summary>Marks the pixels near the carried / thrown ones as 'hit' and keeps their speed down for a moment.</summary>
    private void LimitReactions()
    {
        if (disableGentleHits || clicker == null || lightUntil.Count == 0 && limitedUntil.Count == 0) return;
        float now = Time.time;
        float reach = clicker.OldPixelWorldSize * 1.7f;
        float limit = hitReactionSpeedLimit > 0f ? hitReactionSpeedLimit : 5f;

        scratch.Clear();
        foreach (var pair in lightUntil)
        {
            if (pair.Key == null) { scratch.Add(pair.Key); continue; }
            if (pair.Value < now)
            {
                scratch.Add(pair.Key);   // light time over: back to normal weight
                continue;
            }
            foreach (Rigidbody other in clicker.OldPixels)
            {
                if (other == null || other == pair.Key || other.isKinematic) continue;
                if ((other.position - pair.Key.position).sqrMagnitude < reach * reach) limitedUntil[other] = now + 0.7f;
            }
        }
        foreach (Rigidbody done in scratch)
        {
            lightUntil.Remove(done);
            if (done != null && normalMass.TryGetValue(done, out float mass)) done.mass = mass;
            normalMass.Remove(done);
        }

        scratch.Clear();
        foreach (var pair in limitedUntil)
        {
            Rigidbody other = pair.Key;
            if (other == null || pair.Value < now || other == held) { scratch.Add(other); continue; }
            Vector3 v = GetVelocity(other);
            if (v.magnitude > limit) SetVelocity(other, v.normalized * limit);
            if (other.angularVelocity.magnitude > limit * 2f) other.angularVelocity = other.angularVelocity.normalized * limit * 2f;
        }
        foreach (Rigidbody done in scratch) limitedUntil.Remove(done);
    }

    // ---- Accessibility drop guide: where would the held pixel land if it were let go from a standing position? ----
    private LineRenderer guideLine, guideRing;
    private Material guideMaterial;
    private const int GuideRingPoints = 24;

    private void LateUpdate()
    {
        bool show = held != null && PixelDisplaySettings.DropGuide;
        if (show) show = UpdateDropGuide();
        if (!show && guideLine != null && guideLine.gameObject.activeSelf) { guideLine.gameObject.SetActive(false); guideRing.gameObject.SetActive(false); }
    }

    private LineRenderer MakeGuideRenderer(string objectName, bool loop)
    {
        GameObject go = new GameObject(objectName, typeof(LineRenderer));
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.GetComponent<LineRenderer>();
        lr.sharedMaterial = guideMaterial;
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.alignment = LineAlignment.View;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    /// <summary>Draws a faint line straight down from the held pixel to what it would land on, and a ring there. False when nothing is below it.</summary>
    private bool UpdateDropGuide()
    {
        float half = Mathf.Max(0.02f, held.transform.lossyScale.x * 0.5f);
        RaycastHit[] hits = Physics.SphereCastAll(held.position, half, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        RaycastHit pick = default;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || h.rigidbody == held || h.distance <= 0f) continue;   // (h.distance 0 = already overlapping: no useful surface)
            if (clicker.PixelTransform != null && h.collider.transform.IsChildOf(clicker.PixelTransform)) continue;
            if (h.distance < best) { best = h.distance; pick = h; }
        }
        if (float.IsPositiveInfinity(best)) return false;

        if (guideMaterial == null)
        {
            Shader shader = PixelShaders.SpriteDefault();
            if (shader == null) return false;
            guideMaterial = new Material(shader) { name = "DropGuide" };
        }
        if (guideLine == null)
        {
            guideLine = MakeGuideRenderer("Drop Guide Line", false);
            guideRing = MakeGuideRenderer("Drop Guide Ring", true);
            guideRing.positionCount = GuideRingPoints;
        }
        if (!guideLine.gameObject.activeSelf) { guideLine.gameObject.SetActive(true); guideRing.gameObject.SetActive(true); }

        Vector3 from = held.position + Vector3.down * half;
        Vector3 landing = new Vector3(held.position.x, held.position.y - best, held.position.z);   // the pixel's centre when it touches down
        Vector3 ground = new Vector3(landing.x, pick.point.y, landing.z);
        float width = Mathf.Max(0.02f, half * 0.18f);
        guideLine.startWidth = guideLine.endWidth = width;
        guideLine.startColor = new Color(1f, 1f, 1f, 0.6f);
        guideLine.endColor = new Color(1f, 1f, 1f, 0.25f);
        guideLine.positionCount = 2;
        guideLine.SetPosition(0, from);
        guideLine.SetPosition(1, ground);

        guideRing.startWidth = guideRing.endWidth = width;
        guideRing.startColor = guideRing.endColor = new Color(1f, 1f, 1f, 0.75f);
        Vector3 normal = pick.normal.sqrMagnitude > 0.01f ? pick.normal.normalized : Vector3.up;
        Vector3 tangent = Vector3.Cross(normal, Vector3.right);
        if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(normal, Vector3.forward);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent);
        float radius = half * 1.25f;
        Vector3 centre = ground + normal * (width * 0.6f);
        for (int i = 0; i < GuideRingPoints; i++)
        {
            float a = i / (float)GuideRingPoints * Mathf.PI * 2f;
            guideRing.SetPosition(i, centre + (tangent * Mathf.Cos(a) + bitangent * Mathf.Sin(a)) * radius);
        }
        return true;
    }

    private void FixedUpdate()
    {
        LimitReactions();
        UpdateThrownGrip();
        if (held == null) return;
        if (cam == null) { Release(); return; }

        Ray ray = cam.ScreenPointToRay(PointerPosition());
        Vector3 target;
        if (-ray.direction.y > LevelDragMinDown)
        {
            Plane level = new Plane(Vector3.up, new Vector3(0f, dragLevelY, 0f));
            if (!level.Raycast(ray, out float enterLevel)) return;
            target = ray.GetPoint(enterLevel) + levelOffset + Vector3.up * liftOffset;
            target.y = dragLevelY + liftOffset;
        }
        else
        {
            if (!dragPlane.Raycast(ray, out float enter)) return;
            target = ray.GetPoint(enter) + grabOffset + Vector3.up * liftOffset;
        }

        // Never pushed INTO the ground: the scroll wheel used to lower a held husk below the floor, where it ground against it (crawled after the
        // mouse and played the landing sound over and over). Its bottom stays on the surface under it, and the wheel's lowering stops there too.
        if (TryGroundUnder(target, out float groundY))
        {
            float minY = groundY + HeldHalfHeight() * 1.05f;
            if (target.y < minY) { liftOffset += minY - target.y; target.y = minY; }
        }

        // While time is slowed, velocities are per slowed second: scale so the pixel still follows the mouse at normal speed.
        float slow = PixelTimeStop.SlowFactor;
        Vector3 velocity = (target - held.position) * followSharpness / slow;
        if (velocity.magnitude > maxFollowSpeed / slow) velocity = velocity.normalized * (maxFollowSpeed / slow);
        SetVelocity(held, velocity);
        if (!held.isKinematic) held.angularVelocity *= 1f - spinDamping;
    }

    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    /// <summary>The highest still surface (floor, device, anything without a rigidbody) straight under this point; husks and other moving bodies are skipped.</summary>
    private bool TryGroundUnder(Vector3 point, out float y)
    {
        y = 0f;
        bool found = false;
        int n = Physics.RaycastNonAlloc(point + Vector3.up * 3f, Vector3.down, groundHits, 60f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = groundHits[i];
            if (h.rigidbody != null || h.distance >= best) continue;
            best = h.distance; y = h.point.y; found = true;
        }
        return found;
    }

    private float HeldHalfHeight()
    {
        Collider c = held != null ? held.GetComponent<Collider>() : null;
        return c != null ? c.bounds.extents.y : 0.05f;
    }

    private void Release()
    {
        if (held == null) return;

        Vector3 v = GetVelocity(held) * throwStrength * PixelTimeStop.SlowFactor; // back to what the mouse did
        if (v.magnitude > maxThrowSpeed) v = v.normalized * maxThrowSpeed;
        SetVelocity(held, v);

        OldPixelInfo letGoInfo = held.GetComponent<OldPixelInfo>();
        if (letGoInfo != null) { letGoInfo.heldByPlayer = false; letGoInfo.lastHeldTime = Time.unscaledTime; }
        if (lightUntil.ContainsKey(held)) lightUntil[held] = Time.time + (lightSecondsAfterThrow > 0f ? lightSecondsAfterThrow : 1.5f);   // stays light for a moment after the throw
        if (heldShatter != null) heldShatter.Released(v.magnitude <= gentleReleaseSpeed, gentleLifetimeBonus);
        heldShatter = null;

        if (heldGravity != null) heldGravity.enabled = true;
        held.interpolation = heldWasInterpolation;
        if (!held.isKinematic) held.collisionDetectionMode = heldWasDetection;
        held.freezeRotation = heldWasFreezeRotation;
        held.maxDepenetrationVelocity = heldWasDepenetration;
        StartTumble(held, v);   // after freezeRotation is put back, or the spin would be ignored
        if (heldDespawn != null) { heldDespawn.Held = false; PixelRecycler.KeepIfOver(held); }   // let go over a Recycler: its timer can't run out before the bin takes it
        held = null;
        heldDespawn = null;
        heldGravity = null;
        PixelAudio.Play("drop");
    }

    // ---- Tumbling when thrown: spin along the throw + a little grip so corners catch and tip it over ----
    private struct GripRestore { public Collider col; public UnityEngine.Object original; public float until; }
    private readonly System.Collections.Generic.Dictionary<Rigidbody, GripRestore> thrownGripUntil = new System.Collections.Generic.Dictionary<Rigidbody, GripRestore>();
    private readonly System.Collections.Generic.Dictionary<UnityEngine.Object, UnityEngine.Object> grippyMaterials = new System.Collections.Generic.Dictionary<UnityEngine.Object, UnityEngine.Object>();

    /// <summary>Gives a thrown husk a roll that matches its throw (axis = up x sideways velocity, like a die rolled across a table) and, for a moment, more grip.</summary>
    private void StartTumble(Rigidbody body, Vector3 v)
    {
        if (body == null || body.isKinematic) return;
        Vector3 side = new Vector3(v.x, 0f, v.z);
        if (side.magnitude < minTumbleSpeed || throwSpin <= 0f) return;

        Collider col = body.GetComponent<Collider>();
        float radius = col != null ? Mathf.Max(0.01f, col.bounds.extents.y) : 0.05f;
        Vector3 axis = Vector3.Cross(Vector3.up, side).normalized;
        if (spinWobble > 0f) axis = (axis + Random.insideUnitSphere * spinWobble).normalized;
        float spin = Mathf.Min(side.magnitude / radius * throwSpin, maxThrowSpin);
        if (body.maxAngularVelocity < spin) body.maxAngularVelocity = spin;   // Unity caps spin at 7 rad/s by default
        body.angularVelocity = axis * spin;

        if (thrownGrip > 0f && thrownGripSeconds > 0f && col != null)
        {
            if (!thrownGripUntil.TryGetValue(body, out GripRestore r)) r = new GripRestore { col = col, original = col.sharedMaterial };
            col.sharedMaterial = GrippyCopy(col.sharedMaterial);
            r.until = Time.time + thrownGripSeconds;
            thrownGripUntil[body] = r;
        }
    }

    /// <summary>Puts a thrown husk's own physics material back (grip time over, or it was grabbed again).</summary>
    private void EndThrownGrip(Rigidbody body)
    {
        if (!thrownGripUntil.TryGetValue(body, out GripRestore r)) return;
        if (r.col != null) SetPhysicsMaterial(r.col, r.original);
        thrownGripUntil.Remove(body);
    }

    private void UpdateThrownGrip()
    {
        if (thrownGripUntil.Count == 0) return;
        float now = Time.time;
        scratch.Clear();
        foreach (var pair in thrownGripUntil) if (pair.Key == null || pair.Value.col == null || pair.Value.until < now) scratch.Add(pair.Key);
        foreach (Rigidbody done in scratch)
        {
            if (done != null) EndThrownGrip(done);
            else thrownGripUntil.Remove(done);
        }
    }

#if UNITY_6000_0_OR_NEWER
    /// <summary>A copy of a husk's material with at least thrownGrip friction, combined by Maximum so its own grip wins against slippery floors / husks (cached per original).</summary>
    private PhysicsMaterial GrippyCopy(PhysicsMaterial original)
    {
        UnityEngine.Object key = original != null ? (UnityEngine.Object)original : this;
        float grip = Mathf.Max(thrownGrip, original != null ? original.dynamicFriction : 0f);
        if (grippyMaterials.TryGetValue(key, out UnityEngine.Object cached) && cached != null && Mathf.Approximately(((PhysicsMaterial)cached).dynamicFriction, grip))
            return (PhysicsMaterial)cached;
        PhysicsMaterial m = new PhysicsMaterial((original != null ? original.name : "OldPixel") + "_Thrown")
        {
            bounciness = original != null ? original.bounciness : 0f, dynamicFriction = grip, staticFriction = grip,
            bounceCombine = original != null ? original.bounceCombine : PhysicsMaterialCombine.Average, frictionCombine = PhysicsMaterialCombine.Maximum
        };
        grippyMaterials[key] = m;
        return m;
    }
    private static void SetPhysicsMaterial(Collider col, UnityEngine.Object m) { col.sharedMaterial = (PhysicsMaterial)m; }
#else
    /// <summary>A copy of a husk's material with at least thrownGrip friction, combined by Maximum so its own grip wins against slippery floors / husks (cached per original).</summary>
    private PhysicMaterial GrippyCopy(PhysicMaterial original)
    {
        UnityEngine.Object key = original != null ? (UnityEngine.Object)original : this;
        float grip = Mathf.Max(thrownGrip, original != null ? original.dynamicFriction : 0f);
        if (grippyMaterials.TryGetValue(key, out UnityEngine.Object cached) && cached != null && Mathf.Approximately(((PhysicMaterial)cached).dynamicFriction, grip))
            return (PhysicMaterial)cached;
        PhysicMaterial m = new PhysicMaterial((original != null ? original.name : "OldPixel") + "_Thrown")
        {
            bounciness = original != null ? original.bounciness : 0f, dynamicFriction = grip, staticFriction = grip,
            bounceCombine = original != null ? original.bounceCombine : PhysicMaterialCombine.Average, frictionCombine = PhysicMaterialCombine.Maximum
        };
        grippyMaterials[key] = m;
        return m;
    }
    private static void SetPhysicsMaterial(Collider col, UnityEngine.Object m) { col.sharedMaterial = (PhysicMaterial)m; }
#endif

    private static Vector3 GetVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private static void SetVelocity(Rigidbody rb, Vector3 v)
    {
        if (rb == null || rb.isKinematic) return;   // (a kinematic body cannot be steered: Unity warns)
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}
