using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Ultra Holes (the reworked Ultra Pad; the minigame id stays "pad"). Every so often FOUR round holes open in the floor, one near each corner of
/// the screen. They are plain black voids: old pixels that tumble in fall away and are gone. One of the holes is the CORRECT one, marked by a model
/// of the wanted pixel type spinning in its centre. A pixel of that type dropped into the correct hole pays out (its normal value) and has a chance
/// at an Ultra pixel; anything else that falls into any hole is simply taken away. After every correct drop the marker jumps to another hole,
/// and it also jumps if nothing is deposited for a while. The round ends on a timer. A very gentle pull around the edge of every hole helps
/// nudge pixels in (nothing is drawn for it). Ultra pixels are ONE general currency (PixelClicker.TotalUltra).
///
/// Add this to any GameObject (e.g. the cube). PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelPadMinigame : PixelScheduledMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (for the camera, the husks and the pixel counts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Ultra Pixels")]
    [Range(0f, 1f)]
    [Tooltip("Chance that each CORRECT pixel gives you an Ultra pixel (0.1 = 10%).")]
    [SerializeField] private float ultraChance = 0.1f;

    [Min(1)]
    [Tooltip("The goal shown on the tracker's progress bar (Ultra pixels ever earned; the count keeps going).")]
    [SerializeField] private double ultraGoal = 10;

    [Tooltip("Title of the tracker row in the shop.")]
    [SerializeField] private string trackerTitle = "Ultra Pixels";

    [TextArea(1, 3)]
    [Tooltip("Description of the tracker row.")]
    [SerializeField] private string trackerDescription = "Drop the pixel a hole wants into the marked hole for a chance at Ultra pixels. Your Ultra pixels are shown at the top of the Inventory.";

    [Tooltip("Text on a locked shop pack that needs the goal. {0} = Ultra pixels, {1} = goal.")]
    [SerializeField] private string requirementFormat = "Requires: {0} / {1} Ultra pixels";

    [Header("The Round")]
    [Tooltip("How long the four holes stay open (seconds). 0 = 45.")]
    [SerializeField] private float roundSeconds = 0f;

    [Tooltip("If nothing is dropped in the correct hole for this many seconds, the marker jumps to another hole. 0 = 12.")]
    [SerializeField] private float swapIdleSeconds = 0f;

    [Tooltip("Off (default): the wanted pixel type stays the same for the whole round and only the marked hole changes. On: the type changes with every jump too.")]
    [SerializeField] private bool changeTypeOnSwap = false;

    [Header("The Holes")]
    [Tooltip("Radius of a hole in main-pixel widths. 0 = 1.3.")]
    [SerializeField] private float holeRadiusPixels = 0f;

    [Tooltip("Where the holes open, as viewport fractions in from the screen edge (x) and in from the black bars (y). The four corners use these.")]
    [SerializeField] private Vector2 cornerInset = new Vector2(0.12f, 0.1f);

    [Tooltip("A husk whose middle comes inside this fraction of the radius (and low enough) falls in. 0 = 0.85.")]
    [SerializeField] private float captureFraction = 0f;

    [Tooltip("Layers that count as ground when picking a spot.")]
    [SerializeField] private LayerMask floorLayers = ~0;

    [Tooltip("If no ground is under a corner, the hole goes on a flat plane at this height instead.")]
    [SerializeField] private float fallbackFloorY = 0f;

    [Tooltip("Colour of the dark ring round each hole.")]
    [SerializeField] private Color rimColor = new Color(0.16f, 0.16f, 0.19f, 1f);

    [Tooltip("Spin speed of the marker model (degrees per second).")]
    [SerializeField] private float modelSpin = 60f;

    [Header("Pull")]
    [Min(0f)]
    [Tooltip("A very gentle pull towards the middle of a hole, only in a thin band round its edge (acceleration). Nothing is drawn for it. 0 = none.")]
    [SerializeField] private float pullAcceleration = 5f;

    [Tooltip("Width of the pull band outside the hole's edge, as a fraction of the radius. 0 = 0.5.")]
    [SerializeField] private float pullBandFraction = 0f;

    [Header("Feedback")]
    [Tooltip("Text when a correct pixel falls in.")]
    [SerializeField] private string correctText = "+";

    [Tooltip("Text when you get an Ultra. {0} = pixel name.")]
    [SerializeField] private string ultraFormat = "ULTRA {0}!";

    [Tooltip("Text when a pixel is lost to a wrong hole.")]
    [SerializeField] private string wrongText = "x";

    [Tooltip("Text at the top of the screen while a round runs. {0} = seconds left, {1} = the wanted pixel.")]
    [SerializeField] private string hudFormat = "ULTRA HOLES   {0}s   -   {1}";

    [SerializeField] private Color correctColor = new Color(0.6f, 1f, 0.65f, 1f);
    [SerializeField] private Color ultraColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color wrongColor = new Color(1f, 0.4f, 0.35f, 1f);

    [Min(0.5f)]
    [Tooltip("Size of the rising text (3D text: about 10 per world unit).")]
    [SerializeField] private float messageFontSize = 4.5f;

    [Min(0.1f)]
    [Tooltip("How long the rising text lasts (seconds).")]
    [SerializeField] private float messageSeconds = 1.1f;

    [Header("Events")]
    [Tooltip("Fired when a round starts.")]
    public UnityEvent onPadAppeared;

    [Tooltip("Fired when you receive an Ultra pixel.")]
    public UnityEvent onUltraGained;

    private class Hole
    {
        public Transform root;
        public Transform model;
        public Vector3 centre;
        public float scale;   // 0..1: how far the marker model has grown
    }

    private readonly List<Hole> holes = new List<Hole>();
    private readonly object[] claimOwners = { new object(), new object(), new object(), new object() };   // one area claim per hole, reused every round
    private bool padActive, pulling;
    private float holeRadius;

    // This minigame's own defaults for the shared round schedule (PixelScheduledMinigame; a scene's values override them).
    public PixelPadMinigame()
    {
        firstRoundDelay = 15f;
        minInterval = 45f;
        maxInterval = 90f;
    }

    public override bool Busy => padActive;

    protected override void OnDespawned()
    {
        padActive = false;
        pulling = false;
        holes.Clear();
        foreach (object owner in claimOwners) ReleaseArea(owner);
        ScheduleNext();
    }

    public override string Id => "pad";
    public override string DisplayName => "Ultra Holes";

    // --- Tracker: Ultra pixels ever earned ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => clicker != null ? clicker.UltraEarned : 0d;
    public override void SetTrackerCount(double value) { if (clicker != null) clicker.SetUltraEarned((long)System.Math.Floor(value)); }
    public override double TrackerGoal => ultraGoal;
    public override string RequirementFormat => requirementFormat;

    protected override void Awake()
    {
        base.Awake();
        clicker = clicker != null ? clicker : PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelPadMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        StartSchedule();
    }

    private void Update()
    {
        if (!running || padActive) return;
        if (SpawnDue(ref spawnTimer)) StartCoroutine(RoundRoutine());
    }

    /// <summary>Opens the holes right now (right-click the component &gt; Spawn Pad Now).</summary>
    [ContextMenu("Spawn Pad Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !padActive) StartCoroutine(RoundRoutine());
    }

    // ------------------------------------------------------------------
    // Picking what and where
    // ------------------------------------------------------------------

    /// <summary>A random unlocked pixel type that can actually lie on the floor and is in the rotation being played (the current skill pack's blocks, or the standard pixels; never a hazard), or -1.</summary>
    private int PickTargetTier(int avoid = -1)
    {
        List<int> candidates = new List<int>();
        for (int i = 0; i < clicker.Tiers.Length; i++)
        {
            PixelClicker.PixelTier t = clicker.Tiers[i];
            if (t.unlocked && !t.rareDrop && !t.flyAway && i != avoid && !PixelClicker.IsHazard(t.type) && PixelSkills.InCurrentRotation(t.type)) candidates.Add(i);
        }
        if (candidates.Count == 0 && avoid >= 0) return avoid;
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : -1;
    }

    private float BarFraction()
    {
        return PixelHud.Instance != null
            ? PixelHud.Instance.RawBarHeight * Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f) / Mathf.Max(1, Screen.height) : 0f;
    }

    private bool FloorPoint(Camera cam, Vector2 viewport, out Vector3 point)
    {
        point = Vector3.zero;
        Ray ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, floorLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<OldPixelInfo>() != null) continue;
            if (clicker.PixelTransform != null && hit.transform.IsChildOf(clicker.PixelTransform)) continue;
            if (hit.normal.y < 0.5f) continue;
            point = hit.point;
            return true;
        }
        Plane floor = new Plane(Vector3.up, new Vector3(0f, fallbackFloorY, 0f));
        if (floor.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
        return false;
    }

    /// <summary>The floor spot for one corner: the viewport corner nudged towards the middle until the whole hole is on screen and clear of other minigames.</summary>
    private bool CornerSpot(Camera cam, Vector2 corner, out Vector3 point)
    {
        point = Vector3.zero;
        float bars = BarFraction();
        Vector2 centre = new Vector2(0.5f, 0.5f);
        for (int step = 0; step < 8; step++)
        {
            Vector2 v = Vector2.Lerp(corner, centre, step * 0.05f);
            if (!FloorPoint(cam, v, out Vector3 p)) continue;
            bool visible = true;
            for (int k = 0; k < 4 && visible; k++)
            {
                float a = k * Mathf.PI * 0.5f;
                Vector3 edge = cam.WorldToViewportPoint(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * holeRadius);
                if (edge.z <= 0f || edge.x < 0.01f || edge.x > 0.99f || edge.y < bars + 0.01f || edge.y > 1f - bars - 0.01f) visible = false;
            }
            if (!visible || !AreaFree(this, p, holeRadius * 1.15f)) continue;
            point = p;
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // The holes
    // ------------------------------------------------------------------

    private GameObject Disc(Transform parent, string name, float radius, float height, float y, Color color)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        g.transform.localPosition = new Vector3(0f, y, 0f);
        Material m = clicker.CreateVisualMaterial(color, false);
        Renderer r = g.GetComponent<Renderer>();
        if (m != null) r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    private Hole BuildHole(Vector3 point)
    {
        GameObject root = new GameObject("Ultra Hole");
        Track(root);
        root.transform.position = point;
        Disc(root.transform, "Rim", holeRadius * 1.14f, 0.03f, 0.015f, rimColor);
        Disc(root.transform, "Void", holeRadius, 0.04f, 0.03f, new Color(0.02f, 0.02f, 0.03f, 1f));
        Disc(root.transform, "Deep", holeRadius * 0.72f, 0.04f, 0.036f, Color.black);
        return new Hole { root = root.transform, centre = point };
    }

    private void SetModel(Hole h, int target)
    {
        if (h.model != null) Destroy(h.model.gameObject);
        float pb = clicker.PixelBaseSize;
        GameObject model = clicker.CreateDisplayPixel(target, h.root, 0.6f * pb);
        h.model = model != null ? model.transform : null;
    }

    private void AnimateModels(int correct, float time, float dt)
    {
        float pb = clicker.PixelBaseSize;
        for (int i = 0; i < holes.Count; i++)
        {
            Hole h = holes[i];
            h.scale = Mathf.MoveTowards(h.scale, i == correct ? 1f : 0f, dt * 3f);
            if (h.model == null) continue;
            bool show = h.scale > 0.01f;
            if (h.model.gameObject.activeSelf != show) h.model.gameObject.SetActive(show);
            if (!show) continue;
            h.model.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, h.scale);
            h.model.localPosition = new Vector3(0f, 0.55f * pb + Mathf.Sin(time * 1.8f) * 0.04f * pb, 0f);
            h.model.localRotation = Quaternion.Euler(0f, time * modelSpin, 0f);
        }
    }

    private void FixedUpdate()
    {
        if (!pulling || pullAcceleration <= 0f || holes.Count == 0) return;
        float band = holeRadius * (pullBandFraction > 0f ? pullBandFraction : 0.5f);
        float pb = clicker.PixelBaseSize;
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody body = list[i];
            if (body == null || body.isKinematic) continue;
            if (clicker.IsFlyingPixel(body)) continue;
            OldPixelDespawn d = PixelHuskQuery.Get(body).despawn;
            if (d != null && (d.Held || d.IsDespawning)) continue;
            foreach (Hole h in holes)
            {
                Vector3 to = h.centre - body.position; to.y = 0f;
                float dist = to.magnitude;
                if (dist <= holeRadius || dist > holeRadius + band || body.position.y > h.centre.y + 1.5f * pb) continue;
                float fade = 1f - (dist - holeRadius) / band;   // strongest right at the edge, gone at the outer edge of the band
                body.AddForce(to / Mathf.Max(0.001f, dist) * pullAcceleration * fade, ForceMode.Acceleration);
                break;
            }
        }
    }

    // ------------------------------------------------------------------
    // The round
    // ------------------------------------------------------------------

    private IEnumerator RoundRoutine()
    {
        padActive = true;
        foreach (object owner in claimOwners) ReleaseArea(owner);   // last round's claims are still 'active' (padActive is true again) and would block every corner
        Camera cam = clicker.ViewCamera;
        int target = PickTargetTier();
        holeRadius = (holeRadiusPixels > 0f ? holeRadiusPixels : 1.3f) * Mathf.Max(0.1f, clicker.PixelBaseSize);
        if (cam == null || target < 0)
        {
            spawnTimer = 5f;
            padActive = false;
            yield break;
        }

        // One hole near each corner of the screen (a corner that is blocked is skipped; at least two are needed).
        holes.Clear();
        float bars = BarFraction();
        float x0 = cornerInset.x, x1 = 1f - cornerInset.x;
        float y0 = bars + cornerInset.y, y1 = 1f - bars - cornerInset.y;
        Vector2[] corners = { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x0, y1), new Vector2(x1, y1) };
        foreach (Vector2 c in corners)
            if (CornerSpot(cam, c, out Vector3 spot))
            {
                Hole h = BuildHole(spot);
                holes.Add(h);
                ClaimArea(claimOwners[Mathf.Min(holes.Count - 1, claimOwners.Length - 1)], spot, holeRadius * 1.15f, () => padActive);
            }
        if (holes.Count < 2)
        {
            foreach (Hole h in holes) if (h.root != null) Destroy(h.root.gameObject);
            holes.Clear();
            spawnTimer = 5f;
            padActive = false;
            yield break;
        }
        foreach (Hole h in holes) SetModel(h, target);

        onPadAppeared?.Invoke();
        Report(MinigameEvent.Spawned);

        // The timer line at the top of the screen.
        GameObject hudCanvas = PixelUIKit.CreateCanvas("Ultra Holes HUD", 140, new Vector2(1920f, 1080f), false);
        Track(hudCanvas);
        TMP_Text hud = PixelStatusBox.Create(clicker.UIFont, hudCanvas.transform, 34f);   // slides out from under the top bar

        int correct = Random.Range(0, holes.Count);
        float anim = 0f, idle = 0f;
        float total = roundSeconds > 0f ? roundSeconds : 45f, left = total;
        float idleLimit = swapIdleSeconds > 0f ? swapIdleSeconds : 12f;
        int dropped = 0, ultras = 0;

        // Grow in.
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 0.4f);
            foreach (Hole h in holes) if (h.root != null) h.root.localScale = new Vector3(k, 1f, k);
            yield return null;
        }
        foreach (Hole h in holes) if (h.root != null) h.root.localScale = Vector3.one;
        pulling = true;

        while (left > 0f)
        {
            float dt = Time.deltaTime;
            left -= dt;
            idle += dt;
            anim += dt;

            if (idle >= idleLimit) { correct = NextHole(correct); if (changeTypeOnSwap) target = ChangeType(target); idle = 0f; }
            AnimateModels(correct, anim, dt);
            PixelUIKit.SetText(hud, string.Format(hudFormat, Mathf.CeilToInt(Mathf.Max(0f, left)), clicker.Tiers[target].displayName));

            int result = TakePixels(cam, correct, target);   // 0 nothing, 1 wrong ones taken, 2 a correct drop
            if (result >= 2)
            {
                dropped++;
                correct = NextHole(correct);
                if (changeTypeOnSwap) target = ChangeType(target);
                idle = 0f;
                if (lastUltra) ultras++;
            }
            yield return null;
        }

        // Close.
        pulling = false;
        t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float k = Mathf.Lerp(1f, 0f, t / 0.4f);
            foreach (Hole h in holes) if (h.root != null) h.root.localScale = new Vector3(k, 1f, k);
            yield return null;
        }
        foreach (Hole h in holes) if (h.root != null) Destroy(h.root.gameObject);
        holes.Clear();
        Destroy(hudCanvas);
        if (dropped > 0) PixelHints.Announce("Ultra Holes: " + dropped + " correct" + (ultras > 0 ? ", " + ultras + " Ultra!" : "."));

        ScheduleNext();
        padActive = false;
        foreach (object owner in claimOwners) ReleaseArea(owner);
    }

    private int NextHole(int current)
    {
        if (holes.Count <= 1) return 0;
        int n = Random.Range(0, holes.Count - 1);
        return n >= current ? n + 1 : n;
    }

    private int ChangeType(int target)
    {
        int next = PickTargetTier(target);
        if (next < 0 || next == target) return target;
        foreach (Hole h in holes) SetModel(h, next);
        return next;
    }

    private bool lastUltra;

    /// <summary>Takes every old pixel that has fallen into a hole. Returns 2 if a correct pixel went into the marked hole, 1 if only wrong ones were taken, else 0.</summary>
    private int TakePixels(Camera cam, int correct, int target)
    {
        lastUltra = false;
        int result = 0;
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        float pb = clicker.PixelBaseSize;
        float reach = holeRadius * (captureFraction > 0f ? captureFraction : 0.85f);

        for (int i = list.Count - 1; i >= 0; i--)
        {
            Rigidbody body = list[i];
            if (body == null || clicker.IsFlyingPixel(body) || body.isKinematic) continue;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && (despawn.Held || despawn.IsDespawning)) continue;

            for (int hi = 0; hi < holes.Count; hi++)
            {
                Hole h = holes[hi];
                Vector3 to = body.position - h.centre; to.y = 0f;
                if (to.magnitude > reach || body.position.y > h.centre.y + 1.5f * pb) continue;

                OldPixelInfo info = body.GetComponent<OldPixelInfo>();
                if (info == null) break;
                bool right = hi == correct && info.tierIndex == target;
                if (!clicker.ReleaseOldPixel(body, right)) break;   // (the credit only pays with husksPayCurrency on)
                if (right) clicker.GiveHuskXp(info.tierIndex, 1);   // pixel rules: a correctly sorted husk gives husk XP
                if (despawn != null) despawn.Begin();   // it turns into a trigger and shrinks as it tumbles down through the floor

                Vector3 at = body.position;
                if (right)
                {
                    Report(MinigameEvent.Clicked);
                    result = 2;
                    if (Random.value < ultraChance)
                    {
                        lastUltra = true;
                        clicker.AddUltra(1);
                        PixelStats.Count("ultra.pad");
                        onUltraGained?.Invoke();
                        PixelAudio.Play("pad_ultra");
                        PixelUltraFx.Play(clicker, at, text: false, sound: false);
                        StartCoroutine(RisingMessage(at, cam, string.Format(ultraFormat, clicker.Tiers[target].displayName), ultraColor, 1.4f));
                    }
                    else StartCoroutine(RisingMessage(at, cam, correctText, correctColor, 1f));
                }
                else
                {
                    if (result < 1) result = 1;
                    PixelAudio.Play("pad_wrong");
                    StartCoroutine(RisingMessage(at, cam, wrongText, wrongColor, 0.8f));
                }
                break;
            }
        }
        return result;
    }

    private IEnumerator RisingMessage(Vector3 start, Camera cam, string message, Color color, float sizeScale)
    {
        GameObject go = new GameObject("Hole Message");
        Track(go);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = messageFontSize * sizeScale;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Overflow;
        if (clicker.UIFont != null) text.font = clicker.UIFont;
        text.rectTransform.sizeDelta = new Vector2(20f, 3f);

        float t = 0f;
        while (t < messageSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / messageSeconds);
            go.transform.position = start + cam.transform.up * (0.5f + k * 1.2f) - cam.transform.forward * 0.4f;
            go.transform.rotation = cam.transform.rotation;
            Color c = color;
            c.a *= 1f - k * k;
            text.color = c;
            yield return null;
        }
        Destroy(go);
    }
}
