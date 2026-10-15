// ---- System notes ----
// PixelVacuumCleaner.cs
//   VACUUM CLEANER (a Dev Hub > Tester feature, PixelFeatureTester 'Vacuum Cleaner'; meant to become a consumable): a canister vacuum on wheels with a flexible hose and a
//   floor nozzle, for sucking husks up and putting them down somewhere specific.
//   - Left-click its body to DRIVE it (a teal ring under it; Driving). While driving: W A S D or the arrow keys move it over the floor relative to the camera, diagonals
//     included (normalized, driveSpeedCubes 4 cube widths / s, it turns to face where it goes); the keys are claimed (PixelKeys.ClaimDriveKeys) so S does not slow
//     time and the arrows do not turn the orbital camera; cube clicks and Pixel Grabbing step aside (PixelPointerClaim, PixelGrab's guard).
//   - HOLD the left button: the nozzle leaves its rest beside the body and follows the mouse's point on the floor (within hoseReachCubes 4 of the body); every loose
//     husk within suctionRadiusCubes 1.3 of the nozzle head is pulled in and, at the mouth, sucked into the tank (ReleaseOldPixel without credit: type + value kept,
//     up to capacity 25; a counter over the body shows n / 25).
//   - HOLD the right button (not on the vacuum itself - holding right on it deletes it like any tester object): the tank blows its husks back out of the nozzle, one
//     every blowInterval, towards the mouse's point (SpawnStoredPixel) - drop them in a Recycler, a Storage Crate, a black hole...
//   - Left-click the body again or press Escape (PixelWindows entry) to stop driving. Removing it lets everything in the tank out.
//   Moves through a kinematic Rigidbody (MovePosition), so husks in the way are pushed. Saved with the tester save (position, yaw, tank contents).
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

public class PixelVacuumCleaner : MonoBehaviour
{
    [Header("Driving")]
    [Min(0f)]
    [Tooltip("How fast it drives, in cube widths per second. 0 = the coded default (4).")]
    [SerializeField] private float driveSpeedCubes = 0f;
    [Min(0f)]
    [Tooltip("How fast it turns to face where it drives (degrees per second).")]
    [SerializeField] private float turnSpeed = 540f;

    [Header("Hose and suction")]
    [Min(0f)]
    [Tooltip("How far from the body the nozzle can reach, in cube widths. 0 = the coded default (4).")]
    [SerializeField] private float hoseReachCubes = 0f;
    [Min(0f)]
    [Tooltip("How far round the nozzle head husks are pulled in, in cube widths. 0 = the coded default (1.3).")]
    [SerializeField] private float suctionRadiusCubes = 0f;
    [Min(0f)]
    [Tooltip("How hard husks are pulled towards the nozzle, in cube widths per second. 0 = the coded default (8).")]
    [SerializeField] private float pullSpeedCubes = 0f;
    [Min(1)]
    [Tooltip("How many husks the tank holds.")]
    [SerializeField] private int capacity = 25;
    [Min(0.01f)]
    [Tooltip("Seconds between two husks blown out while the right button is held.")]
    [SerializeField] private float blowInterval = 0.09f;
    [Min(0f)]
    [Tooltip("How fast blown-out husks leave the nozzle, in cube widths per second.")]
    [SerializeField] private float blowSpeedCubes = 6f;

    private static readonly List<PixelVacuumCleaner> all = new List<PixelVacuumCleaner>();
    private static PixelVacuumCleaner driven;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); driven = null; }

    /// <summary>The player is driving a Vacuum Cleaner: its keys and clicks belong to it (Pixel Grabbing steps aside).</summary>
    public static bool Driving => driven != null && driven.isActiveAndEnabled;

    private float DriveSpeed => (driveSpeedCubes > 0f ? driveSpeedCubes : 4f) * unit;
    private float Reach => (hoseReachCubes > 0f ? hoseReachCubes : 4f) * unit;
    private float SuckRadius => (suctionRadiusCubes > 0f ? suctionRadiusCubes : 1.3f) * unit;
    private float Pull => (pullSpeedCubes > 0f ? pullSpeedCubes : 8f) * unit;

    private struct Stored { public int tier; public double amount; }

    private PixelClicker clicker;
    private PixelConsumables consumables;
    private bool ghost;
    private float unit = 1f;
    private Rigidbody body;
    private Transform nozzle, outlet, ring;
    private MeshFilter hoseFilter;
    private Mesh hoseMesh;
    private TextMeshPro counter;
    private readonly List<Stored> tank = new List<Stored>();
    private readonly List<Collider> ownColliders = new List<Collider>();
    private Vector3 nozzlePos;       // world, on the floor
    private bool sucking;
    private float blowTimer;
    private Vector3 driveTarget;
    private Quaternion driveRot;
    private bool hasDrive;

    public static GameObject CreateGhost(PixelClicker c) { return Build(c, true, "Vacuum Cleaner (Ghost)"); }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        GameObject root = Build(c, false, "Vacuum Cleaner");
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        PixelVacuumCleaner v = root.GetComponent<PixelVacuumCleaner>();
        v.nozzlePos = v.RestPoint();
        v.UpdateHose();
        PixelAudio.Play("device_place");
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghostMode, string name)
    {
        GameObject root = new GameObject(name);
        PixelVacuumCleaner v = root.AddComponent<PixelVacuumCleaner>();
        v.clicker = c; v.ghost = ghostMode; v.unit = Mathf.Max(0.05f, c.OldPixelWorldSize);
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
    // Model: a canister on wheels, a handle, an outlet, a flexible hose and a floor nozzle
    // ------------------------------------------------------------------

    private void BuildModel()
    {
        float u = unit;
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color shell = new Color(0.85f, 0.2f, 0.25f, 1f), dark = new Color(0.12f, 0.12f, 0.15f, 1f), chrome = new Color(0.75f, 0.78f, 0.82f, 1f), teal = new Color(0.2f, 0.9f, 0.85f, 1f);
        k.Prim(PrimitiveType.Capsule, "Canister", transform, new Vector3(0f, 0.55f * u, 0f), new Vector3(1.1f * u, 0.9f * u, 1.1f * u), shell, new Vector3(90f, 0f, 0f), 0.3f, 0.75f, !ghost);
        k.Box("Base", transform, new Vector3(0f, 0.18f * u, 0f), new Vector3(1.0f * u, 0.16f * u, 1.7f * u), dark, default, false, 0.2f, 0.3f);
        k.Box("Window", transform, new Vector3(0f, 0.95f * u, -0.15f * u), new Vector3(0.5f * u, 0.12f * u, 0.7f * u), new Color(0.3f, 0.35f, 0.42f, 1f), default, false, 0.6f, 0.9f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                k.Cyl("Wheel", transform, new Vector3(sx * 0.52f * u, 0.16f * u, sz * 0.55f * u), 0.16f * u, 0.12f * u, dark, new Vector3(0f, 0f, 90f), 0.1f, 0.3f);
        k.Box("Handle Post", transform, new Vector3(0f, 1.15f * u, 0.35f * u), new Vector3(0.1f * u, 0.3f * u, 0.1f * u), dark);
        k.Box("Handle", transform, new Vector3(0f, 1.3f * u, 0.15f * u), new Vector3(0.12f * u, 0.1f * u, 0.5f * u), dark);
        GameObject o = k.Cyl("Outlet", transform, new Vector3(0f, 0.6f * u, 0.95f * u), 0.17f * u, 0.22f * u, chrome, new Vector3(90f, 0f, 0f), 0.8f, 0.8f);
        outlet = o.transform;

        // The ring under it while it is being driven.
        GameObject r = k.Cyl("Drive Ring", transform, new Vector3(0f, 0.015f * u, 0f), 1.25f * u, 0.012f * u, new Color(teal.r, teal.g, teal.b, 0.5f), default, 0f, 0.9f);
        ring = r.transform;
        ring.gameObject.SetActive(false);

        if (ghost) return;
        // The nozzle lives in the world (not under the body), so it can be put down anywhere in reach.
        GameObject n = new GameObject("Vacuum Nozzle");
        nozzle = n.transform;
        k.Box("Head", nozzle, new Vector3(0f, 0.07f * u, 0f), new Vector3(0.9f * u, 0.12f * u, 0.32f * u), dark, default, false, 0.2f, 0.4f);
        k.Box("Brush", nozzle, new Vector3(0f, 0.015f * u, 0f), new Vector3(0.85f * u, 0.03f * u, 0.22f * u), new Color(0.35f, 0.3f, 0.25f, 1f));
        k.Cyl("Wand", nozzle, new Vector3(0f, 0.45f * u, 0f), 0.07f * u, 0.7f * u, chrome, default, 0.85f, 0.8f);

        GameObject hose = new GameObject("Vacuum Hose");
        hose.transform.SetParent(transform, false);
        hoseFilter = hose.AddComponent<MeshFilter>();
        MeshRenderer hr = hose.AddComponent<MeshRenderer>();
        hr.sharedMaterial = k.Mat(new Color(0.22f, 0.22f, 0.26f, 1f), 0.1f, 0.5f);
        hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hoseMesh = new Mesh { name = "Vacuum Hose" };
        hoseFilter.sharedMesh = hoseMesh;

        GameObject cg = new GameObject("Counter");
        cg.transform.SetParent(transform, false);
        cg.transform.localPosition = new Vector3(0f, 1.9f * u, 0f);
        counter = cg.AddComponent<TextMeshPro>();
        counter.fontSize = 3.2f;
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
        PixelWindows.Register(this, 205, () => driven == this, StopDriving);   // Escape stops driving
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); if (driven == this) driven = null; }

    private void OnDestroy()
    {
        if (ghost) return;
        PixelWindows.Unregister(this);
        if (driven == this) driven = null;
        if (gameObject.scene.isLoaded) BlowOutAll();   // what was in the tank comes out where it stands (not while the scene is closing)
        if (nozzle != null) Destroy(nozzle.gameObject);
        if (hoseMesh != null) Destroy(hoseMesh);
    }

    private void StopDriving() { if (driven == this) driven = null; }

    // ------------------------------------------------------------------
    // Every frame
    // ------------------------------------------------------------------

    private Camera Cam => clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;

    private Vector3 RestPoint() => transform.TransformPoint(new Vector3(0.9f * unit, 0f, 1.4f * unit));

    private void Update()
    {
        if (ghost || clicker == null) return;
        Camera cam = Cam;
        bool can = cam != null && !PixelCalm.InMenu && Time.timeScale > 0f && !PixelFirstPerson.Active && !PixelFreeCam.Active;   // (first person / free cam use W A S D themselves)
        if (!can && driven == this) driven = null;
        if (ring != null && ring.gameObject.activeSelf != (driven == this)) ring.gameObject.SetActive(driven == this);
        if (counter != null && cam != null) counter.transform.rotation = Quaternion.LookRotation(counter.transform.position - cam.transform.position, cam.transform.up);
        if (!can) { sucking = false; UpdateNozzle(); return; }

        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        bool overMe = !PixelInput.PointerOverUI() && OverBody(ray);
        if (overMe) PixelPointerClaim.Claim();
        if (overMe && PixelInput.LeftPressed() && !PixelFeatureTester.Placing)
        {
            driven = driven == this ? null : this;   // click the body: drive it / stop driving
            PixelAudio.Play("ui_click");
            return;
        }

        if (driven == this)
        {
            PixelPointerClaim.Claim();   // its clicks: never the cube
            PixelKeys.ClaimDriveKeys();
            Drive();
            Vector3 aim = default;
            bool onFloor = !PixelInput.PointerOverUI() && TryFloor(out aim);
            sucking = PixelInput.LeftHeld() && onFloor;
            if (sucking)
            {
                Vector3 from = transform.position, to = aim - from; to.y = 0f;
                if (to.magnitude > Reach) aim = from + to.normalized * Reach;
                nozzlePos = Vector3.Lerp(nozzlePos, new Vector3(aim.x, from.y, aim.z), 1f - Mathf.Exp(-16f * Time.deltaTime));
            }
            if (PixelInput.RightHeld() && !overMe && onFloor && tank.Count > 0)
            {
                blowTimer -= Time.deltaTime;
                if (blowTimer <= 0f) { blowTimer = blowInterval; BlowOne(aim); }
            }
            else blowTimer = 0f;
        }
        else sucking = false;
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
            if (vp.z <= 0f || vp.x < 0.03f || vp.x > 0.97f || vp.y < 0.08f || vp.y > 0.92f) return;   // never off the screen
        }
        driveTarget = next;
        driveRot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), turnSpeed * Time.deltaTime);
        hasDrive = true;
    }

    private void FixedUpdate()
    {
        if (ghost || body == null) return;
        if (hasDrive) { body.MovePosition(driveTarget); body.MoveRotation(driveRot); }
        if (sucking) Suck();
    }

    private void UpdateNozzle()
    {
        if (nozzle == null) return;
        if (!sucking)
        {
            Vector3 rest = RestPoint();
            nozzlePos = Vector3.Lerp(nozzlePos, rest, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }
        Vector3 to = nozzlePos - transform.position; to.y = 0f;
        nozzle.position = new Vector3(nozzlePos.x, transform.position.y, nozzlePos.z);
        if (to.sqrMagnitude > 0.0001f) nozzle.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        nozzle.localScale = Vector3.one * (sucking ? 1f + 0.04f * Mathf.Sin(Time.time * 40f) : 1f);   // (a little buzz while it sucks)
        UpdateHose();
    }

    /// <summary>The hose: from the outlet out and down, then up to the top of the nozzle's wand, sagging in the middle.</summary>
    private void UpdateHose()
    {
        if (hoseMesh == null || outlet == null || nozzle == null) return;
        Vector3 a = outlet.position + transform.forward * 0.12f * unit;
        Vector3 b = nozzle.position + Vector3.up * 0.8f * unit;
        float dist = Vector3.Distance(a, b);
        Vector3 c1 = a + transform.forward * Mathf.Max(0.4f * unit, dist * 0.35f) + Vector3.down * 0.25f * unit;
        Vector3 c2 = b + Vector3.up * Mathf.Max(0.4f * unit, dist * 0.3f);
        const int n = 18;
        Vector3[] pts = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1), m = 1f - t;
            Vector3 p = m * m * m * a + 3f * m * m * t * c1 + 3f * m * t * t * c2 + t * t * t * b;
            p.y = Mathf.Max(p.y, transform.position.y + 0.08f * unit);
            pts[i] = transform.InverseTransformPoint(p);
        }
        PixelTube.Build(pts, 0.085f * unit, 10, hoseMesh);
        hoseMesh.RecalculateBounds();
    }

    // ------------------------------------------------------------------
    // Sucking and blowing
    // ------------------------------------------------------------------

    private void Suck()
    {
        if (clicker == null || nozzle == null) return;
        Vector3 mouth = nozzle.position + Vector3.up * 0.12f * unit;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        float r = SuckRadius;
        bool full = tank.Count >= capacity;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = pixels[i];
            if (rb == null || rb.isKinematic || clicker.IsFlyingPixel(rb) || PixelAttachment.IsAttached(rb) || PixelPathCarrier.IsCarried(rb)) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null || info.heldByPlayer || !clicker.IsValidTierIndex(info.tierIndex)) continue;
            OldPixelDespawn despawn = rb.GetComponent<OldPixelDespawn>();
            if (despawn != null && (despawn.IsDespawning || despawn.Held)) continue;
            Vector3 to = mouth - rb.position;
            float d = to.magnitude;
            if (d > r) continue;
            if (full) continue;
            if (d < 0.4f * unit)
            {
                if (!clicker.ReleaseOldPixel(rb, false)) continue;
                tank.Add(new Stored { tier = info.tierIndex, amount = info.amount });
                StartCoroutine(SuckIn(rb.gameObject));
                full = tank.Count >= capacity;
                RefreshCounter();
                continue;
            }
            float k = 1f - Mathf.Clamp01(d / r);
            if (despawn != null) despawn.KeepAlive();
            PixelPathCarrier.SetVelocity(rb, Vector3.Lerp(VelocityOf(rb), to.normalized * Pull * (0.4f + 0.6f * k), 0.3f + 0.4f * k));
        }
    }

    private static Vector3 VelocityOf(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    /// <summary>A sucked-up husk shrinks into the nozzle's mouth and is gone (its type and value are in the tank).</summary>
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
            Vector3 end = nozzle != null ? nozzle.position + Vector3.up * 0.15f * unit : start;
            go.transform.position = Vector3.Lerp(start, end, e);
            go.transform.localScale = scale * (1f - e);
            yield return null;
        }
        if (go != null) Destroy(go);
        PixelAudio.Play("click");
    }

    /// <summary>Blows the newest husk in the tank out of the nozzle towards 'target' (a lob that lands near it).</summary>
    private void BlowOne(Vector3 target)
    {
        if (tank.Count == 0 || clicker == null || nozzle == null) return;
        Stored s = tank[tank.Count - 1];
        tank.RemoveAt(tank.Count - 1);
        Vector3 from = nozzle.position + Vector3.up * 0.3f * unit;
        Vector3 flat = target - from; float up = flat.y; flat.y = 0f;
        float speed = Mathf.Max(1f, blowSpeedCubes * unit);
        float time = Mathf.Clamp(flat.magnitude / speed, 0.15f, 1.2f);
        float g = Mathf.Abs(Physics.gravity.y);
        Vector3 v = flat / time + Vector3.up * ((up + 0.5f * g * time * time) / time);   // (lands on the target point)
        v += Random.insideUnitSphere * 0.25f * unit;
        clicker.SpawnStoredPixel(s.tier, s.amount, from, v);
        RefreshCounter();
    }

    private void BlowOutAll()
    {
        if (clicker == null) return;
        Vector3 at = nozzle != null ? nozzle.position : transform.position;
        foreach (Stored s in tank)
            clicker.SpawnStoredPixel(s.tier, s.amount, at + Vector3.up * 0.6f * unit, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 3f), Random.Range(-1.5f, 1.5f)));
        tank.Clear();
    }

    private void RefreshCounter()
    {
        if (counter == null) return;
        counter.text = tank.Count + " / " + capacity + (driven == this ? "" : "\n<size=55%>click to drive</size>");
    }

    private void LateUpdate()
    {
        if (counter == null) return;
        bool want = driven == this;
        if (want != counterDriving) { counterDriving = want; RefreshCounter(); }
    }

    private bool counterDriving;

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
