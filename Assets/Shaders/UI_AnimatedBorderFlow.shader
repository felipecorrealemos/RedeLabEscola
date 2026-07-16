Shader "RedeLab/UI/Animated Border Flow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlowSpeed ("Flow Speed", Float) = 0.175
        _ColorSpread ("Visible Color Spread", Float) = 0.35
        _BorderWidth ("Border Thickness", Range(0.001, 0.2)) = 0.025
        _EdgeSoftness ("Border Softness", Range(0.001, 0.08)) = 0.01
        _AspectRatio ("Aspect Ratio", Float) = 2.7
        _Saturation ("Saturation", Range(0, 1)) = 0.85
        _Value ("Value", Range(0, 2)) = 1.25
        _Alpha ("Alpha", Range(0, 1)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _FlowSpeed;
            float _ColorSpread;
            float _BorderWidth;
            float _EdgeSoftness;
            float _AspectRatio;
            float _Saturation;
            float _Value;
            float _Alpha;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float RectPerimeterPosition(float2 uv)
            {
                float left = uv.x;
                float right = 1.0 - uv.x;
                float bottom = uv.y;
                float top = 1.0 - uv.y;
                float edge = min(min(left, right), min(bottom, top));

                if (edge == top)
                {
                    return uv.x * 0.25;
                }

                if (edge == right)
                {
                    return 0.25 + (1.0 - uv.y) * 0.25;
                }

                if (edge == bottom)
                {
                    return 0.5 + (1.0 - uv.x) * 0.25;
                }

                return 0.75 + uv.y * 0.25;
            }

            fixed3 HsvToRgb(float3 hsv)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
                return hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, IN.texcoord) * IN.color;
                float perimeter = RectPerimeterPosition(IN.texcoord);
                float horizontalEdgeDistance = min(IN.texcoord.y, 1.0 - IN.texcoord.y);
                float verticalEdgeDistance = min(IN.texcoord.x, 1.0 - IN.texcoord.x) * max(_AspectRatio, 0.001);
                float edgeDistance = min(verticalEdgeDistance, horizontalEdgeDistance);
                float borderMask = 1.0 - smoothstep(_BorderWidth, _BorderWidth + _EdgeSoftness, edgeDistance);
                float hue = frac(perimeter * _ColorSpread - _Time.y * _FlowSpeed);
                fixed3 flowingColor = HsvToRgb(float3(hue, _Saturation, _Value));

                fixed4 color;
                color.rgb = flowingColor;
                color.a = sprite.a * IN.color.a * borderMask * _Alpha;
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                return color;
            }
            ENDCG
        }
    }
}
