using Assets.Scripts.PlanetGen;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TerrainUtils;

namespace PlanetGen
{
    public enum PlanetFace
    {
        PY,
        NY,
        PX,
        NX,
        PZ,
        NZ
    }

    public class ChunkManager : MonoBehaviour
    {
        [SerializeField] private int _Resolution = 128; // vertices per chunk axis

        [Header("QuadTree Settings")]
        [SerializeField] private float _PlanetRadius = 16000f;
        [SerializeField] private float _MinLeafSize = 128f;
        [SerializeField] private int _BudgetPerFrame = 32; // max nodes to split/merge per frame
        [SerializeField] private bool _EnableCulling = false;
        [SerializeField] private bool _EnableDebugQuad = false;

        [Header("")]
        [SerializeField] private Material _ChunkMaterial;
        [SerializeField] private Camera _CullCamera;
        [SerializeField] private PlanetOptionsSO _OptionsSO;

        private Dictionary<QuadNode, Chunk> _Chunks = new();
        private Stack<Chunk> _Pool = new(); // used as a stack for recycling chunks

        // we will use the job system of unity to generate the chunks in parallel
        private List<JobHandle> _Handles = new();
        private List<Chunk> _ToBuild = new();

        // QuadTree related
        private TerrainQuadTree[] _FaceQuadTrees = new TerrainQuadTree[6];
        private HashSet<QuadNode> _ActiveChunks = new();
        readonly List<QuadNode> _ToActivate = new();
        readonly List<QuadNode> _ToDeactivate = new();
        private float4x4[] _FaceMatrices = new float4x4[6];

        // For culling
        private Plane[] _FrustumPlanes = new Plane[6];

        private void OnEnable()
        {
            if (_CullCamera == null)
                _CullCamera = Camera.main;

            // Face +X
            _FaceMatrices[(int)PlanetFace.PX] = float4x4.TRS(
                new float3(_PlanetRadius, 0f, 0f),
                quaternion.AxisAngle(new float3(0f, 0f, 1f), math.radians(-90f)),
                new float3(1)
            );
            _FaceQuadTrees[(int)PlanetFace.PX] = new TerrainQuadTree(_PlanetRadius * 2f, _MinLeafSize,
                _FaceMatrices[(int)PlanetFace.PX], transform, PlanetFace.PX, _EnableCulling);

            // Face -X
            _FaceMatrices[(int)PlanetFace.NX] = float4x4.TRS(
                new float3(-_PlanetRadius, 0f, 0f),
                quaternion.AxisAngle(new float3(0f, 0f, 1f), math.radians(90f)),
                new float3(1)
            );
            _FaceQuadTrees[(int)PlanetFace.NX] = new TerrainQuadTree(_PlanetRadius * 2f, _MinLeafSize,
                _FaceMatrices[(int)PlanetFace.NX], transform, PlanetFace.NX, _EnableCulling);

            // Face +Y
            _FaceMatrices[(int)PlanetFace.PY] = float4x4.TRS(
                new float3(0, _PlanetRadius, 0),
                quaternion.identity,
                new float3(1)
            );
            _FaceQuadTrees[(int)PlanetFace.PY] = new TerrainQuadTree(_PlanetRadius * 2f, _MinLeafSize,
                _FaceMatrices[(int)PlanetFace.PY], transform, PlanetFace.PY, _EnableCulling);

            // Face -Y
            _FaceMatrices[(int)PlanetFace.NY] = float4x4.TRS(
                new float3(0, -_PlanetRadius, 0),
                quaternion.AxisAngle(new float3(1, 0, 0), math.radians(180)),
                new float3(1)
            );
            _FaceQuadTrees[(int)PlanetFace.NY] = new TerrainQuadTree(_PlanetRadius * 2f, _MinLeafSize,
                _FaceMatrices[(int)PlanetFace.NY], transform, PlanetFace.NY, _EnableCulling);

            // Face +Z
            _FaceMatrices[(int)PlanetFace.PZ] = float4x4.TRS(
                new float3(0f, 0f, _PlanetRadius),
                quaternion.AxisAngle(new float3(1, 0, 0), math.radians(90)),
                new float3(1)
            );
            _FaceQuadTrees[(int)PlanetFace.PZ] = new TerrainQuadTree(_PlanetRadius * 2f, _MinLeafSize,
                _FaceMatrices[(int)PlanetFace.PZ], transform, PlanetFace.PZ, _EnableCulling);

            // Face -Z
            _FaceMatrices[(int)PlanetFace.NZ] = float4x4.TRS(
                new float3(0f, 0f, -_PlanetRadius),
                quaternion.AxisAngle(new float3(1, 0, 0), math.radians(-90)),
                new float3(1)
            );
            _FaceQuadTrees[(int)PlanetFace.NZ] = new TerrainQuadTree(_PlanetRadius * 2f, _MinLeafSize,
                _FaceMatrices[(int)PlanetFace.NZ], transform, PlanetFace.NZ, _EnableCulling);
        }

        private readonly List<QuadNode> _DesiredLeaves = new();

        private void Update()
        {
            if (_CullCamera == null)
                return;

            for (int i = 0; i < _Handles.Count; i++)
                _Handles[i].Complete();
            _Handles.Clear();

            foreach (var chunk in _ToBuild)
            {
                if (chunk == null)
                    continue;
                chunk.ApplyMesh();
            }
            _ToBuild.Clear();
            _Handles.Clear();

            GeometryUtility.CalculateFrustumPlanes(_CullCamera, _FrustumPlanes);
            int budget = _BudgetPerFrame;
            _DesiredLeaves.Clear();
            for (int f = 0; f < 6; ++f)
            {
                _FaceQuadTrees[f].CollectLeavesDistance(_CullCamera.transform.position,
                    _FrustumPlanes, _ActiveChunks, _DesiredLeaves, ref budget);
            }

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
                var qt = _FaceQuadTrees[(int)key.Face];

                var b = qt.GetWorldNodeBounds(key);
                var qtb = qt.GetNodeBounds(key); // without rotation, for positioning 
                
                var chunk = GetChunk();
                chunk.gameObject.name = $"Chunk_{key.Coords.x}_{key.Coords.y}_D{key.Depth}_F{key.Face}";
                chunk.transform.SetParent(transform, false);
                chunk.transform.position = (float3)b.Center;
                chunk.transform.rotation = qt.GetQuadTreeMatrix().rotation;
                
                double3 qtNoRotCenter = qtb.Center;
                qtNoRotCenter.y = _PlanetRadius;
                qtNoRotCenter = math.normalize(qtNoRotCenter) * _PlanetRadius;
                chunk.Initialize(_Resolution, b.Size, _ChunkMaterial, _PlanetRadius, qtb.Center, qtNoRotCenter);

                var tris = SharedTrianglesCache.Get(_Resolution);
                var h = chunk.ScheduleBuild(tris, _OptionsSO);
                _Handles.Add(h);

                _Chunks[key] = chunk;
                _ActiveChunks.Add(key);
                _ToBuild.Add(chunk);
            }
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

        void OnDrawGizmos()
        {
            // only run in play mode
            if (!Application.isPlaying || _FaceQuadTrees == null || _EnableDebugQuad == false)
                return;

            Color visibleColor = new Color(0, 1, 0, 0.25f);
            Color culledColor = new Color(1, 0, 0, 0.25f);
            foreach (var qt in _FaceQuadTrees)
            {
                foreach (var nodeBounds in qt.BoundsToDraw)
                {
                    bool inside = GeometryUtility.TestPlanesAABB(_FrustumPlanes, nodeBounds);

                    Gizmos.color = inside ? visibleColor : culledColor;
                    Gizmos.DrawWireCube(nodeBounds.center, nodeBounds.size);
                }
            }
            Gizmos.color = Color.cyan;
            Gizmos.matrix = _CullCamera.transform.localToWorldMatrix;
            Gizmos.DrawFrustum(new Vector3(), _CullCamera.fieldOfView, _CullCamera.farClipPlane, _CullCamera.nearClipPlane, _CullCamera.aspect);
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
