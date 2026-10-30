Shader "CircuitRace/Ghibli Meadow/Soft Cloud Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.24,0.5,0.64,1)
        _Horizon ("Horizon", Color) = (0.81,0.86,0.83,1)
        _Cloud ("Cloud", Color) = (0.95,0.95,0.88,1)
        _SunDirection ("Sun direction", Vector) = (0.5,0.6,0.6,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _Cloud;
                float4 _SunDirection;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(Hash(i), Hash(i+float2(1,0)), f.x), lerp(Hash(i+float2(0,1)), Hash(i+1), f.x), f.y);
            }
            float Clouds(float2 p) { return Noise(p)*0.57 + Noise(p*2.02+7)*0.28 + Noise(p*4.1+15)*0.15; }
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz; return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float height = saturate(ray.y);
                half3 color = lerp(_Horizon.rgb, _Zenith.rgb, pow(height, 0.48));
                float2 uv = ray.xz / max(ray.y + 0.32, 0.08);
                float wisps = smoothstep(0.61, 0.80, Clouds(uv * float2(1.5, 4.8) + float2(3, 14)));
                wisps *= smoothstep(0.01, 0.14, height) * 0.45;
                color = lerp(color, _Cloud.rgb, wisps);
                float glow = pow(saturate(dot(ray, normalize(_SunDirection.xyz))), 5);
                color = lerp(color, half3(1.0,0.96,0.85), glow * 0.66);
                color += pow(saturate(dot(ray, normalize(_SunDirection.xyz))), 8) * half3(1.1,1.05,0.92);
                color += pow(saturate(dot(ray, normalize(_SunDirection.xyz))), 240) * half3(0.8,0.7,0.5);
                return half4(color * 0.9h, 1);
            }
            ENDHLSL
        }
    }
}
