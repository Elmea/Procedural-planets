using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

// from the example found in the doc for Graphics.RenderMeshIndirect:
// https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html
public class ForestManager : MonoBehaviour
{
    [SerializeField] Material _BarkMaterial;
    [SerializeField] Material _LeavesMaterial;
    [SerializeField] Mesh _Mesh;
    [SerializeField] uint _NumInstanceLat = 20;
    [SerializeField] uint _NumInstanceLon = 40;
    uint _NumberOfInstances;

    GraphicsBuffer _BarkCommandBuffer;
    GraphicsBuffer _LeavesCommandBuffer;
    GraphicsBuffer _TransformBuffer;

    GraphicsBuffer.IndirectDrawIndexedArgs[] _BarkCommandData;
    GraphicsBuffer.IndirectDrawIndexedArgs[] _LeavesCommandData;

    void Start()
    {
        _NumberOfInstances = _NumInstanceLat * _NumInstanceLon;
        _BarkCommandBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size
        );
        _LeavesCommandBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size
        );

        _BarkCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        _LeavesCommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        _TransformBuffer = new GraphicsBuffer(
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

        _BarkCommandData[0].indexCountPerInstance = _Mesh.GetIndexCount(0);
        _BarkCommandData[0].instanceCount = _NumberOfInstances;
        _BarkCommandData[0].startIndex = (uint)_Mesh.GetIndexStart(0);
        _BarkCommandData[0].baseVertexIndex = (uint)_Mesh.GetBaseVertex(0);
        _BarkCommandData[0].startInstance = 0;

        _LeavesCommandData[0].indexCountPerInstance = _Mesh.GetIndexCount(1);
        _LeavesCommandData[0].instanceCount = _NumberOfInstances;
        _LeavesCommandData[0].startIndex = (uint)_Mesh.GetIndexStart(1);
        _LeavesCommandData[0].baseVertexIndex = (uint)_Mesh.GetBaseVertex(1);
        _LeavesCommandData[0].startInstance = 0;

        _BarkCommandBuffer.SetData(_BarkCommandData);
        _LeavesCommandBuffer.SetData(_LeavesCommandData);
    }

    void OnDestroy()
    {
        _BarkCommandBuffer?.Release();
        _BarkCommandBuffer = null;

        _LeavesCommandBuffer?.Release();
        _LeavesCommandBuffer = null;

        _TransformBuffer?.Release();
        _TransformBuffer = null;
    }

    void Update()
    {
        if (_Mesh == null || _BarkMaterial == null)
            return;

        var rp = new RenderParams(_BarkMaterial);
        rp.worldBounds = new Bounds(Vector3.zero, 10000f * Vector3.one);
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_TransformBuffer", _TransformBuffer);
        rp.matProps.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

        Graphics.RenderMeshIndirect(rp, _Mesh, _BarkCommandBuffer, 1);

        rp.material = _LeavesMaterial;
        Graphics.RenderMeshIndirect(rp, _Mesh, _LeavesCommandBuffer, 1);
    }
}
