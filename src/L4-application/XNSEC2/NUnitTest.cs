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

using BoSSS.Application.XNSE_Solver;
using BoSSS.Application.XNSE_Solver.Tests;
using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.IO;
using BoSSS.Foundation.XDG;
using BoSSS.Platform.LinAlg;
using BoSSS.Solution;
using BoSSS.Solution.AdvancedSolvers.Testing;
using BoSSS.Solution.Control;
using BoSSS.Solution.LevelSetTools;
using BoSSS.Solution.LevelSetTools.SolverWithLevelSetUpdater;
using BoSSS.Solution.NSECommon;
using BoSSS.Solution.Utils;
using BoSSS.Solution.XdgTimestepping;
using BoSSS.Solution.XNSECommon;
using ilPSP;
using ilPSP.Utils;
using MPI.Wrappers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BoSSS.Application.XNSEC {

    /// <summary>
    /// An all-up NUnit test for the LowMachCombustion application.
    /// </summary>
    [TestFixture]
    static public partial class NUnitTest {
        //[OneTimeSetUp]
        //public static void SetUp() {
        //    BoSSS.Solution.Application.InitMPI();
        //}

        ///// <summary>
        ///// MPI shutdown.
        ///// </summary>
        //[OneTimeTearDown]
        //public static void TestFixtureTearDown() {
        //    csMPI.Raw.mpiFinalize();
        //}

        /// <summary>
        /// Tests the steady 2D-Channel flow using the 'Steady_SIMPLE' algorithm.***
        /// </summary>
        [Test]
        public static void IncompressibleSteadyPoiseuilleFlowTest() {
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.ChannelFlowTest_NUnit();
                p.Init(c);
                p.RunSolverMode();
                // p.CheckJacobian();
                double err_u = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityX];
                double err_v = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityY];
                double err_p = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.Pressure];
                double thres_u = 5.1e-6;
                double thres_v = 2.8e-6;
                double thres_p = 1.6e-5;

                Console.WriteLine("L2 Error of solution u: " + err_u + " (threshold is " + thres_u + ")");
                Console.WriteLine("L2 Error of solution v: " + err_v + " (threshold is " + thres_v + ")");
                Console.WriteLine("L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");

                Assert.Less(err_u, thres_u, "L2 Error of solution u: " + err_u + " (threshold is " + thres_u + ")");
                Assert.Less(err_v, thres_v, "L2 Error of solution v: " + err_v + " (threshold is " + thres_v + ")");
                Assert.Less(err_p, thres_p, "L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");
            }
        }

        /// <summary>
        /// Tests the steady 2D-Channel flow using the 'Steady_SIMPLE' algorithm.***
        /// </summary>
        //[Test]
        public static void TwoPhaseIncompressibleSteadyPoiseuilleFlowTest() {
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.TwoPhaseChannelFlowTest_NUnit();
                p.Init(c);
                p.RunSolverMode();
                //// p.CheckJacobian();
                //double err_u = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityX ];
                //double err_v = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityY ];
                //double err_p = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.Pressure ];
                //double thres_u = 5.1e-6;
                //double thres_v = 2.8e-6;
                //double thres_p = 1.6e-5;

                //Console.WriteLine("L2 Error of solution u: " + err_u + " (threshold is " + thres_u + ")");
                //Console.WriteLine("L2 Error of solution v: " + err_v + " (threshold is " + thres_v + ")");
                //Console.WriteLine("L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");

                //Assert.Less(err_u, thres_u, "L2 Error of solution u: " + err_u + " (threshold is " + thres_u + ")");
                //Assert.Less(err_v, thres_v, "L2 Error of solution v: " + err_v + " (threshold is " + thres_v + ")");
                //Assert.Less(err_p, thres_p, "L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");
            }
        }

        /// <summary>
        /// Tests the steady combustion solver with a manufactured solution.
        /// Current manufactured solutions used is T = cos(x*y), Y0 = 0.3 cos(x*y), Y1 = 0.6 cos(x*y), Y2 = 0.1 cos(x*y), u = -cos(x), v = -cos(y), p = sin(x*y).
        /// </summary>
        [Test]
        public static void ManufacturedSolutionLowMachCombustionTest() {
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.NUnitTestManuSol_3();
                p.Init(c);
                p.RunSolverMode();

                double err_u = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityX];
                double err_v = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityY];
                double err_p = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.Pressure];
                double err_T = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.Temperature];
                double err_Y0 = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.MassFraction0];
                double err_Y1 = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.MassFraction1];
                double err_Y2 = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.MassFraction2];

                double factor = 1.2;
                double thres_u = 0.002 * factor;
                double thres_v = 0.002 * factor;
                double thres_p = 0.08 * factor;
                double thres_T = 0.002 * factor;
                double thres_Y0 = 0.0004 * factor;
                double thres_Y1 = 0.0004 * factor;
                double thres_Y2 = 0.0005 * factor;

                Console.WriteLine("L2 Error of solution v: " + err_u + " (threshold is " + thres_u + ")");
                Console.WriteLine("L2 Error of solution v: " + err_v + " (threshold is " + thres_v + ")");
                Console.WriteLine("L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");
                Console.WriteLine("L2 Error of solution T: " + err_T + " (threshold is " + thres_T + ")");
                Console.WriteLine("L2 Error of solution Y0: " + err_Y0 + " (threshold is " + thres_Y0 + ")");
                Console.WriteLine("L2 Error of solution Y1: " + err_Y1 + " (threshold is " + thres_Y1 + ")");
                Console.WriteLine("L2 Error of solution Y2: " + err_Y2 + " (threshold is " + thres_Y2 + ")");

                Assert.Less(err_u, thres_u, "L2 Error of solution u: " + err_u + " (threshold is " + thres_u + ")");
                Assert.Less(err_v, thres_v, "L2 Error of solution v: " + err_v + " (threshold is " + thres_v + ")");
                Assert.Less(err_p, thres_p, "L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");
                Assert.Less(err_T, thres_T, "L2 Error of solution T: " + err_T + " (threshold is " + thres_T + ")");
                Assert.Less(err_Y0, thres_Y0, "L2 Error of solution Y0: " + err_Y0 + " (threshold is " + thres_Y0 + ")");
                Assert.Less(err_Y1, thres_Y1, "L2 Error of solution Y1: " + err_Y1 + " (threshold is " + thres_Y1 + ")");
                Assert.Less(err_Y2, thres_Y2, "L2 Error of solution Y2: " + err_Y2 + " (threshold is " + thres_Y2 + ")");

                //Console.WriteLine("Number fix point iterations: " + p.NumIterations + ". Expected number of iterations is less than 17.");
                //Assert.Less(p.NumIterations, 20 * factor);
            }
        }

        /// <summary>
        /// Tests the Taylor vortex flow
        /// </summary>
        [Test]
        public static void IncompressibleUnsteadyTaylorVortexTest() {
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.NUnitUnsteadyTaylorVortex();
                p.Init(c);
                p.RunSolverMode();
                //   p.CheckJacobian();
                double err_u = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityX];
                double err_v = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityY];
                double err_p = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.Pressure];
                double thres_vel = 0.02;
                double thres_p = 0.55;

                Console.WriteLine("L2 Error of solution u: " + err_u + " (threshold is " + thres_vel + ")");
                Console.WriteLine("L2 Error of solution v: " + err_v + " (threshold is " + thres_vel + ")");
                Console.WriteLine("L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");

                Assert.Less(err_u, thres_vel, "L2 Error of solution u: " + err_u + " (threshold is " + thres_vel + ")");
                Assert.Less(err_v, thres_vel, "L2 Error of solution v: " + err_v + " (threshold is " + thres_vel + ")");
                Assert.Less(err_p, thres_p, "L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");
            }
        }

        /// <summary>
        /// Tests the steady low-Mach solver for Couette flow with temperature gradient.
        /// Dg grad = 3 and nCells = 16x16 ***
        /// </summary>
        [Test]
        public static void LowMachSteadyCouetteWithTemperatureGradientTest() {
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.NUnitSteadyCouetteFlowWithTemperatureGradient();
                p.Init(c);
                p.RunSolverMode();
                //p.CheckJacobian();
                double err_u = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityX];
                double err_v = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.VelocityY];
                double err_p = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.Pressure];
                double err_T = (double)p.QueryHandler.QueryResults["Err_" + VariableNames.Temperature];
                double thres_u = 9e-6;
                double thres_v = 6e-5;
                double thres_p = 0.09;
                double thres_T = 6e-6;

                Console.WriteLine("L2 Error of solution u: " + err_u + " (threshold is " + thres_u + ")");
                Console.WriteLine("L2 Error of solution v: " + err_v + " (threshold is " + thres_v + ")");
                Console.WriteLine("L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");
                Console.WriteLine("L2 Error of solution T: " + err_T + " (threshold is " + thres_T + ")");

                Assert.Less(err_u, thres_u, "L2 Error of solution u: " + err_u + " (threshold is " + thres_u + ")");
                Assert.Less(err_v, thres_v, "L2 Error of solution v: " + err_v + " (threshold is " + thres_v + ")");
                Assert.Less(err_p, thres_p, "L2 Error of solution p: " + err_p + " (threshold is " + thres_p + ")");
                Assert.Less(err_T, thres_T, "L2 Error of solution T: " + err_T + " (threshold is " + thres_T + ")");
            }
        }

        /// <summary>
        /// ***
        /// </summary>
        [Test]
        public static void CavityNaturalConvection() {
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.NaturalConvectionSquareCavityTest();
                p.Init(c);
                p.RunSolverMode();
                var p0_solutions = new[] {
                    (1e2, 0.9573),
                    (1e3, 0.9381),
                    (1e4, 0.9146),
                    (1e5, 0.9220),
                    (1e6, 0.9245),
                    (1e7, 0.9226)
                };

                double Ra = c.Rayleigh;
                double p0Reference = -1;
                foreach(var sol in p0_solutions) {
                    if(Math.Abs(sol.Item1 - Ra) < 1e-3) {
                        p0Reference = sol.Item2;
                    }
                }
                if(p0Reference == -1)
                    throw new NotImplementedException();
                var thermoPressure = p.Parameters.Where(f => f.Identification == VariableNames.ThermodynamicPressure).FirstOrDefault();
                double ThermPressureCalculated = thermoPressure.GetMeanValueTotal(null);

                Console.WriteLine("The calculated thermodynamic pressure is  " + ThermPressureCalculated + " (and the reference value is " + p0Reference + ")");

                if(Math.Abs(ThermPressureCalculated - p0Reference) > 1e-2)
                    throw new Exception("Error on calculation of the thermodynamic pressure. End value is not the correct one");

                Console.WriteLine("The test passed! ");
            }
        }

        /// <summary>
        /// ***
        /// </summary>
        [Test]
        public static void CavityNaturalConvectionTest_Homotopy() {
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.NaturalConvectionSquareCavityTest_Homotopy();
                p.Init(c);
                p.RunSolverMode();
                var p0_solutions = new[] {
                    (1e2, 0.9573),
                    (1e3, 0.9381),
                    (1e4, 0.9146),
                    (1e5, 0.9220),
                    (1e6, 0.9245),
                    (1e7, 0.9226)
                };

                double Ra = c.Rayleigh;
                double p0Reference = -1;
                foreach(var sol in p0_solutions) {
                    if(Math.Abs(sol.Item1 - Ra) < 1e-3) {
                        p0Reference = sol.Item2;
                    }
                }
                if(p0Reference == -1)
                    throw new NotImplementedException();
                var thermoPressure = p.Parameters.Where(f => f.Identification == VariableNames.ThermodynamicPressure).FirstOrDefault();
                double ThermPressureCalculated = thermoPressure.GetMeanValueTotal(null);

                Console.WriteLine("The calculated thermodynamic pressure is  " + ThermPressureCalculated + " (and the reference value is " + p0Reference + ")");

                if(Math.Abs(ThermPressureCalculated - p0Reference) > 1e-2)
                    throw new Exception("Error on calculation of the thermodynamic pressure. End value is not the correct one");

                Console.WriteLine("The test passed! ");
            }
        }

        /// <summary>
        /// Testcase where Adaptive Mesh Refinement is used after each newton iteration
        /// </summary>
        //[Test]
        public static void CavityNaturalConvection_AMR_eachNewtonIteration() {
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.CavityNaturalConvection_AMR_eachNewtonIteration();
                p.Init(c);
                p.RunSolverMode();
                var p0_solutions = new[] {
                    (1e2, 0.9573),
                    (1e3, 0.9381),
                    (1e4, 0.9146),
                    (1e5, 0.9220),
                    (1e6, 0.9245),
                    (1e7, 0.9226)
                };

                double Ra = c.Rayleigh;
                double p0Reference = -1;
                foreach(var sol in p0_solutions) { // search for the corresponding reference p0
                    if(Math.Abs(sol.Item1 - Ra) < 1e-3) {
                        p0Reference = sol.Item2;
                    }
                }
                if(p0Reference == -1)
                    throw new NotImplementedException();
                var thermoPressure = p.Parameters.Where(f => f.Identification == VariableNames.ThermodynamicPressure).FirstOrDefault();
                double ThermPressureCalculated = thermoPressure.GetMeanValueTotal(null);

                Console.WriteLine("The calculated thermodynamic pressure is  " + ThermPressureCalculated + " (and the reference value is " + p0Reference + ")");

                if(Math.Abs(ThermPressureCalculated - p0Reference) > 1e-2)
                    throw new Exception("Error on calculation of the thermodynamic pressure. End value is not the correct one");

                Console.WriteLine("The test passed! ");
            }
        }

        /// <summary>
        /// TODO
        /// </summary>
        // [Test]
        public static void ThermodynamicPressureTest() {
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.ThermodynamicPressureTest();
                p.Init(c);
                p.RunSolverMode();
                var Temperature = (XDGField)p.CurrentState.Fields[3];

                double ThermPressureCalculated = p.EoS_A.GetMassDeterminedThermodynamicPressure(1.0, Temperature);
                double ThermPressureReference = 1.15742214311;
                Console.WriteLine("The calculated thermodynamic pressure is  " + ThermPressureCalculated + " (and the reference value is " + ThermPressureReference + ")");

                if(Math.Abs(ThermPressureCalculated - ThermPressureReference) > 1e-2)
                    throw new Exception("Error on calculation of the thermodynamic pressure. End value is not the correct one");

                Console.WriteLine("The test passed! ");
            }
        }

        //#if !DEBUG
        /// <summary>
        /// operator condition number scaling, 2D, for p=1 (polynomial order parameter is unwrapped for better parallelism of test execution)
        /// </summary>
        [Test]
        public static void TestOperatorScaling2D_p1() {
            TestOperatorScaling2D(1);
        }

        /// <summary>
        /// operator condition number scaling, 3D, for p=2 (polynomial order parameter is unwrapped for better parallelism of test execution)
        /// </summary>
        [Test]
        public static void TestOperatorScaling2D_p2() {
            TestOperatorScaling2D(2);
        }

        /// <summary>
        /// operator condition number scaling, 3D, for p=3 (polynomial order parameter is unwrapped for better parallelism of test execution)
        /// </summary>
        //[Test]
        public static void TestOperatorScaling2D_p3() {
            TestOperatorScaling2D(3);
        }

       

        /// <summary>
        /// operator condition number scaling, 2D
        /// </summary>
        public static void TestOperatorScaling2D(int dgDeg) {
            var Controls = new List<XNSEC_Control>();
            {
                int[] ResS = null;

                switch(dgDeg) {
                    //case 1: ResS = new int[] { 4, 5, 6, 7 }; break;
                    case 1: ResS = new int[] { 3, 4, 5, 6 }; break;
                    case 2: ResS = new int[] { 3, 4, 5 }; break; // more than 6 leads to out of memory in matlab
                    case 3: ResS = new int[] { 4, 5, 6, 7 }; break;
                    case 4: ResS = new int[] { 4, 5, 6 }; break;
                    default: throw new NotImplementedException();
                }

                foreach(int res in ResS) {
                    var C = BoSSS.Application.XNSEC.FullNSEControlExamples.ControlManuSolLowMachCombustion(dgDeg, res);
                    //C.TracingNamespaces = "*";
                    C.savetodb = false;
                    Controls.Add(C);
                }
            }

            ConditionNumberScalingTest.Perform(Controls);
        }


        private static void XNSECSolverTest(IXNSECTest Tst, XNSEC_Control C) {
            using(var solver = new XNSEC()) {
                Console.WriteLine("Warning! - enabled immediate plotting");
                C.ImmediatePlotPeriod = 1;
                C.SuperSampling = 3;

                solver.Init(C);
                solver.RunSolverMode();

                //-------------------Evaluate Error ----------------------------------------

                var evaluator = new XNSEErrorEvaluator<XNSEC_Control>(solver);
                double[] XNSE_Errors = evaluator.ComputeL2Error(Tst.steady ? 0.0 : Tst.dt, C);
                var combustionEvaluator = new CombustionErrorEvaluator<XNSEC_Control>(solver);
                double[] CombustionErrors = combustionEvaluator.ComputeL2Error(Tst.steady ? 0.0 : Tst.dt, C);

                List<double> errors = new List<double>();
                errors.AddRange(XNSE_Errors);
                errors.AddRange(CombustionErrors);
                double[] AllErrors = errors.ToArray();

                double[] ErrThresh = Tst.AcceptableL2Error;
                if(AllErrors.Length != ErrThresh.Length)
                    throw new ApplicationException();
                for(int i = 0; i < ErrThresh.Length; i++) {
                    bool ok = AllErrors[i] <= ErrThresh[i];
                    Console.Write("L2 error, '{0}': \t{1}", solver.Operator.DomainVar[i], AllErrors[i]);

                    if(ok)
                        Console.WriteLine("   (ok)");
                    else
                        Console.WriteLine("   Above Threshold (" + ErrThresh[i] + ")");
                }

                double[] ResThresh = Tst.AcceptableResidual;
                double[] ResNorms = new double[ResThresh.Length];
                if(solver.CurrentResidual.Fields.Count != ResThresh.Length)
                    throw new ApplicationException();
                for(int i = 0; i < ResNorms.Length; i++) {
                    ResNorms[i] = solver.CurrentResidual.Fields[i].L2Norm();
                    bool ok = ResNorms[i] <= ResThresh[i];
                    Console.Write("L2 norm, '{0}': \t{1}", solver.CurrentResidual.Fields[i].Identification, ResNorms[i]);

                    if(ok)
                        Console.WriteLine("   (ok)");
                    else
                        Console.WriteLine("   Above Threshold (" + ResThresh[i] + ")");
                }

                for(int i = 0; i < ErrThresh.Length; i++)
                    Assert.LessOrEqual(AllErrors[i], ErrThresh[i], $"Error {solver.CurrentState.Fields[i].Identification} above threshold.");

                for(int i = 0; i < ResNorms.Length; i++)
                    Assert.LessOrEqual(ResNorms[i], ResThresh[i], $"Residual {solver.CurrentResidual.Fields[i].Identification} above threshold.");
            }
        }

        private static XNSEC_Control TstObj2CtrlObj(IXNSECTest tst, int FlowSolverDegree, double AgglomerationTreshold, ViscosityMode vmode,
            XQuadFactoryHelper.MomentFittingVariants CutCellQuadratureType,
            SurfaceStressTensor_IsotropicMode SurfTensionMode,
            bool constantDensity,
            int GridResolution = 1, LinearSolverCode solvercode = LinearSolverCode.classic_pardiso) {
            XNSEC_Control C = new XNSEC_Control();
            int D = tst.SpatialDimension;
            int NoChemSpc = tst.NumberOfChemicalComponents;
            // database setup
            // ==============

            C.DbPath = null;
            C.savetodb = false;
            C.ProjectName = "XNSEC/" + tst.GetType().Name;
            C.ProjectDescription = "Test";
            C.EnableMassFractions = false;
            C.EnableTemperature = false;
            C.rhoOne = constantDensity;

            // DG degree
            // =========

            C.NumberOfChemicalSpecies = tst.NumberOfChemicalComponents;
            C.SetDGdegree(FlowSolverDegree);

            // grid
            // ====

            C.GridFunc = () => tst.CreateGrid(GridResolution);

            // boundary conditions
            // ===================

            foreach(var kv in tst.GetBoundaryConfig()) {
                C.BoundaryValues.Add(kv);
            }

            // Physical parameters
            // ====================
            C.NumberOfChemicalSpecies = tst.NumberOfChemicalComponents;
            C.PhysicalParameters.rho_A = tst.rho_A;
            C.PhysicalParameters.rho_B = tst.rho_B;
            C.PhysicalParameters.mu_A = tst.mu_A;
            C.PhysicalParameters.mu_B = tst.mu_B;
            C.PhysicalParameters.Sigma = tst.Sigma;
            C.PhysicalParameters.IncludeConvection = tst.IncludeConvection;

            // initial values and exact solution
            // =================================

            C.ExactSolutionVelocity = new Dictionary<string, Func<double[], double, double>[]>();
            C.ExactSolutionPressure = new Dictionary<string, Func<double[], double, double>>();
            C.ExactSolutionTemperature = new Dictionary<string, Func<double[], double, double>>();
            C.ExactSolutionMassFractions = new Dictionary<string, Func<double[], double, double>[]>();

            foreach(var spc in new[] { "A", "B" }) {
                C.ExactSolutionPressure.Add(spc, tst.GetPress(spc));
                C.ExactSolutionVelocity.Add(spc, D.ForLoop(d => tst.GetU(spc, d)));
                C.ExactSolutionMassFractions.Add(spc, NoChemSpc.ForLoop(q => tst.GetMassFractions(spc, q)));

                C.ExactSolutionTemperature.Add(spc, tst.GetTemperature(spc));
                for(int d = 0; d < D; d++) {
                    C.InitialValues_Evaluators.Add(VariableNames.Velocity_d(d) + "#" + spc, tst.GetU(spc, d).Convert_Xt2X(0.0));
                    var Gravity_d = tst.GetF(spc, d).Convert_X2Xt();
                    C.SetGravity(spc, d, Gravity_d);
                }

                C.InitialValues_Evaluators.Add(VariableNames.Pressure + "#" + spc, tst.GetPress(spc).Convert_Xt2X(0.0));
                C.InitialValues_Evaluators.Add(VariableNames.Temperature + "#" + spc, X => 1.0);
                C.InitialValues_Evaluators.Add(VariableNames.MassFraction0 + "#" + spc, X => 1.0);
            }
            if(tst.TestImmersedBoundary) {
                for(int d = 0; d < D; d++) {
                    C.InitialValues_Evaluators_TimeDep.Add(VariableNames.AsLevelSetVariable(VariableNames.LevelSetCGidx(1), VariableNames.Velocity_d(d)), tst.GetPhi2U(d));
                }
            }

            C.Phi = tst.GetPhi();
            C.InitialValues_Evaluators_TimeDep.Add(VariableNames.LevelSetCG, tst.GetPhi());

            // advanced spatial discretization settings
            // ========================================

            C.AdvancedDiscretizationOptions.ViscosityMode = vmode;
            C.AgglomerationThreshold = AgglomerationTreshold;
            if(D == 3 && SurfTensionMode != SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_ContactLine) {
                Console.WriteLine($"Reminder: {SurfTensionMode} changed to LaplaceBeltrami_ContactLine for 3D test.");
                C.AdvancedDiscretizationOptions.SST_isotropicMode = SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_ContactLine;
            } else {
                C.AdvancedDiscretizationOptions.SST_isotropicMode = SurfTensionMode;
            }
            C.CutCellQuadratureType = CutCellQuadratureType;

            // immersed boundary
            // =================

            C.UseImmersedBoundary = tst.TestImmersedBoundary;
            if(C.UseImmersedBoundary) {
                C.InitialValues_Evaluators_TimeDep.Add(VariableNames.LevelSetCGidx(1), tst.GetPhi2());
            }

            // timestepping and solver
            // =======================

            if(tst.steady) {
                C.TimesteppingMode = AppControl._TimesteppingMode.Steady;

                C.Option_LevelSetEvolution = LevelSetEvolution.None;
                C.Timestepper_LevelSetHandling = LevelSetHandling.None;
            } else {
                C.TimesteppingMode = AppControl._TimesteppingMode.Transient;

                C.Option_LevelSetEvolution = LevelSetEvolution.Prescribed;
                C.Timestepper_LevelSetHandling = LevelSetHandling.LieSplitting;

                C.NoOfTimesteps = 1;
                C.dtFixed = tst.dt;
            }
            C.NonLinearSolver.SolverCode = NonLinearSolverCode.Newton;
            C.NonLinearSolver.verbose = true;
            C.NonLinearSolver.ConvergenceCriterion = 1e-9;
            //C.LinearSolver.ConvergenceCriterion = 1e-9;
            //C.NonLinearSolver.MaxSolverIterations = 3;
            //C.Solver_ConvergenceCriterion = 1e-9;

            C.LinearSolver.SolverCode = solvercode;
            C.GravityDirection = tst.GravityDirection;
            C.ChemicalReactionActive = tst.ChemicalReactionTermsActive;

            // return
            // ======
            Assert.AreEqual(C.UseImmersedBoundary, tst.TestImmersedBoundary);
            return C;
        }

        
        public static void COMBUSTION_TEST() {
            string basepath = System.Environment.GetEnvironmentVariable("USERPROFILE");
            if(basepath.IsEmptyOrWhite())
                basepath = System.Environment.GetEnvironmentVariable("HOME");
            string path = Path.Combine(basepath, "default_bosss_db_comb23");

            bool alreadyExists = Directory.Exists(path);
            var db = DatabaseInfo.CreateOrOpen(path);

            int rank;
            var comm = csMPI.Raw._COMM.WORLD;
            csMPI.Raw.Comm_Rank(comm, out rank);

            if(rank == 0 && alreadyExists) {
                db.Controller.ClearDatabase();
            }


            using(var p = new XNSEC_MixtureFraction()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.FS_CounterDiffusionFlame(2, 6, 1.0, db.Path);

                p.Init(c);
                p.RunSolverMode();
            }

            Console.WriteLine("Flame sheet calculation done.");
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.Full_CounterDiffusionFlame(2, 6, 1.0, db.Path);
                p.Init(c);
                p.RunSolverMode();

                var temperatureXdg = (XDGField)(p.CurrentStateVector.Fields.Where(f => f.Identification == VariableNames.Temperature).SingleOrDefault());
                var temp = temperatureXdg.ProjectToSinglePhaseField(4);
                double minT; double maxT;
                temp.GetExtremalValues(out minT, out maxT);
                Console.WriteLine("Maximum reached temperature is {0}K", maxT);
            }
            Console.WriteLine("Full calculation done.");
        }

        /// <summary>
        /// This test checks if the solution obtained with an homotopy strategy for increasing
        /// the velocity inlets (by introducing a multiplier as the homotopy-variable)
        /// is the same as the one if one wouldnt use homotopy
        /// Obviously a moderate velocity multiplier has to be used to obtain a solution without homotopy.
        /// </summary>
        //[Test]
        public static void CounterDiffFlameHomotopy_TEST() {
            string basepath = System.Environment.GetEnvironmentVariable("USERPROFILE");
            if(basepath.IsEmptyOrWhite())
                basepath = System.Environment.GetEnvironmentVariable("HOME");
            string path = Path.Combine(basepath, @"default_bosss_db_comb23");

            bool alreadyExists = Directory.Exists(path);
            var db = DatabaseInfo.CreateOrOpen(path);

            int rank;
            var comm = csMPI.Raw._COMM.WORLD;
            csMPI.Raw.Comm_Rank(comm, out rank);

            if(rank == 0 && alreadyExists) {
                db.Controller.ClearDatabase();
            }

            // First, do a calculation without homotopy
            double desiredVelMultiplier = 4.0;
            XDGField VelocityX_NoHomotopy;
            XDGField VelocityX_WithHomotopy;

            List<double> L2NormsWithoutHomotopy;
            List<double> L2NormsWithHomotopy;
            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.FS_CounterDiffusionFlame(2, 6, desiredVelMultiplier, db.Path);
                c.activeAMRlevelIndicators.Clear(); // no mesh refinement
                c.AdaptiveMeshRefinement = false;
                c.NoOfTimesteps = 1;
                p.Init(c);
                p.RunSolverMode();
            }

            // Now do same simulation, using homotopy

            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.FS_CounterDiffusionFlame(2, 6, desiredVelMultiplier, db.Path); //
                c.activeAMRlevelIndicators.Clear(); // no mesh refinement
                c.AdaptiveMeshRefinement = false;
                c.NoOfTimesteps = 1;

                c.HomotopyApproach = XNSEC_Control.HomotopyType.Automatic;
                c.HomotopyVariable = XNSEC_Control.HomotopyVariableEnum.VelocityInletMultiplier;
                c.homotopieAimedValue = desiredVelMultiplier;

                p.Init(c);
                p.RunSolverMode();
            }

            var fieldsNoHomotopy = db.Sessions[1].Timesteps.Last().Fields;
            var fieldsWithHomotopy = db.Sessions[0].Timesteps.Last().Fields;

            foreach(var field in fieldsNoHomotopy) {
                var fieldWithHomotopy = fieldsWithHomotopy.Where(f => f.Identification == field.Identification).FirstOrDefault();
                //Console.WriteLine(field.Identification);
                //Console.WriteLine(field.L2Norm() - fieldWithHomotopy.L2Norm());
                Assert.LessOrEqual(field.L2Norm() - fieldWithHomotopy.L2Norm(), 1e-4, "Comparison of Norms for variable " + field.Identification + " show that they are different when using homotopy");
            }
        }

        public static void COMBUSTION_CoFlowFlame_TEST() {
            string basepath = null;
            basepath = System.Environment.GetEnvironmentVariable("USERPROFILE");
            if(basepath.IsEmptyOrWhite())
                basepath = System.Environment.GetEnvironmentVariable("HOME");
            string path = Path.Combine(basepath, "default_bosss_db_CoFlowcombustion");

            bool alreadyExists = Directory.Exists(path);
            var db = DatabaseInfo.CreateOrOpen(path);

            int rank;
            var comm = csMPI.Raw._COMM.WORLD;
            csMPI.Raw.Comm_Rank(comm, out rank);

            if(rank == 0 && alreadyExists) {
                db.Controller.ClearDatabase();
            }

            //using(var p = new XNSEC_MixtureFraction()) {
            //    var c = BoSSS.Application.XNSEC.FullNSEControlExamples.FS_CoFlowDiffusionFlame(2, 7, 0.5, db.Path);
            //    p.Init(c);
            //    p.RunSolverMode();
            //}

            //Console.WriteLine("Flame sheet calculation done.");

            using(var p = new XNSEC()) {
                var c = BoSSS.Application.XNSEC.FullNSEControlExamples.Full_CoFlowDiffusionFlame(2, 7, 1, 0.5, db.Path);
                p.Init(c);
                p.RunSolverMode();
            }

            Console.WriteLine("Full calculation done.");
        }
    }
}