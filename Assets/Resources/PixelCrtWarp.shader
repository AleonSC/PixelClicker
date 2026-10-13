// Screen frame / warp (used by PixelFilters.cs): the finished frame is copied at the end of every frame and drawn back through this shader.
//   _Frame 0 = just the warped picture with rounded glass corners;
//   _Frame 1 = a TV: the glass sits in a plastic BEZEL (lit inner lip, soft shadow where the glass meets the frame);
//   _Frame 2 = tech GOGGLES: two round lenses with metal rims, a tick-mark ring, crosshairs, a dark body between them.
// Also: barrel warp, scanlines, colour fringing, vignette, a colour tint, flicker and the software mouse cursor (drawn where the picture shows the point the real pointer is on).
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
            float _Zoom, _Warp, _Scan, _Chroma, _Vignette, _Round, _Flip, _Bright, _Flicker, _TimeV, _Frame, _Bezel;
            float4 _Res;      // x = width, y = height (pixels)
            float4 _Cursor;   // xy = where the cursor tip is drawn (pixels, from the bottom left), z = size in pixels, w = 1 when the cursor is drawn
            float4 _Tint;     // rgb = colour, a = how much of the picture is turned into it

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            float3 Grade(float3 col, float2 uv, float vig)
            {
                float scanWave = 0.5 + 0.5 * sin(uv.y * _Res.y * 2.0943951);
                col *= 1.0 - _Scan * (1.0 - scanWave);
                col *= saturate(1.0 - vig);
                float lum = dot(col, float3(0.3, 0.59, 0.11));
                col = lerp(col, lum * _Tint.rgb * 1.6, _Tint.a);
                return col * _Bright * (1.0 + _Flicker * sin(_TimeV * 55.0));
            }

            float3 Pic(float2 suv, float2 off)
            {
                float2 sampleUV = float2(suv.x, lerp(suv.y, 1.0 - suv.y, _Flip));
                float2 o = off; o.y *= lerp(1.0, -1.0, _Flip);
                float3 col;
                col.r = tex2Dlod(_MainTex, float4(sampleUV + o, 0, 0)).r;
                col.g = tex2Dlod(_MainTex, float4(sampleUV, 0, 0)).g;
                col.b = tex2Dlod(_MainTex, float4(sampleUV - o, 0, 0)).b;
                return col;
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 c = uv * 2.0 - 1.0;
                float aspect = _Res.x / _Res.y;
                float3 outCol;

                if (_Frame > 1.5)
                {
                    // ---- tech goggles: two round lenses ----
                    float2 p = c * float2(aspect, 1.0);
                    float cx = aspect * 0.5, R = 0.9;
                    float2 centre = float2(p.x > 0.0 ? cx : -cx, 0.0);
                    float2 l = p - centre;
                    float dl = length(l);
                    float rdG = dl - R;
                    float lensMask = 1.0 - smoothstep(-0.006, 0.006, rdG);

                    float3 pic = Pic(uv, (uv - 0.5) * _Chroma);
                    float r = dl / R;
                    float3 lens = Grade(pic, uv, _Vignette * r * r * r * 1.6);

                    // HUD: a tick ring, a crosshair, a thin glow line at the glass edge.
                    float3 hud = float3(0.35, 1.0, 0.5);
                    float ang = atan2(l.y, l.x) / 6.2831853 + 0.5;
                    float minor = step(0.9, r) * step(r, 0.965) * step(frac(ang * 72.0), 0.14);
                    float major = step(0.855, r) * step(r, 0.965) * step(frac(ang * 12.0), 0.012 * 12.0 * 0.3);
                    float2 lc = l / R;
                    float ring = step(abs(length(lc) - 0.07), 0.004);
                    float cross = step(abs(lc.x), 0.003) * step(0.11, abs(lc.y)) * step(abs(lc.y), 0.2) + step(abs(lc.y), 0.003) * step(0.11, abs(lc.x)) * step(abs(lc.x), 0.2);
                    float edgeGlow = smoothstep(-0.02, 0.0, rdG) * (1.0 - smoothstep(0.0, 0.004, rdG));
                    float pulse = 0.85 + 0.15 * sin(_TimeV * 3.0);
                    lens += hud * saturate(minor * 0.7 + major + ring * 0.9 + cross * 0.9 + edgeGlow * 0.7) * pulse * _Tint.a;

                    // The body between / around the lenses: dark, with a fine hatch, and a metal rim round each lens.
                    float hatch = step(0.5, frac((uv.x * _Res.x + uv.y * _Res.y) * 0.18)) * 0.015;
                    float3 body = float3(0.028, 0.032, 0.028) + hatch + Hash(floor(uv * _Res.xy * 0.5)) * 0.01;
                    float rim = smoothstep(0.0, 0.01, rdG) * (1.0 - smoothstep(0.035, 0.06, rdG));
                    float rimLit = 0.45 + 0.35 * saturate(dot(normalize(l + 1e-4), float2(-0.6, 0.8)));
                    body = lerp(body, float3(0.38, 0.4, 0.38) * rimLit, rim * 0.85);
                    body += hud * 0.18 * (1.0 - smoothstep(0.0, 0.012, abs(rdG - 0.075)));   // a thin green ring on the rim
                    outCol = lerp(body, lens, lensMask);
                }
                else
                {
                    // ---- warped glass, optionally inside a TV bezel ----
                    float bz = _Frame > 0.5 ? _Bezel : 0.0;
                    float2 cg = c / (1.0 - bz);
                    float2 s = cg * _Zoom * (1.0 + _Warp * float2(cg.y * cg.y, cg.x * cg.x));
                    float2 suv = s * 0.5 + 0.5;
                    float2 edgeD = min(suv, 1.0 - suv);
                    float insideImg = smoothstep(0.0, 0.004, min(edgeD.x, edgeD.y));

                    float2 q = abs(cg) - (1.0 - _Round);
                    float rd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Round;   // < 0 inside the glass
                    float glassMask = 1.0 - smoothstep(-0.004, 0.006, rd);

                    float3 glass = Grade(Pic(suv, (suv - 0.5) * _Chroma), uv, _Vignette * dot(c, c) * 0.5);
                    if (_Frame > 0.5)
                    {
                        glass *= 1.0 - 0.6 * smoothstep(-0.09, 0.0, rd);                              // the bezel's shadow on the glass
                        glass += 0.06 * smoothstep(0.7, 0.0, length(cg - float2(-0.45, 0.55)));      // a soft glare
                    }
                    glass *= insideImg;

                    float3 frame = float3(0.0, 0.0, 0.0);
                    if (_Frame > 0.5)
                    {
                        float B = max(0.001, bz / (1.0 - bz));
                        float t = saturate(rd / (B * 1.15));
                        float lip = smoothstep(0.0, 0.05, t) * (1.0 - smoothstep(0.05, 0.16, t));   // the lit inner edge
                        float slope = lerp(0.26, 0.07, smoothstep(0.12, 0.85, t));                   // plastic: lighter near the glass, darker outwards
                        float2 n = normalize(cg + 1e-4);
                        float lightDir = 0.5 + 0.5 * dot(n, normalize(float2(-0.5, 0.8)));          // top left catches the light
                        frame = float3(0.62, 0.58, 0.5) * slope * (0.55 + 0.6 * lightDir);
                        frame += float3(0.55, 0.52, 0.45) * lip * (0.4 + 0.5 * lightDir);
                        frame *= 1.0 - 0.8 * (1.0 - smoothstep(0.0, 0.018, rd));                     // a dark gap where the glass meets the frame
                        frame += Hash(floor(uv * _Res.xy * 0.5)) * 0.012;
                        frame *= 1.0 - 0.45 * smoothstep(0.7, 1.0, t);                               // the outer edge falls into shadow
                    }
                    outCol = lerp(frame, glass, glassMask);
                }

                // The mouse cursor (an arrow texture, tip at the top left), sampled outside any branch.
                float2 pc = uv * _Res.xy - _Cursor.xy;
                float2 tc = float2(pc.x, -pc.y) / max(_Cursor.z, 1.0);
                float curIn = step(0.0, tc.x) * step(tc.x, 1.0) * step(0.0, tc.y) * step(tc.y, 1.0) * step(0.5, _Cursor.w);
                float4 cur = tex2Dlod(_CursorTex, float4(saturate(tc.x), 1.0 - saturate(tc.y), 0, 0));
                outCol = lerp(outCol, cur.rgb, cur.a * curIn);
                return fixed4(outCol, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
