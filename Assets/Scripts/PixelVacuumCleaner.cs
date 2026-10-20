// ---- System notes ----
// PixelVacuumCleaner.cs
//   VACUUM CLEANER (a Dev Hub > Tester feature, PixelFeatureTester 'Vacuum Cleaner'; meant to become a consumable): a big canister vacuum on wheels that SUCKS husks
//   up through the wide intake at its front and SPITS them back out of its hose, wherever the mouse points - a way to move husks to a specific place.
//   - Left-click its body to DRIVE it (a teal ring under it; Driving); click it again or press Escape (PixelWindows entry) to stop.
//   - While driving: W A S D or the arrow keys move it over the floor relative to the camera, diagonals included (normalized, driveSpeedCubes 5 model units per
//     second; it turns to face where it goes). The keys are claimed (PixelKeys.ClaimDriveKeys), so S does not slow time and the arrows do not turn the orbital camera.
//   - THE MOUSE ONLY AIMS: the hose's nozzle swings out towards the mouse's point on the floor (at most hoseReachCubes from the body) and points at it.
//   - Every loose husk that comes within intakeRadiusCubes of the intake is sucked in (ReleaseOldPixel without credit: type + value kept in the tank, capacity 25,
//     counter over the body) and AUTOMATICALLY spat out of the nozzle again, one every spitInterval, in an arc that lands on the mouse's point (SpawnStoredPixel).
//     A husk it just spat out is ignored by the intake for a moment (ignoreSpatSeconds).
//   - Sucking and spitting only happen while it is driven (a parked one just stands there). Removing it lets what is in the tank out.
//   The model is built at modelScale (2.2) x a husk's size = one 'model unit'. Moves through a kinematic Rigidbody (MovePosition), so husks in the way are pushed.
//   Saved with the tester save (position, yaw, tank contents).
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

public class PixelVacuumCleaner : MonoBehaviour
{
    [Header("Size and driving")]
    [Min(0.2f)]
    [Tooltip("How big the vacuum is, in husk widths per model unit (bigger = a bigger vacuum, a wider intake and a longer hose).")]
    [SerializeField] private float modelScale = 2.2f;
    [Min(0f)]
    [Tooltip("How fast it drives, in model units per second (a model unit = modelScale husk widths). 0 = the coded default (5).")]
    [SerializeField] private float driveSpeedCubes = 0f;
    [Min(0f)]
    [Tooltip("How fast it turns to face where it drives (degrees per second).")]
    [SerializeField] private float turnSpeed = 720f;

    [Header("Intake and hose")]
    [Min(0f)]
    [Tooltip("How far round the front intake husks are sucked in, in model units. 0 = the coded default (0.9).")]
    [SerializeField] private float intakeRadiusCubes = 0f;
    [Min(0f)]
    [Tooltip("How hard husks are pulled towards the intake, in model units per second. 0 = the coded default (5).")]
    [SerializeField] private float pullSpeedCubes = 0f;
    [Min(0f)]
    [Tooltip("How far from the body the hose's nozzle can reach, in model units. 0 = the coded default (2.2).")]
    [SerializeField] private float hoseReachCubes = 0f;
    [Min(1)]
    [Tooltip("How many husks the tank holds while they wait to be spat out.")]
    [SerializeField] private int capacity = 25;
    [Min(0.01f)]
    [Tooltip("Seconds between two husks spat out of the hose.")]
    [SerializeField] private float spitInterval = 0.12f;
    [Min(0f)]
    [Tooltip("Seconds a husk it just spat out is ignored by the intake.")]
    [SerializeField] private float ignoreSpatSeconds = 2.5f;

    private static readonly List<PixelVacuumCleaner> all = new List<PixelVacuumCleaner>();
    private static PixelVacuumCleaner driven;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); driven = null; }

    /// <summary>The player is driving a Vacuum Cleaner (its W A S D / arrow keys belong to it).</summary>
    public static bool Driving => driven != null && driven.isActiveAndEnabled;

    private float DriveSpeed => (driveSpeedCubes > 0f ? driveSpeedCubes : 5f) * unit;
    private float IntakeRadius => (intakeRadiusCubes > 0f ? intakeRadiusCubes : 0.9f) * unit;
    private float Pull => (pullSpeedCubes > 0f ? pullSpeedCubes : 5f) * unit;
    private float Reach => (hoseReachCubes > 0f ? hoseReachCubes : 2.2f) * unit;

    private struct Stored { public int tier; public double amount; }

    private PixelClicker clicker;
    private PixelConsumables consumables;
    private bool ghost;
    private float unit = 1f;         // one model unit in world units (modelScale husk widths)
    private Rigidbody body;
    private Transform nozzle, outlet, ring;
    private Mesh hoseMesh;
    private TextMeshPro counter;
    private readonly List<Stored> tank = new List<Stored>();
    private readonly List<Collider> ownColliders = new List<Collider>();
    private readonly Dictionary<Rigidbody, float> ignoreUntil = new Dictionary<Rigidbody, float>();
    private Vector3 nozzlePos, aimPoint;
    private bool hasAim;
    private float spitTimer;
    private Vector3 driveTarget;
    private Quaternion driveRot;
    private bool hasDrive, counterDriving;

    public static GameObject CreateGhost(PixelClicker c) { return Build(c, true, "Vacuum Cleaner (Ghost)"); }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        GameObject root = Build(c, false, "Vacuum Cleaner");
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        PixelVacuumCleaner v = root.GetComponent<PixelVacuumCleaner>();
        v.nozzlePos = v.RestPoint();
        v.UpdateNozzle();
        PixelAudio.Play("device_place");
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghostMode, string name)
    {
        GameObject root = new GameObject(name);
        PixelVacuumCleaner v = root.AddComponent<PixelVacuumCleaner>();
        v.clicker = c; v.ghost = ghostMode;
        v.unit = Mathf.Max(0.05f, c.OldPixelWorldSize) * Mathf.Max(0.2f, v.modelScale);
        v.BuildModel();
        if (!ghostMode)
        {
            v.body = root.AddComponent<Rigidbody>();
            v.body.isKinematic = true;
            v.body.useGravity = false;
            v.body.interpolation = RigidbodyInterpolation.Interpolate;
        }
        return root;
    }

    // ------------------------------------------------------------------
    // Model: a canister on wheels with a wide intake at the front, a handle, an outlet, a flexible hose and a nozzle
    // ------------------------------------------------------------------

    private void BuildModel()
    {
        float u = unit;
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color shell = new Color(0.85f, 0.2f, 0.25f, 1f), dark = new Color(0.12f, 0.12f, 0.15f, 1f), chrome = new Color(0.75f, 0.78f, 0.82f, 1f), teal = new Color(0.2f, 0.9f, 0.85f, 1f);
        k.Prim(PrimitiveType.Capsule, "Canister", transform, new Vector3(0f, 0.55f * u, -0.1f * u), new Vector3(1.0f * u, 0.8f * u, 1.0f * u), shell, new Vector3(90f, 0f, 0f), 0.3f, 0.75f, !ghost);
        k.Box("Base", transform, new Vector3(0f, 0.16f * u, -0.1f * u), new Vector3(0.95f * u, 0.14f * u, 1.5f * u), dark, default, false, 0.2f, 0.3f);
        k.Box("Window", transform, new Vector3(0f, 0.93f * u, -0.25f * u), new Vector3(0.45f * u, 0.1f * u, 0.6f * u), new Color(0.3f, 0.35f, 0.42f, 1f), default, false, 0.6f, 0.9f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                k.Cyl("Wheel", transform, new Vector3(sx * 0.5f * u, 0.15f * u, -0.1f * u + sz * 0.5f * u), 0.15f * u, 0.1f * u, dark, new Vector3(0f, 0f, 90f), 0.1f, 0.3f);
        // The intake: a wide floor head across the front with a dark mouth and a glowing strip.
        k.Box("Intake", transform, new Vector3(0f, 0.09f * u, 0.8f * u), new Vector3(1.3f * u, 0.16f * u, 0.3f * u), dark, default, false, 0.3f, 0.4f);
        k.Box("Intake Mouth", transform, new Vector3(0f, 0.05f * u, 0.96f * u), new Vector3(1.15f * u, 0.07f * u, 0.03f * u), Color.black, default, false, 0f, 0.1f);
        k.Box("Intake Glow", transform, new Vector3(0f, 0.18f * u, 0.8f * u), new Vector3(1.2f * u, 0.015f * u, 0.05f * u), teal, default, false, 0f, 0.9f, teal * 0.6f);
        k.Box("Handle Post", transform, new Vector3(0f, 1.08f * u, -0.55f * u), new Vector3(0.09f * u, 0.3f * u, 0.09f * u), dark);
        k.Box("Handle", transform, new Vector3(0f, 1.23f * u, -0.35f * u), new Vector3(0.11f * u, 0.09f * u, 0.45f * u), dark);
        // The outlet the hose comes out of, on top towards the front.
        GameObject o = k.Cyl("Outlet", transform, new Vector3(0f, 1.0f * u, 0.15f * u), 0.12f * u, 0.18f * u, chrome, default, 0.8f, 0.8f);
        outlet = o.transform;

        GameObject r = k.Cyl("Drive Ring", transform, new Vector3(0f, 0.015f * u, 0f), 1.15f * u, 0.012f * u, new Color(teal.r, teal.g, teal.b, 0.5f), default, 0f, 0.9f);
        ring = r.transform;
        ring.gameObject.SetActive(false);

        if (ghost) return;
        // The nozzle lives in the world (not under the body): it swings out towards the mouse.
        GameObject n = new GameObject("Vacuum Nozzle");
        nozzle = n.transform;
        k.Cyl("Wand", nozzle, new Vector3(0f, 0f, 0.15f * u), 0.07f * u, 0.5f * u, chrome, new Vector3(90f, 0f, 0f), 0.85f, 0.8f);
        k.Cyl("Tip", nozzle, new Vector3(0f, 0f, 0.42f * u), 0.1f * u, 0.06f * u, dark, new Vector3(90f, 0f, 0f), 0.3f, 0.4f);

        GameObject hose = new GameObject("Vacuum Hose");
        hose.transform.SetParent(transform, false);
        MeshRenderer hr = hose.AddComponent<MeshRenderer>();
        hr.sharedMaterial = k.Mat(new Color(0.22f, 0.22f, 0.26f, 1f), 0.1f, 0.5f);
        hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hoseMesh = new Mesh { name = "Vacuum Hose" };
        hose.AddComponent<MeshFilter>().sharedMesh = hoseMesh;

        GameObject cg = new GameObject("Counter");
        cg.transform.SetParent(transform, false);
        cg.transform.localPosition = new Vector3(0f, 1.75f * u, 0f);
        counter = cg.AddComponent<TextMeshPro>();
        counter.fontSize = 2.2f * u;
        counter.fontStyle = FontStyles.Bold;
        counter.alignment = TextAlignmentOptions.Center;
        counter.color = new Color(0.9f, 0.95f, 1f, 1f);
        if (clicker != null && clicker.UIFont != null) counter.font = clicker.UIFont;
        RefreshCounter();
    }

    private void Start()
    {
        if (ghost) return;
        GetComponentsInChildren(true, ownColliders);
        PixelWindows.Register(this, 205, () => driven == this, StopDriving);   // Escape stops driving   // escape-only: stops driving
    }

    private void Awake() => PixelHuskPool.HuskReleasing += OnHuskReleasing;
    // (PixelHuskPool: the husk is going away and may be reused - let go of it)
    private void OnHuskReleasing(Rigidbody body) => ignoreUntil.Remove(body);

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); if (driven == this) driven = null; }

    private void OnDestroy()
    {
        PixelHuskPool.HuskReleasing -= OnHuskReleasing;
        if (ghost) return;
        PixelWindows.Unregister(this);
        if (driven == this) driven = null;
        if (gameObject.scene.isLoaded) EmptyTank();   // what was in the tank comes out where it stands (not while the scene is closing)
        if (nozzle != null) Destroy(nozzle.gameObject);
        if (hoseMesh != null) Destroy(hoseMesh);
    }

    private void StopDriving() { if (driven == this) driven = null; }

    // ------------------------------------------------------------------
    // Every frame
    // ------------------------------------------------------------------

    private Camera Cam => PixelClicker.ViewCameraOf(clicker);

    private Vector3 RestPoint() => transform.TransformPoint(new Vector3(0.75f * unit, 0.5f * unit, 0.2f * unit));
    private Vector3 IntakePoint => transform.TransformPoint(new Vector3(0f, 0.12f * unit, 1.0f * unit));

    private void Update()
    {
        if (ghost || clicker == null) return;
        Camera cam = Cam;
        bool can = cam != null && !PixelCalm.InMenu && Time.timeScale > 0f && !PixelFirstPerson.Active && !PixelFreeCam.Active;   // (first person / free cam use W A S D themselves)
        if (!can && driven == this) driven = null;
        if (ring != null && ring.gameObject.activeSelf != (driven == this)) ring.gameObject.SetActive(driven == this);
        if (counter != null && cam != null) counter.transform.rotation = Quaternion.LookRotation(counter.transform.position - cam.transform.position, cam.transform.up);
        if (counterDriving != (driven == this)) { counterDriving = driven == this; RefreshCounter(); }
        if (!can) { hasAim = false; UpdateNozzle(); return; }

        // Click the body: drive it / stop driving.
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        bool overMe = !PixelInput.PointerOverUI() && OverBody(ray);
        if (overMe) PixelPointerClaim.Claim();
        if (overMe && PixelInput.LeftPressed() && !PixelFeatureTester.Placing)
        {
            driven = driven == this ? null : this;
            PixelAudio.Play("ui_click");
        }

        if (driven == this)
        {
            PixelKeys.ClaimDriveKeys();
            Drive();
            hasAim = TryFloor(out aimPoint);   // the mouse only aims the hose
            if (tank.Count > 0 && hasAim)
            {
                spitTimer -= Time.deltaTime;
                if (spitTimer <= 0f) { spitTimer = spitInterval; SpitOne(); }
            }
        }
        else hasAim = false;
        UpdateNozzle();
    }

    private bool OverBody(Ray ray)
    {
        foreach (Collider c in ownColliders) if (c != null && c.enabled && c.Raycast(ray, out _, 1000f)) return true;
        return false;
    }

    private bool TryFloor(out Vector3 point)
    {
        point = default;
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        if (consumables != null && consumables.TryGetFloorPoint(PixelInput.PointerPosition(), out point)) return true;
        Camera cam = Cam;
        if (cam == null) return false;
        Plane plane = new Plane(Vector3.up, transform.position);
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        if (!plane.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    /// <summary>W A S D / arrows, relative to the camera's view, diagonals normalized; it turns to face where it goes.</summary>
    private void Drive()
    {
        float x = (PixelKeys.KeyHeld(KeyCode.D) || PixelKeys.KeyHeld(KeyCode.RightArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.A) || PixelKeys.KeyHeld(KeyCode.LeftArrow) ? 1f : 0f);
        float z = (PixelKeys.KeyHeld(KeyCode.W) || PixelKeys.KeyHeld(KeyCode.UpArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.S) || PixelKeys.KeyHeld(KeyCode.DownArrow) ? 1f : 0f);
        Vector2 input = new Vector2(x, z);
        if (input.sqrMagnitude < 0.01f) { hasDrive = false; return; }
        input = input.normalized;
        Camera cam = Cam;
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.up + cam.transform.forward, Vector3.up) : Vector3.forward;   // (works for a top-down camera too)
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 dir = (right * input.x + fwd * input.y).normalized;
        Vector3 next = transform.position + dir * DriveSpeed * Time.deltaTime;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(next);
            if (vp.z <= 0f || vp.x < 0.03f || vp.x > 0.97f || vp.y < 0.08f || vp.y > 0.92f) { hasDrive = false; return; }   // never off the screen
        }
        driveTarget = next;
        driveRot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), turnSpeed * Time.deltaTime);
        hasDrive = true;
    }

    private void FixedUpdate()
    {
        if (ghost || body == null) return;
        if (hasDrive) { body.MovePosition(driveTarget); body.MoveRotation(driveRot); }
        if (driven == this) Suck();
    }

    /// <summary>The nozzle swings out towards the aim point (within reach) and points at it; at rest it hangs beside the body.</summary>
    private void UpdateNozzle()
    {
        if (nozzle == null) return;
        Vector3 want, look;
        if (hasAim)
        {
            Vector3 from = transform.position, to = aimPoint - from; to.y = 0f;
            float dist = Mathf.Min(Reach, Mathf.Max(0.6f * unit, to.magnitude - 0.3f * unit));
            Vector3 flatDir = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
            want = from + flatDir * dist + Vector3.up * 0.9f * unit;
            look = aimPoint - want;
        }
        else
        {
            want = RestPoint();
            look = transform.forward + Vector3.down * 0.6f;
        }
        nozzlePos = Vector3.Lerp(nozzlePos, want, 1f - Mathf.Exp(-12f * Time.deltaTime));
        nozzle.position = nozzlePos;
        if (look.sqrMagnitude > 0.0001f) nozzle.rotation = Quaternion.Slerp(nozzle.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), 1f - Mathf.Exp(-14f * Time.deltaTime));
        UpdateHose();
    }

    /// <summary>The hose: up out of the outlet, sagging, into the back of the nozzle's wand.</summary>
    private void UpdateHose()
    {
        if (hoseMesh == null || outlet == null || nozzle == null) return;
        Vector3 a = outlet.position + Vector3.up * 0.08f * unit;
        Vector3 b = nozzle.TransformPoint(new Vector3(0f, 0f, -0.1f * unit));
        float dist = Vector3.Distance(a, b);
        Vector3 c1 = a + Vector3.up * Mathf.Max(0.3f * unit, dist * 0.35f);
        Vector3 c2 = b - nozzle.forward * Mathf.Max(0.3f * unit, dist * 0.35f) + Vector3.down * 0.2f * unit;
        const int n = 18;
        Vector3[] pts = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1), m = 1f - t;
            Vector3 p = m * m * m * a + 3f * m * m * t * c1 + 3f * m * t * t * c2 + t * t * t * b;
            p.y = Mathf.Max(p.y, transform.position.y + 0.08f * unit);
            pts[i] = transform.InverseTransformPoint(p);
        }
        PixelTube.Build(pts, 0.07f * unit, 10, hoseMesh);
        hoseMesh.RecalculateBounds();
    }

    // ------------------------------------------------------------------
    // Sucking in (the front intake) and spitting out (the hose)
    // ------------------------------------------------------------------

    private void Suck()
    {
        if (clicker == null || tank.Count >= capacity) return;
        Vector3 mouth = IntakePoint;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        float r = IntakeRadius;
        for (int i = pixels.Count - 1; i >= 0 && tank.Count < capacity; i--)
        {
            Rigidbody rb = pixels[i];
            if (!PixelHuskHolder.Takeable(clicker, rb, true) || PixelAttachment.IsAttached(rb) || PixelPathCarrier.IsCarried(rb)) continue;   // (one owner per husk)
            if (ignoreUntil.TryGetValue(rb, out float until) && Time.time < until) continue;
            PixelHuskQuery.Husk husk = PixelHuskQuery.Get(rb);
            OldPixelInfo info = husk.info;
            OldPixelDespawn despawn = husk.despawn;
            Vector3 to = mouth - rb.position;
            float d = to.magnitude;
            if (d > r) continue;
            if (d < 0.3f * unit)
            {
                if (!clicker.ReleaseOldPixel(rb, false)) continue;
                tank.Add(new Stored { tier = info.tierIndex, amount = info.amount });
                StartCoroutine(SuckIn(rb.gameObject));
                RefreshCounter();
                continue;
            }
            float k = 1f - Mathf.Clamp01(d / r);
            if (despawn != null) despawn.KeepAlive();
            PixelPathCarrier.SetVelocity(rb, Vector3.Lerp(PixelBody.Velocity(rb), to.normalized * Pull * (0.4f + 0.6f * k), 0.3f + 0.4f * k));
        }
        if (ignoreUntil.Count > 80) ignoreUntil.Clear();
    }


    /// <summary>A sucked-up husk shrinks into the intake and is gone (its type and value are in the tank).</summary>
    private IEnumerator SuckIn(GameObject go)
    {
        if (go == null) yield break;
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>()) if (mb != null) mb.enabled = false;
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        Vector3 start = go.transform.position, scale = go.transform.localScale;
        float t = 0f;
        while (t < 0.15f)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            float e = Mathf.Clamp01(t / 0.15f);
            go.transform.position = Vector3.Lerp(start, IntakePoint, e);
            go.transform.localScale = scale * (1f - e);
            yield return null;
        }
        if (go != null) Destroy(go);
        PixelAudio.Play("click");
    }

    /// <summary>Spits the oldest husk in the tank out of the nozzle in an arc that lands on the aim point.</summary>
    private void SpitOne()
    {
        if (tank.Count == 0 || clicker == null || nozzle == null) return;
        Stored s = tank[0];
        tank.RemoveAt(0);
        Vector3 from = nozzle.TransformPoint(new Vector3(0f, 0f, 0.5f * unit));
        Vector3 flat = aimPoint + Vector3.up * clicker.OldPixelWorldSize * 0.5f - from;
        float up = flat.y; flat.y = 0f;
        float time = Mathf.Clamp(flat.magnitude / Mathf.Max(1f, 3f * unit), 0.18f, 1f);
        float g = Mathf.Abs(Physics.gravity.y);
        Vector3 v = flat / time + Vector3.up * ((up + 0.5f * g * time * time) / time);   // (lands on the aim point)
        if (clicker.SpawnStoredPixel(s.tier, s.amount, from, v))
        {
            IReadOnlyList<Rigidbody> list = clicker.OldPixels;
            if (list.Count > 0 && list[list.Count - 1] != null) ignoreUntil[list[list.Count - 1]] = Time.time + ignoreSpatSeconds;   // (not straight back in)
        }
        RefreshCounter();
    }

    private void EmptyTank()
    {
        if (clicker == null) return;
        Vector3 at = transform.position + Vector3.up * 1.2f * unit;
        foreach (Stored s in tank)
            clicker.SpawnStoredPixel(s.tier, s.amount, at, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 3f), Random.Range(-1.5f, 1.5f)));
        tank.Clear();
    }

    private void RefreshCounter()
    {
        if (counter == null) return;
        counter.text = tank.Count + " / " + capacity + (driven == this ? "" : "\n<size=55%>click to drive</size>");
    }

    // ------------------------------------------------------------------
    // Saving (PixelFeatureTester)
    // ------------------------------------------------------------------

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw; public string tank; }

    public static string ExportState(GameObject root)
    {
        PixelVacuumCleaner v = root != null ? root.GetComponent<PixelVacuumCleaner>() : null;
        if (v == null) return null;
        StringBuilder sb = new StringBuilder();
        foreach (Stored s in v.tank) sb.Append(s.tier).Append(',').Append(s.amount.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        return JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y, tank = sb.ToString() });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        GameObject root = Place(c, s.position, s.yaw);
        PixelVacuumCleaner v = root.GetComponent<PixelVacuumCleaner>();
        if (!string.IsNullOrEmpty(s.tank))
            foreach (string part in s.tank.Split(';'))
            {
                string[] f = part.Split(',');
                if (f.Length < 2 || !int.TryParse(f[0], out int tier) || !double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)) continue;
                if (c.IsValidTierIndex(tier) && v.tank.Count < v.capacity) v.tank.Add(new Stored { tier = tier, amount = amount });
            }
        v.RefreshCounter();
        return root;
    }
}
