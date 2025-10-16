Shader "Custom/VolumetricCloud"
{
    Properties
    {
        _BlitTexture("Main Texture", 2D) = "white" {}   

        _CloudColor("Cloud Color", Color) = (1,1,1,1)
        _Noise3D ("Noise3D", 3D) = "white" {}
        _NoiseScale ("Noise scale", float) = 32.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        
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
                float3 viewDir : TEXCOORD1;
            };

            struct planetData
            {
                float3 center;
                float radius; 
                float2 minMaxHeight;
                float thickness;
                float speed;
            };

            sampler2D _BlitTexture;

            fixed4 _CloudColor;
                
            #define NUM_OCTAVES 5
            float _NoiseScale;
            sampler3D _Noise3D;
            float4 _Noise3D_ST;

            StructuredBuffer<float> planetDataBuffer; 

            planetData samplePlanetData(int planetId)
            {
                planetData result;
                planetId = planetId * 8;

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

            bool intersectSphere(float3 ro, float3 rd, float3 center, float radius, out float t0, out float t1)
            {
                float3 oc = ro - center;
                float b = dot(oc, rd);
                float c = dot(oc, oc) - radius * radius;
                float h = b * b - c;
                if (h < 0.0) return false;
                h = sqrt(h);
                t0 = -b - h;
                t1 = -b + h;
                return true;
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
                // float depth = 0.0;
                float3 color = float3(0.0, 0.0, 0.0);
                float alpha = 0.0;

                float t0, t1;
                if (!intersectSphere(ro, rd, planet.center, planet.radius + planet.minMaxHeight.y, t0, t1))
                    return 0;

                t0 = max(t0, 0.0);
                float depth = t0;
            
                for (int i = 0; i < 80; i++)
                {
                    float3 p = ro + depth * rd;
                    float heightAboveSurface = length(p - planet.center) - planet.radius;

                    if (heightAboveSurface > planet.minMaxHeight.x && heightAboveSurface < planet.minMaxHeight.y)
                    {
                        float density = fbm(p * _NoiseScale);

                        if (density > 0.001)
                        {
                            float3 c = lerp(_CloudColor.rgb, 0, density);
                            float a = density * 0.4 * (1.0 - alpha);
                            a *= SampleNoise3D(p);
                            color += c * a;
                            alpha += a;
                        }
                    }

                    depth += max(0.1, 0.02 * depth);
                    if (alpha > 0.998) break;
                }

                return fixed4(saturate(color), saturate(alpha));
            }

            v2f vert (uint id : SV_VertexID)
            {
                v2f o;
                o.uv = float2((id << 1) & 2, id & 2);
                o.pos = float4(o.uv * float2(2, -2) + float2(-1, 1), 0, 1);

                float3 viewVector = mul(unity_CameraInvProjection, float4(o.uv * 2 - 1, 0, -1));
                o.viewDir = mul(unity_CameraToWorld, float4(viewVector, 0));
                return o;
            }

            #define MAX_PLANETS 15

            fixed4 frag (v2f i) : SV_Target
            {
                if (planetDataBuffer.Length == 0)
                    return tex2D(_BlitTexture, i.uv);

                fixed4 resultcolor = tex2D(_BlitTexture, i.uv);

                float viewLength = length(i.viewDir);

                float3 ro = _WorldSpaceCameraPos;
                float3 rd = i.viewDir / viewLength;

                fixed4 cloudColor = fixed4(0,0,0,0);
                for (int i = 0; i < planetDataBuffer.Length / 8; i++)
                {
                    planetData planet = samplePlanetData(i);
                    cloudColor += volumetricMarch(ro, rd, planet);
                }

                resultcolor.rgb = lerp(resultcolor.rgb, cloudColor.rgb, cloudColor.a);
                
                return resultcolor;
            }
            ENDCG
        }
    }
}
