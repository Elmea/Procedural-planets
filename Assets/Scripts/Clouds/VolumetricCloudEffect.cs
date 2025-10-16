using UnityEngine;


[RequireComponent(typeof(Camera))]
public class VolumetricCloudEffect : MonoBehaviour
{
    public Material cloudMat;
    private CloudRenderer[] cloudRenderers;
    ComputeBuffer buffer;

    private void GatherRenderers()
    {
        cloudRenderers = FindObjectsByType<CloudRenderer>(FindObjectsSortMode.None);
    }

    private void Start()
    {
        GatherRenderers();
        buffer = new ComputeBuffer(cloudRenderers.Length * 8, sizeof(float));
    }

    private void Update()
    {
        if (buffer == null)
            return;

        float[] rawData = new float[cloudRenderers.Length*8];

        for (int i = 0; i < cloudRenderers.Length; i++)
        {
            rawData[i] = cloudRenderers[i].gameObject.transform.position.x;
            rawData[i+1] = cloudRenderers[i].gameObject.transform.position.y;
            rawData[i+2] = cloudRenderers[i].gameObject.transform.position.z;
            rawData[i+3] = cloudRenderers[i].CloudParameters.planetRadius;
            rawData[i+4] = cloudRenderers[i].CloudParameters.minHeight;
            rawData[i+5] = cloudRenderers[i].CloudParameters.maxHeight;
            rawData[i+6] = cloudRenderers[i].CloudParameters.thickness;
            rawData[i+7] = cloudRenderers[i].CloudParameters.speed;
        }

        buffer.SetData(rawData);
        cloudMat.SetBuffer("planetDataBuffer", buffer);
    }

    private void OnDestroy()
    {
        if (buffer != null) buffer.Release();
    }

    void OnDisable()
    {
        if (buffer != null) buffer.Release();
    }
}
