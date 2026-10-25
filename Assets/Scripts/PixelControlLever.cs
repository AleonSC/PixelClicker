// ---- System notes ----
// PixelControlLever.cs
//   CONTROL LEVER (2026-10-20, a Dev Hub > Tester feature 'Control Lever'): a floor lever you put down BESIDE a conveyor belt, on either side, anywhere
//   along it (the ghost snaps next to the belt under the mouse, on the side the mouse is on, turned the belt's way). LEFT-CLICK it to flip it:
//   - ON (handle forward, green lamp): nothing happens, the belts run.
//   - OFF (handle back, red lamp): its belt's whole LINE stops - every belt joined to it end to end, either way round, corners included
//     (PixelConveyor.CollectLine) - by calling PixelConveyor.HoldHalt a few times a second (a belt's speed is 0 while halted: riders wait in place,
//     the chevrons stop). Click again and they all run on. Two levers on one line: either one OFF stops it.
//   SECTIONS (2026-10-20): with TWO OR MORE Belt Sorters on the line, a lever only switches its own SECTION - the belts between the sorters' joints
//   (PixelConveyor.CollectSection); with one sorter or none it switches the whole line - unless both ends of the line are connected (a coupling, an
//   elevator): then one sorter already cuts it into two sections.
//   It finds its belt again by position when needed (a belt laid again in the same spot still counts). Saved with the tester save (position, turn, on / off).
//   Built with PixelFactoryKit; its body has colliders so it can be clicked (PixelPointerClaim blocks the cube click while the mouse is on it).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelControlLever : MonoBehaviour
{
    [Min(0.05f)]
    [Tooltip("Seconds between two checks of the belts it controls (while it is OFF it keeps them halted).")]
    [SerializeField] private float checkSeconds = 0.15f;

    [Header("Texts")]
    [Tooltip("Event log line when you switch the lever off. {0} = belts stopped, {1} = s when more than one.")] [SerializeField] private string leverOffFormat = "Lever OFF - {0} belt{1} stopped";

    private PixelClicker clicker;
    private bool ghost, on = true;
    private float unit = 0.5f, nextCheck, handleAngle = HandleOn;
    private PixelConveyor belt;
    private Transform handle;
    private Renderer lamp;
    private Material lampOnMat, lampOffMat;   // (two shared materials swapped on the lamp: no material copy per lever)
    private readonly List<Collider> body = new List<Collider>();
    private static readonly List<PixelConveyor> lineScratch = new List<PixelConveyor>();

    private const float HandleOn = 35f, HandleOff = -35f;
    private const float StandOffCubes = 0.9f;   // (from the belt's edge to the lever's middle: just clear of the rails)
    private static readonly Color LampOn = new Color(0.3f, 1f, 0.4f, 1f), LampOff = new Color(1f, 0.25f, 0.2f, 1f);

    public bool IsOn => on;

    // ---------------- placing ----------------

    private static float U(PixelClicker c) => Mathf.Max(0.05f, c != null ? c.OldPixelWorldSize : 0.5f);

    public static GameObject CreateGhost(PixelClicker c) => Build(c, true);

    /// <summary>Snaps the ghost beside the belt under the mouse: on the side the mouse is on, turned the belt's way.</summary>
    public static bool TrySnap(PixelClicker c, Vector3 point, float yaw, GameObject ghostRoot, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        if (!Spot(c, point, out PixelConveyor _, out snappedPoint, out snappedYaw)) return false;
        return true;
    }

    /// <summary>Where a lever goes for a mouse point near a belt: beside the nearest belt on the mouse's side, facing along it.</summary>
    private static bool Spot(PixelClicker c, Vector3 point, out PixelConveyor belt, out Vector3 at, out float yaw)
    {
        at = point; yaw = 0f;
        if (!PixelConveyor.NearestOn(c, point, out belt, out float s, 2.5f)) return false;
        Vector3 dir = belt.Direction;
        Vector3 side = Vector3.Cross(Vector3.up, dir);   // (the belt's right)
        Vector3 p = belt.PointAt(s);
        float sign = Vector3.Dot(point - p, side) >= 0f ? 1f : -1f;
        at = p + side * sign * (belt.Width * 0.5f + StandOffCubes * U(c));
        at.y = PixelConveyor.FloorY(c);
        yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        return true;
    }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        if (!Spot(c, point, out PixelConveyor belt, out Vector3 at, out float useYaw)) { PixelHints.Announce(PixelDialogue.File.Text("Device placing", "lever spot", "Put the lever next to a conveyor belt.")); return null; }
        GameObject root = Build(c, false);
        root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, useYaw, 0f));
        root.GetComponent<PixelControlLever>().belt = belt;
        PixelAudio.Play("device_place");
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghostMode)
    {
        GameObject root = new GameObject(ghostMode ? "Control Lever (Ghost)" : "Control Lever");
        PixelControlLever lever = root.AddComponent<PixelControlLever>();
        lever.clicker = c; lever.ghost = ghostMode; lever.unit = U(c);
        float u = lever.unit;
        PixelFactoryKit k = new PixelFactoryKit(c, ghostMode, true);   // (shared materials)
        Transform t = root.transform;
        Color plate = new Color(0.16f, 0.17f, 0.2f, 1f), housing = new Color(0.95f, 0.75f, 0.15f, 1f), steel = new Color(0.7f, 0.73f, 0.78f, 1f),
              dark = new Color(0.07f, 0.07f, 0.08f, 1f), knob = new Color(0.85f, 0.15f, 0.12f, 1f);
        bool col = !ghostMode;
        // A floor plate with four bolts, a yellow housing with a dark slot on top, a steel pivot, the lever arm with a red ball knob, and a lamp.
        k.Box("Plate", t, new Vector3(0f, 0.04f * u, 0f), new Vector3(0.9f * u, 0.08f * u, 1.1f * u), plate, default, col, 0.5f, 0.5f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                k.Cyl("Bolt", t, new Vector3(sx * 0.36f * u, 0.09f * u, sz * 0.46f * u), 0.04f * u, 0.03f * u, steel, default, 0.9f, 0.7f);
        k.Box("Housing", t, new Vector3(0f, 0.28f * u, 0f), new Vector3(0.55f * u, 0.4f * u, 0.75f * u), housing, default, col, 0.3f, 0.55f);
        k.Box("Housing Stripe", t, new Vector3(0f, 0.28f * u, 0f), new Vector3(0.56f * u, 0.08f * u, 0.76f * u), dark, default, false, 0.2f, 0.4f);
        k.Box("Slot", t, new Vector3(0f, 0.485f * u, 0f), new Vector3(0.12f * u, 0.02f * u, 0.6f * u), dark, default, false, 0.1f, 0.3f);
        k.Cyl("Pivot", t, new Vector3(0f, 0.5f * u, 0f), 0.09f * u, 0.4f * u, steel, new Vector3(0f, 0f, 90f), 0.9f, 0.75f);
        GameObject arm = new GameObject("Handle");
        arm.transform.SetParent(t, false);
        arm.transform.localPosition = new Vector3(0f, 0.5f * u, 0f);
        arm.transform.localRotation = Quaternion.Euler(HandleOn, 0f, 0f);
        k.Cyl("Arm", arm.transform, new Vector3(0f, 0.4f * u, 0f), 0.05f * u, 0.8f * u, steel, default, 0.9f, 0.75f);
        k.Ball("Knob", arm.transform, new Vector3(0f, 0.82f * u, 0f), 0.13f * u, knob, 0.2f, 0.7f);
        lever.handle = arm.transform;
        GameObject l = k.Prim(PrimitiveType.Sphere, "Lamp", t, new Vector3(0.2f * u, 0.52f * u, -0.3f * u), Vector3.one * 0.12f * u, LampOn, default, 0f, 0.8f, false, LampOn);
        lever.lamp = l.GetComponent<Renderer>();
        lever.lampOnMat = k.Mat(LampOn, 0f, 0.8f, LampOn);
        lever.lampOffMat = k.Mat(LampOff, 0f, 0.8f, LampOff);
        if (col)
        {
            // A click box round the handle too (the arm itself is thin).
            BoxCollider grip = root.AddComponent<BoxCollider>();
            grip.center = new Vector3(0f, 0.8f * u, 0f);
            grip.size = new Vector3(0.6f * u, 1.1f * u, 1.2f * u);
            grip.isTrigger = true;
            foreach (Collider c2 in root.GetComponentsInChildren<Collider>()) lever.body.Add(c2);
        }
        return root;
    }

    // ---------------- every frame ----------------

    private void Update()
    {
        if (ghost || clicker == null) return;
        // The handle swings to its side.
        float want = on ? HandleOn : HandleOff;
        handleAngle = Mathf.MoveTowards(handleAngle, want, 400f * Time.unscaledDeltaTime);
        if (handle != null) handle.localRotation = Quaternion.Euler(handleAngle, 0f, 0f);
        if (PixelCalm.InMenu) return;
        HandleClick();
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkSeconds;
        if (belt == null && Spot(clicker, transform.position, out PixelConveyor found, out _, out _)) belt = found;   // (its belt was laid again)
        if (on || belt == null) return;
        belt.CollectSection(lineScratch);
        foreach (PixelConveyor b in lineScratch) if (b != null) b.HoldHalt();
    }

    /// <summary>Left-click the lever: it flips.</summary>
    private void HandleClick()
    {
        Camera cam = clicker.ViewCamera;
        if (cam == null || PixelInput.PointerOverUI() || PixelFeatureTester.Placing || PixelCarryHandles.Hot) return;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        bool over = false;
        foreach (Collider c in body) if (c != null && c.Raycast(ray, out _, 500f)) { over = true; break; }
        if (!over) return;
        PixelPointerClaim.ClaimButton();
        if (PixelInput.LeftPressed()) SetOn(!on, true);
    }

    private void SetOn(bool value, bool byHand)
    {
        on = value;
        nextCheck = 0f;   // (the belts react straight away)
        if (lamp != null && lampOnMat != null) lamp.sharedMaterial = on ? lampOnMat : lampOffMat;
        if (!byHand) return;
        PixelAudio.Play("ui_click", on ? 1.2f : 0.8f);
        if (!on && belt != null) { belt.CollectSection(lineScratch); PixelHints.Announce(string.Format(leverOffFormat, lineScratch.Count, lineScratch.Count == 1 ? "" : "s")); }
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw; public bool off; }

    public static string ExportState(GameObject root)
    {
        PixelControlLever l = root != null ? root.GetComponent<PixelControlLever>() : null;
        return l == null ? null : JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y, off = !l.on });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        // Put back exactly where it stood (not re-snapped), then find its belt.
        GameObject root = Build(c, false);
        root.transform.SetPositionAndRotation(s.position, Quaternion.Euler(0f, s.yaw, 0f));
        PixelControlLever l = root.GetComponent<PixelControlLever>();
        if (Spot(c, s.position, out PixelConveyor belt, out _, out _)) l.belt = belt;
        l.handleAngle = s.off ? HandleOff : HandleOn;
        if (s.off) l.SetOn(false, false);
        return root;
    }
}
