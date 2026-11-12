// Soft rounded-rectangle panel for world-space UI (glass cards, buttons, keys, caption plates).
// Signed-distance corners and border in the fragment shader, so one quad per element at any size.
// Unlit and stereo-instancing aware (Quest renders both eyes in one pass).
Shader "SameSky/RoundedPanel"
{
    Properties
    {
        _Color ("Fill", Color) = (0.97, 0.95, 0.92, 0.92)
        _BorderColor ("Border", Color) = (1, 1, 1, 0.35)
        _MainTex ("Image (optional)", 2D) = "white" {}
        _UseTex ("Use image", Float) = 0
        _Size ("Size (m)", Vector) = (0.2, 0.08, 0, 0)
        _Radius ("Corner radius (m)", Float) = 0.02
        _Border ("Border width (m)", Float) = 0.0015
        _Glow ("Hover glow", Range(0, 1)) = 0
        _GlowColor ("Glow color", Color) = (1, 0.86, 0.6, 1)
        // 1 for a nearly solid background (a panel, a card): it then hides the labels of things standing behind it,
        // which draw later than it does. See-through elements keep 0.
        [Toggle] _ZWrite ("Write depth (solid backgrounds)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color, _BorderColor, _GlowColor;
            float4 _Size;
            float _Radius, _Border, _UseTex, _Glow;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float roundedBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - halfSize + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 halfSize = _Size.xy * 0.5;
                float2 p = (i.uv - 0.5) * _Size.xy;
                float d = roundedBox(p, halfSize, min(_Radius, min(halfSize.x, halfSize.y)));
                float aa = max(fwidth(d), 1e-5);
                float inside = 1.0 - smoothstep(-aa, aa, d);
                if (inside <= 0.0) discard;

                fixed4 fill = _Color;
                if (_UseTex > 0.5) fill *= tex2D(_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw);
                float border = 1.0 - smoothstep(-aa, aa, d + _Border);
                fixed4 col = lerp(_BorderColor, fill, border);
                col.rgb = lerp(col.rgb, _GlowColor.rgb, _Glow * 0.35 * (1.0 - border * 0.6));
                col.a *= inside;
                return col;
            }
            ENDCG
        }
    }
}
