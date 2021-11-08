﻿using BoSSS.Foundation;
using BoSSS.Solution.Control;
using BoSSS.Solution.Gnuplot;
using ilPSP;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BoSSS.Solution.Statistic {

    /// <summary>
    /// Utility functions to test for solver convergence
    /// </summary>
    static public class ConvergenceTest {

        /*
        public static void XNSESolverConvergenceTest(AppControl[] CS, bool useExactSolution, double[] ExpectedSlopes) {
            int D;
            int NoOfMeshes = CS.Length;

            double[] hS = new double[NoOfMeshes];
            MultidimensionalArray errorS = null;
            string[] Names = null;

            IApplication[] solvers = new IApplication[NoOfMeshes];
            if (useExactSolution) {

                if (NoOfMeshes < 2)
                    throw new ArgumentException("At least two meshes required for convergence against exact solution.");

                for (int k = 0; k < CS.Length; k++) {

                    var C = CS[k];
                    //using(var solver = new XNSE()) {
                    var solverClass = C.GetSolverType();
                    IApplication solver = (IApplication) Activator.CreateInstance(solverClass);
                    solvers[k] = solver;
                    {
                        //Console.WriteLine("Warning! - enabled immediate plotting");
                        //C.ImmediatePlotPeriod = 1;
                        //C.SuperSampling = 3;

                        solver.Init(C);
                        solver.RunSolverMode();

                        //-------------------Evaluate Error ---------------------------------------- 
                        var evaluator = new XNSEErrorEvaluator<XNSE_Control>(solver);
                        double[] LastErrors = evaluator.ComputeL2Error(Tst.steady ? 0.0 : Tst.dt, C);
                        double[] ErrThresh = Tst.AcceptableL2Error;

                        if (k == 0) {
                            errorS = MultidimensionalArray.Create(NoOfMeshes, LastErrors.Length);
                            Names = new string[LastErrors.Length];
                            if (ExpectedSlopes.Length != Names.Length)
                                throw new ArgumentOutOfRangeException();
                        } else {
                            if (LastErrors.Length != Names.Length)
                                throw new ApplicationException();
                        }

                        if (LastErrors.Length != ErrThresh.Length)
                            throw new ApplicationException();
                        for (int i = 0; i < ErrThresh.Length; i++) {
                            Console.WriteLine($"L2 error, '{solver.Operator.DomainVar[i]}': \t{LastErrors[i]}");
                            Names[i] = solver.Operator.DomainVar[i];
                        }

                        errorS.SetRow(k, LastErrors);
                        hS[k] = evaluator.GetGrid_h();
                    }

                }
            } else {
                if (NoOfMeshes < 3)
                    throw new ArgumentException("At least three meshes required for convergence if finest solution is assumed to be exact.");
                throw new NotImplementedException("todo");
            }

            for (int i = 0; i < errorS.GetLength(1); i++) {
                var slope = hS.LogLogRegression(errorS.GetColumn(i));

                Console.WriteLine($"Convergence slope for Error of '{Names[i]}': \t{slope}\t(Expecting: {ExpectedSlopes[i]})");
            }

            for (int i = 0; i < errorS.GetLength(1); i++) {
                var slope = hS.LogLogRegression(errorS.GetColumn(i));
                Assert.IsTrue(slope >= ExpectedSlopes[i], $"Convergence Slope of {Names[i]} is degenerate.");
            }

            foreach (var s in solvers) {
                s.Dispose();
            }
        }
        */

        public static void SolverConvergenceTest_Experimental(this IEnumerable<AppControl> __CS, string Title, params (string FieldName, double expectedSlope, NormType normType)[] fildNamesAndSlopes) {
            int D = -1;
            var CS = __CS.ToArray();
            int NoOfMeshes = CS.Length;

            if(CS.Length < 3)
                throw new ArgumentException("At least three meshes required for convergence if finest solution is assumed to be exact (experimental convergence).");



            // step 1: compute solutions on different resolutions
            // ===================================================

            IApplication[] solvers = new IApplication[NoOfMeshes];
            List<IEnumerable<DGField>> solutionOnDifferentResolutions = new List<IEnumerable<DGField>>();
            {

                if (NoOfMeshes < 2)
                    throw new ArgumentException("At least two meshes required for convergence against exact solution.");

                for (int k = 0; k < CS.Length; k++) {

                    var C = CS[k];
                    //using(var solver = new XNSE()) {
                    var solverClass = C.GetSolverType();
                    IApplication solver = (IApplication) Activator.CreateInstance(solverClass);
                    solvers[k] = solver;
                    {
                        //Console.WriteLine("Warning! - enabled immediate plotting");
                        //C.ImmediatePlotPeriod = 1;
                        //C.SuperSampling = 3;

                        solver.Init(C);
                        solver.RunSolverMode();

                        if(D < 0) {
                            D = solver.Grid.SpatialDimension;
                        } else {
                            if(D != solver.Grid.SpatialDimension)
                                throw new ArgumentException("unable to compare simulations in different spatial dimensions.");
                        }

                        ////-------------------Evaluate Error ---------------------------------------- 
                        //var evaluator = new XNSEErrorEvaluator<XNSE_Control>(solver);
                        //double[] LastErrors = evaluator.ComputeL2Error(Tst.steady ? 0.0 : Tst.dt, C);
                        //double[] ErrThresh = Tst.AcceptableL2Error;

                        //if (k == 0) {
                        //    errorS = MultidimensionalArray.Create(NoOfMeshes, LastErrors.Length);
                        //    Names = new string[LastErrors.Length];
                        //    if (ExpectedSlopes.Length != Names.Length)
                        //        throw new ArgumentOutOfRangeException();
                        //} else {
                        //    if (LastErrors.Length != Names.Length)
                        //        throw new ApplicationException();
                        //}

                        //if (LastErrors.Length != ErrThresh.Length)
                        //    throw new ApplicationException();
                        //for (int i = 0; i < ErrThresh.Length; i++) {
                        //    Console.WriteLine($"L2 error, '{solver.Operator.DomainVar[i]}': \t{LastErrors[i]}");
                        //    Names[i] = solver.Operator.DomainVar[i];
                        //}

                        //errorS.SetRow(k, LastErrors);
                        //hS[k] = evaluator.GetGrid_h();

                        var solutionAtResolutions = fildNamesAndSlopes.Select(
                            ttt => solver.IOFields.Where(f => f.Identification == ttt.FieldName).Single());

                        solutionOnDifferentResolutions.Add(solutionAtResolutions.ToArray());
                    }

                }
            }

            // step 2: compute errors in specified norms
            // ===================================================

            DGFieldComparison.ComputeErrors(
                solutionOnDifferentResolutions, out var hS, out var DOFs, out var errorS, NormType.L2_embedded);

            


            // step 3: check slopes
            // ===================================================
            foreach(var ttt in fildNamesAndSlopes) {
                string fieldName = ttt.FieldName;
                
                var slope = hS.LogLogRegression(errorS[fieldName]);

                Console.WriteLine($"Convergence slope for Error of '{fieldName}': \t{slope}\t(Expecting: {ttt.expectedSlope} in norm {ttt.normType})");
            }

            var plt = new BoSSS.Solution.Gnuplot.Plot2Ddata();
            plt.LogX = true;
            plt.LogY = true;
            plt.Title = Title;
            int cnt = 0;
            var allPoints = Enum.GetValues(typeof(PointTypes));
            var allColors = Enum.GetValues(typeof(LineColors));
            foreach(var ttt in fildNamesAndSlopes) {
                plt.AddDataGroup(ttt.FieldName + "-" + ttt.normType.ToString(), hS, errorS[ttt.FieldName]);
                plt.dataGroups.Last().Format.PointType = (PointTypes) allPoints.GetValue(cnt % allPoints.Length);
                plt.dataGroups.Last().Format.LineColor = (LineColors) allColors.GetValue(cnt % allColors.Length);
                cnt++;
            }

            string DateNtime = DateTime.Now.ToString("yyyyMMMdd_HHmmss");           
            plt.SaveToSVG($"Convergence-{DateNtime}.svg");



            foreach(var ttt in fildNamesAndSlopes) {
                string fieldName = ttt.FieldName;
                
                var slope = hS.LogLogRegression(errorS[fieldName]);

                Assert.GreaterOrEqual(slope, ttt.expectedSlope, $"Convergence Slope of {fieldName} is degenerate: got {slope}, expecting at lease {ttt.expectedSlope} in norm {ttt.normType}");
            }


            foreach (var s in solvers) {
                s.Dispose();
            }
        }

    }
}