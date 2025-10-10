using System.Collections.Generic;
using UnityEngine;

public class Branch
{
    public Vector3 origin;
    public Vector3 orientation;
    public float length;
    public float radius;
    public int level;
    public int sectionCount;

    public Branch(Vector3 origin, Vector3 orientation, float length, float radius, int level, int sectionCount)
    {
        this.origin = origin;
        this.orientation = orientation;
        this.length = length;
        this.radius = radius;
        this.level = level;
        this.sectionCount = sectionCount;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>A list of sub branches</returns>
    public List<Branch> GenerateBranch()
    {
        return new List<Branch>();
    }
}
