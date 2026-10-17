// ---- System notes ----
// PixelFloorLive.cs
//   The fifth batch of floors (part of partial class PixelFloor, dispatched from ShadeWorlds' fall-through -> ShadeLive; styles from LiveStyles) and the
//   LIVE OVERLAY (PixelFloorOverlay: real 3D objects on the floor plane, updated every frame, so they can follow the mouse without redrawing a texture).
//   GRASS (big still pictures like Lush Grass: 1024 px, stretchToFloor over 40 world units, drawn in WORLD units on worker threads, never repeated or
//   stretched; every blade is its own tapered stroke from a 3 x 3 cell search - BladeLayer - bent by a noise wind field):
//     Wild Meadow (hue patches, wind-bent blades, wildflower clusters, seed heads), Clover Lawn (curving mower stripes, three-leaf clover patches,
//     daisies, worn spots, a fairy ring of mushrooms), Mossy Glade (redrawn: domed moss cushions in drifting greens lit from the top left, fallen leaves, ball-shaded stones with moss creeping up their lit side and lichen, arching fern fronds of paired leaflets, stones and ferns casting shadows down-right - GladeStone / GladeFern -, twigs, dappled sun patches), Prairie Grass (tall golden
//     wind-swept blades, green patches, purple flowers, rocks).
//   SEE-THROUGH LASERS (FloorStyle.hideBase: the floor picture is hidden - ApplyVisibility / the edge fade read HideBaseNow - but the collider stays):
//     Neon Lasers (overlay Lasers: beams drifting / turning / pulsing over nothing, neon colours from the style), Laser Tracker (overlay LaserTracker:
//     emitters round the cube shoot beams that all converge on the mouse's point on the floor, with a pulsing ring there).
//     The beams are ONE flat mesh rebuilt every frame (Ribbon: quads lying on the floor with vertex colours, a solid core over a soft glow; it was
//     LineRenderers with TransformZ alignment, which never showed in the game), never thinner
//     on screen than coreMinPixels / glowMinPixels (WorldPerPixel: the floor is far below the cube, so 0.05 world units was about a pixel). The style's
//     still picture (ShadeLaserPicture, 160 px) is never on the floor; it is the menu icon and the title screen's background picture (shown ONCE there,
//     PixelFloor.TryGetTitlePicture: it does not tile).
//   PICNIC BLANKET: a gingham blanket with a woven texture, fringe, folds and crumbs on meadow grass, plus overlay Ant: a small 3D ant (built from
//     primitives, no colliders) that walks to the mouse's point on the floor with a tripod leg gait and wiggling antennae, and wanders when the mouse is
//     off the floor. The overlay fades with the floor (Display floor / the menu fade) and follows FloorStyle.overlay.
//   CRUMBLING FLOOR (hideBase, overlay Crumble): real stone tiles (cubes, no colliders - the floor's collider stays, so husks still rest on it) over the
//     visible floor circle; on a clock cycle they stay whole, then crack loose one by one (rim first, shuffled: a shake with a warm crack glow, then they
//     drop away tumbling and shrinking), the floor stays empty a moment, then rebuilds in a ripple from the cube outwards. Timings on PixelFloorOverlay.
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

        FloorStyle lasers = S("Neon Lasers", FloorPattern.NeonLasers, new Color(1f, 0.15f, 0.75f), new Color(0.1f, 0.95f, 1f), new Color(0.55f, 1f, 0.2f), 160, false, 8f, 1);
        lasers.hideBase = true; lasers.overlay = FloorOverlay.Lasers;
        yield return lasers;
        FloorStyle tracker = S("Laser Tracker", FloorPattern.LaserTracker, new Color(1f, 0.1f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(0.2f, 0.85f, 1f), 160, false, 8f, 1);
        tracker.hideBase = true; tracker.overlay = FloorOverlay.LaserTracker;
        yield return tracker;
        FloorStyle picnic = Big("Picnic Blanket", FloorPattern.PicnicBlanket, new Color(0.82f, 0.12f, 0.14f), new Color(0.98f, 0.96f, 0.9f), new Color(0.2f, 0.5f, 0.15f), 39);
        picnic.overlay = FloorOverlay.Ant;
        yield return picnic;
        FloorStyle crumble = S("Crumbling Floor", FloorPattern.CrumbleTiles, new Color(0.42f, 0.4f, 0.38f), new Color(0.55f, 0.52f, 0.48f), new Color(1f, 0.6f, 0.25f), 160, false, 8f, 1);
        crumble.hideBase = true; crumble.overlay = FloorOverlay.Crumble;
        yield return crumble;
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
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker == null || clicker.PixelTransform == null) return false;
        FadeRadii(clicker, out float r0, out float r1);
        Vector3 c = clicker.CubeBasePosition;
        centre = new Vector3(c.x, floorRenderer.bounds.max.y, c.z);
        radius = Mathf.Max(2f, r0 > 0f ? r0 : r1);
        alpha = Mathf.Clamp01(visibility) * (FloorOff ? 0f : 1f);
        return true;
    }

    /// <summary>The floor's edge fade: it starts fading r0 and is gone r1 world units from the centre (the cube, or the player in first person).</summary>
    public bool TryGetFadeRadii(out float r0, out float r1)
    {
        r0 = r1 = 0f;
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (floorRenderer == null || clicker == null) return false;
        FadeRadii(clicker, out r0, out r1);
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
        if (s.pattern == FloorPattern.NeonLasers || s.pattern == FloorPattern.LaserTracker) return ShadeLaserPicture(s, u, v, s.pattern == FloorPattern.LaserTracker);
        if (s.pattern == FloorPattern.CrumbleTiles) return ShadeCrumblePicture(s, u, v);
        return s.colorA;
    }

    /// <summary>
    /// The still picture of a see-through laser floor: the game never shows it on the floor (hideBase), but it is the style's menu icon and the
    /// title screen's background picture - glowing beams in the style's three colours over a dark night (converging on the middle for the Tracker).
    /// </summary>
    private static Color ShadeLaserPicture(FloorStyle s, float u, float v, bool tracker)
    {
        Color[] pal = { s.colorA, s.colorB, s.colorC };
        Color col = new Color(0.015f, 0.015f, 0.04f, 1f);
        int beams = tracker ? 8 : 7;
        for (int k = 0; k < beams; k++)
        {
            float d;
            if (tracker)
            {
                // A segment from an emitter on a ring to the middle.
                float ang = (k + 0.3f * PixelNoise.Hash(k, 1, 91)) / beams * Mathf.PI * 2f;
                Vector2 a = new Vector2(0.5f + Mathf.Cos(ang) * 0.44f, 0.5f + Mathf.Sin(ang) * 0.44f), b = new Vector2(0.5f, 0.5f);
                Vector2 ab = b - a, ap = new Vector2(u, v) - a;
                float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude);
                d = (ap - ab * t).magnitude;
            }
            else
            {
                // An endless line through a random point at a random angle.
                float px = 0.15f + 0.7f * PixelNoise.Hash(k, 2, 77), py = 0.15f + 0.7f * PixelNoise.Hash(k, 3, 77), ang = PixelNoise.Hash(k, 4, 77) * Mathf.PI;
                d = Mathf.Abs((u - px) * Mathf.Sin(ang) - (v - py) * Mathf.Cos(ang));
            }
            Color c = pal[k % 3];
            float core = Mathf.Exp(-(d * d) / (0.006f * 0.006f)), glow = Mathf.Exp(-(d * d) / (0.035f * 0.035f)) * 0.45f;
            col += c * glow + Color.Lerp(c, Color.white, 0.5f) * core;
        }
        if (tracker)
        {
            float r = Mathf.Abs(new Vector2(u - 0.5f, v - 0.5f).magnitude - 0.05f);
            col += Color.Lerp(s.colorA, Color.white, 0.6f) * Mathf.Exp(-(r * r) / (0.008f * 0.008f));
        }
        col.r = Mathf.Min(1f, col.r); col.g = Mathf.Min(1f, col.g); col.b = Mathf.Min(1f, col.b); col.a = 1f;
        return col;
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

    // The Mossy Glade's light comes from the top left of the picture (-x, +y); shadows fall the other way by this many world units.
    private const float GladeShadowX = 0.16f, GladeShadowY = -0.16f;

    private static Color ShadeMossyGlade(FloorStyle s, float u, float v, int x, int y)
    {
        float W = Wu(s), wx = u * W, wy = v * W;

        // Moss: rounded cushions (a jittered cell grid in world units, warped so they are not in rows), each its own green,
        // domed and lit from the top left, a little darker in the seams between them, with a fine fuzz.
        const float cushion = 0.75f;
        float warpX = (Fbm(u, v, Per(s, 2.5f), 2, s.seed) - 0.5f) * 0.9f, warpY = (Fbm(u, v, Per(s, 2.5f), 2, s.seed + 31) - 0.5f) * 0.9f;
        float mx = (wx + warpX) / cushion, my = (wy + warpY) / cushion;
        int gx = Mathf.FloorToInt(mx), gy = Mathf.FloorToInt(my);
        float d1 = 9f, d2 = 9f, ndx = 0f, ndy = 0f;
        int bx = 0, by = 0;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = gx + ox, iy = gy + oy;
                float dx = mx - (ix + 0.15f + 0.7f * Hash(ix, iy, s.seed + 40)), dy = my - (iy + 0.15f + 0.7f * Hash(ix, iy, s.seed + 41));
                float d = dx * dx + dy * dy;
                if (d < d1) { d2 = d1; d1 = d; ndx = dx; ndy = dy; bx = ix; by = iy; }
                else if (d < d2) d2 = d;
            }
        d1 = Mathf.Sqrt(d1); d2 = Mathf.Sqrt(d2);
        float dome = Mathf.Clamp01(1f - d1 / 0.8f);
        float lit = Mathf.Clamp01(0.55f + (-ndx + ndy) * 0.55f);
        float seam = Sstep(0f, 0.3f, d2 - d1);
        // The colour drifts smoothly over the glade (patches of deep, fresh and olive moss); each cushion only nudges it a little.
        Color fresh = new Color(0.5f, 0.68f, 0.2f), olive = new Color(0.42f, 0.46f, 0.16f);
        float hue = Fbm(u, v, Per(s, 4f), 3, s.seed + 44), dry = Sstep(0.6f, 0.8f, Fbm(u, v, Per(s, 7f), 3, s.seed + 45));
        Color moss = Color.Lerp(Color.Lerp(s.colorA, s.colorB, 0.35f + 0.65f * hue), fresh, Sstep(0.55f, 0.8f, hue) * 0.6f);
        moss = Color.Lerp(moss, olive, dry * 0.55f);
        moss *= 0.94f + 0.12f * Hash(bx, by, s.seed + 42);
        moss *= Mathf.Lerp(0.82f, 1.1f, lit * 0.5f + Mathf.Sqrt(dome) * 0.5f);
        moss = Color.Lerp(moss * 0.88f, moss, seam);
        // Fuzz: tiny tufts and a per-texel grain.
        moss *= 0.82f + 0.3f * Fbm(u, v, Per(s, 0.12f), 2, s.seed + 43) + 0.12f * (Hash(x, y, s.seed + 2) - 0.5f);
        Color c = moss;

        // Fallen leaves here and there on the moss (small ellipses with a midrib, brown / rust / old yellow).
        const float leafCell = 1.3f;
        int lx0 = Mathf.FloorToInt(wx / leafCell), ly0 = Mathf.FloorToInt(wy / leafCell);
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = lx0 + ox, iy = ly0 + oy;
                if (Hash(ix, iy, s.seed + 50) < 0.86f) continue;
                float lcx = (ix + Hash(ix, iy, s.seed + 51)) * leafCell, lcy = (iy + Hash(ix, iy, s.seed + 52)) * leafCell;
                float ang = Hash(ix, iy, s.seed + 53) * Mathf.PI * 2f, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                float px = wx - lcx, py = wy - lcy;
                float along = (px * ca + py * sa) / 0.3f, across = (-px * sa + py * ca) / 0.13f;
                float e = along * along + across * across;
                if (e >= 1f) continue;
                float lp = Hash(ix, iy, s.seed + 54);
                Color leaf = lp < 0.55f ? new Color(0.4f, 0.29f, 0.15f) : lp < 0.85f ? new Color(0.52f, 0.31f, 0.14f) : new Color(0.62f, 0.54f, 0.26f);
                leaf *= 0.85f + 0.25f * Mathf.Clamp01(0.5f - across * 0.5f);
                if (Mathf.Abs(across) < 0.12f) leaf *= 0.75f;   // midrib
                c = Color.Lerp(c, leaf, 1f - Sstep(0.8f, 1f, e));
            }

        // Shadows of the stones and ferns, cast down to the right.
        float shadow = Mathf.Max(GladeStone(s, wx - GladeShadowX, wy - GladeShadowY, u, v, out _, out _, out _), GladeFern(s, wx - GladeShadowX, wy - GladeShadowY, out _, out _) * 0.8f);
        c *= 1f - 0.42f * shadow;

        // Stones: rounded, lit like a ball, dark at the foot, moss growing over their top and pale lichen spots.
        float stone = GladeStone(s, wx, wy, u, v, out float snx, out float sny, out float snz);
        if (stone > 0f)
        {
            float lambert = Mathf.Clamp01(-snx * 0.5f + sny * 0.5f + snz * 0.72f);
            Color rock = s.colorC * (0.42f + 0.8f * lambert) * (0.92f + 0.16f * Hash(x / 2, y / 2, s.seed + 14));
            float lichen = Fbm(u, v, Per(s, 0.25f), 2, s.seed + 15);
            rock = Color.Lerp(rock, new Color(0.62f, 0.66f, 0.52f) * (0.6f + 0.5f * lambert), Sstep(0.68f, 0.75f, lichen) * 0.6f);   // pale lichen patches
            float capNoise = Fbm(u, v, Per(s, 0.5f), 2, s.seed + 16) - 0.5f;
            float cap = Sstep(0.45f, 0.55f, (-snx + sny) * 0.45f + snz * 0.2f + capNoise * 0.9f);   // moss creeps up the lit (top-left) side in patches
            Color mossCap = moss * (0.7f + 0.45f * lambert);   // the same moss as the ground around it
            Color rc = Color.Lerp(rock, mossCap, cap);
            rc *= Mathf.Lerp(0.6f, 1f, Sstep(0.05f, 0.3f, snz));   // darker where the stone curves down to the ground
            c = Color.Lerp(c, rc, stone);
        }

        // Ferns: arching fronds of paired leaflets that shrink towards the tip.
        float fern = GladeFern(s, wx, wy, out float fernAlong, out float fernTone);
        if (fern > 0f)
        {
            Color fc = Color.Lerp(new Color(0.2f, 0.46f, 0.12f), new Color(0.48f, 0.76f, 0.24f), Mathf.Clamp01(fernAlong * 0.8f + fernTone * 0.4f));
            c = Color.Lerp(c, fc, fern);
        }

        // Twigs.
        float tw = BladeLayer(wx + 50f, wy, 0.4f, 1, 1.6f, 0.5f, 1.1f, 0.05f, 3.1f, 0f, s.seed + 23, out _, out float twt);
        if (tw > 0.3f && Hash(Mathf.FloorToInt(wx / 1.6f), Mathf.FloorToInt(wy / 1.6f), s.seed + 24) > 0.8f) c = Color.Lerp(c, new Color(0.36f, 0.28f, 0.18f) * (0.8f + 0.3f * twt), tw);

        // Dappled light through the canopy: soft warm sun patches over everything.
        float sun = Sstep(0.5f, 0.72f, Fbm(u, v, Per(s, 6f), 3, s.seed + 5));
        c = Color.Lerp(c * 0.9f, c * 1.18f + new Color(0.05f, 0.045f, 0f), sun);
        return Fin(c);
    }

    /// <summary>Mossy Glade stones: coverage (0..1, soft rim) at a world point and the ball-shaped surface normal there.</summary>
    private static float GladeStone(FloorStyle s, float wx, float wy, float u, float v, out float nx, out float ny, out float nz)
    {
        nx = ny = 0f; nz = 1f;
        const float cell = 5.5f;
        int sx = Mathf.FloorToInt(wx / cell), sy = Mathf.FloorToInt(wy / cell);
        float best = 0f;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = sx + ox, iy = sy + oy;
                if (Hash(ix, iy, s.seed + 9) < 0.62f) continue;
                float cx = (ix + 0.2f + 0.6f * Hash(ix, iy, s.seed + 10)) * cell, cy = (iy + 0.2f + 0.6f * Hash(ix, iy, s.seed + 11)) * cell;
                float r = 0.7f + 0.8f * Hash(ix, iy, s.seed + 12);
                float stretch = 0.8f + 0.4f * Hash(ix, iy, s.seed + 13);
                float dx = (wx - cx) / (r * stretch), dy = (wy - cy) / (r / stretch);
                float ang = Mathf.Atan2(dy, dx);
                float wob = 1f + 0.08f * Mathf.Sin(ang * 3f + ix) + 0.05f * Mathf.Sin(ang * 5f + iy);
                float d = Mathf.Sqrt(dx * dx + dy * dy) / wob;
                if (d >= 1f) continue;
                float cover = 1f - Sstep(0.93f, 1f, d);
                if (cover <= best) continue;
                best = cover;
                float k = d > 1e-4f ? Mathf.Min(d, 0.999f) / d : 0f;
                nx = dx * k; ny = dy * k;
                nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
            }
        return best;
    }

    /// <summary>Mossy Glade ferns: coverage (0..1) at a world point, how far along its frond (0 base .. 1 tip) and a per-frond tone.</summary>
    private static float GladeFern(FloorStyle s, float wx, float wy, out float along01, out float tone)
    {
        along01 = 0f; tone = 0f;
        const float cell = 4.2f;
        int fx0 = Mathf.FloorToInt(wx / cell), fy0 = Mathf.FloorToInt(wy / cell);
        float best = 0f;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = fx0 + ox, iy = fy0 + oy;
                if (Hash(ix, iy, s.seed + 17) < 0.5f) continue;
                float bx = (ix + 0.2f + 0.6f * Hash(ix, iy, s.seed + 18)) * cell, by = (iy + 0.2f + 0.6f * Hash(ix, iy, s.seed + 19)) * cell;
                float px = wx - bx, py = wy - by;
                if (px * px + py * py > 2.4f * 2.4f) continue;
                int fronds = 3 + Mathf.FloorToInt(Hash(ix, iy, s.seed + 20) * 3f);
                float turn = Hash(ix, iy, s.seed + 21) * Mathf.PI * 2f;
                for (int f = 0; f < fronds; f++)
                {
                    float a = turn + (f + (Hash(ix + f, iy, s.seed + 22) - 0.5f) * 0.5f) / fronds * Mathf.PI * 2f;
                    float len = 1.3f + 0.8f * Hash(ix, iy + f, s.seed + 23);
                    float bend = (Hash(ix - f, iy, s.seed + 24) > 0.5f ? 1f : -1f) * (0.25f + 0.25f * Hash(ix, iy - f, s.seed + 26));   // the frond arches to one side
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    float along = px * ca + py * sa;
                    float t = along / len;
                    if (t < 0.06f || t > 1f) continue;
                    float side = (-px * sa + py * ca) - bend * len * t * t;
                    float half = 0.32f * Mathf.Pow(1f - t, 0.8f) + 0.03f;   // leaflet length (half the frond's width)
                    float aside = Mathf.Abs(side);
                    float cover = 0f;
                    if (aside < 0.03f) cover = 1f;                            // the midrib
                    else if (aside < half)
                    {
                        // Paired leaflets slanting towards the tip.
                        const float spacing = 0.16f;
                        float q = (along - aside * 0.6f) / spacing;
                        float lf = Mathf.Abs(PixelNoise.Frac(q) - 0.5f) * spacing;
                        float w = spacing * 0.42f * Mathf.Sqrt(Mathf.Max(0f, 1f - (aside / half) * (aside / half)));
                        cover = 1f - Sstep(w * 0.6f, w, lf);
                    }
                    if (cover > best)
                    {
                        best = cover;
                        along01 = t;
                        tone = Hash(ix + f, iy - f, s.seed + 25);
                    }
                }
            }
        return best;
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

    /// <summary>The Crumbling Floor's icon / title picture (the game shows real 3D tiles instead): a grid of stone tiles with a few missing and glowing cracks.</summary>
    private static Color ShadeCrumblePicture(FloorStyle s, float u, float v)
    {
        const int n = 6;
        int cx = Mathf.FloorToInt(u * n), cy = Mathf.FloorToInt(v * n);
        float fx = u * n - cx, fy = v * n - cy;
        float h = PixelNoise.Hash(cx, cy, 77);
        if (h > 0.78f) return new Color(0.02f, 0.02f, 0.03f, 1f);                     // a missing tile: the void
        Color c = Color.Lerp(s.colorA, s.colorB, h * 1.2f);
        float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
        if (edge < 0.05f) c *= 0.45f;                                                  // the gaps between tiles
        float crack = Mathf.Abs(fx - fy * (0.6f + h) - 0.2f);
        if (h > 0.5f && crack < 0.025f) c = Color.Lerp(c, s.colorC, 0.8f);             // a glowing crack
        return c;
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
    [Tooltip("The beams are never thinner on screen than this many pixels (the floor lies far under the cube, so the world widths above can shrink to a pixel or less).")]
    [SerializeField] private float coreMinPixels = 3f;
    [Tooltip("The beams' glow is never thinner on screen than this many pixels.")]
    [SerializeField] private float glowMinPixels = 18f;
    [Tooltip("How many emitters shoot beams at the mouse on the Laser Tracker floor.")]
    [SerializeField] private int trackerCount = 10;
    [Tooltip("How fast the converging point catches up with the mouse (higher = snappier).")]
    [SerializeField] private float trackerFollow = 10f;
    [Tooltip("How far out the Laser Tracker's emitters circle, as a multiple of the visible floor's reach (higher = longer beams, reaching past the screen's edges).")]
    [SerializeField] private float trackerReach = 2f;

    [Header("Caustics")]
    [Tooltip("Size of one cell of the moving caustic light net (world units).")]
    [SerializeField] private float causticScale = 1.6f;
    [Tooltip("How fast the caustic light moves.")]
    [SerializeField] private float causticSpeed = 0.6f;
    [Tooltip("How bright the caustic light is.")]
    [SerializeField] private float causticStrength = 0.35f;
    [Tooltip("Colour of the caustic light.")]
    [SerializeField] private Color causticColor = new Color(0.8f, 1f, 1f, 1f);

    [Header("Ant")]
    [Tooltip("Length of the ant (world units).")]
    [SerializeField] private float antLength = 0.42f;
    [Tooltip("How fast the ant walks (world units per second).")]
    [SerializeField] private float antSpeed = 1.6f;
    [Tooltip("How close to the mouse the ant stops (world units).")]
    [SerializeField] private float antStopDistance = 0.25f;

    [Header("Crumbling Floor")]
    [Tooltip("Size of one stone tile (world units).")]
    [SerializeField] private float crumbleTileSize = 0.9f;
    [Tooltip("Seconds for the whole floor to crumble away, tile by tile.")]
    [SerializeField] private float crumbleSeconds = 45f;
    [Tooltip("Seconds of nothing once the last tile has fallen, before the floor rebuilds.")]
    [SerializeField] private float crumbleEmptySeconds = 3f;
    [Tooltip("Seconds the rebuild takes (a ripple from the cube outwards).")]
    [SerializeField] private float crumbleRebuildSeconds = 4f;
    [Tooltip("Seconds the rebuilt floor stays whole before it starts crumbling again.")]
    [SerializeField] private float crumbleWholeSeconds = 6f;

    private PixelFloor.FloorStyle style;
    private GameObject root;
    private float coreNow, glowNow;   // this frame's beam widths (world units), at least the minimum pixels
    private Material lineMat;
    private readonly List<Vector4> beamState = new List<Vector4>();   // per beam: centre offset x / z (0..1 of the reach), angle, speed
    private bool trackerRing;           // the Laser Tracker's pulsing ring where the beams meet
    // The beams are ONE flat mesh rebuilt every frame (quads lying on the floor, vertex colours), like the edge-fade wireframe.
    private Mesh beamMesh;
    private readonly List<Vector3> beamVerts = new List<Vector3>();
    private readonly List<Color32> beamColors = new List<Color32>();
    private readonly List<int> beamTris = new List<int>();
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
        else if (s.overlay == PixelFloor.FloorOverlay.Caustics) BuildCaustics();
        else if (s.overlay == PixelFloor.FloorOverlay.Crumble) { /* built on the first frame, when the floor's size is known (BuildCrumble) */ }
        else BuildBeams(s.overlay == PixelFloor.FloorOverlay.Lasers ? Mathf.Max(1, laserCount) : Mathf.Max(1, trackerCount), s.overlay == PixelFloor.FloorOverlay.LaserTracker);
    }

    private void Clear()
    {
        if (root != null) Destroy(root);
        if (causticQuad != null) Destroy(causticQuad.GetComponent<MeshFilter>().sharedMesh);
        root = null; causticQuad = null;
        beamState.Clear(); antLegs.Clear(); tiles.Clear(); crumbleBuiltReach = 0f;
        if (beamMesh != null) { Destroy(beamMesh); beamMesh = null; }
        trackerRing = false; ant = null; trackInit = false;
    }

    private void OnDestroy() { Clear(); if (lineMat != null) Destroy(lineMat); if (causticMat != null) Destroy(causticMat); }

    // ---- caustics: one flat quad over the floor drawn with the Caustics shader ----

    private static Shader causticShader;
    private static bool causticShaderMissing;
    private Material causticMat;
    private Transform causticQuad;
    private float causticClock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { causticShader = null; causticShaderMissing = false; }

    private void BuildCaustics()
    {
        if (causticShader == null && !causticShaderMissing)
        {
            causticShader = Resources.Load<Shader>("PixelCaustics");
            if (causticShader == null) { causticShaderMissing = true; Debug.LogWarning("PixelFloorOverlay: Resources/PixelCaustics.shader was not found; the floor has no moving caustic light."); }
        }
        if (causticShader == null || !causticShader.isSupported) return;
        if (causticMat == null) causticMat = new Material(causticShader) { name = "Floor Caustics" };
        GameObject g = new GameObject("Caustics", typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(root.transform, false);
        Mesh quad = new Mesh { name = "Floor Caustics" };
        quad.vertices = new[] { new Vector3(-1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, -1f) };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quad.RecalculateBounds();
        g.GetComponent<MeshFilter>().sharedMesh = quad;
        MeshRenderer mr = g.GetComponent<MeshRenderer>();
        mr.sharedMaterial = causticMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        causticQuad = g.transform;
    }

    private void UpdateCaustics(PixelFloor floor, Vector3 centre, float reach, float alpha)
    {
        if (causticQuad == null || causticMat == null) return;
        if (!PixelTimeStop.IsStopped) causticClock += Time.unscaledDeltaTime;
        if (!floor.TryGetFadeRadii(out float r0, out float r1)) { r0 = reach; r1 = reach * 1.3f; }
        causticQuad.position = new Vector3(centre.x, centre.y + 0.015f, centre.z);
        causticQuad.localScale = Vector3.one * Mathf.Max(1f, r1);
        causticMat.SetColor("_Color", causticColor);
        causticMat.SetFloat("_Scale", causticScale);
        causticMat.SetFloat("_Speed", causticSpeed);
        causticMat.SetFloat("_Strength", causticStrength);
        causticMat.SetFloat("_Clock", causticClock);
        causticMat.SetVector("_Center", new Vector4(centre.x, centre.z, 0f, 0f));
        causticMat.SetFloat("_FadeStart", r0);
        causticMat.SetFloat("_FadeEnd", r1);
        causticMat.SetFloat("_Fade", alpha);
    }

    // ---- lasers ----

    private void BuildBeams(int count, bool tracker)
    {
        if (lineMat == null)
        {
            Shader sh = PixelShaders.SpriteDefault();
            lineMat = sh != null ? new Material(sh) { name = "Floor Lasers", renderQueue = 3100 } : null;
        }
        GameObject g = new GameObject("Beams", typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(root.transform, false);
        beamMesh = new Mesh { name = "Floor Lasers" };
        beamMesh.MarkDynamic();
        g.GetComponent<MeshFilter>().sharedMesh = beamMesh;
        MeshRenderer mr = g.GetComponent<MeshRenderer>();
        mr.sharedMaterial = lineMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        for (int i = 0; i < count; i++)
        {
            float a = tracker ? i / (float)count * Mathf.PI * 2f : Random.value * Mathf.PI;
            beamState.Add(new Vector4(Random.Range(-0.7f, 0.7f), Random.Range(-0.7f, 0.7f), a, Random.Range(-0.25f, 0.25f)));
        }
        trackerRing = tracker;
    }

    private static readonly float[] RibbonAlong = { 0f, 0.25f, 0.75f, 1f };   // where along a ribbon its cross-sections sit (the ends fade over the outer quarters)

    private void BeginBeams() { beamVerts.Clear(); beamColors.Clear(); beamTris.Clear(); }

    private void EndBeams()
    {
        if (beamMesh == null) return;
        beamMesh.Clear();
        beamMesh.SetVertices(beamVerts);
        beamMesh.SetColors(beamColors);
        beamMesh.SetTriangles(beamTris, 0);
        beamMesh.RecalculateBounds();
    }

    /// <summary>
    /// One flat ribbon from a to b on the floor, 'width' across. The alpha fades to alpha * endFade at both ends (over the outer quarters);
    /// 'soft' = the alpha also fades to 0 at the sides (a glow), else the ribbon is solid across (a core).
    /// </summary>
    private void Ribbon(Vector3 a, Vector3 b, float width, Color c, float alpha, float endFade, bool soft)
    {
        Vector3 dir = b - a;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-8f || alpha <= 0.001f) return;
        Vector3 side = Vector3.Cross(Vector3.up, dir.normalized) * (width * 0.5f);
        int across = soft ? 3 : 2;
        int start = beamVerts.Count;
        float[] along = RibbonAlong;
        for (int k = 0; k < along.Length; k++)
        {
            Vector3 p = Vector3.Lerp(a, b, along[k]);
            float al = alpha * (k == 0 || k == along.Length - 1 ? endFade : 1f);
            Color32 edge = new Color(c.r, c.g, c.b, soft ? 0f : Mathf.Clamp01(al));
            Color32 mid = new Color(c.r, c.g, c.b, Mathf.Clamp01(al));
            beamVerts.Add(p - side); beamColors.Add(edge);
            if (soft) { beamVerts.Add(p); beamColors.Add(mid); }
            beamVerts.Add(p + side); beamColors.Add(edge);
        }
        for (int k = 0; k < along.Length - 1; k++)
            for (int j = 0; j < across - 1; j++)
            {
                int i0 = start + k * across + j, i1 = i0 + 1, i2 = i0 + across, i3 = i2 + 1;
                beamTris.Add(i0); beamTris.Add(i2); beamTris.Add(i1);
                beamTris.Add(i1); beamTris.Add(i2); beamTris.Add(i3);
            }
    }

    private Color BeamColor(int i, float time)
    {
        Color[] pal = { style.colorA, style.colorB, style.colorC };
        Color a = pal[i % 3], b = pal[(i + 1) % 3];
        return Color.Lerp(a, b, 0.5f + 0.5f * Mathf.Sin(time * 0.6f + i * 1.7f));
    }

    private void UpdateLasers(Vector3 centre, float reach, float alpha, float time)
    {
        float y = centre.y + 0.02f;
        BeginBeams();
        for (int i = 0; i < beamState.Count; i++)
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
            Ribbon(a, b, glowNow * (0.8f + 0.4f * pulse), c, alpha * pulse * 0.45f, 0f, true);
            Ribbon(a, b, coreNow, Color.Lerp(c, Color.white, 0.45f), alpha * pulse, 0f, false);
        }
        EndBeams();
    }

    private void UpdateTracker(Vector3 centre, float reach, float alpha, float time, Vector3 target)
    {
        float y = centre.y + 0.02f;
        if (!trackInit) { trackPoint = target; trackInit = true; }
        trackPoint = Vector3.Lerp(trackPoint, target, 1f - Mathf.Exp(-trackerFollow * Time.unscaledDeltaTime));
        trackPoint.y = y;
        float emitR = reach * Mathf.Max(0.3f, trackerReach);
        BeginBeams();
        for (int i = 0; i < beamState.Count; i++)
        {
            float ang = beamState[i].z + time * 0.12f;   // the emitters slowly circle the cube
            Vector3 from = centre + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * emitR;
            from.y = y;
            float flicker = 0.75f + 0.25f * Mathf.Sin(time * 9f + i * 2.3f);
            Color c = BeamColor(i, time);
            Ribbon(from, trackPoint, glowNow, c, alpha * flicker * 0.5f, 0.2f, true);
            Ribbon(from, trackPoint, coreNow, Color.Lerp(c, Color.white, 0.5f), alpha * flicker, 0.35f, false);
        }
        if (trackerRing)
        {
            float r = Mathf.Max(0.25f + 0.08f * Mathf.Sin(time * 6f), coreNow * 3f);
            const int segments = 32;
            Color rc = Color.Lerp(style.colorA, Color.white, 0.6f);
            for (int k = 0; k < segments; k++)
            {
                float a0 = k / (float)segments * Mathf.PI * 2f, a1 = (k + 1) / (float)segments * Mathf.PI * 2f;
                Ribbon(trackPoint + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, trackPoint + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r, coreNow * 1.4f, rc, alpha, 1f, false);
            }
        }
        EndBeams();
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
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
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

    // ---- the crumbling floor ----
    // One cycle: WHOLE (crumbleWholeSeconds) -> CRUMBLING (crumbleSeconds: each tile has its own moment, the outer ring first with some randomness; it shakes
    // for a moment, then drops, tumbling and shrinking) -> EMPTY (crumbleEmptySeconds) -> REBUILD (crumbleRebuildSeconds: the tiles grow back up out of the
    // void in a ripple from the cube outwards, with a warm glow that fades) -> whole again. All driven by the clock, so it costs only moving a few hundred tiles.

    private class Tile { public Transform t; public Vector3 home; public float dropAt, rebuildAt, spinX, spinZ, drift; public Renderer r; public int mat; }
    private readonly List<Tile> tiles = new List<Tile>();
    private float crumbleBuiltReach;
    private Material[] tileMats;
    private Material glowMat;

    private void BuildCrumble(Vector3 centre, float reach)
    {
        foreach (Tile old in tiles) if (old.t != null) Destroy(old.t.gameObject);
        tiles.Clear();
        crumbleBuiltReach = reach;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (tileMats == null && clicker != null)
        {
            tileMats = new Material[3];
            for (int i = 0; i < 3; i++) tileMats[i] = clicker.CreateVisualMaterial(Color.Lerp(style.colorA, style.colorB, i / 2f), false);
            glowMat = clicker.CreateVisualMaterial(style.colorC, false);
        }
        float size = Mathf.Max(0.2f, crumbleTileSize), thick = size * 0.22f;
        int n = Mathf.CeilToInt(reach / size);
        for (int ix = -n; ix <= n; ix++)
            for (int iz = -n; iz <= n; iz++)
            {
                Vector3 off = new Vector3((ix + 0.5f) * size, 0f, (iz + 0.5f) * size);
                float d = off.magnitude;
                if (d > reach) continue;
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = "Tile";
                Collider col = g.GetComponent<Collider>();
                if (col != null) Destroy(col);
                g.transform.SetParent(root.transform, false);
                g.transform.localScale = new Vector3(size * 0.96f, thick, size * 0.96f);
                Renderer r = g.GetComponent<Renderer>();
                int matIndex = Mathf.Abs(ix * 7 + iz * 13) % 3;
                if (tileMats != null) r.sharedMaterial = tileMats[matIndex];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float edge = d / reach;   // 0 at the cube .. 1 at the rim
                tiles.Add(new Tile
                {
                    t = g.transform, r = r, mat = matIndex, home = off + Vector3.down * thick * 0.5f,
                    dropAt = Mathf.Clamp01((1f - edge) * 0.75f + Random.value * 0.25f),   // the rim goes first, with a random shuffle
                    rebuildAt = Mathf.Clamp01(edge * 0.85f + Random.value * 0.15f),         // the rebuild ripples outwards from the cube
                    spinX = Random.Range(-220f, 220f), spinZ = Random.Range(-220f, 220f), drift = Random.Range(-0.4f, 0.4f),
                });
            }
    }

    private void UpdateCrumble(Vector3 centre, float reach, float time)
    {
        if (tiles.Count == 0 || Mathf.Abs(reach - crumbleBuiltReach) > crumbleTileSize) BuildCrumble(centre, reach);
        float whole = Mathf.Max(0.5f, crumbleWholeSeconds), fall = Mathf.Max(5f, crumbleSeconds), empty = Mathf.Max(0f, crumbleEmptySeconds), rebuild = Mathf.Max(0.5f, crumbleRebuildSeconds);
        float cycle = whole + fall + empty + rebuild;
        float c = Mathf.Repeat(time, cycle);
        const float shakeTime = 0.7f, dropTime = 1.6f;
        float size = Mathf.Max(0.2f, crumbleTileSize);
        foreach (Tile tile in tiles)
        {
            if (tile.t == null) continue;
            Vector3 pos = centre + tile.home;
            Quaternion rot = Quaternion.identity;
            float scale = 1f;
            bool glowing = false;
            if (c >= whole && c < whole + fall + empty)
            {
                // crumbling: this tile's moment comes at dropAt through the crumble time
                float since = (c - whole) - tile.dropAt * (fall - shakeTime - dropTime);
                if (since > 0f && since < shakeTime)
                {
                    float k = since / shakeTime;   // shaking harder as it comes loose, a crack glow showing
                    pos += new Vector3(Mathf.Sin(time * 61f + tile.drift * 30f), 0f, Mathf.Cos(time * 53f + tile.drift * 20f)) * 0.03f * size * k;
                    rot = Quaternion.Euler(Mathf.Sin(time * 47f) * 4f * k, 0f, Mathf.Cos(time * 43f) * 4f * k);
                    glowing = k > 0.5f;
                }
                else if (since >= shakeTime)
                {
                    float f = since - shakeTime;
                    if (f >= dropTime) { if (tile.t.gameObject.activeSelf) tile.t.gameObject.SetActive(false); continue; }
                    pos += new Vector3(tile.drift * f, -4.5f * f * f, tile.drift * 0.6f * f);       // falls away into the void, tumbling
                    rot = Quaternion.Euler(tile.spinX * f, 0f, tile.spinZ * f);
                    scale = 1f - f / dropTime * 0.7f;
                }
            }
            else if (c >= whole + fall + empty)
            {
                // rebuilding: grows back up out of the void, glowing warm for a moment
                float since = (c - whole - fall - empty) - tile.rebuildAt * (rebuild - 0.8f);
                if (since <= 0f) { if (tile.t.gameObject.activeSelf) tile.t.gameObject.SetActive(false); continue; }
                float k = Mathf.Clamp01(since / 0.8f);
                float ease = 1f - (1f - k) * (1f - k);
                pos += Vector3.down * (1f - ease) * size * 1.5f;
                scale = Mathf.Lerp(0.2f, 1f, ease);
                glowing = k < 0.9f;
            }
            if (!tile.t.gameObject.activeSelf) tile.t.gameObject.SetActive(true);
            tile.t.SetPositionAndRotation(pos, rot);
            float thick = size * 0.22f;
            tile.t.localScale = new Vector3(size * 0.96f * scale, thick * scale, size * 0.96f * scale);
            if (tileMats != null && glowMat != null)
            {
                Material want = glowing ? glowMat : tileMats[tile.mat];
                if (tile.r.sharedMaterial != want) tile.r.sharedMaterial = want;
            }
        }
    }

    // ---- every frame ----

    private bool TryMousePoint(float floorY, out Vector3 point)
    {
        point = default;
        Ray ray;
        if (!PixelFirstPerson.TryGetViewRay(out ray))
        {
            PixelClicker clicker = PixelFind.First<PixelClicker>();
            Camera cam = PixelClicker.ViewCameraOf(clicker);
            if (cam == null) return false;
            ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        }
        Plane plane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        if (!plane.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    /// <summary>World units one screen pixel covers at a point (0 when there is no camera).</summary>
    private static float WorldPerPixel(Vector3 point)
    {
        Camera cam = PixelClicker.ViewCameraOf(PixelFind.Cached<PixelClicker>());
        if (cam == null || cam.pixelHeight <= 0) return 0f;
        if (cam.orthographic) return cam.orthographicSize * 2f / cam.pixelHeight;
        float depth = Mathf.Max(0.1f, Vector3.Dot(point - cam.transform.position, cam.transform.forward));
        return 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight;
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
        float perPixel = WorldPerPixel(centre);
        coreNow = Mathf.Max(coreWidth, perPixel * coreMinPixels);
        glowNow = Mathf.Max(glowWidth, perPixel * glowMinPixels);
        bool hasMouse = TryMousePoint(centre.y, out Vector3 mouse) && (mouse - centre).sqrMagnitude < reach * reach * 1.5f;
        switch (style.overlay)
        {
            case PixelFloor.FloorOverlay.Lasers: UpdateLasers(centre, reach, alpha, time); break;
            case PixelFloor.FloorOverlay.LaserTracker: UpdateTracker(centre, reach, alpha, time, hasMouse ? mouse : centre); break;
            case PixelFloor.FloorOverlay.Crumble: UpdateCrumble(centre, reach, time); break;
            case PixelFloor.FloorOverlay.Caustics: UpdateCaustics(floor, centre, reach, alpha); break;
            case PixelFloor.FloorOverlay.Ant:
                if (ant != null) ant.localScale = Vector3.one * Mathf.Clamp01(alpha);   // (fades by shrinking with the floor)
                UpdateAnt(centre, reach, hasMouse, mouse, PixelTimeStop.IsStopped ? 0f : Time.unscaledDeltaTime);
                break;
        }
    }
}
