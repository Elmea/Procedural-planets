Shader "Custom/TreeInstanced"
{
    Properties
    {
        _MainTex    ("Albedo (RGB)", 2D) = "white" {}
        _MaskTex    ("Roughness-Metallic Map", 2D) = "white" {}
        _BumpMap    ("Normal Map", 2D) = "bump" {}
        [Enum(Off,0,Front,1,Back,2)] _Cull("Cull Mode", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardPass"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            
            ZWrite True
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _FORWARD_PLUS
            #pragma shader_feature_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            CBUFFER_START(UnityPerMaterial)
            uniform float4x4 _ObjectToWorld;
            float4 _MainTex_ST;
            CBUFFER_END
            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _BumpMap;

            StructuredBuffer<float4x4> _TransformBuffer;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos           : SV_POSITION;
                float2 uv            : TEXCOORD0;
                float3 worldPos      : TEXCOORD1;
                float3 worldNormal   : TEXCOORD2;
                float3 worldTangent  : TEXCOORD3;
                float3 worldBitangent: TEXCOORD4;
            };

            v2f vert(appdata v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                uint instanceID = GetIndirectInstanceID(svInstanceID);

                float4x4 inst = _TransformBuffer[instanceID];
                float4x4 model = mul(_ObjectToWorld, inst);
                float4 worldPos = mul(model, v.vertex);

                float3 worldNormal  = normalize(mul((float3x3)model, v.normal));
                float3 worldTangent = normalize(mul((float3x3)model, v.tangent.xyz));
                float3 worldBitangent = normalize(cross(worldNormal, worldTangent) * v.tangent.w);

                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = worldPos.xyz;

                o.worldNormal = worldNormal;
                o.worldTangent = worldTangent;
                o.worldBitangent = worldBitangent;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 albedo = tex2D(_MainTex, i.uv).rgb;
                float alpha = tex2D(_MainTex, i.uv).a;
                if (alpha < 0.1)
                    discard;

                float4 maskSample = tex2D(_MaskTex, i.uv);
                float3 tNormal = UnpackNormal(tex2D(_BumpMap, i.uv));
                
                InputData lighting = (InputData)0;
                SurfaceData surface = (SurfaceData)0;
                lighting.positionCS = i.pos;
                lighting.positionWS = i.worldPos;
                lighting.normalWS = normalize(i.worldNormal);
                lighting.viewDirectionWS = GetWorldSpaceViewDir(i.worldPos);
                lighting.shadowCoord = TransformWorldToShadowCoord(i.worldPos);
                
                surface.albedo = albedo;
                surface.alpha = alpha;
                surface.metallic = 1-maskSample.b;
                surface.smoothness = 1-maskSample.g;
                surface.occlusion = maskSample.a;
                surface.normalTS = tNormal;
                surface.specular = float3(0.04, 0.04, 0.04);
                
                return UniversalFragmentPBR(lighting, surface);
            }
            ENDHLSL
        }
    }
}
