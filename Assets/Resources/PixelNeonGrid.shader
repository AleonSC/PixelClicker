// The first person neon floor (PixelFirstPersonGrid): a world-space grid of glowing lines drawn on a flat quad just above the ground, visible only within a radius of up to 64 lit points
// (the player, the clicker cube and old pixels resting on the ground). _Points[i] = (x, z, radius, 0). Additive, no depth write. Lives in Resources so builds include it.
Shader "Pixel Clicker/Neon Grid"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Points[64];
            float _Count;
            float4 _Color;
            float _Spacing;
            float _Brightness;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float k = 0;
                for (int n = 0; n < 64; n++)
                {
                    if (n >= (int)_Count) break;
                    float d = distance(i.world.xz, _Points[n].xy);
                    float t = saturate(1.0 - d / max(0.001, _Points[n].z));
                    k = max(k, t * t * (3.0 - 2.0 * t));
                }
                if (k <= 0.001) discard;
                float2 g = i.world.xz / max(0.001, _Spacing);
                float2 f = abs(frac(g - 0.5) - 0.5);                 // 0 on a line
                float2 w = fwidth(g) * 1.2 + 0.02;
                float2 line2 = 1.0 - smoothstep(0.0, w, f);
                float line1 = max(line2.x, line2.y);
                float pulse = 0.85 + 0.15 * sin(_Time.y * 2.5 - length(i.world.xz) * 0.8);
                float glow = (1.0 - smoothstep(0.0, 0.5, min(f.x, f.y))) * 0.12;   // a soft halo round each line
                float a = (line1 + glow) * k * pulse * _Brightness;
                float3 col = lerp(_Color.rgb, float3(1, 1, 1), line1 * 0.45);
                return fixed4(col, saturate(a));
            }
            ENDCG
        }
    }
}
