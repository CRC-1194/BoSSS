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

using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.IO;
using BoSSS.Solution.LevelSetTools;
using BoSSS.Solution.Statistic;
using BoSSS.Solution.Tecplot;
using BoSSS.Solution.Utils;
using BoSSS.Solution.XNSECommon.Operator.SurfaceTension;
using ilPSP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BoSSS.Solution.CompressibleFlowCommon.ShockFinding {
    public class InflectionPointFinder {
        private SinglePhaseField gradientX;
        private SinglePhaseField gradientY;
        private SinglePhaseField hessianXX;
        private SinglePhaseField hessianXY;
        private SinglePhaseField hessianYX;
        private SinglePhaseField hessianYY;

        private MultidimensionalArray resultGradX;
        private MultidimensionalArray resultGradY;
        private MultidimensionalArray resultHessXX;
        private MultidimensionalArray resultHessXY;
        private MultidimensionalArray resultHessYX;
        private MultidimensionalArray resultHessYY;

        private readonly GridData gridData;
        private readonly SinglePhaseField densityField;
        private readonly SinglePhaseField avField;
        private readonly SinglePhaseField levelSetField;

        private readonly string sessionPath;
        private readonly ISessionInfo session;

        private bool patchRecoveryGradient;
        private bool patchRecoveryHessian;
        private int firstPoint;
        private int lastPointPlusOne;

        //public int[] IterationsNeeded {
        //    get;
        //    private set;
        //}

        //public bool[] Converged {
        //    get;
        //    private set;
        //}

        //public int[] jCell {
        //    get;
        //    private set;
        //}

        /// <summary>
        /// Main result data structure
        /// <see cref="MultidimensionalArray"/> with three dimensions
        /// [0]: number of seedings points, [1]: maximum possible number of iterations to find the inflection point, [2]: data (*)
        /// Explanation for (*):
        /// [0]: x-coordinates, [1]: y-coordinates, [2]: field values, [3]: second derivative, [4]: step size
        /// </summary>
        public MultidimensionalArray Results {
            get;
            private set;
        }

        public MultidimensionalArray ResultsExtended {
            get;
            private set;
        }

        public InflectionPointFinder(string sessionPath, ISessionInfo session) {
            this.sessionPath = sessionPath;
            this.session = session;

            ITimestepInfo myTimestep = session.Timesteps.Last();
            this.gridData = (GridData)myTimestep.Fields.First().GridDat;
            this.densityField = (SinglePhaseField)myTimestep.Fields.Find("rho");
            this.avField = (SinglePhaseField)myTimestep.Fields.Find("artificialViscosity");
            this.levelSetField = (SinglePhaseField)myTimestep.Fields.Find("levelSet");
        }

        public MultidimensionalArray FindPoints(SeedingSetup seeding = SeedingSetup.av, bool patchRecoveryGradient = true, bool patchRecoveryHessian = true, int firstPoint = 0, int maxNumOfIterations = 100, double eps = 1e-12) {
            this.patchRecoveryGradient = patchRecoveryGradient;
            this.patchRecoveryHessian = patchRecoveryHessian;
            this.firstPoint = firstPoint;

            #region Create seedings points based on artificial viscosity
            MultidimensionalArray avValues = ShockFindingExtensions.GetAVMeanValues(gridData, avField);
            int numOfPoints;

            switch (seeding) {
                case SeedingSetup.av:
                    numOfPoints = avValues.Lengths[0];
                    Results = MultidimensionalArray.Create(numOfPoints, maxNumOfIterations + 1, 5);

                    // Seed points
                    for (int i = 0; i < numOfPoints; i++) {
                        int jLocal = (int)avValues[i, 0];
                        double[] cellCenter = gridData.Cells.GetCenter(jLocal);
                        Results[i, 0, 0] = cellCenter[0];
                        Results[i, 0, 1] = cellCenter[1];
                    }
                    break;

                case SeedingSetup.av3x3:
                    int numOfCells = avValues.Lengths[0];
                    int numOfPointsPerCell = 9;
                    numOfPoints = numOfCells * numOfPointsPerCell;
                    Results = MultidimensionalArray.Create(numOfPoints, maxNumOfIterations + 1, 5);

                    // Seed points
                    for (int i = 0; i < numOfCells; i++) {
                        int jLocal = (int)avValues[i, 0];
                        MultidimensionalArray grid = ShockFindingExtensions.Get3x3Grid(gridData, jLocal);
                        for (int j = 0; j < numOfPointsPerCell; j++) {
                            Results[i * numOfPointsPerCell + j, 0, 0] = grid[j, 0];
                            Results[i * numOfPointsPerCell + j, 0, 1] = grid[j, 1];
                        }
                    }
                    break;

                default:
                    throw new NotSupportedException("This setting does not exist.");
            }

            lastPointPlusOne = Results.Lengths[0];

            ResultsExtended = MultidimensionalArray.Create(numOfPoints, 3);

            //IterationsNeeded = new int[numOfPoints];
            //Converged = new bool[numOfPoints];
            //jCell = new int[numOfPoints];

            Console.WriteLine("Total number of seeding points: " + Results.Lengths[0]);
            #endregion

            #region Find inflection point for every seeding point
            Console.WriteLine("WALKING ON CURVES: START");
            Console.WriteLine("(Counting starts with 0)");
            for (int i = firstPoint; i < lastPointPlusOne; i++) {
                WalkOnCurve(gridData, densityField, Results.ExtractSubArrayShallow(i, -1, -1), out int iter, out bool pointFound, out int jLocal);

                // Save more stuff
                ResultsExtended[i, 0] = iter;
                ResultsExtended[i, 1] = pointFound == true ? 1 : 0;
                ResultsExtended[i, 2] = jLocal;

                if (i == firstPoint) {
                    Console.WriteLine("Point " + i + " (first)");
                } else if (i == lastPointPlusOne - 1) {
                    Console.WriteLine("Point " + i + " (last)");
                } else if (i % 100 == 0) {
                    Console.WriteLine("Point " + i);
                }

                if (!pointFound) {
                    Console.WriteLine(String.Format("Point {0}: not converged", i));
                }
            }
            Console.WriteLine("WALKING ON CURVES: END");
            #endregion

            return Results;
        }

        public void Plot(bool plotDGFields, bool plotSeedingsPoints, bool plotInflectionsPoints, bool plotCurves, bool plotStartEndPairs) {
            Console.WriteLine("PLOTTING: START");

            if (plotDGFields) {
                Tecplot.Tecplot plotDriver = new Tecplot.Tecplot(gridData, showJumps: true, ghostZone: false, superSampling: 2);
                plotDriver.PlotFields(sessionPath + "Fields", 0.0, new DGField[] { densityField, avField, levelSetField });
            }

            if (plotSeedingsPoints) {
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(sessionPath + "seedingPoints.txt")) {
                    string resultLine;
                    for (int j = firstPoint; j < lastPointPlusOne; j++) {
                        resultLine = Results[j, 0, 0] + "\t"
                                   + Results[j, 0, 1] + "\t"
                                   + Results[j, 0, 2];
                        sw.WriteLine(resultLine);
                    }
                    sw.Flush();
                }
            }

            if (plotInflectionsPoints) {
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(sessionPath + "inflectionPoints.txt")) {
                    string resultLine;
                    for (int j = firstPoint; j < lastPointPlusOne; j++) {
                        int pointFound = (int)ResultsExtended[j, 1];
                        resultLine = Results[j, (int)ResultsExtended[j, 0] - 1, 0] + "\t"
                                   + Results[j, (int)ResultsExtended[j, 0] - 1, 1] + "\t"
                                   + Results[j, (int)ResultsExtended[j, 0] - 1, 2] + "\t"
                                   + pointFound;
                        sw.WriteLine(resultLine);
                    }

                    sw.Flush();
                }
            }

            if (plotCurves) {
                for (int i = firstPoint; i < lastPointPlusOne; i++) {
                    using (System.IO.StreamWriter sw = new System.IO.StreamWriter(sessionPath + "curve_" + i + ".txt")) {
                        string resultLine;
                        for (int j = 0; j < (int)ResultsExtended[i, 0]; j++) {
                            resultLine = Results[i, j, 0] + "\t" + Results[i, j, 1] + "\t" + Results[i, j, 2];
                            sw.WriteLine(resultLine);
                        }
                        sw.Flush();
                    }
                }
            }

            if (plotStartEndPairs) {
                for (int j = firstPoint; j < lastPointPlusOne; j++) {
                    using (System.IO.StreamWriter sw = new System.IO.StreamWriter(sessionPath + "startEndPairs_" + j + ".txt")) {
                        string resultLine;
                        int pointFound = (int)ResultsExtended[j, 1];

                        // Starting point
                        resultLine = Results[j, 0, 0] + "\t"
                                   + Results[j, 0, 1] + "\t"
                                   + Results[j, 0, 2] + "\t"
                                   + pointFound;
                        sw.WriteLine(resultLine);

                        // End point
                        resultLine = Results[j, (int)ResultsExtended[j, 0] - 1, 0] + "\t"
                                   + Results[j, (int)ResultsExtended[j, 0] - 1, 1] + "\t"
                                   + Results[j, (int)ResultsExtended[j, 0] - 1, 2] + "\t"
                                   + pointFound;
                        sw.WriteLine(resultLine);

                        sw.Flush();
                    }
                }
            }

            Console.WriteLine("PLOTTING: END");
        }

        private double[] NormalizedGradientByFlux(SinglePhaseField field, int jCell, NodeSet nodeSet) {
            if (this.gradientX == null) {
                // Evaluate gradient
                gradientX = new SinglePhaseField(field.Basis, "gradientX");
                gradientY = new SinglePhaseField(field.Basis, "gradientY");
                gradientX.DerivativeByFlux(1.0, field, d: 0);
                gradientY.DerivativeByFlux(1.0, field, d: 1);

                if (this.patchRecoveryGradient) {
                    gradientX = ShockFindingExtensions.PatchRecovery(gradientX);
                    gradientY = ShockFindingExtensions.PatchRecovery(gradientY);
                }
            }

            if (this.resultGradX == null) {
                resultGradX = MultidimensionalArray.Create(1, 1);
                resultGradY = MultidimensionalArray.Create(1, 1);
            }

            gradientX.Evaluate(jCell, 1, nodeSet, resultGradX);
            gradientY.Evaluate(jCell, 1, nodeSet, resultGradY);

            double[] gradient = new double[] { resultGradX[0, 0], resultGradY[0, 0] };
            gradient.Normalize();
            return gradient;
        }

        private double SecondDerivativeByFlux(SinglePhaseField field, int jCell, NodeSet nodeSet) {
            if (this.gradientX == null) {
                // Evaluate gradient
                gradientX = new SinglePhaseField(field.Basis, "gradientX");
                gradientY = new SinglePhaseField(field.Basis, "gradientY");
                gradientX.DerivativeByFlux(1.0, field, d: 0);
                gradientY.DerivativeByFlux(1.0, field, d: 1);

                if (this.patchRecoveryGradient) {
                    gradientX = ShockFindingExtensions.PatchRecovery(gradientX);
                    gradientY = ShockFindingExtensions.PatchRecovery(gradientY);
                }
            }

            if (this.hessianXX == null) {
                // Evaluate Hessian matrix
                hessianXX = new SinglePhaseField(field.Basis, "hessianXX");
                hessianXY = new SinglePhaseField(field.Basis, "hessianXY");
                hessianYX = new SinglePhaseField(field.Basis, "hessianYX");
                hessianYY = new SinglePhaseField(field.Basis, "hessianYY");
                hessianXX.DerivativeByFlux(1.0, gradientX, d: 0);
                hessianXY.DerivativeByFlux(1.0, gradientX, d: 1);
                hessianYX.DerivativeByFlux(1.0, gradientY, d: 0);
                hessianYY.DerivativeByFlux(1.0, gradientY, d: 1);

                if (this.patchRecoveryHessian) {
                    hessianXX = ShockFindingExtensions.PatchRecovery(hessianXX);
                    hessianXY = ShockFindingExtensions.PatchRecovery(hessianXY);
                    hessianYX = ShockFindingExtensions.PatchRecovery(hessianYX);
                    hessianYY = ShockFindingExtensions.PatchRecovery(hessianYY);
                }
            }

            if (this.resultGradX == null) {
                resultGradX = MultidimensionalArray.Create(1, 1);
                resultGradY = MultidimensionalArray.Create(1, 1);
            }

            if (this.resultHessXX == null) {
                resultHessXX = MultidimensionalArray.Create(1, 1);
                resultHessXY = MultidimensionalArray.Create(1, 1);
                resultHessYX = MultidimensionalArray.Create(1, 1);
                resultHessYY = MultidimensionalArray.Create(1, 1);
            }

            gradientX.Evaluate(jCell, 1, nodeSet, resultGradX);
            gradientY.Evaluate(jCell, 1, nodeSet, resultGradY);

            hessianXX.Evaluate(jCell, 1, nodeSet, resultHessXX);
            hessianXY.Evaluate(jCell, 1, nodeSet, resultHessXY);
            hessianYX.Evaluate(jCell, 1, nodeSet, resultHessYX);
            hessianYY.Evaluate(jCell, 1, nodeSet, resultHessYY);

            // Compute second derivative along curve
            double g_alpha_alpha = 2 * ((resultHessXX[0, 0] * resultGradX[0, 0] + resultHessXY[0, 0] * resultGradY[0, 0]) * resultGradX[0, 0]
                + (resultHessYX[0, 0] * resultGradX[0, 0] + resultHessYY[0, 0] * resultGradY[0, 0]) * resultGradY[0, 0]);

            if (g_alpha_alpha == 0.0 || g_alpha_alpha.IsNaN()) {
                throw new NotSupportedException("Second derivative is zero");
            }

            return g_alpha_alpha;
        }

        /// <summary>
        /// Algorithm to find an inflection point along a curve in the direction of the gradient
        /// </summary>
        /// <param name="gridData">The corresponding grid</param>
        /// <param name="field">The DG field, which shall be evalauted</param>
        /// <param name="results">
        /// The points along the curve (first point has to be user defined)
        /// Lenghts --> [0]: maxIterations + 1, [1]: 5
        /// [1]: x | y | function values | second derivatives | step sizes
        /// </param>
        /// <param name="iterations">The amount of iterations needed in order to find the inflection point</param>
        /// <param name="converged">Has an inflection point been found?</param>
        /// <param name = "jLocal" >Local cell index of the inflection point (if found)</ param >
        /// < param name="byFlux">Calculation of the first and second order derivatives, default: true</param>
        private void WalkOnCurve(GridData gridData, SinglePhaseField field, MultidimensionalArray results, out int iterations, out bool converged, out int jLocal, bool byFlux = true) {
            // Init
            converged = false;

            // Current (global) point
            double[] currentPoint = new double[] { results[0, 0], results[0, 1] };

            // Compute global cell index of current point
            gridData.LocatePoint(currentPoint, out long GlobalId, out long GlobalIndex, out bool IsInside, out bool OnThisProcess);

            // Compute local node set
            NodeSet nodeSet = ShockFindingExtensions.GetLocalNodeSet(gridData, currentPoint, (int)GlobalIndex);

            // Get local cell index of current point
            int j0Grd = gridData.CellPartitioning.i0;
            jLocal = (int)(GlobalIndex - j0Grd);

            // Evaluate the second derivative
            try {
                if (byFlux) {
                    results[0, 3] = SecondDerivativeByFlux(field, jLocal, nodeSet);
                } else {
                    results[0, 3] = ShockFindingExtensions.SecondDerivative(field, jLocal, nodeSet);
                }
            } catch (NotSupportedException) {
                iterations = 0;
                return;
            }

            // Evaluate the function
            MultidimensionalArray f = MultidimensionalArray.Create(1, 1);
            field.Evaluate(jLocal, 1, nodeSet, f);
            results[0, 2] = f[0, 0];

            // Set initial step size to 0.5 * h_minGlobal
            // Set the search direction depending on the sign of the curvature
            results[0, 4] = 0.5 * gridData.Cells.h_minGlobal;

            int n = 1;
            while (n < results.Lengths[0]) {
                // Evaluate the gradient of the current point
                double[] gradient;
                if (byFlux) {
                    gradient = NormalizedGradientByFlux(field, jLocal, nodeSet);
                } else {
                    gradient = ShockFindingExtensions.NormalizedGradient(field, jLocal, nodeSet);
                }

                // Compute new point along curve
                currentPoint[0] = currentPoint[0] + gradient[0] * results[n - 1, 4];
                currentPoint[1] = currentPoint[1] + gradient[1] * results[n - 1, 4];

                // New point has been calculated --> old node set is invalid
                nodeSet = null;

                // Check if new point is still in the same cell or has moved to one of its neighbours
                if (!gridData.Cells.IsInCell(currentPoint, jLocal)) {
                    // Get indices of cell neighbours
                    gridData.GetCellNeighbours(jLocal, GetCellNeighbours_Mode.ViaVertices, out int[] cellNeighbours, out int[] connectingEntities);

                    double[] newLocalCoord = new double[currentPoint.Length];
                    bool found = false;
                    // Find neighbour
                    foreach (int neighbour in cellNeighbours) {
                        if (gridData.Cells.IsInCell(currentPoint, neighbour, newLocalCoord)) {
                            // If neighbour has been found, update
                            jLocal = neighbour + j0Grd;
                            nodeSet = ShockFindingExtensions.GetLocalNodeSet(gridData, currentPoint, jLocal);
                            found = true;
                            break;
                        }
                    }

                    if (found == false) {
                        iterations = n;
                        return;
                    }
                } else {
                    // New point is still in the same cell --> update only the coordiantes (local node set)
                    nodeSet = ShockFindingExtensions.GetLocalNodeSet(gridData, currentPoint, jLocal + j0Grd);
                }

                // Update output
                results[n, 0] = currentPoint[0];
                results[n, 1] = currentPoint[1];

                // Evaluate the function
                f.Clear();
                field.Evaluate(jLocal, 1, nodeSet, f);
                results[n, 2] = f[0, 0];

                // Evaluate the second derivative of the new point
                try {
                    if (byFlux) {
                        results[n, 3] = SecondDerivativeByFlux(field, jLocal, nodeSet);
                    } else {
                        results[n, 3] = ShockFindingExtensions.SecondDerivative(field, jLocal, nodeSet);
                    }
                } catch (NotSupportedException) {
                    break;
                }

                // Check if sign of second derivative has changed
                // Halve the step size and change direction
                if (Math.Sign(results[n, 3]) != Math.Sign(results[n - 1, 3])) {
                    results[n, 4] = -results[n - 1, 4] / 2;
                } else {
                    results[n, 4] = results[n - 1, 4];
                }

                n++;

                // Termination criterion
                // Remark: n has already been incremented!
                double[] diff = new double[currentPoint.Length];
                diff[0] = results[n - 1, 0] - results[n - 2, 0];
                diff[1] = results[n - 1, 1] - results[n - 2, 1];
                if (diff.L2Norm() < 1e-12) {
                    converged = true;
                    break;
                }
            }

            iterations = n;
            return;
        }
    }

    public enum SeedingSetup {
        av = 0,
        av3x3
    }
}