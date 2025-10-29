using PlanetGen;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts.PlanetGen
{
    public struct QuadNode : IEquatable<QuadNode>
    {
        public int2 Coords;
        public int Depth;
        public PlanetFace Face;

        public bool Equals(QuadNode other) => other.Coords.x == Coords.x &&
                                              other.Coords.y == Coords.y &&
                                              other.Depth == Depth &&
                                              other.Face == Face;

        public override int GetHashCode() => Coords.x.GetHashCode() ^
                                             Coords.y.GetHashCode() ^
                                             Depth.GetHashCode() ^
                                             Face.GetHashCode();
        public override bool Equals(object obj) => obj is QuadNode other && Equals(other);
    }

    // Bounding sphere
    public struct QuadNodeBounds
    {
        public double3 Center;
        public double Size;
    }

    public struct LodDecision
    {
        public bool Visible;
        public bool ShouldSplit;
        public bool ShouldMerge;
    }

    public delegate LodDecision EvaluateVisibilityDelegate(QuadNodeBounds bounds);

    public sealed class TerrainQuadTree
    {
        public List<Bounds> BoundsToDraw => _BoundsToDraw;
        private readonly double _RootSize;
        private readonly double _MinLeafSize;
        private Transform _TerrainTransform;
        private Matrix4x4 _QuadTreeMatrix;
        private List<Bounds> _BoundsToDraw = new();
        private PlanetFace _HandledFace;
        private bool _EnableCulling;

        public double SplitDistanceFactor = 1.0;

        public TerrainQuadTree(double rootSize, double minLeafSize, 
            float4x4 quadTreeMatrix, Transform terrainTransform,
            PlanetFace face, bool enableCulling)
        {
            _RootSize = rootSize;
            _MinLeafSize = minLeafSize;
            _QuadTreeMatrix = quadTreeMatrix;
            _TerrainTransform = terrainTransform;
            _HandledFace = face;
            _EnableCulling = enableCulling;
        }

        public QuadNodeBounds GetNodeBounds(QuadNode node)
        {
            double size = _RootSize / (1 << node.Depth);
            double minX = -_RootSize * 0.5 + node.Coords.x * size;
            double minY = -_RootSize * 0.5 + node.Coords.y * size;
            return new QuadNodeBounds
            {
                Center = new double3(minX + size * 0.5, 0.0f, minY + size * 0.5),
                Size = size
            };
        }

        public QuadNodeBounds GetWorldNodeBounds(QuadNode node)
        {
            QuadNodeBounds b = GetNodeBounds(node);
            Vector3 center = _QuadTreeMatrix * new float4(((float3)b.Center), 1);
            center = center.normalized * (float)_RootSize * 0.5f; // project to sphere surface
            b.Center = (float3)center;
            return b;
        }

        public Vector3 GetQuadCenterNoRot(QuadNode node)
        {
            QuadNodeBounds b = GetNodeBounds(node);
            b.Center.y = _RootSize * 0.5; // lift above 0 height
            return (float3)b.Center;
        }

        public Matrix4x4 GetQuadTreeMatrix()
        {
            return _QuadTreeMatrix;
        }

        public void CollectLeavesDistance(
            Vector3 camPos, Plane[] frustumPlanes, HashSet<QuadNode> activeNodes,
            List<QuadNode> outLeaves, ref int budget)
        {
            _BoundsToDraw.Clear();
            var root = new QuadNode { Coords = new int2(0, 0), Depth = 0, Face = _HandledFace };
            TraverseTree(camPos, frustumPlanes, root, activeNodes, outLeaves, ref budget);
        }

        private void TraverseTree(
            Vector3 camPos, Plane[] frustumPlanes,
            QuadNode key, HashSet<QuadNode> activeNodes,
            List<QuadNode> leaves, ref int budget)
        {
            QuadNodeBounds worldBounds = GetWorldNodeBounds(key);
            Vector3 worldCenter = (float3)worldBounds.Center;
            worldCenter = _TerrainTransform.TransformPoint(worldCenter);
            var aabb = new Bounds(
                worldCenter,
                new Vector3((float)worldBounds.Size, (float)worldBounds.Size, (float)worldBounds.Size)); // random high height
            _BoundsToDraw.Add(aabb);
            if (_EnableCulling && !GeometryUtility.TestPlanesAABB(frustumPlanes, aabb))
                return;

            float dist = Vector3.Distance(camPos, worldCenter);

            bool canSplit = worldBounds.Size > _MinLeafSize && budget > 0;
            bool wantSplit = canSplit && (dist < worldBounds.Size * SplitDistanceFactor);

            if (wantSplit)
            {
                double childSize = worldBounds.Size * 0.5;
                double h = childSize * 0.5;
                int d1 = key.Depth + 1;

                // top left
                var topLeftNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 0, key.Coords.y * 2 + 1),
                    Depth = d1, Face = _HandledFace };
                TraverseTree(camPos, frustumPlanes,
                    topLeftNode,
                    activeNodes, leaves, ref budget
                );

                // top right
                var topRightNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 1, key.Coords.y * 2 + 1),
                    Depth = d1, Face = _HandledFace };
                TraverseTree(camPos, frustumPlanes,
                    topRightNode,
                    activeNodes, leaves, ref budget
                );

                // bottom left
                var bottomLeftNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 0, key.Coords.y * 2 + 0),
                    Depth = d1, Face = _HandledFace };
                TraverseTree(camPos, frustumPlanes,
                    bottomLeftNode,
                    activeNodes, leaves, ref budget
                );

                // bottom right
                var bottomRightNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 1, key.Coords.y * 2 + 0),
                    Depth = d1, Face = _HandledFace };
                TraverseTree(camPos, frustumPlanes,
                    bottomRightNode,
                    activeNodes, leaves, ref budget
                );
            }
            else
            {
                if (budget > 0)
                {
                    if (activeNodes.Contains(key) == false)
                        budget--;
                    leaves.Add(key);
                }
            }
        }
    }
}
