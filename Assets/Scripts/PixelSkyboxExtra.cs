using System.Collections.Generic;
using UnityEngine;

/// <summary>More skies (partial class PixelSkybox): weather, space, cube world. Drawn by <c>ShadeExtra</c> from <c>Shade</c>'s fall-through.</summary>
public partial class PixelSkybox
{
    /// <summary>Offsets in degrees of 'dir' from 'sun' along the sky's right / up directions (only meaningful for directions near the sun).</summary>
    private static void Tangent(Vector3 sun, Vector3 dir, out float u, out float v)
    {
        Vector3 right = Vector3.Cross(Vector3.up, sun);
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(sun, right);
        u = Vector3.Dot(dir, right) * Mathf.Rad2Deg;
        v = Vector3.Dot(dir, up) * Mathf.Rad2Deg;
    }

    private static Color ShadeExtra(SkyStyle s, Vector3 dir, Vector3 sun, float d, int x, int y, float sinLat, float res)
    {
        switch (s.pattern)
        {
            // ---------------- weather ----------------
            case SkyPattern.Rain:
            {
                Color c = Gradient(s, dir.y, 0.35f);
                c = Clouds(c, s, dir, Vector3.up, new Vector2(0.9f, 1.1f), 0.5f);
                // Curtains of falling rain: thin vertical streaks, strongest near the horizon.
                float lon = Mathf.Atan2(dir.z, dir.x);
                float streak = Fbm2(lon * 90f, dir.y * 3f, 2, s.seed + 7);
                float curtain = Mathf.Clamp01((streak - 0.52f) * 3f) * Mathf.Exp(-dir.y * 2.2f);
                return Color.Lerp(c, s.accentA, curtain * 0.5f);
            }

            case SkyPattern.Snow:
            {
                Color c = Gradient(s, dir.y, 0.4f);
                c = Clouds(c, s, dir, sun, Vector2.one, 0.4f);
                float chance = 0.03f * sinLat / Mathf.Max(0.25f, res * res);
                if (PixelNoise.Hash(x, y, s.seed + 300) < chance)
                    c = Color.Lerp(c, Color.white, 0.5f + 0.5f * PixelNoise.Hash(x, y, s.seed + 301));
                return c;
            }

            case SkyPattern.Fog:
            {
                Color c = Gradient(s, dir.y, 0.3f);
                float n = Fbm3(dir * 2f, 4, s.seed);
                c = Color.Lerp(c, s.cloudColor, Mathf.Clamp01(n * 1.2f - 0.2f) * 0.5f);
                c += s.sunColor * Mathf.Pow(Mathf.Max(0f, d), 12f) * 0.25f;
                return c;
            }

            case SkyPattern.Sandstorm:
            {
                Color c = Gradient(s, dir.y, 0.3f);
                float lon = Mathf.Atan2(dir.z, dir.x);
                float streak = Fbm2(lon * 5f, dir.y * 40f, 3, s.seed);   // long horizontal streaks of blown sand
                float swirl = Fbm3(dir * 3f, 4, s.seed + 3);
                c = Color.Lerp(c, s.cloudColor, Mathf.Clamp01(swirl * 1.3f - 0.25f) * 0.6f);
                c = Color.Lerp(c, s.cloudShadowColor, Mathf.Clamp01((streak - 0.45f) * 2f) * 0.4f);
                c += s.sunColor * Mathf.Pow(Mathf.Max(0f, d), 30f) * 0.35f;   // a hazy sun
                return c;
            }

            case SkyPattern.Rainbow:
            {
                Color c = Gradient(s, dir.y, 0.55f);
                c += s.sunColor * (Mathf.Pow(Mathf.Max(0f, d), 24f) * 0.35f + Mathf.Pow(Mathf.Max(0f, d), 4f) * 0.08f);
                c = Disc(c, s.sunColor * 1.4f, d, s.sunSize);
                c = Clouds(c, s, dir, sun, Vector2.one, 0.35f);
                // The bow is a ring round the point opposite the sun: red outside, violet inside.
                Vector3 anti = Direction(-s.sunElevation, s.sunAzimuth + 180f);
                float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir, anti), -1f, 1f)) * Mathf.Rad2Deg;
                float t = (ang - 38f) / 5f;
                if (t > 0f && t < 1f)
                {
                    Color bow = Color.HSVToRGB(Mathf.Lerp(0.78f, 0f, t), 0.85f, 1f);
                    c = Color.Lerp(c, bow, Mathf.Sin(t * Mathf.PI) * 0.55f * Mathf.Clamp01(dir.y * 8f + 0.3f));
                }
                return c;
            }

            case SkyPattern.RingedPlanet:
            case SkyPattern.SpiralGalaxy:
            case SkyPattern.BlackHole:
            case SkyPattern.TwinSuns:
                return ShadeExtraSpace(s, dir, sun, d, x, y, sinLat, res);

            case SkyPattern.CubeDay:
            case SkyPattern.CubeNight:
            case SkyPattern.CubeEmber:
            case SkyPattern.CubeVoid:
            case SkyPattern.EyeVoid:
            case SkyPattern.TestGrid:
            case SkyPattern.Starfield:
            case SkyPattern.Underwater:
                return ShadeExtraCubeAndMore(s, dir, sun, d, x, y, sinLat, res);
        }
        return Gradient(s, dir.y, 0.5f);
    }

    /// <summary>Part of ShadeExtra (one group of skies): RingedPlanet to TwinSuns (4 patterns).</summary>
    private static Color ShadeExtraSpace(SkyStyle s, Vector3 dir, Vector3 sun, float d, int x, int y, float sinLat, float res)
    {
        switch (s.pattern)
        {
            // ---------------- space ----------------
            case SkyPattern.RingedPlanet:
            {
                Color c = Stars(Gradient(s, dir.y, 0.5f), s, x, y, sinLat, res, 1f);
                if (d < 0.5f) return c;
                Tangent(sun, dir, out float u, out float v);
                float r = s.sunSize * 0.5f;
                u /= r; v /= r;
                float tilt = 18f * Mathf.Deg2Rad, cs = Mathf.Cos(tilt), sn = Mathf.Sin(tilt);
                float ru = u * cs - v * sn, rv = (u * sn + v * cs) / 0.3f;
                float rr = Mathf.Sqrt(ru * ru + rv * rv);
                bool ring = rr > 1.35f && rr < 2.3f && !(rr > 1.82f && rr < 1.92f);
                bool inPlanet = u * u + v * v < 1f;
                Color ringColor = Color.Lerp(s.cloudShadowColor, s.cloudColor, 0.6f + 0.4f * Mathf.Sin(rr * 40f));
                if (ring && rv > 0f && !inPlanet) c = Color.Lerp(c, ringColor * 0.7f, 0.85f);   // the back of the ring
                if (inPlanet)
                {
                    float nz = Mathf.Sqrt(1f - u * u - v * v);
                    float band = Mathf.Sin(v * 7f + Fbm3(dir * 30f, 2, s.seed) * 2f);
                    Color pc = Color.Lerp(s.sunColor, s.accentB, band * 0.5f + 0.5f);
                    float lit = Mathf.Clamp01(Vector3.Dot(new Vector3(u, v, nz), new Vector3(-0.6f, 0.4f, 0.7f).normalized));
                    c = pc * (0.15f + 0.95f * lit);
                }
                if (ring && rv < 0f) c = Color.Lerp(c, ringColor, 0.9f);                       // the front of the ring
                return c;
            }

            case SkyPattern.SpiralGalaxy:
            {
                Color c = Stars(Gradient(s, dir.y, 0.5f), s, x, y, sinLat, res, 1.2f);
                if (d < 0.2f) return c;
                Tangent(sun, dir, out float u, out float v);
                float r = s.sunSize * 0.5f;
                u /= r; v /= r;
                float tilt = 25f * Mathf.Deg2Rad, cs = Mathf.Cos(tilt), sn = Mathf.Sin(tilt);
                float ru = u * cs - v * sn, rv = (u * sn + v * cs) / 0.5f;
                float rr = Mathf.Sqrt(ru * ru + rv * rv);
                if (rr > 1.6f) return c;
                float theta = Mathf.Atan2(rv, ru);
                float arms = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(2f * theta - 6f * Mathf.Log(rr + 0.1f)), 2.5f)
                             * Mathf.Exp(-rr * 1.8f) * Mathf.Clamp01(1.6f - rr) * (0.6f + 0.8f * Fbm3(dir * 60f, 3, s.seed));
                float core = Mathf.Exp(-rr * rr * 14f);
                c += s.sunColor * core * 1.2f;
                c += Color.Lerp(s.accentA, s.accentB, Mathf.Clamp01(rr)) * arms * 0.9f;
                c += s.accentA * 0.12f * Mathf.Exp(-rr * 3f);
                return c;
            }

            case SkyPattern.BlackHole:
            {
                Color c = Stars(Gradient(s, dir.y, 0.5f), s, x, y, sinLat, res, 0.8f);
                if (d < 0.5f) return c;
                Tangent(sun, dir, out float u, out float v);
                float r = s.sunSize * 0.5f;
                u /= r; v /= r;
                float rho = Mathf.Sqrt(u * u + v * v);
                float tilt = 12f * Mathf.Deg2Rad, cs = Mathf.Cos(tilt), sn = Mathf.Sin(tilt);
                float ru = u * cs - v * sn, rv = (u * sn + v * cs) / 0.22f;
                float rr = Mathf.Sqrt(ru * ru + rv * rv);
                bool inHole = rho < 1f;
                if (inHole) c = Color.black;
                // The bright photon ring hugging the hole.
                float ring = Mathf.Exp(-Mathf.Pow((rho - 1.12f) / 0.07f, 2f));
                c += s.accentA * ring * 1.2f;
                // The accretion disc: bright on the side turning towards us, hidden behind the hole on the far half.
                float dopp = 0.45f + 0.55f * Mathf.Clamp01(ru * 0.5f + 0.5f);
                if (rr > 1.25f && rr < 3.6f && (rv < 0f || !inHole))
                {
                    float k = 1f - (rr - 1.25f) / 2.35f;
                    Color dc = Color.Lerp(s.accentB, s.accentA, k * k) * (0.5f + 1.6f * k * k * k) * dopp;
                    float edge = Mathf.Clamp01((rr - 1.25f) * 12f) * Mathf.Clamp01((3.6f - rr) * 4f);
                    c = Color.Lerp(c, dc, edge);
                }
                // Light from the back of the disc bent over the top of the hole.
                if (v > 0f && rho > 1f && rho < 2.4f)
                    c += s.accentB * Mathf.Exp(-Mathf.Pow((rho - 1.45f) / 0.32f, 2f)) * Mathf.Clamp01(v / rho) * 0.7f * dopp;
                return c;
            }

            case SkyPattern.TwinSuns:
            {
                Color c = Gradient(s, dir.y, 0.5f);
                c += s.sunColor * (Mathf.Pow(Mathf.Max(0f, d), 20f) * 0.35f + Mathf.Pow(Mathf.Max(0f, d), 4f) * 0.08f);
                c = Disc(c, s.sunColor * 1.4f, d, s.sunSize);
                Vector3 sun2 = Direction(s.sunElevation * 0.6f + 8f, s.sunAzimuth + 55f);
                float d2 = Vector3.Dot(dir, sun2);
                c += s.accentA * Mathf.Pow(Mathf.Max(0f, d2), 30f) * 0.3f;
                c = Disc(c, s.accentA * 1.4f, d2, s.sunSize * 0.6f);
                return Clouds(c, s, dir, sun, Vector2.one, 0.35f);
            }
        }
        return Gradient(s, dir.y, 0.5f);
    }

    /// <summary>Part of ShadeExtra (one group of skies): CubeDay to Underwater (8 patterns).</summary>
    private static Color ShadeExtraCubeAndMore(SkyStyle s, Vector3 dir, Vector3 sun, float d, int x, int y, float sinLat, float res)
    {
        switch (s.pattern)
        {
            // ---------------- cube world ----------------
            case SkyPattern.CubeDay:
            {
                Color c = Color.Lerp(s.horizonColor, s.topColor, Mathf.Floor(Mathf.Clamp01(dir.y) * 8f) / 8f);
                if (d > 0.2f)
                {
                    Tangent(sun, dir, out float u, out float v);
                    float h = s.sunSize * 0.5f;
                    if (Mathf.Abs(u) < h && Mathf.Abs(v) < h) c = s.sunColor;   // a square sun
                }
                return BlockClouds(c, s, dir);
            }

            case SkyPattern.CubeNight:
            {
                Color c = Color.Lerp(s.horizonColor, s.topColor, Mathf.Floor(Mathf.Clamp01(dir.y) * 8f) / 8f);
                float chance = s.starAmount * 0.01f * sinLat / Mathf.Max(0.25f, res * res);
                if (PixelNoise.Hash(x / 2, y / 2, s.seed + 400) < chance) c = Color.Lerp(c, Color.white, 0.6f + 0.4f * PixelNoise.Hash(x / 2, y / 2, s.seed + 401));
                if (d > 0.2f)
                {
                    Tangent(sun, dir, out float u, out float v);
                    float h = s.sunSize * 0.5f;
                    if (Mathf.Abs(u) < h && Mathf.Abs(v) < h)   // a square moon with blocky craters
                    {
                        float crater = PixelNoise.Hash(Mathf.FloorToInt(u * 0.8f), Mathf.FloorToInt(v * 0.8f), s.seed + 7);
                        c = s.sunColor * (crater < 0.25f ? 0.72f : 1f);
                    }
                }
                return BlockClouds(c, s, dir);
            }

            case SkyPattern.CubeEmber:
            {
                Color c = Color.Lerp(s.horizonColor, s.topColor, Mathf.Floor(Mathf.Clamp01(dir.y) * 8f) / 8f);
                c += s.accentA * Mathf.Exp(-dir.y * 6f) * 0.2f;
                if (dir.y > 0.05f)
                {
                    float t = 1f / dir.y;
                    int cx = Mathf.FloorToInt(dir.x * t * 1.2f), cz = Mathf.FloorToInt(dir.z * t * 1.2f);
                    if (Fbm2(cx * 0.33f, cz * 0.33f, 3, s.seed) > 0.56f) c *= 0.6f;                  // dark smoke blocks
                    else if (PixelNoise.Hash(cx, cz, s.seed + 9) < 0.04f) c = Color.Lerp(c, s.accentA, 0.7f);   // glowing embers
                }
                return c;
            }

            case SkyPattern.CubeVoid:
            {
                float lon = Mathf.Atan2(dir.z, dir.x), lat = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f));
                int cu = Mathf.FloorToInt(lon * 14f), cv = Mathf.FloorToInt(lat * 14f);
                float n = Fbm2(cu * 0.4f, cv * 0.4f, 3, s.seed);
                Color c = Color.Lerp(s.topColor, s.accentB, Mathf.Clamp01((n - 0.45f) * 2f) * 0.45f);
                if (PixelNoise.Hash(cu, cv, s.seed + 5) > 0.93f) c = Color.Lerp(c, s.accentA, 0.3f);
                float chance = 0.012f * sinLat / Mathf.Max(0.25f, res * res);
                if (PixelNoise.Hash(x / 2, y / 2, s.seed + 600) < chance) c = Color.Lerp(c, Color.white, 0.8f);
                if (d > 0.2f)
                {
                    Tangent(sun, dir, out float u, out float v);
                    float h = s.sunSize * 0.5f, m = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    if (m < h) c = Color.black;                                                  // a black square sun...
                    else c += s.accentB * Mathf.Exp(-(m - h) / (h * 0.8f)) * 0.5f;              // ...with a violet glow
                }
                return c;
            }

            case SkyPattern.EyeVoid:
                return s.topColor;   // pitch black: the eye is a separate object (PixelSkyboxEye.cs)

            case SkyPattern.TestGrid:
                return TestGridColor(dir, sun, d);

            case SkyPattern.Starfield:
            {
                // Space with no objects: a dusty band, lots of stars and a few coloured ones.
                Color c = Gradient(s, dir.y, 0.6f);
                Vector3 n = new Vector3(0.3f, 0.8f, -0.5f).normalized;
                float b = Vector3.Dot(dir, n);
                float band = Mathf.Exp(-b * b * 14f) * Fbm3(dir * 3f, 4, s.seed);
                float dust = Fbm3(dir * 8f, 3, s.seed + 4);
                c += s.accentA * Mathf.Clamp01(band * 1.4f - 0.15f) * 0.45f;
                c += s.accentB * Mathf.Clamp01(band * dust * 1.8f - 0.3f) * 0.35f;
                c = Stars(c, s, x, y, sinLat, res, 1f + band * 4f);
                float chance = s.starAmount * 0.0016f * sinLat / Mathf.Max(0.25f, res * res);
                if (PixelNoise.Hash(x, y, s.seed + 5000) < chance)
                    c = Color.Lerp(c, Color.HSVToRGB(PixelNoise.Hash(x, y, s.seed + 5001), 0.4f, 1f), 0.9f);
                return c;
            }

            case SkyPattern.Underwater:
                return UnderwaterColor(s, dir, sun, x, y);
        }
        return Gradient(s, dir.y, 0.5f);
    }

    /// <summary>
    /// The test sky: a latitude / longitude grid (a line every 15 degrees, thick ones every 45 degrees of azimuth and 30 of elevation), the
    /// eight 45-degree azimuths in different colours (azimuth 0 = +Z is green, 90 = +X is red), the horizon in white, the zenith / nadir marked,
    /// a big orange disc at azimuth 0 / elevation 30 and a white disc at azimuth 90 / elevation 15, and everything BELOW the horizon tinted
    /// differently. Nothing is mirrored, so a screenshot shows exactly where the camera looks and whether the sky is flipped.
    /// </summary>
    private static Color TestGridColor(Vector3 dir, Vector3 sun, float sunDot)
    {
        float el = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        float az = Mathf.Repeat(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 360f);
        float ce = Mathf.Max(0.05f, Mathf.Cos(el * Mathf.Deg2Rad));
        bool below = el < 0f;
        Color c = below ? new Color(0.06f, 0.03f, 0.1f) : new Color(0.02f, 0.05f, 0.12f);
        c += (below ? new Color(0.05f, 0f, 0.08f) : new Color(0f, 0.04f, 0.1f)) * (1f - Mathf.Abs(el) / 90f);

        // parallels (elevation lines)
        float nearestEl = Mathf.Round(el / 15f) * 15f;
        float de = Mathf.Abs(el - nearestEl);
        bool bigEl = Mathf.Abs(nearestEl) % 30f < 0.01f;
        if (de < (bigEl ? 0.45f : 0.25f))
            c = Color.Lerp(c, below ? new Color(0.8f, 0.35f, 0.9f) : new Color(0.3f, 0.8f, 1f), bigEl ? 0.95f : 0.55f);

        // meridians (azimuth lines), measured on the ground so they stay the same width towards the poles
        float nearestAz = Mathf.Round(az / 15f) * 15f;
        float da = Mathf.Abs(az - nearestAz) * ce;
        bool bigAz = Mathf.Abs(nearestAz % 45f) < 0.01f || Mathf.Abs(nearestAz % 45f - 45f) < 0.01f;
        if (da < (bigAz ? 0.45f : 0.22f) && Mathf.Abs(el) < 89.5f)
        {
            Color lineColor = new Color(0.55f, 0.6f, 0.7f);
            if (bigAz)
            {
                switch (Mathf.RoundToInt(Mathf.Repeat(nearestAz, 360f) / 45f) % 8)
                {
                    case 0: lineColor = new Color(0.2f, 1f, 0.3f); break;     // +Z
                    case 1: lineColor = new Color(0.7f, 1f, 0.2f); break;
                    case 2: lineColor = new Color(1f, 0.2f, 0.2f); break;     // +X
                    case 3: lineColor = new Color(1f, 0.55f, 0.1f); break;
                    case 4: lineColor = new Color(0.3f, 0.4f, 1f); break;     // -Z
                    case 5: lineColor = new Color(0.7f, 0.3f, 1f); break;
                    case 6: lineColor = new Color(1f, 1f, 0.2f); break;       // -X
                    default: lineColor = new Color(0.2f, 1f, 0.9f); break;
                }
            }
            c = Color.Lerp(c, lineColor, bigAz ? 0.95f : 0.5f);
        }

        // the horizon
        if (Mathf.Abs(el) < 0.6f) c = Color.Lerp(c, Color.white, 0.95f);
        // zenith (yellow ring) and nadir (magenta ring)
        if (Mathf.Abs(Mathf.Abs(el) - 87f) < 0.5f) c = Color.Lerp(c, el > 0f ? new Color(1f, 1f, 0.3f) : new Color(1f, 0.3f, 0.8f), 0.95f);

        // two big discs so the picture is clearly not symmetric
        Vector3 orange = Direction(30f, 0f), moon = Direction(15f, 90f);
        if (Vector3.Dot(dir, orange) > Mathf.Cos(7f * Mathf.Deg2Rad)) c = new Color(1f, 0.55f, 0.1f);
        if (Vector3.Dot(dir, moon) > Mathf.Cos(5f * Mathf.Deg2Rad)) c = new Color(0.95f, 0.95f, 1f);
        return c;
    }

    /// <summary>Smooth 0..1 step between two edges (the shader-style smoothstep).</summary>
    private static float Sstep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / Mathf.Max(1e-6f, e1 - e0));
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// The underwater sky (animated by <c>shadeTime</c>): light water overhead with a web of rippling caustics, soft shafts of light fanning out
    /// from the sun above, drifting specks, and the water getting darker and bluer towards the depths.
    /// </summary>
    private static Color UnderwaterColor(SkyStyle s, Vector3 dir, Vector3 sun, int x, int y)
    {
        float t = shadeTime;
        float up = dir.y;
        Color c = up >= 0f ? Color.Lerp(s.horizonColor, s.topColor, Mathf.Pow(up, 0.7f)) : Color.Lerp(s.horizonColor, s.groundColor, Mathf.Clamp01(-up * 1.7f));

        // Caustics: bright rippling lines where the surface focuses the light, seen on the 'ceiling' of water overhead.
        if (up > 0.1f)
        {
            float k = 1f / up;
            float px = dir.x * k * 1.3f, pz = dir.z * k * 1.3f;
            float a = Mathf.Sin(px * 2.3f + t * 0.7f + 1.7f * Mathf.Sin(pz * 1.9f + t * 0.5f)) * Mathf.Sin(pz * 2.1f - t * 0.6f + 1.5f * Mathf.Sin(px * 1.7f - t * 0.4f));
            float b = Mathf.Sin(px * 3.7f - t * 0.9f + 1.3f * Mathf.Sin(pz * 3.1f + t * 0.8f)) * Mathf.Sin(pz * 3.4f + t * 0.7f + 1.1f * Mathf.Sin(px * 2.9f - t * 0.6f));
            float line = Mathf.Pow(1f - Mathf.Abs(a), 10f) * 0.8f + Mathf.Pow(1f - Mathf.Abs(b), 12f) * 0.5f;
            c += s.accentA * line * Sstep(0.1f, 0.55f, up) * 0.55f;
        }

        // Shafts of light fanning out from the sun, swaying.
        float dl = Vector3.Dot(dir, sun);
        Vector3 rel = dir - sun * dl;
        Vector3 e1 = Vector3.Cross(sun, Mathf.Abs(sun.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
        Vector3 e2 = Vector3.Cross(sun, e1);
        float ang = Mathf.Atan2(Vector3.Dot(rel, e2), Vector3.Dot(rel, e1));
        float r1 = 0.5f + 0.5f * Mathf.Sin(ang * 11f + t * 0.22f + 2.2f * Mathf.Sin(ang * 4f - t * 0.31f));
        float r2 = 0.5f + 0.5f * Mathf.Sin(ang * 23f - t * 0.35f + 1.3f * Mathf.Sin(ang * 7f + t * 0.2f));
        float rays = r1 * r1 * 0.7f + r2 * r2 * r2 * 0.3f;
        float angDist = Mathf.Acos(Mathf.Clamp(dl, -1f, 1f)) * Mathf.Rad2Deg;
        c += s.accentA * rays * Mathf.Exp(-angDist * 0.028f) * Sstep(-0.4f, 0.3f, up) * 0.5f;
        c += s.accentA * Mathf.Exp(-angDist * 0.12f) * 0.25f;   // the bright patch of sky itself

        // Drifting specks ('marine snow'): cells of a 3D grid that slide slowly upwards.
        Vector3 p = dir * 46f + new Vector3(t * 0.15f, t * 0.55f, 0f);
        int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y), cz = Mathf.FloorToInt(p.z);
        float h = PixelNoise.Hash(cx + cz * 977, cy, s.seed + 400);
        if (h > 0.97f)
        {
            Vector3 centre = new Vector3(PixelNoise.Hash(cx, cy + cz, s.seed + 401), PixelNoise.Hash(cy, cz + cx, s.seed + 402), PixelNoise.Hash(cz, cx + cy, s.seed + 403));
            float d = (new Vector3(p.x - cx, p.y - cy, p.z - cz) - centre).magnitude;
            c += Color.white * Mathf.Clamp01(1f - d / 0.45f) * (0.25f + 0.5f * PixelNoise.Hash(cx, cy, s.seed + 404)) * Mathf.Clamp01(0.4f + up);
        }

        // A SHARK circling far below - only in the bottom of the sky (straight down, never on the sides): a dark, hazy silhouette.
        if (up < -0.5f) c = Color.Lerp(c, s.groundColor * 0.35f, SharkShade(dir, t) * 0.6f * Sstep(0.5f, 0.72f, -up));

        // Deep Ocean only: a bioluminescent jellyfish drifting out in the dark that lights up every few seconds.
        if (s.name == "Deep Ocean") c += JellyGlow(dir, t);
        return c;
    }

    /// <summary>
    /// The Deep Ocean's glowing jellyfish: it drifts slowly round the view a little below the horizon. Every jellyPeriod seconds it LIGHTS UP - the bell's rim
    /// brightens first, then pulses of light run down its wavy tentacles - and fades back to a barely visible dark shape. Returns the light to add.
    /// </summary>
    private static Color JellyGlow(Vector3 dir, float t)
    {
        const float period = 7f;
        float az = Mathf.Atan2(dir.x, dir.z), el = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f));
        float az0 = t * 0.03f + 1.2f, el0 = -0.22f + 0.05f * Mathf.Sin(t * 0.21f);   // slow drift, gentle bob
        float dAz = Mathf.DeltaAngle(az0 * Mathf.Rad2Deg, az * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        float x = dAz * Mathf.Cos(el0), y = el - el0;                                 // local frame: x across, y up (radians)
        if (Mathf.Abs(x) > 0.35f || y > 0.25f || y < -0.6f) return Color.clear;

        // the flash: a quick rise, a slow fade, then dark until the next one
        float phase = Mathf.Repeat(t, period);
        float lit = phase < 0.4f ? phase / 0.4f : Mathf.Exp(-(phase - 0.4f) * 1.1f);
        Color glow = new Color(0.25f, 1f, 0.85f);
        Color pink = new Color(0.85f, 0.4f, 1f);
        float lightOut = 0f, pinkOut = 0f;

        // bell: a dome (upper half-ellipse) with a bright rim and a soft inner glow
        const float bw = 0.12f, bh = 0.09f;
        float bx = x / bw, by = y / bh;
        float bellR = Mathf.Sqrt(bx * bx + by * by);
        if (by > -0.15f)
        {
            float rim = Mathf.Exp(-Mathf.Pow((bellR - 0.92f) / 0.12f, 2f));
            float inner = bellR < 1f ? 0.35f * (1f - bellR) : 0f;
            lightOut += (rim * 0.9f + inner) * (0.08f + lit);
        }
        // tentacles: four wavy lines hanging down, with pulses of light running down them after the flash
        for (int i = 0; i < 4; i++)
        {
            float tx = (i - 1.5f) * 0.05f;
            float depth = -y;                                                        // 0 at the bell's base, more going down
            if (depth < 0f || depth > 0.5f) continue;
            float wave = Mathf.Sin(depth * 18f - t * 2.4f + i * 1.7f) * 0.025f * (0.3f + depth * 2f);
            float dx = x - tx - wave;
            float line = Mathf.Exp(-dx * dx / 0.00018f) * (1f - depth / 0.5f);
            float pulse = Mathf.Exp(-Mathf.Pow((depth - (phase - 0.3f) * 0.22f) / 0.05f, 2f));   // light running down
            lightOut += line * (0.05f + lit * 0.35f + pulse * 0.9f * Mathf.Clamp01(lit * 3f));
            pinkOut += line * pulse * 0.4f * Mathf.Clamp01(lit * 3f);
        }
        // a faint halo round it while it is lit
        float halo = Mathf.Exp(-(x * x + (y + 0.08f) * (y + 0.08f)) / 0.02f) * lit * 0.18f;
        return glow * (lightOut + halo) + pink * pinkOut;
    }

    /// <summary>
    /// 0..1: how much of a swimming shark covers this direction. The directions below are projected onto a flat 'sea floor' plane (the nadir = its middle);
    /// the shark swims a slow wobbly circle round it, its body / tail / fins tested in its own frame, the tail half swaying as it swims.
    /// </summary>
    private static float SharkShade(Vector3 dir, float t)
    {
        float k = 1f / -dir.y;
        Vector2 q = new Vector2(dir.x * k, dir.z * k);
        float theta = t * 0.24f;                                          // one lap in about 26 s
        float radius = 0.42f + 0.06f * Mathf.Sin(t * 0.37f);
        Vector2 pos = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * radius;
        Vector2 fwd = new Vector2(-Mathf.Sin(theta), Mathf.Cos(theta));   // swimming along the circle
        Vector2 side = new Vector2(fwd.y, -fwd.x);
        const float length = 0.42f;
        Vector2 d = q - pos;
        float u = Vector2.Dot(d, fwd) / length, v = Vector2.Dot(d, side) / length;
        if (Mathf.Abs(u) > 1f || Mathf.Abs(v) > 1f) return 0f;
        v -= Mathf.Sin(t * 3.2f - u * 4f) * 0.05f * Mathf.Clamp01(0.4f - u);   // the swimming sway, stronger towards the tail

        // body: a long rounded spindle from the tail stalk (u -0.45) to the pointed snout (u 0.6)
        float bodyHalf = 0f;
        if (u > -0.45f && u < 0.6f)
        {
            float a = (u - 0.05f) / (u > 0.05f ? 0.55f : 0.5f);
            bodyHalf = 0.12f * Mathf.Sqrt(Mathf.Clamp01(1f - a * a));
        }
        float av = Mathf.Abs(v);
        float shape = av < bodyHalf ? 1f : 0f;
        // tail: a forked crescent behind the stalk
        if (u <= -0.42f && u > -0.72f)
        {
            float span = (-0.42f - u) * 0.75f;
            if (av < span && av > span * 0.25f - 0.01f) shape = 1f;
            if (av < 0.025f) shape = 1f;
        }
        // pectoral fins: swept-back triangles either side
        if (InTri(new Vector2(u, av), new Vector2(0.16f, 0.08f), new Vector2(-0.02f, 0.08f), new Vector2(-0.12f, 0.3f))) shape = 1f;
        // the dorsal fin seen from above: a short dark ridge along the back
        if (u > -0.08f && u < 0.12f && av < 0.03f) shape = 1f;
        return shape;
    }

    private static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    /// <summary>Square clouds drawn on cells of a flat layer overhead (cube-world style).</summary>
    private static Color BlockClouds(Color c, SkyStyle s, Vector3 dir)
    {
        if (s.cloudAmount <= 0f || dir.y < 0.04f) return c;
        float t = 1f / dir.y;
        int cx = Mathf.FloorToInt(dir.x * t * 1.4f), cz = Mathf.FloorToInt(dir.z * t * 1.4f);
        float th = Mathf.Lerp(0.78f, 0.45f, s.cloudAmount);
        if (Fbm2(cx * 0.33f, cz * 0.33f, 3, s.seed) <= th) return c;
        bool top = Fbm2(cx * 0.33f, (cz - 1) * 0.33f, 3, s.seed) > th;
        return Color.Lerp(c, top ? s.cloudColor : s.cloudShadowColor, Mathf.Clamp01((dir.y - 0.04f) / 0.1f));
    }

    private static IEnumerable<SkyStyle> ExtraStyles()
    {
        // ---- weather ----
        yield return new SkyStyle
        {
            name = "Rainy Day", pattern = SkyPattern.Rain, seed = 101,
            topColor = new Color(0.28f, 0.31f, 0.36f), horizonColor = new Color(0.5f, 0.54f, 0.58f), groundColor = new Color(0.15f, 0.17f, 0.18f),
            cloudColor = new Color(0.5f, 0.53f, 0.58f), cloudShadowColor = new Color(0.2f, 0.22f, 0.26f), accentA = new Color(0.75f, 0.82f, 0.9f),
            cloudAmount = 0.95f, rotateSpeed = 1f, exposure = 0.9f
        };
        yield return new SkyStyle
        {
            name = "Thunderstorm", pattern = SkyPattern.Rain, seed = 103,
            topColor = new Color(0.1f, 0.11f, 0.15f), horizonColor = new Color(0.25f, 0.27f, 0.33f), groundColor = new Color(0.06f, 0.06f, 0.08f),
            cloudColor = new Color(0.36f, 0.38f, 0.45f), cloudShadowColor = new Color(0.07f, 0.08f, 0.11f), accentA = new Color(0.6f, 0.7f, 0.85f),
            cloudAmount = 1f, rotateSpeed = 1.5f, flashesPerMinute = 9f, exposure = 0.85f
        };
        yield return new SkyStyle
        {
            name = "Snowfall", pattern = SkyPattern.Snow, seed = 107,
            topColor = new Color(0.62f, 0.68f, 0.76f), horizonColor = new Color(0.9f, 0.93f, 0.97f), groundColor = new Color(0.85f, 0.88f, 0.92f),
            cloudColor = new Color(0.95f, 0.96f, 1f), cloudShadowColor = new Color(0.7f, 0.75f, 0.85f), sunColor = new Color(1f, 1f, 1f),
            sunElevation = 20f, cloudAmount = 0.8f, rotateSpeed = 0.5f
        };
        yield return new SkyStyle
        {
            name = "Foggy Morning", pattern = SkyPattern.Fog, seed = 109,
            topColor = new Color(0.7f, 0.74f, 0.76f), horizonColor = new Color(0.85f, 0.87f, 0.86f), groundColor = new Color(0.7f, 0.72f, 0.7f),
            cloudColor = new Color(0.95f, 0.95f, 0.93f), sunColor = new Color(1f, 0.95f, 0.8f), sunElevation = 12f, rotateSpeed = 0.3f
        };
        yield return new SkyStyle
        {
            name = "Sandstorm", pattern = SkyPattern.Sandstorm, seed = 113,
            topColor = new Color(0.62f, 0.4f, 0.2f), horizonColor = new Color(0.85f, 0.6f, 0.32f), groundColor = new Color(0.5f, 0.35f, 0.2f),
            cloudColor = new Color(0.9f, 0.68f, 0.4f), cloudShadowColor = new Color(0.5f, 0.32f, 0.16f), sunColor = new Color(1f, 0.85f, 0.55f),
            sunElevation = 35f, rotateSpeed = 1.2f
        };
        yield return new SkyStyle
        {
            name = "Rainbow Sky", pattern = SkyPattern.Rainbow, seed = 127,
            topColor = new Color(0.16f, 0.4f, 0.85f), horizonColor = new Color(0.68f, 0.84f, 0.98f), groundColor = new Color(0.32f, 0.36f, 0.33f),
            sunColor = new Color(1f, 0.96f, 0.82f), cloudColor = Color.white, cloudShadowColor = new Color(0.7f, 0.75f, 0.85f),
            sunElevation = 22f, sunAzimuth = 180f, sunSize = 4f, cloudAmount = 0.5f, rotateSpeed = 0f
        };

        // ---- space ----
        yield return new SkyStyle
        {
            name = "Binary Suns", pattern = SkyPattern.TwinSuns, seed = 149,
            topColor = new Color(0.18f, 0.12f, 0.35f), horizonColor = new Color(0.95f, 0.5f, 0.35f), groundColor = new Color(0.12f, 0.08f, 0.15f),
            sunColor = new Color(1f, 0.85f, 0.5f), accentA = new Color(0.6f, 0.8f, 1f), cloudColor = new Color(1f, 0.7f, 0.6f), cloudShadowColor = new Color(0.4f, 0.25f, 0.4f),
            sunElevation = 20f, sunAzimuth = 0f, sunSize = 9f, cloudAmount = 0.3f, rotateSpeed = 0f
        };

        // ---- cube world ----
        yield return new SkyStyle
        {
            name = "Blocky Day", pattern = SkyPattern.CubeDay, seed = 151, width = 256, pixelated = true,
            topColor = new Color(0.4f, 0.62f, 1f), horizonColor = new Color(0.75f, 0.88f, 1f), groundColor = new Color(0.35f, 0.5f, 0.3f),
            sunColor = new Color(1f, 1f, 0.85f), cloudColor = Color.white, cloudShadowColor = new Color(0.8f, 0.85f, 0.95f),
            sunElevation = 45f, sunAzimuth = 20f, sunSize = 10f, cloudAmount = 0.5f, rotateSpeed = 0.4f
        };
        yield return new SkyStyle
        {
            name = "Blocky Night", pattern = SkyPattern.CubeNight, seed = 157, width = 256, pixelated = true,
            topColor = new Color(0.02f, 0.03f, 0.09f), horizonColor = new Color(0.06f, 0.08f, 0.18f), groundColor = new Color(0.02f, 0.03f, 0.05f),
            sunColor = new Color(0.92f, 0.92f, 0.85f), cloudColor = new Color(0.18f, 0.2f, 0.3f), cloudShadowColor = new Color(0.1f, 0.11f, 0.2f),
            sunElevation = 40f, sunAzimuth = -25f, sunSize = 8f, starAmount = 1f, cloudAmount = 0.4f, rotateSpeed = 0.3f
        };
        yield return new SkyStyle
        {
            name = "Blocky Ember", pattern = SkyPattern.CubeEmber, seed = 163, width = 256, pixelated = true,
            topColor = new Color(0.18f, 0.02f, 0.02f), horizonColor = new Color(0.75f, 0.22f, 0.06f), groundColor = new Color(0.2f, 0.04f, 0.02f),
            accentA = new Color(1f, 0.55f, 0.1f), rotateSpeed = 0.3f
        };

        foreach (SkyStyle more in MoreExtraStyles()) yield return more;
    }

    /// <summary>Part of ExtraStyles: space variants, under the sea, the watcher and the test grid, in menu order.</summary>
    private static IEnumerable<SkyStyle> MoreExtraStyles()
    {
        // ---- space variants (no objects: they survive the mirror) ----
        yield return new SkyStyle
        {
            name = "Crimson Nebula", pattern = SkyPattern.Nebula, seed = 61,
            topColor = new Color(0.02f, 0f, 0.01f), horizonColor = new Color(1f, 0.8f, 0.6f),
            accentA = new Color(0.7f, 0.05f, 0.12f), accentB = new Color(0.9f, 0.35f, 0.05f),
            starAmount = 1f, cloudAmount = 0f, rotateSpeed = 0.5f
        };

        yield return new SkyStyle
        {
            name = "Emerald Nebula", pattern = SkyPattern.Nebula, seed = 62,
            topColor = new Color(0f, 0.015f, 0.01f), horizonColor = new Color(0.85f, 1f, 0.9f),
            accentA = new Color(0.05f, 0.6f, 0.35f), accentB = new Color(0.05f, 0.3f, 0.6f),
            starAmount = 1f, cloudAmount = 0f, rotateSpeed = 0.5f
        };

        yield return new SkyStyle
        {
            name = "Cobalt Nebula", pattern = SkyPattern.Nebula, seed = 63,
            topColor = new Color(0f, 0.005f, 0.025f), horizonColor = new Color(0.8f, 0.9f, 1f),
            accentA = new Color(0.1f, 0.2f, 0.85f), accentB = new Color(0.4f, 0.1f, 0.7f),
            starAmount = 1f, cloudAmount = 0f, rotateSpeed = 0.5f
        };

        yield return new SkyStyle
        {
            name = "Violet Haze", pattern = SkyPattern.Nebula, seed = 64,
            topColor = new Color(0.01f, 0f, 0.025f), horizonColor = new Color(1f, 0.85f, 1f),
            accentA = new Color(0.45f, 0.15f, 0.75f), accentB = new Color(0.85f, 0.3f, 0.6f),
            starAmount = 1f, cloudAmount = 0f, rotateSpeed = 0.5f
        };

        yield return new SkyStyle
        {
            name = "Golden Dust", pattern = SkyPattern.Nebula, seed = 65,
            topColor = new Color(0.02f, 0.01f, 0f), horizonColor = new Color(1f, 0.95f, 0.7f),
            accentA = new Color(0.75f, 0.45f, 0.1f), accentB = new Color(0.6f, 0.15f, 0.1f),
            starAmount = 1f, cloudAmount = 0f, rotateSpeed = 0.5f
        };

        yield return new SkyStyle
        {
            name = "Teal Void", pattern = SkyPattern.Nebula, seed = 66,
            topColor = new Color(0f, 0.012f, 0.015f), horizonColor = new Color(0.8f, 1f, 1f),
            accentA = new Color(0.05f, 0.5f, 0.55f), accentB = new Color(0.05f, 0.15f, 0.35f),
            starAmount = 1f, cloudAmount = 0f, rotateSpeed = 0.5f
        };

        yield return new SkyStyle
        {
            name = "Deep Space", pattern = SkyPattern.Starfield, seed = 71,
            topColor = new Color(0f, 0f, 0.008f), horizonColor = new Color(0.01f, 0.01f, 0.03f), groundColor = new Color(0f, 0f, 0.008f),
            accentA = new Color(0.35f, 0.4f, 0.8f), accentB = new Color(0.6f, 0.3f, 0.7f),
            starAmount = 1.6f, cloudAmount = 0f, rotateSpeed = 0.4f
        };

        yield return new SkyStyle
        {
            name = "Star Dust", pattern = SkyPattern.Starfield, seed = 72,
            topColor = new Color(0.01f, 0f, 0.02f), horizonColor = new Color(0.05f, 0.02f, 0.08f), groundColor = new Color(0.01f, 0f, 0.02f),
            accentA = new Color(0.9f, 0.5f, 0.7f), accentB = new Color(0.4f, 0.6f, 1f),
            starAmount = 1.2f, cloudAmount = 0f, rotateSpeed = 0.4f
        };

        yield return new SkyStyle
        {
            name = "Cold Stars", pattern = SkyPattern.Starfield, seed = 73,
            topColor = new Color(0f, 0.01f, 0.025f), horizonColor = new Color(0.02f, 0.05f, 0.1f), groundColor = new Color(0f, 0.01f, 0.025f),
            accentA = new Color(0.4f, 0.7f, 1f), accentB = new Color(0.6f, 0.9f, 1f),
            starAmount = 1.4f, cloudAmount = 0f, rotateSpeed = 0.4f
        };

        yield return new SkyStyle
        {
            name = "Violet Aurora", pattern = SkyPattern.Aurora, seed = 81,
            topColor = new Color(0.01f, 0.0f, 0.05f), horizonColor = new Color(0.07f, 0.03f, 0.14f), groundColor = new Color(0.01f, 0.0f, 0.05f),
            accentA = new Color(0.65f, 0.25f, 1f), accentB = new Color(0.2f, 0.5f, 1f),
            starAmount = 0.6f, cloudAmount = 0f, rotateSpeed = 0.3f
        };

        yield return new SkyStyle
        {
            name = "Ice Aurora", pattern = SkyPattern.Aurora, seed = 82,
            topColor = new Color(0f, 0.02f, 0.06f), horizonColor = new Color(0.03f, 0.12f, 0.2f), groundColor = new Color(0f, 0.02f, 0.06f),
            accentA = new Color(0.3f, 0.9f, 1f), accentB = new Color(0.4f, 0.5f, 1f),
            starAmount = 0.6f, cloudAmount = 0f, rotateSpeed = 0.3f
        };

        yield return new SkyStyle
        {
            name = "Crimson Aurora", pattern = SkyPattern.Aurora, seed = 83,
            topColor = new Color(0.03f, 0.0f, 0.03f), horizonColor = new Color(0.12f, 0.03f, 0.08f), groundColor = new Color(0.03f, 0.0f, 0.03f),
            accentA = new Color(1f, 0.2f, 0.3f), accentB = new Color(0.9f, 0.5f, 0.2f),
            starAmount = 0.6f, cloudAmount = 0f, rotateSpeed = 0.3f
        };

        // ---- under the sea (animated) ----
        yield return new SkyStyle
        {
            name = "Underwater", pattern = SkyPattern.Underwater, seed = 91, width = 384,
            topColor = new Color(0.3f, 0.82f, 0.88f), horizonColor = new Color(0.04f, 0.38f, 0.55f), groundColor = new Color(0.01f, 0.09f, 0.2f),
            accentA = new Color(0.7f, 1f, 0.95f), sunElevation = 80f, sunAzimuth = 20f, rotateSpeed = 0f
        };

        yield return new SkyStyle
        {
            name = "Deep Ocean", pattern = SkyPattern.Underwater, seed = 92, width = 384,
            topColor = new Color(0.04f, 0.28f, 0.42f), horizonColor = new Color(0.01f, 0.1f, 0.25f), groundColor = new Color(0f, 0.015f, 0.06f),
            accentA = new Color(0.25f, 0.8f, 0.9f), sunElevation = 85f, sunAzimuth = -40f, rotateSpeed = 0f
        };

        // ---- the watcher ----
        yield return new SkyStyle
        {
            name = "Watcher", pattern = SkyPattern.EyeVoid, seed = 173, width = 64,
            topColor = Color.black, horizonColor = Color.black, groundColor = Color.black,
            accentA = new Color(1f, 0.55f, 0.15f), accentB = new Color(0.6f, 0.1f, 0.1f), rotateSpeed = 0f
        };

        // ---- for diagnosing screenshots ----
        yield return new SkyStyle
        {
            name = "Test Grid", pattern = SkyPattern.TestGrid, seed = 1, width = 2048,
            topColor = Color.black, horizonColor = Color.black, groundColor = Color.black,
            sunElevation = 30f, sunAzimuth = 0f, rotateSpeed = 0f
        };
    }
}
