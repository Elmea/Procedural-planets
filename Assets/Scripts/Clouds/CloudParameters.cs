using UnityEngine;

[CreateAssetMenu(fileName = "CloudParameters", menuName = "Scriptable Objects/CloudParameters")]
public class CloudParameters : ScriptableObject
{
    public float planetRadius;
    public float minHeight;
    public float maxHeight;
    public float thickness;
    public float speed;
}
