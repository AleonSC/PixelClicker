// Moving caustic light (the bright rippling net sunlight makes on a shallow seabed), drawn ADDITIVELY on a flat quad just above the floor by
// PixelFloorOverlay (FloorOverlay.Caustics, e.g. Coral Reef). Worked out from the WORLD position, so it never repeats or stretches; two layers of
// moving cell edges at different sizes and speeds. Fades out round _Center between _FadeStart and _FadeEnd like the floor. _Clock is set by the
// overlay (unscaled time that freezes in Time Stop). Lives in Resources so builds include it (Resources.Load).
Shader "Pixel Clicker/Caustics"
{
    Properties
    {
        _Color ("Light colour", Color) = (0.8, 1, 1, 1)
        _Scale ("Cell size (world units)", Float) = 1.6
        _Speed ("Speed", Float) = 0.6
        _Strength ("Strength", Float) = 0.35
        _Clock ("Clock (seconds, set by the overlay)", Float) = 0
        _Center ("Fade centre (x, z)", Vector) = (0, 0, 0, 0)
        _FadeStart ("Fade start (world units)", Float) = 20
        _FadeEnd ("Fade end (world units)", Float) = 38
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Scale, _Speed, _Strength, _Clock, _FadeStart, _FadeEnd, _Fade;
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

            float2 hash22 (float2 p)
            {
                p = p - 4096.0 * floor(p / 4096.0);
                float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.xx + q.yz) * q.zy);
            }

            // How far from the nearest edge between two moving cells (0 = on an edge).
            float cellEdge (float2 p, float t)
            {
                float2 g = floor(p), f = frac(p);
                float d1 = 8.0, d2 = 8.0;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y);
                        float2 h = hash22(g + o);
                        float2 pt = o + 0.5 + 0.38 * sin(t + 6.2831 * h);   // each cell's point circles inside its cell
                        float2 r = pt - f;
                        float d = dot(r, r);
                        if (d < d1) { d2 = d1; d1 = d; }
                        else if (d < d2) d2 = d;
                    }
                return sqrt(d2) - sqrt(d1);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Clock * _Speed;
                float2 p = i.world.xz / max(0.1, _Scale);
                p += 0.18 * sin(p.yx * 1.7 + t * 0.8);                           // a slow wobble so the net's lines curve
                float a = 1.0 - smoothstep(0.0, 0.11, cellEdge(p, t));
                float b = 1.0 - smoothstep(0.0, 0.09, cellEdge(p * 1.9 + 7.3, t * 1.3));
                float light = saturate(a + b * 0.55);
                light *= light;

                float dist = distance(i.world.xz, _Center.xy);
                float k = saturate((dist - _FadeStart) / max(0.01, _FadeEnd - _FadeStart));
                k = k * k * k * (k * (k * 6.0 - 15.0) + 10.0);                       // the floor rim's smoother-step
                return fixed4(_Color.rgb, saturate(light * _Strength * (1.0 - k) * _Fade));
            }
            ENDCG
        }
    }
    Fallback Off
}
