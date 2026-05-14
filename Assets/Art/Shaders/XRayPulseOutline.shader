Shader "Custom/XRayPulseOutline"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0
        _OutlineWidth ("Outline Width", Range(0, 0.2)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
        }

        Pass
        {
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 _Color;
            float _Alpha;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                v.vertex.xyz += normalize(v.normal) * _OutlineWidth;
                o.position = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, _Color.a * _Alpha);
            }

            ENDCG
        }
    }
}