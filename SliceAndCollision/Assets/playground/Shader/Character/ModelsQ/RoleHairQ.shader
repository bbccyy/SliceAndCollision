Shader "Babel/ModelsQ/RoleHairQ"
{
	Properties
	{
        [Header(Legacy)]
        [Toggle(_NEW_COLOR)]_New_Color ("是不是新版", float) = 0



        [Header(Outline)]
        _Outline ("描边宽度", Range(0, 1)) = 0.006
        _OutlineColor ("描边颜色", color) = (0.3, 0.23, 0.2, 1)

        [Header(Cloth And Skin Common Res)]
        _Color ("主颜色", color) = (0.56, 0.56, 0.56, 1)
        _MainTex ("主贴图", 2D) = "white" { }
        _LightMap ("LightMap图(R:金属度 G:高光 B:自发光 A:阴影遮罩)", 2D) = "black" { }

        [Header(LightColor)]
        _CharacterLightColor ("角色受到的主光颜色", color) = (1, 1, 1, 1)
        _CharacterLightIntensity ("角色受到的主光强度", Range(0, 5)) = 1.25
        _CharacterAmbientLightColor ("角色受到的环境光颜色", color) = (0.5, 0.5, 0.5, 1)
        _CharacterShadowColor ("阴影颜色", Color) = (0.5, 0.5, 0.5, 1)

        // [Header(Specular)]
        // _Shiness ("光泽度", Range(1, 1024)) = 32
        // _Gloss ("高光强度", Range(0, 128)) = 3
        // _SpecColor ("高光颜色", Color) = (1, 1, 1, 1)
        // _SpecularMax ("高光上限", Range(0, 5)) = 1.5

        [Header(Other)]
        _RimColor ("边缘光颜色", Color) = (1, 1, 1, 1)
        [HideInInspector] _RimParam ("Rim Param", Vector) = (0.3, 0.6, 0, 0)

 

        [Header(Turbulence)]
        [Toggle(_DISSOLVE)] _TurbulenceOn ("溶解开关", float) = 0

        [HDR]_EdgeColor ("溶解边缘颜色", Color) = (6.75, 3, 0.95, 1)

        _Turbulence ("溶解图", 2D) = "black" { }

        _Cutout ("溶解程度", Range(0, 1)) = 0
        _Edge ("溶解边缘宽度", Range(0, 1)) = 0.1
	}

	SubShader
	{
		Tags { /*"RenderType" = "Opaque"*/"Queue"="Geometry+2" "RenderPipline" = "UniversalPipeline"}
		LOD 100

		Pass
		{
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile_fog

			//接收阴影
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _NEW_COLOR

			#include"CommonRoleQ.hlsl"

			ENDHLSL
		}

		Pass
		{
			Name "RoleQ_OutLine"
			Cull Front

			Offset[_OffsetSlope],[_OffsetUnit]

			HLSLPROGRAM
			#pragma vertex vertOutline
			#pragma fragment fragOutline
            #pragma multi_compile _ _NEW_COLOR

			#include"CommonRoleQ.hlsl"

			//#pragma multi_compile __ _DISAPPEAR_ROLE

			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags{"LightMode" = "ShadowCaster"}
					
			ZWrite On
			ZTest LEqual
			ColorMask 0
			Cull Back

			HLSLPROGRAM


			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment 

			//#pragma multi_compile __ _DISAPPEAR_ROLE

			//#include "URPToonShadowPass.hlsl"
			#include"CommonRoleQ.hlsl"
			ENDHLSL
		}
	}
	FallBack "Universal Render Pipeline/Simple Lit"
}
