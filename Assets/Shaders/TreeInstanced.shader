Shader "Custom/TreeInstanced"
{
    Properties
    {
        _MainTex    ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic   ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Glossiness;
            float _Metallic;

            StructuredBuffer<float4x4> _TransformBuffer;
            uniform float4x4 _ObjectToWorld;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos          : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldPos     : TEXCOORD1;
                float3 worldNormal  : TEXCOORD2;
            };

            v2f vert(appdata v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                uint instanceID = GetIndirectInstanceID(svInstanceID);

                float4x4 inst = _TransformBuffer[instanceID];
                float4x4 model = mul(_ObjectToWorld, inst);
                float4 worldPos = mul(model, v.vertex);
                float3 worldNormal = normalize(mul((float3x3)model, v.normal));

                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = worldPos.xyz;
                o.worldNormal = worldNormal;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 albedo = tex2D(_MainTex, i.uv).rgb;

                float3 N = normalize(i.worldNormal);
                float3 L = _WorldSpaceLightPos0.xyz;
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                float ndotl = saturate(dot(N, L));
                float3 diffuse = albedo * _LightColor0.rgb * ndotl;

                float3 ambient = ShadeSH9(float4(N, 1.0));

                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), lerp(16, 128, _Glossiness));
                float3 specCol = _LightColor0.rgb * spec * lerp(0.04, 1.0, _Metallic);

                float3 color = diffuse + ambient * albedo + specCol;
                return float4(color, 1);
            }
            ENDCG
        }
    }
}
