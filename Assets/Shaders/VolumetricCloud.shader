// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/VolumetricCloud"
{
    Properties
    {
        _CloudColor("Cloud Color", Color) = (1,1,1,1)

        _Noise3D ("Noise3D", 3D) = "white" {}
        _NoiseScale ("Noise scale", float) = 32.0

        _PlanetRadius("Planet Radius", float) = 1.0
        _CloudHeight("Cloud Base Height", float) = 0.05
        _CloudThickness("Cloud Thickness", float) = 0.1

        _Speed("Cloud speed", float) = 0.01

    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION; 
                float3 worldPos : TEXCOORD0;
            };
            
            fixed4 _CloudColor;

            float _PlanetRadius;
            float _CloudHeight;
            float _CloudThickness;

            #define NUM_OCTAVES 5
            float _NoiseScale;
            sampler3D _Noise3D;
            float4 _Noise3D_ST;
            float _Speed;

            float SampleNoise3D(float3 pos)
            {
                float3 uvw = frac(pos * _NoiseScale);
                float value = tex3D(_Noise3D, uvw).r;

                return value;
            }

            float fbm(float3 st)
            {
                float v = 0.0;
                float a = 0.5;
                float3 shift = float3(100.0, 100.0, 100.0);

                float cosRot = cos(0.5);
                float sinRot = sin(0.5);
                float3x3 rot = float3x3(
                                cosRot, 0, sinRot,
                                0,      1, 0,
                                -sinRot,0, cosRot);

                for (int i = 0; i < NUM_OCTAVES; i++)
                {
                    v += a * SampleNoise3D(st);
                    st = mul(rot, st) * 2.0 + shift;
                    a *= 0.5;
                }

                return v;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }


            fixed4 frag (v2f i) : SV_Target
            {
                float4 objectOrigin = mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0) );
                float3 dir = normalize(i.worldPos - objectOrigin.xyz);

                float heightAboveSurface = length(i.worldPos) - _PlanetRadius;

                float density = smoothstep(_CloudHeight, _CloudHeight + _CloudThickness, heightAboveSurface);

                float3 cloudPos = dir * (_PlanetRadius + _CloudHeight);
                density *= fbm(cloudPos + _Speed * _Time);
                // return fixed4(fbm(cloudPos), 0.0, 0.0, 1.0);
                density = saturate(density);

                return fixed4(_CloudColor.rgb * density, density);
            }
            ENDCG
        }
    }
}
