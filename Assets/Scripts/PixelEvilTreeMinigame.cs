// ---- System notes ----
// PixelEvilTreeMinigame.cs
//   Evil Tree minigame (id eviltree, pack unlocksMinigame = "eviltree", Minigames tab, needs the Lumberjack's Pack; extends PixelMinigame): now and then (firstDelay 60 s, then minInterval 150 ..
//   maxInterval 300 s, PixelMinigameLimits.AllowAnother) a sapling sprouts on the floor near the cube (PixelBait.FindGround, kept inside the view) and GROWS over growSeconds (14) into a giant tree
//   (heightInCubes 5 cubes tall) with glowing eyes and a jagged mouth. It FLAILS so it is hard to click: the whole tree leans and jerks to new spots (the click colliders move with it), its arms
//   whip about, and it flails harder as it grows. It has a HEALTH BAR (health 40, each manual click on it does damagePerClick 1; the auto clicker never hits it): at 0 it topples over and pays
//   a random minWood..maxWood MAGIC WOOD (the minigame's tracker / material: PixelMinigame.MaterialName, shown in the Inventory's Materials tab and Crafting, saved like any minigame count)
//   and Woodcutting XP only (SkillXpTarget = Woodcutting, default 30 XP, whatever skill is in training; needs the Woodcutting skill bought). If it is not felled within stayAfterGrownSeconds (30)
//   of being fully grown it sinks back into the ground. Everything is built from primitives at run time (no assets). Magic Wood has no use yet (crafting later).
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static PixelInput;

public class PixelEvilTreeMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, cube position, floor). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;
    [Tooltip("Is the minigame running? (The shop turns this on when the Evil Tree pack is bought.)")]
    [SerializeField] private bool running = false;

    [Header("Magic wood (the material it gives)")]
    [Min(1)]
    [Tooltip("The tracker's goal (nothing requires it yet; it only fills the bar in the shop).")]
    [SerializeField] private double woodGoal = 100;
    [Tooltip("Runtime: magic wood held (saved with the game).")]
    [SerializeField] private double magicWood = 0;
    [Min(1)]
    [Tooltip("Least magic wood a felled tree pays.")]
    [SerializeField] private int minWood = 6;
    [Min(1)]
    [Tooltip("Most magic wood a felled tree pays.")]
    [SerializeField] private int maxWood = 12;
    [Tooltip("Title of this minigame's tracker row in the shop.")]
    [SerializeField] private string trackerTitle = "Magic Wood";
    [TextArea(1, 3)]
    [Tooltip("Description of the tracker row.")]
    [SerializeField] private string trackerDescription = "Fell the evil tree when it sprouts. It pays Magic Wood and Woodcutting XP.";
    [Tooltip("Text on a locked shop pack that needs the goal. {0} = wood held, {1} = goal.")]
    [SerializeField] private string requirementFormat = "Requires: {0} / {1} magic wood";

    [Header("When it appears")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame starts before the first tree.")]
    [SerializeField] private float firstDelay = 60f;
    [Min(1f)]
    [Tooltip("Shortest wait between trees (seconds).")]
    [SerializeField] private float minInterval = 150f;
    [Min(1f)]
    [Tooltip("Longest wait between trees (seconds).")]
    [SerializeField] private float maxInterval = 300f;

    [Header("The tree")]
    [Min(1f)]
    [Tooltip("Hits it takes to fell it (its health bar).")]
    [SerializeField] private float health = 40f;
    [Min(0.1f)]
    [Tooltip("Health each click takes off.")]
    [SerializeField] private float damagePerClick = 1f;
    [Min(1f)]
    [Tooltip("Seconds it takes to grow from a sapling to a giant.")]
    [SerializeField] private float growSeconds = 14f;
    [Min(1f)]
    [Tooltip("How tall the full-grown tree is, in cube widths.")]
    [SerializeField] private float heightInCubes = 5f;
    [Range(0.05f, 0.5f)]
    [Tooltip("How big the sapling starts, as a fraction of the full size.")]
    [SerializeField] private float startFraction = 0.12f;
    [Min(5f)]
    [Tooltip("Seconds it stays once fully grown before sinking away.")]
    [SerializeField] private float stayAfterGrownSeconds = 30f;

    [Header("Flailing")]
    [Min(0f)]
    [Tooltip("How violently it flails (1 = default; 0 = it stands still).")]
    [SerializeField] private float flailStrength = 1f;
    [Min(0.1f)]
    [Tooltip("Seconds between its jerks to a new spot (shorter = harder to click).")]
    [SerializeField] private float jerkSeconds = 0.9f;

    [Header("Look")]
    [Tooltip("Bark colour.")]
    [SerializeField] private Color barkColor = new Color(0.27f, 0.17f, 0.2f, 1f);
    [Tooltip("Leaf colour.")]
    [SerializeField] private Color leafColor = new Color(0.32f, 0.12f, 0.45f, 1f);
    [Tooltip("Colour of the eyes and mouth glow.")]
    [SerializeField] private Color eyeColor = new Color(1f, 0.25f, 0.2f, 1f);

    private float spawnTimer;
    private bool treeActive;
    private float lastHitTime;

    public override string Id => "eviltree";
    public override string DisplayName => "Evil Tree";
    public override bool Running => running;
    public override bool Busy => treeActive;
    protected override string SkillXpTarget => PixelSkills.WoodcuttingId;

    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => magicWood;
    public override double TrackerGoal => woodGoal;
    public override string RequirementFormat => requirementFormat;
    public override string MaterialName => "Magic Wood";
    public override double MaterialAmount => magicWood;
    public override Color MaterialColor => new Color(0.75f, 0.45f, 1f, 1f);
    public override void SetTrackerCount(double value) { magicWood = System.Math.Max(0d, value); }

    protected override void OnDespawned()
    {
        treeActive = false;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }

    protected override void Awake()
    {
        base.Awake();
        clicker = clicker != null ? clicker : PixelFind.First<PixelClicker>();
        if (clicker == null) { enabled = false; return; }
        if (startRunning) running = true;
        spawnTimer = firstDelay;
    }

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstDelay;
    }

    public override void Deactivate() => running = false;

    [ContextMenu("Sprout Evil Tree Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !treeActive) StartCoroutine(TreeRoutine());
    }

    private void Update()
    {
        if (!running || treeActive || UserDisabled) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        if (PixelMinigameLimits.AllowAnother(this) && !PixelCalm.InMenu && !TakeoverActive) StartCoroutine(TreeRoutine());
        else spawnTimer = PixelMinigameLimits.RetrySeconds;
    }

    // ------------------------------------------------------------------
    // Picking a spot
    // ------------------------------------------------------------------

    private bool TryPickSpot(Camera cam, out Vector3 ground)
    {
        ground = default;
        if (clicker.PixelTransform == null) return false;
        float size = clicker.PixelBaseSize;
        for (int attempt = 0; attempt < 25; attempt++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(2.2f, 6.5f) * size;
            Vector3 probe = clicker.PixelTransform.position + new Vector3(r.x, 0f, r.y);
            if (!PixelBait.FindGround(clicker, probe, size, out Vector3 g)) continue;
            Vector3 vp = cam.WorldToViewportPoint(g + Vector3.up * size * heightInCubes * 0.5f);
            if (vp.z <= 0f || vp.x < 0.14f || vp.x > 0.86f || vp.y < 0.2f || vp.y > 0.8f) continue;
            ground = g;
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // The tree
    // ------------------------------------------------------------------

    private class Arm { public Transform pivot; public float speed, phase, reach; }

    private Material Flat(Color c)
    {
        Shader s = PixelShaders.SpriteDefault();
        Material m = s != null ? new Material(s) : clicker.CreateVisualMaterial(c, false);
        if (m != null && m.HasProperty("_Color")) m.color = c;
        return m;
    }

    private Transform Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, bool collider)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Collider c = g.GetComponent<Collider>();
        if (c != null) { if (collider) { c.isTrigger = true; } else Destroy(c); }
        Renderer r = g.GetComponent<Renderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    private IEnumerator TreeRoutine()
    {
        treeActive = true;
        Camera cam = clicker.ViewCamera;
        if (cam == null || !TryPickSpot(cam, out Vector3 ground)) { treeActive = false; spawnTimer = PixelMinigameLimits.RetrySeconds; yield break; }
        Report(MinigameEvent.Spawned);
        PixelHints.Announce("An evil tree sprouted!", "An evil tree sprouted! Chop it down before it grows too strong. It flails, so aim carefully.");
        PixelAudio.Play("wood_chop");

        float H = clicker.PixelBaseSize * heightInCubes;

        // ---- Build it (unit tree 1 tall, scaled by growth).
        GameObject root = new GameObject("Evil Tree");
        Track(root);
        Material bark = clicker.CreateVisualMaterial(barkColor, false);
        Material leaf = clicker.CreateVisualMaterial(leafColor, false);
        Material glow = Flat(eyeColor);
        Material dark = Flat(new Color(0.05f, 0.02f, 0.05f, 1f));

        Transform body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);
        Part(body, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 0.32f, 0f), new Vector3(0.26f, 0.32f, 0.26f), bark, true);
        Part(body, PrimitiveType.Sphere, "Canopy", new Vector3(0f, 0.78f, 0f), new Vector3(0.62f, 0.5f, 0.62f), leaf, true);
        Part(body, PrimitiveType.Sphere, "Canopy Left", new Vector3(-0.26f, 0.66f, 0f), new Vector3(0.38f, 0.32f, 0.38f), leaf, false);
        Part(body, PrimitiveType.Sphere, "Canopy Right", new Vector3(0.27f, 0.7f, 0.04f), new Vector3(0.36f, 0.3f, 0.36f), leaf, false);
        // Face on the trunk (towards the camera side, set each frame by turning the body).
        Transform eyeL = Part(body, PrimitiveType.Sphere, "Eye L", new Vector3(-0.07f, 0.46f, -0.125f), new Vector3(0.07f, 0.045f, 0.03f), glow, false);
        Transform eyeR = Part(body, PrimitiveType.Sphere, "Eye R", new Vector3(0.07f, 0.46f, -0.125f), new Vector3(0.07f, 0.045f, 0.03f), glow, false);
        Part(body, PrimitiveType.Cube, "Mouth", new Vector3(0f, 0.3f, -0.128f), new Vector3(0.13f, 0.035f, 0.02f), dark, false);
        Part(body, PrimitiveType.Cube, "Fang L", new Vector3(-0.04f, 0.285f, -0.13f), new Vector3(0.016f, 0.03f, 0.02f), glow, false);
        Part(body, PrimitiveType.Cube, "Fang R", new Vector3(0.04f, 0.285f, -0.13f), new Vector3(0.016f, 0.03f, 0.02f), glow, false);

        // Arms: branches that whip about from pivots on the trunk.
        List<Arm> arms = new List<Arm>();
        for (int i = 0; i < 4; i++)
        {
            Transform pivot = new GameObject("Arm " + i).transform;
            pivot.SetParent(body, false);
            float side = i % 2 == 0 ? -1f : 1f;
            pivot.localPosition = new Vector3(0f, 0.42f + 0.07f * (i / 2), 0f);
            pivot.localRotation = Quaternion.Euler(0f, i < 2 ? 0f : 90f, 0f);
            Part(pivot, PrimitiveType.Cylinder, "Branch", new Vector3(side * 0.22f, 0f, 0f), new Vector3(0.035f, 0.22f, 0.035f), bark, false).localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(pivot, PrimitiveType.Sphere, "Twig Leaves", new Vector3(side * 0.45f, 0f, 0f), new Vector3(0.14f, 0.1f, 0.14f), leaf, false);
            arms.Add(new Arm { pivot = pivot, speed = Random.Range(3f, 6f), phase = Random.value * 6.28f, reach = side });
        }

        // Health bar: a flat bar above the tree that always faces the camera.
        GameObject barRoot = new GameObject("Evil Tree Health");
        Track(barRoot);
        Transform barBack = Part(barRoot.transform, PrimitiveType.Quad, "Back", Vector3.zero, new Vector3(1f, 0.1f, 1f), Flat(new Color(0f, 0f, 0f, 0.75f)), false);
        Transform barFill = Part(barRoot.transform, PrimitiveType.Quad, "Fill", Vector3.zero, new Vector3(1f, 0.08f, 1f), Flat(Color.green), false);
        Material fillMat = barFill.GetComponent<Renderer>().sharedMaterial;
        Collider[] colliders = root.GetComponentsInChildren<Collider>();

        // ---- Live.
        float age = 0f, hp = health, grownFor = 0f, punch = 0f, seed = Random.value * 50f;
        Vector3 offset = Vector3.zero, offsetTarget = Vector3.zero, offsetVel = Vector3.zero;
        float nextJerk = 0f;
        bool felled = false;
        while (true)
        {
            float dt = Time.deltaTime;
            age += dt;
            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / growSeconds));
            float size = Mathf.Lerp(startFraction, 1f, grow);
            if (grow >= 1f) grownFor += dt;
            float flail = flailStrength * (0.35f + 0.65f * grow);

            // Jerk to a new spot every so often, gliding there fast.
            nextJerk -= dt;
            if (nextJerk <= 0f)
            {
                nextJerk = jerkSeconds * Random.Range(0.5f, 1.3f);
                Vector2 r = Random.insideUnitCircle * H * 0.22f * flail * size;
                offsetTarget = new Vector3(r.x, 0f, r.y);
            }
            offset = Vector3.SmoothDamp(offset, offsetTarget, ref offsetVel, 0.12f);
            root.transform.position = ground + offset;
            root.transform.localScale = Vector3.one * H * size;

            // Lean and thrash.
            float t = Time.time + seed;
            float leanX = (Mathf.PerlinNoise(t * 2.3f, 0.1f) - 0.5f) * 2f * 22f * flail;
            float leanZ = (Mathf.PerlinNoise(0.7f, t * 2.1f) - 0.5f) * 2f * 22f * flail;
            Vector3 toCam = cam.transform.position - root.transform.position; toCam.y = 0f;
            Quaternion face = toCam.sqrMagnitude > 0.001f ? Quaternion.LookRotation(-toCam.normalized) : Quaternion.identity;   // the face looks at the player
            body.rotation = Quaternion.Euler(leanX, 0f, leanZ) * face;
            foreach (Arm a in arms) a.pivot.localRotation = Quaternion.Euler(0f, a.pivot.localEulerAngles.y, Mathf.Sin(t * a.speed + a.phase) * 75f * flail);
            float blink = 0.7f + 0.3f * Mathf.Sin(t * 9f);
            eyeL.localScale = new Vector3(0.07f, 0.045f * blink, 0.03f); eyeR.localScale = eyeL.localScale;

            // Health bar.
            float frac = Mathf.Clamp01(hp / health);
            Vector3 top = root.transform.position + Vector3.up * H * size * 1.12f;
            barRoot.transform.position = top;
            barRoot.transform.rotation = cam.transform.rotation;
            float barW = Mathf.Max(0.8f, H * 0.55f);
            barBack.localScale = new Vector3(barW, barW * 0.1f, 1f);
            barFill.localScale = new Vector3(barW * 0.96f * frac, barW * 0.075f, 1f);
            barFill.localPosition = new Vector3(-barW * 0.48f * (1f - frac), 0f, -0.001f);
            if (fillMat != null && fillMat.HasProperty("_Color")) fillMat.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.4f, 0.9f, 0.3f), frac);

            // Clicks (manual only).
            punch = Mathf.Max(0f, punch - dt / 0.15f);
            body.localScale = Vector3.one * (1f + 0.12f * punch);
            if (Time.timeScale > 0f && LeftPressed() && !PointerOverUI() && Time.time - lastHitTime > 0.04f)
            {
                Ray ray = cam.ScreenPointToRay(PointerPosition());
                foreach (Collider c in colliders)
                    if (c != null && c.Raycast(ray, out RaycastHit info, 1000f))
                    {
                        lastHitTime = Time.time;
                        punch = 1f;
                        hp -= damagePerClick;
                        PixelAudio.Play("wood_chop");
                        break;
                    }
            }

            if (hp <= 0f) { felled = true; break; }
            if (grownFor >= stayAfterGrownSeconds) break;
            yield return null;
        }

        // ---- The end: it topples and pays, or it sinks away.
        Vector3 fallAxis = Vector3.Cross(Vector3.up, cam.transform.forward).normalized;
        float endT = 0f;
        float endLen = felled ? 1.1f : 0.9f;
        Vector3 pos0 = root.transform.position;
        Vector3 scale0 = root.transform.localScale;
        while (endT < endLen)
        {
            endT += Time.deltaTime;
            float k = Mathf.Clamp01(endT / endLen);
            if (felled)
            {
                body.rotation = Quaternion.AngleAxis(Mathf.Lerp(0f, 88f, k * k), fallAxis);
                root.transform.localScale = scale0 * Mathf.Lerp(1f, 0.85f, k);
            }
            else root.transform.position = pos0 + Vector3.down * H * 0.9f * k;
            barRoot.SetActive(false);
            yield return null;
        }

        if (felled)
        {
            int wood = Random.Range(Mathf.Min(minWood, maxWood), Mathf.Max(minWood, maxWood) + 1);
            magicWood += wood;
            Report(MinigameEvent.Clicked);   // a success: Woodcutting XP (SkillXpTarget)
            PixelAudio.Play("purchase");
            PixelHints.Announce("Evil tree felled: +" + wood + " Magic Wood", "You felled the evil tree and got " + wood + " Magic Wood.");
            StartCoroutine(PopupRoutine(pos0 + Vector3.up * H * 0.5f, cam, "+" + wood + " Magic Wood"));
        }
        else PixelHints.Announce("The evil tree sank back into the ground.");

        Destroy(root);
        Destroy(barRoot);
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        treeActive = false;
    }

    private IEnumerator PopupRoutine(Vector3 start, Camera cam, string message)
    {
        PixelStatusLines.Set("eviltree", message, new Color(0.8f, 0.5f, 1f, 1f), 2f);   // shown in the top status box
        yield break;
    }
}
