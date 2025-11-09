using UnityEngine;
using Unity.Mathematics;

// from the example found in the doc for Graphics.RenderMeshIndirect:
// https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html
public class ForestManager : MonoBehaviour
{
    [SerializeField] Material _BarkMaterial;
    [SerializeField] Material _LeavesMaterial;
    [SerializeField] Mesh _Mesh;
    [SerializeField] uint _NumberOfInstances = 10;

    GraphicsBuffer _BarkCommandBuffer;
    GraphicsBuffer _LeavesCommandBuffer;
    GraphicsBuffer _TransformBuffer;

    GraphicsBuffer.IndirectDrawIndexedArgs[] _BarkCommandData;
    GraphicsBuffer.IndirectDrawIndexedArgs[] _LeavesCommandData;

    void Start()
    {
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

        float4x4[] instanceData = new float4x4[_NumberOfInstances];
        for (int i = 0; i < _NumberOfInstances; i++)
        {
            float x = i * 10f;
            float y = 0f;
            float z = 0f;
            float scale = 0.25f;
            instanceData[i] = float4x4.TRS(new float3(x, y, z), quaternion.identity, new float3(scale, scale, scale));
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
