using System.Collections.Generic;
using UnityEngine;
using static PixelInput;

/// <summary>
/// Egg (id "egg"): a very rare visitor. An orange cat who gives you, on his FIRST visit, an egg - a pure white pixel with slightly rounded corners - that
/// you click between 1 and 50 times (rolled when you take it) to hatch it into a pet you don't own yet. Later visits bring a random present of pixels, like a cat bringing its owner a gift.
/// All the visiting (sliding in, speech box, accept / reject) is in <see cref="PixelVisitorMinigame"/>; the egg itself is <see cref="PixelEgg"/>.
/// </summary>
public class PixelEggMinigame : PixelVisitorMinigame
{
    [Header("Egg")]
    [Range(0f, 1f)]
    [Tooltip("Chance that he really shows up each time his visit timer runs out (a failed roll waits for the next timer). Small = rare.")]
    [SerializeField] private float visitChance = 0.05f;

    [Min(1)]
    [Tooltip("Fewest clicks an egg needs to hatch.")]
    [SerializeField] private int clicksMin = 1;

    [Min(1)]
    [Tooltip("Most clicks an egg needs to hatch.")]
    [SerializeField] private int clicksMax = 50;

    [TextArea(2, 4)]
    [Tooltip("What he says after you take the egg on his first visit (a second speech box).")]
    [SerializeField] private string farewellText = "Click it. Keep clicking. Something will hatch, and I won't tell you what. Mrrp. I'll stop by again. Maybe.";

    [TextArea(2, 4)]
    [Tooltip("What he says after you accept a present on a later visit.")]
    [SerializeField] private string giftFarewellText = "Mrrp. You're welcome. Don't tell the other cats.";

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

    public override string Id => "egg";
    public override string DisplayName => "Egg";
    protected override bool IsWare(int item) => false;
    protected override bool IsGift => true;
    protected override bool TracksMeeting => true;
    protected override float VisitChance => visitChance;
    // The first visit needs a pet you don't own (and no egg waiting); later visits just bring a present.
    protected override bool CanVisit => HasMet || (PixelEgg.PetCandidates(clicker).Count > 0 && PixelEgg.Current == null && Total("egg.pending") < 1d);
    protected override string AcceptLabel => FirstVisitNow ? "Take the egg" : "Thanks, Egg!";
    protected override string RejectLabel => "No thanks";
    protected override string DefaultName => "Egg";
    protected override string DefaultGreeting => FirstVisitNow
        ? "Mrrp. Name's Egg. Yes, the cat. I don't do this often. Today I have exactly one thing to give you: a perfect egg. Pure white. Never a scratch. Interested?"
        : "Mrrp. It's me. Egg. I found something. For you. Don't ask where. I left it right here. Be grateful.";
    protected override string DefaultWaresTitle => "Egg";
    protected override string DefaultArrivedLog => "A cat called Egg stopped by...";
    protected override float StartDelayOffset => 120f;
    protected override Color TitleColor => new Color(1f, 0.62f, 0.2f);

    private static double Total(string key) => PixelStats.Total(key);

    protected override void ModifyWares(List<int> wares) { AsideText = FirstVisitNow ? farewellText : giftFarewellText; }

    protected override void OnGift()
    {
        if (!FirstVisitNow) { GivePixelGift("Egg", giftTypes, giftClicksMin, giftClicksMax); return; }
        int need = Random.Range(Mathf.Max(1, clicksMin), Mathf.Max(clicksMin, clicksMax) + 1);
        PixelStats.Best("egg.pending", 1d);
        PixelStats.Clear("egg.need");
        PixelStats.Count("egg.need", need);
        PixelStats.Clear("egg.clicks");
        PixelEgg.Spawn(clicker, need, 0);
        PixelHints.Announce("Egg gave you an egg. Click it until it hatches!");
        PixelAudio.Play("purchase");
    }

    // A game that was saved with an egg still waiting gets it back.
    private void LateUpdate()
    {
        if (restoreChecked || clicker == null || Time.timeSinceLevelLoad < 4f) return;
        restoreChecked = true;
        if (Total("egg.pending") >= 1d && PixelEgg.Current == null)
            PixelEgg.Spawn(clicker, Mathf.Max(1, Mathf.RoundToInt((float)Total("egg.need"))), Mathf.RoundToInt((float)Total("egg.clicks")));
    }

    protected override void DrawVisitor(Transform art)
    {
        // An orange cube cat: pointed ears, forehead stripes, a cream muzzle, whiskers, a curling tail, and the egg held in front.
        Color fur = new Color(0.98f, 0.58f, 0.16f), furTop = new Color(1f, 0.76f, 0.4f), furSide = new Color(0.78f, 0.4f, 0.08f);
        Color cream = new Color(1f, 0.9f, 0.72f), pink = new Color(1f, 0.6f, 0.65f), dark = new Color(0.14f, 0.08f, 0.05f), iris = new Color(0.85f, 0.92f, 0.25f);
        Color shell = new Color(0.99f, 0.99f, 0.97f), shellShade = new Color(0.9f, 0.9f, 0.86f);
        Transform a = art;

        // Tail curling up behind on the right.
        Block(a, "Tail 1", fur, new Vector2(26f, 90f), new Vector2(128f, 60f), -20f);
        Block(a, "Tail 2", fur, new Vector2(26f, 70f), new Vector2(148f, 118f), 10f);
        Block(a, "Tail Tip", furSide, new Vector2(26f, 34f), new Vector2(144f, 166f), 25f);
        CubeBody(a, fur, furTop, furSide, cream);
        // Ears on top.
        Block(a, "Ear L", fur, new Vector2(46f, 46f), new Vector2(-66f, 312f), 45f);
        Block(a, "Ear R", fur, new Vector2(46f, 46f), new Vector2(66f, 312f), 45f);
        Block(a, "Ear In L", pink, new Vector2(22f, 22f), new Vector2(-66f, 310f), 45f);
        Block(a, "Ear In R", pink, new Vector2(22f, 22f), new Vector2(66f, 310f), 45f);
        // Face: forehead stripes, slit-pupil eyes, cream muzzle, nose, mouth, whiskers.
        Block(a, "Forehead 1", furSide, new Vector2(10f, 30f), new Vector2(-24f, 268f));
        Block(a, "Forehead 2", furSide, new Vector2(10f, 36f), new Vector2(0f, 270f));
        Block(a, "Forehead 3", furSide, new Vector2(10f, 30f), new Vector2(24f, 268f));
        Block(a, "Eye L", iris, new Vector2(44f, 44f), new Vector2(-46f, 214f));
        Block(a, "Eye R", iris, new Vector2(44f, 44f), new Vector2(46f, 214f));
        Block(a, "Pupil L", dark, new Vector2(10f, 38f), new Vector2(-46f, 214f));
        Block(a, "Pupil R", dark, new Vector2(10f, 38f), new Vector2(46f, 214f));
        Block(a, "Glint L", Color.white, new Vector2(9f, 9f), new Vector2(-56f, 228f));
        Block(a, "Glint R", Color.white, new Vector2(9f, 9f), new Vector2(36f, 228f));
        Block(a, "Muzzle", cream, new Vector2(96f, 56f), new Vector2(0f, 156f));
        Block(a, "Nose", pink, new Vector2(20f, 14f), new Vector2(0f, 178f));
        Block(a, "Mouth 1", dark, new Vector2(4f, 14f), new Vector2(0f, 164f));
        Block(a, "Mouth 2", dark, new Vector2(26f, 4f), new Vector2(-13f, 156f), -12f);
        Block(a, "Mouth 3", dark, new Vector2(26f, 4f), new Vector2(13f, 156f), 12f);
        for (int w = 0; w < 3; w++)
        {
            Block(a, "Whisker L" + w, dark, new Vector2(46f, 3f), new Vector2(-112f, 168f - w * 10f), 8f - w * 8f);
            Block(a, "Whisker R" + w, dark, new Vector2(46f, 3f), new Vector2(112f, 168f - w * 10f), -(8f - w * 8f));
        }
        // The egg, held in both paws in front of his chin: a white pixel with softly rounded corners.
        Block(a, "Egg Shade", shellShade, new Vector2(78f, 86f), new Vector2(0f, 88f));
        Block(a, "Egg", shell, new Vector2(72f, 80f), new Vector2(-2f, 92f));
        Block(a, "Egg Round A", shell, new Vector2(58f, 92f), new Vector2(-2f, 92f));
        Block(a, "Egg Round B", shell, new Vector2(84f, 64f), new Vector2(-2f, 92f));
        Block(a, "Paw L", fur, new Vector2(30f, 28f), new Vector2(-46f, 80f));
        Block(a, "Paw R", fur, new Vector2(30f, 28f), new Vector2(42f, 80f));
    }
}

/// <summary>
/// The egg Egg leaves behind: a white cube with rounded corners sitting on the floor beside the cube. Click it (the number of clicks is rolled when it
/// is given) to crack it; the last click hatches it into a random pet you don't own yet. Its state is kept in stat counters (egg.pending / need /
/// clicks), so a saved game gets the egg back.
/// </summary>
public class PixelEgg : MonoBehaviour
{
    public static PixelEgg Current { get; private set; }

    private static int hotFrame = -10;

    /// <summary>True while the mouse is over the egg: PixelClicker then ignores cube clicks.</summary>
    public static bool Hot => hotFrame >= Time.frameCount - 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Current = null; hotFrame = -10; }

    private PixelClicker clicker;
    private Camera cam;
    private int need, clicks;
    private float size, shake, punch, bornTime;
    private Transform body;
    private readonly List<Transform> cracks = new List<Transform>();

    /// <summary>Pet types an egg could hatch into (not owned yet).</summary>
    public static List<PixelClicker.PixelType> PetCandidates(PixelClicker clicker)
    {
        List<PixelClicker.PixelType> list = new List<PixelClicker.PixelType>();
        PixelPets pets = PixelPets.Instance;
        if (pets == null || clicker == null) return list;
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
            if (!t.rareDrop && !PixelClicker.IsStandalone(t.type) && !pets.IsOwned(t.type)) list.Add(t.type);
        return list;
    }

    public static PixelEgg Spawn(PixelClicker owner, int clicksNeeded, int clicksSoFar)
    {
        if (Current != null) return Current;
        GameObject go = new GameObject("Egg");
        PixelEgg egg = go.AddComponent<PixelEgg>();
        egg.Setup(owner, clicksNeeded, clicksSoFar);
        return egg;
    }

    private void Setup(PixelClicker owner, int clicksNeeded, int clicksSoFar)
    {
        Current = this;
        clicker = owner;
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        need = Mathf.Max(1, clicksNeeded);
        clicks = Mathf.Clamp(clicksSoFar, 0, need - 1);
        bornTime = Time.time;

        // The egg takes the cube's place until it hatches: the cube shrinks away (the auto clicker pauses with it) and comes back afterwards.
        size = clicker.PixelBaseSize;
        transform.position = clicker.CubeBasePosition;
        clicker.SetCubeShrink(0.02f, 0.4f);

        GameObject eggBody = new GameObject("Egg Body", typeof(MeshFilter), typeof(MeshRenderer));
        eggBody.transform.SetParent(transform, false);
        eggBody.transform.localScale = Vector3.one * size;
        eggBody.GetComponent<MeshFilter>().sharedMesh = RoundedCubeMesh(0.16f, 6);
        Material mat = clicker.CreateVisualMaterial(new Color(0.99f, 0.99f, 0.97f, 1f), false);
        if (mat != null) eggBody.GetComponent<MeshRenderer>().sharedMaterial = mat;
        body = eggBody.transform;
        BuildCracks();
        ShowCracks();
    }

    private void BuildCracks()
    {
        // Thin dark slivers lying on the faces; more of them show as the egg takes hits.
        Random.State saved = Random.state;
        Random.InitState(need * 31 + 7);
        Vector3[] normals = { Vector3.up, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        for (int i = 0; i < 24; i++)
        {
            Vector3 n = normals[i % normals.Length];
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(c.GetComponent<Collider>());
            c.name = "Crack";
            c.transform.SetParent(body, false);
            Vector3 a = Vector3.Cross(n, Vector3.up); if (a.sqrMagnitude < 0.01f) a = Vector3.right; a.Normalize();
            Vector3 b = Vector3.Cross(n, a);
            c.transform.localPosition = n * 0.505f + a * Random.Range(-0.3f, 0.3f) + b * Random.Range(-0.3f, 0.3f);
            c.transform.localRotation = Quaternion.LookRotation(n, b) * Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
            c.transform.localScale = new Vector3(Random.Range(0.14f, 0.34f), 0.025f, 0.012f);
            Renderer r = c.GetComponent<Renderer>();
            Material m = clicker.CreateVisualMaterial(new Color(0.25f, 0.2f, 0.15f, 1f), false);
            if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cracks.Add(c.transform);
        }
        Random.state = saved;
    }

    private void ShowCracks()
    {
        int visible = Mathf.RoundToInt(cracks.Count * (clicks / (float)need));
        for (int i = 0; i < cracks.Count; i++) cracks[i].gameObject.SetActive(i < visible);
    }

    private void OnDestroy()
    {
        if (Current != this) return;
        Current = null;
        if (clicker != null && !PixelMinigame.TakeoverActive) clicker.SetCubeShrink(1f, 0.5f);   // the cube returns
    }

    private void Update()
    {
        if (clicker == null) return;
        if (cam == null) cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null || body == null) return;

        if (!clicker.CubeHidden && !PixelMinigame.TakeoverActive) clicker.SetCubeShrink(0.02f, 0.4f);   // something else gave the cube back: keep it away while the egg stands in for it
        transform.position = clicker.CubeBasePosition;
        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 2.5f);
        punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 4f);
        float progress = clicks / (float)need;
        float rock = Mathf.Sin(Time.time * 2f) * (1.5f + 5f * progress);
        body.localRotation = Quaternion.Euler(rock * 0.5f, Time.time * 4f * progress, rock + shake * Mathf.Sin(Time.time * 60f) * 8f);
        float grow = Mathf.Clamp01((Time.time - bornTime) / 0.6f);
        body.localScale = Vector3.one * size * grow * (1f - punch * 0.12f);

        if (hatching) return;
        if (PixelPauseMenu.IsPaused || Time.timeScale <= 0f || PointerOverUI()) return;
        Vector3 screen = cam.WorldToScreenPoint(transform.position);
        if (screen.z <= 0f) return;
        Vector3 edge = cam.WorldToScreenPoint(transform.position + cam.transform.right * size * 0.75f);
        float radius = Mathf.Max(30f, Mathf.Abs(edge.x - screen.x));
        if (((Vector2)screen - PointerPosition()).magnitude > radius) return;
        hotFrame = Time.frameCount;
        if (LeftPressed()) Hit();
    }

    private void Hit()
    {
        clicks++;
        PixelStats.Count("egg.clicks");
        shake = 1f;
        punch = 1f;
        PixelAudio.Play("hit");
        if (clicks >= need) { Hatch(); return; }
        ShowCracks();
    }

    private bool hatching;

    private void Hatch()
    {
        if (hatching) return;
        hatching = true;
        List<PixelClicker.PixelType> list = PetCandidates(clicker);
        if (list.Count == 0 || PixelPets.Instance == null)
        {
            ShellBurst();
            PixelStats.Clear("egg.pending");
            PixelHints.Announce("The egg hatched, but nothing was inside...");
            Destroy(gameObject);
            return;
        }
        PixelClicker.PixelType type = list[Random.Range(0, list.Count)];
        int tier = clicker.IndexOf(type);
        string petName = (tier >= 0 ? clicker.Tiers[tier].displayName : type.ToString());
        if (petName.EndsWith(" Pixels")) petName = petName.Substring(0, petName.Length - 1);
        petName += " Pet";

        // The hatching show plays over a dark screen (the egg stays, hidden, so the cube stays away and the state is kept until the pet is awarded).
        body.gameObject.SetActive(false);
        if (PixelEggHatch.Play(clicker, tier, petName, transform.position, () => FinishHatch(type, petName, true))) return;

        body.gameObject.SetActive(true);
        ShellBurst();
        FinishHatch(type, petName, false);
    }

    private void FinishHatch(PixelClicker.PixelType type, string petName, bool shown)
    {
        PixelStats.Clear("egg.pending");
        if (PixelPets.Instance != null) PixelPets.Instance.Award(type, !shown, startOff: true);   // the show replaces the found-a-pet popup; a pet from an egg starts switched OFF in Toggles
        PixelHints.Announce("The egg hatched! A new pet: " + petName);
        Destroy(gameObject);
    }

    private void ShellBurst()
    {
        for (int i = 0; i < 12; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Egg Shell";
            piece.transform.position = transform.position + Random.insideUnitSphere * size * 0.3f;
            piece.transform.rotation = Random.rotation;
            piece.transform.localScale = Vector3.one * size * Random.Range(0.12f, 0.24f);
            Material m = clicker.CreateVisualMaterial(new Color(0.98f, 0.98f, 0.95f, 1f), false);
            if (m != null) piece.GetComponent<Renderer>().sharedMaterial = m;
            Rigidbody rb = piece.AddComponent<Rigidbody>();
            rb.AddForce(Random.onUnitSphere * 3f + Vector3.up * 3f, ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * 5f, ForceMode.VelocityChange);
            Destroy(piece, 2.5f);
        }
        PixelAudio.Play("achievement");
    }

    /// <summary>A cube of edge 1 with rounded corners (radius as a fraction of the edge), made by pushing a subdivided cube out from its inner box.</summary>
    public static Mesh RoundedCubeMesh(float radius, int divisions)
    {
        int n = Mathf.Max(2, divisions);
        List<Vector3> verts = new List<Vector3>(), normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();   // each face maps the whole texture (so a drawn surface wraps the rounded block)
        List<int> tris = new List<int>();
        float inner = 1f - radius * 2f;   // half-extent of the flat part, in the [-1, 1] cube
        for (int axis = 0; axis < 3; axis++)
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                int baseIndex = verts.Count;
                for (int j = 0; j <= n; j++)
                    for (int i = 0; i <= n; i++)
                    {
                        Vector3 p = Vector3.zero;
                        p[axis] = sign;
                        p[ua] = -1f + 2f * i / n;
                        p[va] = -1f + 2f * j / n;
                        Vector3 core = new Vector3(Mathf.Clamp(p.x, -inner, inner), Mathf.Clamp(p.y, -inner, inner), Mathf.Clamp(p.z, -inner, inner));
                        Vector3 d = p - core;
                        Vector3 normal = d.sqrMagnitude > 1e-8f ? d.normalized : p.normalized;
                        verts.Add((core + normal * (radius * 2f)) * 0.5f);
                        normals.Add(normal);
                        uvs.Add(new Vector2(i / (float)n, j / (float)n));
                    }
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int a = baseIndex + j * (n + 1) + i, b = a + 1, c = a + (n + 1), e = c + 1;
                        AddTriangle(verts, normals, tris, a, c, b);
                        AddTriangle(verts, normals, tris, b, c, e);
                    }
            }
        Mesh mesh = new Mesh { name = "Rounded Cube" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddTriangle(List<Vector3> verts, List<Vector3> normals, List<int> tris, int a, int b, int c)
    {
        Vector3 face = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) < 0f) { int t = b; b = c; c = t; }   // wind it outwards
        tris.Add(a); tris.Add(b); tris.Add(c);
    }
}
