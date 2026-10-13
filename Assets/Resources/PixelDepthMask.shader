// Draws nothing, only the DEPTH of the mesh. The clicker aura (PixelClickerAura) uses a copy of the cube's mesh with this shader, drawn after the cube and just before the aura, so the aura
// is cut out wherever the cube is - even when the cube's own material is transparent and writes no depth. Lives in Resources so builds include it.
Shader "Pixel Clicker/Depth Mask"
{
    SubShader
    {
        Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }
            fixed4 frag(v2f i) : SV_Target { return 0; }
            ENDCG
        }
    }
}
