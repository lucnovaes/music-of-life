Shader "mil/LiquidVector"
{
    Properties
    {
        // [PerRendererData] avisa a Unity para injetar o Sprite do Animator aqui dentro automaticamente
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _NoiseMap ("Noise Texture", 2D) = "white" {}
        _DistortionStrength ("Distortion Strength", Float) = 0.015
        _NoiseScale ("Noise Scale", Float) = 4.0
        
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
            "CanUseSpriteAtlas"="True" // OBRIGATÓRIO: Permite que o shader leia Sprites cortados em Atlas
        }
        
        Cull Off 
        Lighting Off 
        ZWrite Off 
        Blend SrcAlpha OneMinusSrcAlpha

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

            // Declaração de texturas seguindo o padrão correto da URP
            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            
            Texture2D _NoiseMap;
            SamplerState sampler_NoiseMap;

            // VARIÁVEIS TÉCNICAS OBRIGATÓRIAS DO COMPONENTE DE SPRITE DA UNITY:
            // Se o SpriteRenderer usar um Atlas ou corte, a Unity injeta a escala/offset aqui.
            // Sem isso, a matemática de UV do Sprite quebra e fica estática!
            float4 _MainTex_ST; 

            float _DistortionStrength;
            float _NoiseScale;
            float _TimeParameter;
            float _MusicBpmSpeed;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // Aplica a escala e o offset corretos do Sprite da Unity na coordenada UV
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                
                output.color = input.color; 
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Move o ruído no tempo do FMOD
                float2 noiseUv = input.uv * _NoiseScale + (_TimeParameter * _MusicBpmSpeed * 0.1);
                float noise = _NoiseMap.Sample(sampler_NoiseMap, noiseUv).r;
                
                // Distorce as coordenadas UV
                float2 distortedUv = input.uv + (noise - 0.5) * _DistortionStrength;
                
                // Amostra o Sprite e garante que pixels transparentes cortem corretamente (Alpha Clipping básico)
                float4 texColor = _MainTex.Sample(sampler_MainTex, distortedUv) * input.color;
                
                return texColor;
            }
            ENDHLSL
        }
    }
}
