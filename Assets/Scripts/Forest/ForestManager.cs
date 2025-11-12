using System;
using System.Linq;
using Unity.Mathematics;
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
    [SerializeField] float _JitterAmount = 0.3f;
    [SerializeField] PlanetOptionsSO _PlanetOptions;
    [SerializeField] float _PlanetRadius;

    GraphicsBuffer _CommandBuffer;
    GraphicsBuffer _CulledCommandBuffer;
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

        float3 sphereCenter = float3.zero;
        float radius = 50f;
        float scale = 0.25f;

        float4x4[] instanceData = new float4x4[_NumberOfInstances];
        int totalCount = 0;
        var rand = new Unity.Mathematics.Random(307878359u);
        // Calculate approximate arc length between points based on the area per point on the sphere
        float arcLength = 2f / math.sqrt(_NumberOfInstances);

        for (uint i = 0; i < _NumberOfInstances; i++)
        {
            float3 dir = FibonacciPointOnSphere(i, _NumberOfInstances);

            float jitter = arcLength * _JitterAmount;
            float2 off = rand.NextFloat2Direction() * jitter;

            float3 up = math.abs(dir.y) > 0.9f ? new float3(1, 0, 0) : new float3(0, 1, 0);
            float3 tangent = math.normalize(math.cross(up, dir));
            float3 bitangent = math.cross(dir, tangent);

            float3 perturbed = dir + off.x * tangent + off.y * bitangent;
            dir = math.normalize(perturbed);

            float3 posOnPlanet = dir * (_PlanetRadius);
            float continent = BurstUtils.ContinentField(posOnPlanet,
                    _PlanetRadius, _PlanetOptions.ContinentWavelength,
                    _PlanetOptions.ContinentWarpAmplitude, _PlanetOptions.ContinentWarpFrequency,
                    _PlanetOptions.ContinentLacunarity, _PlanetOptions.ContinentOctaves, _PlanetOptions.ContinentPersistence);
            float coastlineOffset = BurstUtils.CoastBreaker(posOnPlanet, _PlanetRadius);

            float continentWithCoastline = continent + (0.05f * (coastlineOffset - 0.5f));

            float landMask = math.smoothstep(_PlanetOptions.SeaCoastLimit, _PlanetOptions.LandCoastLimit, continentWithCoastline);

            if (landMask < 1.0f || continentWithCoastline > _PlanetOptions.MountainStart)
                continue;

            float3 pos = dir * radius;
            float3 fwd = tangent;
            quaternion rot = quaternion.LookRotationSafe(fwd, dir);

            instanceData[totalCount++] = float4x4.TRS(pos, rot, new float3(scale));
        }
        _KeptNumberOfInstances = totalCount;
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

        _TransformBuffer.SetData(instanceData,0, 0, totalCount);

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

        if (isCullingEnabled)
        {
            int kernel = _TreeCullingShader.FindKernel("TreeCulling");

            _TreeCullingShader.SetInt("_InstanceCount", _KeptNumberOfInstances);
            _TreeCullingShader.SetVector("_CameraPos", Camera.main.transform.position);
            _TreeCullingShader.SetFloat("_MaxDrawDistance", _MaxDrawDistance);
            _TreeCullingShader.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

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
        rp.worldBounds = new Bounds(Vector3.zero, 10000f * Vector3.one);
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_TransformBuffer", isCullingEnabled ? _VisibleTransformBuffer : _TransformBuffer);
        rp.matProps.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

        Graphics.RenderMeshIndirect(rp, _Mesh, _CommandBuffer, 1, 0);

        rp.material = _LeavesMaterial;
        Graphics.RenderMeshIndirect(rp, _Mesh, _CommandBuffer, 1, 1);
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
}
