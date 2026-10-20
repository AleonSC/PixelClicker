// ---- System notes (moved here from CLAUDE.md) ----
// PixelGelFx.cs
//   The Ghost pixel's look (looksVersion 39 replaced the saved look): a gray, translucent, gelatinous blob - PixelLook.blob swaps the
//   cube for a very rounded one (PixelGelFx.BlobMesh, PixelEgg.RoundedCubeMesh(0.32) scaled 1.1x so it doesn't look smaller than the
//   other pixels, via PixelClicker.MeshFor; hit box still a cube), PixelLook.gelFx adds a darker nucleus that drifts, turns and
//   breathes inside (PixelGelCore, unlit vertex-coloured rounded cube) and a few tiny bubbles rising through it; stronger jelly wobble
//   (wobbleAmount 0.2, speed 2.8); colour gray (0.62, 0.66, 0.71, alpha 0.4), glossy 0.92. Old behaviour unchanged (bounces slowly,
//   floats off screen). Sounds (code-synthesized while their clip lists are empty): click pixel_ghost = PixelSynth.GhostClick (a wet
//   gloopy bloop with a second bubble, a squelch and a breathy tail, 3 variants), landing pixel_bounce_ghost = GhostBounce (soft
//   squishy splut), and ghost_float (played by OldPixelFloat.StartFloating) = GhostFloat (a hollow breathy rising 'whoo').
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Ghost pixel's look: a gray, translucent, gelatinous BLOB - a very rounded cube mesh (<see cref="BlobMesh"/>, swapped in for the plain cube by
/// <c>PixelClicker.MeshFor</c> when <c>PixelLook.blob</c> is set) with a darker nucleus drifting about inside it and a few tiny bubbles
/// rising through it (<see cref="Attach"/>, <c>PixelLook.gelFx</c>). The jelly wobble itself comes from the look's wobble settings.
/// </summary>
public static class PixelGelFx
{
    private static readonly Dictionary<int, Mesh> blobs = new Dictionary<int, Mesh>();
    private static Mesh coreMesh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { blobs.Clear(); coreMesh = null; }

    /// <summary>The blob: the pixel's cube replaced by a very rounded one of the same size.</summary>
    public static Mesh BlobMesh(Mesh cube)
    {
        if (cube == null) return null;
        int key = PixelObjectId.Of(cube);
        if (blobs.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        Mesh m = Object.Instantiate(PixelEgg.RoundedCubeMesh(0.32f, 12));
        m.name = "Gel Blob";
        Vector3 size = cube.bounds.size, centre = cube.bounds.center;
        Vector3[] v = m.vertices;
        for (int i = 0; i < v.Length; i++) v[i] = Vector3.Scale(v[i], size * 1.1f) + centre;   // a touch bigger than the cube: a rounded blob reads smaller than a box
        m.vertices = v;
        m.RecalculateBounds();
        blobs[key] = m;
        return m;
    }

    private static Mesh Core()
    {
        if (coreMesh != null) return coreMesh;
        Mesh m = Object.Instantiate(PixelEgg.RoundedCubeMesh(0.42f, 8));
        m.name = "Gel Core";
        Color[] c = new Color[m.vertexCount];
        for (int i = 0; i < c.Length; i++) c[i] = new Color(0.36f, 0.4f, 0.45f, 0.5f);
        m.colors = c;
        coreMesh = m;
        return coreMesh;
    }

    public static void Attach(Transform parent, Vector3 centre, Vector3 size)
    {
        float unit = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));

        GameObject core = new GameObject("Gel Nucleus", typeof(MeshFilter), typeof(MeshRenderer), typeof(PixelGelCore));
        core.transform.SetParent(parent, false);
        core.transform.localPosition = centre;
        core.layer = parent.gameObject.layer;
        core.GetComponent<MeshFilter>().sharedMesh = Core();
        MeshRenderer mr = core.GetComponent<MeshRenderer>();
        mr.sharedMaterial = PixelLooks.OverlayMaterial();   // unlit vertex colours, drawn after the translucent body
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        core.GetComponent<PixelGelCore>().Setup(centre, unit);

        if (PixelFx.Material == null) return;
        ParticleSystem bubbles = PixelFx.Make(parent, "Gel Bubbles", new PixelFx.Spec
        {
            lifetime = new Vector2(1.4f, 2.6f), speed = new Vector2(0.02f, 0.08f) * unit, size = new Vector2(0.03f, 0.07f) * unit, gravity = -0.03f,
            colorA = new Color(0.9f, 0.93f, 0.97f, 0.7f), colorB = new Color(0.7f, 0.74f, 0.8f, 0.6f), rate = 3f, loop = true, localSpace = true,
            shape = ParticleSystemShapeType.Box, shapePosition = centre, randomDirection = 0.3f, sizeOverLife = new Vector2(1f, 0.4f), maxParticles = 14,
            overLife = PixelFx.FadeGradient(Color.white, Color.white, new Color(0.8f, 0.85f, 0.9f), 0.7f),
        });
        ParticleSystem.ShapeModule shape = bubbles.shape;
        shape.scale = size * 0.55f;
        bubbles.Play();
    }
}

/// <summary>Drifts the darker nucleus about inside the gel blob, slowly turning and breathing.</summary>
public class PixelGelCore : MonoBehaviour
{
    private Vector3 home;
    private float unit, phase;

    public void Setup(Vector3 localCentre, float meshUnit)
    {
        home = localCentre; unit = meshUnit;
        phase = Random.Range(0f, 6.28f);
    }

    private void Update()
    {
        float t = Time.time + phase;
        Vector3 drift = new Vector3(Mathf.Sin(t * 0.9f), Mathf.Sin(t * 1.3f + 1f), Mathf.Cos(t * 0.7f)) * 0.06f;
        transform.localPosition = home + drift * unit;
        transform.localRotation = Quaternion.Euler(t * 12f, t * 20f, 0f);
        transform.localScale = Vector3.one * unit * (0.38f + 0.03f * Mathf.Sin(t * 2.2f));
    }
}
