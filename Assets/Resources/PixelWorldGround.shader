// World-space ground: the pattern is worked out per pixel from the WORLD position, so it never repeats, never stretches and stays sharp at
// any zoom or camera angle. Unlit and alpha-faded round a centre (so the ground melts into the sky). Used by PixelFloor for styles with a
// 'shaderGround' number (1 Lava, 2 Marble, 3 Ice, 4 Deep Space, 5 Data Streams, 6 Aura flames, 7 Death Energy wisps, 8 Matrix Rain, 9 Liquid Flow,
// 10 Laser Corridor). Lives in Resources so builds include it (loaded with Resources.Load).
// Patterns 6-10 are ports of the painted floors: their numbers are the painted texture's, with 'q' = world position / _Scale (_Scale = the painted
// Tile World Size, so one 'q' unit = one painted texture), but the random values come from WORLD cells, so nothing repeats.
// _Clock comes from PixelFloor (unscaled time that freezes in Time Stop, like the painted floors); _Scroll = world units per second; _Pulse = the glow pulse.
Shader "Pixel Clicker/World Ground"
{
    Properties
    {
        _ColorA ("Colour A", Color) = (0.1, 0.1, 0.1, 1)
        _ColorB ("Colour B", Color) = (1, 0.3, 0.05, 1)
        _ColorC ("Colour C", Color) = (1, 0.9, 0.4, 1)
        _Pattern ("Pattern (1 lava, 2 marble, 3 ice, 4 space, 5 data, 6 aura, 7 wisps, 8 matrix, 9 liquid, 10 lasers)", Float) = 1
        _Scale ("Pattern size (world units)", Float) = 6
        _Speed ("Animation speed", Float) = 0.15
        _Glow ("Glow", Float) = 1
        _Seed ("Seed", Float) = 3
        _Center ("Fade centre (x, z)", Vector) = (0, 0, 0, 0)
        _FadeStart ("Fade start (world units)", Float) = 20
        _FadeEnd ("Fade end (world units)", Float) = 38
        _Fade ("Fade", Range(0, 1)) = 1
        _Clock ("Clock (seconds, set by PixelFloor)", Float) = 0
        _Scroll ("Scroll (world units per second, x, z)", Vector) = (0, 0, 0, 0)
        _Pulse ("Glow pulse (1 = full)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _ColorA, _ColorB, _ColorC;
            float _Pattern, _Scale, _Speed, _Glow, _Seed, _FadeStart, _FadeEnd, _Fade, _Clock, _Pulse;
            float4 _Center, _Scroll;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 hash22 (float2 p)
            {
                float n = hash21(p);
                return float2(n, hash21(p + n + 19.19));
            }

            // Lattice cells wrap every 4096 (far beyond the floor): hash21 loses its randomness on huge numbers, which a long-running scroll would reach.
            float2 wrapL (float2 c) { return c - 4096.0 * floor(c / 4096.0); }

            float vnoise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float2 j = wrapL(i + 1.0);
                i = wrapL(i);
                float a = hash21(i), b = hash21(float2(j.x, i.y)), c = hash21(float2(i.x, j.y)), d = hash21(j);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm (float2 p)
            {
                float s = 0.0, a = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    s += a * vnoise(p);
                    p = p * 2.03 + float2(17.1, 9.2);
                    a *= 0.5;
                }
                return s;
            }

            // Layered noise normalised to 0..1, octaves doubling exactly (like PixelNoise.Fbm, which the painted floors use).
            float fbmN (float2 p, int octaves)
            {
                float s = 0.0, a = 0.5, n = 0.0;
                for (int i = 0; i < octaves; i++)
                {
                    s += a * vnoise(p);
                    n += a;
                    p = p * 2.0 + float2(17.1, 9.2);
                    a *= 0.5;
                }
                return s / n;
            }

            // Tiny bright dots: 'density' of the cells of grid 'g' hold one.
            float sparks (float2 g, float density)
            {
                float2 c = wrapL(floor(g));
                float2 f = frac(g) - 0.5;
                return step(1.0 - density, hash21(c + 3.3)) * smoothstep(0.32, 0.08, length(f - (hash22(c) - 0.5) * 0.5));
            }

            // Distance to the nearest (x) and second nearest (y) cell point.
            float2 voronoi (float2 p)
            {
                float2 g = floor(p), f = frac(p);
                float d1 = 8.0, d2 = 8.0;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y);
                        float2 r = o + hash22(g + o) - f;
                        float d = dot(r, r);
                        if (d < d1) { d2 = d1; d1 = d; }
                        else if (d < d2) d2 = d;
                    }
                return float2(sqrt(d1), sqrt(d2));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.world.xz / max(0.1, _Scale) + _Seed * 17.7;
                float t = _Clock * _Speed;
                float3 col = _ColorA.rgb;
                float pixelWorld = length(fwidth(i.world.xz));   // world units one screen pixel covers (worked out here: a gradient inside the pattern branches would make the compiler run every pattern)

                if (_Pattern < 1.5)            // lava: a dark crust with glowing veins
                {
                    float warp = fbm(p * 0.8 + t * 0.2);
                    float n = fbm(p * 1.5 + warp * 1.8 + float2(t * 0.1, -t * 0.07));
                    float ridge = 1.0 - abs(2.0 * n - 1.0);
                    float veins = smoothstep(0.82, 0.97, ridge);
                    float pulse = 0.85 + 0.15 * sin(_Clock * 0.8 + n * 6.0);
                    col = lerp(_ColorA.rgb * (0.7 + 0.6 * n), lerp(_ColorB.rgb, _ColorC.rgb, veins * veins) * (_Glow * pulse), veins);
                }
                else if (_Pattern < 2.5)       // marble: soft stone with long veins
                {
                    float n = fbm(p * 0.6);
                    float v = abs(sin((p.x * 0.8 + p.y * 0.5 + n * 3.5) * 3.14159));
                    float vein = 1.0 - smoothstep(0.0, 0.12, v);
                    float fine = 1.0 - smoothstep(0.0, 0.05, abs(sin((p.x * 0.5 - p.y * 0.9 + fbm(p * 1.7 + 7.0) * 3.0) * 6.28318)));
                    col = lerp(_ColorA.rgb * (0.9 + 0.12 * fbm(p * 3.0)), _ColorB.rgb, vein * 0.7);
                    col = lerp(col, _ColorC.rgb, fine * 0.5);
                }
                else if (_Pattern < 3.5)       // ice: cracked plates
                {
                    float2 v = voronoi(p * 0.55);
                    float edge = 1.0 - smoothstep(0.0, 0.06, v.y - v.x);
                    float n = fbm(p * 1.2);
                    col = lerp(_ColorA.rgb, _ColorB.rgb, n);
                    col = lerp(col, _ColorC.rgb, edge * 0.85);
                    col += _ColorC.rgb * 0.12 * fbm(p * 5.0);
                }
                else if (_Pattern < 4.5)       // deep space: nebula clouds and stars
                {
                    float n1 = fbm(p * 0.25 + t * 0.02);
                    float n2 = fbm(p * 0.5 + 11.3 - t * 0.03);
                    col = _ColorA.rgb + _ColorB.rgb * pow(n1, 2.2) * 1.3 + _ColorC.rgb * pow(n2, 3.0) * 1.1;
                    float2 sp = p * 7.0;
                    float2 g = floor(sp);
                    float h = hash21(g);
                    float2 f = frac(sp) - 0.5;
                    float star = step(0.93, h) * smoothstep(0.3, 0.0, length(f - (hash22(g) - 0.5) * 0.6));
                    col += star * (0.6 + 0.4 * sin(_Clock * 2.0 + h * 40.0)) * _Glow;
                }

                else if (_Pattern > 5.5)       // the ports of painted floors (6-10)
                {
                    float T = _Clock;
                    float2 q = (i.world.xz + _Scroll.xy * T) / max(0.1, _Scale);   // one q unit = one painted texture
                    float2 so = float2(frac(_Seed * 0.6180339) * 61.0, frac(_Seed * 0.4142135) * 47.0);   // the seed moves the noise (kept small: big numbers lose precision)
                    float boost = 1.0 + 0.5 * _Glow * _Pulse;   // (the painted floors glow through their emission)

                    if (_Pattern < 6.5)            // aura: tall flame streaks scrolling along, with sparks
                    {
                        float warp = fbmN(q * 21.0 + so, 3) * 0.15;
                        float flame = fbmN(float2((q.x + warp) * 70.0, q.y * 14.0) + so + 3.1, 3);
                        float flicker = fbmN(q * 28.0 + so + 7.7, 3);
                        float k = saturate((flame * 0.75 + flicker * 0.35 - 0.35) * 2.0);
                        k = k * k;
                        col = lerp(_ColorA.rgb, _ColorB.rgb, saturate(k * 1.6));
                        col = lerp(col, _ColorC.rgb, saturate(k * 2.2 - 1.1));
                        col = lerp(col, _ColorC.rgb, sparks(q * 256.0 + so, 0.02));
                        col *= boost;
                    }
                    else if (_Pattern < 7.5)       // death energy: domain-warped swirling wisps
                    {
                        float a1 = fbmN(q * 18.0 + so, 4);
                        float r = fbmN((q + a1 * 0.6) * 18.0 + so + 5.3, 4);
                        float w = fbmN(float2(q.x + r * 0.8, q.y - r * 0.8) * 24.0 + so + 9.1, 4);
                        float k = saturate((w - 0.45) * 2.5);
                        k = k * k * (3.0 - 2.0 * k);
                        col = lerp(_ColorA.rgb, _ColorB.rgb, k);
                        col = lerp(col, _ColorC.rgb, saturate(k * k * 1.6 - 0.5));
                        col = lerp(col, _ColorC.rgb, sparks(q * 256.0 + so, 0.015));
                        col *= boost;
                    }
                    else if (_Pattern < 8.5)       // matrix rain: columns of 8x8-texel glyphs, each column with its own bright trail
                    {
                        float2 g = q * 128.0;                                   // 128 glyph columns per painted texture
                        float2 cell = wrapL(floor(g));
                        float2 sub = floor(frac(g) * 8.0);
                        float head = hash21(float2(cell.x, 0.5) + so);
                        float trail = frac(head - q.y * 6.0 + hash21(float2(cell.x, 1.5) + so) * 0.3);
                        trail = pow(1.0 - trail, 2.5);
                        float lit = (sub.x >= 1.0 && sub.x < 6.0 && sub.y >= 1.0 && sub.y < 7.0
                                     && hash21(cell + sub * 0.125 + so + 5.0) > 0.45) ? 1.0 : 0.0;
                        // Far away a glyph's dots are smaller than a screen pixel: show their average instead of shimmering.
                        float far = saturate(pixelWorld / max(0.1, _Scale) * 128.0 * 8.0 - 0.5);
                        lit = lerp(lit, 0.26, far);
                        float b = trail * (0.6 + 0.4 * hash21(cell + so + 6.0));
                        col = lerp(_ColorA.rgb, _ColorB.rgb, b * lit);
                        if (trail > 0.9) col = lerp(col, _ColorC.rgb, 0.7 * lit);   // white-hot head
                        col *= boost;
                    }
                    else if (_Pattern < 9.5)       // liquid flow: warped waves with thin bright streaks where two wave sets meet
                    {
                        float w1 = fbmN(q * 9.0 + so, 3);
                        float w2 = fbmN((q + w1 * 0.4) * 9.0 + so + 4.7, 3);
                        float a = sin((q.x * 12.0 + w2 * 3.0) * 6.28318);
                        float b = sin((q.y * 12.0 - w1 * 3.0) * 6.28318);
                        float k = saturate(0.5 + 0.25 * (a + b));
                        col = lerp(_ColorA.rgb, _ColorB.rgb, k);
                        float ridge = pow(saturate(1.0 - abs(a - b) * 1.6), 6.0);
                        col = lerp(col, _ColorC.rgb, ridge * 0.8);
                        col *= boost;
                    }
                    else                           // laser corridor: metal panels with lit seams, red beams swaying and blinking both ways
                    {
                        const float TX = 128.0;                                  // the painted texture's texels per q unit (line widths)
                        const float BEAMS = 8.0;                                 // beams per q unit, each way
                        float2 pp = frac(q * 4.0);                               // 4 panels per q unit
                        float seam = min(min(pp.x, 1.0 - pp.x), min(pp.y, 1.0 - pp.y)) * TX / 4.0;
                        col = _ColorA.rgb * (0.82 + 0.25 * fbmN(q * 6.0 + so, 3) + 0.05 * sin(q.y * TX * 0.8));
                        if (seam < 1.2) col = _ColorA.rgb * 0.3;                                       // the seam itself
                        else if (seam < 3.2 && pp.x > 0.5) col = lerp(col, _ColorB.rgb, 0.35);          // a glass edge catching the light
                        if (pp.x > 0.93 && frac(q.y * 24.0) < 0.45) col = lerp(col, _ColorB.rgb, 0.9);   // light dashes along the seams

                        float2 acc = float2(0.0, 0.0);
                        for (int axis = 0; axis < 2; axis++)
                        {
                            float coord = (axis == 0 ? q.y : q.x) * BEAMS;
                            float baseI = floor(coord);
                            for (int o = -1; o <= 1; o++)
                            {
                                float bi = baseI + o;
                                float id = bi - 4096.0 * floor(bi / 4096.0);
                                float rate = 0.8 + 0.8 * hash21(float2(id, axis + 11.0) + so);
                                float centre = bi + 0.5 + 0.32 * sin(T * rate + id * 1.9 + axis * 2.4);
                                float slot = floor(T * 1.4 + id * 0.37 + axis * 3.0);
                                slot -= 4096.0 * floor(slot / 4096.0);
                                float on = hash21(float2(id * 7.0 + axis, slot) + so) > 0.28 ? 1.0 : 0.0;   // blinking off now and then
                                float d = abs(coord - centre) * TX / BEAMS;                          // painted texels from the beam
                                float beam = saturate(saturate(1.3 - d) + exp(-d * 0.55) * 0.55);
                                if (axis == 0) acc.x = max(acc.x, beam * on);
                                else acc.y = max(acc.y, beam * on);
                            }
                        }
                        float best = max(acc.x, acc.y);
                        float hot = min(acc.x, acc.y);                                               // white-hot where two beams cross
                        col = lerp(col, _ColorC.rgb * boost, saturate(best));
                        col = lerp(col, float3(1, 1, 1), saturate(hot) * 0.85);
                    }
                }
                else                           // data streams: lanes of glowing packets racing along them (a lane is _Scale world units wide)
                {
                    float lane = floor(p.x);
                    float lf = frac(p.x);
                    float dirSign = hash21(float2(lane, 2.0) + _Seed) > 0.5 ? 1.0 : -1.0;
                    float speed = (0.15 + 0.5 * hash21(float2(lane, 1.0) + _Seed)) * dirSign;
                    float yc = p.y * 0.9 + _Clock * speed * 1.6;
                    float ci = floor(yc);
                    float cf = frac(yc);
                    col = _ColorA.rgb * (0.85 + 0.2 * hash21(float2(lane, 8.0) + _Seed));
                    if (lf < 0.05 || lf > 0.95) col = lerp(col, _ColorB.rgb, 0.12);          // lane rails
                    float on = hash21(float2(ci, lane + 50.0) + _Seed);
                    if (on > 0.5 && lf > 0.2 && lf < 0.8 && cf > 0.08 && cf < 0.92)
                    {
                        float pick = hash21(float2(ci, lane + 9.0) + _Seed);
                        float3 packet = pick < 0.6 ? _ColorB.rgb : (pick < 0.88 ? _ColorC.rgb : float3(1, 1, 1));
                        float shine = 0.55 + 0.45 * hash21(float2(ci, lane + 33.0) + _Seed);
                        float lead = dirSign > 0.0 ? cf : 1.0 - cf;                            // the leading edge is brighter
                        col = packet * (shine * (0.6 + 0.6 * lead)) * (0.85 + 0.3 * _Glow);
                    }
                    else if (on > 0.5 && lf > 0.35 && lf < 0.65) col = lerp(col, _ColorB.rgb, 0.25);   // glow smear between packets
                }

                float dist = distance(i.world.xz, _Center.xy);
                float k = saturate((dist - _FadeStart) / max(0.01, _FadeEnd - _FadeStart));
                k = k * k * k * (k * (k * 6.0 - 15.0) + 10.0);   // the same smoother-step as the wireframe rim's fade
                float alpha = (1.0 - k) * _Fade;
                return fixed4(saturate(col), alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
