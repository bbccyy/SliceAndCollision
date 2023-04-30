Shader "Babeitime/Character/OpaqueV2"
	{
    Properties 
	{
		[Header(Main)]
		_Color ("Main Color", Color) = (0.5, 0.5, 0.5, 1)
        _BassRGB ("Bass-RGB", 2D) = "white" {}
        _Mask ("Mask", 2D) = "black" {}
        _More ("More", 2D) = "black" {}

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("裁剪模式", Float) = 2


		[Header(Rim)]
        _RimColor ("Rim-Color", Color) = (0.5, 0.5, 0.5, 1)
        _RimEdge ("Rim-Edge", Range(1, 2)) = 1.6
        _RimSoft ("Rim-Soft", Range(0.01, 40)) = 10

		[Header(Specular)]
        _Specular ("SpecularColor", Color) = (1, 1, 1, 1)
        _Gloss ("Gloss", Float) = 16
        _SpecMultiplier ("Specular Multiplier", Float) = 2
        
		[Header(Reflection)]
        _ReflectTex ("ReflectTex", 2D) = "white" {}
        _ReflectColor ("ReflectColor", Color) = (1, 1, 1, 1)
        _ReflectPower ("ReflectPower", Float) = 1.3
        _ReflectionMultiplier ("ReflectionMultiplier", Float) = 2
		
		[Header(Emissive)]
		_EmissiveRange ("EmissiveRange", Range(0,10)) = 3
		_EmissiveOffsite ("EmissiveOffsite", Color) = (1, 1, 1, 0)

        [Header(Dissove)]
        _DissoveTex("溶解噪声图", 2D) = "white" {}
        _DissoveThreshold("溶解阈值", Range(-0.2, 1)) = -0.2
        _DissoveRange("溶解边界宽度", Range(0, 1)) = 0
        [HDR]_RangeColor("溶解边界颜色", Color) = (1, 0, 0, 1)

    }

    SubShader 
	{
        Tags 
		{
            "RenderType"="Opaque"
        }
        LOD 200
        Cull [_Cull]
        Pass 
		{
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
//            #define UNITY_PASS_FORWARDBASE
            #include "UnityCG.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma multi_compile_fog
            #pragma exclude_renderers d3d11_9x xbox360 xboxone ps3 ps4 psp2 

            #pragma target 3.0
			uniform float4 _Color;
            uniform sampler2D _BassRGB;
            uniform float4 _BassRGB_ST;
            uniform sampler2D _Mask;
            uniform sampler2D _More;
            uniform float4 _LightColor0;

            uniform float4 _RimColor;
            uniform float _RimEdge;
            uniform float _RimSoft;

            uniform float4 _Specular;
            uniform float _Gloss;
            uniform float _SpecMultiplier;
            
            uniform sampler2D _ReflectTex;
            uniform float _ReflectPower;
            uniform float _ReflectionMultiplier;
            uniform float3 _ReflectColor;

			uniform float _EmissiveRange;
			uniform float3 _EmissiveOffsite;

            uniform sampler2D _DissoveTex;
            uniform float _DissoveThreshold;
            uniform float _DissoveRange;
            uniform float4 _RangeColor;

            
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
				float2 reflectUV : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };

            VertexOutput vert (VertexInput v) 
			{
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);
				
                float2 viewNormal = mul(o.normalDir, UNITY_MATRIX_V);
                o.reflectUV = viewNormal * float2(0.5, 0.5) + float2(0.5, 0.5);

                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }

            float4 frag(VertexOutput i) : COLOR 
			{

               



                
                float4 _VLightingDirect = -_WorldSpaceLightPos0;
                
/////// Vectors:
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 normalDirection = normalize(i.normalDir);
                float2 uv = TRANSFORM_TEX(i.uv0, _BassRGB);
                half4 mask = tex2D(_Mask, uv);
                half4 more = tex2D(_More, uv);
////// Lighting:
////// Emissive:
				_LightColor0 *= 0.6;
                float4 _BassRGB_var = tex2D(_BassRGB, uv);
                float node_4011 = 0.5*dot(viewDirection,i.normalDir)+0.5;
                float3 emissive = lerp(
                                    (_RimColor.rgb + _BassRGB_var.rgb)
                                    ,lerp(
                                        saturate((_BassRGB_var.rgb > 0.5 ?  (1.0-(2.0-2.0*_BassRGB_var.rgb)*(1.0-_LightColor0.rgb)) : (2.0*_BassRGB_var.rgb*_LightColor0.rgb)))	// 不明所以的光照函数
                                        ,saturate((_BassRGB_var.rgb * UNITY_LIGHTMODEL_AMBIENT.rgb))
                                        ,saturate(((dot(_VLightingDirect.rgb,i.normalDir))+0.8))
                                        )
                                     ,min(pow(saturate(node_4011*_RimEdge),_RimSoft) + 1 - mask.a, 1));
                float3 finalColor = emissive;

// 反射
                // TODO: 不使用normal图
                float3 reflectTexClr = tex2D(_ReflectTex, i.reflectUV).rgb;
                float3 reflectInfluenceClr = exp2(log2(reflectTexClr * _ReflectColor.rgb) * _ReflectPower);
                finalColor = (mask.g * (reflectInfluenceClr * _ReflectionMultiplier - 1) + 1) * finalColor;

////// 镜面光
                fixed3 reflectDir = normalize(reflect(_VLightingDirect, normalDirection));
                float3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0.0,dot(reflectDir, viewDirection)), _Gloss) * _SpecMultiplier;
                specular = specular * mask.r;
                finalColor = finalColor + specular;
                
                // 用b通道加强来达到曝光的效果
                finalColor += finalColor * mask.b * _EmissiveRange * _EmissiveOffsite;

                fixed4 finalRGBA = fixed4(finalColor, 1) * _Color * 2;
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                finalRGBA = lerp(finalRGBA,_BassRGB_var*0.93,more.r);

                //Dissove
                half4 dissoveVal = tex2D(_DissoveTex, i.uv0);
                clip(dissoveVal.r - _DissoveThreshold);

                half rangeValue = step(_DissoveThreshold+_DissoveRange, dissoveVal.r);

                return lerp(_RangeColor, finalRGBA, rangeValue);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
    //CustomEditor "ShaderForgeMaterialInspector"
}
