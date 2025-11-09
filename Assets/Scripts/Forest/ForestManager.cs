using UnityEngine;
using Unity.Mathematics;

// from the example found in the doc for Graphics.RenderMeshIndirect:
// https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html
public class ForestManager : MonoBehaviour
{
    [SerializeField] Material _Material;
    [SerializeField] Mesh _Mesh;
    [SerializeField] uint _NumberOfInstances = 10;

    GraphicsBuffer _CommandBuffer;
    GraphicsBuffer _TransformBuffer;

    GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

    void Start()
    {
        _CommandBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments,
            1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size
        );
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        _TransformBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            (int)_NumberOfInstances,
            sizeof(float) * 4 * 4
        );

        float4x4[] instanceData = new float4x4[_NumberOfInstances];
        for (int i = 0; i < _NumberOfInstances; i++)
        {
            float x = i * 1.5f;
            float y = 0f;
            float z = 0f;
            float scale = 1.0f;
            instanceData[i] = float4x4.TRS(new float3(x, y, z), quaternion.identity, new float3(scale, scale, scale));
        }

        _TransformBuffer.SetData(instanceData);

        commandData[0].indexCountPerInstance = _Mesh.GetIndexCount(0);
        commandData[0].instanceCount = _NumberOfInstances;
        commandData[0].startIndex = (uint)_Mesh.GetIndexStart(0);
        commandData[0].baseVertexIndex = (uint)_Mesh.GetBaseVertex(0);
        commandData[0].startInstance = 0;

        _CommandBuffer.SetData(commandData);
    }

    void OnDestroy()
    {
        _CommandBuffer?.Release();
        _CommandBuffer = null;

        _TransformBuffer?.Release();
        _TransformBuffer = null;
    }

    void Update()
    {
        if (_Mesh == null || _Material == null)
            return;

        var rp = new RenderParams(_Material);
        rp.worldBounds = new Bounds(Vector3.zero, 10000f * Vector3.one);
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_TransformBuffer", _TransformBuffer);
        rp.matProps.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

        Graphics.RenderMeshIndirect(rp, _Mesh, _CommandBuffer, 1);
    }
}
