using System.Collections.Generic;
using UnityEngine;
using static PixelInput;

/// <summary>
/// Bean (id "bean"): a very rare visitor (a grey tabby cat) who gives you Magic Beans on her FIRST visit (a random present of pixels on later ones). Plant them (right-click the item in the Inventory's Seeds category, click the
/// floor) and a tall stalk grows with a handful of random cubes along it - and one Dragon Cube you don't hold yet. She never comes back, whatever you
/// decide. All the visiting is in <see cref="PixelVisitorMinigame"/>; the plant is <see cref="PixelBeanstalk"/>; the beans are a consumable made by
/// <see cref="PixelConsumables"/> (the device flagged <c>beans</c>).
/// </summary>
public class PixelBeanMinigame : PixelVisitorMinigame
{
    [Header("Bean")]
    [Range(0f, 1f)]
    [Tooltip("Chance that she really shows up each time her visit timer runs out (a failed roll waits for the next timer). Small = rare.")]
    [SerializeField] private float visitChance = 0.04f;

    [TextArea(2, 4)]
    [Tooltip("What she says after you take the beans on her first visit (a second speech box).")]
    [SerializeField] private string farewellText = "Find a nice open spot and plant them. Then stand back, dear. Don't wait too long to pick what grows. Mrrow!";

    [TextArea(2, 4)]
    [Tooltip("What she says after you accept a present on a later visit.")]
    [SerializeField] private string giftFarewellText = "Mrrow! Fresh from the garden. I'll bring more if I find any.";

    [Header("Later visits (presents)")]
    [Min(1)]
    [Tooltip("How many different pixel types the present holds.")]
    [SerializeField] private int giftTypes = 3;

    [Min(1)]
    [Tooltip("Each pixel type in the present is worth at least this many normal clicks of that pixel.")]
    [SerializeField] private int giftClicksMin = 30;

    [Min(1)]
    [Tooltip("...and at most this many.")]
    [SerializeField] private int giftClicksMax = 120;

    private bool restoreChecked;

    public override string Id => "bean";
    public override string DisplayName => "Bean";
    protected override bool IsWare(int item) => false;
    protected override bool IsGift => true;
    protected override bool TracksMeeting => true;
    protected override float VisitChance => visitChance;
    // The first visit needs a Dragon Cube you don't hold (and no beans / stalk about); later visits just bring a present.
    protected override bool CanVisit => clicker != null && consumables != null && (HasMet ||
        (clicker.PickMissingDragonCube() >= 0 && consumables.BeansItem() >= 0 && consumables.ItemOwned(consumables.BeansItem()) < 1
         && PixelBeanstalk.Current == null && PixelStats.Total("bean.pending") < 1d));
    protected override string AcceptLabel => FirstVisitNow ? "Take the beans" : "Thanks, Bean!";
    protected override string RejectLabel => "No thanks";
    protected override string DefaultName => "Bean";
    protected override string DefaultGreeting => FirstVisitNow
        ? "Mrrow, hello there! I'm Bean. I grow things, and I give things away, and today I have a very special handful of beans for you. No, no, you can't pay for them. Care to take them?"
        : "Mrrow! Bean here. I was digging in my garden and look what turned up! A little present, just for you. Go on, take it.";
    protected override string DefaultWaresTitle => "Bean";
    protected override string DefaultArrivedLog => "A grey tabby called Bean stopped by...";
    protected override float StartDelayOffset => 180f;
    protected override Color TitleColor => new Color(0.5f, 0.9f, 0.45f);

    protected override void ModifyWares(List<int> wares) { AsideText = FirstVisitNow ? farewellText : giftFarewellText; }

    protected override void OnGift()
    {
        if (!FirstVisitNow) { GivePixelGift("Bean", giftTypes, giftClicksMin, giftClicksMax); return; }
        int item = consumables != null ? consumables.BeansItem() : -1;
        if (item < 0) return;
        consumables.AddItem(item, 1);
        PixelHints.Announce("Bean gave you Magic Beans. Plant them in the Inventory's Seeds category!");
        PixelAudio.Play("purchase");
    }

    // A game saved while a beanstalk was still growing gets it back.
    private void LateUpdate()
    {
        if (restoreChecked || clicker == null || Time.timeSinceLevelLoad < 4f) return;
        restoreChecked = true;
        double pending = PixelStats.Total("bean.pending");
        if (pending >= 1d && PixelBeanstalk.Current == null) PixelBeanstalk.Plant(clicker, Vector3.zero, Mathf.RoundToInt((float)pending) - 1, true);
    }

    protected override void DrawVisitor(Transform art)
    {
        // A grey tabby cat in a little green apron and a floppy sun hat, holding a pouch of beans.
        Color fur = new Color(0.62f, 0.64f, 0.68f), furDark = new Color(0.36f, 0.38f, 0.43f), cream = new Color(0.9f, 0.9f, 0.92f);
        Color pink = new Color(1f, 0.65f, 0.7f), dark = new Color(0.12f, 0.1f, 0.1f), iris = new Color(0.55f, 0.85f, 0.4f);
        Color apron = new Color(0.32f, 0.72f, 0.36f), apronDark = new Color(0.2f, 0.52f, 0.25f);
        Color hat = new Color(0.9f, 0.78f, 0.4f), hatDark = new Color(0.72f, 0.58f, 0.25f);
        Color pouch = new Color(0.55f, 0.38f, 0.2f), bean = new Color(0.5f, 0.72f, 0.28f), beanDark = new Color(0.36f, 0.55f, 0.2f);

        // Tail curling up on the left, with tabby rings.
        Block(art, "Tail 1", fur, new Vector2(28f, 90f), new Vector2(-112f, 70f), 20f);
        Block(art, "Tail 2", fur, new Vector2(28f, 70f), new Vector2(-132f, 128f), -10f);
        Block(art, "Tail Ring 1", furDark, new Vector2(30f, 10f), new Vector2(-116f, 84f), 20f);
        Block(art, "Tail Ring 2", furDark, new Vector2(30f, 10f), new Vector2(-130f, 120f), -10f);
        Block(art, "Tail Tip", furDark, new Vector2(28f, 34f), new Vector2(-128f, 168f), -25f);
        // Body, belly, apron, feet.
        Block(art, "Body", fur, new Vector2(150f, 160f), new Vector2(0f, 100f));
        Block(art, "Belly", cream, new Vector2(78f, 104f), new Vector2(0f, 96f));
        Block(art, "Apron", apron, new Vector2(110f, 96f), new Vector2(0f, 104f));
        Block(art, "Apron Hem", apronDark, new Vector2(110f, 10f), new Vector2(0f, 60f));
        Block(art, "Apron Pocket", apronDark, new Vector2(54f, 30f), new Vector2(0f, 96f));
        for (int k = 0; k < 4; k++)
        {
            Block(art, "Stripe L" + k, furDark, new Vector2(10f, 30f), new Vector2(-64f, 150f - k * 28f), 8f);
            Block(art, "Stripe R" + k, furDark, new Vector2(10f, 30f), new Vector2(64f, 150f - k * 28f), -8f);
        }
        Block(art, "Foot L", fur, new Vector2(56f, 26f), new Vector2(-38f, 14f));
        Block(art, "Foot R", fur, new Vector2(56f, 26f), new Vector2(38f, 14f));
        Block(art, "Toe L", cream, new Vector2(40f, 10f), new Vector2(-38f, 10f));
        Block(art, "Toe R", cream, new Vector2(40f, 10f), new Vector2(38f, 10f));
        // Head with ears and tabby markings.
        Block(art, "Head", fur, new Vector2(160f, 124f), new Vector2(0f, 236f));
        Block(art, "Ear L", fur, new Vector2(46f, 46f), new Vector2(-56f, 306f), 45f);
        Block(art, "Ear R", fur, new Vector2(46f, 46f), new Vector2(56f, 306f), 45f);
        Block(art, "Ear In L", pink, new Vector2(24f, 24f), new Vector2(-56f, 304f), 45f);
        Block(art, "Ear In R", pink, new Vector2(24f, 24f), new Vector2(56f, 304f), 45f);
        Block(art, "Mark M1", furDark, new Vector2(10f, 32f), new Vector2(-22f, 286f), 10f);
        Block(art, "Mark M2", furDark, new Vector2(10f, 38f), new Vector2(0f, 288f));
        Block(art, "Mark M3", furDark, new Vector2(10f, 32f), new Vector2(22f, 286f), -10f);
        Block(art, "Cheek Stripe L", furDark, new Vector2(34f, 8f), new Vector2(-62f, 236f), -8f);
        Block(art, "Cheek Stripe R", furDark, new Vector2(34f, 8f), new Vector2(62f, 236f), 8f);
        Block(art, "Muzzle", cream, new Vector2(80f, 46f), new Vector2(0f, 212f));
        // Eyes, nose, mouth, whiskers.
        Block(art, "Eye L", iris, new Vector2(34f, 36f), new Vector2(-38f, 248f));
        Block(art, "Eye R", iris, new Vector2(34f, 36f), new Vector2(38f, 248f));
        Block(art, "Pupil L", dark, new Vector2(9f, 30f), new Vector2(-38f, 248f));
        Block(art, "Pupil R", dark, new Vector2(9f, 30f), new Vector2(38f, 248f));
        Block(art, "Glint L", Color.white, new Vector2(8f, 8f), new Vector2(-46f, 258f));
        Block(art, "Glint R", Color.white, new Vector2(8f, 8f), new Vector2(30f, 258f));
        Block(art, "Nose", pink, new Vector2(18f, 14f), new Vector2(0f, 226f));
        Block(art, "Mouth 1", dark, new Vector2(4f, 14f), new Vector2(0f, 212f));
        Block(art, "Mouth 2", dark, new Vector2(24f, 4f), new Vector2(-12f, 204f), -12f);
        Block(art, "Mouth 3", dark, new Vector2(24f, 4f), new Vector2(12f, 204f), 12f);
        for (int w = 0; w < 3; w++)
        {
            Block(art, "Whisker L" + w, dark, new Vector2(54f, 3f), new Vector2(-104f, 218f - w * 10f), 8f - w * 8f);
            Block(art, "Whisker R" + w, dark, new Vector2(54f, 3f), new Vector2(104f, 218f - w * 10f), -(8f - w * 8f));
        }
        // Floppy sun hat (ears poke through the brim) with a little sprout on top.
        Block(art, "Hat Brim", hat, new Vector2(190f, 14f), new Vector2(0f, 322f));
        Block(art, "Hat Top", hat, new Vector2(84f, 36f), new Vector2(0f, 342f));
        Block(art, "Hat Band", apronDark, new Vector2(86f, 10f), new Vector2(0f, 328f));
        Block(art, "Hat Sprout", bean, new Vector2(8f, 28f), new Vector2(0f, 372f));
        Block(art, "Hat Leaf", bean, new Vector2(26f, 12f), new Vector2(14f, 380f), 20f);
        // Paws holding the pouch of beans on the right.
        Block(art, "Pouch", pouch, new Vector2(66f, 62f), new Vector2(112f, 112f));
        Block(art, "Pouch Tie", hatDark, new Vector2(70f, 10f), new Vector2(112f, 140f));
        for (int i = 0; i < 4; i++)
            Block(art, "Bean " + i, i % 2 == 0 ? bean : beanDark, new Vector2(14f, 22f), new Vector2(98f + i * 10f, 154f + (i % 2) * 6f), 20f - i * 14f);
        Block(art, "Paw L", fur, new Vector2(30f, 30f), new Vector2(-40f, 112f));
        Block(art, "Paw R", fur, new Vector2(30f, 30f), new Vector2(84f, 116f));
    }
}

/// <summary>
/// The beanstalk the Magic Beans grow: a tall twisting stalk with leaves and a handful of random pixel cubes hanging along it, one of them a Dragon Cube
/// you don't hold yet. Click a cube to pick it: an ordinary one pops off as a paid old pixel, the Dragon Cube is yours at once (with the find show).
/// Left alone it withers after a while and hands over the Dragon Cube anyway. The pending Dragon Cube is kept in the stat counter bean.pending
/// (tier index + 1), so a saved game grows the stalk back.
/// </summary>
public class PixelBeanstalk : MonoBehaviour
{
    public static PixelBeanstalk Current { get; private set; }

    private static int hotFrame = -10;

    /// <summary>True while the mouse is over a fruit of the stalk: PixelClicker then ignores cube clicks.</summary>
    public static bool Hot => hotFrame >= Time.frameCount - 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Current = null; hotFrame = -10; }

    private const int Segments = 18;
    private const float GrowSeconds = 7f;
    private const float WitherAfterSeconds = 900f;

    private class Fruit
    {
        public Transform stem, holder;
        public GameObject model;
        public int tier;
        public bool dragon, taken;
        public int segment;
        public float scale = 1f;
    }

    private PixelClicker clicker;
    private Camera cam;
    private float unit, height, age, wither;
    private bool witheringAway;
    private readonly Vector3[] points = new Vector3[Segments + 1];
    private readonly Transform[] pieces = new Transform[Segments];
    private readonly List<Transform> leaves = new List<Transform>();
    private readonly List<Fruit> fruits = new List<Fruit>();
    private Material stalkMat, leafMat;
    private int dragonTier;

    /// <summary>Plants a beanstalk at 'point' (the Dragon Cube it grows is chosen here). 'restored' = from a save: it grows straight to full height beside the cube.</summary>
    public static PixelBeanstalk Plant(PixelClicker owner, Vector3 point, int dragonTierIndex, bool restored)
    {
        if (Current != null || owner == null) return Current;
        GameObject go = new GameObject("Beanstalk");
        PixelBeanstalk stalk = go.AddComponent<PixelBeanstalk>();
        stalk.Setup(owner, point, dragonTierIndex, restored);
        return stalk;
    }

    private void Setup(PixelClicker owner, Vector3 point, int dragonTierIndex, bool restored)
    {
        Current = this;
        clicker = owner;
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        unit = clicker.PixelBaseSize;
        dragonTier = dragonTierIndex;
        PixelStats.Clear("bean.pending");
        PixelStats.Count("bean.pending", dragonTier + 1);

        if (restored)
        {
            Vector3 side = cam != null ? cam.transform.right : Vector3.right;
            point = clicker.CubeBasePosition + side * unit * 3f;
            float floorY = clicker.CubeBasePosition.y - unit * 1.5f;
            if (Physics.Raycast(point + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
            point.y = floorY;
            age = GrowSeconds;
        }
        transform.position = point;

        height = unit * 6f;
        if (cam != null)
        {
            // Keep the top of the stalk on screen.
            for (int i = 0; i < 8; i++)
            {
                Vector3 vp = cam.WorldToViewportPoint(point + Vector3.up * (height + unit));
                if (vp.y <= 0.86f) break;
                height *= 0.88f;
            }
            height = Mathf.Max(unit * 3f, height);
        }

        stalkMat = clicker.CreateVisualMaterial(new Color(0.3f, 0.7f, 0.25f, 1f), false);
        leafMat = clicker.CreateVisualMaterial(new Color(0.4f, 0.85f, 0.3f, 1f), false);

        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float angle = t * 7f;
            float radius = unit * 0.35f * Mathf.Sin(t * Mathf.PI * 0.9f + 0.1f);
            points[i] = new Vector3(Mathf.Sin(angle) * radius, t * height, Mathf.Cos(angle) * radius * 0.6f);
        }
        for (int i = 0; i < Segments; i++)
        {
            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seg.name = "Stalk " + i;
            Destroy(seg.GetComponent<Collider>());
            seg.transform.SetParent(transform, false);
            if (stalkMat != null) seg.GetComponent<Renderer>().sharedMaterial = stalkMat;
            seg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pieces[i] = seg.transform;
            if (i % 2 == 1)
            {
                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.name = "Leaf";
                Destroy(leaf.GetComponent<Collider>());
                leaf.transform.SetParent(seg.transform.parent, false);
                if (leafMat != null) leaf.GetComponent<Renderer>().sharedMaterial = leafMat;
                leaf.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                leaves.Add(leaf.transform);
            }
        }
        BuildFruits();
        ApplyGrowth(restored ? 1f : 0f);
    }

    private void BuildFruits()
    {
        // A handful of random cubes (not rare drops / fly-aways) and the Dragon Cube among them.
        List<int> pool = new List<int>();
        for (int i = 0; i < clicker.Tiers.Length; i++)
        {
            PixelClicker.PixelTier t = clicker.Tiers[i];
            if (t.unlocked && !t.rareDrop && !t.flyAway) pool.Add(i);
        }
        if (pool.Count == 0) pool.Add(Mathf.Max(0, clicker.IndexOf(PixelClicker.PixelType.White)));
        int others = Random.Range(4, 7);
        int total = others + 1;
        int dragonSlot = Random.Range(0, total);
        for (int k = 0; k < total; k++)
        {
            Fruit f = new Fruit
            {
                dragon = k == dragonSlot,
                segment = 3 + Mathf.RoundToInt(k * (Segments - 5) / (float)Mathf.Max(1, total - 1)),
            };
            f.tier = f.dragon ? dragonTier : pool[Random.Range(0, pool.Count)];
            f.holder = new GameObject("Fruit").transform;
            f.holder.SetParent(transform, false);
            f.model = clicker.CreateDisplayPixel(f.tier, f.holder, unit * 0.75f);
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            Destroy(stem.GetComponent<Collider>());
            stem.transform.SetParent(transform, false);
            if (stalkMat != null) stem.GetComponent<Renderer>().sharedMaterial = stalkMat;
            f.stem = stem.transform;
            fruits.Add(f);
        }
    }

    private float SegmentGrowth(int i, float grow) { return Mathf.Clamp01(grow * Segments - i); }

    private void ApplyGrowth(float grow)
    {
        float radius = unit * 0.12f;
        for (int i = 0; i < Segments; i++)
        {
            float g = SegmentGrowth(i, grow);
            Vector3 a = points[i], b = Vector3.Lerp(points[i], points[i + 1], g);
            Vector3 d = b - a;
            float len = d.magnitude;
            pieces[i].gameObject.SetActive(g > 0.001f);
            if (g <= 0.001f || len < 1e-4f) continue;
            float taper = Mathf.Lerp(1.3f, 0.6f, i / (float)Segments);
            pieces[i].localPosition = (a + b) * 0.5f;
            pieces[i].localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            pieces[i].localScale = new Vector3(radius * 2f * taper, len * 0.5f + 0.01f, radius * 2f * taper);
        }
        for (int l = 0; l < leaves.Count; l++)
        {
            int seg = 1 + l * 2;
            float g = SegmentGrowth(Mathf.Min(seg, Segments - 1), grow);
            leaves[l].gameObject.SetActive(g > 0.9f);
            if (g <= 0.9f) continue;
            float side = (l % 2 == 0 ? 1f : -1f);
            float s = (g - 0.9f) * 10f;
            leaves[l].localPosition = points[seg] + new Vector3(side * unit * 0.28f, 0f, 0f);
            leaves[l].localRotation = Quaternion.Euler(0f, l * 50f, side * 22f);
            leaves[l].localScale = new Vector3(unit * 0.6f, unit * 0.05f, unit * 0.28f) * s;
        }
        foreach (Fruit f in fruits)
        {
            bool shown = !f.taken && SegmentGrowth(f.segment, grow) >= 1f;
            float s = Mathf.Clamp01((grow * Segments - f.segment - 1f) * 2f) * f.scale;
            f.holder.gameObject.SetActive(shown);
            f.stem.gameObject.SetActive(shown);
            if (!shown) continue;
            float side = (f.segment % 2 == 0 ? 1f : -1f);
            Vector3 anchor = points[f.segment];
            Vector3 hang = anchor + new Vector3(side * unit * 0.95f, -unit * 0.15f, 0f);
            f.holder.localPosition = hang;
            f.holder.localScale = Vector3.one * s;
            Vector3 d = hang - anchor;
            f.stem.localPosition = (anchor + hang) * 0.5f;
            f.stem.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            f.stem.localScale = new Vector3(unit * 0.06f, d.magnitude * 0.5f, unit * 0.06f) * Mathf.Min(1f, s * 2f);
        }
    }

    private void OnDestroy() { if (Current == this) Current = null; }

    private void Update()
    {
        if (clicker == null) return;
        if (Time.timeScale <= 0f) return;
        age += Time.deltaTime;
        float grow = Mathf.Clamp01(age / GrowSeconds);
        if (witheringAway)
        {
            wither += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Clamp01(1f - wither / 1.5f);
            if (wither >= 1.5f) Destroy(gameObject);
            return;
        }
        ApplyGrowth(grow);
        foreach (Fruit f in fruits) if (f.model != null && !f.taken) f.model.transform.localRotation = Quaternion.Euler(15f, Time.time * 40f + f.segment * 30f, 0f);

        if (age >= WitherAfterSeconds) { Finish(true); return; }
        if (grow < 1f || PixelPauseMenu.IsPaused || PointerOverUI()) return;
        if (cam == null) cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;

        Vector2 pointer = PointerPosition();
        Fruit hover = null;
        float best = float.MaxValue;
        foreach (Fruit f in fruits)
        {
            if (f.taken) continue;
            Vector3 screen = cam.WorldToScreenPoint(f.holder.position);
            if (screen.z <= 0f) continue;
            Vector3 edge = cam.WorldToScreenPoint(f.holder.position + cam.transform.right * unit * 0.45f);
            float radius = Mathf.Max(28f, Mathf.Abs(edge.x - screen.x));
            float dist = ((Vector2)screen - pointer).magnitude;
            if (dist <= radius && dist < best) { best = dist; hover = f; }
        }
        if (hover == null) return;
        hotFrame = Time.frameCount;
        if (LeftPressed()) Pick(hover);
    }

    private void Pick(Fruit f)
    {
        f.taken = true;
        Vector3 at = f.holder.position;
        if (f.dragon)
        {
            clicker.AddCurrency(f.tier, 1d);
            PixelDragonFind.Play(f.tier, at);
            PixelStats.Clear("bean.pending");
            PixelStats.Count("bean.dragon");
        }
        Vector2 side = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1.2f);
        double pay = f.dragon ? 0d : clicker.RollOldPixelAmount(f.tier) * 3d;   // an ordinary cube pays three times a normal pixel
        clicker.SpawnStoredPixel(f.tier, pay, at, new Vector3(side.x, Random.Range(2.5f, 4f), side.y));
        PixelAudio.Play("click");
        f.holder.gameObject.SetActive(false);
        f.stem.gameObject.SetActive(false);

        bool dragonLeft = false, any = false;
        foreach (Fruit other in fruits) { if (!other.taken) any = true; if (!other.taken && other.dragon) dragonLeft = true; }
        if (!any) Finish(false);
        else if (!dragonLeft) { /* the Dragon Cube is already yours: the rest can wait */ }
    }

    private void Finish(bool timedOut)
    {
        if (timedOut)
        {
            // Left too long: the stalk withers, but the Dragon Cube is still yours.
            foreach (Fruit f in fruits)
                if (!f.taken && f.dragon) { clicker.AddCurrency(f.tier, 1d); f.taken = true; PixelStats.Clear("bean.pending"); PixelHints.Announce("The beanstalk withered, but its Dragon Cube is yours."); }
        }
        witheringAway = true;
        wither = 0f;
    }
}
