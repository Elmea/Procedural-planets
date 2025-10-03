using System;
using UnityEngine;

namespace PlanetGen
{
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkGenerator : MonoBehaviour
{
	[SerializeField] private float _ChunkSize = 128f;
	[SerializeField] private int _Resolution = 256;
	
	private MeshFilter _MeshFilter;
	private Mesh _Mesh;

	// Update is called once per frame
    private void Awake()
    {
	    _MeshFilter = GetComponent<MeshFilter>();
	    _Mesh = new() { name = "Chunk" };
	    _MeshFilter.sharedMesh = _Mesh;
    }

    private void Start()
    {
	    Build();
    }

    void Update()
    {
    }
    
    public void Build()
    {
	    Mesh mesh = new() { name = "Chunk" };

	    int vertCount = (_Resolution + 1) * (_Resolution + 1);
	    Vector3[] vertices = new Vector3[vertCount];
	    Vector2[] uvs = new Vector2[vertCount];
	    Vector3[] normals = new Vector3[vertCount];

	    int index = 0;
	    for (int y = 0; y <= _Resolution; y++)
	    {
		    for (int x = 0; x <= _Resolution; x++)
		    {
			    float xPos = (x / (float)_Resolution - 0.5f) * _ChunkSize;
			    float yPos = (y / (float)_Resolution - 0.5f) * _ChunkSize;

			    vertices[index] = new Vector3(xPos, 0, yPos);
			    uvs[index] = new Vector2(x / (float)_Resolution, y / (float)_Resolution); // will it really be useful??
			    normals[index] = Vector3.up;
			    index++;
		    }
	    }

	    int[] triangles = new int[_Resolution * _Resolution * 6];
	    int triIndex = 0;
	    for (int y = 0; y < _Resolution; y++)
	    {
		    for (int x = 0; x < _Resolution; x++)
		    {
			    int i = y * (_Resolution + 1) + x;

			    triangles[triIndex++] = i;
			    triangles[triIndex++] = i + _Resolution + 1;
			    triangles[triIndex++] = i + 1;

			    triangles[triIndex++] = i + 1;
			    triangles[triIndex++] = i + _Resolution + 1;
			    triangles[triIndex++] = i + _Resolution + 2;
		    }
	    }

	    mesh.vertices = vertices;
	    mesh.uv = uvs;
	    mesh.normals = normals;
	    mesh.triangles = triangles;

	    GetComponent<MeshFilter>().mesh = mesh;
    }
}
}
