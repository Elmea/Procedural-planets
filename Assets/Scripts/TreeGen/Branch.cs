using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Branch
{
    private TreeParameters parameters;
    private BranchConstructionData constructionData;
    
    [NonSerialized] public List<Vector3> vertices = new();
    [NonSerialized] public List<Vector2> uvs = new();
    [NonSerialized] public List<int> indices = new();
    
    [NonSerialized] public List<Vector3> leavesVertices = new();
    [NonSerialized] public List<Vector2> leavesUvs = new();
    [NonSerialized] public List<int> leavesIndices = new();
    
    public List<Branch> branches = new();

    public struct BranchConstructionData
    {
        public Vector3 origin;
        public Vector3 orientation;
        public int level;
        public int branches;
        public float length;
        public float radius;
        public float thinning;
        public float gnarliness;
        public int sectionCount;
        public int segmentCount; //min 3 to form a triangle

        public BranchConstructionData(TreeParameters parameters, Vector3 origin, Vector3 orientation, int level)
        {
            this.origin = origin;
            this.level = level;
            this.orientation = orientation;
            this.branches = level == parameters.branches.Count ? 0 : parameters.branches[level];
            this.length = parameters.lengths[level];
            this.radius = parameters.radius[level];
            this.thinning = parameters.thinning[level];
            this.gnarliness = parameters.gnarliness[level];
            this.sectionCount = parameters.sectionCount[level];
            this.segmentCount = parameters.meshSegmentCount[level];
        }
    }
    
    private struct Section
    {
        public Vector3 origin;
        public Vector3 orientation;
        public float radius;

        public Section(Vector3 origin, Vector3 orientation, float radius)
        {
            this.origin = origin;
            this.orientation = orientation;
            this.radius = radius;
        }
    }
    
    public Branch(TreeParameters parameters, BranchConstructionData branchData)
    {
        this.parameters = parameters;
        this.constructionData = branchData;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>A list of sub-branches</returns>
    public void GenerateBranch()
    {
        Vector3 sectionOrientation = constructionData.orientation;
        Vector3 sectionOrigin = constructionData.origin;
        float sectionLength = constructionData.length / constructionData.sectionCount;
        
        //Generate branch sections
        List<Section> sections = new();
        for (int i = 0; i <= constructionData.sectionCount; i++)
        {
            float sectionRadius = constructionData.radius;
            
            //If this section is final level, set radius to 0
            if (i == constructionData.sectionCount && constructionData.level == parameters.level)
                sectionRadius = 0.001f;
            else //Thin the section. The higher the section, the higher effect thinning has
                 sectionRadius *= 1 - constructionData.thinning * (i / (float)constructionData.sectionCount);
            
            //Create the segments of this section, i.e. how many segments will form the circumference 
            Vector3 firstVertex = Vector3.zero;
            Vector2 firstUV = Vector2.zero;
            for (int j = 0; j < constructionData.segmentCount; j++)
            {
                float angle = (2f * Mathf.PI * j) / constructionData.segmentCount;

                Vector3 segmentPos = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                
                //Create segment geometry data
                Vector3 vertex = Quaternion.Euler(sectionOrientation) * (segmentPos *  sectionRadius) + sectionOrigin;
                Vector2 uv = new Vector2(j / (float)constructionData.segmentCount, i % 2);

                vertices.Add(vertex);
                uvs.Add(uv);
                
                if (j == 0)
                {
                    firstVertex = vertex;
                    firstUV = uv;
                }
            }
            
            //Duplicate first data for continuity
            vertices.Add(firstVertex);
            uvs.Add(firstUV);
            
            sections.Add(new Section(
                new Vector3(sectionOrigin.x, sectionOrigin.y, sectionOrigin.z),
                new Vector3(sectionOrientation.x, sectionOrientation.y, sectionOrientation.z),
                sectionRadius
                ));
            
            sectionOrigin += Quaternion.Euler(sectionOrientation) * new Vector3(0, sectionLength, 0);
            
            float gnarliness = 
                Mathf.Max(1f, 1f / Mathf.Sqrt(sectionRadius)) * 
                constructionData.gnarliness;
            
            sectionOrientation.x += Random.Range(-gnarliness, gnarliness);
            sectionOrientation.z += Random.Range(-gnarliness, gnarliness);
            
            Quaternion qSection = Quaternion.Euler(sectionOrientation);
            Quaternion qForce = Quaternion.FromToRotation(Vector3.up, parameters.growth.direction.normalized);
            
            qSection = Quaternion.RotateTowards(qSection, qForce, 
                (parameters.growth.strength * (Mathf.PI / 180)) / sectionRadius);
            
            sectionOrientation = qSection.eulerAngles;
        }
        
        GenerateIndices();

        Section lastSection = sections[^1];

        if (constructionData.level < parameters.level)
        {
            BranchConstructionData data = new BranchConstructionData(
                parameters, 
                lastSection.origin,
                lastSection.orientation,
                constructionData.level + 1
                );
            
            data.radius = lastSection.radius;
            data.sectionCount = constructionData.sectionCount;
            data.segmentCount = constructionData.segmentCount;
            
            Branch branch = new Branch(parameters, data);
                
            branches.Add(branch);
            branch.GenerateBranch();
        }
        else if (parameters.generateLeaves)
            GenerateLeaf(lastSection.origin, lastSection.orientation);

        if (constructionData.level < parameters.level)
        {
            List<Branch> childBranches = GenerateChildBranches(
                constructionData.branches, 
                constructionData.level + 1, 
                sections);
            
            foreach (Branch branch in childBranches)
                branch.GenerateBranch();
            
            branches.AddRange(childBranches);
        }
        else if (parameters.generateLeaves)
            GenerateLeaves(sections);
    }

    private List<Branch> GenerateChildBranches(int count, int branchLevel, List<Section> sections)
    {
        float radialOffset = Random.value;
        
        List<Branch> childBranches = new List<Branch>();
        for (int i = 0; i < count; i++)
        {
            //Gives a percentage of how far along the parent branch the child branch emerges
            float branchEmergenceAlpha = Random.Range(parameters.childBranchEmergencePos[branchLevel - 1].x, parameters.childBranchEmergencePos[branchLevel - 1].y);
            
            // Find which sections are on either side of the child branch origin point
            // so we can determine the origin, orientation and radius of the branch
            int sectionIndex = Mathf.FloorToInt(branchEmergenceAlpha * (sections.Count - 1));
            Section a = sections[sectionIndex];
            Section b = sectionIndex == sections.Count - 1 ? a : sections[sectionIndex + 1];

            // Find normalized distance from section A to section B (0 to 1)
            float alpha = branchEmergenceAlpha * (sections.Count - 1) - sectionIndex;
            
            Vector3 branchEmergence = Vector3.Lerp(a.origin, b.origin, alpha);
            float radius = parameters.radius[branchLevel] * Mathf.Lerp(a.radius, b.radius, alpha);
            Vector3 parentOrientation = Quaternion.Slerp(Quaternion.Euler(a.orientation), Quaternion.Euler(b.orientation), alpha).eulerAngles;

            float radialAngle = 2f * Mathf.PI * (radialOffset + i / (float)count);
            Quaternion q1 = Quaternion.AngleAxis(parameters.angle[branchLevel - 1], Vector3.right);
            Quaternion q2 = Quaternion.AngleAxis(radialAngle * (180f / Mathf.PI), Vector3.up);
            Quaternion q3 = Quaternion.Euler(parentOrientation);
            
            Vector3 childBranchOrientation = (q3 * (q2 * q1)).eulerAngles;
            
            BranchConstructionData newData = new BranchConstructionData(
                parameters, 
                branchEmergence, 
                childBranchOrientation, 
                branchLevel
                );
            
            newData.radius = radius;
            
            childBranches.Add(new Branch(parameters, newData));
        }
        
        return childBranches;
    }

    private void GenerateLeaves(List<Section> sections)
    {
        //Radian
        float radialOffset = Random.value;

        for (int i = 0; i < parameters.leavesCount; i++)
        {
            //Gives a percentage of how far along the parent branch the child branch emerges
            float leafEmergenceAlpha = Random.Range(parameters.leavesEmergenceStartEnd.x, parameters.leavesEmergenceStartEnd.y);
            
            // Find which sections are on either side of the child branch origin point
            // so we can determine the origin, orientation and radius of the branch
            int sectionIndex = Mathf.FloorToInt(leafEmergenceAlpha * (sections.Count - 1));
            Section a = sections[sectionIndex];
            Section b = sectionIndex == sections.Count - 1 ? a : sections[sectionIndex + 1];
            
            // Find normalized distance from section A to section B (0 to 1)
            float alpha = leafEmergenceAlpha * (sections.Count - 1) - sectionIndex;
            
            Vector3 leafOrigin = Vector3.Lerp(a.origin, b.origin, alpha);
            Vector3 parentOrientation = Quaternion.Slerp(Quaternion.Euler(a.orientation), Quaternion.Euler(b.orientation), alpha).eulerAngles;
            
            float radialAngle = 2f * Mathf.PI * (radialOffset + i / (float)parameters.leavesCount);
            Quaternion q1 = Quaternion.AngleAxis(parameters.leavesAngle, Vector3.right);
            Quaternion q2 = Quaternion.AngleAxis(radialAngle * (180f / Mathf.PI), Vector3.up);
            Quaternion q3 = Quaternion.Euler(parentOrientation);
            
            Vector3 leafOrientation = (q3 * (q2 * q1)).eulerAngles;

            GenerateLeaf(leafOrigin, leafOrientation);
        }
    }

    private void GenerateLeaf(Vector3 leafOrigin, Vector3 leafOrientation)
    {
        // Width and length of the leaf quad
        float leafSize = parameters.leafSize * (1 + Random.Range(-parameters.leafSizeVariance, parameters.leafSizeVariance));

        float w = leafSize;
        float l = leafSize;

        void CreateLeaf(float rotation)
        {
            int off = leavesVertices.Count;
            
            List<Vector3> quad = new List<Vector3>
            {
                new(-w / 2, l , 0),
                new(-w / 2, 0, 0),
                new(w / 2, 0, 0),
                new(w / 2, l, 0),
            };

            for (int i = 0; i < quad.Count; i++)
                quad[i] = Quaternion.Euler(leafOrientation) * (Quaternion.Euler(0, rotation * (180f / Mathf.PI), 0) * quad[i]) + leafOrigin;

            leavesVertices.AddRange(quad);
        
            leavesUvs.AddRange(new List<Vector2>
            {
                new(0, 1),
                new(0, 0),
                new(1, 0),
                new(1, 1),
            });
        
            leavesIndices.AddRange(new List<int> { 0 + off, 1 + off, 2 + off, 0 + off, 2 + off, 3 + off });
        }
        
        CreateLeaf(0);
        CreateLeaf(Mathf.PI / 2f);
    }
    
    private void GenerateIndices()
    {
        int n = constructionData.segmentCount + 1;

        // Build geometry each section of the branch (cylinder without end caps)
        for (int i = 0; i < constructionData.sectionCount; i++)
        {      
            // Build the quad for each segment of the section
            for (int j = 0; j < constructionData.segmentCount; j++)
            {
                var v1 = i * n + j;
                var v2 = i * n + j + 1;
                var v3 = v1 + n;
                var v4 = v2 + n;
                
                //Don't forget to add an index offset when building geometry (which is i / 3)
                indices.AddRange(new List<int>{
                    v1, v3, v2, v2, v3, v4
                });
            }
        }
    }
}
