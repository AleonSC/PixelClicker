// ---- System notes ----
// PixelCarryHandles.cs
//   Shared carrying handles for placed things (first used by the Storage Crate, now also the Recycler):
//   - BuildParts(part, halfWidth, depth, y, size, opacity): the model - on each short (+/-X) end two arms and a grip bar, all parts named "Handle",
//     made through the caller's own 'part' callback (so each model keeps its own materials / PixelFactoryKit). 'size' = how far they stick out.
//   - The component (AddComponent on the root, then Init): finds the child renderers named "Handle"; hovering one shows the open-hand cursor
//     (PixelHandCursor), left-drag moves the root over the floor plane (kept on screen) with the fist cursor, scroll / Q / E turn it while dragging
//     (PixelConsumables.TurnInput). Over(ray) / Dragging tell the owner the pointer is the handles'; static Hot (hovering or dragging any handles)
//     is part of PixelClicker.ExternalClickBlock and PixelGrab's guard. 'Blocked' (a Func the owner sets) pauses it, e.g. while the Recycler works.
//   While carried, the device's coupling holes shine red AIM BEAMS (PixelAimLaser.Ports) so it lines up exactly.
//   The clicker cube wins: a handle hidden behind the cube (the cube nearer along the mouse ray, CubeInFront) is not hovered or grabbed.
//   Moves go through a kinematic Rigidbody on the root (MovePosition / MoveRotation in FixedUpdate) so loose husks are pushed, not passed through; it follows the mouse tightly (followSharpness 25 = closes the gap fast, capped at carrySpeedLimit 60 u/s; the old 7 u/s cap made it crawl and keep sliding after the drag) and husks it shoves are slowed to pushedSpeedCap (2.5 u/s) so they slide aside instead of flying. The position / yaw are the root's own, so the saved PlacedState picks up where it was carried to.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Two grip handles on a placed thing's short ends: hover for the open hand, left-drag to carry it over the floor, scroll / Q / E to turn it while carrying.</summary>
public class PixelCarryHandles : MonoBehaviour
{
    [Tooltip("How close to the screen edges (viewport share) the carried thing may go: it is never dragged off the screen.")]
    [SerializeField] private Vector2 screenMargin = new Vector2(0.04f, 0.1f);

    private static int hotFrame = -10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { hotFrame = -10; }

    /// <summary>True while the pointer is over (or dragging by) any carry handles: clicks there belong to the handles, not the cube or Pixel Grabbing.</summary>
    public static bool Hot => hotFrame >= Time.frameCount - 1;

    /// <summary>The owner can pause the handles (true = no hover, no drag), e.g. while a machine is busy.</summary>
    public Func<bool> Blocked;

    /// <summary>True while the player is carrying this by a handle.</summary>
    public bool Dragging => dragging;

    private readonly List<Renderer> handles = new List<Renderer>();
    private Func<Camera> cameraSource;
    private PixelConsumables consumables;
    private bool dragging;
    private Vector3 dragOffset;
    private Rigidbody body;
    private bool hasTarget;
    private Vector3 targetPos;
    private Quaternion targetRot;

    // (Renamed from maxCarrySpeed: its old 7 u/s made the carried thing crawl after the mouse and keep sliding after a quick drag.)
    [Tooltip("How snappily the carried thing follows the mouse: it closes this share of the gap per second (bigger = tighter).")]
    [SerializeField] private float followSharpness = 25f;
    [Tooltip("Fastest the carried thing may move (units per second), so a fling of the mouse can't throw it through things.")]
    [SerializeField] private float carrySpeedLimit = 60f;
    [Tooltip("Husks shoved by the carried thing are slowed to at most this speed (units per second), so they slide aside instead of flying off.")]
    [SerializeField] private float pushedSpeedCap = 2.5f;

    private readonly Collider[] nearby = new Collider[64];

    private void FixedUpdate()
    {
        if (body == null) return;
        if (hasTarget)
        {
            // Follow the mouse tightly: close most of the gap each step (a far target moves fast, a near one settles), capped at carrySpeedLimit.
            float dist = Vector3.Distance(body.position, targetPos);
            float speed = Mathf.Min(Mathf.Max(0.5f, carrySpeedLimit), Mathf.Max(2f, dist * Mathf.Max(1f, followSharpness)));
            float step = speed * Time.fixedDeltaTime;
            Vector3 next = Vector3.MoveTowards(body.position, targetPos, step);
            body.MovePosition(next);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, targetRot, 360f * Time.fixedDeltaTime));
            if ((next - targetPos).sqrMagnitude < 0.000001f && Quaternion.Angle(body.rotation, targetRot) < 0.1f) hasTarget = false;
        }
        if (dragging || hasTarget) CalmPushed();
    }

    /// <summary>Loose rigidbodies (husks) next to the carried thing are slowed down, so being shoved never flings them across the floor.</summary>
    private void CalmPushed()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c == null || c.isTrigger) continue;
            if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
        }
        if (!any) return;
        b.Expand(0.6f);
        int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, nearby, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        float cap = Mathf.Max(0.1f, pushedSpeedCap);
        for (int i = 0; i < n; i++)
        {
            Rigidbody rb = nearby[i] != null ? nearby[i].attachedRigidbody : null;
            if (rb == null || rb == body || rb.isKinematic) continue;
#if UNITY_6000_0_OR_NEWER
            if (rb.linearVelocity.sqrMagnitude > cap * cap) rb.linearVelocity = rb.linearVelocity.normalized * cap;
#else
            if (rb.velocity.sqrMagnitude > cap * cap) rb.velocity = rb.velocity.normalized * cap;
#endif
            if (rb.angularVelocity.sqrMagnitude > 25f) rb.angularVelocity = rb.angularVelocity.normalized * 5f;
        }
    }

    private Vector3 CurrentPos => hasTarget ? targetPos : transform.position;
    private Quaternion CurrentRot => hasTarget ? targetRot : transform.rotation;

    private void MoveTo(Vector3 pos, Quaternion rot)
    {
        if (body == null) { transform.SetPositionAndRotation(pos, rot); return; }
        targetPos = pos; targetRot = rot; hasTarget = true;
    }

    /// <summary>Builds the handle parts: on each short (+/-X) end two arms and a grip bar, named "Handle". 'halfWidth' = where the end faces are,
    /// 'depth' = the thing's depth along Z, 'y' = the height of the handles, 'size' = how far they stick out (the Storage Crate uses 0.34).</summary>
    public static void BuildParts(Action<string, Vector3, Vector3, Color> part, float halfWidth, float depth, float y, float size, float opacity = 1f)
    {
        Color grip = new Color(0.25f, 0.25f, 0.3f, opacity), gripLight = new Color(0.7f, 0.72f, 0.78f, opacity);
        float arm = size * 0.206f, bar = size * 0.265f;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            float mid = sx * (halfWidth + size * 0.5f), outer = sx * (halfWidth + size);
            part("Handle", new Vector3(mid, y, -depth * 0.22f), new Vector3(size, arm, arm), grip);
            part("Handle", new Vector3(mid, y, depth * 0.22f), new Vector3(size, arm, arm), grip);
            part("Handle", new Vector3(outer, y, 0f), new Vector3(bar, bar, depth * 0.44f + bar), gripLight);
        }
    }

    /// <summary>Starts working: finds the "Handle" parts under this object. 'cam' gives the camera to use (the clicker's).</summary>
    public void Init(Func<Camera> cam)
    {
        cameraSource = cam;
        // A kinematic Rigidbody on the root: the carried thing is moved through the physics engine (MovePosition / MoveRotation), so loose husks
        // are pushed aside instead of the colliders teleporting into them (that let husks glitch through the walls).
        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;   // (smoothed only while carried: otherwise the owner may move the transform itself, e.g. the Recycler's shake)
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        handles.Clear();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r.name == "Handle") handles.Add(r);
    }

    /// <summary>Is this ray on one of the handles (and not hidden behind the clicker cube)?</summary>
    public bool Over(Ray ray)
    {
        float nearest = float.MaxValue;
        foreach (Renderer r in handles)
            if (r != null && r.enabled && r.bounds.IntersectRay(ray, out float d) && d < nearest) nearest = d;
        return nearest < float.MaxValue && !CubeInFront(ray, nearest);
    }

    /// <summary>Is the clicker cube (its full-size trigger hitbox) on the ray nearer than 'distance'? Then the click is the cube's, not a handle's behind it.</summary>
    public static bool CubeInFront(Ray ray, float distance)
    {
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        Transform cube = clicker != null ? clicker.PixelTransform : null;
        if (cube == null) return false;
        foreach (RaycastHit h in Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Collide))
            if (h.transform == cube || h.transform.IsChildOf(cube)) return true;
        return false;
    }

    private Camera Cam => cameraSource != null && cameraSource() != null ? cameraSource() : PixelClicker.ViewCameraOf(PixelFind.Cached<PixelClicker>());

    private IPixelSocketDevice socketDevice;   // (the carried device's sockets: its aim beams)

    private PixelAimOpening[] aimOpenings;

    private void Update()
    {
        if (handles.Count == 0) return;
        Camera cam = Cam;
        bool blocked = Blocked != null && Blocked();
        if (cam == null || blocked || Time.timeScale <= 0f || PixelGameMode.PauseMenuOpen) { if (dragging) StopDrag(); return; }

        if (dragging)
        {
            if (!PixelInput.LeftHeld()) { StopDrag(); return; }
            hotFrame = PixelPointerClaim.Claim();
            // Red aim beams out of the device's coupling holes while it is carried (PixelAimLaser), so it lines up exactly.
            if (socketDevice == null) socketDevice = GetComponent<IPixelSocketDevice>();
            if (socketDevice != null) PixelAimLaser.Ports(socketDevice.SocketRoot, socketDevice.Sockets);
            if (aimOpenings == null) aimOpenings = GetComponentsInChildren<PixelAimOpening>(true);   // (its marked holes: a wide beam each - found once)
            PixelAimLaser.Openings(transform, aimOpenings);
            PixelHandCursor.Request(PixelHandCursor.Shape.Closed);
            if (consumables == null) consumables = PixelFind.Cached<PixelConsumables>();
            float turn = consumables != null ? consumables.TurnInput() : 0f;
            if (!Mathf.Approximately(turn, 0f))
            {
                MoveTo(CurrentPos, Quaternion.Euler(0f, turn, 0f) * CurrentRot);
                dragOffset = Quaternion.Euler(0f, turn, 0f) * dragOffset;   // the grabbed point swings round the centre with it
            }
            DragTo(cam, cam.ScreenPointToRay(PixelInput.PointerPosition()));
            return;
        }
        if (PixelInput.PointerOverUI()) return;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        if (!Over(ray)) return;
        hotFrame = PixelPointerClaim.Claim();
        PixelHandCursor.Request(PixelHandCursor.Shape.Open);
        if (PixelInput.LeftPressed() && FloorPoint(ray, out Vector3 hit))
        {
            dragging = true;
            dragOffset = CurrentPos - hit;
            if (body != null) body.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void StopDrag()
    {
        dragging = false;
        if (body != null) body.interpolation = RigidbodyInterpolation.None;
    }

    private bool FloorPoint(Ray ray, out Vector3 point)
    {
        point = default;
        Plane plane = new Plane(Vector3.up, CurrentPos);
        if (!plane.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    private void DragTo(Camera cam, Ray ray)
    {
        if (!FloorPoint(ray, out Vector3 hit)) return;
        Vector3 target = hit + dragOffset;
        target.y = CurrentPos.y;
        Vector3 vp = cam.WorldToViewportPoint(target);
        if (vp.z <= 0f || vp.x < screenMargin.x || vp.x > 1f - screenMargin.x || vp.y < screenMargin.y || vp.y > 1f - screenMargin.y) return;   // never off the screen
        MoveTo(target, CurrentRot);
    }
}
