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

        public bool Equals(QuadNode other) => other.Coords.x == Coords.x &&
                                             other.Coords.y == Coords.y &&
                                             other.Depth == Depth;

        public override int GetHashCode() => Coords.x.GetHashCode() ^ Coords.y.GetHashCode() ^ Depth.GetHashCode();
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
        private List<Bounds> _BoundsToDraw = new();

        public double SplitDistanceFactor = 1.0;

        public TerrainQuadTree(double rootSize, double minLeafSize, Transform terrainTransform)
        {
            _RootSize = rootSize;
            _MinLeafSize = minLeafSize;
            this._TerrainTransform = terrainTransform;
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

        public void CollectLeavesDistance(
            Vector3 camPos, Plane[] frustumPlanes,
            List<QuadNode> outLeaves, ref int budget)
        {
            outLeaves.Clear();
            _BoundsToDraw.Clear();
            var root = new QuadNode { Coords = new int2(0, 0), Depth = 0 };
            var rootBounds = GetNodeBounds(root);
            TraverseTree(camPos, frustumPlanes, root, rootBounds, outLeaves, ref budget);
        }

        private void TraverseTree(
            Vector3 camPos, Plane[] frustumPlanes,
            QuadNode key, QuadNodeBounds b,
            List<QuadNode> leaves, ref int budget)
        {
            Vector3 worldCenter = _TerrainTransform.TransformPoint(new Vector3((float)b.Center.x, 0f, (float)b.Center.z));
            var aabb = new Bounds(
                worldCenter,
                new Vector3((float)b.Size, (float)b.Size, (float)b.Size)); // random high height
            _BoundsToDraw.Add(aabb);
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, aabb))
                return;

            float dist = Vector3.Distance(camPos, worldCenter);

            bool canSplit = b.Size > _MinLeafSize && budget > 0;
            bool wantSplit = canSplit && (dist < b.Size * SplitDistanceFactor);

            if (wantSplit)
            {

                double childSize = b.Size * 0.5;
                double h = childSize * 0.5;
                int d1 = key.Depth + 1;

                // top left
                var topLeftNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 0, key.Coords.y * 2 + 1), Depth = d1 };
                TraverseTree(camPos, frustumPlanes,
                    topLeftNode,
                    new QuadNodeBounds
                    {
                        Center = b.Center + new double3(-h, 0f, +h),
                        Size = childSize
                    },
                    leaves, ref budget
                );

                // top right
                var topRightNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 1, key.Coords.y * 2 + 1), Depth = d1 };
                TraverseTree(camPos, frustumPlanes,
                    topRightNode,
                    new QuadNodeBounds
                    {
                        Center = b.Center + new double3(+h, 0f, +h),
                        Size = childSize
                    },
                    leaves, ref budget
                );

                // bottom left
                var bottomLeftNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 0, key.Coords.y * 2 + 0), Depth = d1 };
                TraverseTree(camPos, frustumPlanes,
                    bottomLeftNode,
                    new QuadNodeBounds
                    {
                        Center = b.Center + new double3(-h, 0f, -h),
                        Size = childSize
                    },
                    leaves, ref budget
                );

                // bottom right
                var bottomRightNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 1, key.Coords.y * 2 + 0), Depth = d1 };
                TraverseTree(camPos, frustumPlanes,
                    bottomRightNode,
                    new QuadNodeBounds
                    {
                        Center = b.Center + new double3(+h, 0f, -h),
                        Size = childSize
                    },
                    leaves, ref budget
                );
            }
            else
            {
                budget--;
                leaves.Add(key);
            }
        }
    }
}
