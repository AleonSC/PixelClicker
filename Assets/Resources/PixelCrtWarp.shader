// CRT screen warp (used by PixelFilters.cs): the finished frame is copied at the end of every frame and drawn back through this shader:
// barrel warp, rounded black corners, scanlines, colour fringing, vignette, and the software mouse cursor (drawn where the warped picture shows the point the real pointer is on).
Shader "Pixel Clicker/CRT Warp"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _CursorTex ("Cursor", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CursorTex;
            float _Warp, _Scan, _Chroma, _Vignette, _Round, _Flip, _Bright, _Flicker, _TimeV;
            float4 _Res;      // x = width, y = height (pixels)
            float4 _Cursor;   // xy = where the cursor tip is drawn (pixels, from the bottom left), z = size in pixels, w = 1 when the cursor is drawn

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 c = uv * 2.0 - 1.0;
                // The picture's point shown at this spot: pushed outwards towards the corners, so the edges curve like a tube.
                float2 s = c * (1.0 + _Warp * float2(c.y * c.y, c.x * c.x));
                float2 suv = s * 0.5 + 0.5;
                float inside = step(0.0, suv.x) * step(suv.x, 1.0) * step(0.0, suv.y) * step(suv.y, 1.0);

                // Rounded corners of the glass.
                float2 q = abs(c) - (1.0 - _Round);
                float rd = length(max(q, 0.0)) - _Round;
                float mask = inside * (1.0 - smoothstep(0.0, 0.012, rd));

                float2 sampleUV = float2(suv.x, lerp(suv.y, 1.0 - suv.y, _Flip));
                float2 off = (suv - 0.5) * _Chroma;
                off.y *= lerp(1.0, -1.0, _Flip);
                float3 col;
                col.r = tex2D(_MainTex, sampleUV + off).r;
                col.g = tex2D(_MainTex, sampleUV).g;
                col.b = tex2D(_MainTex, sampleUV - off).b;

                // Scanlines (a 3 pixel period on the screen's own pixels), vignette, brightness, a faint flicker.
                float line = 0.5 + 0.5 * sin(uv.y * _Res.y * 2.0943951);
                col *= 1.0 - _Scan * (1.0 - line);
                col *= saturate(1.0 - _Vignette * dot(c, c) * 0.5);
                col *= _Bright * (1.0 + _Flicker * sin(_TimeV * 55.0));
                col *= mask;

                // The mouse cursor (an arrow texture, tip at the top left).
                if (_Cursor.w > 0.5)
                {
                    float2 p = uv * _Res.xy - _Cursor.xy;
                    float2 t = float2(p.x, -p.y) / _Cursor.z;
                    if (t.x >= 0.0 && t.x <= 1.0 && t.y >= 0.0 && t.y <= 1.0)
                    {
                        fixed4 cur = tex2D(_CursorTex, float2(t.x, 1.0 - t.y));
                        col = lerp(col, cur.rgb, cur.a);
                    }
                }
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
