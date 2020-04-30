﻿using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.Quadrature;
using BoSSS.Solution.Statistic;
using ilPSP;
using ilPSP.Tracing;
using MPI.Wrappers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoSSS.Solution.Statistic {

    /// <summary>
    /// L2 and H1 distances for DG fields on different meshes
    /// </summary>
    public static class DGField_Norms {

        /// <summary>
        /// Projects a DG field <paramref name="source"/>, which may be defined on some different mesh,
        /// onto the DG field <paramref name="target"/>.
        /// </summary>
        static public void ProjectFromOtherGrid(this ConventionalDGField target, double alpha, ConventionalDGField source, CellQuadratureScheme scheme = null) {
            using(new FuncTrace()) {
                int maxDeg = Math.Max(target.Basis.Degree, source.Basis.Degree);
                var CompQuadRule = scheme.SaveCompile(target.GridDat, maxDeg * 3 + 3); // use over-integration

                if(object.ReferenceEquals(source.GridDat, target.GridDat)) {
                    // +++++++++++++++++
                    // equal grid branch
                    // +++++++++++++++++

                    target.ProjectField(alpha, source.Evaluate, CompQuadRule);

                } else {
                    // +++++++++++++++++++++
                    // different grid branch
                    // +++++++++++++++++++++

                    var eval = new FieldEvaluation(GridHelper.ExtractGridData(source.GridDat));

                    void SourceEval(MultidimensionalArray input, MultidimensionalArray output) {
                        int L = input.GetLength(0);
                        Debug.Assert(output.GetLength(0) == L);

                        eval.Evaluate(1.0, new DGField[] { source }, input, 0.0, output.ResizeShallow(L, 1));

                    }

                    target.ProjectField(alpha, SourceEval, CompQuadRule);
                }
            }
        }



        /// <summary>
        /// Approximate L2 distance between two DG fields; this also supports DG fields on different meshes, 
        /// it could be used for convergence studies.
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <param name="IgnoreMeanValue">
        /// if true, the mean value (mean over entire domain) will be subtracted - this mainly useful for comparing pressures 
        /// </param>
        /// <param name="scheme">
        /// a cell quadrature scheme on the coarse of the two meshes
        /// </param>
        /// <returns></returns>
        static public double L2Distance(this ConventionalDGField A, ConventionalDGField B, bool IgnoreMeanValue = false, CellQuadratureScheme scheme = null) {
            int maxDeg = Math.Max(A.Basis.Degree, B.Basis.Degree);
            int quadOrder = maxDeg * 3 + 3;

            if (A.GridDat.SpatialDimension != B.GridDat.SpatialDimension)
                throw new ArgumentException("Both fields must have the same spatial dimension.");

            if(object.ReferenceEquals(A.GridDat, B.GridDat) && false) {
                // ++++++++++++++
                // equal meshes
                // ++++++++++++++
                CellMask domain = scheme != null ? scheme.Domain : null;

                double errPow2 = A.L2Error(B, domain).Pow2();

                if (IgnoreMeanValue) {
                    // domain volume
                    double Vol = 0;
                    int J = A.GridDat.iGeomCells.NoOfLocalUpdatedCells;
                    for(int j = 0; j < J; j++) {
                        Vol += A.GridDat.iGeomCells.GetCellVolume(j);
                    }
                    Vol = Vol.MPISum();
                    
                    // mean value
                    double mean = A.GetMeanValueTotal(domain) - B.GetMeanValueTotal(domain);

                    // Note: for a field p, we have 
                    // || p - <p> ||^2 = ||p||^2 - <p>^2*vol
                    return Math.Sqrt(errPow2 - mean * mean * Vol);
                } else {
                     return Math.Sqrt(errPow2);
                }

            } else {
                // ++++++++++++++++++
                // different meshes
                // ++++++++++++++++++


                DGField fine, coarse;
                if(A.GridDat.CellPartitioning.TotalLength > B.GridDat.CellPartitioning.TotalLength) {
                    fine = A;
                    coarse = B;
                } else {
                    fine = B;
                    coarse = A;
                }
                
                var CompQuadRule = scheme.SaveCompile(coarse.GridDat, maxDeg * 3 + 3); // use over-integration

                var eval = new FieldEvaluation(GridHelper.ExtractGridData(fine.GridDat));

                void FineEval(MultidimensionalArray input, MultidimensionalArray output) {
                    int L = input.GetLength(0);
                    Debug.Assert(output.GetLength(0) == L);

                    eval.Evaluate(1.0, new DGField[] { fine }, input, 0.0, output.ResizeShallow(L, 1));
                   
                }


                double errPow2 = coarse.LxError(FineEval, (double[] X, double fC, double fF) => (fC - fF).Pow2(), CompQuadRule, Quadrature_ChunkDataLimitOverride:int.MaxValue);

                if(IgnoreMeanValue == true) {
                    
                    // domain volume
                    double Vol = 0;
                    int J = coarse.GridDat.iGeomCells.NoOfLocalUpdatedCells;
                    for(int j = 0; j < J; j++) {
                        Vol += coarse.GridDat.iGeomCells.GetCellVolume(j);
                    }
                    Vol = Vol.MPISum();

                    // mean value times domain volume 
                    double meanVol = coarse.LxError(FineEval, (double[] X, double fC, double fF) => fC - fF, CompQuadRule, Quadrature_ChunkDataLimitOverride:int.MaxValue);
                    

                    // Note: for a field p, we have 
                    // || p - <p> ||^2 = ||p||^2 - <p>^2*vol
                    return Math.Sqrt(errPow2 - meanVol * meanVol / Vol);
                } else {

                    return Math.Sqrt(errPow2);
                }
            }
        }

        /// <summary>
        /// Approximate H1 distance (difference in the H1 norm) between two DG fields; this also supports DG fields on different meshes, 
        /// it could be used for convergence studies.
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <param name="scheme">
        /// a cell quadrature scheme on the coarse of the two meshes
        /// </param>
        /// <returns></returns>
        static public double H1Distance(this ConventionalDGField A, ConventionalDGField B, CellQuadratureScheme scheme = null) {
            if (A.GridDat.SpatialDimension != B.GridDat.SpatialDimension)
                throw new ArgumentException("Both fields must have the same spatial dimension.");

            int D = A.GridDat.SpatialDimension;

            double Acc = 0.0;
            Acc += L2Distance(A, B, false, scheme).Pow2();
            for(int d = 0; d < D; d++) {
                ConventionalDGField dA_dd = new SinglePhaseField(A.Basis);
                dA_dd.Derivative(1.0, A, d, scheme != null ? scheme.Domain : null);

                ConventionalDGField dB_dd = new SinglePhaseField(B.Basis);
                dB_dd.Derivative(1.0, B, d, scheme != null ? scheme.Domain : null);

                Acc += L2Distance(dA_dd, dB_dd, false, scheme).Pow2();
            }

            return Acc.Sqrt();
        }
    }
}
