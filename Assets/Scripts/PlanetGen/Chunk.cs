using Codice.Client.BaseCommands.Changelist;
using System.IO;
using System.Runtime.CompilerServices;
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

        private Mesh.MeshDataArray _MeshDataArray;
        private Mesh.MeshData _MeshData;

        private NativeArray<float3> _Verts;
        private NativeArray<float3> _Normals;
        private NativeArray<float2> _Uvs;
        private NativeArray<float4> _Colors;

        float _PlanetRadius;
        double3 _QuadTreeBoundsCenter;
        double3 _QuadTreeSphereNoRotCenter;

        public void Initialize(int resolution, double chunkSize, Material mat, float planetRadius, double3 qtCenter, double3 qtSphereNoRotCenter)
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

            // Schedule the vertex job
            var job = new BuildChunkMeshJob
            {
                Resolution = _Resolution,
                Size = _Size,
                PlanetRadius = _PlanetRadius,
                QuadTreeSphereNoRotCenter = _QuadTreeSphereNoRotCenter,
                QuadTreeBoundsCenter = _QuadTreeBoundsCenter,
                WorldPos = (float3)transform.position,
                WorldRot = (quaternion)transform.rotation,

                ContinentWavelength = planetOptions.ContinentWavelength,
                ContinentLacunarity = planetOptions.ContinentLacunarity,
                ContinentPersistence = planetOptions.ContinentPersistence,
                ContinentOctaves = planetOptions.ContinentOctaves,
                WarpAmplitude = planetOptions.ContinentWarpAmplitude,
                WarpFrequency = planetOptions.ContinentWarpFrequency,

                SeaLevel = planetOptions.SeaLevel,
                SeaCoastWidth = planetOptions.SeaCoastWidth,
                LandCoastWidth = planetOptions.LandCoastWidth,

                BaseLandLevel = planetOptions.BaseLandLevel,
                LandMaxHeight = planetOptions.LandMaxHeight,

                ShelfDepth = planetOptions.ShelfDepth,
                ShelfPortion = planetOptions.ShelfPortion,
                ShelfSharpness = planetOptions.ShelfSharpness,
                OceanMaxDepth = planetOptions.OceanMaxDepth,
                OceanPlateauDepth = planetOptions.OceanPlateauDepth,

                Vertices = _Verts,
                Normals = _Normals,
                UVs = _Uvs,
                Colors = _Colors,
            };

            JobHandle handle = job.Schedule(_Verts.Length, 128);

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
            public float SeaCoastWidth;
            public float LandCoastWidth;

            public float BaseLandLevel;
            public float LandMaxHeight;

            public float ShelfDepth;
            public float ShelfPortion;
            public float ShelfSharpness;
            public float OceanPlateauDepth;
            public float OceanMaxDepth;

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
                //float baseHeight = FMB(noiseDir * Frequency * PlanetRadius, Lacunarity, Octaves, Persistence);
                //float height = baseHeight * Amplitude;

                float3 posMeters = noiseDir * PlanetRadius; // pos at scale
                float continent = ContinentField(posMeters, PlanetRadius); // try to delimit continents
                float coastlineOffset = CoastBreaker(posMeters, PlanetRadius);

                float continentWithCoastline = continent + (0.05f * (coastlineOffset - 0.5f)); // try to get more interesting coastlines

                float landMask = MakeLandMask(continentWithCoastline);

                float land = CoastLandProfile(landMask, 0, BaseLandLevel);
                float ocean = CoastOceanProfile(landMask, ShelfPortion, ShelfDepth, OceanPlateauDepth, ShelfSharpness);

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
                        Colors[index] = new float4(new float3(0.408f, 0.741f, 0.337f), 1.0f);
                }
            }

            float ContinentField(float3 posMeters, float planetRadiusMeters)
            {
                float continentWavelength = planetRadiusMeters * ContinentWavelength;
                float baseFreq = 1f / continentWavelength;

                float3 pt = posMeters * baseFreq;
                float3 ptWarped = Warp(pt, WarpAmplitude, WarpFrequency);
                float height = ContinentFBM(ptWarped, ContinentLacunarity, ContinentOctaves, ContinentPersistence);

                return height;
            }

            float MakeLandMask(float continent)
            {
                float edgeMin = math.saturate(SeaLevel - 0.5f * SeaCoastWidth);
                float edgeMax = math.saturate(SeaLevel + 0.5f * LandCoastWidth);
                return math.smoothstep(edgeMin, edgeMax, continent);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static float CoastLandProfile(float m, float baseLand, float landRelief)
            {
                // m in [0..1]; push most of the relief inland using a smooth curve
                float g = math.smoothstep(0f, 1f, m);
                return baseLand + landRelief * g; // meters above sea
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static float CoastOceanProfile(float m, float shelfPortion, float shelfDepth, float oceanDepth, float sharpness)
            {
                // m in [0..1], oceanFactor is 1-m (0 = shore, 1 = far ocean)
                float o = 1f - m;

                // gentle shelf portion [0..shelfPortion] goes from 0..-shelfDepth with a smooth curve
                float depthShelf;
                if (o <= shelfPortion)
                {
                    float t = o / math.max(shelfPortion, 1e-6f);
                    depthShelf = -shelfDepth * math.smoothstep(0f, 1f, t);
                    return depthShelf; // still on the shelf
                }

                // beyond shelf: sharp fall to -OceanDepth
                float t2 = (o - shelfPortion) / math.max(1f - shelfPortion, 1e-6f);
                float w = math.pow(math.saturate(t2), math.max(sharpness, 1.0f));
                return math.lerp(-shelfDepth, -oceanDepth, w);
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

            static float ContinentFBM(float3 pt, float lacunarity, int octave, float persistence)
            {
                float a = 1f, sum = 0f, amplitude = 0f;
                float3 q = pt;
                for (int i = 0; i < octave; i++)
                {
                    sum += a * (1f - (noise.snoise(q) * 0.5f + 0.5f));
                    amplitude += a;
                    q *= lacunarity;
                    a *= persistence;
                }
                return sum / math.max(amplitude, 1e-6f);
            }

            static float FBM(float3 pt, float lacunarity, int octaves, float persistence)
            {
                float a = 1f;
                float amplitude = 0f;
                float sum = 0f;
                float3 q = pt * lacunarity;
                for (int i = 0; i < octaves; i++)
                {
                    sum += a * (noise.snoise(q) * 0.5f + 0.5f);
                    amplitude += a;
                    q *= 2f;
                    a *= persistence;
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
    }
}
