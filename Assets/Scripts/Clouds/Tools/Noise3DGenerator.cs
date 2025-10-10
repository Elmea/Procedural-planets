using UnityEngine;
using UnityEditor;

public class Noise3DGenerator : EditorWindow
{
    int size = 32;
    float scale = 10f;
    string saveName = "NewNoise3D";

    [MenuItem("Tools/Noise 3D Generator")]
    public static void ShowWindow()
    {
        GetWindow<Noise3DGenerator>("Noise 3D Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("Noise parameters", EditorStyles.boldLabel);

        size = EditorGUILayout.IntSlider("Size", size, 4, 128);
        scale = EditorGUILayout.Slider("Scale", scale, 1f, 50f);
        saveName = EditorGUILayout.TextField("File name", saveName);

        if (GUILayout.Button("Generate"))
        {
            Texture3D tex = GenerateNoise3D(size, scale);
            SaveTexture(tex, saveName);
        }
    }

    Texture3D GenerateNoise3D(int size, float scale)
    {
        Texture3D tex = new Texture3D(size, size, size, TextureFormat.RFloat, false);
        Color[] cols = new Color[size * size * size];

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    float nx = (float)x / size * scale;
                    float ny = (float)y / size * scale;
                    float nz = (float)z / size * scale;

                    float val = Mathf.PerlinNoise(nx, ny) * 0.5f
                              + Mathf.PerlinNoise(ny, nz) * 0.25f
                              + Mathf.PerlinNoise(nx, nz) * 0.25f;

                    cols[x + y * size + z * size * size] = new Color(val, val, val, 1);
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    void SaveTexture(Texture3D tex, string name)
    {
        string path = "Assets/GeneratedTexture/" + name + ".asset";
        AssetDatabase.CreateAsset(tex, path);
        AssetDatabase.SaveAssets();
    }
}