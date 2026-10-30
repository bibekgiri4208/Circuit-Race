Shader "CircuitRace/Ghibli Meadow/Painterly Ground"
{
    Properties
    {
        _BaseMap ("Painted ground texture", 2D) = "white" {}
        _TextureStrength ("Texture strength", Range(0,1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _TextureStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 lightmapUV : TEXCOORD1; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half4 color : COLOR; half fog : TEXCOORD3; float2 lightmapUV : TEXCOORD4; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fog = ComputeFogFactor(position.positionCS.z);
                output.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half lightRamp = smoothstep(-0.2, 0.85, dot(normalize(input.normalWS), sun.direction));
                half3 textureTint = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 albedo = input.color.rgb * lerp(1.0h, textureTint * 1.6h, _TextureStrength);
                #if defined(LIGHTMAP_ON)
                    half3 ambient = SampleLightmap(input.lightmapUV, normalize(input.normalWS));
                #else
                    half3 ambient = SampleSH(normalize(input.normalWS));
                #endif
                half3 illumination = ambient + sun.color * lightRamp * sun.shadowAttenuation * 0.78h;
                return half4(MixFog(albedo * illumination, input.fog), 1);
            }
            ENDHLSL
        }
        // Export the actual painted, vertex-coloured albedo for bounced light.
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex MetaVert
            #pragma fragment MetaFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _TextureStrength;
            CBUFFER_END
            struct MetaAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; half4 color : COLOR; };
            struct MetaVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            MetaVaryings MetaVert(MetaAttributes input)
            {
                MetaVaryings output;
                output.positionCS = MetaVertexPosition(input.positionOS, input.uv1, input.uv2, unity_LightmapST, unity_DynamicLightmapST);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }
            half4 MetaFrag(MetaVaryings input) : SV_Target
            {
                MetaInput meta = (MetaInput)0;
                half3 tint = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                meta.Albedo = input.color.rgb * lerp(1.0h, tint * 1.6h, _TextureStrength);
                return MetaFragment(meta);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
