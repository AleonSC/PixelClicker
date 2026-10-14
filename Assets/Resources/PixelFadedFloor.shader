// A painted floor picture that melts into the sky: unlit, alpha-blended, the picture sampled from the mesh's UVs (the tiling / scrolling the script sets on _MainTex), and its alpha faded to nothing round a
// centre (the cube or the first person player) between _FadeStart and _FadeEnd (the same smoother-step as the wireframe rim), so the floor never ends in a solid band that cuts the sky off.
// _Light dims it with the day / night cycle (an unlit picture would not darken by itself), _EmissionColor adds the picture's own glow. Used by PixelFloor for painted styles (fadeIntoSky). Lives in Resources
// so builds include it (loaded with Resources.Load).
Shader "Pixel Clicker/Faded Floor"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _BaseMap ("Picture (URP name, unused)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BaseColor ("Tint (URP name, unused)", Color) = (1, 1, 1, 1)
        _EmissionColor ("Glow", Color) = (0, 0, 0, 1)
        _Light ("Light", Float) = 1
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color, _EmissionColor;
            float _Light, _FadeStart, _FadeEnd, _Fade;
            float4 _Center;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float3 col = tex.rgb * _Color.rgb * _Light + tex.rgb * _EmissionColor.rgb;
                float dist = distance(i.world.xz, _Center.xy);
                float k = saturate((dist - _FadeStart) / max(0.01, _FadeEnd - _FadeStart));
                k = k * k * k * (k * (k * 6.0 - 15.0) + 10.0);
                float alpha = (1.0 - k) * _Fade * _Color.a;
                return fixed4(saturate(col), alpha);
            }
            ENDCG
        }
    }
}
