using UnityEngine;

[CreateAssetMenu(fileName = "CloudParameters", menuName = "Scriptable Objects/CloudParameters")]
public class CloudParameters : ScriptableObject
{
    public float altitude;
    public float cloudHeight;
    public float speed;

    public Material material;
    public Texture3D Noise;
}
