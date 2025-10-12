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
        public double2 Center;
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
        private readonly double _RootSize;
        private readonly double _MinLeafSize;
        private int _BudgetPerFrame;

        public double SplitDistanceFactor = 1.0;
        public double MergeDistanceFactor = 0.8;

        public TerrainQuadTree(double rootSize, double minLeafSize, double splitPx, double mergePx, int budgetPerFrame)
        {
            _RootSize = rootSize;
            _MinLeafSize = minLeafSize;
            _BudgetPerFrame = budgetPerFrame;
        }

        public QuadNodeBounds GetNodeBounds(QuadNode node)
        {
            double size = _RootSize / (1 << node.Depth);
            double minX = -_RootSize * 0.5 + node.Coords.x * size;
            double minY = -_RootSize * 0.5 + node.Coords.y * size;
            return new QuadNodeBounds
            {
                Center = new double2(minX + size * 0.5, minY + size * 0.5),
                Size = size
            };
        }

        public void CollectLeavesDistance(
            Vector3 camPos, Plane[] frustumPlanes,
            List<QuadNode> outLeaves)
        {
            outLeaves.Clear();
            int budget = _BudgetPerFrame <= 0 ? int.MaxValue : _BudgetPerFrame;
            var root = new QuadNode { Coords = new int2(0, 0), Depth = 0 };
            var rootBounds = GetNodeBounds(root);
            TraverseTree(camPos, frustumPlanes, root, rootBounds, outLeaves, ref budget);
        }

        private void TraverseTree(
            Vector3 camPos, Plane[] frustumPlanes,
            QuadNode key, QuadNodeBounds b,
            List<QuadNode> leaves, ref int budget)
        {
            var aabb = new Bounds(
                new Vector3((float)b.Center.x, 0f, (float)b.Center.y),
                new Vector3((float)b.Size, 20000f, (float)b.Size)); // random high height
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, aabb))
                return;

            float distXZ = DistanceFromPointToSquareXZ(
                new Vector2(camPos.x, camPos.z),
                new Vector2((float)b.Center.x, (float)b.Center.y),
                (float)b.Size * 0.5f);

            bool canSplit = b.Size > _MinLeafSize && budget > 0;
            bool wantSplit = canSplit && (distXZ < b.Size * SplitDistanceFactor);

            if (wantSplit)
            {
                budget--;

                double childSize = b.Size * 0.5;
                double h = childSize * 0.5;
                int d1 = key.Depth + 1;

                // top right
                var topRightNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 1, key.Coords.y * 2 + 1), Depth = d1 };
                TraverseTree(camPos, frustumPlanes,
                    topRightNode,
                    new QuadNodeBounds
                    {
                        Center = b.Center + new double2(+h, +h),
                        Size = childSize
                    },
                    leaves, ref budget
                );

                // top left
                var topLeftNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 0, key.Coords.y * 2 + 1), Depth = d1 };
                TraverseTree(camPos, frustumPlanes,
                    topLeftNode,
                    new QuadNodeBounds {
                        Center = b.Center + new double2(-h, +h),
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
                        Center = b.Center + new double2(+h, -h),
                        Size = childSize
                    },
                    leaves, ref budget
                );

                // bottom left
                var bottomLeftNode = new QuadNode { Coords = new int2(key.Coords.x * 2 + 0, key.Coords.y * 2 + 0), Depth = d1 };
                TraverseTree(camPos, frustumPlanes,
                    bottomLeftNode,
                    new QuadNodeBounds {
                        Center = b.Center + new double2(-h, -h),
                        Size = childSize
                    },
                    leaves, ref budget
                );
            }
            else
                leaves.Add(key);
        }

        private static float DistanceFromPointToSquareXZ(Vector2 p, Vector2 center, float half)
        {
            float dx = Mathf.Max(Mathf.Abs(p.x - center.x) - half, 0f);
            float dz = Mathf.Max(Mathf.Abs(p.y - center.y) - half, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
