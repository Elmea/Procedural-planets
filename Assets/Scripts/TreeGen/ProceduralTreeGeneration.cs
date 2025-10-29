using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct Growth
{
    /// <summary>
    /// As the name imply. Values should be between -1 and 1, will be normalized anyway.
    /// </summary>
    public Vector3 direction;
    
    /// <summary>
    /// The strength of the direction of the growth
    /// </summary>
    public float strength;
}

public struct TreeParameters
{
    public int seed;
    public int level;

    //Branch
    public List<int> branches;
    public List<float> lengths;
    public List<float> radius;
    public List<float> thinning;
    public List<Vector2> childBranchEmergencePos;
    public List<float> angle;
    public List<float> gnarliness;
    public Growth growth;
    public List<int> sectionCount;
    public List<int> meshSegmentCount;
    
    //Leaf
    public bool generateLeaves;
    public int leavesCount;
    public Vector2 leavesEmergenceStartEnd;
    public float leavesAngle;
    public float leafSize;
    public float leafSizeVariance;
}

public struct TreeMeshData
{
    public Vector3[] vertices;
    public Vector2[] uvs;
    public int[] barkIndices;
    public int[] leavesIndices;

    public TreeMeshData(List<Vector3> vertices, List<Vector2> uvs, List<int> barkIndices, List<int> leavesIndices)
    {
        this.vertices = vertices.ToArray();
        this.uvs = uvs.ToArray();
        this.barkIndices = barkIndices.ToArray();
        this.leavesIndices = leavesIndices.ToArray();
    }
}

public static class ProceduralTreeGeneration
{
     public static TreeMeshData GenerateTree(TreeParameters parameters)
     {
         Random.InitState(parameters.seed);
         
         Branch.BranchConstructionData newData = new Branch.BranchConstructionData(
             parameters, 
             Vector3.zero,
             Vector3.zero, 
             0);
         
         //Clean old tree
         Branch trunk = new Branch(parameters, newData);
         trunk.GenerateBranch();
         
         TreeMeshData meshData = GenerateGeometry(trunk);
         return meshData;
     }

     private static TreeMeshData GenerateGeometry(Branch trunk)
     {
         List<Vector3> newVertices = new List<Vector3>();
         List<Vector2> uvs = new List<Vector2>();
         List<int> barkIndices = new List<int>();
         List<int> leavesIndices = new List<int>();

         RetrieveGeometryData(trunk, newVertices, uvs, barkIndices, leavesIndices);
         
         TreeMeshData meshData = new TreeMeshData(newVertices, uvs, barkIndices, leavesIndices);
         return meshData;
     }

     private static void RetrieveGeometryData(Branch branch, List<Vector3> newVertices, List<Vector2> uvs, List<int> barkIndices, List<int> leavesIndices)
     {
         //Branch
         List<int> treeIndices = new(branch.indices);
         for (int i = 0; i < treeIndices.Count; i++)
             treeIndices[i] += newVertices.Count;
         
         newVertices.AddRange(branch.vertices);
         uvs.AddRange(branch.uvs);
         barkIndices.AddRange(treeIndices);

         treeIndices.Clear();
         
         //Leaves
         treeIndices = new(branch.leavesIndices);
         for (int i = 0; i < treeIndices.Count; i++)
             treeIndices[i] += newVertices.Count;
         
         newVertices.AddRange(branch.leavesVertices);
         uvs.AddRange(branch.leavesUvs);
         leavesIndices.AddRange(treeIndices);
         
         foreach (Branch childBranch in branch.branches)
             RetrieveGeometryData(childBranch, newVertices, uvs, barkIndices, leavesIndices);
     }
}


