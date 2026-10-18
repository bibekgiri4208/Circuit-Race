Shader "Circuit Race/Arcade Marker"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex : POSITION; };
            struct Varyings { float4 position : SV_POSITION; };
            float4 _Color;
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
