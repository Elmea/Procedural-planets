using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetGen
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Chunk : MonoBehaviour
    {
        private int _Resolution;
        private double _Size;

        private Mesh _Mesh;
        private MeshFilter _MeshFilter;
        private MeshRenderer _MeshRenderer;

        private bool _IsWater;

        private Mesh.MeshDataArray _MeshDataArray;
        private Mesh.MeshData _MeshData;

        private NativeArray<float3> _Verts;
        private NativeArray<float3> _Normals;
        private NativeArray<float2> _Uvs;
        private NativeArray<float4> _Colors;

        float _PlanetRadius;
        double3 _QuadTreeBoundsCenter;
        double3 _QuadTreeSphereNoRotCenter;

        public void Initialize(int resolution, double chunkSize, 
            Material mat, bool isWater,
            float planetRadius, double3 qtCenter, double3 qtSphereNoRotCenter)
        {
            _Resolution = math.max(2, resolution);
            _Size = math.max(1f, chunkSize);

            _MeshFilter = GetComponent<MeshFilter>();
            _MeshRenderer = GetComponent<MeshRenderer>();

            _Mesh = new Mesh { name = "ChunkMesh" };
            _Mesh.MarkDynamic();
            _MeshFilter.sharedMesh = _Mesh;
            _MeshRenderer.sharedMaterial = mat;
            _PlanetRadius = planetRadius;
            _QuadTreeBoundsCenter = qtCenter;
            _QuadTreeSphereNoRotCenter = qtSphereNoRotCenter;
            _IsWater = isWater;
        }

        public void Dispose()
        {
            if (_Verts.IsCreated) _Verts.Dispose();
            if (_Uvs.IsCreated) _Uvs.Dispose();
        }

        public JobHandle ScheduleBuild(NativeArray<int> sharedTriangles, PlanetOptionsSO planetOptions)
        {
            // https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Mesh.MeshData.html
            _MeshDataArray = Mesh.AllocateWritableMeshData(1); // will be disposed by Apply
            _MeshData = _MeshDataArray[0];

            int vertCount = (_Resolution + 1) * (_Resolution + 1);
            int indexCount = sharedTriangles.Length;

            _MeshData.SetVertexBufferParams(vertCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, stream: 2),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, stream: 3)
            );
            _MeshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

            _Verts = new NativeArray<float3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // temp arrays that's disposed by apply
            _Normals = new NativeArray<float3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // temp arrays that's disposed by apply
            _Uvs = new NativeArray<float2>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // temp arrays that's disposed by apply
            _Colors = new NativeArray<float4>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // temp arrays that's disposed by apply

            var ib = _MeshData.GetIndexData<int>();
            ib.CopyFrom(sharedTriangles); // can do this here since it's shared and already built

            _MeshData.subMeshCount = 1;
            _MeshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles));

            int workerCount;
            try
            {
                workerCount = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount;
            }
            catch
            {
                workerCount = Mathf.Max(1, SystemInfo.processorCount - 1);
            }
            int targetBatches = workerCount * 2;
            int innerloopBatchCount = Mathf.CeilToInt(vertCount / targetBatches);
            innerloopBatchCount = ((innerloopBatchCount + 31) / 32) * 32; // multiple of 32 for better SIMD/cache

            JobHandle handle;
            // Schedule the vertex job
            if (_IsWater)
            {
                var flatJob = new BuildChunkFlatMeshJob
                {
                    Resolution = _Resolution,
                    Size = _Size,
                    PlanetRadius = _PlanetRadius,
                    QuadTreeSphereNoRotCenter = _QuadTreeSphereNoRotCenter,
                    QuadTreeBoundsCenter = _QuadTreeBoundsCenter,
                    WorldPos = (float3)transform.position,
                    WorldRot = (quaternion)transform.rotation,
                    Vertices = _Verts,
                    Normals = _Normals,
                    UVs = _Uvs,
                    Colors = _Colors,
                };
                handle = flatJob.Schedule(_Verts.Length, innerloopBatchCount);
                return handle;
            }
            else
            {
                var job = new BuildChunkMeshJob
                {
                    Resolution = _Resolution,
                    Size = _Size,
                    PlanetRadius = _PlanetRadius,
                    QuadTreeSphereNoRotCenter = _QuadTreeSphereNoRotCenter,
                    QuadTreeBoundsCenter = _QuadTreeBoundsCenter,
                    WorldPos = (float3)transform.position,
                    WorldRot = (quaternion)transform.rotation,

                    // Continent settings
                    ContinentWavelength = planetOptions.ContinentWavelength,
                    ContinentLacunarity = planetOptions.ContinentLacunarity,
                    ContinentPersistence = planetOptions.ContinentPersistence,
                    ContinentOctaves = planetOptions.ContinentOctaves,
                    WarpAmplitude = planetOptions.ContinentWarpAmplitude,
                    WarpFrequency = planetOptions.ContinentWarpFrequency,

                    // Land/Ocean basic settings
                    SeaLevel = planetOptions.SeaLevel,
                    SeaCoastLimit = planetOptions.SeaCoastLimit,
                    LandCoastLimit = planetOptions.LandCoastLimit,

                    BaseLandLevel = planetOptions.BaseLandLevel,
                    LandMaxHeight = planetOptions.LandMaxHeight,

                    ShelfDepth = planetOptions.ShelfDepth,
                    ShelfPortion = planetOptions.ShelfPortion,
                    ShelfSharpness = planetOptions.ShelfSharpness,
                    OceanMaxDepth = planetOptions.OceanMaxDepth,
                    OceanPlateauDepth = planetOptions.OceanPlateauDepth,

                    // Inland settings
                    LandHillRampLimit = planetOptions.LandHillRampLimit,
                    HillsWavelength = planetOptions.HillsWavelength,
                    HillsOctaves = planetOptions.HillsOctaves,
                    HillsPersistence = planetOptions.HillsPersistence,
                    HillsLacunarity = planetOptions.HillsLacunarity,
                    HillsAmplitudeMeters = planetOptions.HillsAmplitudeMeters,

                    MountainStart = planetOptions.MountainStart,
                    MountainRampLimit = planetOptions.MountainRampLimit,
                    MountainWavelength = planetOptions.MountainWavelength,
                    MountainOctaves = planetOptions.MountainOctaves,
                    MountainGain = planetOptions.MountainGain,
                    MountainLacunarity = planetOptions.MountainLacunarity,
                    MountainAmplitudeMeters = planetOptions.MountainAmplitudeMeters,

                    Vertices = _Verts,
                    Normals = _Normals,
                    UVs = _Uvs,
                    Colors = _Colors,
                };
                handle = job.Schedule(_Verts.Length, innerloopBatchCount);
            }

            // When job completes, copy into MeshData vertex buffers on main thread vis ApplyMesh
            // for now, it's called by ChunkManager after all jobs are completed
            return handle;
        }

        public void ApplyMesh()
        {
            if (!_Verts.IsCreated || !_Uvs.IsCreated)
                return;

            var vbPos = _MeshData.GetVertexData<float3>(0);
            var vbNorm = _MeshData.GetVertexData<float3>(1);
            var vbUv = _MeshData.GetVertexData<float2>(2);
            var vbColors = _MeshData.GetVertexData<float4>(3);

            vbPos.CopyFrom(_Verts);
            vbNorm.CopyFrom(_Normals);
            vbUv.CopyFrom(_Uvs);
            vbColors.CopyFrom(_Colors);

            Mesh.ApplyAndDisposeWritableMeshData(_MeshDataArray, _Mesh);
            _Verts.Dispose(); // must be disposed or else mem leaks
            _Uvs.Dispose(); // must be disposed or else mem leaks
            _Normals.Dispose(); // must be disposed or else mem leaks
            _Colors.Dispose(); // must be disposed or else mem leaks

            _Mesh.RecalculateNormals();
            _Mesh.RecalculateBounds();
        }

        [BurstCompile]
        private struct BuildChunkMeshJob : IJobParallelFor
        {
            public int Resolution;
            public float PlanetRadius;
            public double Size;
            public double3 WorldPos;
            public quaternion WorldRot;
            public double3 QuadTreeSphereNoRotCenter;
            public double3 QuadTreeBoundsCenter;

            public float ContinentWavelength;
            public float ContinentLacunarity;
            public float ContinentPersistence;
            public int ContinentOctaves;
            public float WarpAmplitude;
            public float WarpFrequency;

            public float SeaLevel;
            public float SeaCoastLimit;
            public float LandCoastLimit;

            public float BaseLandLevel;
            public float LandMaxHeight;

            public float ShelfDepth;
            public float ShelfPortion;
            public float ShelfSharpness;
            public float OceanPlateauDepth;
            public float OceanMaxDepth;

            public float LandHillRampLimit;
            public float HillsWavelength;
            public int HillsOctaves;
            public float HillsPersistence;
            public float HillsLacunarity;
            public float HillsAmplitudeMeters;

            public float MountainStart;
            public float MountainRampLimit;
            public float MountainWavelength;
            public int MountainOctaves;
            public float MountainGain;
            public float MountainLacunarity;
            public float MountainAmplitudeMeters;

            public NativeArray<float3> Vertices;
            public NativeArray<float2> UVs;
            public NativeArray<float3> Normals;
            public NativeArray<float4> Colors;

            public void Execute(int index)
            {
                int vertsPerSide = Resolution + 1;
                int y = index / vertsPerSide;
                int x = index % vertsPerSide;

                double fx = (double)x / Resolution;
                double fy = (double)y / Resolution;

                double xPos = (fx - 0.5) * Size + QuadTreeBoundsCenter.x;
                double zPos = (fy - 0.5) * Size + QuadTreeBoundsCenter.z;

                double3 planetPoint = new double3(xPos, PlanetRadius, zPos);
                float3 dir = math.normalize((float3)planetPoint);

                float3 noiseDir = math.mul(WorldRot, dir); // to have a 3d noise that wraps around the sphere instead of local to the face

                float3 posMeters = noiseDir * PlanetRadius; // pos at scale
                float continent = ContinentField(posMeters); // try to delimit continents
                float coastlineOffset = CoastBreaker(posMeters, PlanetRadius);

                float continentWithCoastline = continent + (0.05f * (coastlineOffset - 0.5f)); // try to get more interesting coastlines

                // 0 to 1 land mask
                float landMask = math.smoothstep(SeaCoastLimit, LandCoastLimit, continentWithCoastline);
                float land = CoastLandProfile(landMask);

                float hillsMask = math.smoothstep(LandCoastLimit, LandHillRampLimit, continentWithCoastline);
                land = HillsField(posMeters, land, hillsMask);

                float mountainsMask = math.smoothstep(MountainStart, MountainRampLimit, continentWithCoastline);
                land = MountainsField(posMeters, land, mountainsMask);

                float ocean = OceanProfile(landMask);

                float oceanFactor = 1f - landMask;
                float elevation = math.lerp(ocean, land, landMask);

                float3 p = dir * (PlanetRadius + elevation);

                Vertices[index] = p - (float3)QuadTreeSphereNoRotCenter;
                UVs[index] = new float2((float)fx, (float)fy);
                if (continentWithCoastline < SeaLevel)
                {
                    if (landMask > 0.0f)
                        Colors[index] = new float4(new float3(0.714f, 0.651f, 0.435f), 1.0f);
                    else
                        Colors[index] = new float4(new float3(0f, 0.067f, 0.102f), 1.0f);
                }
                else
                {
                    if (landMask < 1.0f)
                        Colors[index] = new float4(new float3(0.855f, 0.761f, 0.624f), 1.0f);
                    else
                    {
                        if (continentWithCoastline > 0.7f)
                            Colors[index] = new float4(new float3(0.275f, 0.247f, 0.18f), 1.0f);
                        else
                            Colors[index] = new float4(new float3(0.408f, 0.741f, 0.337f), 1.0f);
                    }
                }
            }

            float ContinentField(float3 posMeters)
            {
                float continentWavelength = PlanetRadius * ContinentWavelength;
                float baseFreq = 1f / continentWavelength;

                float3 pt = posMeters * baseFreq;
                float3 ptWarped = Warp(pt, WarpAmplitude, WarpFrequency);
                float height = FBM(ptWarped, ContinentLacunarity, ContinentOctaves, ContinentPersistence);

                return height;
            }

            float CoastLandProfile(float landMask)
            {
                float gradient = math.smoothstep(0f, 1f, landMask); // smoother 0 to 1
                return BaseLandLevel * gradient; // meters above sea
            }

            float OceanProfile(float landMask)
            {
                float oceanFactor = 1f - landMask;

                // fall off between 0 to shelfPortion/-shelfDepth
                float depthShelf;
                if (oceanFactor <= ShelfPortion)
                {
                    float t = oceanFactor / math.max(ShelfPortion, 1e-6f);
                    depthShelf = -ShelfDepth * math.smoothstep(0f, 1f, t);
                    return depthShelf; // still on the shelf
                }

                float t2 = (oceanFactor - ShelfPortion) / math.max(1f - ShelfPortion, 1e-6f);
                float finalCoef = math.pow(math.saturate(t2), math.max(ShelfSharpness, 1.0f));
                return math.lerp(-ShelfDepth, -OceanPlateauDepth, finalCoef);
            }

            // attempt to make a noise that looks like coastlines
            static float CoastBreaker(float3 posMeters, float planetRadiusMeters)
            {
                float wavelength = planetRadiusMeters * 0.10f;
                float freq = 1f / wavelength;

                float3 p = posMeters * freq;
                float3 pw = Warp(p, 0.5f, 2.0f);
                float r = FBM(pw, 2.0f, 4, 0.5f);
                r = 0.5f * (r + 1f);
                return r;
            }

            float HillsField(float3 posMeters, float coastValue, float hillsMask)
            {
                float hillsWavelength = PlanetRadius * HillsWavelength;
                float baseFreq = 1f / math.max(hillsWavelength, 1e-6f);
                float3 pt = posMeters * baseFreq;
                //float3 ptWarped = Warp(pt, HillsWarpAmplitude, HillsWarpFrequency);
                float hillsValue = FBM(pt, HillsLacunarity, HillsOctaves, HillsPersistence) * HillsAmplitudeMeters + BaseLandLevel;

                return math.lerp(coastValue, hillsValue, hillsMask);
            }

            float MountainsField(float3 posMeters, float hillsValue, float mountainsMask)
            {
                float mountainWavelength = PlanetRadius * MountainWavelength;
                float baseFreq = 1f / math.max(mountainWavelength, 1e-6f);
                float3 pt = posMeters * baseFreq;
                //float3 ptWarped = Warp(pt, MountainWarpAmplitude, MountainWarpFrequency);
                float mountainsValue = RidgedFBM(pt, MountainLacunarity, MountainOctaves, MountainGain) * MountainAmplitudeMeters + hillsValue;

                return math.lerp(hillsValue, mountainsValue, mountainsMask);
            }

            // FBM between 0 and 1
            static float FBM(float3 pt, float lacunarity, int octaves, float persistence)
            {
                float a = 1f;
                float amplitude = 0f;
                float sum = 0f;
                float3 q = pt;
                for (int i = 0; i < octaves; i++)
                {
                    sum += a * noise.snoise(q);
                    amplitude += a;
                    q *= lacunarity;
                    a *= persistence;
                }
                return (sum / math.max(amplitude, 1e-6f)) * 0.5f + 0.5f;
            }

            static float RidgedFBM(float3 pt, float lacunarity, int octaves, float gain)
            {
                float a = 1f;
                float amplitude = 0f;
                float sum = 0f;
                float3 q = pt;
                for (int i = 0; i < octaves; i++)
                {
                    float n = 1f - math.abs(noise.snoise(q));
                    sum += a * n;
                    amplitude += a;
                    q *= lacunarity;
                    a *= gain;
                }
                return sum / math.max(amplitude, 1e-6f);
            }

            static float3 Warp(float3 pt, float amplitude, float frequency)
            {
                float3 w;
                w.x = noise.snoise(pt * frequency + new float3(37.2f, 15.7f, 91.1f));
                w.y = noise.snoise(pt * frequency + new float3(-12.3f, 44.5f, 7.9f));
                w.z = noise.snoise(pt * frequency + new float3(9.4f, -55.6f, 23.3f));
                return pt + amplitude * w;
            }
        }

        [BurstCompile]
        private struct BuildChunkFlatMeshJob : IJobParallelFor
        {
            public int Resolution;
            public float PlanetRadius;
            public double Size;
            public double3 WorldPos;
            public quaternion WorldRot;
            public double3 QuadTreeSphereNoRotCenter;
            public double3 QuadTreeBoundsCenter;

            public NativeArray<float3> Vertices;
            public NativeArray<float2> UVs;
            public NativeArray<float3> Normals;
            public NativeArray<float4> Colors;

            public void Execute(int index)
            {
                int vertsPerSide = Resolution + 1;
                int y = index / vertsPerSide;
                int x = index % vertsPerSide;

                double fx = (double)x / Resolution;
                double fy = (double)y / Resolution;

                double xPos = (fx - 0.5) * Size + QuadTreeBoundsCenter.x;
                double zPos = (fy - 0.5) * Size + QuadTreeBoundsCenter.z;

                double3 planetPoint = new double3(xPos, PlanetRadius, zPos);
                float3 dir = math.normalize((float3)planetPoint);

                float3 p = dir * PlanetRadius;

                Vertices[index] = p - (float3)QuadTreeSphereNoRotCenter;
                UVs[index] = new float2((float)fx, (float)fy);
                Normals[index] = dir;
                Colors[index] = new float4(1f, 1f, 1f, 1f);
            }
        }
    }
}
