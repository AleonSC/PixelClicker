using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static PixelInput;

/// <summary>
/// Sorting Race minigame for Pixel Clicker.
///
/// Every so often (once bought in the shop's Minigames tab) some of the old pixels on the floor freeze, slide into a tidy grid in
/// a jumbled order, and a timer starts. Click one pixel and then another to swap them. Get every pixel type into ONE unbroken run
/// (all the reds together, all the blues together... in any order) before the time is up and you are paid a multiple of their worth,
/// with a bonus for finishing early. If the time runs out the pixels simply drop and age normally again.
///
/// Like Snake it takes over the old pixels, so the two never run at once (see <see cref="PixelMinigame.Takeover"/>).
/// Add this to any GameObject (PixelShop adds it automatically if it is missing).
/// </summary>
public class PixelSortMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, old pixels, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when the Sorting Race is bought.)")]
    [SerializeField] private bool running = false;

    [Header("When it starts")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame is switched on before the first race can start.")]
    [SerializeField] private float firstRoundDelay = 90f;

    [Min(1f)]
    [Tooltip("Shortest wait between races (seconds).")]
    [SerializeField] private float minInterval = 150f;

    [Min(1f)]
    [Tooltip("Longest wait between races (seconds).")]
    [SerializeField] private float maxInterval = 300f;

    [Min(4)]
    [Tooltip("A race only starts when at least this many old pixels (of at least two different types) are lying around.")]
    [SerializeField] private int minPixels = 8;

    [Min(4)]
    [Tooltip("Most pixels in one race (the rest keep ageing normally). Types are mixed evenly.")]
    [SerializeField] private int maxPixels = 16;

    [Header("Ultra Pixels")]
    [Tooltip("Chance that each pixel in a won race (more likely the faster you finish) gives one Ultra pixel of its type. 0 = 0.06.")]
    [SerializeField] private float ultraChance = 0.06f;

    [Tooltip("Most Ultra pixels one round can give. 0 = 3.")]
    [SerializeField] private int ultraMaxPerRound = 3;

    [Tooltip("Turn Ultra pixel rewards from this minigame off.")]
    [SerializeField] private bool disableUltra = false;

    [Header("Rules")]
    [Min(5f)]
    [Tooltip("Seconds of the race: this plus 'Seconds Per Pixel' for every pixel in it.")]
    [SerializeField] private float baseSeconds = 20f;

    [Min(0f)]
    [Tooltip("Extra seconds for every pixel in the race.")]
    [SerializeField] private float secondsPerPixel = 2.5f;

    [Min(0f)]
    [Tooltip("Payout when sorted: this many times the worth of the pixels.")]
    [SerializeField] private float rewardMultiplier = 3f;

    [Min(0f)]
    [Tooltip("Extra multiplier on top for finishing instantly; it shrinks to 0 as the time runs out.")]
    [SerializeField] private float speedBonusMultiplier = 2f;

    [Header("Layout (sizes are in old-pixel widths)")]
    [Min(1)]
    [Tooltip("Most pixels in one row of the grid.")]
    [SerializeField] private int maxColumns = 8;

    [Min(1f)]
    [Tooltip("Distance between neighbouring pixels in the grid.")]
    [SerializeField] private float spacing = 1.6f;

    [Range(0f, 1f)]
    [Tooltip("Where the grid sits on the screen: how far up (0 = the bottom, 1 = the top), the left-right middle is always the middle.")]
    [SerializeField] private float gridHeightOnScreen = 0.4f;

    [Min(0.1f)]
    [Tooltip("Seconds the pixels take to slide into the grid at the start, and to swap places.")]
    [SerializeField] private float slideSeconds = 0.8f;

    [Min(0.05f)]
    [Tooltip("Seconds a swap takes.")]
    [SerializeField] private float swapSeconds = 0.25f;

    [Min(1.05f)]
    [Tooltip("The selected pixel grows by this much and hops a little.")]
    [SerializeField] private float selectedScale = 1.3f;

    [Tooltip("The old pixels that are not in the race shrink away while it runs and grow back afterwards.")]
    [SerializeField] private bool shrinkOthers = true;

    [Min(0.05f)]
    [Tooltip("Seconds the other old pixels take to shrink away / grow back.")]
    [SerializeField] private float bystanderSeconds = 0.6f;

    [Range(0.05f, 1f)]
    [Tooltip("During a race the cube shrinks to this fraction of its size so it doesn't cover the grid, then grows back afterwards (1 = don't shrink it).")]
    [SerializeField] private float cubeShrinkScale = 0.2f;

    [Range(0.2f, 1f)]
    [Tooltip("Safeguard: the grid is never wider than this fraction of the screen width (and about 0.6 times that tall); it packs the pixels closer instead.")]
    [SerializeField] private float maxScreenFraction = 0.6f;

    [Min(10f)]
    [Tooltip("A pixel can be clicked within this many pixels of its centre (at 1080p screen height).")]
    [SerializeField] private float clickRadiusPixels = 70f;

    [Header("Text")]
    [Tooltip("Status line. {0} = groups now, {1} = groups needed, {2} = seconds left.")]
    [SerializeField] private string statusFormat = "SORT   groups {0} -> {1}   {2}s";

    [Tooltip("Status line while the pixels slide into place.")]
    [SerializeField] private string readyText = "Get ready...";

    [Tooltip("Message when sorted. {0} = the payout.")]
    [SerializeField] private string winFormat = "SORTED!  +{0}";

    [Tooltip("Message when time runs out.")]
    [SerializeField] private string timeoutText = "Out of time!";

    [Tooltip("Size of the status line (canvas units).")]
    [SerializeField] private float statusFontSize = 40f;

    [Tooltip("Size of the end message (3D text: about 10 per world unit).")]
    [SerializeField] private float messageTextSize = 3f;

    [Min(0.2f)]
    [Tooltip("How long the end message stays (seconds).")]
    [SerializeField] private float messageSeconds = 2.2f;

    // ------------------------------------------------------------------

    private class Piece
    {
        public Rigidbody body;
        public Transform tf;
        public int tier;
        public double amount;
        public OldPixelDespawn despawn;
        public Collider[] colliders;
        public Behaviour[] paused;
        public Vector3 baseScale;
        public Vector3 from, to;
        public float moveT = 1f, moveSeconds = 0.25f;
    }

    private readonly List<Piece> pieces = new List<Piece>();
    private Piece[] order;           // order[slot] = the piece standing in that grid slot
    private Vector3[] slots;
    private bool activeRound, arranging;
    private float spawnTimer, timeLeft, groundY, unit;
    private int selected = -1, distinctTypes;
    private Camera cam;
    private GameObject uiRoot;
    private TMP_Text statusLabel;
    private bool setBlock;

    public override string Id => "sort";
    public override string DisplayName => "Sorting Race";
    public override bool Running => running;
    public override bool Busy => activeRound;
    public override bool Takeover => true;

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstRoundDelay;
    }

    public override void Deactivate()
    {
        running = false;
        if (activeRound) EndRound(false, null);
    }

    protected override void Awake()
    {
        base.Awake();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelSortMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        if (startRunning) running = true;
        spawnTimer = firstRoundDelay;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (activeRound) EndRound(false, null);
        activeRound = false;
        bystanderFactor = 1f;
        UpdateBystanders(); // put the other old pixels back at once
    }

    protected override void OnDespawned()
    {
        if (activeRound) EndRound(false, null);
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }

    [ContextMenu("Start A Sorting Race Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !activeRound) TryStart(true);
    }

    // The old pixels that are not part of the race shrink away while it runs and grow back afterwards.
    private class Bystander { public Rigidbody body; public Transform tf; public Vector3 baseScale; public Collider[] colliders; public Behaviour[] paused; }
    private readonly List<Bystander> bystanders = new List<Bystander>();
    private float bystanderFactor = 1f;

    private void ShrinkBystanders(HashSet<Rigidbody> inRace)
    {
        bystanders.Clear();
        var all = clicker.OldPixels;
        for (int i = 0; i < all.Count; i++)
        {
            Rigidbody rb = all[i];
            if (rb == null || inRace.Contains(rb) || clicker.IsFlyingPixel(rb)) continue;
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null && (d.IsDespawning || d.Held)) continue;
            bystanders.Add(new Bystander
            {
                body = rb, tf = rb.transform, baseScale = rb.transform.localScale,
                colliders = rb.GetComponents<Collider>(),
                paused = new Behaviour[] { rb.GetComponent<OldPixelGravityWell>(), rb.GetComponent<OldPixelFloat>() },
            });
        }
        foreach (Bystander b in bystanders)
        {
            SetVelocities(b.body, Vector3.zero);
            b.body.isKinematic = true;
            foreach (Collider c in b.colliders) if (c != null) c.enabled = false;
            foreach (Behaviour x in b.paused) if (x != null) x.enabled = false;
        }
    }

    private void UpdateBystanders()
    {
        if (bystanders.Count == 0) return;
        float target = activeRound ? 0f : 1f;
        bystanderFactor = Mathf.MoveTowards(bystanderFactor, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, bystanderSeconds));
        bool done = !activeRound && bystanderFactor >= 1f;
        for (int i = bystanders.Count - 1; i >= 0; i--)
        {
            Bystander b = bystanders[i];
            if (b.body == null || b.tf == null) { bystanders.RemoveAt(i); continue; }
            b.tf.localScale = b.baseScale * Mathf.Max(0.001f, bystanderFactor);
            if (done)
            {
                foreach (Collider c in b.colliders) if (c != null) c.enabled = true;
                foreach (Behaviour x in b.paused) if (x != null) x.enabled = true;
                b.body.isKinematic = false;
            }
        }
        if (done) bystanders.Clear();
    }

    private void Update()
    {
        UpdateBystanders();
        if (activeRound) { Tick(Time.deltaTime); return; }
        if (!running) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        if (!PixelMinigameLimits.AllowAnother(this)) { spawnTimer = PixelMinigameLimits.RetrySeconds; return; }
        spawnTimer = TryStart(false) ? 0f : 12f; // not enough mixed pixels lying around yet
    }

    // ------------------------------------------------------------------
    // Starting
    // ------------------------------------------------------------------

    private bool TryStart(bool dev)
    {
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return false;

        // The old pixels lying around, grouped by type.
        Dictionary<int, List<Piece>> byType = new Dictionary<int, List<Piece>>();
        var all = clicker.OldPixels;
        for (int i = 0; i < all.Count; i++)
        {
            Rigidbody rb = all[i];
            if (rb == null || rb.isKinematic || clicker.IsFlyingPixel(rb)) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null) continue;
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null && (d.IsDespawning || d.Held)) continue;

            if (!byType.TryGetValue(info.tierIndex, out List<Piece> list)) byType[info.tierIndex] = list = new List<Piece>();
            list.Add(new Piece
            {
                body = rb, tf = rb.transform, tier = info.tierIndex, amount = info.amount, despawn = d,
                colliders = rb.GetComponents<Collider>(),
                paused = new Behaviour[] { rb.GetComponent<OldPixelGravityWell>(), rb.GetComponent<OldPixelFloat>() },
            });
        }
        if (byType.Count < 2) return false; // everything is the same type: nothing to sort

        // Take the types in turn so the groups are about the same size.
        List<Piece> chosen = new List<Piece>();
        List<List<Piece>> lists = new List<List<Piece>>(byType.Values);
        int round = 0;
        while (chosen.Count < maxPixels)
        {
            bool took = false;
            foreach (List<Piece> l in lists)
            {
                if (round < l.Count && chosen.Count < maxPixels) { chosen.Add(l[round]); took = true; }
            }
            if (!took) break;
            round++;
        }
        if (chosen.Count < (dev ? 3 : minPixels)) return false;

        // Which types ended up in the race, and how big a pixel is.
        HashSet<int> types = new HashSet<int>();
        float sum = 0f;
        groundY = float.MaxValue;
        Vector3 centroid = Vector3.zero;
        foreach (Piece p in chosen)
        {
            types.Add(p.tier);
            Renderer r = p.body.GetComponentInChildren<Renderer>();
            sum += r != null ? Mathf.Max(r.bounds.size.x, r.bounds.size.z) : 0.5f;
            groundY = Mathf.Min(groundY, p.body.position.y);
            centroid += p.body.position;
            p.baseScale = p.tf.localScale;
        }
        if (types.Count < 2) return false;
        distinctTypes = types.Count;
        unit = Mathf.Max(0.1f, sum / chosen.Count);
        centroid /= chosen.Count;

        pieces.Clear();
        pieces.AddRange(chosen);

        // Freeze them: nothing despawns, nothing moves by itself.
        OldPixelDespawn.HoldAll = true;
        foreach (Piece p in pieces)
        {
            SetVelocities(p.body, Vector3.zero);
            p.body.isKinematic = true;
            foreach (Behaviour b in p.paused) if (b != null) b.enabled = false;
        }

        HashSet<Rigidbody> inRace = new HashSet<Rigidbody>();
        foreach (Piece p in pieces) inRace.Add(p.body);
        if (shrinkOthers) { ShrinkBystanders(inRace); bystanderFactor = 1f; }

        BuildSlots(centroid);

        // Shuffle (and make sure it is not already sorted), then send every pixel to its slot.
        order = pieces.ToArray();
        for (int attempt = 0; attempt < 12; attempt++)
        {
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Piece t = order[i]; order[i] = order[j]; order[j] = t;
            }
            if (!IsSorted()) break;
        }
        for (int slot = 0; slot < order.Length; slot++)
        {
            Piece p = order[slot];
            p.from = p.tf.position;
            p.to = slots[slot];
            p.moveT = 0f;
            p.moveSeconds = slideSeconds;
            p.tf.rotation = Quaternion.identity;
        }

        if (cubeShrinkScale < 0.999f) clicker.SetCubeShrink(cubeShrinkScale, 0.5f); // the cube steps out of the way
        selected = -1;
        arranging = true;
        timeLeft = baseSeconds + secondsPerPixel * pieces.Count;
        activeRound = true;
        TakeoverActive = true;
        if (!PixelClicker.ExternalClickBlock) { PixelClicker.ExternalClickBlock = true; setBlock = true; }
        BuildUi();
        ultraRoundCount = 0;
        Report(MinigameEvent.Spawned);
        PixelAudio.Play("sort_start");
        return true;
    }

    /// <summary>Grid slots (row by row, far row first) centred on the middle of the screen's floor, shrunk to fit the view.</summary>
    private void BuildSlots(Vector3 fallbackCentre)
    {
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;

        // The floor point at the chosen height in the middle of the screen.
        Vector3 centre = fallbackCentre;
        Plane floor = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, gridHeightOnScreen, 0f));
        if (floor.Raycast(ray, out float enter)) centre = ray.GetPoint(enter);
        centre.y = groundY;

        int count = pieces.Count;
        int cols = Mathf.Min(count, Mathf.Max(1, maxColumns));
        float step = spacing * unit;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int rows = Mathf.CeilToInt(count / (float)cols);
            slots = new Vector3[count];
            bool fits = true;
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < count; i++)
            {
                int r = i / cols, c = i % cols;
                slots[i] = centre + right * ((c - (cols - 1) * 0.5f) * step) + forward * (((rows - 1) * 0.5f - r) * step);
                Vector3 v = cam.WorldToViewportPoint(slots[i]);
                if (v.z <= 0f || v.x < 0.06f || v.x > 0.94f || v.y < 0.2f || v.y > 0.85f) fits = false;
                lo = Vector2.Min(lo, new Vector2(v.x, v.y));
                hi = Vector2.Max(hi, new Vector2(v.x, v.y));
            }
            // Never take over more than a fraction of the screen.
            if (hi.x - lo.x > maxScreenFraction || hi.y - lo.y > maxScreenFraction * 0.6f) fits = false;
            if (fits) return;
            step *= 0.88f; // too wide for the screen: pack them closer
        }
    }

    // ------------------------------------------------------------------
    // The race
    // ------------------------------------------------------------------

    private void Tick(float dt)
    {
        // Slide pieces toward their slots.
        bool moving = false;
        foreach (Piece p in pieces)
        {
            if (p.tf == null) continue;
            if (p.moveT < 1f)
            {
                p.moveT = Mathf.Min(1f, p.moveT + dt / Mathf.Max(0.05f, p.moveSeconds));
                moving = true;
                p.tf.position = Vector3.Lerp(p.from, p.to, Mathf.SmoothStep(0f, 1f, p.moveT));
            }
        }

        if (arranging)
        {
            if (!moving) { arranging = false; PixelAudio.Play("sort_ready"); }
            UpdateLabel(true);
            return;
        }

        timeLeft -= dt;
        if (timeLeft <= 0f) { EndRound(false, timeoutText); return; }

        UpdateSelectedLook();
        if (Time.timeScale > 0f && LeftPressed() && !PointerOverUI()) HandleClick();

        UpdateLabel(false);
        if (IsSorted()) EndRound(true, null);
    }

    private void HandleClick()
    {
        float radius = clickRadiusPixels * Screen.height / 1080f;
        Vector2 pointer = PointerPosition();
        int hit = -1;
        float best = float.MaxValue;
        for (int slot = 0; slot < order.Length; slot++)
        {
            Piece p = order[slot];
            if (p == null || p.tf == null || p.moveT < 1f) continue; // can't pick one that is still sliding
            Vector3 sp = cam.WorldToScreenPoint(p.tf.position);
            float d = Vector2.Distance(pointer, sp);
            if (sp.z > 0f && d <= radius && d < best) { best = d; hit = slot; }
        }

        if (hit < 0) { selected = -1; ResetSelectedLook(); return; }
        if (selected < 0) { selected = hit; PixelAudio.Play("sort_pick"); return; }
        if (selected == hit) { selected = -1; ResetSelectedLook(); return; }

        Swap(selected, hit);
        selected = -1;
        ResetSelectedLook();
    }

    private void Swap(int a, int b)
    {
        Piece pa = order[a], pb = order[b];
        order[a] = pb; order[b] = pa;
        pa.from = pa.tf.position; pa.to = slots[b]; pa.moveT = 0f; pa.moveSeconds = swapSeconds;
        pb.from = pb.tf.position; pb.to = slots[a]; pb.moveT = 0f; pb.moveSeconds = swapSeconds;
        PixelAudio.Play("sort_swap");
    }

    private void UpdateSelectedLook()
    {
        for (int slot = 0; slot < order.Length; slot++)
        {
            Piece p = order[slot];
            if (p == null || p.tf == null) continue;
            bool on = slot == selected;
            p.tf.localScale = Vector3.Lerp(p.tf.localScale, p.baseScale * (on ? selectedScale : 1f), 0.25f);
            if (on && p.moveT >= 1f)
                p.tf.position = slots[slot] + Vector3.up * (unit * (0.25f + 0.08f * Mathf.Sin(Time.time * 8f)));
            else if (p.moveT >= 1f)
                p.tf.position = Vector3.Lerp(p.tf.position, slots[slot], 0.3f);
        }
    }

    private void ResetSelectedLook()
    {
        // The scale and height ease back on their own in UpdateSelectedLook.
    }

    /// <summary>Number of unbroken runs of one type along the grid (reading order).</summary>
    private int Runs()
    {
        int runs = 0, last = int.MinValue;
        foreach (Piece p in order)
        {
            if (p == null) continue;
            if (p.tier != last) { runs++; last = p.tier; }
        }
        return runs;
    }

    private bool IsSorted() => order != null && Runs() == distinctTypes;

    private void UpdateLabel(bool getReady)
    {
        if (statusLabel == null) return;
        PixelUIKit.SetText(statusLabel, getReady ? readyText
            : string.Format(statusFormat, Runs(), distinctTypes, Mathf.CeilToInt(Mathf.Max(0f, timeLeft))));
    }

    // ------------------------------------------------------------------
    // Ending
    // ------------------------------------------------------------------

    private void EndRound(bool win, string message)
    {
        PixelStats.Count(win ? "sort.win" : "sort.loss");
        if (win) PixelStats.Fastest("sort.fastest", baseSeconds + secondsPerPixel * Mathf.Max(1, pieces.Count) - timeLeft);
        activeRound = false;
        arranging = false;
        TakeoverActive = false;
        clicker.SetCubeShrink(1f, 0.6f); // the cube grows back
        OldPixelDespawn.HoldAll = false;
        if (setBlock) { PixelClicker.ExternalClickBlock = PixelBank.HoseOn; setBlock = false; }

        Vector3 messageAt = slots != null && slots.Length > 0 ? slots[0] : Vector3.zero;
        double payout = 0d;
        float fraction = Mathf.Clamp01(timeLeft / Mathf.Max(0.01f, baseSeconds + secondsPerPixel * Mathf.Max(1, pieces.Count)));
        float multiplier = rewardMultiplier + speedBonusMultiplier * fraction;

        foreach (Piece p in pieces)
        {
            if (p.body == null) continue;
            if (p.tf != null) p.tf.localScale = p.baseScale;
            foreach (Collider c in p.colliders) if (c != null) c.enabled = true;
            foreach (Behaviour b in p.paused) if (b != null) b.enabled = true;
            p.body.isKinematic = false;
            if (win)
            {
                double pay = p.amount * multiplier;
                payout += pay;
                clicker.AddCurrency(p.tier, pay);
                UltraRoll(clicker, p.tier, ultraChance, 0.06f, ultraMaxPerRound, 3, disableUltra, 1f + fraction, p.body.position);
                if (p.despawn != null) p.despawn.Begin(); // the sorted pixels shrink away
            }
            else
            {
                SetVelocities(p.body, Vector3.zero); // they just drop and age normally again
                if (p.despawn != null) p.despawn.AddLifetime(3f);
            }
        }

        pieces.Clear();
        order = null;
        if (uiRoot != null) Destroy(uiRoot);
        uiRoot = null;
        statusLabel = null;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));

        if (win)
        {
            Report(MinigameEvent.Clicked);
            PixelAudio.Play("sort_win");
            ShowMessage(messageAt, string.Format(winFormat, PixelClicker.FormatNumber(payout)));
        }
        else if (!string.IsNullOrEmpty(message))
        {
            PixelAudio.Play("sort_lose");
            ShowMessage(messageAt, message);
        }
    }

    private void ShowMessage(Vector3 at, string text)
    {
        Camera c = cam != null ? cam : Camera.main;
        if (c != null) StartCoroutine(MessageRoutine(at, c, text));
    }

    private IEnumerator MessageRoutine(Vector3 start, Camera c, string message)
    {
        GameObject go = new GameObject("Sort Message");
        Track(go);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = messageTextSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        if (clicker.UIFont != null) text.font = clicker.UIFont;
        text.rectTransform.sizeDelta = new Vector2(14f, 4f);

        float t = 0f;
        while (t < messageSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / messageSeconds);
            go.transform.position = PixelUIKit.KeepOnScreen(c, text, start + Vector3.up * (1.2f + k * 1.2f));
            go.transform.rotation = c.transform.rotation;
            text.alpha = 1f - k * k;
            yield return null;
        }
        Destroy(go);
    }

    private void BuildUi()
    {
        uiRoot = PixelUIKit.CreateCanvas("Sort UI", 125, new Vector2(1920f, 1080f), true);
        uiRoot.transform.SetParent(transform, false);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        statusLabel = PixelUIKit.CreateText(clicker.UIFont, uiRoot.transform, "Status", "", statusFontSize, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform sr = statusLabel.rectTransform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(900f, statusFontSize * 1.5f);
        sr.anchoredPosition = new Vector2(0f, -(bar + 12f));
    }

    private static void SetVelocities(Rigidbody rb, Vector3 v)
    {
        if (rb == null || rb.isKinematic) return;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
        rb.angularVelocity = Vector3.zero;
    }
}
