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
            name = "Ringed Planet", pattern = SkyPattern.RingedPlanet, seed = 131,
            topColor = new Color(0.005f, 0.005f, 0.02f), horizonColor = new Color(0.02f, 0.02f, 0.05f),
            sunColor = new Color(0.85f, 0.7f, 0.5f), accentA = new Color(0.7f, 0.8f, 1f), accentB = new Color(0.55f, 0.4f, 0.3f),
            cloudColor = new Color(0.9f, 0.85f, 0.7f), cloudShadowColor = new Color(0.45f, 0.38f, 0.3f),
            sunElevation = 18f, sunAzimuth = 0f, sunSize = 34f, starAmount = 0.9f, rotateSpeed = 0f
        };
        yield return new SkyStyle
        {
            name = "Spiral Galaxy", pattern = SkyPattern.SpiralGalaxy, seed = 137,
            topColor = new Color(0.003f, 0.004f, 0.015f), horizonColor = new Color(0.015f, 0.015f, 0.04f),
            sunColor = new Color(1f, 0.9f, 0.7f), accentA = new Color(0.5f, 0.6f, 1f), accentB = new Color(1f, 0.5f, 0.7f),
            sunElevation = 25f, sunAzimuth = 0f, sunSize = 60f, starAmount = 0.8f, rotateSpeed = 0f
        };
        yield return new SkyStyle
        {
            name = "Black Hole", pattern = SkyPattern.BlackHole, seed = 139,
            topColor = new Color(0.002f, 0.002f, 0.01f), horizonColor = new Color(0.01f, 0.008f, 0.02f),
            accentA = new Color(1f, 0.85f, 0.5f), accentB = new Color(0.9f, 0.35f, 0.1f),
            sunElevation = 15f, sunAzimuth = 0f, sunSize = 22f, starAmount = 0.8f, rotateSpeed = 0f
        };
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
        yield return new SkyStyle
        {
            name = "Blocky Void", pattern = SkyPattern.CubeVoid, seed = 167, width = 256, pixelated = true,
            topColor = new Color(0.03f, 0.01f, 0.05f), horizonColor = new Color(0.05f, 0.02f, 0.08f),
            accentA = new Color(0.8f, 0.9f, 0.85f), accentB = new Color(0.35f, 0.15f, 0.5f),
            sunElevation = 35f, sunAzimuth = 0f, sunSize = 12f, rotateSpeed = 0.2f
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
