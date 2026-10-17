// ---- System notes ----
// PixelBaitBucket.cs
//   Bait Bucket (DeviceKind.BaitBucket 14, a Supplies-category consumable; component added by PixelClicker.Awake; disableBaitBucket):
//   right-click it in the Inventory (PixelConsumables.TryUseItem -> PixelBaitBucket.TryPlace, needs Fishing as the skill in play, one at a
//   time) and a code-built red bucket grows in on the floor at a fixed screen spot (baitBucketScreenSpot, 0 = (0.89, 0.19), bucketScale
//   0 = 1.2 x the main pixel). It stays only while Fishing is the skill in play: switching skills (or Quit > Main menu) shrinks it away and
//   everything inside pops out again as ordinary old pixels. Drag or throw loose old FISH pixels (up to fishCapacity 0 = 20) or WATER
//   pixels (waterCapacity 0 = 40) over it (ScanPixels, like the woodchipper): they are stored with their payout instead of splashing /
//   despawning (a Water pixel dropped in is saved from breaking). Stored fish are live bait: PixelBaitBucket.FishBonus (fishBonusEach 0 =
//   0.15 per fish, waterBonusEach 0 = 0.05 per water) is added to PixelBait.FishMultiplier, so Fish spawns more. Left-click the bucket
//   (PixelPointerClaim / ClaimsPointer keep the cube and Pixel Grabbing out of it) takes the last stored pixel out into the mouse's hand. Not
//   saved: a bucket is gone after a load (its contents are lost). Its bail is a carrying handle (shared PixelCarryHandles): drag it to move
//   the bucket anywhere on the floor (scroll / Q / E turn it); the bucket's own click steps aside while the bail has the pointer.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>The Bait Bucket consumable: a bucket on the floor that stores Fish and Water pixels and makes fish spawn more while Fishing is the skill in play.</summary>
public class PixelBaitBucket : MonoBehaviour
{
    [Header("Bait Bucket")]
    [Tooltip("Turn the Bait Bucket off (it cannot be placed).")]
    [SerializeField] private bool disableBaitBucket = false;

    [Tooltip("Where the bucket stands, as a fraction of the screen (x from the left, y up from the bottom). (0, 0) = the default (0.89, 0.19).")]
    [SerializeField] private Vector2 baitBucketScreenSpot = Vector2.zero;

    [Min(0f)]
    [Tooltip("Size of the bucket as a multiple of the main pixel's width. 0 = 1.2.")]
    [SerializeField] private float bucketScale = 0f;

    [Min(0)]
    [Tooltip("Fish pixels the bucket holds. 0 = 20.")]
    [SerializeField] private int fishCapacity = 0;

    [Min(0)]
    [Tooltip("Water pixels the bucket holds. 0 = 40.")]
    [SerializeField] private int waterCapacity = 0;

    [Min(0f)]
    [Tooltip("Each stored Fish pixel adds this to the factor on Fish's spawn weight (0.15 = +15%). 0 = 0.15.")]
    [SerializeField] private float fishBonusEach = 0f;

    [Min(0f)]
    [Tooltip("Each stored Water pixel adds this to the factor on Fish's spawn weight. 0 = 0.05.")]
    [SerializeField] private float waterBonusEach = 0f;

    private struct Entry { public int tier; public double amount; public bool fish; }

    private static PixelBaitBucket instance;


    /// <summary>True when a bucket stands on the floor.</summary>
    public static bool Placed => instance != null && instance.root != null;

    /// <summary>Added to the factor on Fish's spawn weight by what is stored (0 with no bucket).</summary>
    public static float FishBonus => instance != null && instance.root != null ? instance.Bonus() : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private PixelClicker clicker;
    private Camera cam;
    private GameObject root;
    private PixelCarryHandles carry;   // the bail works as a carrying handle
    private Transform waterDisc;
    private TextMeshPro label;
    private float appear, scale = 1f, scanTimer, floorY;
    private bool wanted;
    private readonly List<Entry> stored = new List<Entry>();
    private readonly List<Transform> models = new List<Transform>();
    private readonly List<float> modelPhase = new List<float>();
    private int fishCount, waterCount;

    private int FishCap => fishCapacity > 0 ? fishCapacity : 20;
    private int WaterCap => waterCapacity > 0 ? waterCapacity : 40;
    private float Bonus() => fishCount * (fishBonusEach > 0f ? fishBonusEach : 0.15f) + waterCount * (waterBonusEach > 0f ? waterBonusEach : 0.05f);

    private void Awake()
    {
        instance = this;
        clicker = GetComponent<PixelClicker>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (root != null) Destroy(root);
    }

    /// <summary>Right-click use of the consumable. Returns false (with a message to show) when the bucket cannot be put out.</summary>
    public static bool TryPlace(out string message)
    {
        message = null;
        if (instance == null || instance.disableBaitBucket || instance.clicker == null) { message = "The Bait Bucket is not available"; return false; }
        PixelSkills skills = PixelSkills.Instance;
        if (skills == null || skills.ActiveId != PixelSkills.FishingId) { message = "The Bait Bucket needs Fishing as the skill in play"; return false; }
        if (Placed) { message = "A Bait Bucket is already out"; return false; }
        if (PixelTitleScreen.Showing || PixelCameraIntro.Moving || PixelMinigame.TakeoverActive) { message = "Not now"; return false; }
        instance.cam = instance.clicker.ViewCamera;
        if (instance.cam == null) return false;
        instance.Build();
        message = "Bait Bucket set out. Drop Fish and Water pixels into it - stored fish make more fish show up. It leaves when you stop Fishing.";
        return true;
    }

    // ------------------------------------------------------------------

    private void Update()
    {
        if (root == null || clicker == null) return;
        PixelSkills skills = PixelSkills.Instance;
        wanted = !disableBaitBucket && skills != null && skills.ActiveId == PixelSkills.FishingId && !PixelTitleScreen.Showing && !PixelTitleScreen.Reversing;
        float dt = Time.unscaledDeltaTime;
        bool wasWanted = appear > 0.999f;
        appear = Mathf.MoveTowards(appear, wanted ? 1f : 0f, dt / 0.6f);
        root.transform.localScale = Vector3.one * (scale * Mathf.Max(0.001f, Mathf.SmoothStep(0f, 1f, appear)));
        if (!wanted)
        {
            if (wasWanted || appear < 0.999f && stored.Count > 0) ReleaseAll();
            if (appear <= 0.001f) { Destroy(root); root = null; ClearModels(); }
            return;
        }

        float bob = Time.time;
        for (int i = 0; i < models.Count; i++)
            if (models[i] != null)
            {
                Vector3 p = models[i].localPosition;
                p.y = 0.95f + 0.07f * Mathf.Sin(bob * 2f + modelPhase[i]);
                models[i].localPosition = p;
                models[i].Rotate(0f, 40f * Time.deltaTime, 0f, Space.Self);
            }

        if (cam == null) cam = Camera.main;
        if (Time.timeScale <= 0f) return;
        scanTimer -= dt;
        if (scanTimer <= 0f) { scanTimer = 0.08f; ScanPixels(); }
        HandleMouse();
    }

    private void LateUpdate()
    {
        if (root == null || cam == null || label == null) return;
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
    }

    // ------------------------------------------------------------------
    // Taking pixels in and out
    // ------------------------------------------------------------------

    private bool InZone(Vector3 world)
    {
        if (appear < 0.95f) return false;
        Vector3 local = root.transform.InverseTransformPoint(world) / Mathf.Max(0.0001f, scale);
        return Mathf.Abs(local.x) < 1.05f && Mathf.Abs(local.z) < 1.05f && local.y > -0.4f && local.y < 4.5f;
    }

    private void ScanPixels()
    {
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        if (list == null || list.Count == 0) return;
        List<Rigidbody> snapshot = new List<Rigidbody>(list);
        foreach (Rigidbody body in snapshot)
        {
            if (body == null || body.isKinematic) continue;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && (despawn.Held || despawn.IsDespawning)) continue;   // taken when you let go
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || !clicker.IsValidTierIndex(info.tierIndex) || !InZone(body.position)) continue;
            PixelClicker.PixelType type = clicker.Tiers[info.tierIndex].type;
            bool fish = type == PixelClicker.PixelType.Fish, water = type == PixelClicker.PixelType.Water;
            if (fish && fishCount >= FishCap) continue;
            if (water && waterCount >= WaterCap) continue;
            if (!fish && !water) continue;
            double amount = info.amount;
            int tier = info.tierIndex;
            if (!clicker.ReleaseOldPixel(body, false)) continue;   // kept in the bucket with its payout, not collected now
            stored.Add(new Entry { tier = tier, amount = amount, fish = fish });
            if (fish) { fishCount++; AddModel(tier); } else waterCount++;
            PixelAudio.Play("device_place");
            StartCoroutine(FlyIn(body));
            Refresh();
        }
    }

    private System.Collections.IEnumerator FlyIn(Rigidbody body)
    {
        if (body == null) yield break;
        Transform t = body.transform;
        foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;
        body.isKinematic = true;
        OldPixelDespawn d = body.GetComponent<OldPixelDespawn>();
        if (d != null) d.enabled = false;
        Vector3 start = t.position, startScale = t.localScale;
        Vector3 target = root != null ? root.transform.TransformPoint(new Vector3(0f, 0.9f, 0f)) : start;
        for (float time = 0f; time < 0.3f; time += Time.deltaTime)
        {
            if (t == null) yield break;
            float k = Mathf.Clamp01(time / 0.3f);
            t.position = Vector3.Lerp(start, target, k * k);
            t.localScale = startScale * (1f - k * 0.9f);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    private void HandleMouse()
    {
        if (carry != null && (carry.Dragging || PixelCarryHandles.Hot)) return;   // the bail has the pointer (carrying)
        if (cam == null || stored.Count == 0 && !Over()) return;
        if (PixelInput.PointerOverUI()) return;
        if (!Over()) return;
        PixelPointerClaim.Claim();
        if (stored.Count == 0 || !PixelInput.LeftPressed()) return;
        TakeOut();
    }

    private bool Over()
    {
        if (root == null || cam == null || appear < 0.95f) return false;
        Vector3 center = root.transform.position + Vector3.up * scale * 0.7f;
        Vector3 sp = cam.WorldToScreenPoint(center);
        if (sp.z <= 0f) return false;
        Vector3 edge = cam.WorldToScreenPoint(center + cam.transform.right * scale * 0.85f);
        float radius = Mathf.Max(24f, Mathf.Abs(edge.x - sp.x));
        return Vector2.Distance(PixelInput.PointerPosition(), new Vector2(sp.x, sp.y)) <= radius;
    }

    /// <summary>Would a click at the pointer do something here? Pixel Grabbing steps aside then.</summary>
    public static bool ClaimsPointer() => instance != null && instance.root != null && instance.stored.Count > 0 && instance.Over() && !PixelCarryHandles.Hot;

    private void TakeOut()
    {
        Entry e = stored[stored.Count - 1];
        stored.RemoveAt(stored.Count - 1);
        if (e.fish) { fishCount--; RemoveModel(); } else waterCount--;
        Refresh();
        Vector3 at = root.transform.position + Vector3.up * scale * 1.4f;
        if (!clicker.SpawnStoredPixel(e.tier, e.amount, at, Vector3.zero)) return;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        Rigidbody body = pixels.Count > 0 ? pixels[pixels.Count - 1] : null;
        PixelGrab grab = PixelFind.First<PixelGrab>();
        if (body != null && grab != null) grab.BeginHold(body);
    }

    private void ReleaseAll()
    {
        if (stored.Count == 0) return;
        Vector3 basePos = root != null ? root.transform.position + Vector3.up * scale * 1.2f : clicker.CubeBasePosition;
        foreach (Entry e in stored)
        {
            Vector3 v = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 3.2f), Random.Range(-1.5f, 1.5f));
            clicker.SpawnStoredPixel(e.tier, e.amount, basePos, v);
        }
        stored.Clear();
        fishCount = waterCount = 0;
        ClearModels();
    }

    // ------------------------------------------------------------------
    // Look
    // ------------------------------------------------------------------

    private void AddModel(int tier)
    {
        if (root == null || models.Count >= 8) return;
        GameObject model = clicker.CreateDisplayPixel(tier, root.transform, 0.42f);
        if (model == null) return;
        float a = models.Count * 2.4f;
        float r = 0.35f + 0.1f * (models.Count % 3);
        model.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0.95f, Mathf.Sin(a) * r);
        models.Add(model.transform);
        modelPhase.Add(Random.value * 6.28f);
    }

    private void RemoveModel()
    {
        if (models.Count == 0 || fishCount >= models.Count) return;   // (more fish stored than shown: nothing to hide yet)
        Transform t = models[models.Count - 1];
        if (t != null) Destroy(t.gameObject);
        models.RemoveAt(models.Count - 1);
        modelPhase.RemoveAt(modelPhase.Count - 1);
    }

    private void ClearModels()
    {
        foreach (Transform t in models) if (t != null) Destroy(t.gameObject);
        models.Clear();
        modelPhase.Clear();
    }

    private void Refresh()
    {
        if (label != null) label.text = "BAIT BUCKET\n<size=70%>fish " + fishCount + " / " + FishCap + "   water " + waterCount + " / " + WaterCap + "</size>";
        if (waterDisc != null)
        {
            Vector3 p = waterDisc.localPosition;
            p.y = Mathf.Lerp(0.62f, 0.93f, Mathf.Clamp01(waterCount / (float)WaterCap));
            waterDisc.localPosition = p;
        }
    }

    private Transform Part(string name, PrimitiveType shape, Transform parent, Vector3 pos, Vector3 size, Color color, Vector3 euler = default)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material mat = clicker.CreateVisualMaterial(color, false);
        if (mat != null) r.sharedMaterial = mat; else r.material.color = color;
        return go.transform;
    }

    private void Build()
    {
        // Floor height under the cube: the highest solid non-rigidbody collider below it.
        Vector3 origin = clicker.CubeBasePosition + Vector3.up * 6f;
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(origin, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider == null || h.collider.attachedRigidbody != null) continue;
            if (clicker.PixelTransform != null && h.collider.transform.IsChildOf(clicker.PixelTransform)) continue;
            if (h.point.y > best) best = h.point.y;
        }
        floorY = float.IsNegativeInfinity(best) ? clicker.CubeBasePosition.y - clicker.PixelBaseSize * 0.5f : best;

        scale = clicker.PixelBaseSize * (bucketScale > 0f ? bucketScale : 1.2f);
        Vector2 spot = baitBucketScreenSpot == Vector2.zero ? new Vector2(0.89f, 0.19f) : baitBucketScreenSpot;
        Ray ray = cam.ViewportPointToRay(new Vector3(spot.x, spot.y, 0f));
        Plane plane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        Vector3 at = plane.Raycast(ray, out float d) ? ray.GetPoint(d) : clicker.CubeBasePosition + Vector3.right * 4f;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;

        root = new GameObject("Bait Bucket");
        root.transform.SetPositionAndRotation(at, Quaternion.LookRotation(fwd.normalized, Vector3.up));
        root.transform.localScale = Vector3.one * 0.001f;
        Transform r = root.transform;
        Color red = new Color(0.78f, 0.2f, 0.2f), redLight = new Color(0.95f, 0.4f, 0.38f), dark = new Color(0.16f, 0.16f, 0.2f);
        Part("Body", PrimitiveType.Cylinder, r, new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 0.5f, 1.5f), red);
        Part("Rim", PrimitiveType.Cylinder, r, new Vector3(0f, 1.0f, 0f), new Vector3(1.62f, 0.04f, 1.62f), redLight);
        Part("Inside", PrimitiveType.Cylinder, r, new Vector3(0f, 0.9f, 0f), new Vector3(1.38f, 0.03f, 1.38f), dark);
        waterDisc = Part("Water", PrimitiveType.Cylinder, r, new Vector3(0f, 0.62f, 0f), new Vector3(1.36f, 0.02f, 1.36f), new Color(0.3f, 0.62f, 1f));
        Part("Band", PrimitiveType.Cylinder, r, new Vector3(0f, 0.55f, 0f), new Vector3(1.53f, 0.05f, 1.53f), dark);
        Part("Handle Post L", PrimitiveType.Cube, r, new Vector3(-0.78f, 1.2f, 0f), new Vector3(0.05f, 0.42f, 0.05f), dark);
        Part("Handle Post R", PrimitiveType.Cube, r, new Vector3(0.78f, 1.2f, 0f), new Vector3(0.05f, 0.42f, 0.05f), dark);
        Part("Handle", PrimitiveType.Cube, r, new Vector3(0f, 1.42f, 0f), new Vector3(1.6f, 0.09f, 0.09f), dark);
        // The bail is a carrying handle (shared PixelCarryHandles, as on the Storage Crate and Recycler): drag it to move the bucket, scroll / Q / E turns it.
        carry = root.AddComponent<PixelCarryHandles>();
        carry.Init(() => cam);
        carry.Blocked = () => appear < 0.95f || !wanted;

        GameObject lg = new GameObject("Label");
        lg.transform.SetParent(r, false);
        lg.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        label = lg.AddComponent<TextMeshPro>();
        if (clicker.UIFont != null) label.font = clicker.UIFont;
        label.fontSize = 2.2f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.rectTransform.sizeDelta = new Vector2(8f, 2f);

        stored.Clear();
        models.Clear();
        modelPhase.Clear();
        fishCount = waterCount = 0;
        appear = 0.001f;
        Refresh();
        PixelStats.Count("baitbucket.placed");
    }
}
