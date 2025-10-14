using Assets.Scripts.PlanetGen;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen
{
    public class ChunkManager : MonoBehaviour
    {
        [SerializeField] private int _Resolution = 128; // vertices per chunk axis

        [Header("QuadTree Settings")]
        [SerializeField] private float _TerrainMaxSize = 32000.0f;
        [SerializeField] private float _MinLeafSize = 128f;
        [SerializeField] private double _SplitPx = 2.0f; // if projected size > SplitPx, split
        [SerializeField] private double _MergePx = 1.414f;
        [SerializeField] private int _BudgetPerFrame = 32; // max nodes to split/merge per frame

        [Header("")]
        [SerializeField] private Material _ChunkMaterial;
        // for culling and LOD
        [SerializeField] private Camera _CullCamera;
        [SerializeField] private double _MaxChunkHeightCoef = 0.5; // 0.5 because 0.414 for from center of cube face to heighest point on sphere and some error boost to 0.5

        private Dictionary<QuadNode, Chunk> _Chunks = new();
        private Stack<Chunk> _Pool = new(); // used as a stack for recycling chunks

        // we will use the job system of unity to generate the chunks in parallel
        private List<JobHandle> _Handles = new();
        private List<Chunk> _ToBuild = new();

        // QuadTree related
        private TerrainQuadTree _QuadTree;
        private HashSet<QuadNode> _ActiveChunks = new();
        readonly List<QuadNode> _ToActivate = new();
        readonly List<QuadNode> _ToDeactivate = new();

        // For culling
        private Plane[] _FrustumPlanes = new Plane[6];

        private void OnEnable()
        {
            if (_CullCamera == null)
                _CullCamera = Camera.main;
            _QuadTree = new TerrainQuadTree(_TerrainMaxSize, _MinLeafSize, _SplitPx, _MergePx, _BudgetPerFrame);
        }

        private readonly List<QuadNode> _DesiredLeaves = new();

        private void Update()
        {
            if (_CullCamera == null) return;

            GeometryUtility.CalculateFrustumPlanes(_CullCamera, _FrustumPlanes);

            _QuadTree.CollectLeavesDistance(_CullCamera.transform.position, _FrustumPlanes, _DesiredLeaves);

            var desiredSet = new HashSet<QuadNode>(_DesiredLeaves);

            _ToDeactivate.Clear();
            foreach (var active in _ActiveChunks)
                if (!desiredSet.Contains(active))
                    _ToDeactivate.Add(active);

            foreach (var n in _ToDeactivate)
            {
                if (_Chunks.TryGetValue(n, out var chunk))
                {
                    RecycleChunk(chunk);
                    _Chunks.Remove(n);
                }
                _ActiveChunks.Remove(n);
            }

            _ToActivate.Clear();
            foreach (var want in _DesiredLeaves)
                if (!_ActiveChunks.Contains(want))
                    _ToActivate.Add(want);

            foreach (var key in _ToActivate)
            {
                var b = _QuadTree.GetNodeBounds(key);
                var chunk = GetChunk();
                chunk.gameObject.name = $"Chunk_{key.Coords.x}_{key.Coords.y}_D{key.Depth}";
                chunk.transform.SetParent(transform, false);
                chunk.transform.position = new Vector3((float)b.Center.x, 0f, (float)b.Center.y);
                chunk.Initialize(_Resolution, b.Size, _ChunkMaterial);

                var tris = SharedTrianglesCache.Get(_Resolution);
                var h = chunk.ScheduleBuild(tris);
                _Handles.Add(h);

                _Chunks[key] = chunk;
                _ActiveChunks.Add(key);
                _ToBuild.Add(chunk);
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _Handles.Count; i++)
                _Handles[i].Complete();
            _Handles.Clear();

            foreach (var chunk in _ToBuild)
            {
                if (chunk == null) continue;
                chunk.ApplyMesh();
            }
            _ToBuild.Clear();
            _Handles.Clear();
        }

        private void OnDisable()
        {
            foreach (var kv in _Chunks) kv.Value.Dispose();
            _Chunks.Clear();
            SharedTrianglesCache.DisposeAll();
        }

        private void RecycleChunk(Chunk chunk)
        {
            if (!chunk) return;
            chunk.gameObject.SetActive(false);
            _Pool.Push(chunk);
        }

        private Chunk GetChunk()
        {
            while (_Pool.Count > 0 && _Pool.Peek() == null)
                _Pool.Pop(); // normally shouldn't happen but just in case...

            if (_Pool.Count > 0)
            {
                var c = _Pool.Pop();
                c.gameObject.SetActive(true);
                return c;
            }
            var go = new GameObject("Chunk");
            var cnew = go.AddComponent<Chunk>();
            return cnew;
        }

        internal static class SharedTrianglesCache
        {
            private static readonly Dictionary<int, Unity.Collections.NativeArray<int>> _cache = new();

            public static Unity.Collections.NativeArray<int> Get(int res)
            {
                if (_cache.TryGetValue(res, out var arr) && arr.IsCreated) return arr;

                int quadCount = res * res;
                var triangles = new Unity.Collections.NativeArray<int>(quadCount * 6, Unity.Collections.Allocator.Persistent, Unity.Collections.NativeArrayOptions.UninitializedMemory);
                int t = 0;
                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                    {
                        int i = y * (res + 1) + x;
                        triangles[t++] = i;
                        triangles[t++] = i + res + 1;
                        triangles[t++] = i + 1;

                        triangles[t++] = i + 1;
                        triangles[t++] = i + res + 1;
                        triangles[t++] = i + res + 2;
                    }
                _cache[res] = triangles;
                return triangles;
            }

            public static void DisposeAll()
            {
                foreach (var kv in _cache)
                    if (kv.Value.IsCreated) kv.Value.Dispose();
                _cache.Clear();
            }
        }

    }
}
