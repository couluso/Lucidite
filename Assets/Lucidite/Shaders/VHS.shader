// Effet VHS / PS1 appliqué à l'image basse résolution du jeu (utilisé sur une RawImage plein écran).
// Distorsion de tracking, bandes de glitch, aberration chromatique, réduction de couleurs + dithering de Bayer,
// scanlines, grain, vignette et flash coloré lors des dégâts.
Shader "Lucidite/VHS"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Glitch ("Glitch", Float) = 0
        _Flash ("Flash", Float) = 0
        _FlashColor ("Flash Color", Color) = (0.64, 0.09, 0.1, 1)
        _Intensity ("Intensity", Range(0, 1)) = 1
        _Resolution ("Resolution", Vector) = (640, 360, 0, 0)
        _ColorDepth ("Color Depth", Float) = 40
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float _Glitch, _Flash, _Intensity, _ColorDepth;
            float4 _FlashColor, _Resolution;

            static const float bayer[16] = {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0 };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float I = _Intensity;
                float2 res = max(_Resolution.xy, float2(1, 1));
                float2 uv = i.uv;
                float lineY = floor(uv.y * res.y);

                // Tracking : ondulation horizontale légère
                float wob = (sin(uv.y * 38.0 + t * 2.7) * 0.0007 + (hash(float2(lineY, floor(t * 24.0))) - 0.5) * 0.0012) * I;

                // Bandes de glitch (plus fréquentes avec _Glitch)
                float bandRnd = hash(float2(floor(uv.y * 22.0), floor(t * 14.0)));
                float band = step(0.988 - _Glitch * 0.3, bandRnd);
                wob += band * (hash(float2(lineY, t)) - 0.5) * (0.02 * I + 0.08 * _Glitch);

                // Barre de défilement VHS
                float roll = frac(uv.y * 0.6 - t * 0.06);
                float rollBar = smoothstep(0.0, 0.02, roll) * smoothstep(0.07, 0.02, roll);
                wob += rollBar * 0.0015 * I;

                uv.x += wob;

                // Aberration chromatique
                float ca = 0.0007 * I + _Glitch * 0.012;
                float r = tex2D(_MainTex, uv + float2(ca, 0)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2(ca, 0)).b;
                float3 col = float3(r, g, b);

                // Bavure de couleur (chroma VHS)
                float3 bleed = tex2D(_MainTex, uv - float2(2.0 / res.x, 0)).rgb;
                col = lerp(col, (col + bleed) * 0.5, 0.2 * I);

                // Réduction de couleurs + dithering Bayer (look PS1)
                int2 p = int2(fmod(floor(i.uv * res), 4.0));
                float d = bayer[p.x + p.y * 4] - 0.5;
                float depth = max(4.0, _ColorDepth);
                float3 q = floor(col * depth + d + 0.5) / depth;
                col = lerp(col, q, I);

                // Scanlines
                float scan = 0.9 + 0.1 * sin(i.uv.y * res.y * 3.14159 * 2.0);
                col *= lerp(1.0, scan, 0.4 * I);

                // Grain + neige pendant les glitchs
                float n = hash(i.uv * res + frac(t * 7.13) * 100.0);
                col += (n - 0.5) * (0.03 * I + _Glitch * 0.25);
                col += band * _Glitch * 0.25 * hash(float2(t, lineY));

                // Vignette
                float2 c = i.uv - 0.5;
                col *= saturate(1.0 - dot(c, c) * 0.8 * I);

                // Flash (dégâts / espoir)
                col = lerp(col, _FlashColor.rgb * 0.8, saturate(_Flash) * 0.45);

                return fixed4(saturate(col), 1);
            }
            ENDCG
        }
    }
}
