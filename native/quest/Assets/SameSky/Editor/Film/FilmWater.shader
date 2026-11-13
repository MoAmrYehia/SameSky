// Water for the animated film "Wish You Were Here" (editor-only: it never reaches a build of the app).
// It shows the world mirrored in it: FilmWishKit.Water draws the scene from below the surface into
// _FilmWaterReflection before every frame, and this shader reads that picture where each bit of water is on screen,
// pushed about by two layers of drifting ripples so lights stretch into broken streaks. Steeply seen water is
// darker and shows more of its own colour; water seen at a low angle is nearly a mirror.
Shader "SameSky/Film/Water"
{
    Properties
    {
        _Deep ("Deep colour", Color) = (0.01, 0.02, 0.04, 1)
        _Tint ("Reflection tint", Color) = (1, 1, 1, 1)
        _BumpMap ("Ripples", 2D) = "bump" {}
        _Distort ("Ripple strength", Float) = 0.035
        _Stretch ("Vertical stretch", Float) = 3
        _Mirror ("Mirror seen from straight above", Range(0, 1)) = 0.3
        _Tiles ("Ripple sizes (per metre)", Vector) = (0.16, 0.05, 0, 0)
        _Flow ("Ripple drift", Vector) = (0.02, 0.05, -0.012, 0.02)
        _FlipY ("Flip the reflection", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Deep;
            half4 _Tint;
            float4 _BumpMap_ST;
            float4 _Tiles;
            float4 _Flow;
            float _Distort;
            float _Stretch;
            float _Mirror;
            float _FlipY;
        CBUFFER_END
        float _FilmTime;
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_FilmWaterReflection); SAMPLER(sampler_FilmWaterReflection);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screen : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screen = ComputeScreenPos(p.positionCS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv1 = i.positionWS.xz * _Tiles.x + _Flow.xy * _FilmTime;
                float2 uv2 = i.positionWS.zx * _Tiles.y + _Flow.zw * _FilmTime;
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv1));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv2));
                half2 n = n1.xy * 0.6 + n2.xy * 0.4;
                float3 toEye = _WorldSpaceCameraPos - i.positionWS;
                float dist = length(toEye);
                half up = saturate(abs(toEye.y) / max(dist, 0.001));
                float2 suv = i.screen.xy / i.screen.w;
                if (_FlipY > 0.5) suv.y = 1.0 - suv.y;
                float2 offset = n * _Distort * float2(1.0, _Stretch) / (1.0 + dist * 0.015);
                half3 mirrored = SAMPLE_TEXTURE2D(_FilmWaterReflection, sampler_FilmWaterReflection, suv + offset).rgb * _Tint.rgb;
                half fresnel = _Mirror + (1.0 - _Mirror) * pow(1.0 - up, 4.0);
                half3 c = lerp(_Deep.rgb, mirrored, fresnel);
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 frag() : SV_Target { return half4(0, 1, 0, 0); }
            ENDHLSL
        }
    }
}
