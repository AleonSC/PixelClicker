// World-space ground: the pattern is worked out per pixel from the WORLD position, so it never repeats, never stretches and stays sharp at
// any zoom or camera angle. Unlit and alpha-faded round a centre (so the ground melts into the sky). Used by PixelFloor for styles with a
// 'shaderGround' number (1 Lava, 2 Marble, 3 Ice, 4 Deep Space). Lives in Resources so builds include it (loaded with Resources.Load).
Shader "Pixel Clicker/World Ground"
{
    Properties
    {
        _ColorA ("Colour A", Color) = (0.1, 0.1, 0.1, 1)
        _ColorB ("Colour B", Color) = (1, 0.3, 0.05, 1)
        _ColorC ("Colour C", Color) = (1, 0.9, 0.4, 1)
        _Pattern ("Pattern (1 lava, 2 marble, 3 ice, 4 space)", Float) = 1
        _Scale ("Pattern size (world units)", Float) = 6
        _Speed ("Animation speed", Float) = 0.15
        _Glow ("Glow", Float) = 1
        _Seed ("Seed", Float) = 3
        _Center ("Fade centre (x, z)", Vector) = (0, 0, 0, 0)
        _FadeStart ("Fade start (world units)", Float) = 20
        _FadeEnd ("Fade end (world units)", Float) = 38
        _Fade ("Fade", Range(0, 1)) = 1
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
            #include "UnityCG.cginc"

            fixed4 _ColorA, _ColorB, _ColorC;
            float _Pattern, _Scale, _Speed, _Glow, _Seed, _FadeStart, _FadeEnd, _Fade;
            float4 _Center;

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

            float vnoise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
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
                float t = _Time.y * _Speed;
                float3 col = _ColorA.rgb;

                if (_Pattern < 1.5)            // lava: a dark crust with glowing veins
                {
                    float warp = fbm(p * 0.8 + t * 0.2);
                    float n = fbm(p * 1.5 + warp * 1.8 + float2(t * 0.1, -t * 0.07));
                    float ridge = 1.0 - abs(2.0 * n - 1.0);
                    float veins = smoothstep(0.82, 0.97, ridge);
                    float pulse = 0.85 + 0.15 * sin(_Time.y * 0.8 + n * 6.0);
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
                else                           // deep space: nebula clouds and stars
                {
                    float n1 = fbm(p * 0.25 + t * 0.02);
                    float n2 = fbm(p * 0.5 + 11.3 - t * 0.03);
                    col = _ColorA.rgb + _ColorB.rgb * pow(n1, 2.2) * 1.3 + _ColorC.rgb * pow(n2, 3.0) * 1.1;
                    float2 sp = p * 7.0;
                    float2 g = floor(sp);
                    float h = hash21(g);
                    float2 f = frac(sp) - 0.5;
                    float star = step(0.93, h) * smoothstep(0.3, 0.0, length(f - (hash22(g) - 0.5) * 0.6));
                    col += star * (0.6 + 0.4 * sin(_Time.y * 2.0 + h * 40.0)) * _Glow;
                }

                float dist = distance(i.world.xz, _Center.xy);
                float alpha = (1.0 - smoothstep(_FadeStart, max(_FadeStart + 0.01, _FadeEnd), dist)) * _Fade;
                return fixed4(saturate(col), alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
