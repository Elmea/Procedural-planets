using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Data/Tree")]
public class ProceduralTreeParameters : ScriptableObject
{
    [field:SerializeField] public int Seed { get; set; }
    
    [field:SerializeField] public int TreeLevels { get; set;} = 3;
    
    [field: SerializeField] public Material BarkMat { get; set; } = null;
    [field:SerializeField] public List<int> ChildBranchesCounts { get; set;} = new();
    
    [field:SerializeField] public List<float> BranchLength {get; set;} = new();
    [field:SerializeField] public List<float> BranchRadius {get; set;} = new();
    [field:SerializeField] public List<int> BranchSectionCount {get; set;} = new();
    [field:SerializeField] public List<int> MeshSegmentCount {get; set;} = new();
    
    /// <summary>
    /// The strength by which the tree can bend. I advise for the angle to be between -30 and 30.
    /// </summary>
    [field:SerializeField] public List<float> Gnarliness {get; set;} = new();
    
    [field:SerializeField] public Growth TreeGrowth {get; set;} = new();
    
    /// <summary>
    /// Child branches pitch angle
    /// </summary>
    [field:SerializeField] public List<float> Angle {get; set;} = new();
    
    /// <summary>
    /// How much the radius of the branch is thinning per section. Between 0 and 1.
    /// </summary>
    [field:SerializeField] public List<float> Thinning { get; set;} = new() { 0.7f, 0.7f, 0.7f, 0.7f };
    
    /// <summary>
    /// Where can a branch emerge along the parent branch.  Between 0 and 1.
    /// </summary>
    [field:SerializeField] public List<Vector2> ChildBranchEmergencePos {get; set;} = new();

    [field: SerializeField] public Material LeavesMat { get; set; } = null;
    [field:SerializeField] public bool GenerateLeaves {get; set;} = true;
    [field:SerializeField] public int LeavesCount {get; set;} = 50;
    [field:SerializeField] public Vector2 LeavesEmergenceStartEnd {get; set;} = new Vector2(0.1f, 1f);
    [field:SerializeField] public float LeavesAngle {get; set;} = 50f;
    [field:SerializeField] public float LeafSize {get; set;} = 4f;
    [field:SerializeField] public float LeafSizeVariance {get; set;} = 0.7f;
}
