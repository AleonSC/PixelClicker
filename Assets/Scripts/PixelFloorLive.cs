// ---- System notes ----
// PixelFloorLive.cs
//   The fifth batch of floors (part of partial class PixelFloor, dispatched from ShadeWorlds' fall-through -> ShadeLive; styles from LiveStyles) and the
//   LIVE OVERLAY (PixelFloorOverlay: real 3D objects on the floor plane, updated every frame, so they can follow the mouse without redrawing a texture).
//   GRASS (big still pictures like Lush Grass: 1024 px, stretchToFloor over 40 world units, drawn in WORLD units on worker threads, never repeated or
//   stretched; every blade is its own tapered stroke from a 3 x 3 cell search - BladeLayer - bent by a noise wind field):
//     Wild Meadow (hue patches, wind-bent blades, wildflower clusters, seed heads), Clover Lawn (curving mower stripes, three-leaf clover patches,
//     daisies, worn spots, a fairy ring of mushrooms), Mossy Glade (speckled moss, ferns, mossy stones, twigs, puddles), Prairie Grass (tall golden
//     wind-swept blades, green patches, purple flowers, rocks).
//   SEE-THROUGH LASERS (FloorStyle.hideBase: the floor picture is hidden - ApplyVisibility / the edge fade read HideBaseNow - but the collider stays):
//     Neon Lasers (overlay Lasers: beams drifting / turning / pulsing over nothing, neon colours from the style), Laser Tracker (overlay LaserTracker:
//     emitters round the cube shoot beams that all converge on the mouse's point on the floor, with a pulsing ring there).
//   PICNIC BLANKET: a gingham blanket with a woven texture, fringe, folds and crumbs on meadow grass, plus overlay Ant: a small 3D ant (built from
//     primitives, no colliders) that walks to the mouse's point on the floor with a tripod leg gait and wiggling antennae, and wanders when the mouse is
//     off the floor. The overlay fades with the floor (Display floor / the menu fade) and follows FloorStyle.overlay.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using static PixelNoise;

public partial class PixelFloor
{
    // ------------------------------------------------------------------
    // Styles and hooks
    // ------------------------------------------------------------------

    private static IEnumerable<FloorStyle> LiveStyles()
    {
        FloorStyle Big(string name, FloorPattern p, Color a, Color b, Color c, int seed)
        {
            FloorStyle f = S(name, p, a, b, c, 1024, false, 40, 1, 0.1f, 0f);
            f.stretchToFloor = true; f.detail = 6; f.seed = seed;
            return f;
        }
        yield return Big("Wild Meadow", FloorPattern.WildMeadow, new Color(0.1f, 0.3f, 0.08f), new Color(0.45f, 0.68f, 0.22f), new Color(0.95f, 0.55f, 0.75f), 31);
        yield return Big("Clover Lawn", FloorPattern.CloverLawn, new Color(0.14f, 0.42f, 0.12f), new Color(0.3f, 0.6f, 0.2f), new Color(0.98f, 0.98f, 0.92f), 33);
        yield return Big("Mossy Glade", FloorPattern.MossyGlade, new Color(0.12f, 0.24f, 0.07f), new Color(0.38f, 0.55f, 0.15f), new Color(0.45f, 0.42f, 0.38f), 35);
        yield return Big("Prairie Grass", FloorPattern.PrairieGrass, new Color(0.55f, 0.45f, 0.2f), new Color(0.88f, 0.76f, 0.42f), new Color(0.6f, 0.35f, 0.75f), 37);

        FloorStyle lasers = S("Neon Lasers", FloorPattern.NeonLasers, new Color(1f, 0.15f, 0.75f), new Color(0.1f, 0.95f, 1f), new Color(0.55f, 1f, 0.2f), 16, false, 8f, 1);
        lasers.hideBase = true; lasers.overlay = FloorOverlay.Lasers;
        yield return lasers;
        FloorStyle tracker = S("Laser Tracker", FloorPattern.LaserTracker, new Color(1f, 0.1f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(0.2f, 0.85f, 1f), 16, false, 8f, 1);
        tracker.hideBase = true; tracker.overlay = FloorOverlay.LaserTracker;
        yield return tracker;
        FloorStyle picnic = Big("Picnic Blanket", FloorPattern.PicnicBlanket, new Color(0.82f, 0.12f, 0.14f), new Color(0.98f, 0.96f, 0.9f), new Color(0.2f, 0.5f, 0.15f), 39);
        picnic.overlay = FloorOverlay.Ant;
        yield return picnic;
    }

    private PixelFloorOverlay liveOverlay;
    private FloorStyle liveStyle;

    /// <summary>The current style hides the floor picture (a see-through floor).</summary>
    private bool HideBaseNow => liveStyle != null && liveStyle.hideBase;

    /// <summary>Called by Apply for every style: switches the live overlay to it.</summary>
    private void ApplyLive(FloorStyle style)
    {
        liveStyle = style;
        if (liveOverlay == null) liveOverlay = gameObject.AddComponent<PixelFloorOverlay>();
        liveOverlay.Set(style);
    }

    /// <summary>Where the overlay lives this frame: the floor's top height, the centre (the cube, or the player in first person), the reach of the
    /// visible floor and how visible the floor is (0..1).</summary>
    public bool TryGetLiveFrame(out Vector3 centre, out float radius, out float alpha)
    {
        centre = Vector3.zero; radius = 10f; alpha = 0f;
        if (floorRenderer == null) return false;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null || clicker.PixelTransform == null) return false;
        FadeRadii(clicker, out float r0, out float r1);
        Vector3 c = clicker.CubeBasePosition;
        centre = new Vector3(c.x, floorRenderer.bounds.max.y, c.z);
        radius = Mathf.Max(2f, r0 > 0f ? r0 : r1);
        alpha = Mathf.Clamp01(visibility) * (FloorOff ? 0f : 1f);
        return true;
    }

    // ------------------------------------------------------------------
    // The patterns
    // ------------------------------------------------------------------

    private static Color ShadeLive(FloorStyle s, float u, float v, int x, int y, int n)
    {
        switch (s.pattern)
        {
            case FloorPattern.WildMeadow: return ShadeWildMeadow(s, u, v, x, y);
            case FloorPattern.CloverLawn: return ShadeCloverLawn(s, u, v, x, y);
            case FloorPattern.MossyGlade: return ShadeMossyGlade(s, u, v, x, y);
            case FloorPattern.PrairieGrass: return ShadePrairie(s, u, v, x, y);
            case FloorPattern.PicnicBlanket: return ShadePicnic(s, u, v, x, y);
        }
        return s.colorA;   // (the see-through laser floors: never shown)
    }

    /// <summary>
    /// Grass blades: tapered strokes rooted on a grid that is turned to the base 'wind' direction (radians): cells are 'lenMax' long along the wind and
    /// 'laneW' wide across it, 'perCell' blades each, so only a handful of cells are searched per point. Each blade leans 'wind' + a random share of
    /// 'spread' + a slow gust wave ('gust' radians). Returns the cover (0..1) of the topmost blade at this point (blades overlap in a random order),
    /// how far up that blade the point is (t: 0 root .. 1 tip) and that blade's random tone (0..1). All sizes in world units.
    /// </summary>
    private static float BladeLayer(float wx, float wy, float wind, int perCell, float laneW, float lenMin, float lenMax, float width, float spread, float gust, int seed, out float t, out float tone)
    {
        t = 0f; tone = 0f;
        float cw = Mathf.Cos(wind), sw = Mathf.Sin(wind);
        float a = wx * cw + wy * sw, b = -wx * sw + wy * cw;   // a = along the wind, b = across it
        float cellA = Mathf.Max(0.01f, lenMax), cellB = Mathf.Max(0.01f, laneW);
        int ia0 = Mathf.FloorToInt(a / cellA), ib0 = Mathf.FloorToInt(b / cellB);
        int lat = Mathf.CeilToInt((lenMax * Mathf.Sin(Mathf.Min(1.4f, spread * 0.5f + Mathf.Abs(gust))) + width) / cellB);
        float best = 0f, bestOrder = -1f;
        int iaHi = spread > 1.5f ? ia0 + 1 : ia0;   // (blades that can point backwards need the row ahead too)
        for (int ia = ia0 - 1; ia <= iaHi; ia++)
            for (int ib = ib0 - lat; ib <= ib0 + lat; ib++)
                for (int k = 0; k < perCell; k++)
                {
                    int hx = ia * 7 + k, hy = ib;
                    float ra = (ia + Hash(hx, hy, seed)) * cellA, rb = (ib + Hash(hx, hy, seed + 1)) * cellB;
                    float dev = (Hash(hx, hy, seed + 2) - 0.5f) * spread + gust * Mathf.Sin(ra * 0.23f + rb * 0.11f + seed);
                    float len = Mathf.Lerp(lenMin, lenMax, Hash(hx, hy, seed + 3));
                    float dx = Mathf.Cos(dev), dy = Mathf.Sin(dev);
                    float pa = a - ra, pb = b - rb;
                    float along = (pa * dx + pb * dy) / len;
                    if (along < 0f || along > 1f) continue;
                    float bend = 0.2f * along * along * len * (Hash(hx, hy, seed + 4) - 0.5f);   // a slight curve towards the tip
                    float side = Mathf.Abs(pa * -dy + pb * dx - bend);
                    float w = width * (1f - along * 0.85f);
                    if (side > w) continue;
                    float order = Hash(hx, hy, seed + 5);
                    if (order <= bestOrder) continue;   // blades overlap in a random order
                    bestOrder = order;
                    best = Mathf.Clamp01((1f - side / Mathf.Max(0.0001f, w)) * 2.5f);
                    t = along; tone = Hash(hx, hy, seed + 6);
                }
        return best;
    }

    // 1. Wild Meadow --------------------------------------------------------------------

    private static Color ShadeWildMeadow(FloorStyle s, float u, float v, int x, int y)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        // Big hue patches: lush, dry and blue-green areas drift through each other.
        float patchA = Fbm(u, v, Per(s, 11f), 4, s.seed), patchB = Fbm(u, v, Per(s, 7f), 3, s.seed + 1);
        Color lush = s.colorA, dry = Color.Lerp(s.colorB, new Color(0.7f, 0.66f, 0.3f), 0.55f), cool = Color.Lerp(s.colorA, new Color(0.12f, 0.36f, 0.3f), 0.6f);
        Color ground = Color.Lerp(Color.Lerp(lush, dry, Sstep(0.45f, 0.75f, patchA)), cool, Sstep(0.55f, 0.8f, patchB) * 0.7f);
        ground *= 0.55f;   // the shadowed soil between the blades
        float wind = 1.25f + 1.1f * (Fbm(u, v, Per(s, 6f), 2, s.seed + 2) - 0.5f);
        float cover = BladeLayer(wx, wy, 1.25f, 4, 0.25f, 0.3f, 0.75f, 0.05f, 0.9f, wind - 1.25f, s.seed + 3, out float t, out float tone);
        Color bladeBase = Color.Lerp(Color.Lerp(lush, dry, Sstep(0.45f, 0.75f, patchA) * 0.8f), s.colorB, tone * 0.6f);
        Color blade = Color.Lerp(bladeBase * 0.6f, bladeBase * 1.35f, t);
        Color c = Color.Lerp(ground, blade, cover);
        // A second, finer layer of short blades fills the gaps.
        float cover2 = BladeLayer(wx + 13.7f, wy + 4.1f, 1.45f, 4, 0.15f, 0.14f, 0.32f, 0.035f, 1.2f, wind - 1.25f, s.seed + 9, out float t2, out float tone2);
        c = Color.Lerp(c, Color.Lerp(bladeBase * 0.7f, bladeBase * 1.2f, t2) * (0.85f + 0.3f * tone2), cover2 * (1f - cover * 0.85f));

        // Wildflower clusters: one cluster centre per 3.2 m cell, several heads round it, colour per cluster.
        float cl = 3.2f;
        int qx = Mathf.FloorToInt(wx / cl), qy = Mathf.FloorToInt(wy / cl);
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = qx + ox, iy = qy + oy;
                if (Hash(ix, iy, s.seed + 20) < 0.45f) continue;
                float ccx = (ix + Hash(ix, iy, s.seed + 21)) * cl, ccy = (iy + Hash(ix, iy, s.seed + 22)) * cl;
                float pick = Hash(ix, iy, s.seed + 23);
                Color petal = pick < 0.3f ? s.colorC : pick < 0.55f ? new Color(1f, 0.85f, 0.25f) : pick < 0.8f ? new Color(0.98f, 0.98f, 1f) : new Color(0.55f, 0.45f, 0.95f);
                int heads = 6 + Mathf.FloorToInt(Hash(ix, iy, s.seed + 24) * 10f);
                for (int h = 0; h < heads; h++)
                {
                    float a = Hash(ix * 31 + h, iy, s.seed + 25) * Mathf.PI * 2f, r = Mathf.Sqrt(Hash(ix, iy * 31 + h, s.seed + 26)) * 0.9f;
                    float fx = ccx + Mathf.Cos(a) * r, fy = ccy + Mathf.Sin(a) * r;
                    float dx = wx - fx, dy = wy - fy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float size = 0.08f + 0.05f * Hash(ix + h, iy - h, s.seed + 27);
                    float rim = size * (0.75f + 0.25f * Mathf.Cos(5f * Mathf.Atan2(dy, dx) + h));
                    if (d < rim) c = d < size * 0.32f ? new Color(0.98f, 0.75f, 0.15f) : petal * (0.85f + 0.2f * (1f - d / rim));
                }
            }
        // Pale seed heads scattered on single tall blades.
        if (cover > 0.5f && t > 0.88f && tone > 0.93f) c = Color.Lerp(c, new Color(0.92f, 0.88f, 0.7f), 0.7f);
        return Fin(c);
    }

    // 2. Clover Lawn ---------------------------------------------------------------------

    private static Color ShadeCloverLawn(FloorStyle s, float u, float v, int x, int y)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        // Mower stripes that bend gently with the lawn.
        float bendy = wy + 1.4f * Mathf.Sin(wx * 0.12f + s.seed) + 0.6f * Mathf.Sin(wx * 0.31f);
        float stripe = Mathf.Sin(bendy * Mathf.PI / 1.6f);
        float light = Sstep(-0.15f, 0.15f, stripe);
        Color baseCol = Color.Lerp(s.colorA, s.colorB, 0.35f + 0.4f * light);
        baseCol *= 0.85f + 0.3f * Fbm(u, v, Per(s, 5f), 3, s.seed);
        // Short dense mown blades, leaning with the stripe direction.
        float wind = light > 0.5f ? 1.45f : 1.7f;
        float cover = BladeLayer(wx, wy, wind, 3, 0.12f, 0.1f, 0.22f, 0.032f, 1.2f, 0f, s.seed + 3, out float t, out float tone);
        Color c = Color.Lerp(baseCol * 0.7f, baseCol * (0.95f + 0.35f * t) * (0.9f + 0.2f * tone), cover);

        // Worn patches: thin, yellowed grass with soil showing.
        float worn = Sstep(0.7f, 0.85f, Fbm(u, v, Per(s, 4f), 4, s.seed + 5));
        if (worn > 0f)
        {
            Color soil = Color.Lerp(new Color(0.45f, 0.36f, 0.22f), new Color(0.62f, 0.55f, 0.3f), Hash(x, y, s.seed + 6));
            c = Color.Lerp(c, Color.Lerp(soil, c * 1.1f, cover * 0.5f), worn * 0.8f);
        }
        // Clover patches: three heart-ish leaves per sprig, dense where the patch noise is high.
        float patch = Sstep(0.55f, 0.72f, Fbm(u + 0.4f, v, Per(s, 3f), 3, s.seed + 7));
        if (patch > 0f)
        {
            float cell = 0.26f;
            int ix = Mathf.FloorToInt(wx / cell), iy = Mathf.FloorToInt(wy / cell);
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int jx = ix + ox, jy = iy + oy;
                    if (Hash(jx, jy, s.seed + 8) > patch) continue;
                    float scx = (jx + Hash(jx, jy, s.seed + 9)) * cell, scy = (jy + Hash(jx, jy, s.seed + 10)) * cell;
                    float rot = Hash(jx, jy, s.seed + 11) * Mathf.PI * 2f, leafR = 0.075f;
                    for (int l = 0; l < 3; l++)
                    {
                        float a = rot + l * Mathf.PI * 2f / 3f;
                        float lx = scx + Mathf.Cos(a) * leafR, ly = scy + Mathf.Sin(a) * leafR;
                        float dx = wx - lx, dy = wy - ly;
                        if (dx * dx + dy * dy < leafR * leafR)
                        {
                            float shade = 0.85f + 0.3f * Hash(jx + l, jy, s.seed + 12);
                            c = Color.Lerp(s.colorA * 1.25f, new Color(0.32f, 0.62f, 0.25f), 0.5f) * shade;
                            if (Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - leafR * 0.5f) < 0.012f) c *= 1.2f;   // the pale chevron on each leaf
                        }
                    }
                }
        }
        // Daisies: small white heads with a yellow eye, one chance per cell.
        {
            float cell = 0.9f;
            int ix = Mathf.FloorToInt(wx / cell), iy = Mathf.FloorToInt(wy / cell);
            if (Hash(ix, iy, s.seed + 13) > 0.8f)
            {
                float fx = (ix + 0.2f + 0.6f * Hash(ix, iy, s.seed + 14)) * cell, fy = (iy + 0.2f + 0.6f * Hash(ix, iy, s.seed + 15)) * cell;
                float dx = wx - fx, dy = wy - fy, d = Mathf.Sqrt(dx * dx + dy * dy);
                float rim = 0.11f * (0.7f + 0.3f * Mathf.Abs(Mathf.Cos(6f * Mathf.Atan2(dy, dx))));
                if (d < rim) c = d < 0.035f ? new Color(1f, 0.8f, 0.15f) : s.colorC * (0.9f + 0.1f * (1f - d / rim));
            }
        }
        // A fairy ring: dark lush grass on a circle with mushroom caps along it.
        float ringCx = W * 0.62f, ringCy = W * 0.38f, ringR = 2.6f;
        float rd = Mathf.Sqrt((wx - ringCx) * (wx - ringCx) + (wy - ringCy) * (wy - ringCy));
        float ringBand = 1f - Sstep(0.12f, 0.35f, Mathf.Abs(rd - ringR));
        c = Color.Lerp(c, c * 0.72f + new Color(0f, 0.05f, 0f), ringBand * 0.7f);
        float ang = Mathf.Atan2(wy - ringCy, wx - ringCx);
        int slot = Mathf.FloorToInt((ang + Mathf.PI) / (Mathf.PI * 2f) * 22f);
        if (Hash(slot, 1, s.seed + 16) > 0.35f)
        {
            float sa = (slot + 0.5f) / 22f * Mathf.PI * 2f - Mathf.PI, mr = ringR + (Hash(slot, 2, s.seed + 17) - 0.5f) * 0.3f;
            float mx = ringCx + Mathf.Cos(sa) * mr, my = ringCy + Mathf.Sin(sa) * mr;
            float dx = wx - mx, dy = wy - my, d = Mathf.Sqrt(dx * dx + dy * dy), cap = 0.09f + 0.05f * Hash(slot, 3, s.seed + 18);
            if (d < cap) c = Color.Lerp(new Color(0.72f, 0.58f, 0.42f), new Color(0.92f, 0.85f, 0.72f), 1f - d / cap) * (dy > 0f ? 1.05f : 0.9f);
        }
        return Fin(c);
    }

    // 3. Mossy Glade ---------------------------------------------------------------------

    private static Color ShadeMossyGlade(FloorStyle s, float u, float v, int x, int y)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        // Moss: lumpy cushions in several greens with a fine speckle.
        float lumps = Fbm(u, v, Per(s, 1.4f), 4, s.seed), tones = Fbm(u, v, Per(s, 8f), 3, s.seed + 1);
        Color moss = Color.Lerp(Color.Lerp(s.colorA, s.colorB, lumps), new Color(0.45f, 0.5f, 0.12f), Sstep(0.6f, 0.85f, tones) * 0.6f);
        moss *= 0.7f + 0.45f * Hash(x, y, s.seed + 2) * Sstep(0.3f, 0.7f, lumps);
        moss *= 0.75f + 0.35f * Sstep(0.35f, 0.65f, lumps);   // shaded between cushions
        Color c = moss;
        // Bare soil and old needles where the moss thins out.
        float soil = Sstep(0.62f, 0.78f, Fbm(u, v, Per(s, 5f), 4, s.seed + 3));
        if (soil > 0f)
        {
            Color dirt = Color.Lerp(new Color(0.22f, 0.15f, 0.08f), new Color(0.38f, 0.26f, 0.14f), Hash(x / 2, y / 2, s.seed + 4));
            float needles = BladeLayer(wx, wy, 0.7f, 2, 0.12f, 0.14f, 0.26f, 0.026f, 3.1f, 0f, s.seed + 6, out _, out float nt);
            dirt = Color.Lerp(dirt, new Color(0.55f, 0.38f, 0.18f) * (0.8f + 0.3f * nt), needles);
            c = Color.Lerp(c, dirt, soil);
        }
        // Puddles: dark still water with a pale sky sheen.
        float puddle = Sstep(0.78f, 0.82f, Fbm(u + 0.3f, v + 0.1f, Per(s, 3f), 4, s.seed + 7));
        if (puddle > 0f)
        {
            float sheen = Fbm(u, v, Per(s, 0.8f), 2, s.seed + 8);
            c = Color.Lerp(c, Color.Lerp(new Color(0.08f, 0.12f, 0.14f), new Color(0.55f, 0.65f, 0.72f), Sstep(0.55f, 0.8f, sheen) * 0.6f), puddle);
        }
        // Mossy stones: rounded grey rocks with moss on their upper side.
        float scell = 4.5f;
        int sx = Mathf.FloorToInt(wx / scell), sy = Mathf.FloorToInt(wy / scell);
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = sx + ox, iy = sy + oy;
                if (Hash(ix, iy, s.seed + 9) < 0.55f) continue;
                float rcx = (ix + Hash(ix, iy, s.seed + 10)) * scell, rcy = (iy + Hash(ix, iy, s.seed + 11)) * scell;
                float r = 0.5f + 0.7f * Hash(ix, iy, s.seed + 12);
                float dx = wx - rcx, dy = wy - rcy;
                float wob = 1f + 0.15f * (Fbm(u, v, Per(s, 0.6f), 2, s.seed + 13) - 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy * 1.3f) / (r * wob);
                if (d < 1f)
                {
                    float lit = Mathf.Clamp01(0.5f + (-dx + dy) / (r * 2.2f));
                    Color rock = s.colorC * (0.55f + 0.6f * lit) * (0.9f + 0.2f * Hash(x / 3, y / 3, s.seed + 14));
                    float mossTop = Sstep(0.1f, 0.5f, dy / r + 0.3f * (lumps - 0.5f));
                    c = Color.Lerp(rock, moss * 1.1f, mossTop * 0.8f);
                    if (d > 0.9f) c *= 0.7f;   // the rim's shadow
                }
            }
        // Ferns: a stem with leaflets that shrink towards the tip.
        float fcell = 3.4f;
        int fx0 = Mathf.FloorToInt(wx / fcell), fy0 = Mathf.FloorToInt(wy / fcell);
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = fx0 + ox, iy = fy0 + oy;
                if (Hash(ix, iy, s.seed + 15) < 0.4f) continue;
                float bx = (ix + Hash(ix, iy, s.seed + 16)) * fcell, by = (iy + Hash(ix, iy, s.seed + 17)) * fcell;
                int fronds = 4 + Mathf.FloorToInt(Hash(ix, iy, s.seed + 18) * 4f);
                for (int f = 0; f < fronds; f++)
                {
                    float a = (f + Hash(ix + f, iy, s.seed + 19) * 0.6f) / fronds * Mathf.PI * 2f, len = 0.8f + 0.6f * Hash(ix, iy + f, s.seed + 20);
                    float dxs = Mathf.Cos(a), dys = Mathf.Sin(a);
                    float px = wx - bx, py = wy - by;
                    float along = (px * dxs + py * dys) / len;
                    if (along < 0f || along > 1f) continue;
                    float side = px * -dys + py * dxs;
                    float leafW = 0.22f * (1f - along) + 0.02f;
                    float leaflet = Mathf.Abs(Mathf.Sin(along * len * 9f));
                    if (Mathf.Abs(side) < 0.025f || (Mathf.Abs(side) < leafW * leaflet && Mathf.Abs(side) > 0.02f))
                    {
                        Color fern = Color.Lerp(new Color(0.18f, 0.45f, 0.12f), new Color(0.42f, 0.7f, 0.22f), along) * (0.85f + 0.3f * Hash(ix + f, iy + f, s.seed + 21));
                        c = Mathf.Abs(side) < 0.025f ? fern * 0.75f : fern;
                    }
                }
            }
        // Twigs.
        float tw = BladeLayer(wx + 50f, wy, 0.4f, 1, 1.6f, 0.5f, 1.1f, 0.05f, 3.1f, 0f, s.seed + 23, out _, out float twt);
        if (tw > 0.3f && Hash(Mathf.FloorToInt(wx / 1.6f), Mathf.FloorToInt(wy / 1.6f), s.seed + 24) > 0.6f) c = Color.Lerp(c, new Color(0.3f, 0.2f, 0.12f) * (0.8f + 0.3f * twt), tw);
        return Fin(c);
    }

    // 4. Prairie Grass -------------------------------------------------------------------

    private static Color ShadePrairie(FloorStyle s, float u, float v, int x, int y)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        float greenPatch = Sstep(0.55f, 0.75f, Fbm(u, v, Per(s, 9f), 4, s.seed));
        Color straw = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, Per(s, 5f), 3, s.seed + 1));
        Color live = Color.Lerp(new Color(0.35f, 0.45f, 0.15f), new Color(0.55f, 0.6f, 0.25f), Fbm(u, v, Per(s, 3f), 2, s.seed + 2));
        Color tint = Color.Lerp(straw, live, greenPatch * 0.75f);
        Color c = tint * 0.42f;   // deep shade under the tall grass
        // The wind: one big sweeping direction with gusts bending it.
        float gust = Fbm(u, v, Per(s, 12f), 3, s.seed + 3);
        float wind = 0.35f + 0.9f * (gust - 0.5f);
        float cover = BladeLayer(wx, wy, 0.35f, 5, 0.2f, 0.5f, 1.2f, 0.055f, 0.3f, wind - 0.35f, s.seed + 4, out float t, out float tone);
        Color blade = Color.Lerp(tint * 0.65f, Color.Lerp(tint, new Color(1f, 0.95f, 0.75f), 0.35f) * 1.15f, t) * (0.85f + 0.3f * tone);
        c = Color.Lerp(c, blade, cover);
        // Gust highlights: the bent grass catches the light in long bands.
        c *= 0.9f + 0.25f * Sstep(0.55f, 0.8f, gust);
        // Purple wildflowers riding on blade tips.
        if (cover > 0.4f && t > 0.85f && tone > 0.965f) c = s.colorC * (0.9f + 0.2f * Hash(x, y, s.seed + 5));
        // Rocks peeking out.
        float rcell = 6f;
        int rx = Mathf.FloorToInt(wx / rcell), ry = Mathf.FloorToInt(wy / rcell);
        if (Hash(rx, ry, s.seed + 6) > 0.6f)
        {
            float cx = (rx + 0.25f + 0.5f * Hash(rx, ry, s.seed + 7)) * rcell, cy = (ry + 0.25f + 0.5f * Hash(rx, ry, s.seed + 8)) * rcell;
            float r = 0.35f + 0.4f * Hash(rx, ry, s.seed + 9);
            float dx = wx - cx, dy = wy - cy, d = Mathf.Sqrt(dx * dx + dy * dy) / r;
            if (d < 1f && cover < 0.6f)
            {
                float lit = Mathf.Clamp01(0.5f + (-dx + dy) / (r * 2f));
                c = new Color(0.55f, 0.52f, 0.48f) * (0.55f + 0.6f * lit) * (0.9f + 0.2f * Hash(x / 2, y / 2, s.seed + 10));
            }
        }
        return Fin(c);
    }

    // 5. Picnic Blanket ------------------------------------------------------------------

    private static Color ShadePicnic(FloorStyle s, float u, float v, int x, int y)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        // Meadow grass all round (short blades on dappled green).
        Color grassBase = Color.Lerp(s.colorC, new Color(0.38f, 0.62f, 0.22f), Fbm(u, v, Per(s, 6f), 3, s.seed));
        float cover = BladeLayer(wx, wy, 1.4f, 4, 0.14f, 0.15f, 0.36f, 0.036f, 1.1f, 0.6f * (Fbm(u, v, Per(s, 7f), 2, s.seed + 1) - 0.5f), s.seed + 2, out float t, out float tone);
        Color c = Color.Lerp(grassBase * 0.55f, grassBase * (0.9f + 0.4f * t) * (0.9f + 0.2f * tone), cover);

        // The blanket: a slightly turned rectangle in the middle with a fringe.
        float cx = W * 0.5f, cy = W * 0.5f;
        Rot2(wx - cx, wy - cy, -8f, out float bx, out float by);
        float halfW = 6.2f, halfH = 4.6f, fringe = 0.35f;
        float ex = Mathf.Abs(bx) - halfW, ey = Mathf.Abs(by) - halfH;
        // Folds / creases: soft long waves across the cloth.
        float fold = 0.92f + 0.12f * Mathf.Sin(bx * 0.9f + 1.3f * Mathf.Sin(by * 0.4f)) + 0.06f * (Fbm(u, v, Per(s, 2.2f), 3, s.seed + 3) - 0.5f);
        if (ex < 0f && ey < 0f)
        {
            // Gingham: stripes of colour A both ways over the cream colour B; where they cross it is darkest.
            float check = 0.55f;
            bool sx = Mathf.Repeat(bx / check, 2f) < 1f, sy = Mathf.Repeat(by / check, 2f) < 1f;
            Color cloth = sx && sy ? s.colorA : (sx || sy) ? Color.Lerp(s.colorA, s.colorB, 0.5f) : s.colorB;
            // The weave: fine alternating threads.
            float thread = 0.045f;
            bool warp = (Mathf.FloorToInt(bx / thread) + Mathf.FloorToInt(by / thread)) % 2 == 0;
            cloth *= warp ? 1.02f : 0.97f;
            cloth *= 0.92f + 0.12f * Hash(Mathf.FloorToInt(bx / thread), Mathf.FloorToInt(by / (thread * 6f)), s.seed + 4);
            // A stitched hem just inside the edge.
            float edge = Mathf.Max(ex, ey);
            if (edge > -0.22f && edge < -0.16f && Mathf.Repeat((Mathf.Abs(bx) > Mathf.Abs(by) * halfW / halfH ? by : bx) / 0.12f, 1f) < 0.6f) cloth = Color.Lerp(cloth, s.colorB * 1.05f, 0.8f);
            cloth *= fold;
            // Crumbs.
            if (Hash(x / 2, y / 2, s.seed + 5) > 0.997f) cloth = new Color(0.85f, 0.7f, 0.45f);
            c = cloth;
            // A napkin folded in one corner and a little juice stain.
            float nx = bx - 3.6f, ny = by - 2.3f;
            if (Mathf.Abs(nx) + Mathf.Abs(ny) < 0.85f) c = new Color(0.96f, 0.96f, 0.98f) * (0.9f + 0.1f * Mathf.Sign(nx - ny));
            float sd = Mathf.Sqrt((bx + 2.8f) * (bx + 2.8f) + (by + 1.6f) * (by + 1.6f));
            if (sd < 0.45f + 0.08f * Mathf.Sin(Mathf.Atan2(by + 1.6f, bx + 2.8f) * 5f)) c = Color.Lerp(c, new Color(0.55f, 0.15f, 0.35f), 0.35f);
        }
        else if ((ex < fringe && ey < 0f) || (ey < fringe && ex < 0f))
        {
            // The fringe: loose threads along the edges.
            float along = ex < 0f ? bx : by;
            if (Mathf.Repeat(along / 0.11f, 1f) < 0.5f) c = Color.Lerp(s.colorB * 0.9f, s.colorA, Hash(Mathf.FloorToInt(along / 0.11f), 0, s.seed + 6) * 0.5f) * fold;
        }
        else
        {
            // The blanket's soft shadow on the grass.
            float shadow = 1f - Sstep(0f, 0.6f, Mathf.Max(ex, ey) - fringe);
            c *= 1f - 0.3f * shadow;
        }
        return Fin(c);
    }
}

/// <summary>
/// The live overlay of a floor style: real 3D objects on the floor plane, updated every frame (neon lasers, lasers that converge on the mouse, an ant that
/// follows the mouse). Added by PixelFloor.ApplyLive; fades with the floor.
/// </summary>
public class PixelFloorOverlay : MonoBehaviour
{
    [Header("Lasers")]
    [Tooltip("How many beams the Neon Lasers floor draws.")]
    [SerializeField] private int laserCount = 14;
    [Tooltip("Width of a beam's bright core (world units).")]
    [SerializeField] private float coreWidth = 0.05f;
    [Tooltip("Width of a beam's soft glow (world units).")]
    [SerializeField] private float glowWidth = 0.4f;
    [Tooltip("How many emitters shoot beams at the mouse on the Laser Tracker floor.")]
    [SerializeField] private int trackerCount = 10;
    [Tooltip("How fast the converging point catches up with the mouse (higher = snappier).")]
    [SerializeField] private float trackerFollow = 10f;

    [Header("Ant")]
    [Tooltip("Length of the ant (world units).")]
    [SerializeField] private float antLength = 0.42f;
    [Tooltip("How fast the ant walks (world units per second).")]
    [SerializeField] private float antSpeed = 1.6f;
    [Tooltip("How close to the mouse the ant stops (world units).")]
    [SerializeField] private float antStopDistance = 0.25f;

    private PixelFloor.FloorStyle style;
    private GameObject root;
    private Material lineMat;
    private readonly List<LineRenderer> cores = new List<LineRenderer>(), glows = new List<LineRenderer>();
    private readonly List<Vector4> beamState = new List<Vector4>();   // per beam: centre offset x / z (0..1 of the reach), angle, speed
    private LineRenderer ring;
    private Vector3 trackPoint;
    private bool trackInit;
    private Transform ant;
    private readonly List<Transform> antLegs = new List<Transform>();
    private Transform antAntennaL, antAntennaR;
    private float antPhase, antWanderTimer;
    private Vector3 antWander;

    public void Set(PixelFloor.FloorStyle s)
    {
        style = s;
        Clear();
        if (s == null || s.overlay == PixelFloor.FloorOverlay.None) return;
        root = new GameObject("Floor Overlay (" + s.name + ")");
        if (s.overlay == PixelFloor.FloorOverlay.Ant) BuildAnt();
        else BuildBeams(s.overlay == PixelFloor.FloorOverlay.Lasers ? Mathf.Max(1, laserCount) : Mathf.Max(1, trackerCount), s.overlay == PixelFloor.FloorOverlay.LaserTracker);
    }

    private void Clear()
    {
        if (root != null) Destroy(root);
        root = null;
        cores.Clear(); glows.Clear(); beamState.Clear(); antLegs.Clear();
        ring = null; ant = null; trackInit = false;
    }

    private void OnDestroy() { Clear(); if (lineMat != null) Destroy(lineMat); }

    // ---- lasers ----

    private LineRenderer Line(string name, float width)
    {
        if (lineMat == null) { Shader sh = PixelShaders.SpriteDefault(); lineMat = sh != null ? new Material(sh) : null; }
        GameObject g = new GameObject(name);
        g.transform.SetParent(root.transform, false);
        g.transform.rotation = Quaternion.LookRotation(Vector3.up);   // TransformZ alignment: the beam lies flat on the floor
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.sharedMaterial = lineMat;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.TransformZ;
        lr.widthMultiplier = width;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 2;
        return lr;
    }

    private void BuildBeams(int count, bool tracker)
    {
        for (int i = 0; i < count; i++)
        {
            glows.Add(Line("Beam Glow " + i, glowWidth));
            cores.Add(Line("Beam " + i, coreWidth));
            float a = tracker ? i / (float)count * Mathf.PI * 2f : Random.value * Mathf.PI;
            beamState.Add(new Vector4(Random.Range(-0.7f, 0.7f), Random.Range(-0.7f, 0.7f), a, Random.Range(-0.25f, 0.25f)));
        }
        if (tracker)
        {
            ring = Line("Focus Ring", coreWidth * 1.4f);
            ring.loop = true;
            ring.positionCount = 32;
        }
    }

    private Color BeamColor(int i, float time)
    {
        Color[] pal = { style.colorA, style.colorB, style.colorC };
        Color a = pal[i % 3], b = pal[(i + 1) % 3];
        return Color.Lerp(a, b, 0.5f + 0.5f * Mathf.Sin(time * 0.6f + i * 1.7f));
    }

    private static void Paint(LineRenderer lr, Color c, float alpha, float endFade)
    {
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                  new[] { new GradientAlphaKey(alpha * endFade, 0f), new GradientAlphaKey(alpha, 0.25f), new GradientAlphaKey(alpha, 0.75f), new GradientAlphaKey(alpha * endFade, 1f) });
        lr.colorGradient = g;
    }

    private void UpdateLasers(Vector3 centre, float reach, float alpha, float time)
    {
        float y = centre.y + 0.02f;
        for (int i = 0; i < cores.Count; i++)
        {
            Vector4 st = beamState[i];
            float ang = st.z + time * st.w;
            Vector3 mid = centre + new Vector3((st.x + 0.15f * Mathf.Sin(time * 0.3f + i)) * reach, 0f, (st.y + 0.15f * Mathf.Cos(time * 0.27f + i * 1.3f)) * reach);
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            float len = reach * (0.9f + 0.4f * Mathf.Sin(time * 0.4f + i * 2.1f));
            Vector3 a = mid - dir * len, b = mid + dir * len;
            a.y = b.y = y;
            float pulse = 0.65f + 0.35f * Mathf.Sin(time * (1.5f + (i % 4) * 0.6f) + i);
            Color c = BeamColor(i, time);
            cores[i].SetPosition(0, a); cores[i].SetPosition(1, b);
            glows[i].SetPosition(0, a); glows[i].SetPosition(1, b);
            Paint(cores[i], Color.Lerp(c, Color.white, 0.45f), alpha * pulse, 0f);
            Paint(glows[i], c, alpha * pulse * 0.22f, 0f);
            glows[i].widthMultiplier = glowWidth * (0.8f + 0.4f * pulse);
        }
    }

    private void UpdateTracker(Vector3 centre, float reach, float alpha, float time, Vector3 target)
    {
        float y = centre.y + 0.02f;
        if (!trackInit) { trackPoint = target; trackInit = true; }
        trackPoint = Vector3.Lerp(trackPoint, target, 1f - Mathf.Exp(-trackerFollow * Time.unscaledDeltaTime));
        trackPoint.y = y;
        float emitR = reach * 0.85f;
        for (int i = 0; i < cores.Count; i++)
        {
            float ang = beamState[i].z + time * 0.12f;   // the emitters slowly circle the cube
            Vector3 from = centre + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * emitR;
            from.y = y;
            float flicker = 0.75f + 0.25f * Mathf.Sin(time * 9f + i * 2.3f);
            Color c = BeamColor(i, time);
            cores[i].SetPosition(0, from); cores[i].SetPosition(1, trackPoint);
            glows[i].SetPosition(0, from); glows[i].SetPosition(1, trackPoint);
            Paint(cores[i], Color.Lerp(c, Color.white, 0.5f), alpha * flicker, 0.35f);
            Paint(glows[i], c, alpha * flicker * 0.25f, 0.2f);
        }
        if (ring != null)
        {
            float r = 0.25f + 0.08f * Mathf.Sin(time * 6f);
            for (int k = 0; k < ring.positionCount; k++)
            {
                float a = k / (float)ring.positionCount * Mathf.PI * 2f;
                ring.SetPosition(k, trackPoint + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
            }
            Paint(ring, Color.Lerp(style.colorA, Color.white, 0.6f), alpha, 1f);
        }
    }

    // ---- the ant ----

    private void BuildAnt()
    {
        GameObject a = new GameObject("Ant");
        a.transform.SetParent(root.transform, false);
        ant = a.transform;
        float L = Mathf.Max(0.05f, antLength);
        Color body = new Color(0.12f, 0.06f, 0.04f), shine = new Color(0.25f, 0.14f, 0.1f);
        Part(PrimitiveType.Sphere, ant, new Vector3(0f, L * 0.14f, -L * 0.3f), new Vector3(L * 0.34f, L * 0.26f, L * 0.42f), body);   // abdomen
        Part(PrimitiveType.Sphere, ant, new Vector3(0f, L * 0.14f, 0f), new Vector3(L * 0.2f, L * 0.18f, L * 0.26f), shine);          // thorax
        Part(PrimitiveType.Sphere, ant, new Vector3(0f, L * 0.16f, L * 0.24f), new Vector3(L * 0.24f, L * 0.2f, L * 0.22f), body);    // head
        for (int side = -1; side <= 1; side += 2)
        {
            Transform antenna = new GameObject("Antenna").transform;
            antenna.SetParent(ant, false);
            antenna.localPosition = new Vector3(side * L * 0.05f, L * 0.22f, L * 0.32f);
            Part(PrimitiveType.Cube, antenna, new Vector3(side * L * 0.06f, L * 0.04f, L * 0.1f), new Vector3(L * 0.02f, L * 0.02f, L * 0.24f), body, new Vector3(-20f, side * 25f, 0f));
            if (side < 0) antAntennaL = antenna; else antAntennaR = antenna;
            for (int k = -1; k <= 1; k++)
            {
                Transform hip = new GameObject("Leg").transform;
                hip.SetParent(ant, false);
                hip.localPosition = new Vector3(side * L * 0.08f, L * 0.12f, k * L * 0.08f);
                Part(PrimitiveType.Cube, hip, new Vector3(side * L * 0.16f, -L * 0.06f, k * L * 0.04f), new Vector3(L * 0.3f, L * 0.025f, L * 0.025f), body, new Vector3(0f, -k * side * 25f, side * -25f));
                antLegs.Add(hip);
            }
        }
        antWander = Vector3.zero;
    }

    private static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Collider col = g.GetComponent<Collider>();
        if (col != null) Destroy(col);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = scale;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        Material m = clicker != null ? clicker.CreateVisualMaterial(color, false) : null;
        Renderer r = g.GetComponent<Renderer>();
        if (m != null) r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void UpdateAnt(Vector3 centre, float reach, bool hasTarget, Vector3 target, float dt)
    {
        if (ant == null) return;
        if (ant.position == Vector3.zero || (ant.position - centre).sqrMagnitude > reach * reach * 4f) ant.position = centre + new Vector3(1.5f, 0f, -1.5f);
        Vector3 pos = ant.position; pos.y = centre.y;
        if (!hasTarget)
        {
            // No mouse on the floor: wander between random spots near the cube.
            antWanderTimer -= dt;
            if (antWanderTimer <= 0f || (antWander - pos).sqrMagnitude < 0.05f)
            {
                antWanderTimer = Random.Range(2f, 5f);
                Vector2 r = Random.insideUnitCircle * reach * 0.5f;
                antWander = centre + new Vector3(r.x, 0f, r.y);
            }
            target = antWander;
        }
        target.y = centre.y;
        Vector3 to = target - pos;
        float dist = to.magnitude;
        bool walking = dist > antStopDistance;
        if (walking)
        {
            Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
            ant.rotation = Quaternion.RotateTowards(ant.rotation, want, 360f * dt);
            float ahead = Vector3.Dot(ant.forward, to.normalized);   // walks forward, slows down while turning sharply
            pos += ant.forward * antSpeed * Mathf.Clamp01(ahead * 0.8f + 0.2f) * Mathf.Min(1f, dist / 0.6f + 0.3f) * dt;
            antPhase += dt * antSpeed * 14f;
        }
        ant.position = pos;
        // Tripod gait: legs 0, 2, 4 swing against 1, 3, 5.
        for (int i = 0; i < antLegs.Count; i++)
        {
            float swing = walking ? Mathf.Sin(antPhase + (i % 2 == 0 ? 0f : Mathf.PI)) * 28f : 0f;
            antLegs[i].localRotation = Quaternion.Euler(0f, swing, walking ? Mathf.Max(0f, Mathf.Cos(antPhase + (i % 2 == 0 ? 0f : Mathf.PI))) * 12f : 0f);
        }
        float wig = Mathf.Sin(Time.unscaledTime * (walking ? 9f : 4f)) * 12f;
        if (antAntennaL != null) antAntennaL.localRotation = Quaternion.Euler(wig, 0f, 0f);
        if (antAntennaR != null) antAntennaR.localRotation = Quaternion.Euler(-wig, 0f, 0f);
    }

    // ---- every frame ----

    private bool TryMousePoint(float floorY, out Vector3 point)
    {
        point = default;
        Ray ray;
        if (!PixelFirstPerson.TryGetViewRay(out ray))
        {
            PixelClicker clicker = PixelFind.First<PixelClicker>();
            Camera cam = clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
            if (cam == null) return false;
            ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        }
        Plane plane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        if (!plane.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    private void LateUpdate()
    {
        if (root == null || style == null) return;
        PixelFloor floor = PixelFloor.Instance;
        if (floor == null || !floor.TryGetLiveFrame(out Vector3 centre, out float reach, out float alpha) || PixelTitleScreen.Showing && alpha <= 0.01f)
        {
            if (root.activeSelf) root.SetActive(false);
            return;
        }
        bool show = alpha > 0.01f;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) return;
        float time = PixelTimeStop.IsStopped ? 0f : Time.unscaledTime;
        bool hasMouse = TryMousePoint(centre.y, out Vector3 mouse) && (mouse - centre).sqrMagnitude < reach * reach * 1.5f;
        switch (style.overlay)
        {
            case PixelFloor.FloorOverlay.Lasers: UpdateLasers(centre, reach, alpha, time); break;
            case PixelFloor.FloorOverlay.LaserTracker: UpdateTracker(centre, reach, alpha, time, hasMouse ? mouse : centre); break;
            case PixelFloor.FloorOverlay.Ant:
                if (ant != null) ant.localScale = Vector3.one * Mathf.Clamp01(alpha);   // (fades by shrinking with the floor)
                UpdateAnt(centre, reach, hasMouse, mouse, PixelTimeStop.IsStopped ? 0f : Time.unscaledDeltaTime);
                break;
        }
    }
}
