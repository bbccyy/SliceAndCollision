#ifndef URPToonShadowPass
#define URPToonShadowPass

#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

CBUFFER_START(UnityPerMaterial)
float3 _LightDirection;
float _SeaPlaneYHeight;
CBUFFER_END

struct Attributes
{
	float4 positionOS   : POSITION;
	float3 normalOS     : NORMAL;
	float2 uv     : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS               : SV_POSITION;
	float2 uv                       : TEXCOORD0;
	float3 positionWS:TEXCOORD1;

};


float4 GetShadowPositionHClip(Attributes input)
{
	float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
	float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

	float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

#if UNITY_REVERSED_Z
	positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
	positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

	return positionCS;
}


Varyings ShadowPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);

	output.positionCS = GetShadowPositionHClip(input);

	return output;

}



half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
	//#ifdef _DISAPPEAR_ROLE
	//(角色死亡效果)海平面以下的角色部位被裁减掉
	clip(input.positionWS.y - _SeaPlaneYHeight);
	//#endif

//		half alpha = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a * _BaseColor.a;
//#if defined(_ALPHATEST_ON)
//	clip(alpha - _Cutoff);
//#endif
	return 0;
}



#endif