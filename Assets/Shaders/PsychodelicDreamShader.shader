Shader "mil/PsychedelicDream"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _WaveFrequency ("Wave Frequency", Float) = 6.0
        _WaveAmplitude ("Wave Amplitude", Float) = 0.03
        
        _TimeParameter ("FMOD Audio Time", Float) = 0.0
        _MusicBpmSpeed ("Music BPM Speed", Float) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;

            float _WaveFrequency;
            float _WaveAmplitude;
            float _TimeParameter;
            float _MusicBpmSpeed;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float3 HueShift(float3 color, float shift)
            {
                float3 k = float3(0.57735, 0.57735, 0.57735);
                float cosAngle = cos(shift);
                return color * cosAngle + cross(k, color) * sin(shift) + k * dot(k, color) * (1.0 - cosAngle);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 center = input.uv - 0.5;
                float dist = length(center);
                
                float waveTime = _TimeParameter * _MusicBpmSpeed * 2.0;
                float wave = sin(dist * _WaveFrequency - waveTime) * _WaveAmplitude;
                
                float2 distortedUv = input.uv + (center / (dist + 0.001)) * wave;
                
                float4 texColor = _MainTex.Sample(sampler_MainTex, distortedUv) * input.color;
                
                float colorShiftCycle = _TimeParameter * _MusicBpmSpeed * 1.5;
                texColor.rgb = HueShift(texColor.rgb, colorShiftCycle + dist * 3.0);
                
                return texColor;
            }
            ENDHLSL
        }
    }
}
