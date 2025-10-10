using System;
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
        private float _Size;

        private Mesh _Mesh;
        private MeshFilter _MeshFilter;
        private MeshRenderer _MeshRenderer;


        private Mesh.MeshDataArray _MeshDataArray;
        private Mesh.MeshData _MeshData;

        private NativeArray<float3> _Verts;
        private NativeArray<float2> _Uvs;

        public void Initialize(int resolution, float chunkSize, Material mat)
        {
            _Resolution = math.max(2, resolution);
            _Size = math.max(1f, chunkSize);

            _MeshFilter = GetComponent<MeshFilter>();
            _MeshRenderer = GetComponent<MeshRenderer>();

            _Mesh = new Mesh { name = "ChunkMesh" };
            _Mesh.MarkDynamic();
            _MeshFilter.sharedMesh = _Mesh;
            _MeshRenderer.sharedMaterial = mat;
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
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, stream: 1)
            );
            _MeshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

            var vbPosUv = _MeshData.GetVertexData<float3>(0);
            var vbUv = _MeshData.GetVertexData<float2>(1);

            _Verts = new NativeArray<float3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // temp arrays that's disposed by apply
            _Uvs = new NativeArray<float2>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // temp arrays that's disposed by apply

            var ib = _MeshData.GetIndexData<int>();
            ib.CopyFrom(sharedTriangles); // can do this here since it's shared and already built

            _MeshData.subMeshCount = 1;
            _MeshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles));

            // Schedule the vertex job
            var job = new BuildFlatChunkJob
            {
                Resolution = _Resolution,
                Size = _Size,
                Vertices = _Verts,
                UVs = _Uvs
            };

            JobHandle handle = job.Schedule(_Verts.Length, 128);

            // When job completes, copy into MeshData vertex buffers on main thread vis ApplyMesh
            // for now, it's called by ChunkManager after all jobs are completed
            return handle;
        }

        public void ApplyMesh()
        {
            if (!_Verts.IsCreated || !_Uvs.IsCreated) return;

            var vbPos = _MeshData.GetVertexData<float3>(0);
            var vbUv = _MeshData.GetVertexData<float2>(1);
            vbPos.CopyFrom(_Verts);
            vbUv.CopyFrom(_Uvs);

            Mesh.ApplyAndDisposeWritableMeshData(_MeshDataArray, _Mesh);
            _Verts.Dispose(); // must be disposed or else mem leaks
            _Uvs.Dispose(); // must be disposed or else mem leaks

            _Mesh.RecalculateNormals();
            _Mesh.RecalculateBounds();
        }

        [BurstCompile]
        private struct BuildFlatChunkJob : IJobParallelFor
        {
            public int Resolution;
            public float Size;

            public NativeArray<float3> Vertices;
            public NativeArray<float2> UVs;

            public void Execute(int index)
            {
                int vertsPerSide = Resolution + 1;

                int y = index / vertsPerSide;
                int x = index % vertsPerSide;

                float fx = (float)x / Resolution;
                float fy = (float)y / Resolution;

                float xPos = (fx - 0.5f) * Size;
                float zPos = (fy - 0.5f) * Size;

                Vertices[index] = new float3(xPos, 0f, zPos);
                UVs[index] = new float2(fx, fy);
            }
        }
    }
}
