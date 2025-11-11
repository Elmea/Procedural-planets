using System;
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
    [SerializeField] Mesh _Mesh;
    [SerializeField] uint _NumInstanceLat = 20;
    [SerializeField] uint _NumInstanceLon = 40;
    [SerializeField] float _MaxDrawDistance = 20f;
    uint _NumberOfInstances;

    GraphicsBuffer _CommandBuffer;
    GraphicsBuffer _CulledCommandBuffer;
    GraphicsBuffer _TransformBuffer;
    GraphicsBuffer _VisibleTransformBuffer;

    IndirectDrawArgs[] _InitCommandData;

    void Start()
    {
        _NumberOfInstances = _NumInstanceLat * _NumInstanceLon;
        _CommandBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            2,
            IndirectDrawArgs.size
        );

        _InitCommandData = new IndirectDrawArgs[2];

        _TransformBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            (int)_NumberOfInstances,
            sizeof(float) * 4 * 4
        );
        _VisibleTransformBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            (int)_NumberOfInstances,
            sizeof(float) * 4 * 4
        );

        float3 sphereCenter = float3.zero;
        float radius = 50f;
        float scale = 0.25f;

        float4x4[] instanceData = new float4x4[_NumberOfInstances];
        for (uint lat = 0; lat < _NumInstanceLat; lat++)
        {
            float v = (float)lat / _NumInstanceLat;
            float theta = v * math.PI;

            float sinTheta = math.sin(theta);
            float cosTheta = math.cos(theta);

            for (uint lon = 0; lon < _NumInstanceLon; lon++)
            {
                float u = (float)lon / _NumInstanceLon;
                float phi = u * math.PI2;

                float3 normal = new float3(
                    math.cos(phi) * sinTheta,
                    cosTheta,
                    math.sin(phi) * sinTheta
                );

                float3 pos = sphereCenter + normal * radius;

                float3 tangent = math.normalize(math.cross(new float3(0, 1, 0), normal));
                float3 forward = math.cross(normal, tangent);
                quaternion rot = quaternion.LookRotationSafe(forward, normal);

                uint index = lat * _NumInstanceLon + lon;
                instanceData[index] = float4x4.TRS(pos, rot, new float3(scale));
            }
        }

        _TransformBuffer.SetData(instanceData);

        _InitCommandData[0].indexCountPerInstance = _Mesh.GetIndexCount(0);
        _InitCommandData[0].instanceCount = 0;
        _InitCommandData[0].startIndex = (uint)_Mesh.GetIndexStart(0);
        _InitCommandData[0].baseVertexIndex = (uint)_Mesh.GetBaseVertex(0);
        _InitCommandData[0].startInstance = 0;

        _InitCommandData[1].indexCountPerInstance = _Mesh.GetIndexCount(1);
        _InitCommandData[1].instanceCount = 0;
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

        if (_TreeCullingShader != null)
        {
            int kernel = _TreeCullingShader.FindKernel("TreeCulling");
            _CommandBuffer.SetData(_InitCommandData);

            _TreeCullingShader.SetInt("_InstanceCount", (int)_NumberOfInstances);
            _TreeCullingShader.SetVector("_CameraPos", Camera.main.transform.position);
            _TreeCullingShader.SetFloat("_MaxDrawDistance", _MaxDrawDistance);
            _TreeCullingShader.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

            _TreeCullingShader.SetBuffer(kernel, "_AllTransforms", _TransformBuffer);
            _TreeCullingShader.SetBuffer(kernel, "_VisibleTransforms", _VisibleTransformBuffer);
            _TreeCullingShader.SetBuffer(kernel, "_IndirectArgs", _CommandBuffer);

            _TreeCullingShader.Dispatch(
                kernel,
                Mathf.CeilToInt(_NumberOfInstances / 64f),
                1,
                1
            );
        }

        var rp = new RenderParams(_BarkMaterial);
        rp.worldBounds = new Bounds(Vector3.zero, 10000f * Vector3.one);
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_TransformBuffer", _VisibleTransformBuffer);
        rp.matProps.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

        Graphics.RenderMeshIndirect(rp, _Mesh, _CommandBuffer, 1, 0);

        rp.material = _LeavesMaterial;
        Graphics.RenderMeshIndirect(rp, _Mesh, _CommandBuffer, 1, 1);
    }
}
