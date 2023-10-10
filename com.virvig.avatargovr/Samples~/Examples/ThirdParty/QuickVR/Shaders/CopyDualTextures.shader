Shader "Custom/CopyDualTextures" {
    Properties {
        _LeftTex ("Left Texture", 2D) = "white" {}
        _RightTex ("Right Texture", 2D) = "white" {}
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _LeftTex;
            sampler2D _RightTex;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target {
                half2 uv = i.uv;
                half4 color;
                if (uv.x < 0.5) {
                    color = tex2D(_LeftTex, uv * float2(2, 1));
                } else {
                    color = tex2D(_RightTex, (uv - float2(0.5, 0)) * float2(2, 1));
                }
                return color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
