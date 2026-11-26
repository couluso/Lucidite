// Shader "PS1" pour URP : sommets accrochés à une grille d'écran (tremblement caractéristique),
// textures en mapping affine (déformation), éclairage diffus par pixel (lumières additionnelles + ombres),
// brouillard. Noms de propriétés compatibles avec URP/Lit (_BaseMap, _BaseColor, _Cutoff, _EmissionColor, _Cull).
Shader "Lucidite/PSXLit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Couleur", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Émission", Color) = (0, 0, 0, 1)
        _EmissionMap ("Carte d'émission", 2D) = "white" {}
        _Cutoff ("Seuil alpha", Range(0, 1)) = 0.5
        _Jitter ("Tremblement des sommets", Range(0, 1)) = 1
        _Affine ("Mapping affine", Range(0, 1)) = 0.7
        _Wrap ("Éclairage enveloppant", Range(0, 1)) = 0.25
        _DamageMap ("Blessures (RGB = couleur, A = seuil d'apparition)", 2D) = "black" {}
        _Damage ("Blessures (0 = intacte, 1 = massacrée)", Range(0, 1)) = 0
        _DamageMix ("Opacité des blessures (cartes : on devine l'icône sous le sang)", Range(0, 1)) = 1
        _DamageGlow ("Lueur des blessures dans la pénombre (cartes en main)", Range(0, 3)) = 0
        _SuitMap ("Classe de la carte (symbole + motif, RGBA)", 2D) = "black" {}
        _SuitMix ("Classe : opacité", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _Cutoff;
            half _Jitter;
            half _Affine;
            half _Wrap;
            half _Cull;
            half _Damage;
            half _DamageMix;
            half _DamageGlow;
            half _SuitMix;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_DamageMap); SAMPLER(sampler_DamageMap);
        TEXTURE2D(_SuitMap); SAMPLER(sampler_SuitMap);
        float _PSXSnapRes;

        float4 PSXSnap(float4 positionCS)
        {
            float res = _PSXSnapRes > 1.0 ? _PSXSnapRes : 160.0;
            float2 grid = float2(res * (_ScreenParams.x / _ScreenParams.y), res);
            float2 ndc = positionCS.xy / positionCS.w;
            float2 snapped = round(ndc * grid * 0.5) / (grid * 0.5);
            positionCS.xy = lerp(positionCS.xy, snapped * positionCS.w, _Jitter);
            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                noperspective float2 uvAffine : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float fogCoord : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                float4 cs = TransformWorldToHClip(o.positionWS);
                o.fogCoord = ComputeFogFactor(cs.z);
                o.positionCS = PSXSnap(cs);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uvAffine = o.uv;
                return o;
            }

            half3 Diffuse(Light l, half3 n)
            {
                half ndl = dot(n, l.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                return l.color * (l.distanceAttenuation * l.shadowAttenuation) * wrapped;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = lerp(i.uv, i.uvAffine, _Affine);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                // Classe de la carte (symbole sous le coût, motif de fond), sous le sang
                half4 suit = SAMPLE_TEXTURE2D(_SuitMap, sampler_SuitMap, uv);
                suit.a *= _SuitMix;
                albedo.rgb = lerp(albedo.rgb, suit.rgb, suit.a);
                // Blessures progressives : chaque marque apparaît quand _Damage dépasse son seuil (1 - alpha)
                half4 dm = SAMPLE_TEXTURE2D(_DamageMap, sampler_DamageMap, uv);
                half show = saturate((_Damage - (1.0h - dm.a)) * 25.0h) * step(0.004h, dm.a);
                show *= _DamageMix;
                albedo.rgb = lerp(albedo.rgb, dm.rgb, show);
                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif

                half3 n = normalize(i.normalWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                half3 light = SampleSH(n);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                light += Diffuse(mainLight, n);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                        light += Diffuse(l, n);
                    LIGHT_LOOP_END
                #endif

                half3 color = albedo.rgb * light;
                #if defined(_EMISSION)
                    // Le sang éteint en partie ce qui brillait dessous (contours des cartes)
                    half3 em = lerp(SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb, suit.rgb, suit.a);
                    color += em * _EmissionColor.rgb * (1.0h - show * 0.75h);
                #endif
                // Sang lisible même loin de la lampe (cartes en main) : il ressort d'autant plus qu'il fait sombre
                color += dm.rgb * show * _DamageGlow * saturate(1.0h - dot(light, half3(0.3h, 0.59h, 0.11h)));
                color = MixFog(color, i.fogCoord);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(cs);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = PSXSnap(TransformObjectToHClip(v.positionOS.xyz));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = PSXSnap(TransformObjectToHClip(v.positionOS.xyz));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return half4(normalize(i.normalWS), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
