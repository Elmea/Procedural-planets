using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using IndirectDrawArgs = UnityEngine.GraphicsBuffer.IndirectDrawIndexedArgs;

// from the example found in the doc for Graphics.RenderMeshIndirect:
// https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html
public class ForestManager : MonoBehaviour
{
    [SerializeField] Material _BarkMaterial;
    [SerializeField] Material _LeavesMaterial;
    [SerializeField] ComputeShader _TreeCullingShader;
    [SerializeField] bool _EnableCulling = true;
    [SerializeField] Mesh _Mesh;
    [SerializeField] float _MaxDrawDistance = 20f;
    [Tooltip("It's the total number of instances before culling based on the land mask")]
    [SerializeField] uint _NumberOfInstances;
    [SerializeField] TextAsset _TransformBufferAsset;
    [SerializeField] float _JitterAmount = 0.3f;
    [SerializeField] PlanetOptionsSO _PlanetOptions;
    [SerializeField] float _MinTreeScale = 0.2f;
    [SerializeField] float _MaxTreeScale = 0.3f;

    GraphicsBuffer _CommandBuffer;
    GraphicsBuffer _TransformBuffer;
    GraphicsBuffer _VisibleTransformBuffer;

    IndirectDrawArgs[] _InitCommandData;
    int _KeptNumberOfInstances;

    void Start()
    {
        bool isCullingEnabled = _TreeCullingShader != null && _EnableCulling;

        _CommandBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            2,
            IndirectDrawArgs.size
        );

        _InitCommandData = new IndirectDrawArgs[2];

        //CreateForestBuffer(out var totalCount, out var transformBuffer);
        LoadTransformBufferAsset(out var totalCount, out var transformBuffer);

        _KeptNumberOfInstances = (int)totalCount;
        _TransformBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            _KeptNumberOfInstances,
            sizeof(float) * 4 * 4
        );
        _VisibleTransformBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            _KeptNumberOfInstances,
            sizeof(float) * 4 * 4
        );

        Debug.Log("ForestManager: kept " + _KeptNumberOfInstances + " instances out of " + _NumberOfInstances);

        _TransformBuffer.SetData(transformBuffer, 0, 0, (int)totalCount);

        _InitCommandData[0].indexCountPerInstance = _Mesh.GetIndexCount(0);
        _InitCommandData[0].instanceCount = isCullingEnabled ? 0 : (uint)_KeptNumberOfInstances;
        _InitCommandData[0].startIndex = (uint)_Mesh.GetIndexStart(0);
        _InitCommandData[0].baseVertexIndex = (uint)_Mesh.GetBaseVertex(0);
        _InitCommandData[0].startInstance = 0;

        _InitCommandData[1].indexCountPerInstance = _Mesh.GetIndexCount(1);
        _InitCommandData[1].instanceCount = isCullingEnabled ? 0 : (uint)_KeptNumberOfInstances;
        _InitCommandData[1].startIndex = (uint)_Mesh.GetIndexStart(1);
        _InitCommandData[1].baseVertexIndex = (uint)_Mesh.GetBaseVertex(1);
        _InitCommandData[1].startInstance = 0;

        _CommandBuffer.SetData(_InitCommandData);
    }

    void OnDestroy()
    {
        _TransformBuffer?.Release();
        _TransformBuffer = null;

        _VisibleTransformBuffer?.Release();
        _VisibleTransformBuffer = null;

        _CommandBuffer?.Release();
        _CommandBuffer = null;
    }

    void LateUpdate()
    {
        if (_Mesh == null || _BarkMaterial == null)
            return;
        bool isCullingEnabled = _TreeCullingShader != null && _EnableCulling;
        _InitCommandData[0].instanceCount = isCullingEnabled ? 0 : (uint)_KeptNumberOfInstances;
        _InitCommandData[1].instanceCount = isCullingEnabled ? 0 : (uint)_KeptNumberOfInstances;
        _CommandBuffer.SetData(_InitCommandData);

        if (math.distance(Camera.main.transform.position, transform.position) > _PlanetOptions.PlanetRadius + _MaxDrawDistance * 1.5)
            return;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
        Vector4[] planeVectors = new Vector4[6];
        for (int i = 0; i < planes.Length; i++)
        {
            Vector3 normal = planes[i].normal;

            // just to be sure everything is normal (drum roll)
            float inverseLength = 1.0f / normal.magnitude;
            normal *= inverseLength;
            float distance = planes[i].distance * inverseLength;
            planeVectors[i] = new Vector4(normal.x, normal.y, normal.z, distance);
        }

        if (isCullingEnabled)
        {
            int kernel = _TreeCullingShader.FindKernel("TreeCulling");

            _TreeCullingShader.SetInt("_InstanceCount", _KeptNumberOfInstances);
            _TreeCullingShader.SetVector("_CameraPos", Camera.main.transform.position);
            _TreeCullingShader.SetFloat("_MaxDrawDistance", _MaxDrawDistance);
            _TreeCullingShader.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);
            _TreeCullingShader.SetVectorArray("_FrustumPlanes", planeVectors);
            _TreeCullingShader.SetFloat("_BoundingSphereRadius", _Mesh.bounds.extents.magnitude * _MaxTreeScale);

            _TreeCullingShader.SetBuffer(kernel, "_AllTransforms", _TransformBuffer);
            _TreeCullingShader.SetBuffer(kernel, "_VisibleTransforms", _VisibleTransformBuffer);
            _TreeCullingShader.SetBuffer(kernel, "_IndirectArgs", _CommandBuffer);

            _TreeCullingShader.Dispatch(
                kernel,
                Mathf.CeilToInt(_KeptNumberOfInstances / 64f),
                1,
                1
            );
        }

        var rp = new RenderParams(_BarkMaterial);
        rp.worldBounds = new Bounds(transform.position, (_PlanetOptions.PlanetRadius * 1.5f) * Vector3.one);
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_TransformBuffer", isCullingEnabled ? _VisibleTransformBuffer : _TransformBuffer);
        rp.matProps.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

        Graphics.RenderMeshIndirect(rp, _Mesh, _CommandBuffer, 1, 0);

        rp.material = _LeavesMaterial;
        Graphics.RenderMeshIndirect(rp, _Mesh, _CommandBuffer, 1, 1);
    }

    public void CreateForestBuffer(out uint totalCount, out float4x4[] transformBuffer)
    {
        uint numThreads = (uint)Math.Max(1, Environment.ProcessorCount);
        uint chunk = (_NumberOfInstances + numThreads - 1u) / numThreads;

        var perThreadBuffers = new List<float4x4>[numThreads];
        var perThreadCounts = new uint[numThreads];
        uint baseSeed = 307878359u;

        float arcLength = 2f / math.sqrt(_NumberOfInstances);

        float planetRadius = _PlanetOptions.PlanetRadius;

        Parallel.For(0, numThreads, t =>
        {
            uint start = (uint)t * chunk;
            uint end = math.min(start + chunk, _NumberOfInstances);
            if (start >= end)
            {
                perThreadBuffers[t] = new List<float4x4>(0);
                perThreadCounts[t] = 0;
                return;
            }

            uint seed = ScrambleSeed(baseSeed, (uint)t);
            var rand = new Unity.Mathematics.Random(seed);

            uint capacity = end - start;
            var local = new List<float4x4>((int)capacity);
            uint localCount = 0;

            for (uint i = (uint)start; i < (uint)end; i++)
            {
                float3 dir = FibonacciPointOnSphere(i, (uint)_NumberOfInstances);

                float jitter = arcLength * _JitterAmount;
                float2 off = rand.NextFloat2Direction() * jitter;

                float3 up = math.abs(dir.y) > 0.9f ? new float3(1, 0, 0) : new float3(0, 1, 0);
                float3 tangent = math.normalize(math.cross(up, dir));
                float3 bitangent = math.cross(dir, tangent);

                float3 perturbed = dir + off.x * tangent + off.y * bitangent;
                dir = math.normalize(perturbed);

                float3 posOnPlanet = dir * planetRadius;

                float continent = BurstUtils.ContinentField(
                    posOnPlanet,
                    planetRadius, _PlanetOptions.ContinentWavelength,
                    _PlanetOptions.ContinentWarpAmplitude, _PlanetOptions.ContinentWarpFrequency,
                    _PlanetOptions.ContinentLacunarity, _PlanetOptions.ContinentOctaves, _PlanetOptions.ContinentPersistence);

                float coastlineOffset = BurstUtils.CoastBreaker(posOnPlanet, planetRadius);

                float continentWithCoastline = continent + (0.05f * (coastlineOffset - 0.5f));
                float landMask = math.smoothstep(_PlanetOptions.SeaCoastLimit, _PlanetOptions.LandCoastLimit, continentWithCoastline);

                if (landMask < 1.0f || continentWithCoastline > _PlanetOptions.MountainStart)
                    continue;

                float land = BurstUtils.CoastLandProfile(landMask, _PlanetOptions.BaseLandLevel);

                float hillsMask = math.smoothstep(_PlanetOptions.LandCoastLimit, _PlanetOptions.LandHillRampLimit, continentWithCoastline);
                land = BurstUtils.HillsField(
                    posOnPlanet, land, hillsMask, _PlanetOptions.BaseLandLevel,
                    planetRadius, _PlanetOptions.HillsWavelength,
                    _PlanetOptions.HillsLacunarity, _PlanetOptions.HillsOctaves, _PlanetOptions.HillsPersistence,
                    _PlanetOptions.HillsAmplitudeMeters);

                float3 pos = dir * (planetRadius + land);
                float3 fwd = tangent;
                quaternion rot = quaternion.LookRotationSafe(fwd, dir);

                float scaleValue = math.lerp(_MinTreeScale, _MaxTreeScale, rand.NextFloat());
                local.Add(float4x4.TRS(pos, rot, scaleValue));
                localCount++;
            }

            perThreadBuffers[t] = local;
            perThreadCounts[t] = localCount;
        });

        totalCount = 0;
        for (int t = 0; t < numThreads; ++t)
            totalCount += perThreadCounts[t];

        transformBuffer = new float4x4[totalCount];
        int index = 0;
        for (int t = 0; t < numThreads; ++t)
        {
            var buffer = perThreadBuffers[t];
            uint count = perThreadCounts[t];
            if (count == 0) continue;

            buffer.CopyTo(0, transformBuffer, index, (int)count);
            index += (int)count;
        }
    }

    private void LoadTransformBufferAsset(out uint totalCount, out float4x4[] transformBuffer)
    {
        using var ms = new MemoryStream(_TransformBufferAsset.bytes);
        using var br = new BinaryReader(ms);

        totalCount = br.ReadUInt32();
        transformBuffer = new float4x4[totalCount];

        for (int i = 0; i < totalCount; i++)
        {
            float4x4 m;
            m.c0 = new float4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            m.c1 = new float4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            m.c2 = new float4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            m.c3 = new float4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            transformBuffer[i] = m;
        }
    }

    private static float PHI = math.PI * (math.sqrt(5.0f) - 1.0f);
    // https://stackoverflow.com/questions/9600801/evenly-distributing-n-points-on-a-sphere/26127012#26127012
    float3 FibonacciPointOnSphere(uint i, uint n)
    {
        float y = 1f - 2f * ((i + 0.5f) / n);
        float r = math.sqrt(1f - y * y);
        float theta = PHI * i;
        float x = math.cos(theta) * r;
        float z = math.sin(theta) * r;
        return new float3(x, y, z);
    }

    // just a hash
    private static uint ScrambleSeed(uint baseSeed, uint threadIndex)
    {
        uint s = baseSeed ^ (threadIndex * 747796405u) ^ 2891336453u;
        if (s == 0u) 
            s = 0xA3C59AC3u;
        return s;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ForestManager))]
class ForestManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        ForestManager manager = (ForestManager)target;

        if (GUILayout.Button("Generate Forest Positions"))
        {
            manager.CreateForestBuffer(out var totalCount, out var transformBuffer);
            string pathToAsset = EditorUtility.SaveFilePanel("Save forest instance buffer", "Asset/Resources/Forest", "DefaultPlanetBuffer", "bytes");
            if (string.IsNullOrEmpty(pathToAsset)) 
                return;

            using (var fs = new FileStream(pathToAsset, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(totalCount);
                for (int i = 0; i < totalCount; i++)
                {
                    float4x4 m = transformBuffer[i];
                    // row-major; matches Matrix4x4 in Unity
                    bw.Write(m.c0.x); bw.Write(m.c0.y); bw.Write(m.c0.z); bw.Write(m.c0.w);
                    bw.Write(m.c1.x); bw.Write(m.c1.y); bw.Write(m.c1.z); bw.Write(m.c1.w);
                    bw.Write(m.c2.x); bw.Write(m.c2.y); bw.Write(m.c2.z); bw.Write(m.c2.w);
                    bw.Write(m.c3.x); bw.Write(m.c3.y); bw.Write(m.c3.z); bw.Write(m.c3.w);
                }
            }

            AssetDatabase.ImportAsset(RelativeToDataPath(pathToAsset));
            Debug.Log($"Saved instance buffer at: {pathToAsset}");
        }
    }

    static string RelativeToDataPath(string absolute)
    {
        var projectPath = Application.dataPath.Replace("/Assets", "");
        var rel = absolute.Replace(projectPath + "/", "");
        return rel;
    }
}
#endif
