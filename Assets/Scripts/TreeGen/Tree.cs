using System.Collections.Generic;
using UnityEngine;

public class Tree : MonoBehaviour
{
    private List<Branch> branchQueue = new();
    
    [SerializeField] private List<float> lengths = new();
    [SerializeField] private List<float> radius = new();
    [SerializeField] private List<int> sectionCounts = new();
    
    public void GenerateTree()
    {
        //Clean old tree
        branchQueue.Clear();
        
        branchQueue.Add(
            new Branch(
            Vector3.zero, 
            Vector3.zero, 
            lengths[0], 
            radius[0], 
            0, 
            sectionCounts[0]
            )
        );

        while (branchQueue.Count > 0)
        {
            Branch branch = branchQueue[0];
            branchQueue.RemoveAt(0);
            branchQueue.AddRange(branch.GenerateBranch());
        }
    }
}
