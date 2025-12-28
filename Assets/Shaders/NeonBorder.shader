Shader "Custom/NeonBorderUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Header(Neon Settings)]
        _Color1 ("Color 1", Color) = (0.176, 0.537, 0.984, 1) // 2d89fb
        _Color2 ("Color 2", Color) = (1, 0, 1, 1) // Pink default, user can change
        _CoreWidth ("Core White Width", Range(0, 1)) = 0.2 // Ratio relative to border
        _BorderWidth ("Border Width", Range(0, 0.5)) = 0.05
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.1
        _GlowIntensity ("Glow Intensity", Range(1, 5)) = 2.0
        _Speed ("Rotation Speed", Range(-5, 5)) = 1.0
        _Hardness ("Hardness", Range(0.1, 100)) = 50.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            float4 _Color1;
            float4 _Color2;
            float _CoreWidth;
            float _BorderWidth;
            float _CornerRadius;
            float _GlowIntensity;
            float _Speed;
            float _Hardness;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Helper for rounded box SDF
            float sdRoundedBox(float2 p, float2 b, float4 r)
            {
                r.xy = (p.x > 0.0) ? r.xy : r.zw;
                r.x  = (p.y > 0.0) ? r.x  : r.y;
                float2 q = abs(p) - b + r.x;
                return min(max(q.x,q.y),0.0) + length(max(q,0.0)) - r.x;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord - 0.5;
                float2 halfSize = float2(0.5, 0.5);

                // 1. Base Box Shape (The path of the line)
                // We offset halfSize by half-border-width so the border stays *inside* the UI bounds if needed,
                // or centered on the edge. Let's Center it on pure 0.5 bounds inset slightly.
                float2 boxSize = halfSize - _BorderWidth; 
                float baseDist = sdRoundedBox(uv, boxSize, _CornerRadius.xxxx);
                
                // 2. Annular SDF (Make it a hollow outline)
                // abs(dist) makes 0 the center of the line.
                // We want the line to be _BorderWidth thick.
                float distFromOneLine = abs(baseDist);
                float alphaDist = distFromOneLine - (_BorderWidth * 0.5);
                
                // 3. Alpha / Softness
                float alpha = 1.0 - saturate(alphaDist * _Hardness);

                // --- COLOR ANIMATION ---
                float angle = atan2(uv.y, uv.x);
                float angle01 = angle / 6.283185307 + 0.5;
                float t = angle01 + _Time.y * _Speed * 0.5;
                
                // Hard Transition logic
                float sineVal = sin(t * 6.283185307);
                float mixFactor = smoothstep(-0.05, 0.05, sineVal);
                
                float3 gradientColor = lerp(_Color1.rgb, _Color2.rgb, mixFactor);
                
                // --- WHITE CORE EFFECT ---
                // distFromOneLine IS the distance from the center.
                // We make the white core thinner than the border width.
                float coreThickness = _BorderWidth * _CoreWidth; // User defined raio
                float coreFactor = 1.0 - smoothstep(0, coreThickness, distFromOneLine);
                
                // Mix: Core is white, edges are colored.
                // To make it look like a tube, we can keep the color but add white.
                float3 finalRGB = lerp(gradientColor, float3(1, 1, 1), coreFactor);

                float4 finalColor;
                finalColor.rgb = finalRGB * _GlowIntensity;
                finalColor.a = alpha;

                finalColor *= IN.color;
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                
                #ifdef UNITY_UI_ALPHACLIP
                clip (finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}
