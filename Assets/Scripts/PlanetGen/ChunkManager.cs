using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen
{
    public class ChunkManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int2 _GridSize = new int2(4,4); // using int2 from Unity.Mathematics because burst compatibility
        [SerializeField] private float _ChunkSize = 128f; // in WU and 1WU = 1m I guess
        [SerializeField] private int _Resolution = 128; // vertices per chunk axis
    
        [Header("Others (Idk need to rename this)")]
        [SerializeField] private Material _ChunkMaterial;
    
        private Dictionary<int2, Chunk> _Chunks = new();
        // we will use the job system of unity to generate the chunks in parallel
        private List<JobHandle> _Handles = new();
    
        private NativeArray<int> _Triangles; // shared between all chunks

        private void OnEnable()
        {
            BuildSharedTriangles();

            for (int z = 0; z < _GridSize.y; ++z)
            {
                for (int x = 0; x < _GridSize.x; ++x)
                {
                    int2 cellID = new int2(x,z);
                    SpawnChunk(cellID);
                }
            }

            ScheduleAllChunkBuilds();
        }

        private void BuildSharedTriangles()
        {
            _Triangles = new NativeArray<int>(_Resolution * _Resolution * 6, 
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            int t = 0;
            for (int y = 0; y < _Resolution; y++)
            {
                for (int x = 0; x < _Resolution; x++)
                {
                    int i = y * (_Resolution + 1) + x;
                    _Triangles[t++] = i;
                    _Triangles[t++] = i + _Resolution + 1;
                    _Triangles[t++] = i + 1;

                    _Triangles[t++] = i + 1;
                    _Triangles[t++] = i + _Resolution + 1;
                    _Triangles[t++] = i + _Resolution + 2;
                }
            }

        }

        private void LateUpdate()
        {
            for (int i = 0; i < _Handles.Count; i++) 
                _Handles[i].Complete();
            _Handles.Clear();

            foreach (var kv in _Chunks)
                kv.Value.ApplyMesh();
        }

        private void OnDisable()
        {
            foreach (var kv in _Chunks) kv.Value.Dispose();
            _Chunks.Clear();

            if (_Triangles.IsCreated)
                _Triangles.Dispose();
        }
    
        private void ScheduleAllChunkBuilds()
        {
            foreach (var kv in _Chunks)
            {
                var handle = kv.Value.ScheduleBuild(_Triangles);
                _Handles.Add(handle);
            }
        }

        private void SpawnChunk(int2 cellID)
        {
            if (_Chunks.ContainsKey(cellID))
                return;
        
            var go = new GameObject($"Chunk_{cellID.x}_{cellID.y}");
            go.transform.SetParent(transform, false);

            float originX = -(_GridSize.x * 0.5f) * _ChunkSize + _ChunkSize * 0.5f;
            float originZ = -(_GridSize.y * 0.5f) * _ChunkSize + _ChunkSize * 0.5f;
            float worldX  = originX + cellID.x * _ChunkSize;
            float worldZ  = originZ + cellID.y * _ChunkSize;
            go.transform.position = new Vector3(worldX, 0f, worldZ);

            var chunk = go.AddComponent<Chunk>();
            chunk.Initialize(_Resolution, _ChunkSize, _ChunkMaterial);
            _Chunks.Add(cellID, chunk);
        }
    }
}
