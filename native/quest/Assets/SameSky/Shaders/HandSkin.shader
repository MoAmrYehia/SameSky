// Skin for the tracked hands (Meta's OpenXR hand mesh): a chosen skin tone with a lighter palm, nails, knuckle and
// palm creases from two baked maps (Editor/HandSkinSetup.cs), soft wrapped light with a warm scatter at the shadow
// edge, a small highlight (sharper on the nails), the room's light and shadows, and a soft fade at the wrist.
// Forward only, single-pass stereo aware; the hands also cast a shadow on the table.
Shader "SameSky/HandSkin"
{
    Properties
    {
        _SkinColor ("Skin tone (back of the hand)", Color) = (0.81, 0.61, 0.46, 1)
        _PalmColor ("Palm tone", Color) = (0.90, 0.72, 0.62, 1)
        _BloodColor ("Scattered light", Color) = (0.86, 0.38, 0.30, 1)
        _NailColor ("Nail plate", Color) = (0.93, 0.74, 0.68, 1)
        _NailTipColor ("Nail free edge", Color) = (0.96, 0.93, 0.88, 1)
        [NoScaleOffset] _MaskA ("Nail, palm, flush, cavity", 2D) = "black" {}
        [NoScaleOffset] _MaskB ("Normal xy, wrist, nail edge", 2D) = "gray" {}
        _NormalStrength ("Detail strength", Range(0, 2)) = 1
        _SkinSmoothness ("Skin smoothness", Range(0, 1)) = 0.36
        _NailSmoothness ("Nail smoothness", Range(0, 1)) = 0.74
        _Reflection ("Room reflection", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MaskA); SAMPLER(sampler_MaskA);
        TEXTURE2D(_MaskB); SAMPLER(sampler_MaskB);

        CBUFFER_START(UnityPerMaterial)
            half4 _SkinColor, _PalmColor, _BloodColor, _NailColor, _NailTipColor;
            half _NormalStrength, _SkinSmoothness, _NailSmoothness, _Reflection;
        CBUFFER_END

        // The wrist fades out over a few centimetres. With multisampling the fade becomes coverage; a little
        // per-pixel noise inside the fade hides the steps between coverage levels.
        half WristAlpha(half wrist, float2 pixel)
        {
            half noise = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            half inFade = wrist * (1.0 - wrist) * 4.0;
            return saturate(wrist + (noise - 0.5) * 0.35 * inFade);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            // Light on skin: the part that reaches past the geometric shadow edge has travelled through the skin
            // and comes out warmer. A normalised Blinn-Phong highlight: faint on skin, sharp on a nail.
            half3 ShadeSkin(half3 albedo, half3 N, half3 V, Light light, half smoothness, half nail)
            {
                half ndl = dot(N, light.direction);
                half direct = saturate(ndl);
                half wrapped = saturate((ndl + 0.4) / 1.4);
                half3 diffuse = direct + (wrapped - direct) * _BloodColor.rgb * (1.0 - nail);
                half3 H = SafeNormalize(light.direction + V);
                half power = exp2(10.0 * smoothness + 1.0);
                half specular = pow(saturate(dot(N, H)), power) * (power + 2.0) * 0.125 * lerp(0.028, 0.045, nail) * direct;
                return (albedo * diffuse + specular) * light.color * (light.distanceAttenuation * light.shadowAttenuation);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 a = SAMPLE_TEXTURE2D(_MaskA, sampler_MaskA, i.uv);
                half4 b = SAMPLE_TEXTURE2D(_MaskB, sampler_MaskB, i.uv);
                half nail = a.r, palm = a.g, flush = a.b, cavity = a.a;

                half alpha = WristAlpha(b.b, i.positionCS.xy);
                clip(alpha - 0.04);

                half3 skin = lerp(_SkinColor.rgb, _PalmColor.rgb, palm);
                skin *= lerp(half3(1.0, 1.0, 1.0), half3(1.03, 0.86, 0.83), flush);
                half3 nailColor = lerp(_NailColor.rgb, _NailTipColor.rgb, b.a);
                half3 albedo = lerp(skin, nailColor, nail);
                albedo *= lerp(half3(0.62, 0.5, 0.47), half3(1.0, 1.0, 1.0), cavity);   // creases are darker and a little redder

                half3 nTS;
                nTS.xy = (b.rg * 2.0 - 1.0) * _NormalStrength;
                nTS.z = sqrt(saturate(1.0 - dot(nTS.xy, nTS.xy)));
                // The tangent frame comes from how position and texture coordinates change across the pixel, so it
                // needs no tangents on the skinned mesh and is right for the mirrored hand too.
                float3 normalWS = normalize(i.normalWS);
                float3 dp1 = ddx(i.positionWS), dp2 = ddy(i.positionWS);
                float2 duv1 = ddx(i.uv), duv2 = ddy(i.uv);
                float3 dp2perp = cross(dp2, normalWS), dp1perp = cross(normalWS, dp1);
                float3 tangentWS = dp2perp * duv1.x + dp1perp * duv2.x;
                float3 bitangentWS = dp2perp * duv1.y + dp1perp * duv2.y;
                float scale = rsqrt(max(max(dot(tangentWS, tangentWS), dot(bitangentWS, bitangentWS)), 1e-20));
                half3 N = normalize(nTS.x * tangentWS * scale + nTS.y * bitangentWS * scale + nTS.z * normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                half smoothness = lerp(_SkinSmoothness, _NailSmoothness, nail) * lerp(0.55, 1.0, cavity);
                half3 color = SampleSH(N) * albedo * lerp(0.7, 1.0, cavity);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                color += ShadeSkin(albedo, N, V, mainLight, smoothness, nail);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint l = 0u; l < count; ++l)
                    color += ShadeSkin(albedo, N, V, GetAdditionalLight(l, i.positionWS), smoothness, nail);
                #endif

                // what the room looks like in the skin's sheen: only at grazing angles, stronger on the nails
                half fresnel = pow(1.0 - saturate(dot(N, V)), 5.0);
                half3 room = GlossyEnvironmentReflection(reflect(-V, N), 1.0 - smoothness, 1.0);
                color += room * lerp(0.03, 1.0, fresnel) * _Reflection * lerp(0.5, 1.0, nail) * cavity;

                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MaskB, sampler_MaskB, i.uv).b - 0.4);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                clip(SAMPLE_TEXTURE2D(_MaskB, sampler_MaskB, i.uv).b - 0.4);
                return half4(i.positionCS.z, 0, 0, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
