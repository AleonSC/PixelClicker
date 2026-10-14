// ---- System notes ----
// PixelBeltSorter.cs
//   BELT SORTER (a Dev Hub > Tester feature, PixelFeatureTester 'Belt Sorter'): a low gantry that straddles a conveyor belt anywhere along it and kicks every pixel of ITS TYPE off the side. Placing: the ghost snaps to the belt under the mouse (TrySnap) and the chute
//   is on whichever side of the belt the mouse is (the snapped yaw is turned round for the left side, the chute is always the model's +X). A new sorter is LEARNING: the first pixel that rides past sets the filter (it carries on); the label over the gantry shows
//   the filter (a swatch-coloured name) or 'Learning...'. LEFT CLICK the gantry to learn a new filter. While a pixel of the filter type rides under the beam, PixelConveyorSystem asks PixelBeltSorter.TryDivert (called from its rider loop): the pixel leaves
//   the belt (a paddle sweeps across), and is thrown out of the side - or put onto a belt that STARTS at the chute's mouth (within ~1.3 cubes), so a second belt can be fed from the side. Pixels of other types are untouched. The spot is stored as a distance along
//   the belt (it follows the belt); saved with the tester save (spot, side, filter type).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PixelBeltSorter : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("How hard a kicked pixel is thrown out of the side, in cube widths per second. 0 = the coded default (3).")]
    [SerializeField] private float kickSpeedCubes = 0f;

    private float Kick => kickSpeedCubes > 0f ? kickSpeedCubes : 3f;

    private static readonly List<PixelBeltSorter> all = new List<PixelBeltSorter>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 1f, beltWidth;
    private PixelConveyor belt;
    private float along;
    private Vector3 sideDir = Vector3.right;
    private bool learning = true;
    private PixelClicker.PixelType filter;
    private TextMeshPro label;
    private Transform paddle;
    private float kickT = -1f;
    private int placedFrame;

    // ---------------- placing ----------------

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

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw) { return PlaceWith(c, point, yaw, true, default); }

    private static GameObject PlaceWith(PixelClicker c, Vector3 point, float yaw, bool learn, PixelClicker.PixelType type)
    {
        if (!PixelConveyor.NearestOn(c, point, out PixelConveyor belt, out float s, 1.6f)) { PixelHints.Announce("Click a spot on a conveyor belt for the sorter."); return null; }
        GameObject root = Build(c, false, "Belt Sorter", belt.Width);
        PixelBeltSorter b = root.GetComponent<PixelBeltSorter>();
        b.belt = belt; b.along = s;
        b.learning = learn; b.filter = type;
        b.placedFrame = Time.frameCount;
        Vector3 p = belt.PointAt(s);
        root.transform.SetPositionAndRotation(new Vector3(p.x, p.y + belt.SurfaceHeight, p.z), Quaternion.Euler(0f, yaw, 0f));
        b.sideDir = root.transform.right;
        PixelAudio.Play("device_place");
        return root;
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
        k.Box("Chute", transform, new Vector3(half + 0.55f * u, 0.12f * u, 0f), new Vector3(1.1f * u, 0.07f * u, 0.9f * u), dark, new Vector3(0f, 0f, -8f), false, 0.4f, 0.4f);
        k.Box("Chute Wall F", transform, new Vector3(half + 0.55f * u, 0.3f * u, 0.46f * u), new Vector3(1.1f * u, 0.3f * u, 0.07f * u), steel, default, false, 0.7f, 0.5f);
        k.Box("Chute Wall B", transform, new Vector3(half + 0.55f * u, 0.3f * u, -0.46f * u), new Vector3(1.1f * u, 0.3f * u, 0.07f * u), steel, default, false, 0.7f, 0.5f);
        if (!ghost)
        {
            GameObject t = new GameObject("Label", typeof(TextMeshPro));
            t.transform.SetParent(transform, false);
            t.transform.localPosition = new Vector3(0f, beamY + 0.7f * u, 0f);
            label = t.GetComponent<TextMeshPro>();
            if (clicker.UIFont != null) label.font = clicker.UIFont;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 4.2f * u;
#if UNITY_2023_1_OR_NEWER
            label.textWrappingMode = TextWrappingModes.NoWrap;
#else
            label.enableWordWrapping = false;
#endif
            RefreshLabel();
        }
    }

    // ---------------- behaviour ----------------

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    private void RefreshLabel()
    {
        if (label == null) return;
        if (learning) { label.text = "Learning..."; label.color = new Color(1f, 0.9f, 0.4f, 1f); return; }
        int idx = clicker.IndexOf(filter);
        label.text = idx >= 0 ? clicker.Tiers[idx].displayName : filter.ToString();
        label.color = idx >= 0 ? Color.Lerp(clicker.Tiers[idx].color, Color.white, 0.35f) : Color.white;
    }

    private void Update()
    {
        if (ghost || clicker == null) return;
        if (belt == null) { Destroy(gameObject); return; }
        Vector3 p = belt.PointAt(Mathf.Clamp(along, 0f, belt.Length));
        transform.position = new Vector3(p.x, p.y + belt.SurfaceHeight, p.z);
        if (label != null)
        {
            Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
            if (cam != null) label.transform.rotation = cam.transform.rotation;
        }
        if (paddle != null)
        {
            float half = beltWidth * 0.5f;
            float k = 0f;
            if (kickT >= 0f) { kickT += Time.deltaTime; k = kickT < 0.12f ? kickT / 0.12f : Mathf.Max(0f, 1f - (kickT - 0.12f) / 0.2f); if (kickT > 0.32f) kickT = -1f; }
            paddle.localPosition = new Vector3(Mathf.Lerp(-half + 0.1f * unit, half - 0.1f * unit, k), 0.55f * unit, 0f);
        }
        if (Time.frameCount != placedFrame && PixelInput.LeftPressed()) CheckClick();
    }

    private void CheckClick()
    {
        Ray ray;
        if (PixelFirstPerson.Active) { if (!PixelFirstPerson.TryGetViewRay(out ray)) return; }
        else
        {
            Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
            if (cam == null || PixelInput.PointerOverUI()) return;
            ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        }
        foreach (RaycastHit h in Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Ignore))
            if (h.transform.IsChildOf(transform)) { learning = true; RefreshLabel(); PixelAudio.Play("ui_click"); return; }
    }

    /// <summary>Called by the belt system for every riding pixel: true when a sorter took it off the belt (the system then forgets it).</summary>
    public static bool TryDivert(PixelClicker c, PixelConveyor belt, float s, Rigidbody rb)
    {
        for (int i = all.Count - 1; i >= 0; i--)
        {
            PixelBeltSorter b = all[i];
            if (b == null || b.ghost || b.belt != belt || Mathf.Abs(s - b.along) > 0.2f * b.unit) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null || !c.IsValidTierIndex(info.tierIndex)) continue;
            PixelClicker.PixelType type = c.Tiers[info.tierIndex].type;
            if (b.learning) { b.filter = type; b.learning = false; b.RefreshLabel(); PixelAudio.Play("ui_click", 1.3f); continue; }   // the example carries on
            if (type != b.filter) continue;
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
        Vector3 mouth = transform.position + sideDir * (beltWidth * 0.5f + 1.0f * u);
        rb.isKinematic = false;
        Vector3 pos = transform.position + sideDir * (beltWidth * 0.5f + 0.2f * u) + Vector3.up * (oldSize * 0.6f);
        Vector3 vel = sideDir * Kick * u + Vector3.up * (0.6f * u);
        foreach (PixelConveyor other in PixelConveyor.All)
        {
            if (other == null || other.IsGhost || other == belt) continue;
            Vector3 d = other.StartPoint - mouth;
            if (new Vector3(d.x, 0f, d.z).magnitude > 1.3f * u || Mathf.Abs(d.y) > 1.2f * u) continue;
            Vector3 over = other.PointAt(0.6f * oldSize);
            pos = new Vector3(over.x, over.y + other.SurfaceHeight + oldSize * 0.7f, over.z);
            vel = other.Direction * other.Speed * 0.5f;
            break;
        }
        rb.position = pos;
        PixelPathCarrier.SetVelocity(rb, vel);
    }

    // ---------------- saving ----------------

    [System.Serializable]
    private class Save { public Vector3 spot; public float yaw; public bool learning; public string filter; }

    public static string ExportState(GameObject root)
    {
        PixelBeltSorter b = root != null ? root.GetComponent<PixelBeltSorter>() : null;
        return b == null || b.belt == null ? null : JsonUtility.ToJson(new Save { spot = root.transform.position, yaw = root.transform.eulerAngles.y, learning = b.learning, filter = b.filter.ToString() });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        PixelClicker.PixelType type = default;
        bool learn = s.learning || !System.Enum.TryParse(s.filter, out type);
        return PlaceWith(c, s.spot, s.yaw, learn, type);
    }
}
