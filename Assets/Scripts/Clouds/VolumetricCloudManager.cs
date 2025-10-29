using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[ExecuteAlways]
public class VolumetricCloudManager : MonoBehaviour
{
    public Material cloudMat;
    private CloudRenderer[] cloudRenderers;
    ComputeBuffer buffer;

    private void CleanBuffer()
    {
        if (buffer != null) buffer.Release();
    }

    public void GatherRenderers()
    {
        CleanBuffer();
        cloudRenderers = FindObjectsByType<CloudRenderer>(FindObjectsSortMode.None);
        buffer = new ComputeBuffer(cloudRenderers.Length * 8, sizeof(float));
    }

    private void Start()
    {
        GatherRenderers();
    }

    private void Update()
    {
        if (buffer == null)
            return;

        float[] rawData = new float[cloudRenderers.Length*8];

        for (int i = 0; i < cloudRenderers.Length; i++)
        {
            rawData[i * 8] = cloudRenderers[i].gameObject.transform.position.x;
            rawData[i * 8 + 1] = cloudRenderers[i].gameObject.transform.position.y;
            rawData[i * 8 + 2] = cloudRenderers[i].gameObject.transform.position.z;
            rawData[i * 8 + 3] = cloudRenderers[i].CloudParameters.planetRadius;
            rawData[i * 8 + 4] = cloudRenderers[i].CloudParameters.minHeight;
            rawData[i * 8 + 5] = cloudRenderers[i].CloudParameters.maxHeight;
            rawData[i * 8 + 6] = cloudRenderers[i].CloudParameters.size;
            rawData[i * 8 + 7] = cloudRenderers[i].CloudParameters.speed;
        }

        buffer.SetData(rawData);
        cloudMat.SetBuffer("planetDataBuffer", buffer);
    }

    private void OnDestroy()
    {
        CleanBuffer();
    }

    void OnDisable()
    {
        CleanBuffer();
    }
}

[CustomEditor(typeof(VolumetricCloudManager))]
class DVolumetricCloudManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        VolumetricCloudManager manager = (VolumetricCloudManager)target;

        if (GUILayout.Button("Generate"))
            manager.GatherRenderers();
    }
}
