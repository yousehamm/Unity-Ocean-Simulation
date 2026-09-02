Shader "Custom/OceanDisplacementDebug"
{
    Properties
    {
        _DisplacementTex ("Displacement Tex", 2D) = "black" {}
        _HeightScale ("Height Scale", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _DisplacementTex;
            float _HeightScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float height : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float h = tex2Dlod(_DisplacementTex, float4(v.uv, 0, 0)).r;
                v.vertex.y += h * _HeightScale;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.height = h;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Grayscale by height, just to visualize
                float c = saturate(i.height * 0.5 + 0.5);
                return fixed4(c, c, c, 1);
            }
            ENDCG
        }
    }
}