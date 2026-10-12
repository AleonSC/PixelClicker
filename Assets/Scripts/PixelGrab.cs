using UnityEngine;
using static PixelInput;

/// <summary>
/// Pixel Grabbing (bought as an upgrade in the shop). With it, you can press the left mouse button on an old pixel,
/// drag it around the screen (it keeps colliding with other old pixels and the floor) and let go to drop or throw it.
/// A grabbed pixel doesn't despawn while you hold it. A click on an old pixel no longer falls through to the cube behind.
///
/// Add this to any GameObject. PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelGrab : MonoBehaviour
{
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

    [Min(1f)]
    [Tooltip("How tightly a held pixel follows the mouse (higher = snappier, lower = floaty).")]
    [SerializeField] private float followSharpness = 18f;

    [Min(1f)]
    [Tooltip("Fastest a held pixel can move (world units per second), so it can't smash through things.")]
    [SerializeField] private float maxFollowSpeed = 40f;

    [Range(0f, 1f)]
    [Tooltip("How quickly a held pixel stops spinning (0 = keeps spinning, 1 = stops at once).")]
    [SerializeField] private float spinDamping = 0.2f;

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
            float wheel = PixelInput.ScrollY();
            if (wheel != 0f)
            {
                float cap = maxWheelLift > 0f ? maxWheelLift : 6f;
                liftOffset = Mathf.Clamp(liftOffset - Mathf.Sign(wheel) * Mathf.Min(Mathf.Abs(wheel), 3f) * (wheelLift > 0f ? wheelLift : 0.4f), -cap, cap);
            }
            return;
        }

        if (Time.timeScale <= 0f || PixelPauseMenu.IsPaused || PixelBank.HoseOn || PixelRobotWorker.Piloting || PixelMinigame.TakeoverActive || PixelClicker.GodMode || PixelPets.HoveringPet || !LeftPressed() || PointerOverUI() || PixelStorageCrate.ClaimsPointer()) return;
        TryGrab();
    }

    /// <summary>The old pixel being dragged right now (null when none).</summary>
    public Rigidbody HeldBody => held;

    /// <summary>Puts an old pixel straight into the mouse's hand (the left button must be held), as if it had just been grabbed. Used by the Storage Crate.</summary>
    public void BeginHold(Rigidbody body)
    {
        if (!Working || clicker == null || held != null || body == null) return;
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;
        Grab(body, body.GetComponent<OldPixelDespawn>(), cam.ScreenPointToRay(PointerPosition()));
    }

    private void TryGrab()
    {
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
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
            if (body == null || !IsOldPixel(body)) continue;
            if (clicker.IsFlyingPixel(body) && !PixelTimeStop.IsSlowed) continue; // a flying meteor can only be caught while time is slowed

            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && despawn.IsDespawning) continue; // already vanishing

            // The cube only wins when the cursor is really on it and it is in front of this pixel under the cursor.
            // A pixel beside the cube (the cursor not on the cube) is always grabbable, even right up against it.
            float pixelExact = float.PositiveInfinity;
            foreach (RaycastHit h in exact)
                if (h.rigidbody == body && h.distance < pixelExact) pixelExact = h.distance;
            if (cubeExact < pixelExact) return;
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
        clicker.LandFlyingPixel(body); // a caught meteor becomes an ordinary old pixel (solid, falls when let go)
        held = body;
        liftOffset = 0f;
        heldDespawn = despawn;
        if (heldDespawn != null) heldDespawn.Held = true; // frozen lifetime while held

        heldGravity = body.GetComponent<ScaledGravity>();
        if (heldGravity != null) heldGravity.enabled = false;

        // Drag on the plane facing the camera that passes through the pixel.
        dragPlane = new Plane(-cam.transform.forward, body.position);
        grabOffset = dragPlane.Raycast(ray, out float enter) ? body.position - ray.GetPoint(enter) : Vector3.zero;

        heldShatter = body.GetComponent<OldPixelShatter>();
        if (heldShatter != null) heldShatter.Grabbed(); // can't break while carried

        if (!disableGentleHits) MakeLight(body);

        PixelAudio.Play("grab");
    }

    private OldPixelShatter heldShatter;

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

    private void FixedUpdate()
    {
        LimitReactions();
        if (held == null) return;
        if (cam == null) { Release(); return; }

        Ray ray = cam.ScreenPointToRay(PointerPosition());
        if (!dragPlane.Raycast(ray, out float enter)) return;
        Vector3 target = ray.GetPoint(enter) + grabOffset + Vector3.up * liftOffset;

        // While time is slowed, velocities are per slowed second: scale so the pixel still follows the mouse at normal speed.
        float slow = PixelTimeStop.SlowFactor;
        Vector3 velocity = (target - held.position) * followSharpness / slow;
        if (velocity.magnitude > maxFollowSpeed / slow) velocity = velocity.normalized * (maxFollowSpeed / slow);
        SetVelocity(held, velocity);
        if (!held.isKinematic) held.angularVelocity *= 1f - spinDamping;
    }

    private void Release()
    {
        if (held == null) return;

        Vector3 v = GetVelocity(held) * throwStrength * PixelTimeStop.SlowFactor; // back to what the mouse did
        if (v.magnitude > maxThrowSpeed) v = v.normalized * maxThrowSpeed;
        SetVelocity(held, v);

        if (lightUntil.ContainsKey(held)) lightUntil[held] = Time.time + (lightSecondsAfterThrow > 0f ? lightSecondsAfterThrow : 1.5f);   // stays light for a moment after the throw
        if (heldShatter != null) heldShatter.Released(v.magnitude <= gentleReleaseSpeed, gentleLifetimeBonus);
        heldShatter = null;

        if (heldGravity != null) heldGravity.enabled = true;
        if (heldDespawn != null) heldDespawn.Held = false;
        held = null;
        heldDespawn = null;
        heldGravity = null;
        PixelAudio.Play("drop");
    }

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
