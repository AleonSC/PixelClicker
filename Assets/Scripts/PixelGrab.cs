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
            if (!LeftHeld() || Time.timeScale <= 0f) Release();
            return;
        }

        if (Time.timeScale <= 0f || PixelPauseMenu.IsPaused || PixelBank.HoseOn || PixelClicker.GodMode || !LeftPressed() || PointerOverUI()) return;
        TryGrab();
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
        clicker.LandFlyingPixel(body); // a caught meteor becomes an ordinary old pixel (solid, falls when let go)
        held = body;
        heldDespawn = despawn;
        if (heldDespawn != null) heldDespawn.Held = true; // frozen lifetime while held

        heldGravity = body.GetComponent<ScaledGravity>();
        if (heldGravity != null) heldGravity.enabled = false;

        // Drag on the plane facing the camera that passes through the pixel.
        dragPlane = new Plane(-cam.transform.forward, body.position);
        grabOffset = dragPlane.Raycast(ray, out float enter) ? body.position - ray.GetPoint(enter) : Vector3.zero;

        heldShatter = body.GetComponent<OldPixelShatter>();
        if (heldShatter != null) heldShatter.Grabbed(); // can't break while carried

        PixelAudio.Play("grab");
    }

    private OldPixelShatter heldShatter;

    private void FixedUpdate()
    {
        if (held == null) return;
        if (cam == null) { Release(); return; }

        Ray ray = cam.ScreenPointToRay(PointerPosition());
        if (!dragPlane.Raycast(ray, out float enter)) return;
        Vector3 target = ray.GetPoint(enter) + grabOffset;

        // While time is slowed, velocities are per slowed second: scale so the pixel still follows the mouse at normal speed.
        float slow = PixelTimeStop.SlowFactor;
        Vector3 velocity = (target - held.position) * followSharpness / slow;
        if (velocity.magnitude > maxFollowSpeed / slow) velocity = velocity.normalized * (maxFollowSpeed / slow);
        SetVelocity(held, velocity);
        held.angularVelocity *= 1f - spinDamping;
    }

    private void Release()
    {
        if (held == null) return;

        Vector3 v = GetVelocity(held) * throwStrength * PixelTimeStop.SlowFactor; // back to what the mouse did
        if (v.magnitude > maxThrowSpeed) v = v.normalized * maxThrowSpeed;
        SetVelocity(held, v);

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
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}
