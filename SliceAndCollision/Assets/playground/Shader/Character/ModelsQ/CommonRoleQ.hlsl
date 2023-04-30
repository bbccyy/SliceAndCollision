
#ifndef COMMON_ROLE_Q
#define COMMON_ROLE_Q

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Color.hlsl"

float3 _LightDirection;
float _SeaPlaneYHeight;

sampler2D _MainTex, _Turbulence;

sampler2D _LightMap;

sampler2D _CausticsTex, _IntersectionNoise, _NormalMapOn;

CBUFFER_START(UnityPerMaterial)
    float _CharacterLightIntensity;
    half4 _CharacterLightColor;
    half4 _CharacterAmbientLightColor;
    float4 _MainTex_ST;
    float4 _CharacterShadowColor;


    half4 _Color;
    half4 _RimColor;
    half4 _RimParam;


    float _Outline;
    half4 _OutlineColor;


    float _Cutout, _Edge, _SoftEdge;
    float4 _EdgeColor, _Turbulence_ST;


    // half _Gloss;
    // half _Shiness;
    // half _SpecularMax;



CBUFFER_END


struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float4 tangent : TANGENT;
    float4 color : COLOR;
};

struct v2f
{
    float2 uv0 : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    half3 viewDir : TEXCOORD2;
    half3 normalDir : TEXCOORD3;
    half3 positionWS : TEXCOORD4;
    half fogFactor : TEXCOORD5;
};

v2f vert(appdata v)
{
    v2f o;

    VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);

    o.positionWS = vertexInput.positionWS;
    o.positionCS = vertexInput.positionCS;

    o.uv0 = v.uv;
    o.normalDir = TransformObjectToWorldNormal(v.normal.xyz);
    o.viewDir = normalize(GetCameraPositionWS() - vertexInput.positionWS);

    o.fogFactor = ComputeFogFactor(o.positionCS.z);


    return o;
}

half4 frag(v2f i) : SV_Target
{


    half4 finalColor = 1;

    half4 albedo = tex2D(_MainTex, i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw);
    half4 lightmap = tex2D(_LightMap, i.uv0);


    #ifndef _NEW_COLOR
        albedo = LinearToSRGB(albedo);
        _Color = LinearToSRGB(_Color);
        _CharacterShadowColor = LinearToSRGB(_CharacterShadowColor);
        _CharacterLightColor = LinearToSRGB(_CharacterLightColor);
        _CharacterAmbientLightColor = LinearToSRGB(_CharacterAmbientLightColor);
        _RimColor = LinearToSRGB(_RimColor);
        _EdgeColor.rgb = LinearToSRGB(_EdgeColor.rgb);


    #endif


    half4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    float3 viewDir = i.viewDir;
    float3 halfDir = normalize(viewDir + mainLight.direction);

    float NdotL = max(0, dot(i.normalDir, mainLight.direction));
    float NdotH = max(0, dot(i.normalDir, halfDir));


    float atten = saturate(pow(NdotL, 0.45));

    half4 ShadowColor = lerp(_CharacterShadowColor, 1, atten);

    half4 lightColor = _CharacterLightColor * _CharacterLightIntensity;
    
    half4 ambient = albedo * _CharacterAmbientLightColor;




    finalColor.rgb = ((albedo * lightColor * ShadowColor) + ambient);

    finalColor.a = albedo.a;


    //边缘光
    half NdotV = 1 - max(0, dot(normalize(i.viewDir), normalize(i.normalDir)));
    NdotV = pow(NdotV, 4);
    half4 rim = smoothstep(_RimParam.x, _RimParam.y, saturate(NdotV)) * _RimColor * (1 + _RimParam.z);

    finalColor.rgb += saturate(rim * finalColor);
    
    
    //高光
    finalColor *= _Color;
    float shiness = exp2(10 * lightmap.g + 1);

    float spec = min(pow(NdotH, shiness), 1);


    finalColor.rgb += spec * lightmap.r;


    //自发光
    finalColor.rgb += finalColor.rgb * lightmap.b ;



    #ifdef _DISSOLVE
        half4 turbulenceTex = tex2D(_Turbulence, i.uv0.xy * _Turbulence_ST.xy + _Turbulence_ST.zw);
        half turbulence = saturate(turbulenceTex.r + 0.1 - _Cutout * 1.1);
        half edge = smoothstep(_Edge * 0.1, 0, turbulence);
        finalColor.rgb = lerp(finalColor.rgb, _EdgeColor.rgb, edge);
        clip(turbulence - 0.001);

    #endif

    // #ifndef _ROLE_FACE_Q
    // 	finalColor.a = 1;
    // #endif



    finalColor.rgb = MixFog(finalColor.rgb, i.fogFactor);

    #ifndef _NEW_COLOR
        finalColor.rgb = SRGBToLinear(finalColor.rgb);
    #endif


    return finalColor;
}




float4 OutlineProcess(float3 positionOS, float3 normaldir, float amount)
{
    float4 pos;
    pos.xyz = TransformObjectToWorld(positionOS);

    float3 normal = TransformObjectToWorldNormal(normaldir);

    pos.xyz = TransformWorldToView(pos.xyz);
    normal = TransformWorldToViewDir(normal);

    pos.xy += (normal.xy) * amount * 2;
    pos = TransformWViewToHClip(pos.xyz);

    return pos;
}

v2f vertOutline(appdata v)
{
    v2f o = (v2f)0;
    o.uv0 = v.uv;





    
    o.positionCS = OutlineProcess(v.vertex.xyz, normalize(v.tangent.xyz), _Outline * v.color.a);



    return o;
}

half4 fragOutline(v2f i) : SV_Target
{
    clip(i.positionWS.y - _SeaPlaneYHeight);

    half4 color = tex2D(_MainTex, i.uv0);


    #ifndef _NEW_COLOR
        color = LinearToSRGB(color);
        _OutlineColor = LinearToSRGB(_OutlineColor);
        _EdgeColor.rgb = LinearToSRGB(_EdgeColor.rgb);
    #endif


    color *= _OutlineColor;
    color.a = 1;
    #ifdef _DISSOLVE
        half4 turbulenceTex = tex2D(_Turbulence, i.uv0.xy * _Turbulence_ST.xy + _Turbulence_ST.zw);
        half turbulence = saturate(turbulenceTex.r + 0.1 - _Cutout * 1.1);
        half edge = smoothstep(_Edge * 0.1, 0, turbulence);
        color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge);
        clip(turbulence - 0.001);

    #endif

    #ifndef _NEW_COLOR
        color = SRGBToLinear(color);
    #endif

    return color;
}

float4 GetShadowPositionHClip(appdata input)
{
    float3 positionWS = TransformObjectToWorld(input.vertex.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.tangent.xyz);
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif

    return positionCS;
}

v2f ShadowPassVertex(appdata input)
{
    v2f output = (v2f)0;
    UNITY_SETUP_INSTANCE_ID(input);

    output.positionCS = GetShadowPositionHClip(input);
    output.uv0 = input.uv;

    return output;
}

half4 ShadowPassFragment(v2f input) : SV_TARGET
{
    clip(input.positionWS.y - _SeaPlaneYHeight);

    float4 color = float4(0, 0, 0, 0);
    #ifdef _DISSOLVE
        half4 turbulenceTex = tex2D(_Turbulence, input.uv0.xy * _Turbulence_ST.xy + _Turbulence_ST.zw);
        half turbulence = saturate(turbulenceTex.r + 0.1 - _Cutout * 1.1);
        clip(turbulence - 0.001);

    #endif
    return 0;
}

#endif