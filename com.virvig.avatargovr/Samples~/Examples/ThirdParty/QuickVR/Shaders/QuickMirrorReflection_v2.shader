// original source from: http://wiki.unity3d.com/index.php/MirrorReflection4
Shader "QuickVR/MirrorReflection_v2"
{
	Properties
	{
		_MainTex("StereoTexture (Single Pass)", 2D) = "white" {}
		_LeftEyeTexture("Left Eye Texture", 2D) = "white" {}
		_RightEyeTexture("Right Eye Texture", 2D) = "white" {}
		_ReflectionPower("Reflection Power", Range(0.0, 1.0)) = 1.0
		_NoiseMask("Noise Mask", 2D) = "white" {}
		_NoiseColor("Noise Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_NoisePower("Noise Power", Range(0.0, 1.0)) = 0.0
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
		}

		Pass
		{
		CGPROGRAM
			#pragma vertex vert_v2
			#pragma fragment frag
			#include "UnityCG.cginc"
			
			/////////////////////////////////////////////////////////
// UNIFORM PARAMETERS 
/////////////////////////////////////////////////////////
uniform sampler2D _MainTex;					//Used for single instancing pass
uniform sampler2D _LeftEyeTexture;			//The texture containing the reflection of the DEFAULT geometry for the left eye
uniform sampler2D _RightEyeTexture;			//The texture containing the reflection of the DEFAULT geometry for the right eye
uniform sampler2D _NoiseMask;				//A texture used to create imperfections in the reflection
uniform float _ReflectionPower;				//Indicates how much light is the reflection. It is used to simulate the light lost during the reflection
uniform float _NoisePower;					//Indicates how much powerful is the noise texture
uniform float4 _NoiseColor;					//The color of the noise

uniform int REFLECTION_INVERT_Y;
uniform int STEREO_TARGET_EYE;

struct appdata
{
    float4 pos : POSITION;
    float2 uv : TEXCOORD0;

#if UNITY_SINGLE_PASS_STEREO
    UNITY_VERTEX_INPUT_INSTANCE_ID
#endif
};

struct v2f
{
	float2 uv : TEXCOORD0;
	float4 screenPos : TEXCOORD1;
	float4 pos : SV_POSITION;

#if UNITY_SINGLE_PASS_STEREO
	UNITY_VERTEX_INPUT_INSTANCE_ID 
	UNITY_VERTEX_OUTPUT_STEREO
#endif
};

float2 GetProjUV(float4 screenPos) 
{
	float2 projUV = screenPos.xy / screenPos.w;

#if UNITY_SINGLE_PASS_STEREO
	float4 scaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
	projUV = (projUV - scaleOffset.zw) / scaleOffset.xy;
#endif

	return projUV;
}

int isEyeLeft() 
{
	//if (STEREO_TARGET_EYE == 1) return 1;
	//else if (STEREO_TARGET_EYE == 2) return 0;
	return unity_StereoEyeIndex == 0 ? 1 : 0;
}
fixed4 ComputeFinalColor(float2 uvReflection, float2 uvTex) 
{
#if UNITY_SINGLE_PASS_STEREO
    float2 singlePassUV = uvReflection;
    singlePassUV.x = isEyeLeft() ? uvReflection.x * 0.5 : 0.5 + uvReflection.x * 0.5;
    return tex2D(_MainTex, singlePassUV);
#else
    fixed4 refl = isEyeLeft() ? tex2D(_LeftEyeTexture, uvReflection) : tex2D(_RightEyeTexture, uvReflection);
    fixed4 noiseColor = tex2D(_NoiseMask, uvTex) * _NoiseColor;
    fixed4 finalColor = refl * _ReflectionPower + noiseColor * _NoisePower;
    return saturate(finalColor);
#endif
}

			uniform float4x4 _mvpEyeLeft;
			uniform float4x4 _mvpEyeRight;

			v2f vert_v2(appdata i)
			{
				v2f o;
				
			#if UNITY_SINGLE_PASS_STEREO
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			#endif

				o.pos = UnityObjectToClipPos(i.pos);
				o.uv = i.uv;
				float4x4 mvp = isEyeLeft() ? _mvpEyeLeft : _mvpEyeRight;
				o.screenPos = ComputeScreenPos(mul(mvp, i.pos));

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
			#if UNITY_SINGLE_PASS_STEREO
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
			#endif

				float2 projUV = GetProjUV(i.screenPos);
				if (REFLECTION_INVERT_Y == 1) 
				{
					projUV.y = 1.0 - projUV.y;
				}
				
				return ComputeFinalColor(projUV, i.uv);
			}
		ENDCG
		}
	}
}