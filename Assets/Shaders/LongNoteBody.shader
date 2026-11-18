// 인게임 롱노트 몸통 (기획서 3-4 「인게임 톤 개편 (11/18)」: 흰색 반투명, 아래 85% → 위 55%).
// - SpriteRenderer(Draw Mode Sliced)용 안 받는(unlit) 스프라이트 셰이더. 색 = 스프라이트 × 정점 색(SpriteRenderer.color).
// - 세로 그라데이션: t = 메시 로컬 y ÷ _Height + 0.5 (0 = 아래 끝 머리, 1 = 위 끝 꼬리), 알파 × lerp(_BottomAlpha, _TopAlpha, t).
//   _Height(막대 높이)는 LongNoteView가 MaterialPropertyBlock으로 넘긴다. 롱노트 길이가 바뀌어도 막대 전체에 같은 그라데이션이 걸린다.
//   식은 UITheme.LongNoteAlphaAt과 같다 (EditMode 테스트가 그 함수를 본다). 값은 UITheme 인게임 묶음.
// - URP 2D 렌더러가 그리도록 Universal2D 패스 하나만 둔다.
Shader "Kairos/LongNoteBody"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TopAlpha ("Top Alpha", Range(0, 1)) = 0.55
        _BottomAlpha ("Bottom Alpha", Range(0, 1)) = 0.85
        _Height ("Body Height", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _TopAlpha;
                float _BottomAlpha;
                float _Height;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float t : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                output.t = saturate(input.positionOS.y / max(_Height, 1e-4) + 0.5);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                color.a *= lerp(_BottomAlpha, _TopAlpha, input.t);
                return color;
            }
            ENDHLSL
        }
    }
}
