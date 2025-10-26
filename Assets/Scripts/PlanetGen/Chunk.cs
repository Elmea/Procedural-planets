using Codice.Client.BaseCommands.Changelist;
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
        private NativeArray<float2> _Uvs;
        private NativeArray<float3> _Normals;

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

        public JobHandle ScheduleBuild(NativeArray<int> sharedTriangles)
        {
            // https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Mesh.MeshData.html
            _MeshDataArray = Mesh.AllocateWritableMeshData(1); // will be disposed by Apply
            _MeshData = _MeshDataArray[0];

            int vertCount = (_Resolution + 1) * (_Resolution + 1);
            int indexCount = sharedTriangles.Length;

            _MeshData.SetVertexBufferParams(vertCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, stream: 2)
            );
            _MeshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

            _Verts = new NativeArray<float3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // temp arrays that's disposed by apply
            _Normals = new NativeArray<float3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // temp arrays that's disposed by apply
            _Uvs = new NativeArray<float2>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // temp arrays that's disposed by apply

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
                NormalDelta = _Size / (_Resolution * 2.0),
                WorldPos = (float3)transform.position,
                WorldRot = (quaternion)transform.rotation,
                Vertices = _Verts,
                Normals = _Normals,
                UVs = _Uvs,
                Frequency = 0.005f,
                Amplitude = 100f,
                Octaves = 5,
                Lacunarity = 2f,
                Persistence = 0.5f,
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
            vbPos.CopyFrom(_Verts);
            vbNorm.CopyFrom(_Normals);
            vbUv.CopyFrom(_Uvs);

            Mesh.ApplyAndDisposeWritableMeshData(_MeshDataArray, _Mesh);
            _Verts.Dispose(); // must be disposed or else mem leaks
            _Uvs.Dispose(); // must be disposed or else mem leaks
            _Normals.Dispose(); // must be disposed or else mem leaks

            _Mesh.RecalculateNormals();
            _Mesh.RecalculateBounds();
        }

        [BurstCompile]
        private struct BuildChunkMeshJob : IJobParallelFor
        {
            public int Resolution;
            public float PlanetRadius;
            public double Size;
            public double NormalDelta;
            public double3 WorldPos;
            public quaternion WorldRot;
            public double3 QuadTreeSphereNoRotCenter;
            public double3 QuadTreeBoundsCenter;

            public float Frequency;
            public float Amplitude;
            public int Octaves;
            public float Lacunarity;
            public float Persistence;

            public NativeArray<float3> Vertices;
            public NativeArray<float2> UVs;
            public NativeArray<float3> Normals;

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
                float baseHeight = fbm(noiseDir * Frequency * PlanetRadius, Lacunarity, Octaves, Persistence);
                float height = baseHeight * Amplitude;

                float3 p = dir * (PlanetRadius + height);

                Vertices[index] = p - (float3)QuadTreeSphereNoRotCenter;
                UVs[index] = new float2((float)fx, (float)fy);
            }

            public void ExecuteOld(int index)
            {
                int vertsPerSide = Resolution + 1;
                int y = index / vertsPerSide;
                int x = index % vertsPerSide;

                double fx = (double)x / Resolution;
                double fy = (double)y / Resolution;

                double xPos = (fx - 0.5) * Size;
                double zPos = (fy - 0.5) * Size;

                float xHeightL = noise.snoise(new float2((float)(xPos - NormalDelta + QuadTreeBoundsCenter.x), (float)(zPos + QuadTreeBoundsCenter.z)) * 0.01f) * 1.0f;
                float xHeightR = noise.snoise(new float2((float)(xPos + NormalDelta + QuadTreeBoundsCenter.x), (float)(zPos + QuadTreeBoundsCenter.z)) * 0.01f) * 1.0f;
                float zHeightD = noise.snoise(new float2((float)(xPos + QuadTreeBoundsCenter.x), (float)(zPos - NormalDelta + QuadTreeBoundsCenter.z)) * 0.01f) * 1.0f;
                float zHeightU = noise.snoise(new float2((float)(xPos + QuadTreeBoundsCenter.x), (float)(zPos + NormalDelta + QuadTreeBoundsCenter.z)) * 0.01f) * 1.0f;
                //float3 pos = new float3((float)xPos, noise.snoise(new float2((float)(xPos + WorldPos.x), (float)(zPos + WorldPos.y)) * 0.01f) * 1.0f, (float)zPos);
                double3 pos = new double3(xPos + QuadTreeBoundsCenter.x, PlanetRadius, zPos + QuadTreeBoundsCenter.z);
                pos = math.normalize(pos);
                pos *= (PlanetRadius) + noise.snoise(new float2((float)(xPos + QuadTreeBoundsCenter.x), (float)(zPos + QuadTreeBoundsCenter.z)) * 0.01f) * 10.0f;
                pos -= QuadTreeSphereNoRotCenter; // make relative to world pos

                Vertices[index] = (float3)pos;
                Normals[index] = math.normalize(new float3(xHeightL - xHeightR, 2.0f * (float)NormalDelta, zHeightD - zHeightU));
                UVs[index] = new float2((float)fx, (float)fy);
            }

            static float fbm(float3 pt, float lacunarity, int octaves, float persistence)
            {
                float a = 1f;
                float amplitude = 0f;
                float sum = 0f;
                float3 q = pt * lacunarity;
                for (int i = 0; i < octaves; i++)
                {
                    sum += a * noise.snoise(q);
                    amplitude += a;
                    q *= 2f;
                    a *= persistence;
                }
                return sum / math.max(amplitude, 1e-6f);
            }
        }
    }
}
