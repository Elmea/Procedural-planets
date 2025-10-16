Shader "Custom/VolumetricCloud"
{
    Properties
    {
        _MainTex("Main Texture", 2DArray) = "grey" {}
        _CloudColor("Cloud Color", Color) = (1,1,1,1)

        _Noise3D ("Noise3D", 3D) = "white" {}
        _NoiseScale ("Noise scale", float) = 32.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            struct planetData
            {
                float3 center;
                float radius; 
                float2 minMaxHeight;
                float thickness;
                float speed;
            };

            sampler2D _MainTex;
            fixed4 _CloudColor;
                
            #define NUM_OCTAVES 5
            float _NoiseScale;
            sampler3D _Noise3D;
            float4 _Noise3D_ST;

            StructuredBuffer<float> planetDataBuffer; 

            planetData samplePlanetData(int planetId)
            {
                planetData result;

                result.center = float3(planetDataBuffer[planetId], planetDataBuffer[planetId+1], planetDataBuffer[planetId+2]);
                result.radius = planetDataBuffer[planetId+3];
                result.minMaxHeight = float2(planetDataBuffer[planetId+4], planetDataBuffer[planetId+5]);
                result.thickness = planetDataBuffer[planetId+6];
                result.speed = planetDataBuffer[planetId+7];

                return result;
            }

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

                return saturate(v);
            }

            fixed4 volumetricMarch(float3 ro, float3 rd, planetData planet)
            {
                float depth = 0.0;
                float3 color = float3(0.0, 0.0, 0.0);
                float alpha = 0.0;
            
                for (int i = 0; i < 100; i++)
                {
                    float3 p = ro + depth * rd;
                    float heightAboveSurface = length(p - planet.center) - planet.radius;

                    if (heightAboveSurface > planet.minMaxHeight.x && heightAboveSurface < planet.minMaxHeight.y)
                    {
                        float density = fbm(p * _Time * planet.speed);

                        if (density > 0.001)
                        {
                            float3 c = lerp(_CloudColor.rgb, 0, density);
                            float a = density * 0.4 * (1.0 - alpha);
                            color += c * a;
                            alpha += a;
                        }
                    }

                    depth += max(0.05, 0.02 * depth);
                }

                return fixed4(saturate(color), saturate(alpha));
            }

            v2f vert (uint id : SV_VertexID)
            {
                v2f o;
                o.uv = float2((id << 1) & 2, id & 2);
                o.pos = float4(o.uv * 2 - 1, 0, 1);
                return o;
            }

            #define MAX_PLANETS 15

            fixed4 frag (v2f i) : SV_Target
            {
                // return fixed4 (1.0, 0.0, 0.0, 1.0);
                // return tex2D(_MainTex, i.uv);
                if (planetDataBuffer.Length == 0)
                    return tex2D(_MainTex, i.uv);

                fixed4 resultcolor = tex2D(_MainTex, i.uv);

                // Construction du rayon en world space
                float2 ndc = i.uv * 2.0 - 1.0;
                float4 clip = float4(ndc, 1.0, 1.0);
                float4 view = mul(unity_CameraInvProjection, clip);
                view /= view.w;
                float3 rd = normalize(mul((float3x3)unity_CameraToWorld, view.xyz));
                float3 ro = _WorldSpaceCameraPos;
                
                fixed4 cloudColor = volumetricMarch(ro, rd, samplePlanetData(0));
                return cloudColor;

                // #ifdef SHADER_API_D3D11
                for (int i = 0; i < planetDataBuffer.Length / 8; i++)
                {
                    planetData planet = samplePlanetData(i);
                    fixed4 cloudColor = volumetricMarch(ro, rd, planet);
                    resultcolor.rgb = lerp(resultcolor.rgb, cloudColor.rgb, cloudColor.a * 0.6);
                    // resultcolor = resultcolor * cloudColor;
                }
                // #endif
                
                return resultcolor;
            }
            ENDCG
        }
    }
}
