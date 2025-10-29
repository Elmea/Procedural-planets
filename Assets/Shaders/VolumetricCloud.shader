Shader "Custom/VolumetricCloud"
{
    Properties
    {
        _BlitTexture("Main Texture", 2D) = "white" {}   

        _CloudColor("Cloud Color", Color) = (1,1,1,1)
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
                float cloudSize;
                float speed;
            };

            sampler2D _BlitTexture;
            sampler2D _CameraDepthTexture;

            fixed4 _CloudColor;
                
            #define NUM_OCTAVES 10

            StructuredBuffer<float> planetDataBuffer; 

            planetData samplePlanetData(int planetId)
            {
                planetData result;
                planetId = planetId * 8;

                result.center = float3(planetDataBuffer[planetId], planetDataBuffer[planetId+1], planetDataBuffer[planetId+2]);
                result.radius = planetDataBuffer[planetId+3];
                result.minMaxHeight = float2(planetDataBuffer[planetId+4], planetDataBuffer[planetId+5]);
                result.cloudSize = planetDataBuffer[planetId+6];
                result.speed = planetDataBuffer[planetId+7];

                return result;
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

            float rand(float3 p) 
            {
                return frac(sin(dot(p, float3(12.345, 67.89, 412.12))) * 42123.45) * 2.0 - 1.0;
            }

            float fbmNoise(float3 p) 
            {
                float3 u = floor(p);
                float3 v = frac(p);
                float3 s = smoothstep(0.0, 1.0, v);
    
                float a = rand(u);
                float b = rand(u + float3(1.0, 0.0, 0.0));
                float c = rand(u + float3(0.0, 1.0, 0.0));
                float d = rand(u + float3(1.0, 1.0, 0.0));
                float e = rand(u + float3(0.0, 0.0, 1.0));
                float f = rand(u + float3(1.0, 0.0, 1.0));
                float g = rand(u + float3(0.0, 1.0, 1.0));
                float h = rand(u + float3(1.0, 1.0, 1.0));
    
                return lerp(lerp(lerp(a, b, s.x), lerp(c, d, s.x), s.y),
                           lerp(lerp(e, f, s.x), lerp(g, h, s.x), s.y),
                           s.z);
            }

            float fbm(float3 p)
            {
                float3 q = p;
                float weight = 0.5;
                float ret = 0.0;
    
                for (int i = 0; i < NUM_OCTAVES; i++)
                {
                    ret += weight * fbmNoise(q); 
                    q *= 2.0;
                    weight *= 0.5;
                }
                return clamp(ret, 0.0, 1.0);
            }

            float uvDepth;
            bool DepthTest(float3 pos)
            {
                float4 clipPos = UnityObjectToClipPos(pos);
                float zDepth = clipPos.z / clipPos.w;
                
                #ifndef UNITY_REVERSED_Z // OpenGL
                zDepth = zDepth * 0.5 + 0.5;
                #endif

                return zDepth > uvDepth;
            }

            fixed4 volumetricMarch(float3 ro, float3 rd, planetData planet)
            {
                float3 color = float3(0.0, 0.0, 0.0);
                float alpha = 0.0;

                float t0, t1;
                
                if (!intersectSphere(ro, rd, planet.center, planet.radius + planet.minMaxHeight.y, t0, t1))
                    return 0;

                t0 = max(t0, 0.0);
                float depth = 100;
            
                for (int i = 0; i < 1500; i++)
                {
                    float3 p = ro + depth * rd;
                    
                    if (!DepthTest(p))
                        break;

                    float heightAboveSurface = abs(length(p - planet.center) - planet.radius);

                    if (heightAboveSurface > planet.minMaxHeight.x && heightAboveSurface < planet.minMaxHeight.y)
                    {
                        float density = fbm((p + planet.speed * _Time) / planet.cloudSize);   

                        if (density > 0.001)
                        {
                            float3 c = lerp(_CloudColor.rgb, 0, density);
                            float a = density * 0.4 * (1.0 - alpha);
                            color += c * a;
                            alpha += a;
                        }
                    }

                    depth += max(0.0065, 0.0055 * depth);
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

                uvDepth = tex2D(_CameraDepthTexture, i.uv);

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
