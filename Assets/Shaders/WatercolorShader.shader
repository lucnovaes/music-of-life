Shader "mil/Watercolor"
{
    Properties
    {
        [MainTexture] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint Color", Color) = (1,1,1,1)
        
        [Header(Watercolor Settings)]
        _NoiseMap("Wobble Noise (Greyscale)", 2D) = "gray" {}
        _PaperTex("Paper Grain Texture", 2D) = "white" {}
        _Distortion("Edge Wobble Intensity", Range(0, 0.05)) = 0.015
        _PaperFrequency("Paper Grain Scale", Range(1, 20)) = 5.0
        
        [Header(Paint Effects)]
        _EdgeThickness("Water Fringe Border", Range(0.01, 0.1)) = 0.03
        _EdgeDarkness("Water Fringe Intensity", Range(0, 2)) = 1.3
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
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float2 screenUV     : TEXCOORD1;
            };

            Texture2D _MainTex;         SamplerState sampler_MainTex;
            Texture2D _NoiseMap;        SamplerState sampler_NoiseMap;
            Texture2D _PaperTex;        SamplerState sampler_PaperTex;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Distortion;
                float _PaperFrequency;
                float _EdgeThickness;
                float _EdgeDarkness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Transforma a geometria plana 2D padrão para a tela
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                
                // Repassa os dados de cor do Sprite Renderer (essencial para Flip e Tint do Sprite)
                output.color = input.color * _Color;
                output.uv = input.uv;
                
                // Calcula UVs de tela nativas da URP para projetar a textura do papel de aquarela fixa em tela
                float4 screenPos = ComputeScreenPos(vertexInput.positionCS);
                output.screenUV = screenPos.xy / max(screenPos.w, 0.00001);
                
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Distorção de Bordas (Efeito de tinta sangrando nas fibras do papel)
                // Amostra o ruído usando as coordenadas locais do Sprite
                float2 noise = _NoiseMap.Sample(sampler_NoiseMap, input.uv).rg * 2.0 - 1.0;
                float2 distortedUV = input.uv + noise * _Distortion;
                
                // 2. Amostragem do Sprite com UV distorcida
                float4 spriteColor = _MainTex.Sample(sampler_MainTex, distortedUV) * input.color;
                
                // Corta o pixel imediatamente se o alfa for nulo para evitar processamento inútil
                if (spriteColor.a < 0.001) discard;

                // 3. Efeito Water Fringe (Acúmulo de pigmento escuro nos limites opacos da imagem)
                // Como não há iluminação 3D, simulamos a borda da aquarela checando a opacidade vizinha através do ruído
                float borderAlpha = _MainTex.Sample(sampler_MainTex, distortedUV + noise * _EdgeThickness).a;
                
                // Detecta onde o alfa transiciona (borda do desenho do Sprite)
                float edgeMask = smoothstep(0.1, 0.9, spriteColor.a) * (1.0 - smoothstep(0.8, 1.0, borderAlpha));
                float3 edgeDarkening = lerp(float3(1.0, 1.0, 1.0), float3(0.3, 0.3, 0.3) * _EdgeDarkness, edgeMask);

                // 4. Textura de Papel Fixo em Tela (Dá a ilusão de pintura física estacionária)
                float4 paperGrain = _PaperTex.Sample(sampler_PaperTex, input.screenUV * _PaperFrequency);

                // 5. Composição Final (Subtrativa / Multiplicativa)
                float3 finalRGB = spriteColor.rgb;
                finalRGB *= edgeDarkening;    // Escurece os contornos externos do sprite
                finalRGB *= paperGrain.rgb;   // Aplica os relevos e porosidades do papel

                return float4(finalRGB, spriteColor.a);
            }
            ENDHLSL
        }
    }
}