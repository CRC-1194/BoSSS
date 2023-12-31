﻿/* =======================================================================
Copyright 2017 Technische Universitaet Darmstadt, Fachgebiet fuer Stroemungsdynamik (chair of fluid dynamics)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections.Generic;
using BoSSS.Foundation;
using BoSSS.Foundation.XDG.Quadrature;
using BoSSS.Platform;
using BoSSS.Foundation.XDG.Quadrature.HMF;
using BoSSS.Foundation.Grid;
using ilPSP;
using System.Diagnostics;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Platform.LinAlg;
using BoSSS.Foundation.XDG;

namespace CutCellQuadrature {

    public class AnalyticEllipseLevelSet : IAnalyticLevelSet {

        private double xMajor = 3.0;

        private double yMajor = 1.5;

        private GridData gridData;

        private double offsetX;

        private double offsetY;

        public Object Clone() {
            return new AnalyticEllipseLevelSet(this.gridData) {
                offsetX = this.offsetX,
                offsetY = this.offsetY,
                xMajor = this.xMajor,
                yMajor = this.yMajor
            };
        }

        public AnalyticEllipseLevelSet(GridData gridData) {
            this.gridData = gridData;
        }

        public void SetParameters(double xMajor, double yMajor, double offsetX, double offsetY) {
            this.offsetX = offsetX;
            this.offsetY = offsetY;
            this.xMajor = xMajor;
            this.yMajor = yMajor;
        }

        private MultidimensionalArray GetGlobalNodes(int j0, int Len, NodeSet nodeSet) {
            int noOfNodes = nodeSet.GetLength(0);
            int D = nodeSet.GetLength(1);

            MultidimensionalArray nodes = MultidimensionalArray.Create(Len, noOfNodes, D);
            gridData.TransformLocal2Global(nodeSet, j0, Len, nodes, 0);

            for (int i = 0; i < Len; i++) {
                for (int j = 0; j < noOfNodes; j++) {
                    nodes[i, j, 0] -= offsetX;
                    nodes[i, j, 1] -= offsetY;
                }
            }

            return nodes;
        }

        #region ILevelSet Members

        public void Evaluate(int j0, int Len, NodeSet Ns, MultidimensionalArray result) {
            MultidimensionalArray nodes = GetGlobalNodes(j0, Len, Ns);
            int noOfNodes = nodes.GetLength(1);

            double aSquare = xMajor * xMajor / 4.0;
            double bSquare = yMajor * yMajor / 4.0;

            for (int i = 0; i < Len; i++) {
                for (int j = 0; j < noOfNodes; j++) {
                    double x = nodes[i, j, 0];
                    double y = nodes[i, j, 1];

                    result[i, j] = 1.0 - Math.Sqrt(x * x / aSquare + y * y / bSquare);
                }
            }
        }

        public void EvaluateGradient(int j0, int Len, NodeSet Ns, MultidimensionalArray result) {
            MultidimensionalArray nodes = GetGlobalNodes(j0, Len, Ns);
            int noOfNodes = nodes.GetLength(1);

            double aSquare = xMajor * xMajor / 4.0;
            double bSquare = yMajor * yMajor / 4.0;

            for (int i = 0; i < Len; i++) {
                for (int j = 0; j < noOfNodes; j++) {
                    double x = nodes[i, j, 0];
                    double y = nodes[i, j, 1];

                    double c = Math.Sqrt(x * x / aSquare + y * y / bSquare);

                    // Gradient not defined, just perturb it
                    if (c == 0) {
                        x += 1e-15;
                        y += 1e-15;
                        c = Math.Sqrt(x * x / aSquare + y * y / bSquare);
                    }

                    result[i, j, 0] = -x / aSquare / c;
                    result[i, j, 1] = -y / bSquare / c;
                }
            }
        }

        public void EvaluateHessian(int j0, int Len, NodeSet Ns, MultidimensionalArray result) {
            MultidimensionalArray nodes = GetGlobalNodes(j0, Len, Ns);
            int noOfNodes = nodes.GetLength(1);

            double aSquare = xMajor * xMajor / 4.0;
            double bSquare = yMajor * yMajor / 4.0;

            for (int i = 0; i < Len; i++) {
                for (int j = 0; j < noOfNodes; j++) {
                    double x = nodes[i, j, 0];
                    double y = nodes[i, j, 1];

                    double c = x * x / aSquare + y * y / bSquare;
                    result[i, j, 0, 0] = -y * y / (Math.Sqrt(c) * (aSquare * y * y + bSquare * x * x));
                    result[i, j, 1, 0] = x * y / aSquare / bSquare * Math.Pow(c, -1.5);
                    result[i, j, 0, 1] = result[i, j, 1, 0];
                    result[i, j, 1, 1] = -x * x / (Math.Sqrt(c) * (aSquare * y * y + bSquare * x * x));
                }
            }
        }

        /// <summary>
        /// curvature computation according to Bonnet's formula
        /// Copy-Paste from <see cref="LevelSet.EvaluatetotalCurvature"/>, since there is no analytic formula for the curvature of an ellipse
        /// </summary>
        public void EvaluateTotalCurvature(int j0, int Len, NodeSet NodeSet, MultidimensionalArray result) {
            // (siehe FK, persoenliche Notizen, 08mar13)


            // checks
            // ------

            int K = NodeSet.NoOfNodes;
            if (result.Dimension != 2)
                throw new ArgumentException();
            if (result.GetLength(0) != Len)
                throw new ArgumentException();
            if (result.GetLength(1) != K)
                throw new ArgumentException();
            int D = NodeSet.SpatialDimension;
            //Debug.Assert(D == this.GridDat.SpatialDimension);


            // buffers:
            // --------
            //MultidimensionalArray Phi = new MultidimensionalArray(2);
            MultidimensionalArray GradPhi = new MultidimensionalArray(3);
            MultidimensionalArray HessPhi = new MultidimensionalArray(4);

            MultidimensionalArray ooNormGrad = new MultidimensionalArray(2);
            MultidimensionalArray Laplace = new MultidimensionalArray(2);
            MultidimensionalArray Q = new MultidimensionalArray(3);

            //Phi.Allocate(Len, K);
            GradPhi.Allocate(Len, K, D);
            HessPhi.Allocate(Len, K, D, D);
            ooNormGrad.Allocate(Len, K);
            Laplace.Allocate(Len, K);
            Q.Allocate(Len, K, D);

            // derivatives
            // -----------

            // evaluate gradient
            this.EvaluateGradient(j0, Len, NodeSet, GradPhi);

            // evaluate Hessian
            this.EvaluateHessian(j0, Len, NodeSet, HessPhi);


            // compute the monstrous formula
            // -----------------------------

            // norm of Gradient:
            for (int d = 0; d < D; d++) {
                var GradPhi_d = GradPhi.ExtractSubArrayShallow(-1, -1, d);
                ooNormGrad.Multiply(1.0, GradPhi_d, GradPhi_d, 1.0, "ik", "ik", "ik");
            }
            ooNormGrad.ApplyAll(x => 1.0 / Math.Sqrt(x));

            // laplacian of phi:
            for (int d = 0; d < D; d++) {
                var HessPhi_d_d = HessPhi.ExtractSubArrayShallow(-1, -1, d, d);
                Laplace.Acc(1.0, HessPhi_d_d);
            }

            // result = Laplacian(phi)/|Grad phi|
            result.Multiply(1.0, Laplace, ooNormGrad, 0.0, "ik", "ik", "ik");


            // result += Grad(1/|Grad(phi)|)
            for (int d1 = 0; d1 < D; d1++) {
                var Qd = Q.ExtractSubArrayShallow(-1, -1, d1);

                for (int d2 = 0; d2 < D; d2++) {
                    var Grad_d2 = GradPhi.ExtractSubArrayShallow(-1, -1, d2);
                    var Hess_d2_d1 = HessPhi.ExtractSubArrayShallow(-1, -1, d2, d1);

                    Qd.Multiply(-1.0, Grad_d2, Hess_d2_d1, 1.0, "ik", "ik", "ik");
                }
            }

            ooNormGrad.ApplyAll(x => x * x * x);

            result.Multiply(1.0, GradPhi, Q, ooNormGrad, 1.0, "ik", "ikd", "ikd", "ik");

        }


        public IEnumerable<double> GetRoots(LineSegment lineSegment, int element) {
            int D = lineSegment.SpatialDimension;

            MultidimensionalArray localSegmentCoordinates = MultidimensionalArray.Create(2, D);
            localSegmentCoordinates[0, 0] = lineSegment.Start[0];
            localSegmentCoordinates[0, 1] = lineSegment.Start[1];
            localSegmentCoordinates[1, 0] = lineSegment.End[0];
            localSegmentCoordinates[1, 1] = lineSegment.End[1];

            MultidimensionalArray globalSegmentCoordinates = MultidimensionalArray.Create(1, 2, D);
            gridData.TransformLocal2Global(localSegmentCoordinates, element, 1, globalSegmentCoordinates, 0);

            Vector start = new Vector(D);
            Vector end = new Vector(D);
            start[0] = globalSegmentCoordinates[0, 0, 0];
            start[1] = globalSegmentCoordinates[0, 0, 1];
            end[0] = globalSegmentCoordinates[0, 1, 0];
            end[1] = globalSegmentCoordinates[0, 1, 1];
            LineSegment globalSegment = new LineSegment(D,gridData.Cells.RefElements[0], start, end);

            double xMajorSquare = xMajor * xMajor / 4.0;
            double yMajorSquare = yMajor * yMajor / 4.0;

            double p = 0.0;
            double q = 0.0;
            double divisor = 0.0;
            {
                // x
                double a = 0.5 * (globalSegment.End[0] - globalSegment.Start[0]);
                double b = 0.5 * (globalSegment.Start[0] + globalSegment.End[0]) - offsetX;

                p += 2.0 * a * b / xMajorSquare;
                q += b * b / xMajorSquare;
                divisor += a * a / xMajorSquare;

                // y
                a = 0.5 * (globalSegment.End[1] - globalSegment.Start[1]);
                b = 0.5 * (globalSegment.Start[1] + globalSegment.End[1]) - offsetY;

                p += 2.0 * a * b / yMajorSquare;
                q += b * b / yMajorSquare;
                divisor += a * a / yMajorSquare;
            }

            p /= divisor;
            q = (q - 1.0) / divisor;

            double radicand = 0.25 * p * p - q;
            if (radicand < 0.0) {
                yield break;
            } else if (radicand == 0.0) {
                double root = -0.5 * p;
                if (Math.Abs(root) < 1.0) {
                    yield return root;
                }
            } else {
                double root = -0.5 * p - Math.Sqrt(radicand);
                if (Math.Abs(root) < 1.0) {
                    yield return root;
                }

                root = -0.5 * p + Math.Sqrt(radicand);
                if (Math.Abs(root) < 1.0) {
                    yield return root;
                }
            }
        }

        public void CopyFrom(ILevelSet other) {
            throw new NotImplementedException();
        }

        #endregion
    }
}
