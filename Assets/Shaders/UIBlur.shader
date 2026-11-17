// 곡 선택 배경 블러 (기획서 3-3 ⑨). UIBlurBackground가 Graphics.Blit으로 쓴다.
// - 0번 패스: 가로, 1번 패스: 세로. 9탭 가우시안을 선형 보간 5번 읽기로 계산한다.
// - _Radius: 탭 간격 배율 (클수록 더 흐림). 머티리얼(Assets/Materials/UIBlur.mat) 값으로 정한다.
// - 카메라 렌더링 밖에서 Blit으로만 쓰므로 URP에서도 내장 UnityCG 함수로 충분하다.
Shader "Kairos/UIBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Radius", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        float _Radius;

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vert(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord;
            return o;
        }

        half4 Blur(float2 uv, float2 direction)
        {
            float2 offset = direction * _MainTex_TexelSize.xy * _Radius;
            half4 color = tex2D(_MainTex, uv) * 0.2270270;
            color += (tex2D(_MainTex, uv + offset * 1.3846154) + tex2D(_MainTex, uv - offset * 1.3846154)) * 0.3162162;
            color += (tex2D(_MainTex, uv + offset * 3.2307692) + tex2D(_MainTex, uv - offset * 3.2307692)) * 0.0702703;
            return color;
        }
        ENDCG

        Pass
        {
            Name "BlurHorizontal"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return Blur(i.uv, float2(1, 0)); }
            ENDCG
        }

        Pass
        {
            Name "BlurVertical"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return Blur(i.uv, float2(0, 1)); }
            ENDCG
        }
    }
}
