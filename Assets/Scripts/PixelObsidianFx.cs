// ---- System notes (moved here from CLAUDE.md) ----
// PixelObsidianFx.cs
//   Obsidian flair: natural black glass (dark neutral bevel rim, no emission / violet; looksVersion 31) with PixelLook.obsidianFx = a
//   few pale glints (Attach, from AddExtras), Hit (shards + glints, stronger with progress) and Break (glass spray + glints) called
//   from CollectInternal (disableFarmFx), plus FractureFx (jagged white crack lines racing outward in the camera plane, flickering and
//   fading; more / longer with progress; replaces the old rings) and shine. Obsidian's hit / break no longer use the white FractureFx
//   lines: they throw black / grey dust that poofs sideways (PixelObsidianFx.Dust: a flat ring of swelling, fading puffs plus a few
//   drifting up) along with the glass shards. The Obsidian sheen (PixelLook.shine) follows the jagged mesh (ShineMesh(size, jagged,
//   seed) displaces its grid with PixelRockMesh.Displace) so it never sticks out past the chipped edges. Obsidian clicks play
//   PixelSynth.ObsidianPing (while pixel_obsidian has no clips) with a pitch that rises per hit (PixelAudio.OnHit 0.6 -> 1.45, break
//   1.6). Hit / Break bursts are pooled (PixelFx.Rent); FractureFx flashes are pooled too (switched off and reused, MaxPooled 12).
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Extra flair for the Obsidian pixel: violet sparks drifting off the cube while it sits there (<see cref="Attach"/>, driven by
/// <see cref="PixelLook.obsidianFx"/>), glassy shards and a violet flash on every non-final hit (<see cref="Hit"/>, stronger as it nears
/// breaking) and a big shatter with a ring when it breaks (<see cref="Break"/>). Built from <see cref="PixelFx"/>; all code-made.
/// </summary>
public static class PixelObsidianFx
{
    // Natural volcanic glass: cold white glints, grey-black shards - nothing coloured.
    private static readonly Color Violet = new Color(0.55f, 0.6f, 0.66f, 1f);
    private static readonly Color Pale = new Color(0.93f, 0.96f, 1f, 1f);

    public static void Attach(Transform parent, Vector3 centre, Vector3 size)
    {
        if (PixelFx.Material == null) return;
        float unit = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        PixelFx.Spec s = new PixelFx.Spec
        {
            loop = true, shape = ParticleSystemShapeType.Box, shapePosition = centre, randomDirection = 0.6f, maxParticles = 50,
            lifetime = new Vector2(0.8f, 1.6f), speed = new Vector2(0.05f, 0.3f) * unit, size = new Vector2(0.04f, 0.1f) * unit,
            gravity = -0.08f, colorA = Pale, colorB = Violet, rate = 5f, sizeOverLife = new Vector2(1f, 0f),
            overLife = PixelFx.FadeGradient(Color.white, new Color(0.85f, 0.9f, 0.95f), new Color(0.45f, 0.5f, 0.56f), 1f),
        };
        ParticleSystem ps = PixelFx.Make(parent, "Obsidian Sparks", s);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.scale = size * 1.05f;
        ps.Play();
    }

    // (a pooled one-shot burst: PixelFx.Rent; it goes back to the pool by itself after 3.5 s)
    private static PixelFx.Burst Root(string name, Vector3 centre) => PixelFx.Rent(centre, 3.5f);

    /// <summary>Black and grey dust that poofs sideways off the cube, swelling and fading (a flat ring of puffs plus a few that drift up).</summary>
    private static void Dust(PixelFx.Burst root, float size, int count, float strength)
    {
        Gradient fade = PixelFx.FadeGradient(Color.white, new Color(0.85f, 0.85f, 0.88f), new Color(0.55f, 0.55f, 0.6f), 0.75f);
        root.Add("Dust", new PixelFx.Spec
        {
            lifetime = new Vector2(0.7f, 1.4f), speed = new Vector2(0.6f, 2.1f) * size * strength, size = new Vector2(0.2f, 0.42f) * size, gravity = -0.04f,
            colorA = new Color(0.07f, 0.07f, 0.08f, 1f), colorB = new Color(0.46f, 0.47f, 0.5f, 1f), burst = count,
            shape = ParticleSystemShapeType.Circle, shapeRadius = size * 0.45f, shapeRotation = new Vector3(90f, 0f, 0f), randomDirection = 0.12f,
            sizeOverLife = new Vector2(0.5f, 1.8f), maxParticles = 60, overLife = fade,
        }).Play();
        root.Add("Dust Up", new PixelFx.Spec
        {
            lifetime = new Vector2(0.8f, 1.5f), speed = new Vector2(0.2f, 0.9f) * size * strength, size = new Vector2(0.18f, 0.34f) * size, gravity = -0.12f,
            colorA = new Color(0.1f, 0.1f, 0.11f, 1f), colorB = new Color(0.4f, 0.41f, 0.44f, 1f), burst = Mathf.Max(3, count / 3),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.2f,
            sizeOverLife = new Vector2(0.5f, 1.6f), maxParticles = 30, overLife = fade,
        }).Play();
    }

    /// <summary>A non-final hit (progress 0-1): dark glassy shards and violet glints; more of them the closer it is to breaking.</summary>
    public static void Hit(Vector3 centre, float size, float progress)
    {
        if (PixelFx.Material == null) return;
        PixelFx.Burst root = Root("Obsidian Hit", centre);
        root.Add("Shards", new PixelFx.Spec
        {
            lifetime = new Vector2(0.5f, 0.9f), speed = new Vector2(1f, 3f) * size, size = new Vector2(0.04f, 0.1f) * size, gravity = 2.4f,
            colorA = new Color(0.05f, 0.05f, 0.06f), colorB = new Color(0.22f, 0.23f, 0.26f), burst = Mathf.RoundToInt(Mathf.Lerp(5f, 12f, progress)),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.1f, maxParticles = 20,
        }).Play();
        root.Add("Glints", new PixelFx.Spec
        {
            lifetime = new Vector2(0.3f, 0.7f), speed = new Vector2(0.6f, 2.2f) * size, size = new Vector2(0.05f, 0.12f) * size, gravity = -0.1f,
            colorA = Pale, colorB = Violet, burst = Mathf.RoundToInt(Mathf.Lerp(3f, 10f, progress)), sizeOverLife = new Vector2(1f, 0f),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.55f, randomDirection = 0.1f, maxParticles = 20,
            overLife = PixelFx.FadeGradient(Color.white, Pale, Violet, 1f),
        }).Play();
        Dust(root, size, Mathf.RoundToInt(Mathf.Lerp(5f, 12f, progress)), 0.6f);   // a little puff of dust off the sides
    }

    /// <summary>The final break: a spray of black glass, a violet burst and an expanding ring.</summary>
    public static void Break(Vector3 centre, float size)
    {
        if (PixelFx.Material == null) return;
        PixelFx.Burst root = Root("Obsidian Break", centre);
        root.Add("Glass", new PixelFx.Spec
        {
            lifetime = new Vector2(0.7f, 1.3f), speed = new Vector2(1.5f, 4.5f) * size, size = new Vector2(0.05f, 0.15f) * size, gravity = 2.6f,
            colorA = new Color(0.05f, 0.05f, 0.06f), colorB = new Color(0.22f, 0.23f, 0.26f), burst = 34,
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.1f, maxParticles = 44,
        }).Play();
        root.Add("Violet", new PixelFx.Spec
        {
            lifetime = new Vector2(0.8f, 1.6f), speed = new Vector2(0.8f, 3.2f) * size, size = new Vector2(0.06f, 0.16f) * size, gravity = -0.1f,
            colorA = Pale, colorB = Violet, burst = 40, sizeOverLife = new Vector2(1f, 0f),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.6f, randomDirection = 0.1f, maxParticles = 50,
            overLife = PixelFx.FadeGradient(Color.white, new Color(0.85f, 0.9f, 0.95f), new Color(0.45f, 0.5f, 0.56f), 1f),
        }).Play();
        Dust(root, size, 30, 1f);   // black / grey dust poofs off the sides
    }
}

/// <summary>
/// The Obsidian's own effect instead of a ring: jagged white fracture lines flash across the glass and race outward from the
/// middle (in the camera's plane), flickering and fading in a fraction of a second. Count / reach / time set how big it is.
/// </summary>
public class FractureFx : MonoBehaviour
{
    private Vector3 centre, right, up;
    private float reach, seconds, age, unit;
    private Color coreColor = new Color(0.97f, 0.98f, 1f, 1f), glowColor = new Color(0.8f, 0.86f, 0.95f, 1f);
    private float[] angles, lengths;
    private Mesh mesh;
    private readonly PixelBolts.Builder builder = new PixelBolts.Builder();

    // Pooled: a finished flash is switched off and reused (object, mesh, arrays) - electric pixels flash on every hit.
    private const int MaxPooled = 12;
    private static readonly System.Collections.Generic.Stack<FractureFx> pool = new System.Collections.Generic.Stack<FractureFx>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { pool.Clear(); PixelStatics.ResetOnRestart(ClearPool); }   // restart-reset-not-progress: the obsidian effect pool
    private static void ClearPool() => pool.Clear();   // (Restart: the pooled flashes go with the scene)

    public static void Play(Vector3 centre, float size, int lines, float reachInSizes, float seconds) => Play(centre, size, lines, reachInSizes, seconds, new Color(0.97f, 0.98f, 1f, 1f), new Color(0.8f, 0.86f, 0.95f, 1f));

    /// <summary>The same crack / arc flash in other colours (the Electrician pixels use it for their lightning).</summary>
    public static void Play(Vector3 centre, float size, int lines, float reachInSizes, float seconds, Color core, Color glow)
    {
        Camera cam = Camera.main;
        if (cam == null || PixelFx.Material == null) return;
        FractureFx fx = null;
        while (pool.Count > 0 && fx == null) fx = pool.Pop();   // (a destroyed one reads as null: skip it)
        if (fx == null)
        {
            GameObject made = new GameObject("Obsidian Fracture", typeof(MeshFilter), typeof(MeshRenderer));
            fx = made.AddComponent<FractureFx>();
            fx.mesh = new Mesh { name = "Fracture" };
            fx.mesh.MarkDynamic();
            made.GetComponent<MeshFilter>().sharedMesh = fx.mesh;
            MeshRenderer mr = made.GetComponent<MeshRenderer>();
            mr.sharedMaterial = PixelLooks.OverlayMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        GameObject go = fx.gameObject;
        go.transform.position = centre;
        fx.age = 0f;
        fx.centre = centre; fx.right = cam.transform.right; fx.up = cam.transform.up;
        fx.coreColor = core; fx.glowColor = glow;
        fx.unit = size; fx.reach = size * reachInSizes; fx.seconds = Mathf.Max(0.1f, seconds);
        if (fx.angles == null || fx.angles.Length != lines) { fx.angles = new float[lines]; fx.lengths = new float[lines]; }
        float offset = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < lines; i++)
        {
            fx.angles[i] = offset + (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / lines;
            fx.lengths[i] = Random.Range(0.6f, 1f);
        }
        go.SetActive(true);
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float k = age / seconds;
        if (k >= 1f) { Finish(); return; }
        float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(k * 2.2f), 3f);   // races out fast, then holds while it fades
        float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.3f) / 0.7f));
        builder.Clear();
        Bounds bounds = new Bounds(centre, Vector3.one * reach * 2f);
        Color core = coreColor, glow = glowColor;
        for (int i = 0; i < angles.Length; i++)
        {
            Vector3 dir = right * Mathf.Cos(angles[i]) + up * Mathf.Sin(angles[i]);
            Vector3 from = centre + dir * reach * 0.08f;
            Vector3 to = centre + dir * reach * lengths[i] * grow;
            int segs = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from, to) / (unit * 0.25f)), 2, 8);
            PixelBolts.Jagged(builder, from, to, segs, Vector3.Distance(from, to) * 0.12f, unit * 0.035f, unit * 0.13f, true, core, glow, alpha);
        }
        // The mesh is in world space; the object sits at the centre, so move the vertices into its local space.
        for (int i = 0; i < builder.verts.Count; i++) builder.verts[i] -= centre;
        bounds.center = Vector3.zero;
        builder.Apply(mesh, bounds);
    }

    /// <summary>Done: back to the pool (switched off, mesh emptied), or gone when the pool is full.</summary>
    private void Finish()
    {
        if (pool.Count >= MaxPooled) { Destroy(gameObject); return; }
        mesh.Clear();
        gameObject.SetActive(false);
        pool.Push(this);
    }

    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
