Shader "mil/HandDrawnSketch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _PaperTex ("Paper Texture", 2D) = "white" {}
        
        // 1. ADDED: Slider to control the Paper Texture's opacity in the Inspector
        _PaperAlpha ("Paper Alpha", Range(0.0, 1.0)) = 1.0 
        
        _SketchJitter ("Sketch Jitter Strength", Float) = 0.004
        _TimeParameter ("FMOD Audio Time", Float) = 0.0
        _FrameRate ("Animation Frame Rate", Float) = 12.0
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
            Texture2D _PaperTex;
            SamplerState sampler_PaperTex;

            float4 _MainTex_ST; 

            // 2. ADDED: Matching uniform variable for the slider
            float _PaperAlpha; 

            float _SketchJitter;
            float _TimeParameter;
            float _FrameRate;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float posterizedTime = floor(_TimeParameter * _FrameRate) / _FrameRate;
                
                float2 jitter = float2(
                    sin(posterizedTime * 15.0 + input.uv.y * 10.0),
                    cos(posterizedTime * 20.0 + input.uv.x * 12.0)
                ) * _SketchJitter;
                
                float2 distortedUv = input.uv + jitter;
                
                float4 texColor = _MainTex.Sample(sampler_MainTex, distortedUv) * input.color;
                float4 paperColor = _PaperTex.Sample(sampler_PaperTex, input.uv);
                
                // 3. UPDATED: Apply transparency control
                // Using lerp allows you to smoothly blend the paper texture effect to a neutral white (1,1,1,1) 
                // based on your slider, meaning at 0 alpha, the paper texture has no multiplying effect.
                float4 finalPaper = lerp(float4(1.0, 1.0, 1.0, 1.0), paperColor, _PaperAlpha);
                
                return texColor * finalPaper;
            }
            ENDHLSL
        }
    }
}
