// ---- System notes (moved here from CLAUDE.md) ----
// PixelFloorFade.cs
//   FADE INTO THE SKY (fadeIntoSky, on by default): a painted floor uses Resources/PixelFadedFloor.shader (unlit, alpha) that fades the floor itself to fully see-through past the fade circle (_Center / _FadeStart / _FadeEnd set by
//   UpdateGroundShader, _Light follows the day / night cycle), so the solid sky-coloured ring (below) is not needed and the skybox shows all the way down; only the wireframe rim stays. PixelFloor.SelfFading = ground shader or faded picture.
//   Floor edge fade (partial class PixelFloor, UpdateEdgeFade from LateUpdate; disableEdgeFade): the floor fades out in a circle round
//   the player (the cube, or the first person player via PixelFirstPerson.TryGetPlayerPosition) so its edges / seams are never seen: a
//   flat annulus mesh with vertex alpha (BuildEdgeFade: see-through inside r0, solid from r1 and far past the floor's edges, coloured
//   like the sky's horizon (EdgeFadeColor: TryGetHorizonColor, else the scene sky tint / background / the floor's average),
//   Sprites/Default, a hair above the floor top) plus a digital wireframe in the rim (RebuildWire: a world-aligned grid every
//   wireSpacingCubes (0 = 1.25 pixel widths) and three rings, clipped to the rim, brightness bell-shaped across it, wireColor (alpha 0
//   = cyan), wireStrength 0 = 0.9; rebuilt when the centre moves). fadeStartCubes / fadeWidthCubes 0 = automatic from the floor's size
//   (start about half the way to the nearest edge) - BUT with a base view (PixelOrbitCamera) every floor uses the STANDARD area instead (StandardRadii: fade starts at standardFadeStart 0.9 of
//   the screen's half width on the floor at 16:9, solid at standardFadeEnd 1.3, so the floor fills the screen and only the sides / corners show the wireframe; stretched one-copy pictures are
//   enlarged to cover it, EffectiveTileSize). The ring is solid only OVER the floor: past the floor's own edge its alpha melts away over ringSoft (UpdateRingAlpha, per-vertex, redone when the centre moves), so the sky below the horizon shows instead of a flat horizon-coloured band. Scaled by the floor's own fade-in visibility; hidden with the floor / FloorOff.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Floor edge fade (partial class PixelFloor): the floor fades out in a circle round the player (the clicker cube; in first person mode the player
/// walking about), so its edges and seams are never seen. Nothing about the floor itself changes - a flat disc with a hole, coloured like the
/// sky's horizon, lies just over it and goes from see-through at the inner radius to solid at the outer one (and keeps going far beyond the floor).
/// In the outer rim a glowing digital wireframe (a world-aligned grid and a few rings) shows and fades away with the floor.
/// </summary>
public partial class PixelFloor
{
    [Header("Floor edge fade (the floor fades out round the player)")]
    [Tooltip("Turn the circular fade-out of the floor off (the floor then simply ends at its edges).")]
    [SerializeField] private bool disableEdgeFade = false;

    [Tooltip("Painted floors melt into the sky: past the fade circle the floor itself goes completely see-through (a small unlit shader, Resources/PixelFadedFloor), instead of ending in a solid sky-coloured ring that cuts the sky off. Off = the old ring. (The picture is not lit by the scene lights then; it follows the day / night cycle by hand.)")]
    [SerializeField] private bool fadeIntoSky = true;

    [Min(0f)]
    [Tooltip("Where the fade starts, in main-pixel widths from the player. 0 = automatic (about half the way to the nearest edge of the floor).")]
    [SerializeField] private float fadeStartCubes = 0f;

    [Min(0f)]
    [Tooltip("How wide the fading rim is, in main-pixel widths. 0 = automatic (a bit less than half of the start distance).")]
    [SerializeField] private float fadeWidthCubes = 0f;

    [Range(0.3f, 1.5f)]
    [Tooltip("Standard floor area: where the fade into the wireframe starts, as a fraction of the screen's half width on the floor at the base view (0.9 = the last tenth of the sides and the corners). Used when 'Fade Start Cubes' is 0.")]
    [SerializeField] private float standardFadeStart = 0.9f;

    [Range(0.4f, 2f)]
    [Tooltip("Standard floor area: where the floor is fully faded, as a fraction of the screen's half width (1.3 = a bit beyond the corners).")]
    [SerializeField] private float standardFadeEnd = 1.3f;

    [Min(0f)]
    [Tooltip("Distance between the grid lines of the wireframe rim, in main-pixel widths. 0 = the default (1.25).")]
    [SerializeField] private float wireSpacingCubes = 0f;

    [Tooltip("Colour of the wireframe. Leave the alpha at 0 for the default (a digital cyan).")]
    [SerializeField] private Color wireColor = new Color(0f, 0f, 0f, 0f);

    [Min(0f)]
    [Tooltip("How bright the wireframe is (0 = the default, 0.9).")]
    [SerializeField] private float wireStrength = 0f;

    private GameObject edgeRoot, ringObject, wireObject;
    private Mesh ringMesh, wireMesh;
    private Material ringMat, wireMat;
    private float ringR0, ringR1, wireY, edgeColorTimer;
    private Vector2 wireCenter = new Vector2(float.MaxValue, 0f);
    private PixelClicker edgeClicker;
    private readonly List<Vector3> wireVerts = new List<Vector3>();
    private readonly List<Color32> wireCols = new List<Color32>();
    private readonly List<int> wireTris = new List<int>();

    private const int RingSegments = 96;
    private float[] ringRadii, ringBaseAlpha;     // the ring's rings (radius, alpha before the floor-coverage fade)
    private Vector2 ringAlphaCentre = new Vector2(float.MaxValue, 0f);
    private Bounds ringFloorBounds;
    private float ringSoft;                         // how far past the floor's edge the solid ring melts away into the sky
    private Color32[] ringColors;

    private void UpdateEdgeFade()
    {
        bool want = !disableEdgeFade && floorRenderer != null && ((visibility > 0.001f && !FloorOff) || PlaneVisible);
        if (!want)
        {
            if (edgeRoot != null && edgeRoot.activeSelf) edgeRoot.SetActive(false);
            return;
        }
        if (edgeClicker == null) edgeClicker = PixelFind.First<PixelClicker>();
        if (edgeClicker == null || edgeClicker.PixelTransform == null) return;

        float unit = Mathf.Max(0.05f, edgeClicker.PixelBaseSize);
        Bounds fb = floorRenderer.bounds;
        FadeRadii(edgeClicker, out float r0, out float r1);
        if (edgeRoot == null || !Mathf.Approximately(r0, ringR0) || !Mathf.Approximately(r1, ringR1)) BuildEdgeFade(r0, r1, fb);
        if (edgeRoot == null) return;
        if (!edgeRoot.activeSelf) edgeRoot.SetActive(true);

        Vector3 center = edgeClicker.CubeBasePosition;
        if (PixelFirstPerson.TryGetPlayerPosition(out Vector3 playerPos)) center = playerPos;
        float y = fb.max.y + unit * 0.012f;
        ringObject.transform.position = new Vector3(center.x, y, center.z);
        bool ringOn = !(SelfFading && !FloorOff);   // a shader ground fades itself (see-through); only the wireframe rim is added to it
        if (ringObject.activeSelf != ringOn) ringObject.SetActive(ringOn);

        Vector2 c2 = new Vector2(center.x, center.z);
        float moved = unit * 0.05f;
        if ((c2 - wireCenter).sqrMagnitude > moved * moved || !Mathf.Approximately(y, wireY)) RebuildWire(c2, y, r0, r1, unit);

        // Past the floor's own edge the ring melts away (see UpdateRingAlpha), so the sky below the horizon shows instead of a flat band.
        ringFloorBounds = fb;
        ringSoft = Mathf.Max(unit * 8f, r1 * 0.6f);
        if (ringOn && ((c2 - ringAlphaCentre).sqrMagnitude > (unit * 0.25f) * (unit * 0.25f) || ringColors == null)) UpdateRingAlpha(c2);

        edgeColorTimer -= Time.unscaledDeltaTime;
        if (edgeColorTimer <= 0f)
        {
            edgeColorTimer = 0.3f;
            Color flat = EdgeFadeColor();
            ringBase = flat;
        }
        float fadeAlpha = FloorOff ? 1f : visibility;   // (the flat plane's rim is solid)
        ringMat.color = new Color(ringBase.r, ringBase.g, ringBase.b, fadeAlpha);
        Color wc = wireColor.a > 0f ? wireColor : new Color(0.25f, 1f, 0.9f, 1f);
        wireMat.color = new Color(wc.r, wc.g, wc.b, FloorOff ? 0f : visibility * (wireStrength > 0f ? wireStrength : 0.9f));
    }

    /// <summary>
    /// Where the floor's fade starts (r0) and ends (r1), in world units from the player. Normally from the floor's size; a painted picture that is
    /// ONE clamped copy (stretchToFloor) must be gone before its own edge, or its edge pixels would show smeared outwards.
    /// </summary>
    /// <summary>
    /// The STANDARD area every floor covers: sized from the base view (PixelOrbitCamera), so the floor fills the screen and only its very edge - the left and right sides
    /// and the corners - fades into the wireframe. The fade starts at 90% of the screen's half width (measured on the floor, at 16:9) and is solid at 130%.
    /// False when there is no base view (the old size-based fade is used then).
    /// </summary>
    private bool StandardRadii(Bounds fb, out float r0, out float r1)
    {
        r0 = r1 = 0f;
        if (!PixelOrbitCamera.TryGetBaseView(out Vector3 camPos, out float fov)) return false;
        float height = Mathf.Max(0.5f, camPos.y - fb.max.y);
        float halfWidth = height * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * (16f / 9f);
        r0 = halfWidth * Mathf.Max(0.1f, standardFadeStart);
        r1 = halfWidth * Mathf.Max(standardFadeStart + 0.05f, standardFadeEnd);
        return true;
    }

    /// <summary>Size of the ONE copy of a stretched picture, in world units: at least the standard area (so the picture covers it instead of fading out early), else the style's own size.</summary>
    private float EffectiveTileSize(FloorStyle style)
    {
        float tile = Mathf.Max(0.1f, style.tileWorldSize);
        if (!style.stretchToFloor || floorRenderer == null || !StandardRadii(floorRenderer.bounds, out _, out float r1)) return tile;
        float aspect = style.tileAspect > 0.01f ? style.tileAspect : 1f;
        return Mathf.Max(tile, 2f * r1 / 0.94f / Mathf.Min(1f, aspect));
    }

    /// <summary>The circle the floor fades out at (centre = the floor's middle, radius = where the fade ends): old pixels are kept inside it (PixelViewBounds). False when there is no floor yet.</summary>
    public bool TryGetEdgeCircle(out Vector3 center, out float radius)
    {
        center = Vector3.zero;
        radius = 0f;
        if (floorRenderer == null) return false;
        PixelClicker c = edgeClicker != null ? edgeClicker : PixelFind.First<PixelClicker>();
        if (c == null) return false;
        FadeRadii(c, out _, out float r1);
        center = floorRenderer.bounds.center;
        radius = r1;
        return radius > 0.1f;
    }

    private void FadeRadii(PixelClicker clicker, out float r0, out float r1)
    {
        float unit = Mathf.Max(0.05f, clicker.PixelBaseSize);
        Bounds fb = floorRenderer.bounds;
        float half = Mathf.Max(unit, Mathf.Min(fb.extents.x, fb.extents.z));
        if (fadeStartCubes <= 0f && StandardRadii(fb, out r0, out r1))
        {
            float cap = half * 0.97f;   // (never past the floor's own edge)
            if (r1 > cap) { float k = cap / r1; r0 *= k; r1 = cap; }
            return;
        }
        r0 = fadeStartCubes > 0f ? unit * fadeStartCubes : Mathf.Max(unit * 5f, half * 0.5f);
        r1 = r0 + (fadeWidthCubes > 0f ? unit * fadeWidthCubes : Mathf.Max(unit * 3f, half * 0.45f));
        if (groundShaderOn || current < 0 || current >= styles.Count || !styles[current].stretchToFloor) return;
        FloorStyle st = styles[current];
        float aspect = st.tileAspect > 0.01f ? st.tileAspect : 1f;
        float limit = Mathf.Min(st.tileWorldSize * aspect, st.tileWorldSize) * 0.5f * 0.94f;
        if (r1 <= limit) return;
        r1 = Mathf.Max(unit * 4f, limit);
        r0 = Mathf.Max(unit * 1.5f, Mathf.Min(r0, r1 - Mathf.Max(unit * 3f, r1 * 0.45f)));
    }

    private Color ringBase = new Color(0.5f, 0.55f, 0.6f);

    /// <summary>The colour the floor fades into: the sky's horizon colour (so the circle melts into the sky), else the scene's sky tint / background, else the floor's own average.</summary>
    private Color EdgeFadeColor()
    {
        Color flat;
        if (PixelSkybox.Instance != null && PixelSkybox.Instance.TryGetHorizonColor(out Color sky)) flat = sky;
        else if (PixelSkybox.Instance != null && PixelSkybox.Instance.Usable && PixelSkybox.Instance.Current >= 0) flat = PixelSkybox.Instance.PreviewColor(PixelSkybox.Instance.Current);
        else if (Camera.main != null && Camera.main.clearFlags == CameraClearFlags.SolidColor) flat = Camera.main.backgroundColor;
        else flat = AverageColor();
        flat.a = 1f;
        return flat;
    }

    private void BuildEdgeFade(float r0, float r1, Bounds floorBounds)
    {
        DestroyEdgeFade();
        Shader shader = PixelShaders.SpriteDefault();
        if (shader == null) return;
        ringR0 = r0; ringR1 = r1;
        edgeRoot = new GameObject("Floor Edge Fade");
        edgeRoot.hideFlags = HideFlags.DontSave;

        // The ring: see-through inside r0, solid from r1, then solid out far past the floor's edges.
        float outer = Mathf.Max(r1 * 3f, floorBounds.size.magnitude * 2f);
        List<Vector3> v = new List<Vector3>();
        List<Color32> c = new List<Color32>();
        List<int> t = new List<int>();
        const int steps = 10;
        const int outerSteps = 28;   // finer rings out past r1: the ring fades away past the floor's edge, wherever that is
        float[] radii = new float[steps + 1 + outerSteps];
        float[] alphas = new float[steps + 1 + outerSteps];
        for (int i = 0; i <= steps; i++)
        {
            float k = i / (float)steps;
            radii[i] = Mathf.Lerp(r0, r1, k);
            alphas[i] = k * k * k * (k * (6f * k - 15f) + 10f);   // smoother-step from 0 to 1 (no visible band at either end)
        }
        for (int i = 1; i <= outerSteps; i++)
        {
            radii[steps + i] = r1 * Mathf.Pow(outer / r1, i / (float)outerSteps);   // geometric: fine near the player, coarse far away
            alphas[steps + i] = 1f;
        }
        ringRadii = radii; ringBaseAlpha = alphas;
        for (int ring = 0; ring < radii.Length; ring++)
            for (int s = 0; s < RingSegments; s++)
            {
                float a = s / (float)RingSegments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * radii[ring], 0f, Mathf.Sin(a) * radii[ring]));
                c.Add(new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alphas[ring] * 255f)));
            }
        for (int ring = 0; ring < radii.Length - 1; ring++)
            for (int s = 0; s < RingSegments; s++)
            {
                int a0 = ring * RingSegments + s, a1 = ring * RingSegments + (s + 1) % RingSegments;
                int b0 = (ring + 1) * RingSegments + s, b1 = (ring + 1) * RingSegments + (s + 1) % RingSegments;
                t.Add(a0); t.Add(b0); t.Add(a1);
                t.Add(a1); t.Add(b0); t.Add(b1);
            }
        ringMesh = new Mesh { name = "Floor Fade Ring" };
        ringMesh.SetVertices(v);
        ringMesh.SetColors(c);
        ringColors = new Color32[c.Count];
        ringAlphaCentre = new Vector2(float.MaxValue, 0f);
        ringMesh.SetTriangles(t, 0);
        ringMesh.bounds = new Bounds(Vector3.zero, new Vector3(outer * 2f, 1f, outer * 2f));

        ringObject = new GameObject("Ring", typeof(MeshFilter), typeof(MeshRenderer));
        ringObject.transform.SetParent(edgeRoot.transform, false);
        ringObject.GetComponent<MeshFilter>().sharedMesh = ringMesh;
        ringMat = new Material(shader) { name = "Floor Fade Ring", renderQueue = 3000 };
        MeshRenderer rr = ringObject.GetComponent<MeshRenderer>();
        rr.sharedMaterial = ringMat;
        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rr.receiveShadows = false;

        wireObject = new GameObject("Wireframe", typeof(MeshFilter), typeof(MeshRenderer));
        wireObject.transform.SetParent(edgeRoot.transform, false);
        wireMesh = new Mesh { name = "Floor Fade Wireframe" };
        wireMesh.MarkDynamic();
        wireObject.GetComponent<MeshFilter>().sharedMesh = wireMesh;
        wireMat = new Material(shader) { name = "Floor Fade Wireframe", renderQueue = 3001 };
        MeshRenderer wr = wireObject.GetComponent<MeshRenderer>();
        wr.sharedMaterial = wireMat;
        wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        wr.receiveShadows = false;

        wireCenter = new Vector2(float.MaxValue, 0f);
        edgeColorTimer = 0f;
    }

    /// <summary>
    /// Ring alpha = its radial fade x "is this spot over the floor": 1 over the floor (so edges / seams stay hidden), melting to 0 over ringSoft past the floor's edge, so beyond the
    /// floor the sky (also below the horizon) shows instead of a solid horizon-coloured band. Recomputed when the centre has moved.
    /// </summary>
    private void UpdateRingAlpha(Vector2 centre)
    {
        if (ringMesh == null || ringRadii == null) return;
        ringAlphaCentre = centre;
        Vector3 mn = ringFloorBounds.min, mx = ringFloorBounds.max;
        float soft = Mathf.Max(0.01f, ringSoft);
        int i = 0;
        for (int ring = 0; ring < ringRadii.Length; ring++)
            for (int sg = 0; sg < RingSegments; sg++, i++)
            {
                float ang = sg / (float)RingSegments * Mathf.PI * 2f;
                float x = centre.x + Mathf.Cos(ang) * ringRadii[ring], z = centre.y + Mathf.Sin(ang) * ringRadii[ring];
                float dx = Mathf.Max(Mathf.Max(mn.x - x, x - mx.x), 0f), dz = Mathf.Max(Mathf.Max(mn.z - z, z - mx.z), 0f);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float cover = 1f - Mathf.SmoothStep(0f, 1f, d / soft);
                ringColors[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(ringBaseAlpha[ring] * cover * 255f));
            }
        ringMesh.SetColors(ringColors);
    }

    /// <summary>The digital wireframe: grid lines on the ground (fixed in the world) and a few rings, only inside the fading rim, brightest in the middle of it.</summary>
    private void RebuildWire(Vector2 centre, float y, float r0, float r1, float unit)
    {
        wireCenter = centre;
        wireY = y;
        wireVerts.Clear(); wireCols.Clear(); wireTris.Clear();
        float spacing = unit * (wireSpacingCubes > 0f ? wireSpacingCubes : 1.25f);
        float lineW = Mathf.Max(unit * 0.03f, spacing * 0.03f);
        float piece = Mathf.Max(unit * 0.5f, (r1 - r0) / 6f);

        // lines at constant x, then at constant z
        for (int axis = 0; axis < 2; axis++)
        {
            float c = axis == 0 ? centre.x : centre.y;      // the coordinate the lines are fixed in
            float o = axis == 0 ? centre.y : centre.x;      // the other one (along the line)
            int first = Mathf.CeilToInt((c - r1) / spacing), last = Mathf.FloorToInt((c + r1) / spacing);
            for (int i = first; i <= last; i++)
            {
                float g = i * spacing, d = g - c;
                if (Mathf.Abs(d) >= r1) continue;
                float zo = Mathf.Sqrt(r1 * r1 - d * d);
                float zi = Mathf.Abs(d) < r0 ? Mathf.Sqrt(r0 * r0 - d * d) : 0f;
                AddInterval(axis, g, o, d, zi, zo, y, r0, r1, piece, lineW);
                AddInterval(axis, g, o, d, -zo, -zi, y, r0, r1, piece, lineW);
            }
        }
        // rings
        for (int k = 1; k <= 3; k++)
        {
            float r = Mathf.Lerp(r0, r1, k / 4f);
            float t = (r - r0) / (r1 - r0);
            float a = WireAlpha(t);
            for (int s = 0; s < RingSegments; s++)
            {
                float a0 = s / (float)RingSegments * Mathf.PI * 2f, a1 = (s + 1) / (float)RingSegments * Mathf.PI * 2f;
                AddSegment(new Vector3(centre.x + Mathf.Cos(a0) * r, y, centre.y + Mathf.Sin(a0) * r), new Vector3(centre.x + Mathf.Cos(a1) * r, y, centre.y + Mathf.Sin(a1) * r), a, a, lineW * 1.3f);
            }
        }
        wireMesh.Clear();
        wireMesh.SetVertices(wireVerts);
        wireMesh.SetColors(wireCols);
        wireMesh.SetTriangles(wireTris, 0);
        wireMesh.RecalculateBounds();
    }

    private static float WireAlpha(float t) => Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)), 0.8f);

    /// <summary>A stretch of a grid line (offsets zFrom..zTo along it, measured from the centre), cut into pieces so its brightness follows the distance.</summary>
    private void AddInterval(int axis, float g, float o, float d, float zFrom, float zTo, float y, float r0, float r1, float piece, float width)
    {
        if (zTo - zFrom < 0.001f) return;
        int n = Mathf.Max(1, Mathf.CeilToInt((zTo - zFrom) / piece));
        for (int i = 0; i < n; i++)
        {
            float za = Mathf.Lerp(zFrom, zTo, i / (float)n), zb = Mathf.Lerp(zFrom, zTo, (i + 1) / (float)n);
            float ra = Mathf.Sqrt(d * d + za * za), rb = Mathf.Sqrt(d * d + zb * zb);
            float aa = WireAlpha((ra - r0) / (r1 - r0)), ab = WireAlpha((rb - r0) / (r1 - r0));
            Vector3 pa = axis == 0 ? new Vector3(g, y, o + za) : new Vector3(o + za, y, g);
            Vector3 pb = axis == 0 ? new Vector3(g, y, o + zb) : new Vector3(o + zb, y, g);
            AddSegment(pa, pb, aa, ab, width);
        }
    }

    private void AddSegment(Vector3 a, Vector3 b, float alphaA, float alphaB, float width)
    {
        Vector3 dir = b - a;
        if (dir.sqrMagnitude < 1e-8f) return;
        dir.Normalize();
        Vector3 side = new Vector3(-dir.z, 0f, dir.x) * (width * 0.5f);
        int i = wireVerts.Count;
        wireVerts.Add(a - side); wireVerts.Add(a + side); wireVerts.Add(b - side); wireVerts.Add(b + side);
        Color32 ca = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaA) * 255f));
        Color32 cb = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaB) * 255f));
        wireCols.Add(ca); wireCols.Add(ca); wireCols.Add(cb); wireCols.Add(cb);
        wireTris.Add(i); wireTris.Add(i + 2); wireTris.Add(i + 1);
        wireTris.Add(i + 1); wireTris.Add(i + 2); wireTris.Add(i + 3);
    }

    private void DestroyEdgeFade()
    {
        if (edgeRoot != null) Destroy(edgeRoot);
        if (ringMesh != null) Destroy(ringMesh);
        if (wireMesh != null) Destroy(wireMesh);
        if (ringMat != null) Destroy(ringMat);
        if (wireMat != null) Destroy(wireMat);
        edgeRoot = ringObject = wireObject = null;
        ringMesh = wireMesh = null;
        ringMat = wireMat = null;
    }
}
