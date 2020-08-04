﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityTools.Algorithm;
using UnityTools.Common;

namespace UnityFishSimulation
{
    public interface FishSolver
    {
        void Step(FishModelData fish, float dt);
        void PreSolve(FishModelData fish);
        void ApplyForces(FishModelData fish);
        void Intergrate(FishModelData fish, float dt);
        void PostSolve(FishModelData fish);
    }

    [System.Serializable]
    public class FishEularSolver : FishSolver
    {
        [SerializeField, Range(0.01f, 1)] protected float fluidForceScale = 1f;

        public void Step(FishModelData fish, float dt = 0.055F)
        {
            var step = 10;
            dt = dt / step;
            foreach (var value in Enumerable.Range(0, step))
            {
                this.PreSolve(fish);
                this.ApplyForces(fish);
                this.Intergrate(fish, dt);
                this.PostSolve(fish);
            }
        }

        public void PreSolve(FishModelData fish)
        {
            foreach (var n in fish.FishGraph.Nodes) n.Force = 0;
        }
        public void ApplyForces(FishModelData fish)
        {
            this.ApplySpringForce(fish);
            this.ApplyFluidForce(fish);
        }

        public void Intergrate(FishModelData fish, float dt)
        {
            foreach (var n in fish.FishGraph.Nodes)
            {
                var newVelocity = n.Velocity + (n.Force / n.Mass) * dt;
                n.Force += -fish.Damping * newVelocity;

                n.Velocity += (n.Force / n.Mass) * dt;
                n.Position += n.Velocity * dt;
            }
        }

        public void PostSolve(FishModelData fish)
        {
        }

        protected void ApplySpringForce(FishModelData fish)
        {
            foreach (var n in fish.FishGraph.Nodes)
            {
                var force = this.GetSpringForce(n, fish);
                n.Force += force;
            }
        }

        protected void ApplyFluidForce(FishModelData fish)
        {
            foreach (var face in fish.FishNormalFace)
            {
                face.ApplyForceToNode(this.fluidForceScale);
            }
        }

        protected float3 GetSpringForce(MassPoint i, FishModelData fish)
        {
            var neighbors = fish.FishGraph.GetNeighborsNode(i);
            var ret = float3.zero;

            foreach (var j in neighbors)
            {
                var r = j.Position - i.Position;
                var r_ij = math.length(r);
                var s_ij = fish.FishGraph.GetEdge(i, j);
                Assert.IsNotNull(s_ij);

                var e_ij = r_ij - s_ij.CurrentL;

                var u_ij = j.Velocity - i.Velocity;
                var r_dot = (u_ij * r) / r_ij;

                var force_ij = (((s_ij.C * e_ij) + (s_ij.K * r_dot)) / r_ij) * r;
                ret += force_ij;
            }

            return ret;
        }
    }

    [System.Serializable]
    public class FishMatrixSolver : FishSolver
    {
        [SerializeField, Range(0.01f, 1)] protected float fluidForceScale = 1f;

        public void Step(FishModelData fish, float dt = 0.055F)
        {
            var step = 1;
            dt = dt / step;
            foreach (var value in Enumerable.Range(0, step))
            {
                this.PreSolve(fish);
                this.ApplyForces(fish);
                this.Intergrate(fish, dt);
                this.PostSolve(fish);
            }
        }

        public void PreSolve(FishModelData fish)
        {
            foreach (var n in fish.FishGraph.Nodes) n.Force = 0;
        }
        public void ApplyForces(FishModelData fish)
        {
            this.ApplyFluidForce(fish);
        }

        public void Intergrate(FishModelData fish, float dt)
        {
            //var na = 7;

            var dim = fish.FishGraph.AdjMatrix.Size;
            var nodes = fish.FishGraph.Nodes.ToList();

            var At = new Matrix<float3>(dim.x, dim.y);
            var Gt = new Vector<float3>(dim.x);

            foreach (var s_ij in fish.FishGraph.Edges)
            {
                Assert.IsNotNull(s_ij);

                var ni = s_ij.Left;
                var nj = s_ij.Right;
                var i = ni.Index;
                var j = nj.Index;

                var n_ij = GetN(ni, nj, s_ij.C, s_ij.K, s_ij.CurrentL);
                var r_ij = nj.Position - ni.Position;

                At[i, i] = At[i, i] + n_ij * dt;
                At[j, j] = At[j, j] + n_ij * dt;

                At[i, j] = At[j, i] = -n_ij * dt;

                Gt[i] = Gt[i] + n_ij * r_ij;
                Gt[j] = Gt[j] - n_ij * r_ij;
            }



            for (var i = 0; i < nodes.Count; ++i)
            {
                var mi = nodes[i].Mass;
                var fi = nodes[i].Force;
                var vi = nodes[i].Velocity;
                At[i, i] = At[i, i] + mi / dt;
                Gt[i] = Gt[i] + fi + (mi / dt) * vi;
            }


            var L = new Matrix<float3>(dim.x, dim.y);
            var D = new Matrix<float3>(dim.x, dim.y);
            var LT = new Matrix<float3>(dim.x, dim.y);
            for (var i = 0; i < dim.x; ++i)
            {
                for (var j = 0; j < dim.y; ++j)
                {
                    if (i >= j) L[i, j] = At[i, j];
                    if (i == j) D[i, j] = At[i, j];
                    if (i <= j) LT[i, j] = At[i, j];
                }
            }

            //this.Print(L, "L", true);
            //this.Print(D, "D", true);
            //this.Print(LT, "LT", true);

            var Q = Solver.SolverFS(L, Gt);
            //this.Print(Q, "Q", true);
            //D-1Q
            for (var i = 0; i < dim.x; ++i)
            {
                Q[i] *= 1f / D[i, i];
            }
            //this.Print(Q, "Q_1", true);

            var X_dot = Solver.SolverBS(LT, Q);

            //this.Print(X_dot, "X_Velocity", true);

            foreach (var n in fish.FishGraph.Nodes)
            {
                n.Velocity = X_dot[n.Index];
                n.Position += n.Velocity * dt;
            }
        }

        public void PostSolve(FishModelData fish)
        {
        }
        protected void ApplyFluidForce(FishModelData fish)
        {
            foreach (var face in fish.FishNormalFace)
            {
                face.ApplyForceToNode(this.fluidForceScale);
            }
        }

        protected float3 GetN(MassPoint i, MassPoint j, float c, float k, float l)
        {
            var r = j.Position - i.Position;
            var r_ij = math.length(r);

            var e_ij = r_ij - l;

            var u_ij = j.Velocity - i.Velocity;
            var r_dot = math.dot(u_ij, r) / r_ij;

            var n_ij = ((c * e_ij) + (k * r_dot)) / r_ij;

            return n_ij;
        }
    }
}
