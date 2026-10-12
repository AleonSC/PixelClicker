// ---- System notes (moved here from CLAUDE.md) ----
// PixelWoodworking.cs
//   Woodchipper + log rack (component added by PixelClicker.Awake; disableWoodworking): while Woodcutting is the skill in play
//   (ActiveId, not on the title screen / camera intro / takeover minigames) two code-built machines grow in on the floor at the
//   screen's sides (chipperScreenSpot default (0.12, 0.3), rackScreenSpot (0.88, 0.3), machineScale; placed on the floor under the
//   cube by PlaceMachines, rotated to face the camera, labels billboard) and shrink away when you switch skills. Drag old blocks into
//   them with Pixel Grabbing (or throw them): ScanPixels (every 0.08 s) takes any loose, un-held old pixel inside a machine's
//   footprint (InZone): the chipper takes the scraps Bark / Sap / Leaf / Twig (blades spin faster, wood chips spit out of the chute,
//   chipper_feed), the rack takes the logs Wood / Heartwood (a log appears on the shelves, rack_store; rackCapacity 0 = 15 then a
//   truck takes them all). The block is used up with NO payout (ReleaseOldPixel(body, false) + a fly-in) and fills the Woodcutting
//   bonus XP bank (PixelSkills.OldPixelConsumed, which now returns the change; a Twig adds a flat twigBonus (0 = 1) via
//   PixelSkills.AddBonusXp), with a rising gold '+N bonus XP'. Counters wood.chipped / wood.stored; tip woodworking.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The Woodcutting skill's two machines, which set themselves up on the floor while Woodcutting is the skill in play: a <b>woodchipper</b> (left)
/// and a <b>log storage rack</b> (right). Hand-drag old wood blocks into them with Pixel Grabbing (or throw them in): the chipper takes the scraps
/// (Bark, Sap, Leaf and Twig), the rack takes the logs (Wood and Heartwood). Each block that goes in is used up (no payout) and fills the Woodcutting
/// <b>bonus XP bank</b> (<see cref="PixelSkills.OldPixelConsumed"/>; a Twig adds a flat 1), which doubles the XP of every later chop. The machines are
/// code-built from primitives and fade / grow away when you switch to another skill. Added by <see cref="PixelClicker"/>.
/// </summary>
public class PixelWoodworking : MonoBehaviour
{
    [Header("General")]
    [Tooltip("Turn the woodchipper and the log rack off.")]
    [SerializeField] private bool disableWoodworking = false;

    [Tooltip("Where the woodchipper stands, as a fraction of the screen (x = across from the left, y = up from the bottom). (0, 0) = the default (0.12, 0.3).")]
    [SerializeField] private Vector2 chipperScreenSpot = Vector2.zero;

    [Tooltip("Where the log rack stands, as a fraction of the screen. (0, 0) = the default (0.88, 0.3).")]
    [SerializeField] private Vector2 rackScreenSpot = Vector2.zero;

    [Min(0f)]
    [Tooltip("Size of both machines as a multiple of the main pixel's width. 0 = 1.")]
    [SerializeField] private float machineScale = 0f;

    [Min(0)]
    [Tooltip("Logs the rack holds before a truck takes them all away (they pay nothing, the bonus XP is given as they go in). 0 = 15.")]
    [SerializeField] private int rackCapacity = 0;

    [Min(0f)]
    [Tooltip("Bonus XP a Twig adds to the bank when it is chipped (the other blocks use the bonus bank's own values). 0 = 1.")]
    [SerializeField] private float twigBonus = 0f;

    private PixelClicker clicker;
    private Camera cam;

    private class Machine
    {
        public GameObject root;
        public Transform spinner, front;
        public TextMeshPro label;
        public ParticleSystem fx;
        public float appear, feedTimer, scale = 1f;
        public Vector3 spot;
        public bool isRack;
        public int stored;
        public readonly List<GameObject> logs = new List<GameObject>();
    }

    private Machine chipper, rack;
    private float scanTimer, spotTimer, floorY;
    private bool haveFloor, wantNow;
    private int chipped;

    private const int LogsPerShelf = 5, Shelves = 3;

    private void Awake()
    {
        clicker = GetComponent<PixelClicker>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private static bool TakesChipper(PixelClicker.PixelType t) =>
        t == PixelClicker.PixelType.Bark || t == PixelClicker.PixelType.Sap || t == PixelClicker.PixelType.Leaf || t == PixelClicker.PixelType.Twig;

    private static bool TakesRack(PixelClicker.PixelType t) => t == PixelClicker.PixelType.Wood || t == PixelClicker.PixelType.Heartwood;

    // ------------------------------------------------------------------

    private void Update()
    {
        if (clicker == null) return;
        PixelSkills skills = PixelSkills.Instance;
        wantNow = !disableWoodworking && skills != null && skills.ActiveId == PixelSkills.WoodcuttingId &&
                  !PixelTitleScreen.Showing && !PixelTitleScreen.Reversing && !PixelCameraIntro.Moving && !PixelMinigame.TakeoverActive;
        if (wantNow && chipper == null)
        {
            cam = Camera.main;
            if (cam == null) return;
            PixelHints.Trigger("woodworking");
            chipper = BuildChipper();
            rack = BuildRack();
            spotTimer = 0f;
        }
        if (chipper == null) return;

        float dt = Time.unscaledDeltaTime;
        foreach (Machine m in new[] { chipper, rack })
        {
            m.appear = Mathf.MoveTowards(m.appear, wantNow ? 1f : 0f, dt / 0.6f);
            float k = Mathf.SmoothStep(0f, 1f, m.appear);
            m.root.transform.localScale = Vector3.one * (m.scale * Mathf.Max(0.001f, k));
            m.root.SetActive(m.appear > 0.001f);
        }
        if (!wantNow && chipper.appear <= 0.001f && rack.appear <= 0.001f) { DestroyMachines(); return; }

        spotTimer -= dt;
        if (spotTimer <= 0f) { spotTimer = 0.5f; PlaceMachines(); }

        // Spinning blades: slow while idle, fast while chipping.
        chipper.feedTimer = Mathf.Max(0f, chipper.feedTimer - dt);
        float speed = chipper.feedTimer > 0f ? 900f : 120f;
        if (chipper.spinner != null) chipper.spinner.Rotate(0f, speed * dt, 0f, Space.Self);

        if (!wantNow) return;
        scanTimer -= dt;
        if (scanTimer <= 0f) { scanTimer = 0.08f; ScanPixels(); }
    }

    private void LateUpdate()
    {
        if (chipper == null || cam == null) return;
        foreach (Machine m in new[] { chipper, rack })
            if (m != null && m.label != null) m.label.transform.rotation = Quaternion.LookRotation(m.label.transform.position - cam.transform.position);
    }

    private void DestroyMachines()
    {
        if (chipper != null && chipper.root != null) Destroy(chipper.root);
        if (rack != null && rack.root != null) Destroy(rack.root);
        chipper = rack = null;
    }

    private void OnDestroy() { DestroyMachines(); }

    // ------------------------------------------------------------------
    // Placing them on the floor
    // ------------------------------------------------------------------

    /// <summary>The floor's height under the cube: the top of the highest solid collider that is not a rigidbody (the pixels) below it.</summary>
    private bool FindFloorY()
    {
        Vector3 origin = clicker.CubeBasePosition + Vector3.up * 6f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        bool found = false;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || h.collider.attachedRigidbody != null) continue;
            if (clicker.PixelTransform != null && h.collider.transform.IsChildOf(clicker.PixelTransform)) continue;
            if (h.point.y > best) { best = h.point.y; found = true; }
        }
        if (found) floorY = best;
        else floorY = clicker.CubeBasePosition.y - clicker.PixelBaseSize * 0.5f;
        return true;
    }

    private Vector3 SpotOnFloor(Vector2 viewport)
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        Plane plane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        if (plane.Raycast(ray, out float d)) return ray.GetPoint(d);
        return new Vector3(clicker.CubeBasePosition.x + (viewport.x < 0.5f ? -4f : 4f), floorY, clicker.CubeBasePosition.z);
    }

    private void PlaceMachines()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        FindFloorY();
        Vector2 cs = chipperScreenSpot == Vector2.zero ? new Vector2(0.12f, 0.3f) : chipperScreenSpot;
        Vector2 rs = rackScreenSpot == Vector2.zero ? new Vector2(0.88f, 0.3f) : rackScreenSpot;
        float s = clicker.PixelBaseSize * (machineScale > 0f ? machineScale : 1f);
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        Quaternion face = Quaternion.LookRotation(fwd.normalized, Vector3.up);   // +X = screen right, -Z = towards the camera
        foreach (Machine m in new[] { chipper, rack })
        {
            m.scale = s;
            m.spot = SpotOnFloor(m.isRack ? rs : cs);
            m.root.transform.position = m.spot;
            m.root.transform.rotation = face;
        }
    }

    // ------------------------------------------------------------------
    // Taking pixels in
    // ------------------------------------------------------------------

    private void ScanPixels()
    {
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        if (list == null || list.Count == 0) return;
        List<Rigidbody> snapshot = new List<Rigidbody>(list);
        foreach (Rigidbody body in snapshot)
        {
            if (body == null || body.isKinematic) continue;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && (despawn.Held || despawn.IsDespawning)) continue;   // still in the hand (it is taken when you let go)
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || !clicker.IsValidTierIndex(info.tierIndex)) continue;
            PixelClicker.PixelType type = clicker.Tiers[info.tierIndex].type;
            if (TakesChipper(type) && InZone(chipper, body.position, 1.15f, 1.15f)) TakeIn(chipper, body, info, type);
            else if (TakesRack(type) && InZone(rack, body.position, 1.6f, 1.1f)) TakeIn(rack, body, info, type);
        }
    }

    /// <summary>Is a point over a machine's footprint (halfWidth / halfDepth in main-pixel widths) and not far above it?</summary>
    private bool InZone(Machine m, Vector3 world, float halfWidth, float halfDepth)
    {
        if (m == null || m.appear < 0.95f) return false;
        Vector3 local = m.root.transform.InverseTransformPoint(world) / Mathf.Max(0.0001f, m.scale);
        return Mathf.Abs(local.x) < halfWidth && Mathf.Abs(local.z) < halfDepth && local.y > -0.4f && local.y < 4.5f;
    }

    private void TakeIn(Machine m, Rigidbody body, OldPixelInfo info, PixelClicker.PixelType type)
    {
        if (!clicker.ReleaseOldPixel(body, false)) return;   // used up: no payout

        double bonus;
        if (m == chipper && type == PixelClicker.PixelType.Twig) bonus = PixelSkills.AddBonusXp(twigBonus > 0f ? twigBonus : 1d);
        else bonus = PixelSkills.OldPixelConsumed(info.tierIndex);

        Vector3 target;
        if (m == chipper)
        {
            chipped++;
            PixelStats.Count("wood.chipped", 1);
            m.feedTimer = 1.1f;
            target = m.root.transform.TransformPoint(new Vector3(0f, 1.9f, 0f) * m.scale / Mathf.Max(0.0001f, m.scale));
            PixelAudio.Play("chipper_feed");
            Chips();
            SetChipperLabel();
        }
        else
        {
            PixelStats.Count("wood.stored", 1);
            target = AddLog(m, type == PixelClicker.PixelType.Heartwood);
            PixelAudio.Play("rack_store");
        }
        StartCoroutine(FlyIn(body, target));
        BonusText(m, bonus);
    }

    private IEnumerator FlyIn(Rigidbody body, Vector3 target)
    {
        if (body == null) yield break;
        Transform t = body.transform;
        foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;
        body.isKinematic = true;
        OldPixelDespawn d = body.GetComponent<OldPixelDespawn>();
        if (d != null) d.enabled = false;
        Vector3 start = t.position, startScale = t.localScale;
        for (float time = 0f; time < 0.35f; time += Time.deltaTime)
        {
            if (t == null) yield break;
            float k = Mathf.Clamp01(time / 0.35f);
            float e = k * k;
            t.position = Vector3.Lerp(start, target, e) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.25f);
            t.localScale = startScale * (1f - e * 0.9f);
            t.Rotate(0f, 520f * Time.deltaTime, 0f, Space.World);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    // ------------------------------------------------------------------
    // Effects and labels
    // ------------------------------------------------------------------

    private void Chips()
    {
        if (chipper == null || chipper.fx == null) return;
        chipper.fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        chipper.fx.Play();
    }

    private void BonusText(Machine m, double bonus)
    {
        if (m == null || bonus == 0d) return;
        StartCoroutine(RisingText(m.root.transform.position + Vector3.up * (m.scale * (m.isRack ? 2.6f : 2.5f)),
                                  (bonus > 0d ? "+" : "") + PixelClicker.FormatNumberShort(bonus) + " bonus XP", bonus > 0d ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f), m.scale));
    }

    private IEnumerator RisingText(Vector3 from, string text, Color color, float scale)
    {
        GameObject go = new GameObject("Bonus Text");
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (clicker.UIFont != null) tmp.font = clicker.UIFont;
        tmp.text = text;
        tmp.fontSize = 3.2f * Mathf.Max(0.4f, scale);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        go.transform.position = from;
        for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
        {
            if (go == null) yield break;
            float k = t / 1.4f;
            go.transform.position = from + Vector3.up * (k * scale * 1.2f);
            if (cam != null) go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
            tmp.color = new Color(color.r, color.g, color.b, 1f - k * k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private void SetChipperLabel()
    {
        if (chipper != null && chipper.label != null) chipper.label.text = "WOODCHIPPER\n<size=70%>chipped: " + chipped + "</size>";
    }

    private void SetRackLabel()
    {
        if (rack != null && rack.label != null) rack.label.text = "LOG RACK\n<size=70%>" + rack.stored + " / " + Capacity + " logs</size>";
    }

    private int Capacity => Mathf.Min(LogsPerShelf * Shelves, rackCapacity > 0 ? rackCapacity : LogsPerShelf * Shelves);

    // ------------------------------------------------------------------
    // The log rack's contents
    // ------------------------------------------------------------------

    /// <summary>Lays one more log on the rack (a truck takes them all once it is full). Returns where the log is going, in world space.</summary>
    private Vector3 AddLog(Machine m, bool heart)
    {
        if (m.stored >= Capacity) ClearRack(m);
        int index = m.stored;
        int shelf = index / LogsPerShelf, slot = index % LogsPerShelf;
        Vector3 local = new Vector3(-1.2f + slot * 0.6f, 0.52f + shelf * 0.7f, 0f);
        GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log.name = "Log";
        Destroy(log.GetComponent<Collider>());
        log.transform.SetParent(m.root.transform, false);
        log.transform.localPosition = local;
        log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        log.transform.localScale = new Vector3(0.34f, 0.4f, 0.34f);
        Renderer r = log.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material mat = clicker.CreateVisualMaterial(heart ? new Color(0.78f, 0.33f, 0.25f) : new Color(0.58f, 0.4f, 0.22f), false);
        if (mat != null) r.sharedMaterial = mat;
        log.SetActive(false);   // appears when the block arrives
        m.logs.Add(log);
        m.stored++;
        StartCoroutine(ShowLogLater(log, 0.3f));
        SetRackLabel();
        return m.root.transform.TransformPoint(local);
    }

    private IEnumerator ShowLogLater(GameObject log, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (log != null) log.SetActive(true);
    }

    private void ClearRack(Machine m)
    {
        foreach (GameObject log in m.logs) if (log != null) Destroy(log);   // a truck comes by
        m.logs.Clear();
        m.stored = 0;
        PixelHints.Announce("A truck took the full rack of logs!");
        SetRackLabel();
    }

    // ------------------------------------------------------------------
    // Building the machines (code-made primitives; +X = screen right, -Z = towards the camera; units = main-pixel widths, scaled by the root)
    // ------------------------------------------------------------------

    private Transform Part(string name, PrimitiveType shape, Transform parent, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material mat = clicker.CreateVisualMaterial(color, false);
        if (mat != null) r.sharedMaterial = mat; else r.material.color = color;
        return go.transform;
    }

    private TextMeshPro MakeLabel(Transform root, Vector3 pos)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (clicker.UIFont != null) tmp.font = clicker.UIFont;
        tmp.fontSize = 2.4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
        return tmp;
    }

    private Machine BuildChipper()
    {
        Machine m = new Machine { root = new GameObject("Woodchipper"), isRack = false };
        Transform r = m.root.transform;
        Color orange = new Color(0.95f, 0.55f, 0.12f), dark = new Color(0.12f, 0.12f, 0.15f), steel = new Color(0.62f, 0.66f, 0.72f);
        Part("Base", PrimitiveType.Cube, r, new Vector3(0f, 0.55f, 0f), new Vector3(2.3f, 0.5f, 1.9f), dark);
        Part("Body", PrimitiveType.Cube, r, new Vector3(0f, 1.2f, 0f), new Vector3(2.2f, 0.9f, 1.8f), orange);
        // Wheels.
        foreach (float x in new[] { -0.85f, 0.85f })
            Part("Wheel", PrimitiveType.Cylinder, r, new Vector3(x, 0.32f, 0f), new Vector3(0.64f, 0.12f, 0.64f), dark, new Vector3(0f, 0f, 90f));
        // Hopper: a square tube on top with a dark mouth and spinning blades inside.
        Part("Hopper Front", PrimitiveType.Cube, r, new Vector3(0f, 1.95f, -0.8f), new Vector3(1.8f, 0.9f, 0.12f), orange);
        Part("Hopper Back", PrimitiveType.Cube, r, new Vector3(0f, 1.95f, 0.8f), new Vector3(1.8f, 0.9f, 0.12f), orange);
        Part("Hopper Left", PrimitiveType.Cube, r, new Vector3(-0.84f, 1.95f, 0f), new Vector3(0.12f, 0.9f, 1.6f), orange);
        Part("Hopper Right", PrimitiveType.Cube, r, new Vector3(0.84f, 1.95f, 0f), new Vector3(0.12f, 0.9f, 1.6f), orange);
        Part("Hopper Mouth", PrimitiveType.Cube, r, new Vector3(0f, 1.66f, 0f), new Vector3(1.55f, 0.06f, 1.55f), dark);
        GameObject spin = new GameObject("Blades");
        spin.transform.SetParent(r, false);
        spin.transform.localPosition = new Vector3(0f, 1.78f, 0f);
        m.spinner = spin.transform;
        Part("Blade A", PrimitiveType.Cube, m.spinner, Vector3.zero, new Vector3(1.4f, 0.06f, 0.16f), steel);
        Part("Blade B", PrimitiveType.Cube, m.spinner, Vector3.zero, new Vector3(0.16f, 0.06f, 1.4f), steel);
        Part("Hub", PrimitiveType.Cylinder, m.spinner, Vector3.zero, new Vector3(0.28f, 0.06f, 0.28f), dark);
        // Chute out of the right side, angled down, with a catch box.
        Part("Chute", PrimitiveType.Cube, r, new Vector3(1.55f, 1.0f, 0f), new Vector3(1.0f, 0.24f, 0.6f), steel, new Vector3(0f, 0f, -28f));
        Part("Chute Lip", PrimitiveType.Cube, r, new Vector3(1.98f, 0.72f, 0f), new Vector3(0.24f, 0.14f, 0.66f), dark, new Vector3(0f, 0f, -28f));
        // Control panel with a green light, and an exhaust stack.
        Part("Panel", PrimitiveType.Cube, r, new Vector3(-0.55f, 1.2f, -0.92f), new Vector3(0.7f, 0.5f, 0.06f), dark);
        Part("Light", PrimitiveType.Sphere, r, new Vector3(-0.7f, 1.3f, -0.97f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.3f, 1f, 0.4f));
        Part("Light 2", PrimitiveType.Sphere, r, new Vector3(-0.4f, 1.3f, -0.97f), new Vector3(0.14f, 0.14f, 0.14f), new Color(1f, 0.35f, 0.3f));
        Part("Stack", PrimitiveType.Cylinder, r, new Vector3(-0.9f, 1.95f, 0.4f), new Vector3(0.22f, 0.45f, 0.22f), steel);

        // Wood chips shoot out of the chute when something goes in.
        PixelFx.Spec spec = new PixelFx.Spec
        {
            lifetime = new Vector2(0.5f, 0.9f), speed = new Vector2(1.4f, 3.2f), size = new Vector2(0.07f, 0.15f), gravity = 2.4f,
            colorA = new Color(0.82f, 0.62f, 0.38f), colorB = new Color(0.55f, 0.38f, 0.2f), burst = 22, maxParticles = 40,
            shape = ParticleSystemShapeType.Cone, shapeAngle = 28f, shapeRadius = 0.18f, randomDirection = 0.15f, shapeRotation = new Vector3(0f, 90f, -24f),
        };
        m.fx = PixelFx.Make(r, "Chips", spec);
        m.fx.transform.localPosition = new Vector3(2.0f, 0.7f, 0f);
        m.label = MakeLabel(r, new Vector3(0f, 3.1f, 0f));
        m.root.SetActive(false);
        chipper = m;
        SetChipperLabel();
        return m;
    }

    private Machine BuildRack()
    {
        Machine m = new Machine { root = new GameObject("Log Rack"), isRack = true };
        Transform r = m.root.transform;
        Color wood = new Color(0.55f, 0.36f, 0.18f), woodDark = new Color(0.38f, 0.24f, 0.12f);
        foreach (float x in new[] { -1.5f, 1.5f })
            foreach (float z in new[] { -0.45f, 0.45f })
                Part("Post", PrimitiveType.Cube, r, new Vector3(x, 1.2f, z), new Vector3(0.16f, 2.4f, 0.16f), woodDark);
        for (int shelf = 0; shelf < Shelves; shelf++)
            Part("Shelf " + shelf, PrimitiveType.Cube, r, new Vector3(0f, 0.28f + shelf * 0.7f, 0f), new Vector3(3.2f, 0.1f, 1.1f), wood);
        Part("Roof", PrimitiveType.Cube, r, new Vector3(0f, 2.45f, 0f), new Vector3(3.5f, 0.1f, 1.4f), woodDark);
        Part("Back", PrimitiveType.Cube, r, new Vector3(0f, 1.3f, 0.5f), new Vector3(3.1f, 2.0f, 0.05f), new Color(0.3f, 0.2f, 0.1f));
        m.label = MakeLabel(r, new Vector3(0f, 3.0f, 0f));
        m.root.SetActive(false);
        rack = m;
        SetRackLabel();
        return m;
    }
}
