Shader "CircuitRace/Ghibli Meadow/Sunlit Vegetation"
{
    Properties
    {
        _RootColor ("Root colour", Color) = (0.2,0.48,0.22,1)
        _TipColor ("Sunlit tips", Color) = (0.62,0.83,0.32,1)
        _Variation ("Painted variation", 2D) = "gray" {}
        _FlowerColor ("Pack flower colour atlas", 2D) = "white" {}
        _FlowerMask ("Pack flower cutout atlas", 2D) = "white" {}
        _UseAtlas ("Use flower atlas", Float) = 0
        _WindStrength ("Breeze", Float) = 0.045
        _FoliageFadeDistances ("Distance fade start / end", Vector) = (68,85,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_Variation); SAMPLER(sampler_Variation);
        TEXTURE2D(_FlowerColor); SAMPLER(sampler_FlowerColor);
        TEXTURE2D(_FlowerMask); SAMPLER(sampler_FlowerMask);
        CBUFFER_START(UnityPerMaterial)
            half4 _RootColor, _TipColor;
            float4 _Variation_ST, _FlowerColor_ST, _FlowerMask_ST;
            half _UseAtlas, _WindStrength;
            float4 _FoliageFadeDistances;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half height : TEXCOORD3;
            half fog : TEXCOORD4;
            half variation : TEXCOORD5;
            half distanceFade : TEXCOORD6;
            half3 bakedGI : TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 world = TransformObjectToWorld(input.positionOS.xyz);
            float height = saturate(input.positionOS.y * 3.1);
            float wave = sin(world.x*0.63 + world.z*0.38 + _Time.y*1.4);
            world.xz += float2(1,0.34) * wave * _WindStrength * height * height;
            output.positionWS = world;
            output.positionCS = TransformWorldToHClip(world);
            output.normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), float3(0,1,0), 0.7));
            output.uv = input.uv;
            output.height = height;
            output.fog = ComputeFogFactor(output.positionCS.z);
            output.variation = SAMPLE_TEXTURE2D_LOD(_Variation, sampler_Variation, world.xz*0.025, 0).r;
            output.distanceFade = saturate((_FoliageFadeDistances.y - distance(world, _WorldSpaceCameraPos)) /
                max(_FoliageFadeDistances.y - _FoliageFadeDistances.x, 0.001));
            // Probe lighting varies slowly across a blade: evaluate per vertex, not per pixel.
            output.bakedGI = SampleSH(output.normalWS);
            return output;
        }
        void Cutout(Varyings input)
        {
            if (_UseAtlas > 0.5) clip(SAMPLE_TEXTURE2D(_FlowerMask, sampler_FlowerMask, input.uv).r - 0.42);
            clip(input.distanceFade - InterleavedGradientNoise(input.positionCS.xy, 0));
        }
        ENDHLSL
        Pass
        {
            Name "SunlitFoliage"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            AlphaToMask On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Cutout(input);
                half3 albedo = lerp(_RootColor.rgb, _TipColor.rgb, smoothstep(0.02,0.9,input.height));
                albedo *= lerp(0.88h,1.12h,input.variation);
                if (_UseAtlas > 0.5) albedo = SAMPLE_TEXTURE2D(_FlowerColor, sampler_FlowerColor, input.uv).rgb;
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half wrapped = 0.6h + 0.4h * saturate(dot(normalize(input.normalWS), sun.direction));
                half3 illumination = input.bakedGI + sun.color * wrapped * sun.shadowAttenuation * 0.78h;
                half3 colour = albedo * illumination;
                return half4(MixFog(colour, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            half4 Depth(Varyings input) : SV_Target { Cutout(input); return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Normals
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 Normals(Varyings input) : SV_Target
            {
                Cutout(input);
                half3 n = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct*0.5+0.5)),0);
                #else
                    return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
