using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Must;

public class CloudRenderer : MonoBehaviour
{
    public CloudParameters CloudParameters;

    private void Awake()
    {
    }
}

[CustomEditor(typeof(CloudRenderer))]
public class GeneratedCloudEditor : Editor
{

    public override void OnInspectorGUI() 
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate"))
        {

        }
    }
}