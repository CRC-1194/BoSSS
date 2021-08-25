/* =======================================================================
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
using System.Linq;
using System.Text;
using BoSSS.Solution.NSECommon;
using ilPSP;
using BoSSS.Solution.Control;
using BoSSS.Foundation.Grid;
using System.Diagnostics;
using BoSSS.Solution.AdvancedSolvers;
using ilPSP.Utils;
using BoSSS.Platform.Utils.Geom;
using BoSSS.Solution.XNSECommon;
using BoSSS.Foundation.IO;
using BoSSS.Solution.LevelSetTools;
using BoSSS.Solution.XdgTimestepping;
using BoSSS.Solution.LevelSetTools.FourierLevelSet;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Solution.Timestepping;
using BoSSS.Solution.LevelSetTools.SolverWithLevelSetUpdater;
using BoSSS.Foundation.XDG;
using BoSSS.Application.XNSE_Solver.Loadbalancing;
using BoSSS.Application.XNSE_Solver.LoadBalancing;

namespace BoSSS.Application.XNSE_Solver {

    /// <summary>
    /// A few example configurations.
    /// </summary>
    public static class HardcodedControl {
       

        /// <summary>
        /// Maintainer: kummer
        /// </summary>
        public static XNSE_Control TransientDroplet(
            //string _DbPath = @"\\fdyprime\userspace\kummer\BoSSS-db-XNSE",
            string _DbPath = null,
            int degree = 2,
            double dt = 2e-4,
            double elipsDelta = 0.1,
            int NoOfTs = 100000) {


            XNSE_Control C = new XNSE_Control();


            // basic database options
            // ======================

            C.DbPath = _DbPath;
            C.savetodb = _DbPath != null;
            C.ProjectName = "XNSE/Droplet";
            C.ProjectDescription = "Multiphase Droplet";
            C.Tags.Add("oscillating");
            C.Tags.Add("fourier");

            // DG degrees
            // ==========

            C.SetFieldOptions(degree, 2);

            // grid and boundary conditions
            // ============================

            const double BaseSize = 1.0;
            const bool xPeriodic = false;
            const double VelXBase = 0.0;

            int xkelem = 54;
            int ykelem = 54;
            double xSize = -4.5 * BaseSize;
            double ySize = -4.5 * BaseSize;

            double hMin = Math.Min(2 * xSize / (xkelem), 2 * ySize / (ykelem));


            C.GridFunc = delegate {
                double[] Xnodes = GenericBlas.Linspace(-4.5 * BaseSize, 4.5 * BaseSize, 55);
                double[] Ynodes = GenericBlas.Linspace(-4.5 * BaseSize, 4.5 * BaseSize, 55);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: xPeriodic);
                grd.EdgeTagNames.Add(1, "wall_lower");
                grd.EdgeTagNames.Add(2, "wall_upper");
                if (!xPeriodic) {
                    grd.EdgeTagNames.Add(3, "wall_left");
                    grd.EdgeTagNames.Add(4, "wall_right");
                }

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] - (-4.5 * BaseSize)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - (+4.5 * BaseSize)) <= 1.0e-8)
                        et = 2;
                    if (!xPeriodic && Math.Abs(X[0] - (-4.5 * BaseSize)) <= 1.0e-8)
                        et = 3;
                    if (!xPeriodic && Math.Abs(X[0] - (+4.5 * BaseSize)) <= 1.0e-8)
                        et = 4;


                    Debug.Assert(et != 0);
                    return et;
                });

                return grd;
            };

            C.AddBoundaryValue("wall_lower", "VelocityX#A", (x, t) => VelXBase);
            C.AddBoundaryValue("wall_upper", "VelocityX#A", (x, t) => VelXBase);
            C.AddBoundaryValue("wall_lower", "VelocityX#B", (x, t) => VelXBase);
            C.AddBoundaryValue("wall_upper", "VelocityX#B", (x, t) => VelXBase);
            if (!xPeriodic) {
                C.AddBoundaryValue("wall_left", "VelocityX#A", (x, t) => VelXBase);
                C.AddBoundaryValue("wall_right", "VelocityX#A", (x, t) => VelXBase);
                C.AddBoundaryValue("wall_left", "VelocityX#B", (x, t) => VelXBase);
                C.AddBoundaryValue("wall_right", "VelocityX#B", (x, t) => VelXBase);

            }

            // Initial Values
            // ==============

            //var database = new DatabaseInfo(_DbPath);

            //var latestSession = database.Sessions.OrderByDescending(e => e.CreationTime)
            //    .First(sess => sess.ProjectName == "XNSE/Droplet" && (sess.Timesteps.Last().TimeStepNumber.MajorNumber - sess.Timesteps.First().TimeStepNumber.MajorNumber) > 50);

            //C.RestartInfo = new Tuple<Guid, Foundation.IO.TimestepNumber>(latestSession.ID, null);

            //ISessionInfo latestSession = database.Sessions.OrderByDescending(e => e.CreationTime)
            //    .FirstOrDefault(sess => sess.ProjectName == "XNSE/Droplet" && sess.Timesteps.Count > 50 && sess.Tags.Contains("highPenalty"));

            //if (latestSession == null) {
            //    C.RestartInfo = new Tuple<Guid, Foundation.IO.TimestepNumber>(new Guid("6c567939-b310-44a8-b45c-d94880e04cbf"), new TimestepNumber(500));
            //} else {
            //    C.RestartInfo = new Tuple<Guid, Foundation.IO.TimestepNumber>(latestSession.ID, null);
            //}





            double radius = 0.835;

            C.InitialValues_Evaluators.Add("Phi",
                (X => (X[0] / (radius * BaseSize * (1.0 + elipsDelta))).Pow2() + (X[1] / (radius * BaseSize * (1.0 - elipsDelta))).Pow2() - 1.0)   // quadratic form
                );
            C.InitialValues_Evaluators.Add("VelocityX", X => VelXBase);

            // Physical Parameters
            // ===================


            // Air-Water (lenght scale == centimeters, 3D space)
            C.PhysicalParameters.rho_A = 1e-3; //     kg / cm³
            C.PhysicalParameters.rho_B = 1.2e-6; //   kg / cm³
            C.PhysicalParameters.mu_A = 1e-5; //      kg / cm / sec
            C.PhysicalParameters.mu_B = 17.1e-8; //   kg / cm / sec
            C.PhysicalParameters.Sigma = 72.75e-3; // kg / sec²     
            //*/

            /*
            // Air-Water (lenght scale == centimeters, 2D space i.e. pressure = Force/Len, density = mass/Len/Len, etc.)
            // Dimensions are different, therefore different scaling. hovever, results scale the same way.
            C.PhysicalParameters.rho_A = 1e-1; //     kg / cm²
            C.PhysicalParameters.rho_B = 1.2e-4; //   kg / cm²
            C.PhysicalParameters.mu_A = 1e-3; //      kg / sec
            C.PhysicalParameters.mu_B = 17.1e-6; //   kg / sec
            C.PhysicalParameters.Sigma = 72.75e-1; // kg cm / sec²     
            */

            /*
            // Air-Water (lenght scale == millimeters, 3D space)
            C.PhysicalParameters.rho_A = 1e-6; //     kg / mm³
            C.PhysicalParameters.rho_B = 1.2e-9; //   kg / mm³
            C.PhysicalParameters.mu_A = 1e-6; //      kg / mm / sec
            C.PhysicalParameters.mu_B = 17.1e-9; //   kg / mm / sec
            C.PhysicalParameters.Sigma = 72.75e-3; // kg / sec²     
            */

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            // misc. solver options
            // ====================

            //C.VelocityBlockPrecondMode = MultigridOperator.Mode.SymPart_DiagBlockEquilib;

            //C.Solver_ConvergenceCriterion = 1.0e-6;
            C.LevelSet_ConvergenceCriterion = 1.0e-6;


            bool useFourierLevelSet = false;
            if (useFourierLevelSet) {
                // Fourier -- level-set

                Func<double, double> radius_of_alpha = delegate (double alpha) {
                    double ret = radius + Math.Cos(2.0 * alpha) * radius * elipsDelta;
                    return ret;
                };

                C.FourierLevSetControl = new FourierLevSetControl() { 
                    FType = FourierType.Polar,
                    numSp = 1024,
                    DomainSize = 2 * Math.PI,
                    PeriodicFunc = radius_of_alpha,
                    //FilterWidth = 0.5,
                    UnderRelax = 0.5,
                    InterpolationType = Interpolationtype.LinearSplineInterpolation};

                C.Option_LevelSetEvolution = LevelSetEvolution.Fourier;
                C.AdvancedDiscretizationOptions.SST_isotropicMode = Solution.XNSECommon.SurfaceStressTensor_IsotropicMode.Curvature_Fourier;
            } else {

                C.Option_LevelSetEvolution = LevelSetEvolution.FastMarching;
                C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.Default;
                C.AdvancedDiscretizationOptions.SST_isotropicMode = Solution.XNSECommon.SurfaceStressTensor_IsotropicMode.Curvature_Projected;
                C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 2;
            }

            C.ComputeEnergyProperties = true;

            // Timestepping
            // ============

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 100;
            C.NoOfTimesteps = NoOfTs;

            // haben fertig...
            // ===============

            return C;
        }

        public static XNSE_Control ManufacturedDroplet() {
            XNSE_Control C = new XNSE_Control();


            C.DbPath = null;
            C.savetodb = false;

            C.ProjectName = "XNSE/Droplet";
            C.ProjectDescription = "Multiphase Droplet";

            C.SetFieldOptions(3, 4);

            C.GridFunc = delegate {
                //double[] Xnodes = GenericBlas.Linspace(-1.5, 1.5, 18);
                //double[] Ynodes = GenericBlas.Linspace(-1.5, 1.5, 18);
                double[] Xnodes = GenericBlas.Linspace(-2, 2, 7);
                double[] Ynodes = GenericBlas.Linspace(-2, 2, 8);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes);
                grd.EdgeTagNames.Add(1, "velocity_inlet");

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 1;
                    return et;
                });

                return grd;
            };

            const double dt = 0.3;
            const double CC_A = 1.0;
            const double CC_B = 0.0;
            const double RHO_A = 1.2;
            const double RHO_B = 0.1;
            const double MU_A = 0.1;
            const double MU_B = 0.01;
            const double a0 = 1.0; // Parameter fuer Ellipse
            const double b0 = 0.5; // Parameter fuer Ellipse


            C.InitialValues_Evaluators.Add("Phi",
                X => -1.0 + (X[0] / a0).Pow2() + (X[1] / b0).Pow2()
                //X => -1.0 + ((X[0] / a0).Pow2() + (X[1] / b0).Pow2()).Sqrt()
                );
            C.InitialValues_Evaluators.Add("VelocityX", X => 0.0);
            C.InitialValues_Evaluators.Add("VelocityY", X => 0.0);
            C.InitialValues_Evaluators.Add("GravityX#A", X => -(-2.0 * X[0] * CC_A / RHO_A - (1.0 / dt) * (-X[0])));
            C.InitialValues_Evaluators.Add("GravityX#B", X => -(-2.0 * X[0] * CC_B / RHO_B - (1.0 / dt) * (-X[0])));
            C.InitialValues_Evaluators.Add("GravityY#A", X => +((1.0 / dt) * (+X[1])));
            C.InitialValues_Evaluators.Add("GravityY#B", X => +((1.0 / dt) * (+X[1])));



            C.InitialValues_Evaluators.Add("SurfaceForceX", X => -((CC_A - CC_B) * (1 + X[0].Pow2()) + 2.0 * (MU_A - MU_B)));
            C.InitialValues_Evaluators.Add("SurfaceForceY", X => -((CC_A - CC_B) * (1 + X[0].Pow2()) - 2.0 * (MU_A - MU_B)));

            C.AddBoundaryValue("velocity_inlet", "VelocityX#A", (X, t) => -X[0]);
            C.AddBoundaryValue("velocity_inlet", "VelocityY#A", (X, t) => X[1]);
            C.AddBoundaryValue("velocity_inlet", "VelocityX#B", (X, t) => -X[0]);
            C.AddBoundaryValue("velocity_inlet", "VelocityY#B", (X, t) => X[1]);


            C.AgglomerationThreshold = 0.0;

            C.PhysicalParameters.useArtificialSurfaceForce = true;
            C.PhysicalParameters.rho_A = RHO_A;
            C.PhysicalParameters.rho_B = RHO_B;
            C.PhysicalParameters.mu_A = MU_A;
            C.PhysicalParameters.mu_B = MU_B;
            C.PhysicalParameters.Sigma = 0.0;

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            C.AdvancedDiscretizationOptions.ViscosityMode = Solution.XNSECommon.ViscosityMode.Standard;


            //C.LevelSetSmoothing = false;
            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            //C.LevelSetOptions.CutCellVelocityProjectiontype = Solution.LevelSetTools.Advection.NonconservativeAdvection.CutCellVelocityProjectiontype.L2_plain;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 2 * dt;
            C.NoOfTimesteps = 1;


            return C;
        }


        /// <summary>
        /// See publication, Section 6.3:
        /// Extended discontinuous Galerkin methods for two-phase flows: the spatial discretization, F. Kummer, IJNME 109 (2), 2017. 
        /// </summary>
        public static XNSE_Control TaylorCouette(string _DbPath = null, int k = 3, int sizeFactor = 4) {
            XNSE_Control C = new XNSE_Control();


            // basic database options
            // ======================

            C.DbPath = _DbPath;
            C.savetodb = _DbPath != null;
            C.ProjectName = "XNSE/Droplet";
            C.ProjectDescription = "Multiphase Droplet";
            C.Tags.Add("oscillating");

            // DG degrees
            // ==========

            C.SetFieldOptions(k, 2);

            // grid and boundary conditions
            // ============================

            string innerWallTag = IncompressibleBcType.Velocity_Inlet.ToString() + "_inner";
            string outerWallTag = IncompressibleBcType.Velocity_Inlet.ToString() + "_outer";


            C.GridFunc = delegate {
                double[] Xnodes = GenericBlas.Linspace(-2, 2, 8 * sizeFactor + 1);
                double[] Ynodes = GenericBlas.Linspace(-2, 2, 8 * sizeFactor + 1);
                var cutOut = new BoundingBox(new double[] { -0.5, -0.5 }, new double[] { +0.5, +0.5 });
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, CutOuts: cutOut);
                grd.EdgeTagNames.Add(1, innerWallTag);
                grd.EdgeTagNames.Add(2, outerWallTag);

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[0] - (-0.5)) <= 1.0e-8 || Math.Abs(X[0] - (+0.5)) <= 1.0e-8
                        || Math.Abs(X[1] - (-0.5)) <= 1.0e-8 || Math.Abs(X[1] - (+0.5)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] - (-2)) <= 1.0e-8 || Math.Abs(X[0] - (+2)) <= 1.0e-8
                        || Math.Abs(X[1] - (-2)) <= 1.0e-8 || Math.Abs(X[1] - (+2)) <= 1.0e-8)
                        et = 2;

                    if (et == 0)
                        throw new ApplicationException("error in DefineEdgeTags");
                    return et;
                });

                return grd;
            };


            // Physical Parameters
            // ===================

            const double Ui = 2;
            const double Ua = 1;
            const double rhoA = 0.1;
            const double rhoB = 1.3;
            const double muA = 0.01;
            const double muB = 0.2;
            const double sigma = 0.9;
            double Ri = Math.Sqrt(2) / 2, Ra = 2, Rm = (Ri + Ra) / 2;



            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;
            C.PhysicalParameters.rho_A = rhoA;
            C.PhysicalParameters.rho_B = rhoB;
            C.PhysicalParameters.mu_A = muA;
            C.PhysicalParameters.mu_B = muB;
            C.PhysicalParameters.Sigma = sigma;

            // Exact solution
            // ==============

            //const double _C1A = 1.119266055, _C1B = 1.328440367, _C2A = .2201834863, _C2B = 0.1100917431e-1, _C3A = .9221025166, _C3B = 0;

            //double _C1A = (2*(12*Ua*muB+5*Ui*muA-9*Ui*muB))/(5*muA+27*muB),
            //    _C1B = (2*(3*Ua*muA+9*Ua*muB-4*Ui*muA))/(5*muA+27*muB),
            //    _C2A = -6*muB*(Ua-3*Ui)/(5*muA+27*muB),
            //    _C2B = -6*muA*(Ua-3*Ui)/(5*muA+27*muB),
            //    _C3A = (108*Ua.Pow2()*muA*muB*rhoB-270*Ua.Pow2()*muB.Pow2()*rhoA+162*Ua.Pow2()*muB.Pow2()*rhoB+60*Ua*Ui*muA.Pow2()*rhoB-240*Ua*Ui*muA*muB*rhoA-144*Ua*Ui*muA*muB*rhoB+324*Ua*Ui*muB.Pow2()*rhoA-50*Ui.Pow2()*muA.Pow2()*rhoA-130*Ui.Pow2()*muA.Pow2()*rhoB+180*Ui.Pow2()*muA*muB*rhoA+25*muA.Pow2()*sigma+270*muA*muB*sigma+729*muB.Pow2()*sigma)/(5*muA+27*muB).Pow2(),
            //    _C3B = 0;

            double
               _C1A = (Ra.Pow2() * Ri * Ui * muA - Ra.Pow2() * Ri * Ui * muB + Ra * Rm.Pow2() * Ua * muB - Ri * Rm.Pow2() * Ui * muA) / (Ra.Pow2() * Ri.Pow2() * muA - Ra.Pow2() * Ri.Pow2() * muB + Ra.Pow2() * Rm.Pow2() * muB - Ri.Pow2() * Rm.Pow2() * muA),
               _C1B = (Ra * Ri.Pow2() * Ua * muA - Ra * Ri.Pow2() * Ua * muB + Ra * Rm.Pow2() * Ua * muB - Ri * Rm.Pow2() * Ui * muA) / (Ra.Pow2() * Ri.Pow2() * muA - Ra.Pow2() * Ri.Pow2() * muB + Ra.Pow2() * Rm.Pow2() * muB - Ri.Pow2() * Rm.Pow2() * muA),
               _C2A = Ri * Ra * Rm.Pow2() * muB * (Ra * Ui - Ri * Ua) / (Ra.Pow2() * Ri.Pow2() * muA - Ra.Pow2() * Ri.Pow2() * muB + Ra.Pow2() * Rm.Pow2() * muB - Ri.Pow2() * Rm.Pow2() * muA),
               _C2B = Ra * Ri * Rm.Pow2() * muA * (Ra * Ui - Ri * Ua) / (Ra.Pow2() * Ri.Pow2() * muA - Ra.Pow2() * Ri.Pow2() * muB + Ra.Pow2() * Rm.Pow2() * muB - Ri.Pow2() * Rm.Pow2() * muA),
               _C3A = (1.0 / 2.0) * (-Ra.Pow(4) * Ri.Pow2() * Rm.Pow(3) * Ui.Pow2() * muA.Pow2() * rhoA - Ra.Pow(4) * Ri.Pow2() * Rm.Pow(3) * Ui.Pow2() * muA.Pow2() * rhoB + Ra.Pow2() * Ri.Pow(4) * Rm.Pow(3) * Ua.Pow2() * muB.Pow2() * rhoA + Ra.Pow2() * Ri.Pow(4) * Rm.Pow(3) * Ua.Pow2() * muB.Pow2() * rhoB - 2 * Ra.Pow2() * Ri.Pow2() * Rm.Pow(5) * Ua.Pow2() * muB.Pow2() * rhoB + 2 * Ra.Pow2() * Ri.Pow2() * Rm.Pow(5) * Ui.Pow2() * muA.Pow2() * rhoA + 4 * Ra.Pow(4) * Ri.Pow2() * Rm.Pow2() * muA * muB * sigma + 4 * Ra.Pow2() * Ri.Pow(4) * Rm.Pow2() * muA * muB * sigma - 4 * Ra.Pow2() * Ri.Pow2() * Rm.Pow(4) * muA * muB * sigma - Ra.Pow2() * Rm.Pow(7) * Ua.Pow2() * muB.Pow2() * rhoA + Ra.Pow2() * Rm.Pow(7) * Ua.Pow2() * muB.Pow2() * rhoB - Ri.Pow2() * Rm.Pow(7) * Ui.Pow2() * muA.Pow2() * rhoA + Ri.Pow2() * Rm.Pow(7) * Ui.Pow2() * muA.Pow2() * rhoB - 4 * Ra.Pow(4) * Ri.Pow(4) * muA * muB * sigma - 4 * Ra.Pow(4) * Ri.Pow2() * Rm.Pow2() * muB.Pow2() * sigma - 4 * Ra.Pow2() * Ri.Pow(4) * Rm.Pow2() * muA.Pow2() * sigma + 2 * Ra.Pow(4) * Ri.Pow(4) * muA.Pow2() * sigma + 2 * Ra.Pow(4) * Ri.Pow(4) * muB.Pow2() * sigma + 2 * Ra.Pow(4) * Rm.Pow(4) * muB.Pow2() * sigma + 2 * Ri.Pow(4) * Rm.Pow(4) * muA.Pow2() * sigma + 4 * Ra.Pow(3) * Ri.Pow(3) * Rm.Pow(3) * Ua * Ui * muA.Pow2() * rhoB * Math.Log(Rm) - 4 * Ra.Pow(3) * Ri.Pow(3) * Rm.Pow(3) * Ua * Ui * muB.Pow2() * rhoA * Math.Log(Rm) - 4 * Ra.Pow(3) * Ri * Rm.Pow(5) * Ua * Ui * muB.Pow2() * rhoA * Math.Log(Rm) + 4 * Ra.Pow2() * Ri.Pow(4) * Rm.Pow(3) * Ua.Pow2() * muA * muB * rhoB * Math.Log(Rm) - 4 * Ra.Pow2() * Ri.Pow2() * Rm.Pow(5) * Ua.Pow2() * muA * muB * rhoB * Math.Log(Rm) + 4 * Ra.Pow2() * Ri.Pow2() * Rm.Pow(5) * Ui.Pow2() * muA * muB * rhoA * Math.Log(Rm) + 4 * Ra * Ri.Pow(3) * Rm.Pow(5) * Ua * Ui * muA.Pow2() * rhoB * Math.Log(Rm) - 2 * Ra.Pow(3) * Ri * Rm.Pow(5) * Ua * Ui * muA * muB * rhoA + 2 * Ra * Ri.Pow(3) * Rm.Pow(5) * Ua * Ui * muA * muB * rhoB + 2 * Ra * Ri * Rm.Pow(7) * Ua * Ui * muA * muB * rhoA - 2 * Ra * Ri * Rm.Pow(7) * Ua * Ui * muA * muB * rhoB - 4 * Ra.Pow(4) * Ri.Pow2() * Rm.Pow(3) * Ui.Pow2() * muA * muB * rhoA * Math.Log(Rm) + 4 * Ra.Pow(3) * Ri.Pow(3) * Rm.Pow(3) * Ua * Ui * muA * muB * rhoA * Math.Log(Rm) - 4 * Ra.Pow(3) * Ri.Pow(3) * Rm.Pow(3) * Ua * Ui * muA * muB * rhoB * Math.Log(Rm) + 4 * Ra.Pow(3) * Ri * Rm.Pow(5) * Ua * Ui * muA * muB * rhoB * Math.Log(Rm) - 4 * Ra * Ri.Pow(3) * Rm.Pow(5) * Ua * Ui * muA * muB * rhoA * Math.Log(Rm) + 4 * Ra.Pow(4) * Ri.Pow2() * Rm.Pow(3) * Ui.Pow2() * muB.Pow2() * rhoA * Math.Log(Rm) - 4 * Ra.Pow2() * Ri.Pow(4) * Rm.Pow(3) * Ua.Pow2() * muA.Pow2() * rhoB * Math.Log(Rm) + 4 * Ra.Pow2() * Ri.Pow2() * Rm.Pow(5) * Ua.Pow2() * muB.Pow2() * rhoA * Math.Log(Rm) - 4 * Ra.Pow2() * Ri.Pow2() * Rm.Pow(5) * Ui.Pow2() * muA.Pow2() * rhoB * Math.Log(Rm) + 2 * Ra.Pow(4) * Ri.Pow2() * Rm.Pow(3) * Ui.Pow2() * muA * muB * rhoA + 2 * Ra.Pow(3) * Ri.Pow(3) * Rm.Pow(3) * Ua * Ui * muA.Pow2() * rhoB - 2 * Ra.Pow(3) * Ri.Pow(3) * Rm.Pow(3) * Ua * Ui * muB.Pow2() * rhoA + 2 * Ra.Pow(3) * Ri * Rm.Pow(5) * Ua * Ui * muB.Pow2() * rhoA - 2 * Ra.Pow2() * Ri.Pow(4) * Rm.Pow(3) * Ua.Pow2() * muA * muB * rhoB + 2 * Ra.Pow2() * Ri.Pow2() * Rm.Pow(5) * Ua.Pow2() * muA * muB * rhoB - 2 * Ra.Pow2() * Ri.Pow2() * Rm.Pow(5) * Ui.Pow2() * muA * muB * rhoA - 2 * Ra * Ri.Pow(3) * Rm.Pow(5) * Ua * Ui * muA.Pow2() * rhoB) / (Rm * (Ra.Pow2() * Ri.Pow2() * muA - Ra.Pow2() * Ri.Pow2() * muB + Ra.Pow2() * Rm.Pow2() * muB - Ri.Pow2() * Rm.Pow2() * muA).Pow2()),
               _C3B = 0;


            Func<double, double> vA = r => _C1A * r + _C2A / r;
            Func<double, double> vB = r => _C1B * r + _C2B / r;
            Func<double, double> psiA = r => 0.5 * rhoA * _C1A.Pow2() * r.Pow2() - 0.5 * rhoA * _C2A.Pow2() / (r.Pow2()) + 2 * rhoA * _C1A * _C2A * Math.Log(r) + _C3A;
            Func<double, double> psiB = r => 0.5 * rhoB * _C1B.Pow2() * r.Pow2() - 0.5 * rhoB * _C2B.Pow2() / (r.Pow2()) + 2 * rhoB * _C1B * _C2B * Math.Log(r) + _C3B;
            //Func<double,double> _psiA = r => 0.5838609245e-2 * r.Pow2() - .1256228666 / r.Pow2() - .1083300757 * Math.Log(r) + .4668609891;
            //Func<double,double> _psiB = r => .1498764510 * r.Pow2() - 0.4082743166e-2 / r.Pow2() + 0.9894702064e-1 * Math.Log(r);

            //Console.WriteLine("Errors: {0}, {1}", psiA(0.7) - _psiA(0.7), psiB(1.7) - _psiB(1.7));
            //Console.WriteLine("Drücke: {0}, {1}", psiA(Rm), psiB(Rm));


            Func<double[], double, double> UA1 = (X, t) => (-X[1] / X.L2Norm()) * vA(X.L2Norm());
            Func<double[], double, double> UA2 = (X, t) => (+X[0] / X.L2Norm()) * vA(X.L2Norm());
            Func<double[], double, double> UB1 = (X, t) => (-X[1] / X.L2Norm()) * vB(X.L2Norm());
            Func<double[], double, double> UB2 = (X, t) => (+X[0] / X.L2Norm()) * vB(X.L2Norm());

            Func<double[], double, double> PsiA = (X, t) => psiA(X.L2Norm());
            Func<double[], double, double> PsiB = (X, t) => psiB(X.L2Norm());


            C.ExactSolutionVelocity = new Dictionary<string, Func<double[], double, double>[]>();
            C.ExactSolutionVelocity.Add("A", new Func<double[], double, double>[] { UA1, UA2 });
            C.ExactSolutionVelocity.Add("B", new Func<double[], double, double>[] { UB1, UB2 });

            C.ExactSolutionPressure = new Dictionary<string, Func<double[], double, double>>();
            C.ExactSolutionPressure.Add("A", PsiA);
            C.ExactSolutionPressure.Add("B", PsiB);


            // Boundary condition
            // ==================

            C.AddBoundaryValue(innerWallTag, "VelocityX#A", UA1);
            C.AddBoundaryValue(innerWallTag, "VelocityY#A", UA2);
            C.AddBoundaryValue(innerWallTag, "VelocityX#B", (X, t) => double.NaN);
            C.AddBoundaryValue(innerWallTag, "VelocityY#B", (X, t) => double.NaN);

            C.AddBoundaryValue(outerWallTag, "VelocityX#A", (X, t) => double.NaN);
            C.AddBoundaryValue(outerWallTag, "VelocityY#A", (X, t) => double.NaN);
            C.AddBoundaryValue(outerWallTag, "VelocityX#B", UB1);
            C.AddBoundaryValue(outerWallTag, "VelocityY#B", UB2);


            // Initial Values
            // ==============


            C.InitialValues_Evaluators.Add("Phi",
                (X => X.L2NormPow2() - Rm.Pow2())  // quadratic form
                );

            C.InitialValues_Evaluators.Add("VelocityX#A", x => UA1(x, 0));
            C.InitialValues_Evaluators.Add("VelocityY#A", x => UA2(x, 0));
            C.InitialValues_Evaluators.Add("VelocityX#B", x => UB1(x, 0));
            C.InitialValues_Evaluators.Add("VelocityY#B", x => UB2(x, 0));

            C.InitialValues_Evaluators.Add("Pressure#A", x => PsiA(x, 0));
            C.InitialValues_Evaluators.Add("Pressure#B", x => PsiB(x, 0));



            // misc. solver options
            // ====================

            C.AgglomerationThreshold = 0.1;
            C.AdvancedDiscretizationOptions.ViscosityMode = Solution.XNSECommon.ViscosityMode.FullySymmetric;
            C.Option_LevelSetEvolution = LevelSetEvolution.None;
            C.Timestepper_LevelSetHandling = LevelSetHandling.None;
            //C.VelocityBlockPrecondMode = MultigridOperator.Mode.SymPart_DiagBlockEquilib;
            C.LinearSolver.NoOfMultigridLevels = 3;
            C.LinearSolver.MaxSolverIterations = 20;
            C.NonLinearSolver.MaxSolverIterations = 20;
            //C.Solver_MaxIterations = 20;

            // Timestepping
            // ============

            C.TimesteppingMode = AppControl._TimesteppingMode.Steady;

            // haben fertig...
            // ===============

            return C;
        }

        public static XNSE_Control Rotating_Cube(int k = 1, int Res = 20, int SpaceDim = 3, bool useAMR = true, int NoOfTimesteps = 10, bool writeToDB = false, bool tracing = false, bool loadbalancing = false) {

            double anglev = 10;
            double[] pos = new double[SpaceDim];
            double particleRad = 0.261;

            Func<double[], double, double> PhiFunc = delegate (double[] X, double t) {
                double power = 10;
                //anglev *= t < 0.005 ? Math.Sin(2000 * Math.PI * t - Math.PI / 2) / 2 + 0.5 : 1;
                double angle = -(anglev * t) % (2 * Math.PI);
                switch (SpaceDim) {
                    case 2:
                    // Inf-Norm square
                    return -Math.Max(Math.Abs((X[0] - pos[0]) * Math.Cos(angle) - (X[1] - pos[1]) * Math.Sin(angle)),
                        Math.Abs((X[0] - pos[0]) * Math.Sin(angle) + (X[1] - pos[1]) * Math.Cos(angle)))
                        + particleRad;

                    // p-Norm square
                    //return -Math.Pow((Math.Pow((X[0] - pos[0]) * Math.Cos(angle) - (X[1] - pos[1]) * Math.Sin(angle), power)
                    //+ Math.Pow((X[0] - pos[0]) * Math.Sin(angle) + (X[1] - pos[1]) * Math.Cos(angle), power)), 1.0/power)
                    //+ particleRad; // 1e6

                    // 0-Norm square
                    //return -Math.Abs((X[0] - pos[0]) * Math.Cos(angle) - (X[1] - pos[1]) * Math.Sin(angle))
                    //- Math.Abs((X[0] - pos[0]) * Math.Sin(angle) + (X[1] - pos[1]) * Math.Cos(angle))
                    //+ Math.Abs(particleRad);

                    case 3:
                    // Inf-Norm cube
                    return -Math.Max(Math.Abs((X[0] - pos[0]) * Math.Cos(angle) - (X[1] - pos[1]) * Math.Sin(angle)),
                                            Math.Max(Math.Abs((X[0] - pos[0]) * Math.Sin(angle) + (X[1] - pos[1]) * Math.Cos(angle)),
                                            Math.Abs(X[2] - pos[2])))
                                            + particleRad;

                    // p-Norm cube
                    //return -Math.Pow(Math.Pow((X[0] - pos[0]) * Math.Cos(angle) - (X[1] - pos[1]) * Math.Sin(angle), power)
                    //+ Math.Pow((X[0] - pos[0]) * Math.Sin(angle) + (X[1] - pos[1]) * Math.Cos(angle), power)
                    //+ Math.Pow(X[2] - pos[2], power),1.0/power)
                    //+ particleRad;

                    // 0-Norm cube
                    //return -Math.Abs((X[0] - pos[0]) * Math.Cos(angle) - (X[1] - pos[1]) * Math.Sin(angle))
                    //- Math.Abs((X[0] - pos[0]) * Math.Sin(angle) + (X[1] - pos[1]) * Math.Cos(angle))
                    //- Math.Abs(X[2] - pos[2])
                    //+ Math.Abs(particleRad);

                    default:
                    throw new NotImplementedException();
                }
            };
            return Rotating_Something(k, Res, SpaceDim, useAMR, NoOfTimesteps, writeToDB, tracing, loadbalancing, pos, anglev, particleRad, PhiFunc);
        }

        public static XNSE_Control Rotating_Sphere(int k = 1, int Res = 10, int SpaceDim = 3, bool useAMR = true, int NoOfTimesteps = 10, bool writeToDB = false, bool tracing = false, bool loadbalancing = false) {
            //cs:BoSSS.Application.XNSE_Solver.HardcodedControl.Rotating_Sphere(1,10,3,true,10,false,true,true)
            double anglev = 10;
            double[] pos = new double[SpaceDim];
            double particleRad = 0.261;

            Func<double[], double, double> PhiFunc = delegate (double[] X, double t) {
                double power = 10;
                //anglev *= t < 0.005 ? Math.Sin(2000 * Math.PI * t - Math.PI / 2) / 2 + 0.5 : 1;
                double angle = -(anglev * t) % (2 * Math.PI);
                switch (SpaceDim) {
                    case 2:
                    // circle
                    return -X[0] * X[0] - X[1] * X[1] + particleRad * particleRad;

                    case 3:
                    // sphere
                    return -X[0] * X[0] - X[1] * X[1] - X[2] * X[2] + particleRad * particleRad;

                    default:
                    throw new NotImplementedException();
                }
            };

            var C = Rotating_Something(k, Res, SpaceDim, useAMR, NoOfTimesteps, writeToDB, tracing, loadbalancing, pos, anglev, particleRad, PhiFunc);
            C.LSContiProjectionMethod = ContinuityProjectionOption.None;
            return C;
        }

        public static XNSE_Control Rotating_Something(int k, int Res, int SpaceDim, bool useAMR, int NoOfTimesteps,bool writeToDB, bool tracing, bool loadbalancing, double[] pos, double anglev, double particleRad, Func<double[], double, double> PhiFunc ) {
            XNSE_Control C = new XNSE_Control();
            // basic database options
            // ======================

            if (writeToDB) {
                var thisOS = System.Environment.OSVersion.Platform;
                var MachineName = System.Environment.MachineName;
                switch(thisOS) {
                    case PlatformID.Unix:
                        C.AlternateDbPaths = new[] {
                            (@"/work/scratch/jw52xeqa/DB_IBM_test", ""),
                            (@"W:\work\scratch\jw52xeqa\DB_IBM_test","")};
                        break;
                    case PlatformID.Win32NT:
                        if (MachineName == "PCMIT32") {
                            C.DbPath = @"D:\trash_db";
                            //C.DbPath = @"D:\2D_Partitioning_samples";
                        } else {
                            C.DbPath = @"\\hpccluster\hpccluster-scratch\weber\DB_IBM_test";
                        }
                        break;
                    default:
                        throw new Exception("No Db-path specified. You stupid?");
                }               
                (@"C:\Users\flori\default_bosss_db", "stormbreaker").AddToArray(ref C.AlternateDbPaths);
            }
            C.savetodb = writeToDB;
            C.ProjectName = "XNSE/IBM_benchmark";
            C.ProjectDescription = "rotating cube";
            C.Tags.Add("rotating");
            C.Tags.Add("3_cluster");

            // DG degrees
            // ==========

            //C.SetFieldOptions(k, Math.Max(6, k * 2));
            C.SetFieldOptions(k, Math.Max(k, 2));
            C.SessionName = "XNSE_rotsphere";
            C.saveperiod = 1;
            if (tracing) 
                C.TracingNamespaces = "*";
            //IBMCestimator = new 
            //C.DynamicLoadBalancing_CellCostEstimatorFactories = new List<Func<IApplication, int, ICellCostEstimator>>();

            // grid and boundary conditions
            // ============================

            //// Create Grid
            double xMin = -1, yMin = -1, zMin = -1;
            double xMax = 1, yMax = 1, zMax = 1;

            Func<double[], int> MakeDebugPart = delegate (double[] X) {
                double x = X[0];
                double range = xMax - xMin;
                double interval = range / ilPSP.Environment.MPIEnv.MPI_Size;
                return (int)((x - xMin) / interval);
            };

            C.GridFunc = delegate {

                // x-direction
                
                var _xNodes = GenericBlas.Linspace(xMin, xMax, Res + 1);
                //var _xNodes = GenericBlas.Logspace(0, 3, cells_x + 1);
                // y-direction
                var _yNodes = GenericBlas.Linspace(yMin, yMax, Res + 1);
                // z-direction
                var _zNodes = GenericBlas.Linspace(zMin, zMax, Res + 1);


                GridCommons grd;
                switch (SpaceDim) {
                    case 2:
                    grd = Grid2D.Cartesian2DGrid(_xNodes, _yNodes);
                    break;

                    case 3:
                    grd = Grid3D.Cartesian3DGrid(_xNodes, _yNodes, _zNodes, Foundation.Grid.RefElements.CellType.Cube_Linear, false, false, false);
                    break;

                    default:
                    throw new ArgumentOutOfRangeException();
                }

                //grd.AddPredefinedPartitioning("debug", MakeDebugPart);

                grd.EdgeTagNames.Add(1, "Velocity_inlet");
                grd.EdgeTagNames.Add(2, "Wall");
                grd.EdgeTagNames.Add(3, "Pressure_Outlet");

                grd.DefineEdgeTags(delegate (double[] _X) {
                    var X = _X;
                    double x, y, z;
                    x = X[0];
                    y = X[1];
                    if (SpaceDim == 3)
                        z = X[2];

                    return 2;
                });

                return grd;

            };
            //C.GridPartType = GridPartType.Predefined;
            //C.GridPartOptions = "debug";
            C.GridPartType = GridPartType.clusterHilbert;
            C.Tags.Add(C.GridPartType.ToString());

            C.DynamicLoadbalancing_ClassifierType = ClassifierType.CutCells;
            C.DynamicLoadBalancing_On = loadbalancing;
            C.DynamicLoadBalancing_RedistributeAtStartup = true;
            C.DynamicLoadBalancing_Period = 1;
            C.DynamicLoadBalancing_CellCostEstimatorFactories = Loadbalancing.XNSECellCostEstimator.Factory().ToList();
            C.DynamicLoadBalancing_ImbalanceThreshold = -0.1;

            //// Set Initial Conditions
            //C.InitialValues_Evaluators.Add("VelocityX", X => 0);
            //C.InitialValues_Evaluators.Add("VelocityY", X => 0);
            //if (SpaceDim == 3)
            //    C.InitialValues_Evaluators.Add("VelocityZ", X => 0);

            // Phi (X,t): p-norm cube with forced rotation

            // Physical Parameters
            // ===================
            const double rhoA = 1;
            const double muA = 1;

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;
            C.PhysicalParameters.rho_A = rhoA;
            C.PhysicalParameters.mu_A = muA;

            Func<double[], double, double[]> VelocityAtIB = delegate (double[] X, double time) {

                if (pos.Length != X.Length)
                    throw new ArgumentException("check dimension of center of mass");

                Vector angVelo = new Vector(new double[] { 0, 0, anglev });
                Vector CenterofMass = new Vector(pos);
                Vector radialVector = new Vector(X) - CenterofMass;
                Vector transVelocity = new Vector(new double[SpaceDim]);
                Vector pointVelocity;

                switch (SpaceDim) {
                    case 2:
                    pointVelocity = new Vector(transVelocity[0] - angVelo[2] * radialVector[1], transVelocity[1] + angVelo[2] * radialVector[0]);
                    break;
                    case 3:
                    pointVelocity = transVelocity + angVelo.CrossProduct(radialVector);
                    break;
                    default:
                    throw new NotImplementedException("this number of dimensions is not supported");
                }

                return pointVelocity;
            };

            Func<double[], double, double> VelocityX = delegate (double[] X, double time) { return VelocityAtIB(X, time)[0]; };
            Func<double[], double, double> VelocityY = delegate (double[] X, double time) { return VelocityAtIB(X, time)[1]; };
            Func<double[], double, double> VelocityZ = delegate (double[] X, double time) { return VelocityAtIB(X, time)[2]; };

            var PhiFuncDelegate = BoSSS.Solution.Utils.NonVectorizedScalarFunction.Vectorize(PhiFunc);

            C.InitialValues_Evaluators.Add(VariableNames.LevelSetCGidx(0), X => -1); 
            C.UseImmersedBoundary = true;
            if (C.UseImmersedBoundary) {
                C.InitialValues_Evaluators_TimeDep.Add(VariableNames.LevelSetCGidx(1), PhiFunc);
                //C.InitialValues_EvaluatorsVec.Add(VariableNames.LevelSetCGidx(1), PhiFuncDelegate);
                C.InitialValues_Evaluators_TimeDep.Add("VelocityX@Phi2", VelocityX);
                C.InitialValues_Evaluators_TimeDep.Add("VelocityY@Phi2", VelocityY);
                if (SpaceDim == 3)
                    C.InitialValues_Evaluators_TimeDep.Add("VelocityZ@Phi2", VelocityZ);
            }
            C.InitialValues_Evaluators.Add("Pressure", X => 0);
            C.AddBoundaryValue("Wall");

            //C.OperatorMatrixAnalysis = false;

            // misc. solver options
            // ====================

            //C.EqualOrder = false;
            //C.PressureStabilizationFactor = 1;
            C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Saye;
            C.LSContiProjectionMethod = Solution.LevelSetTools.ContinuityProjectionOption.ConstrainedDG;

            C.UseSchurBlockPrec = true;
            //C.VelocityBlockPrecondMode = MultigridOperator.Mode.SymPart_DiagBlockEquilib_DropIndefinite;
            //C.PressureBlockPrecondMode = MultigridOperator.Mode.SymPart_DiagBlockEquilib_DropIndefinite;
            C.AgglomerationThreshold = 0.1;
            C.AdvancedDiscretizationOptions.ViscosityMode = ViscosityMode.FullySymmetric;
            C.Option_LevelSetEvolution2 = LevelSetEvolution.Prescribed;
            C.Option_LevelSetEvolution = LevelSetEvolution.None;
            C.Timestepper_LevelSetHandling = LevelSetHandling.Coupled_Once;
            C.LinearSolver.NoOfMultigridLevels = 5;
            C.LinearSolver.ConvergenceCriterion = 1E-8;
            C.LinearSolver.MaxSolverIterations = 1000;
            C.LinearSolver.MaxKrylovDim = 50;
            C.LinearSolver.TargetBlockSize = 10000;
            C.LinearSolver.verbose = true;
            C.LinearSolver.SolverCode = LinearSolverCode.exp_Kcycle_schwarz;
            C.NonLinearSolver.SolverCode = NonLinearSolverCode.Picard;
            C.NonLinearSolver.MaxSolverIterations = 50;
            C.NonLinearSolver.verbose = true;
            
            C.AdaptiveMeshRefinement = useAMR;
            if (useAMR) {
                C.activeAMRlevelIndicators.Add(new AMRonNarrowband() { maxRefinementLevel = 2 });
                C.AMR_startUpSweeps = 1;
            }

            // Timestepping
            // ============

            //C.TimesteppingMode = AppControl._TimesteppingMode.Steady;
            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            double dt = 0.01;
            //C.dtMax = dt;
            //C.dtMin = dt*1E-2;
            C.dtFixed = dt;
            C.NoOfTimesteps = NoOfTimesteps;

            // haben fertig...
            // ===============
            return C;

        }


        public static XNSE_Control Rotating_Cube2(int dim = 3, int p = 2, int kelem = 20, bool useAMR = true) {

            XNSE_Control C = new XNSE_Control();

            bool useIB = true;

            C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Saye;

            AppControl._TimesteppingMode compMode = AppControl._TimesteppingMode.Transient;

            string _DbPath = null;

            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = C.DbPath != null;
            C.ProjectName = "RotatingCube3D";
            C.SessionName = "SetupTest";

            C.ContinueOnIoError = false;

            #endregion


            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            if (dim == 3) {
                C.FieldOptions.Add("VelocityZ", new FieldOpts() {
                    Degree = p,
                    SaveToDB = FieldOpts.SaveToDBOpt.TRUE
                });
            }
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = Math.Max(p, 2),
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            if (useIB) {
                C.FieldOptions.Add("PhiDG2", new FieldOpts() {
                    SaveToDB = FieldOpts.SaveToDBOpt.TRUE
                });
                C.FieldOptions.Add("Phi2", new FieldOpts() {
                    Degree = Math.Max(p, 2),
                    SaveToDB = FieldOpts.SaveToDBOpt.TRUE
                });
            }
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // Physical Parameters
            // ===================
            #region physics

            C.PhysicalParameters.rho_A = 1;
            C.PhysicalParameters.rho_B = 1;
            C.PhysicalParameters.mu_A = 1;
            C.PhysicalParameters.mu_B = 1;
            C.PhysicalParameters.Sigma = 0;

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            #endregion


            // grid generation
            // ===============
            #region grid

            C.GridFunc = delegate () {
                double[] cube = GenericBlas.Linspace(-1.0, 1.0, kelem + 1);
                GridCommons grd; 
                if (dim == 3) 
                    grd = Grid3D.Cartesian3DGrid(cube, cube, cube);
                else
                    grd = Grid2D.Cartesian2DGrid(cube, cube);

                grd.EdgeTagNames.Add(1, "wall");

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + (1.0)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - (1.0)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] + (1.0)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] - (1.0)) <= 1.0e-8)
                        et = 1;
                    if (dim == 3) {
                        if (Math.Abs(X[2] + (1.0)) <= 1.0e-8)
                            et = 1;
                        if (Math.Abs(X[2] - (1.0)) <= 1.0e-8)
                            et = 1;
                    }
                    return et;
                });

                return grd;
            };

            #endregion


            // Initial Values
            // ==============
            #region init

            Func<double[], double, double> PhiFunc = delegate (double[] X, double t) {
                double[] pos = new double[3];
                double anglev = 10;
                //double t = 0;
                double angle = -(anglev * t) % (2 * Math.PI);
                double particleRad = 0.261;

                if (dim == 3) {
                    return -Math.Max(Math.Abs((X[0] - pos[0]) * Math.Cos(angle) - (X[1] - pos[1]) * Math.Sin(angle)),
                                            Math.Max(Math.Abs((X[0] - pos[0]) * Math.Sin(angle) + (X[1] - pos[1]) * Math.Cos(angle)),
                                            Math.Abs(X[2] - pos[2])))
                                            + particleRad;
                } else {
                    return -Math.Max(Math.Abs((X[0] - pos[0]) * Math.Cos(angle) - (X[1] - pos[1]) * Math.Sin(angle)),
                        Math.Abs((X[0] - pos[0]) * Math.Sin(angle) + (X[1] - pos[1]) * Math.Cos(angle)))
                        + particleRad;
                }
             
            };

          
            if (useIB) {
                C.InitialValues_Evaluators.Add(VariableNames.LevelSetCGidx(0), X => -1);
                C.UseImmersedBoundary = true;
                C.InitialValues_Evaluators_TimeDep.Add(VariableNames.LevelSetCGidx(1), PhiFunc);
            } else {
                C.InitialValues_Evaluators_TimeDep.Add("Phi", PhiFunc);
            }


            #endregion


            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("wall");

            #endregion


            // misc. solver options
            // ====================
            #region solver

            C.solveKineticEnergyEquation = false;
            //C.ComputeEnergyProperties = true;

            C.CheckJumpConditions = false;
            C.CheckInterfaceProps = false;

            C.LSContiProjectionMethod = Solution.LevelSetTools.ContinuityProjectionOption.ConstrainedDG;

            C.LinearSolver.SolverCode = LinearSolverCode.classic_pardiso;
            //C.NonLinearSolver.SolverCode = NonLinearSolverCode.Newton;

            C.LinearSolver.NoOfMultigridLevels = 3;
            C.NonLinearSolver.MaxSolverIterations = 50;
            C.LinearSolver.MaxSolverIterations = 50;
            C.NonLinearSolver.MinSolverIterations = 2;
            //C.Solver_MaxIterations = 80;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            //C.Solver_ConvergenceCriterion = 1e-8;
            C.LevelSet_ConvergenceCriterion = 1e-6;

            C.AdvancedDiscretizationOptions.ViscosityMode = ViscosityMode.FullySymmetric;

            #endregion


            // level set options
            // ====================
            #region solver

            C.Option_LevelSetEvolution = LevelSetEvolution.None;
            if (useIB)
                C.Option_LevelSetEvolution2 = LevelSetEvolution.None;

            C.AdvancedDiscretizationOptions.SST_isotropicMode = SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_ContactLine;

            //C.InitSignedDistance = true;


            C.AdaptiveMeshRefinement = useAMR;
            C.activeAMRlevelIndicators.Add(new AMRonNarrowband() { maxRefinementLevel = 2 });
            C.AMR_startUpSweeps = 1;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.Timestepper_BDFinit = TimeStepperInit.SingleInit;

            C.Timestepper_LevelSetHandling = LevelSetHandling.None;

            C.TimesteppingMode = compMode;

            if (compMode == AppControl._TimesteppingMode.Transient) {
                double dt = 1;
                C.dtMax = dt;
                C.dtMin = dt;
                C.Endtime = 1000;
                C.NoOfTimesteps = 2;
            }

            #endregion

            return C;
        }


        public static XNSE_Control CouettePoiseuille(string _DbPath = null, int p = 2) {

            XNSE_Control C = new XNSE_Control();

            //const double pSize = 1.0;
            bool xPeriodic = true;

            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = _DbPath != null;
            C.ProjectName = "XNSE/CouettePoiseuille";
            C.ProjectDescription = "Static Multiphase";

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = 4,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = 8,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // physical parameters
            // ===================
            #region physics

            const double rhoA = 20;
            const double rhoB = 10;
            const double muA = 3;
            const double muB = 1;
            const double sigma = 0.0;

            C.PhysicalParameters.rho_A = rhoA;
            C.PhysicalParameters.rho_B = rhoB;
            C.PhysicalParameters.mu_A = muA;
            C.PhysicalParameters.mu_B = muB;
            C.PhysicalParameters.Sigma = sigma;

            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion


            // grid genration
            // ==============
            #region grid

            double H = 1;
            double L = 2 * H;

            int ykelem = 5;
            int xkelem = 2 * ykelem;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-L / 2, L / 2, xkelem);
                double[] Ynodes = GenericBlas.Linspace(0, H, ykelem);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: xPeriodic);
                grd.EdgeTagNames.Add(1, "Velocity_inlet_lower");
                grd.EdgeTagNames.Add(2, "Velocity_inlet_upper");

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1]) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - H) <= 1.0e-8)
                        et = 2;

                    return et;
                });
                return grd;
            };

            #endregion


            // boundary conditions
            // ===================
            #region BC

            const double u_w = 0.5;

            C.AddBoundaryValue("Velocity_inlet_lower", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("Velocity_inlet_lower", "VelocityX#B", (X, t) => 0.0);
            C.AddBoundaryValue("Velocity_inlet_upper", "VelocityX#A", (X, t) => u_w);
            C.AddBoundaryValue("Velocity_inlet_upper", "VelocityX#B", (X, t) => u_w);

            #endregion


            // initial values
            // ==================
            #region init

            double y_s = 3.0 / 5.0;

            C.InitialValues_Evaluators.Add("Phi",
                (X => X[1] - y_s)
                );

            double eps = muA / muB;
            double Re = rhoA * H * u_w / muA;

            Func<double[], double> u0_A = X => u_w * (Math.Exp(Re * X[1]) - 1.0) / (Math.Exp(Re * y_s) - 1.0 - Math.Exp(Re * y_s * (1.0 - eps)) * (Math.Exp(eps * Re * y_s) - Math.Exp(eps * Re)));
            Func<double[], double> u0_B = X => u_w + u_w * Math.Exp(Re * y_s * (1.0 - eps)) * (Math.Exp(eps * Re * X[1]) - Math.Exp(eps * Re)) / (Math.Exp(Re * y_s) - 1.0 - Math.Exp(Re * y_s * (1.0 - eps)) * (Math.Exp(eps * Re * y_s) - Math.Exp(eps * Re)));

            C.InitialValues_Evaluators.Add("VelocityX#A", u0_A);
            C.InitialValues_Evaluators.Add("VelocityX#B", u0_B);

            #endregion


            // misc. solver options
            // ====================
            #region solver

            C.Option_LevelSetEvolution = LevelSetEvolution.None;
            C.ComputeEnergyProperties = false;

            //C.VelocityBlockPrecondMode = MultigridOperator.Mode.SymPart_DiagBlockEquilib;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 100;
            C.NonLinearSolver.MaxSolverIterations = 100;
            //C.Solver_MaxIterations = 100;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            //C.Solver_ConvergenceCriterion = 1e-8;

            #endregion


            // Timestepping
            // ===============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            //C.TimeStepper = XNSE_Control._Timestepper.BDF2;
            double dt = 0.1;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 15;

            #endregion

            return C;
        }


        public static XNSE_Control TranspiratingChannel(string _DbPath = null, int p = 2) {

            XNSE_Control C = new XNSE_Control();

            const int kelem = 9;
            const double pSize = 1;
            bool xPeriodic = true;
            // const double y_0 = 0.1; // has to be also changed in XNSE_SolverMain

            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = _DbPath != null;
            C.ProjectName = "XNSE/TranspiratingChannel";
            C.ProjectDescription = "Manufactured Solution";

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = 4,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = 8,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion

            // grid genration
            // ==============
            #region grid

            double xSize = 2 * pSize;
            double ySize = pSize;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-xSize, xSize, 2 * kelem + 1);
                double[] Ynodes = GenericBlas.Linspace(-ySize, ySize, kelem + 1);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: xPeriodic);
                grd.EdgeTagNames.Add(1, "Velocity_inlet_lower");
                grd.EdgeTagNames.Add(2, "Velocity_inlet_upper");
                if (!xPeriodic) {
                    grd.EdgeTagNames.Add(3, "Pressure_Dirichlet_left");
                    grd.EdgeTagNames.Add(4, "Pressure_Dirichlet_right");
                }

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + ySize) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - ySize) <= 1.0e-8)
                        et = 2;
                    if (!xPeriodic) {
                        if (Math.Abs(X[0] + xSize) <= 1.0e-8)
                            et = 3;
                        if (Math.Abs(X[0] - xSize) <= 1.0e-8)
                            et = 4;
                    }

                    return et;
                });

                return grd;
            };

            #endregion

            // physical parameters
            // ===================
            #region physics

            const double rhoA = 1;
            const double rhoB = 2;
            const double muA = 1;
            const double muB = 2;
            const double sigma = 1;

            C.PhysicalParameters.rho_A = rhoA;
            C.PhysicalParameters.rho_B = rhoB;
            C.PhysicalParameters.mu_A = muA;
            C.PhysicalParameters.mu_B = muB;
            C.PhysicalParameters.Sigma = sigma;

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            #endregion

            // exact solution
            // ==============
            #region solution

            const double v0 = 0.1;
            const double psi0 = -0.1;

            Func<double, double> a_A = t => (-1 / rhoA) * psi0 * t;
            Func<double, double> a_B = t => (-1 / rhoB) * psi0 * t;
            double b_A = -(psi0 / v0) * (((1 / rhoB) - (1 / rhoA)) / (1 - (muA / muB)));
            double b_B = (muA / muB) * b_A;

            Func<double[], double, double> u_A = (X, t) => a_A(t) + b_A * X[1];
            Func<double[], double, double> u_B = (X, t) => a_B(t) + b_B * X[1];
            Func<double[], double, double> v_0 = (X, t) => v0;

            Func<double[], double, double> psi_0 = (X, t) => psi0 * X[0] + (xSize * psi0 + 1);

            //Func<double, double> phi = t => t * v0;

            C.ExactSolutionVelocity = new Dictionary<string, Func<double[], double, double>[]>();
            C.ExactSolutionVelocity.Add("A", new Func<double[], double, double>[] { u_A, v_0 });
            C.ExactSolutionVelocity.Add("B", new Func<double[], double, double>[] { u_B, v_0 });

            C.ExactSolutionPressure = new Dictionary<string, Func<double[], double, double>>();
            C.ExactSolutionPressure.Add("A", psi_0);
            C.ExactSolutionPressure.Add("B", psi_0);

            //double[] X_test = new double[] {0,1};
            //double u_B_test = u_B(X_test,0);
            //Console.WriteLine("u_B = {0}", u_B_test);
            //u_B_test = u_B(X_test, 1);
            //Console.WriteLine("u_B = {0}", u_B_test);

            #endregion

            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("Velocity_inlet_lower", "VelocityX#A", u_A);
            C.AddBoundaryValue("Velocity_inlet_lower", "VelocityX#B", (X, t) => double.NaN);
            C.AddBoundaryValue("Velocity_inlet_lower", "VelocityY#A", v_0);
            C.AddBoundaryValue("Velocity_inlet_lower", "VelocityY#B", (X, t) => double.NaN);


            C.AddBoundaryValue("Velocity_inlet_upper", "VelocityX#A", (X, t) => double.NaN);
            C.AddBoundaryValue("Velocity_inlet_upper", "VelocityX#B", u_B);
            C.AddBoundaryValue("Velocity_inlet_upper", "VelocityY#A", (X, t) => double.NaN);
            C.AddBoundaryValue("Velocity_inlet_upper", "VelocityY#B", v_0);


            if (!xPeriodic) {
                C.AddBoundaryValue("Pressure_Dirichlet_left", "Pressure#A", psi_0);
                C.AddBoundaryValue("Pressure_Dirichlet_left", "Pressure#B", psi_0);
                C.AddBoundaryValue("Pressure_Dirichlet_right", "Pressure#A", psi_0);
                C.AddBoundaryValue("Pressure_Dirichlet_right", "Pressure#B", psi_0);
            }


            #endregion

            // initial values
            // ==================
            #region init

            C.InitialValues_Evaluators.Add("Phi",
                (X => X[1])
                );
            C.InitialValues_Evaluators.Add("VelocityX#A", X => u_A(X, 0));
            C.InitialValues_Evaluators.Add("VelocityX#B", X => u_B(X, 0));
            C.InitialValues_Evaluators.Add("VelocityY#A", X => v_0(X, 0));
            C.InitialValues_Evaluators.Add("VelocityY#B", X => v_0(X, 0));


            C.InitialValues_Evaluators.Add("Pressure#A", X => psi_0(X, 0));
            C.InitialValues_Evaluators.Add("Pressure#B", X => psi_0(X, 0));


            #endregion

            // misc. solver options
            // ====================
            #region solver

            C.AgglomerationThreshold = 0.2;
            C.AdvancedDiscretizationOptions.ViscosityMode = Solution.XNSECommon.ViscosityMode.FullySymmetric;

            C.Option_LevelSetEvolution = LevelSetEvolution.None;


            C.LinearSolver.NoOfMultigridLevels = 1;

            
            #endregion

            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            double dt = 0.1;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 10;

            #endregion

            return C;
        }

        /*
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// Maintainer: Florian 
        /// </remarks>
        public static XNSE_Control OscillatingDroplet(int p = 2, int kelem = 54) {

            XNSE_Control C = new XNSE_Control();

            // basic database options
            // ======================
            #region db

            C.DbPath = @"\\fdyprime\userspace\kummer\BoSSS-db-XNSE";
            C.savetodb = false;
            C.ProjectName = "XNSE/Droplet";
            C.ProjectDescription = "Oscillating Droplet";

            #endregion


            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new AppControl.BaseConfig.FieldOpts() {
                Degree = p,
                SaveToDB = AppControl.BaseConfig.FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new AppControl.BaseConfig.FieldOpts() {
                Degree = p,
                SaveToDB = AppControl.BaseConfig.FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new AppControl.BaseConfig.FieldOpts() {
                Degree = p - 1,
                SaveToDB = AppControl.BaseConfig.FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new AppControl.BaseConfig.FieldOpts() {
                SaveToDB = AppControl.BaseConfig.FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new AppControl.BaseConfig.FieldOpts() {
                Degree = 4,
                SaveToDB = AppControl.BaseConfig.FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new AppControl.BaseConfig.FieldOpts() {
                Degree = 8,
                SaveToDB = AppControl.BaseConfig.FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // grid genration
            // ==============
            #region grid

            double sizeFactor = 1.0;
                        double xSize = sizeFactor * 4.5;
            double ySize = sizeFactor * 4.5;

            
            int xkelem = kelem * 1;
            int ykelem = kelem * 1;

            double hMin = Math.Min(2 * xSize / (xkelem), 2 * ySize / (ykelem));


            C.GridFunc = delegate() {
                double[] Xnodes = GenericBlas.Linspace(-xSize, xSize, xkelem + 1);
                double[] Ynodes = GenericBlas.Linspace(-ySize, ySize, ykelem + 1);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes);

                grd.EdgeTagNames.Add(1, "wall_lower");
                grd.EdgeTagNames.Add(2, "wall_upper");
                grd.EdgeTagNames.Add(3, "wall_left");
                grd.EdgeTagNames.Add(4, "wall_right");
                
                grd.DefineEdgeTags(delegate(double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + ySize) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - ySize) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0] + xSize) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - xSize) <= 1.0e-8)
                        et = 4;
                    return et;
                });

                return grd;
            };

            #endregion


            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("wall_lower", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_lower", "VelocityX#B", (X, t) => 0.0);
            C.AddBoundaryValue("wall_upper", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_upper", "VelocityX#B", (X, t) => 0.0);
            C.AddBoundaryValue("wall_left", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_left", "VelocityX#B", (X, t) => 0.0);
            C.AddBoundaryValue("wall_right", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_right", "VelocityX#B", (X, t) => 0.0);
 
            #endregion


            // Initial Values
            // ==============
            #region init

            double baseSize = 1.0;
            double elipsDelta = 0.1;
            double radius = 0.835;

            C.InitialValues_Evaluators.Add("Phi",
                //(X => (X[0].Pow2() / (0.8 * 0.8) * 1.25 + X[1].Pow2() / (0.8 * 0.8) * 0.8) - 1.0 + 0.2 * Math.Sin(10 * X[0] * X[1])) // Kartoffel
                (X => (X[0] / (radius * baseSize * (1.0 + elipsDelta))).Pow2() + (X[1] / (radius * baseSize * (1.0 - elipsDelta))).Pow2() - 1.0)   // quadratic form
                //(X => ((X[0] / (radius * baseSize * (1.0 + elipsDelta))).Pow2() + (X[1] / (radius * baseSize * (1.0 - elipsDelta))).Pow2()).Sqrt() - 1.0)  // signed-distance form
                //(X => ((X[0] / (0.8 * BaseSize)).Abs().Pow(1.2) + (X[1] / (0.8 * BaseSize)).Abs().Pow(1.2)) - 1.0.Abs().Pow(1.2))
                );
            C.InitialValues_Evaluators.Add("VelocityX#A", X => 0.0);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => 0.0);

            #endregion


            // Physical Parameters
            // ===================
            #region physics

            // Air-Water (lenght scale == centimeters, 3D space)
            C.PhysicalParameters.rho_A = 1e-3;      // kg / cm^3
            C.PhysicalParameters.rho_B = 1.2e-6;    // kg / cm^3
            C.PhysicalParameters.mu_A = 1e-5;       // kg / cm * sec
            C.PhysicalParameters.mu_B = 17.1e-8;    // kg / cm * sec
            C.PhysicalParameters.Sigma = 72.75e-3;  // kg / sec^2     
            

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            #endregion


            // misc. solver options
            // ====================
            #region solver

            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.1;
            C.AdvancedDiscretizationOptions.ViscosityImplementation = ViscosityImplementation.H;
            C.AdvancedDiscretizationOptions.ViscosityMode = Solution.XNSECommon.ViscosityMode.FullySymmetric;
            C.AdvancedDiscretizationOptions.UseGhostPenalties = false;
            C.EnforceLevelSetContinuity  = true;
            //C.Option_LevelSetEvolution = LevelSetEvolution.Outer_Loop;
            C.Option_LevelSetEvolution = LevelSetEvolution.Inner_Loop;
            C.option_solver = "direct";
            C.VelocityBlockPrecondMode = MultigridOperator.Mode.SymPart_DiagBlockEquilib;
            C.NoOfMultigridLevels = 1;
            C.Solver_MaxIterations = 300;
            C.ConvergenceCriterion = 1.0e-6;
            C.LevelSet_ConvergenceCriterion = 1.0e-6;

            // extension-velocity -- level-set
            //C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.Default;
            //C.AdvancedDiscretizationOptions.surfTensionMode = Solution.XNSECommon.SurfaceTensionMode.Curvature_ClosestPoint;
            //C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 0;

            // Fourier -- level-set
            C.FourierLevSetControl = new FourierLevSetControl(
                FourierType.Cylindrical,
                1024,
                2 * Math.PI,
                hMin,
                alpha => radius + Math.Cos(alpha)*radius*elipsDelta,
                Interpolationtype.LinearSplineInterpolation,
                0.5);

            C.ComputeEnergy = true;

            #endregion


            // Timestepping
            // ============
            #region time

            C.CompMode = AppControl._CompMode.Transient;
            double dt = 2e-4;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1e12;
            C.NoOfTimesteps = 3;

            #endregion

            return C;
        }
        */

        public static XNSE_Control SloshingTank2(string _DbPath = @"\\fdyprime\userspace\smuda\Databases\test_db", int p = 2) {

            XNSE_Control C = new XNSE_Control();

            _DbPath = @"D:\local\local_test_db";

            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = C.DbPath != null;
            C.ProjectName = "XNSE/Tank";
            C.ProjectDescription = "Sloshing Tank";
            C.Tags.Add("freeslip boundary condition");

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("FilteredVelocityX", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("FilteredVelocityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = 4,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = 8,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });


            #endregion

            // grid genration
            // ==============
            #region grid

            double L = 1;
            double H = 0.5 * L;

            bool xPeriodic = false;

            int xkelem = 16;
            int ykelem_Interface = 10 * xkelem + 1;
            int ykelem_outer = 2 * xkelem;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-L / 2, L / 2, xkelem + 1);
                double[] Ynodes_Interface = GenericBlas.Linspace(-L, L, ykelem_Interface + 1);
                Ynodes_Interface = Ynodes_Interface.GetSubVector(1, Ynodes_Interface.GetLength(0) - 2);
                double[] Ynodes_lower = GenericBlas.Linspace(-(H + L), -L, ykelem_outer + 1);
                double[] Ynodes_upper = GenericBlas.Linspace(L, (H + L), ykelem_outer + 1);
                double[] Ynodes = Ynodes_lower.Concat(Ynodes_Interface).ToArray().Concat(Ynodes_upper).ToArray();

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: xPeriodic);

                grd.EdgeTagNames.Add(1, "wall_lower");
                grd.EdgeTagNames.Add(2, "wall_upper");

                if (!xPeriodic) {
                    grd.EdgeTagNames.Add(3, "freeslip_left");
                    grd.EdgeTagNames.Add(4, "freeslip_right");
                    //grd.EdgeTagNames.Add(3, "wall_left");
                    //grd.EdgeTagNames.Add(4, "wall_right");
                }

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + (H + L)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - (H + L)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0] + L / 2) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - L / 2) <= 1.0e-8)
                        et = 4;

                    return et;
                });

                return grd;
            };

            #endregion

            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("wall_lower");
            C.AddBoundaryValue("wall_upper");
            if (!xPeriodic) {
                C.AddBoundaryValue("freeslip_left");
                C.AddBoundaryValue("freeslip_right");
                //C.AddBoundaryCondition("wall_left");
                //C.AddBoundaryCondition("wall_right");
            }


            #endregion

            // Initial Values
            // ==============
            #region init

            //double sigma = 0.1;
            //Func<double, double> gaussbump = x => ((1 / Math.Sqrt(2 * Math.PI * sigma.Pow2())) * Math.Exp(-x.Pow2() / (2 * sigma.Pow2())));
            Func<double, double> wave = x => 0.01 * Math.Cos(x * 2 * Math.PI);

            C.InitialValues_Evaluators.Add("Phi",
                (X => (X[1] - wave(X[0]))));

            C.InitialValues_Evaluators.Add("VelocityY#A", X => 0.0);
            C.InitialValues_Evaluators.Add("VelocityY#B", X => 0.0);

            C.InitialValues_Evaluators.Add("GravityY#A", X => -9.81e2);
            C.InitialValues_Evaluators.Add("GravityY#B", X => -9.81e2);


            //var database = new DatabaseInfo(_DbPath);
            //var sessTank = database.Sessions.Where(s => s.Name.ToLower().Contains("tank"));
            //var latestSession = sessTank.OrderByDescending(e => e.CreationTime).First();
            //C.RestartInfo = new Tuple<Guid, Foundation.IO.TimestepNumber>(latestSession.ID, null);


            #endregion

            // Physical Parameters
            // ===================
            #region physics


            // Air-Water (length scale: meters)
            //C.PhysicalParameters.rho_A = 1000;      // kg / m^3
            //C.PhysicalParameters.rho_B = 1.2;       // kg / m^3
            //C.PhysicalParameters.mu_A = 1.0e-3;     // kg / m * s
            //C.PhysicalParameters.mu_B = 17.1e-6;    // kg / m * s
            //C.PhysicalParameters.Sigma = 72.75e-3;  // kg / s^2     


            // Air-Water (length scale: centimeters)
            C.PhysicalParameters.rho_A = 1e-3;      // kg / cm^3
            C.PhysicalParameters.rho_B = 1.2e-6;    // kg / cm^3
            C.PhysicalParameters.mu_A = 1e-5;       // kg / cm * s
            C.PhysicalParameters.mu_B = 17.1e-8;    // kg / cm * s
            C.PhysicalParameters.Sigma = 72.75e-3;  // kg / s^2     


            // 
            //C.PhysicalParameters.rho_A = 1;
            //C.PhysicalParameters.rho_B = 0.1;
            //C.PhysicalParameters.mu_A = 1e-3;
            //C.PhysicalParameters.mu_B = 1e-4;

            C.PhysicalParameters.Sigma = 0.0;   // free surface boundary condition

            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion

            // misc. solver options
            // ====================
            #region solver

            C.AgglomerationThreshold = 0.1;
            C.AdvancedDiscretizationOptions.ViscosityMode = Solution.XNSECommon.ViscosityMode.FullySymmetric;
            C.LinearSolver.MaxSolverIterations = 100;
            C.NonLinearSolver.MaxSolverIterations = 100;
            C.LinearSolver.NoOfMultigridLevels = 1;

            C.ComputeEnergyProperties = false;

            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.Default;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = Solution.XNSECommon.SurfaceStressTensor_IsotropicMode.Curvature_Projected;
            C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 0;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            double dt = 1e-3;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 1000;

            #endregion


            return C;
        }


        public static XNSE_Control ForcedSloshingTank(string _DbPath = null, int p = 2) {

            XNSE_Control C = new XNSE_Control();

            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = _DbPath != null;
            C.ProjectName = "XNSE/Tank";
            C.ProjectDescription = "Forced Sloshing Tank";

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = 4,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = 8,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion

            // grid genration
            // ==============
            #region grid

            double xSize = 100;
            double ySize = 120;

            int xkelem = 20;
            int ykelem = 25;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-xSize / 2, xSize / 2, xkelem + 1);
                double[] Ynodes = GenericBlas.Linspace(0, ySize, ykelem + 1);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes);


                // oscillation induced by wall motion
                //grd.EdgeTagNames.Add(1, "velocity_inlet_lower");
                //grd.EdgeTagNames.Add(2, "velocity_inlet_upper");
                //grd.EdgeTagNames.Add(3, "velocity_inlet_left");
                //grd.EdgeTagNames.Add(4, "velocity_inlet_right");

                // oscillation induced by body force
                grd.EdgeTagNames.Add(1, "wall_lower");
                grd.EdgeTagNames.Add(2, "wall_upper");
                grd.EdgeTagNames.Add(3, "wall_left");
                grd.EdgeTagNames.Add(4, "wall_right");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1]) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - ySize) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0] + xSize / 2) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - xSize / 2) <= 1.0e-8)
                        et = 4;

                    return et;
                });

                return grd;
            };

            #endregion

            // boundary conditions
            // ===================
            #region BC


            //double A = 0.93;
            //double omega = 1 / 1.183;

            // oscillation induced by wall motion
            //C.AddBoundaryCondition("velocity_inlet_lower", "VelocityX#A", (X, t) => A * omega * Math.Sin(omega * t));
            //C.AddBoundaryCondition("velocity_inlet_lower", "VelocityX#B", (X, t) => A * omega * Math.Sin(omega * t));
            //C.AddBoundaryCondition("velocity_inlet_upper", "VelocityX#A", (X, t) => A * omega * Math.Sin(omega * t));
            //C.AddBoundaryCondition("velocity_inlet_upper", "VelocityX#B", (X, t) => A * omega * Math.Sin(omega * t));

            //C.AddBoundaryCondition("velocity_inlet_left", "VelocityX#A", (X, t) => A * omega * Math.Sin(omega * t));
            //C.AddBoundaryCondition("velocity_inlet_left", "VelocityX#B", (X, t) => A * omega * Math.Sin(omega * t));
            //C.AddBoundaryCondition("velocity_inlet_right", "VelocityX#A", (X, t) => A * omega * Math.Sin(omega * t));
            //C.AddBoundaryCondition("velocity_inlet_right", "VelocityX#B", (X, t) => A * omega * Math.Sin(omega * t));


            // oscillation induced by body force
            C.AddBoundaryValue("wall_lower", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_upper", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_lower", "VelocityX#B", (X, t) => 0.0);
            C.AddBoundaryValue("wall_upper", "VelocityX#B", (X, t) => 0.0);

            C.AddBoundaryValue("wall_left", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_right", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_left", "VelocityX#B", (X, t) => 0.0);
            C.AddBoundaryValue("wall_right", "VelocityX#B", (X, t) => 0.0);


            #endregion

            // Initial Values
            // ==============
            #region init

            C.InitialValues_Evaluators.Add("Phi",
                (X => (X[1] - 50))
                );

            C.InitialValues_Evaluators.Add("VelocityX#A", X => 0.0);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => 0.0);

            C.InitialValues_Evaluators.Add("GravityY#A", X => -9.81e2);
            C.InitialValues_Evaluators.Add("GravityY#B", X => -9.81e2);


            #endregion

            // Physical Parameters
            // ===================
            #region physics

            // Air-Water (length scale: centimeters)
            C.PhysicalParameters.rho_A = 1e-3;      // kg / cm^3
            C.PhysicalParameters.rho_B = 1.2e-6;    // kg / cm^3
            C.PhysicalParameters.mu_A = 1e-5;       // kg / cm * s
            C.PhysicalParameters.mu_B = 17.1e-8;    // kg / cm * s
            C.PhysicalParameters.Sigma = 72.75e-3;  // kg / s^2   

            C.PhysicalParameters.Sigma = 0.0;       // free surface condition

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            #endregion

            // misc. solver options
            // ====================
            #region solver

            C.AgglomerationThreshold = 0.1;
            C.AdvancedDiscretizationOptions.ViscosityMode = Solution.XNSECommon.ViscosityMode.FullySymmetric;
            C.LinearSolver.NoOfMultigridLevels = 1;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            double dt = 1e-4;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 10;

            #endregion


            return C;
        }


        public static XNSE_Control Test_PlanarFourierLS(string _DbPath = @"\\fdyprime\userspace\smuda\Databases\test_db", int p = 2) {

            XNSE_Control C = new XNSE_Control();

            // basic database options
            // ======================
            #region db

            C.DbPath = null; // _DbPath;
            C.savetodb = C.DbPath != null;
            C.ProjectName = "XNSE/PlanarFourierLS_test";

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // Physical Parameters
            // ===================
            #region physics  

            C.PhysicalParameters.rho_A = 1.0;
            C.PhysicalParameters.rho_B = 1.0;
            C.PhysicalParameters.mu_A = 1.0;
            C.PhysicalParameters.mu_B = 1.0;
            C.PhysicalParameters.Sigma = 0.0;


            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion


            // grid generation
            // ==============
            #region grid

            double lambda = 1;

            double L = lambda;
            double H = 2 * L;

            int xkelem = 9;
            int ykelem_Interface = 10 * xkelem + 1;
            int ykelem_outer = 1 * xkelem;

            double grdSize = L / (double)xkelem;

            C.GridFunc = delegate () {
                //double[] Xnodes = GenericBlas.Linspace(0, L, xkelem + 1);
                //double[] Ynodes = GenericBlas.Linspace(-L / 2, L / 2, xkelem);
                //var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: true);

                //grd.EdgeTagNames.Add(1, "velocity_inlet_lower");
                //grd.EdgeTagNames.Add(2, "velocity_inlet_upper");
                //grd.EdgeTagNames.Add(3, "velocity_inlet_left");
                //grd.EdgeTagNames.Add(4, "velocity_inlet_right");

                //grd.DefineEdgeTags(delegate (double[] X) {
                //    byte et = 0;
                //    if (Math.Abs(X[1] + (L/2)) <= 1.0e-8)
                //        et = 1;
                //    if (Math.Abs(X[1] - (L/2)) <= 1.0e-8)
                //        et = 2;
                //    if (Math.Abs(X[0]) <= 1.0e-8)
                //        et = 3;
                //    if (Math.Abs(X[0] - L) <= 1.0e-8)
                //        et = 4;

                //    return et;
                //});

                double[] Xnodes = GenericBlas.Linspace(0, 6, 2 * xkelem + 1);
                double[] Ynodes = GenericBlas.Linspace(0, 3, xkelem + 1);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes);

                grd.EdgeTagNames.Add(1, "velocity_inlet_lower");
                grd.EdgeTagNames.Add(2, "velocity_inlet_upper");
                grd.EdgeTagNames.Add(3, "velocity_inlet_left");
                grd.EdgeTagNames.Add(4, "velocity_inlet_right");

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + 0) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - 3) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0]) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - 6) <= 1.0e-8)
                        et = 4;

                    return et;
                });

                return grd;
            };


            #endregion


            // boundary conditions
            // ===================
            #region BC

            double Yvel = 1.0;

            C.AddBoundaryValue("velocity_inlet_lower", "VelocityY#A", X => Yvel);
            C.AddBoundaryValue("velocity_inlet_lower", "VelocityY#B", X => Yvel);
            C.AddBoundaryValue("velocity_inlet_upper", "VelocityY#A", X => Yvel);
            C.AddBoundaryValue("velocity_inlet_upper", "VelocityY#B", X => Yvel);

            C.AddBoundaryValue("velocity_inlet_left", "VelocityY#A", X => Yvel);
            C.AddBoundaryValue("velocity_inlet_left", "VelocityY#B", X => Yvel);
            C.AddBoundaryValue("velocity_inlet_right", "VelocityY#A", X => Yvel);
            C.AddBoundaryValue("velocity_inlet_right", "VelocityY#B", X => Yvel);

            //double yVel_max = 1.0;
            //Func<double[], double, double> yVel_seesaw = (X, t) => yVel_max * Math.Sin(2*Math.PI*t);

            //C.AddBoundaryCondition("velocity_inlet_lower", "VelocityY#A", (X,t) => yVel_seesaw(X, -t));
            //C.AddBoundaryCondition("velocity_inlet_upper", "VelocityY#B", yVel_seesaw);

            #endregion


            // Initial Values
            // ==============
            #region init

            double h0 = 0.0;

            double A0 = 0.1; // lambda / 10;
            Func<double, double> PeriodicFunc = x => h0 + A0 * Math.Sin(x * 2 * Math.PI / lambda);

            C.InitialValues_Evaluators.Add("Phi", (X => X[1] - 1)); // Math.Sin(X[0]) + Math.Cos(X[0]) + X[0] - (X[1] + 1))); 

            C.InitialValues_Evaluators.Add("VelocityY#A", X => Yvel);
            C.InitialValues_Evaluators.Add("VelocityY#B", X => Yvel);


            //var database = new DatabaseInfo(_DbPath);
            //var sessProjName = database.Sessions.Where(s => s.Name.ToLower().Contains("test"));
            //var latestSession = sessProjName.OrderByDescending(e => e.CreationTime).First();
            //C.RestartInfo = new Tuple<Guid, Foundation.IO.TimestepNumber>(latestSession.ID, null);

            #endregion


            // exact solution
            // ==============

            //C.Phi = (X, t) => (X[1] - (h0 + Yvel * t));

            //C.ExSol_Velocity = new Dictionary<string, Func<double[], double, double>[]>();
            //C.ExSol_Velocity.Add("A", new Func<double[], double, double>[] { (X, t) => 0.0, (X, t) => 1.0 });
            //C.ExSol_Velocity.Add("B", new Func<double[], double, double>[] { (X, t) => 0.0, (X, t) => 1.0 });

            //C.ExSol_Pressure = new Dictionary<string, Func<double[], double, double>>();
            //C.ExSol_Pressure.Add("A", (X, t) => 0.0);
            //C.ExSol_Pressure.Add("B", (X, t) => 0.0);


            // Fourier Level-Set
            // =================
            #region Fourier

            //int numSp = 64;
            //double[] FourierP = new double[numSp];
            //double[] samplP = new double[numSp];
            //for (int sp = 0; sp < numSp; sp++) {
            //    FourierP[sp] = sp * (L / (double)numSp);
            //    samplP[sp] = PeriodicFunc(FourierP[sp]);
            //}

            //C.FourierLevSetControl = new FourierLevSetControl(FourierType.Planar, L, FourierP, samplP, 1.0 / (double)xkelem) {
            //    FourierEvolve = Fourier_Evolution.MaterialPoints,
            //};


            #endregion

            // misc. solver options
            // ====================
            #region solver

            C.ComputeEnergyProperties = false;

            //C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.3;
            //C.AdvancedDiscretizationOptions.PenaltySafety = 40;

            C.LSContiProjectionMethod = ContinuityProjectionOption.ConstrainedDG;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 100;
            C.NonLinearSolver.MaxSolverIterations = 100;
            //C.Solver_MaxIterations = 100;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            //C.Solver_ConvergenceCriterion = 1e-8;
            C.LevelSet_ConvergenceCriterion = 1e-8;

            //C.Option_LevelSetEvolution = LevelSetEvolution.Fourier;
            //C.AdvancedDiscretizationOptions.surfTensionMode = SurfaceTensionMode.Curvature_Fourier;

            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.Default;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = Solution.XNSECommon.SurfaceStressTensor_IsotropicMode.Curvature_Projected;
            C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 1;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.Timestepper_BDFinit = TimeStepperInit.SingleInit;
            C.Timestepper_LevelSetHandling = LevelSetHandling.Coupled_Iterative;
            C.LSunderrelax = 0.7;

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            double dt = 1e-2;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 50;

            #endregion


            return C;
        }


        public static XNSE_Control Test_PolarFourierLS(string _DbPath = null, int p = 2) {

            XNSE_Control C = new XNSE_Control();

            // basic database options
            // ======================
            #region db

            C.DbPath = null;  //@"D:\local\local_test_db";
            C.savetodb = C.DbPath != null;
            C.ProjectName = "XNSE/PolarFourierLS_test";

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // Physical Parameters
            // ===================
            #region physics  

            C.PhysicalParameters.rho_A = 1.0;
            C.PhysicalParameters.rho_B = 1.0;
            C.PhysicalParameters.mu_A = 1.0;
            C.PhysicalParameters.mu_B = 1.0;
            C.PhysicalParameters.Sigma = 0.0;

            //C.Tags.Add("Bubble");
            //C.PhysicalParameters.rho_A = 100;
            //C.PhysicalParameters.rho_B = 1000;
            //C.PhysicalParameters.mu_A = 1;
            //C.PhysicalParameters.mu_B = 10;
            //C.PhysicalParameters.Sigma = 24.5;

            //C.Tags.Add("Droplet");
            //C.PhysicalParameters.rho_A = 1000;
            //C.PhysicalParameters.rho_B = 100;
            //C.PhysicalParameters.mu_A = 10;
            //C.PhysicalParameters.mu_B = 1;
            //C.PhysicalParameters.Sigma = 0;

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            #endregion


            // grid generation
            // ==============
            #region grid

            double L = 1.0;
            double ch_fac = 3;
            int k = 20;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-L, ch_fac * L, ((int)ch_fac + 1) * k + 1);
                double[] Ynodes = GenericBlas.Linspace(-L, L, 2 * k + 1);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: true);

                //grd.EdgeTagNames.Add(1, "velocity_inlet_lower");
                //grd.EdgeTagNames.Add(2, "velocity_inlet_upper");
                //grd.EdgeTagNames.Add(3, "velocity_inlet_left");
                //grd.EdgeTagNames.Add(4, "velocity_inlet_right");
                grd.EdgeTagNames.Add(1, "freeslip_lower");
                grd.EdgeTagNames.Add(2, "freeslip_upper");
                grd.EdgeTagNames.Add(3, "velocity_inlet_left");
                grd.EdgeTagNames.Add(4, "pressure_outlet_right");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + (L)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - (L)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0] + (L)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - (ch_fac * L)) <= 1.0e-8)
                        et = 4;

                    return et;
                });

                return grd;
            };


            #endregion


            // boundary conditions
            // ===================
            #region BC

            double Xvel = 1.0;

            //C.AddBoundaryCondition("velocity_inlet_lower", "VelocityX#A", X => Xvel);
            //C.AddBoundaryCondition("velocity_inlet_lower", "VelocityX#B", X => Xvel);
            //C.AddBoundaryCondition("velocity_inlet_upper", "VelocityX#A", X => Xvel);
            //C.AddBoundaryCondition("velocity_inlet_upper", "VelocityX#B", X => Xvel);

            C.AddBoundaryValue("freeslip_lower");
            C.AddBoundaryValue("freeslip_upper");

            //C.AddBoundaryCondition("velocity_inlet_left", "VelocityX#A", X => Xvel);
            //C.AddBoundaryCondition("velocity_inlet_left", "VelocityX#B", X => Xvel);
            //C.AddBoundaryCondition("velocity_inlet_right", "VelocityX#A", X => Xvel);
            //C.AddBoundaryCondition("velocity_inlet_right", "VelocityX#B", X => Xvel);

            C.AddBoundaryValue("pressure_outlet_right");
            C.AddBoundaryValue("velocity_inlet_left", "VelocityX#A", X => Xvel);


            #endregion


            // Initial Values
            // ==============
            #region init

            double[] center = new double[] { 0.0, 0.0 };
            //double a = 0.3;
            //double b = 0.15;
            //Func<double, double> radius = phi => a * b / Math.Sqrt(a.Pow2() * Math.Sin(phi).Pow2() + b.Pow2() * Math.Cos(phi).Pow2());
            double radius = 0.25;


            C.InitialValues_Evaluators.Add("Phi",
                //(X => (X[0] - center[0]).Pow2() + (X[1] - center[1]).Pow2() - radius.Pow2())   // quadratic form
                (X => ((X[0] - center[0]).Pow2() + (X[1] - center[1]).Pow2()).Sqrt() - radius)  // signed-distance form
                                                                                                //(X => (X[0].Pow2() / a.Pow2() + X[1].Pow2() / b.Pow2()) - 1)                    // ellipsoid
                );

            C.InitialValues_Evaluators.Add("VelocityX#A", X => Xvel);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => Xvel);

            C.InitialValues_Evaluators.Add("Pressure#A", X => 0.0);
            C.InitialValues_Evaluators.Add("Pressure#B", X => 0.0);

            //C.InitialValues_Evaluators.Add("GravityX#A", X => 3); //0.1
            //C.InitialValues_Evaluators.Add("GravityX#B", X => 3);


            //var database = new DatabaseInfo(_DbPath);
            //Guid restartID = new Guid("7b94348f-5133-48aa-88c0-be625a70ff92");
            //C.RestartInfo = new Tuple<Guid, Foundation.IO.TimestepNumber>(restartID, null);

            #endregion


            // exact solution
            // ==============


            C.Phi = ((X, t) => ((X[0] - (center[0] + Xvel * t)).Pow2() + (X[1] - (center[1])).Pow2()).Sqrt() - radius);

            C.ExactSolutionVelocity = new Dictionary<string, Func<double[], double, double>[]>();
            C.ExactSolutionVelocity.Add("A", new Func<double[], double, double>[] { (X, t) => Xvel, (X, t) => 0.0 });
            C.ExactSolutionVelocity.Add("B", new Func<double[], double, double>[] { (X, t) => Xvel, (X, t) => 0.0 });

            C.ExactSolutionPressure = new Dictionary<string, Func<double[], double, double>>();
            C.ExactSolutionPressure.Add("A", (X, t) => 0.0);
            C.ExactSolutionPressure.Add("B", (X, t) => 0.0);


            // Fourier Level-Set
            // =================
            #region Fourier

            int numSp = 640;
            double[] FourierP = new double[numSp];
            double[] samplP = new double[numSp];
            for (int sp = 0; sp < numSp; sp++) {
                FourierP[sp] = sp * (2 * Math.PI / (double)numSp);
                samplP[sp] = radius;
            }

            //C.FourierLevSetControl = new FourierLevSetControl(FourierType.Polar, 2 * Math.PI, FourierP, samplP, 1.0 / (double)k) {
            //    center = center,
            //    FourierEvolve = Fourier_Evolution.MaterialPoints,
            //    centerMove = CenterMovement.Reconstructed,
            //};


            #endregion

            // misc. solver options
            // ====================
            #region solver

            //C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            //C.AdvancedDiscretizationOptions.PenaltySafety = 40;

            C.LSContiProjectionMethod = ContinuityProjectionOption.SpecFEM;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 100;
            C.NonLinearSolver.MaxSolverIterations = 100;
            //C.Solver_MaxIterations = 100;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            //C.Solver_ConvergenceCriterion = 1e-8;
            C.LevelSet_ConvergenceCriterion = 1e-6;

            //C.Option_LevelSetEvolution = LevelSetEvolution.Fourier;
            //C.AdvancedDiscretizationOptions.surfTensionMode = SurfaceTensionMode.Curvature_Fourier;
            //C.FourierLevSetControl.Timestepper = FourierLevelSet_Timestepper.ExplicitEuler;

            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.Default;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = Solution.XNSECommon.SurfaceStressTensor_IsotropicMode.Curvature_Projected;
            C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 1;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.Timestepper_BDFinit = TimeStepperInit.SingleInit;
            //C.dt_increment = 4;
            C.Timestepper_LevelSetHandling = LevelSetHandling.LieSplitting;
 
            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;

            double dt = 1e-2;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 10;

            #endregion


            return C;

        }


        public static XNSE_Control Test_ChannelFlow(int degree = 2) {

            XNSE_Control C = new XNSE_Control();


            // basic database options
            // ======================
            #region db
            C.DbPath = null;
            C.savetodb = false;

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = degree,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = degree,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = degree - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = degree + 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = degree + 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion

            // grid genration
            // ==============
            #region grid

            double L = 2;
            double H = 1;
            bool xPeriodic = false;

            int wall_bc = 3; // 1 = wall, 2 = freeslip, 3 = velocity_inlet, 4 = mixed

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(0, L, 22);
                double[] Ynodes = GenericBlas.Linspace(0, H, 15);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: xPeriodic);
                switch (wall_bc) {
                    case 1: {
                            grd.EdgeTagNames.Add(1, "wall_lower");
                            grd.EdgeTagNames.Add(2, "wall_upper");
                            break;
                        }
                    case 2: {
                            grd.EdgeTagNames.Add(1, "freeslip_lower");
                            grd.EdgeTagNames.Add(2, "freeslip_upper");
                            break;
                        }
                    case 3: {
                            grd.EdgeTagNames.Add(1, "Velocity_inlet_lower");
                            grd.EdgeTagNames.Add(2, "Velocity_inlet_upper");
                            break;
                        }
                    case 4:
                        {
                            grd.EdgeTagNames.Add(1, "freeslip_lower");
                            grd.EdgeTagNames.Add(2, "Velocity_inlet_upper");
                            //grd.EdgeTagNames.Add(1, "Velocity_inlet_lower");
                            //grd.EdgeTagNames.Add(2, "freeslip_upper");
                            break;
                        }
                }

                if (!xPeriodic) {
                    grd.EdgeTagNames.Add(3, "Velocity_inlet_left");
                    grd.EdgeTagNames.Add(4, "pressure_outlet_right");
                }

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1]) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - H) <= 1.0e-8)
                        et = 2;
                    if (!xPeriodic) {
                        if (Math.Abs(X[0]) <= 1.0e-8)
                            et = 3;
                        if (Math.Abs(X[0] - L) <= 1.0e-8)
                            et = 4;
                    }

                    return et;
                });

                return grd;
            };

            #endregion

            // physical parameters
            // ===================
            #region physics

            const double rhoA = 1;
            const double rhoB = 1;
            const double muA = 1;
            const double muB = 1;
            const double sigma = 1;

            C.PhysicalParameters.rho_A = rhoA;
            C.PhysicalParameters.rho_B = rhoB;
            C.PhysicalParameters.mu_A = muA;
            C.PhysicalParameters.mu_B = muB;
            C.PhysicalParameters.Sigma = sigma;

            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion


            // boundary conditions
            // ===================
            #region BC

            double velX = 1.0;

            switch (wall_bc) {
                case 1:
                    {
                        C.AddBoundaryValue("wall_lower");
                        C.AddBoundaryValue("wall_upper");
                        break;
                    }
                case 2:
                    {
                        C.AddBoundaryValue("freeslip_lower");
                        C.AddBoundaryValue("freeslip_upper");
                        break;
                    }
                case 3:
                    {
                        C.AddBoundaryValue("Velocity_inlet_lower", "VelocityX#A", (X, t) => velX);
                        C.AddBoundaryValue("Velocity_inlet_lower", "VelocityX#B", (X, t) => velX);
                        C.AddBoundaryValue("Velocity_inlet_upper", "VelocityX#A", (X, t) => velX);
                        C.AddBoundaryValue("Velocity_inlet_upper", "VelocityX#B", (X, t) => velX);
                        //C.AddBoundaryCondition("Velocity_inlet_lower", "VelocityX#A", (X, t) => 0.0);
                        //C.AddBoundaryCondition("Velocity_inlet_lower", "VelocityX#B", (X, t) => 0.0);
                        //C.AddBoundaryCondition("Velocity_inlet_upper", "VelocityX#A", (X, t) => 0.0);
                        //C.AddBoundaryCondition("Velocity_inlet_upper", "VelocityX#B", (X, t) => 0.0);
                        break;
                    }
                case 4:
                    {
                        C.AddBoundaryValue("freeslip_lower");
                        C.AddBoundaryValue("Velocity_inlet_upper", "VelocityX#A", (X, t) => velX);
                        C.AddBoundaryValue("Velocity_inlet_upper", "VelocityX#B", (X, t) => velX);
                        //C.AddBoundaryCondition("Velocity_inlet_lower", "VelocityX#A", (X, t) => velX);
                        //C.AddBoundaryCondition("Velocity_inlet_lower", "VelocityX#B", (X, t) => velX);
                        //C.AddBoundaryCondition("freeslip_upper");
                        break;
                    }
            }

            if (!xPeriodic) {
                C.AddBoundaryValue("Velocity_inlet_left", "VelocityX#A", (X, t) => velX);
                C.AddBoundaryValue("Velocity_inlet_left", "VelocityX#B", (X, t) => velX);
                C.AddBoundaryValue("Pressure_outlet_right");
            }


            #endregion


            // initial values
            // ==============
            #region init


            double fx = 1.0;

            if(xPeriodic)
                C.InitialValues_Evaluators.Add("GravityX", X => fx);


            // exact solution for periodic testcase with lower freeslip and upper velocity inlet
            //Func<double[], double, double> u = (X, t) => 0.5 * fx * (H.Pow2() - X[1].Pow2()) + velX;

            //C.ExactSolutionVelocity = new Dictionary<string, Func<double[], double, double>[]>();
            //C.ExactSolutionVelocity.Add("A", new Func<double[], double, double>[] { u, (X, t) => 0.0 });

            C.InitialValues_Evaluators.Add("Phi",
                (X => X[0] - 1)
                );
            C.InitialValues_Evaluators.Add("VelocityX#A", X => velX);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => velX);

            #endregion


            // misc. solver options
            // ====================
            #region solver


            C.Option_LevelSetEvolution = LevelSetEvolution.FastMarching;

            C.LinearSolver.MaxSolverIterations = 100;
            C.NonLinearSolver.MaxSolverIterations = 100;
            C.LinearSolver.NoOfMultigridLevels = 1;

            C.ComputeEnergyProperties = false;

            #endregion

            // Timestepping
            // ============
            #region time

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            double dt = 0.001;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 100;

            #endregion

            return C;
        }


        public static XNSE_Control Test_StagnationPointFlow(int degree = 2) {

            XNSE_Control C = new XNSE_Control();


            // basic database options
            // ======================
            #region db
            C.DbPath = null;
            C.savetodb = false;

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = degree,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = degree,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = degree - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = degree,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = degree,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion

            // grid genration
            // ==============
            #region grid

            double L = 2;
            double H = 1;

            int kelem = 10;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-L/2.0, L/2.0, 2 * kelem +1);
                double[] Ynodes = GenericBlas.Linspace(0, H, kelem);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false);

                grd.EdgeTagNames.Add(1, "freeslip_lower");
                grd.EdgeTagNames.Add(2, "velocity_inlet_upper");
                grd.EdgeTagNames.Add(3, "pressure_outlet_left");
                grd.EdgeTagNames.Add(4, "pressure_outlet_right");

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1]) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - H) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0] + (L / 2.0)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - (L / 2.0)) <= 1.0e-8)
                        et = 4;
                    return et;
                });

                return grd;
            };

            #endregion

            // physical parameters
            // ===================
            #region physics

            const double rhoA = 1;
            const double rhoB = 1;
            const double muA = 1;
            const double muB = 1;
            const double sigma = 1;

            C.PhysicalParameters.rho_A = rhoA;
            C.PhysicalParameters.rho_B = rhoB;
            C.PhysicalParameters.mu_A = muA;
            C.PhysicalParameters.mu_B = muB;
            C.PhysicalParameters.Sigma = sigma;

            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion


            // boundary conditions
            // ===================
            #region BC

            double a = 1.0;

            C.AddBoundaryValue("freeslip_lower");

            C.AddBoundaryValue("Velocity_inlet_upper", "VelocityX#A", (X, t) => a * X[0]);
            C.AddBoundaryValue("Velocity_inlet_upper", "VelocityY#A", (X, t) => - a * X[1]);

            C.AddBoundaryValue("pressure_outlet_left");
            C.AddBoundaryValue("Pressure_outlet_right");

            #endregion


            // initial values
            // ==============
            #region init

            C.InitialValues_Evaluators.Add("Phi",
                (X => -1)
                );
            C.InitialValues_Evaluators.Add("VelocityX#A", X => a * X[0]);
            C.InitialValues_Evaluators.Add("VelocityY#A", X => - a * X[1]);

            #endregion


            // misc. solver options
            // ====================
            #region solver


            C.Option_LevelSetEvolution = LevelSetEvolution.None;
            C.Timestepper_LevelSetHandling = LevelSetHandling.None;

            C.LinearSolver.MaxSolverIterations = 100;
            C.NonLinearSolver.MaxSolverIterations = 100;
            C.LinearSolver.NoOfMultigridLevels = 1;

            C.ComputeEnergyProperties = false;

            #endregion

            // Timestepping
            // ============
            #region time

            C.TimesteppingMode = AppControl._TimesteppingMode.Steady;
            double dt = 0.1;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 10;

            #endregion

            return C;
        }


        public static XNSE_Control FallingDropletOnSurface(int p = 2, int kelem = 40, string _DbPath = null) {

            XNSE_Control C = new XNSE_Control();

            //_DbPath = @"D:\local\local_Testcase_databases\Testcase_RisingBubble";
            _DbPath = @"D:\local\local_test_db";
            //_DbPath = @"\\fdyprime\userspace\smuda\cluster\cluster_db";


            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = C.DbPath != null;
            C.ProjectName = "XNSE/Droplet";
            C.ProjectDescription = "falling droplet";

            #endregion


            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("DivergenceVelocity", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // Physical Parameters
            // ===================
            #region physics

            C.Tags.Add("Testcase 1");
            C.PhysicalParameters.rho_A = 1000;
            C.PhysicalParameters.rho_B = 100;
            C.PhysicalParameters.mu_A = 10;
            C.PhysicalParameters.mu_B = 1;
            C.PhysicalParameters.Sigma = 24.5;


            //C.Tags.Add("Testcase 1 - higher parameters");
            //C.PhysicalParameters.rho_A = 1000;
            //C.PhysicalParameters.rho_B = 10000;
            //C.PhysicalParameters.mu_A = 10;
            //C.PhysicalParameters.mu_B = 100;
            //C.PhysicalParameters.Sigma = 245;

            //C.Tags.Add("Testcase 2");
            //C.PhysicalParameters.rho_A = 1;
            //C.PhysicalParameters.rho_B = 1000;
            //C.PhysicalParameters.mu_A = 0.1;
            //C.PhysicalParameters.mu_B = 10;
            //C.PhysicalParameters.Sigma = 1.96;

            // Re = 3.5 ; Bo(Eo) = 1
            //C.PhysicalParameters.rho_A = 1;
            //C.PhysicalParameters.rho_B = 1000;
            //C.PhysicalParameters.mu_A = 1;
            //C.PhysicalParameters.mu_B = 100;
            //C.PhysicalParameters.Sigma = 245;

            //// Re = 35 ; Bo(Eo) = 100
            //C.PhysicalParameters.rho_A = 1;
            //C.PhysicalParameters.rho_B = 1000;
            //C.PhysicalParameters.mu_A = 0.1;
            //C.PhysicalParameters.mu_B = 10;
            //C.PhysicalParameters.Sigma = 2.45;

            //// Re = 70 ; Bo(Eo) = 10
            //C.PhysicalParameters.rho_A = 1;
            //C.PhysicalParameters.rho_B = 1000;
            //C.PhysicalParameters.mu_A = 0.05;
            //C.PhysicalParameters.mu_B = 5;
            //C.PhysicalParameters.Sigma = 24.5;


            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion

            // grid generation
            // ===============
            #region grid


            double xSize = 1.0;
            double ySize = 2.0;

            //int kelem = 160;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(0, xSize, kelem + 1);
                double[] Ynodes = GenericBlas.Linspace(0, ySize, 2 * kelem + 1);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false);


                grd.EdgeTagNames.Add(1, "wall_lower");
                grd.EdgeTagNames.Add(2, "wall_upper");
                grd.EdgeTagNames.Add(3, "freeslip_left");
                grd.EdgeTagNames.Add(4, "freeslip_right");

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1]) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - ySize) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0]) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - xSize) <= 1.0e-8)
                        et = 4;

                    return et;
                });

                return grd;
            };

            #endregion



            // Initial Values
            // ==============
            #region init

            double[] center = new double[] { 0.5, 1.5 }; //0.5,0.5
            double radius = 0.25;
            Func<double[], double> droplet = (X => ((X[0] - center[0]).Pow2() + (X[1] - center[1]).Pow2()).Sqrt() - radius); // signed-distance form

            Func<double[], double> surface = (X => (X[1] - 1.0));

            Func<double[], double> PhiFunc = X => Math.Min( droplet(X), surface(X));
            C.InitialValues_Evaluators.Add("Phi", PhiFunc);

            C.InitialValues_Evaluators.Add("VelocityX#A", X => 0.0);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => 0.0);

            C.InitialValues_Evaluators.Add("GravityY#A", X => -9.81e-1);
            C.InitialValues_Evaluators.Add("GravityY#B", X => -9.81e-1);


            //var database = new DatabaseInfo(_DbPath);
            //Guid restartID = new Guid("58745416-3320-4e0c-a5fa-fc3a2c5203c7");
            //C.RestartInfo = new Tuple<Guid, Foundation.IO.TimestepNumber>(restartID, null);

            #endregion

            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("wall_lower");
            C.AddBoundaryValue("wall_upper");
            C.AddBoundaryValue("freeslip_left");
            C.AddBoundaryValue("freeslip_right");

            #endregion


            // misc. solver options
            // ====================
            #region solver

            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 50;
            C.NonLinearSolver.MaxSolverIterations = 50;
            //C.Solver_MaxIterations = 50;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            //C.Solver_ConvergenceCriterion = 1e-8;
            C.LevelSet_ConvergenceCriterion = 1e-6;

            C.AdvancedDiscretizationOptions.ViscosityMode = ViscosityMode.Standard;


            C.Option_LevelSetEvolution = LevelSetEvolution.FastMarching;
            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.Default;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = Solution.XNSECommon.SurfaceStressTensor_IsotropicMode.Curvature_Projected;
            C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 1;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.Timestepper_BDFinit = TimeStepperInit.SingleInit;
            C.Timestepper_LevelSetHandling = LevelSetHandling.LieSplitting;

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            double dt = 1e-3; // (1.0 / (double)kelem) / 16.0;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 3000; // (int)(3 / dt);
            C.saveperiod = 30;

            #endregion

            return C;

        }


        public static XNSE_Control OscillatingSphere(int p = 1, int kelem = 19, string _DbPath = null) {

            XNSE_Control C = new XNSE_Control();

            //_DbPath = @"D:\local\local_Testcase_databases\Testcase_OscillatingSphere";

            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = C.DbPath != null;
            C.ProjectName = "XNSE/OscillatingSphere";
            C.ProjectDescription = "static droplet";
            C.Tags.Add("hysing");

            #endregion


            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // grid genration
            // ==============
            #region grid

            //double L = 4.5;

            //double h_min = L / (double)kelem;

            //C.GridFunc = delegate () {
            //    double[] Xnodes = GenericBlas.Linspace(-L, L, kelem + 1);
            //    double[] Ynodes = GenericBlas.Linspace(-L, L, kelem + 1);
            //    var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false);

            //    grd.EdgeTagNames.Add(1, "wall_lower");
            //    grd.EdgeTagNames.Add(2, "wall_upper");
            //    grd.EdgeTagNames.Add(3, "wall_left");
            //    grd.EdgeTagNames.Add(4, "wall_right");

            //    grd.DefineEdgeTags(delegate (double[] X) {
            //        byte et = 0;
            //        if (Math.Abs(X[1] + L) <= 1.0e-8)
            //            et = 1;
            //        if (Math.Abs(X[1] - L) <= 1.0e-8)
            //            et = 2;
            //        if (Math.Abs(X[0] + L) <= 1.0e-8)
            //            et = 3;
            //        if (Math.Abs(X[0] - L) <= 1.0e-8)
            //            et = 4;
            //        return et;
            //    });
            //    return grd;
            //};

            // smolianski
            double L = 1.0;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(0, L, kelem + 1);
                double[] Ynodes = GenericBlas.Linspace(0, L, kelem + 1);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false);

                grd.EdgeTagNames.Add(1, "wall_lower");
                grd.EdgeTagNames.Add(2, "wall_upper");
                grd.EdgeTagNames.Add(3, "wall_left");
                grd.EdgeTagNames.Add(4, "wall_right");

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1]) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - L) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0]) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - L) <= 1.0e-8)
                        et = 4;
                    return et;
                });
                return grd;
            };


            #endregion


            // Physical Parameters
            // ===================
            #region physics

            //C.Tags.Add("Bubble");
            //C.PhysicalParameters.rho_A = 100;
            //C.PhysicalParameters.rho_B = 1000;
            //C.PhysicalParameters.mu_A = 1;
            //C.PhysicalParameters.mu_B = 10;
            //C.PhysicalParameters.Sigma = 24.5;

            //C.Tags.Add("Droplet");
            //C.PhysicalParameters.rho_A = 1000;
            //C.PhysicalParameters.rho_B = 100;
            //C.PhysicalParameters.mu_A = 10;
            //C.PhysicalParameters.mu_B = 1;
            //C.PhysicalParameters.Sigma = 24.5;

            //C.PhysicalParameters.rho_A = 1.0;
            //C.PhysicalParameters.rho_B = 1.0;
            //C.PhysicalParameters.mu_A = 1.0;
            //C.PhysicalParameters.mu_B = 1.0;
            //C.PhysicalParameters.Sigma = 1.0;

            //// Air-Water (lenght scale == centimeters, 3D space)
            //C.PhysicalParameters.rho_A = 1e-3; //     kg / cm³
            //C.PhysicalParameters.rho_B = 1.2e-6; //   kg / cm³
            //C.PhysicalParameters.mu_A = 1e-5; //      kg / cm / sec
            //C.PhysicalParameters.mu_B = 17.1e-8; //   kg / cm / sec
            //C.PhysicalParameters.Sigma = 72.75e-3; // kg / sec²   

            // La = 5000
            C.PhysicalParameters.rho_A = 1.0;
            C.PhysicalParameters.rho_B = 1.0;
            C.PhysicalParameters.mu_A = 1.0;
            C.PhysicalParameters.mu_B = 1.0e-4;
            C.PhysicalParameters.Sigma = 1.0;


            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion


            // Initial Values
            // ==============
            #region init


            double[] center = new double[] { 0.5, 0.5 };
            //double a = 2.4;
            //double b = 2.4;
            //Func<double, double> radius = phi => a * b / Math.Sqrt(a.Pow2() * Math.Sin(phi).Pow2() + b.Pow2() * Math.Cos(phi).Pow2());
            double radius = 0.25;
            Func<double, double> radiusFunc = phi => radius;

            //double delta = 0.0;
            C.InitialValues_Evaluators.Add("Phi",
                //(X => (X[0].Pow2() / a.Pow2() + X[1].Pow2() / b.Pow2()) - 1)
                (X => ((X[0] - center[0]).Pow2() + (X[1] - center[1]).Pow2()).Sqrt() - radius)  // signed distance
                //(X => ((1 + delta) * (X[0] - center[0])).Pow2() + ((1.0 - delta) * (X[1] - center[1])).Pow2() - radius.Pow2())   // quadratic form
                );

            C.InitialValues_Evaluators.Add("VelocityX#A", X => 0.0);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => 0.0);


            //double pressureJump = C.PhysicalParameters.Sigma / radius;
            //C.InitialValues_Evaluators.Add("Pressure#A", X => pressureJump);
            //C.InitialValues_Evaluators.Add("Pressure#B", X => 0.0);


            //var database = new DatabaseInfo(_DbPath);
            //Guid restartID = new Guid("fb857c4c-c060-4d10-a86a-e4ef6a93f5c8");
            //C.RestartInfo = new Tuple<Guid, Foundation.IO.TimestepNumber>(restartID, 180);

            #endregion


            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("wall_lower");
            C.AddBoundaryValue("wall_upper");
            C.AddBoundaryValue("wall_left");
            C.AddBoundaryValue("wall_right");

            #endregion


            // exact solution
            // ==============
            #region exact

            //C.Phi = ((X, t) => ((X[0] - center[0]).Pow2() + (X[1] - center[1]).Pow2()).Sqrt() - radius);

            //C.ExactSolutionVelocity = new Dictionary<string, Func<double[], double, double>[]>();
            //C.ExactSolutionVelocity.Add("A", new Func<double[], double, double>[] { (X, t) => 0.0, (X, t) => 0.0 });
            //C.ExactSolutionVelocity.Add("B", new Func<double[], double, double>[] { (X, t) => 0.0, (X, t) => 0.0 });

            //C.ExactSolutionPressure = new Dictionary<string, Func<double[], double, double>>();
            //C.ExactSolutionPressure.Add("A", (X, t) => pressureJump);
            //C.ExactSolutionPressure.Add("B", (X, t) => 0.0);

            #endregion


            // Fourier Level-Set
            // =================
            #region Fourier

            int numSp = 640;
            double[] FourierP = new double[numSp];
            double[] samplP = new double[numSp];
            for (int sp = 0; sp < numSp; sp++) {
                FourierP[sp] = sp * (2 * Math.PI / (double)numSp);
                samplP[sp] = radius;
            }

            C.FourierLevSetControl = new FourierLevSetControl(FourierType.Polar, 2 * Math.PI, FourierP, samplP, L / (double)kelem) {
                center = center,
                FourierEvolve = Fourier_Evolution.MaterialPoints,
                centerMove = CenterMovement.Reconstructed,
            };


            #endregion


            // misc. solver options
            // ====================
            #region solver

            C.ComputeEnergyProperties = true;

            //C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            //C.AdvancedDiscretizationOptions.PenaltySafety = 40;
            //C.AdvancedDiscretizationOptions.UseGhostPenalties = true;

            //C.ContiField = XNSE_Control.ContinuityProjection.ContinuousDG;
            C.LinearSolver.NoOfMultigridLevels = 2;
            C.LinearSolver.MaxSolverIterations = 50;
            C.NonLinearSolver.MaxSolverIterations = 50;
            //C.Solver_MaxIterations = 50;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            //C.Solver_ConvergenceCriterion = 1e-8;
            C.LevelSet_ConvergenceCriterion = 1e-6;

            C.Option_LevelSetEvolution = LevelSetEvolution.FastMarching;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = SurfaceStressTensor_IsotropicMode.Curvature_Projected;

            C.AdvancedDiscretizationOptions.ViscosityMode = ViscosityMode.Standard;

            //C.AdvancedDiscretizationOptions.surfTensionMode = Solution.XNSECommon.SurfaceTensionMode.LaplaceBeltrami_Local;
            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.NoFilter;
            //C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 1;

            C.LinearSolver.SolverCode=LinearSolverCode.classic_pardiso;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.Timestepper_BDFinit = TimeStepperInit.SingleInit;
            C.Timestepper_LevelSetHandling = LevelSetHandling.Coupled_Once;
            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;

            double dt = 5e-5;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 125;
            C.NoOfTimesteps = 20;
            C.saveperiod = 10;

            #endregion

            return C;

        }

        /// <summary>
        /// Benchmark. Do not change!
        /// </summary>
        /// <param name="p"></param>
        /// <param name="kelem"></param>
        /// <param name="_DbPath"></param>
        /// <param name="D">2D or 3D</param>
        /// <returns></returns>
        public static XNSE_Control StokesSphere(int p = 4, int kelem =32, string _DbPath = null, int D = 2) {

            XNSE_Control C = new XNSE_Control();

            // basic database options
            // ======================
            #region db
            //_DbPath = @"D:\trash_DB";
            C.DbPath = _DbPath;
            C.savetodb = C.DbPath != null;
            C.ProjectName = "XNSE/StokesSphere";
            C.ProjectDescription = "static droplet";

            #endregion


            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("Velocity*", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = 2,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = Math.Max(4, 2 * p + 2),
                //Degree = 4,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // grid generation
            // ===============
            #region grid



            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-1, 1, kelem + 1);
                double[] Ynodes = GenericBlas.Linspace(-1, 1, kelem + 1);
                double[] Znodes = GenericBlas.Linspace(-1, 1, kelem + 1);

                GridCommons grd;
                switch(D) {
                    case 2:
                    grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes);
                    break;

                    case 3:
                    grd = Grid3D.Cartesian3DGrid(Xnodes, Ynodes, Znodes);
                    break;

                    default:
                    throw new ArgumentOutOfRangeException();
                }


                grd.DefineEdgeTags(delegate (double[] X) {
                    if (Math.Abs(X[0] - (-1)) <= 1.0e-8)
                        return "wall_left";
                    if (Math.Abs(X[0] - (+1)) <= 1.0e-8)
                        return "wall_right";
                    if (Math.Abs(X[1] - (-1)) <= 1.0e-8)
                        return "wall_front";
                    if (Math.Abs(X[1] - (+1)) <= 1.0e-8)
                        return "wall_back";
                    if(D > 2) {
                        if(Math.Abs(X[2] - (-1)) <= 1.0e-8)
                            return "wall_top";
                        if(Math.Abs(X[2] - (+1)) <= 1.0e-8)
                            return "wall_bottom";
                    }

                    throw new ArgumentException("unknown wall");
                });
                return grd;
            };


            #endregion


            // Physical Parameters
            // ===================
            #region physics


            // Air-Water (lenght scale == centimeters, 3D space)
            C.PhysicalParameters.rho_A = 1e-3; //     kg / cm³
            C.PhysicalParameters.rho_B = 1.2e-6; //   kg / cm³
            C.PhysicalParameters.mu_A = 1e-5; //      kg / cm / sec
            C.PhysicalParameters.mu_B = 17.1e-8; //   kg / cm / sec
            C.PhysicalParameters.Sigma = 72.75e-3; // kg / sec²   



            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            #endregion


            // Initial Values
            // ==============
            #region init

            //double[] center = new double[] { 0.5, 0.5 };
            ////double a = 2.4;
            ////double b = 2.4;
            ////Func<double, double> radius = phi => a * b / Math.Sqrt(a.Pow2() * Math.Sin(phi).Pow2() + b.Pow2() * Math.Cos(phi).Pow2());
            //double radius = 0.25;
            //Func<double, double> radiusFunc = phi => radius;

            //double delta = 0.0;

            double r = 0.49;
            double nonsp = 0.5;

            if (D == 2)
                C.AddInitialValue("Phi", new Formula($"X => (X[0]/{r * nonsp}).Pow2() + (X[1]/{r}).Pow2() - 1.0", false));
            else if (D == 3)
                C.AddInitialValue("Phi", new Formula($"X => (X[0]/{r * nonsp}).Pow2() + (X[1]/{r}).Pow2() + (X[2]/{r}).Pow2() - 1.0", false));
            else
                throw new ArgumentOutOfRangeException();

            C.LSContiProjectionMethod = ContinuityProjectionOption.None;


            #endregion


            // boundary conditions
            // ===================
            #region BC

            //C.AddBoundaryValue("wall_lower");
            //C.AddBoundaryValue("wall_upper");
            //C.AddBoundaryValue("wall_left");
            //C.AddBoundaryValue("wall_right");
            //C.AddBoundaryValue("wall_left");
            //C.AddBoundaryValue("wall_right");

            #endregion

            // misc. solver options
            // ====================
            #region solver

            C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Saye;
            C.ComputeEnergyProperties = false;
            C.AgglomerationThreshold = 0.1;
            //C.AdvancedDiscretizationOptions.PenaltySafety = 40;
            //C.AdvancedDiscretizationOptions.UseGhostPenalties = true;

            // Solver related Stuff
            //C.ContiField = XNSE_Control.ContinuityProjection.ContinuousDG;
            //C.VelocityBlockPrecondMode = MultigridOperator.Mode.SymPart_DiagBlockEquilib;
            //C.VelocityBlockPrecondMode = MultigridOperator.Mode.IdMass_DropIndefinite;
            //C.PressureBlockPrecondMode = MultigridOperator.Mode.IdMass_DropIndefinite;
            C.UseSchurBlockPrec = true;

            C.LinearSolver.NoOfMultigridLevels = 5;
            C.LinearSolver.MaxSolverIterations = 30;
            C.LinearSolver.TargetBlockSize = 10000;
            C.LinearSolver.MaxKrylovDim = 50;
            C.LinearSolver.SolverCode = LinearSolverCode.exp_Kcycle_schwarz;
            C.LinearSolver.verbose = true;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            C.NonLinearSolver.verbose = true;
            C.NonLinearSolver.MaxSolverIterations = 100;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LevelSet_ConvergenceCriterion = 1e-6;

            // Levelset & Co
            C.Option_LevelSetEvolution = LevelSetEvolution.None;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_ContactLine;
            C.AdvancedDiscretizationOptions.ViscosityMode = ViscosityMode.FullySymmetric;
            //C.AdvancedDiscretizationOptions.surfTensionMode = Solution.XNSECommon.SurfaceTensionMode.LaplaceBeltrami_Local;
            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.NoFilter;
            //C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 1;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.Timestepper_BDFinit = TimeStepperInit.SingleInit;
            C.Timestepper_LevelSetHandling = LevelSetHandling.None;
            C.Option_LevelSetEvolution = LevelSetEvolution.None;

            C.TimesteppingMode = AppControl._TimesteppingMode.Steady;
            //C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            //C.dtFixed = 1E-4;


            #endregion

            C.SessionName = String.Format("J{0}_p{1}_{2}", Math.Pow(kelem, D), p, C.LinearSolver.SolverCode.ToString());

            return C;

        }


        public static XNSE_Control CollidingDroplets(int p = 2, int kelem = 40, string _DbPath = null) {

            XNSE_Control C = new XNSE_Control();

            // basic database options
            // ======================
            #region db

            _DbPath = @"D:\local\local_Testcase_databases\Testcase_CollidingDroplets";

            C.DbPath = _DbPath;
            C.savetodb = C.DbPath != null;
            C.ProjectName = "XNSE/Droplets";
            C.ProjectDescription = "colliding droplets";

            #endregion


            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // grid genration
            // ==============
            #region grid


            double xSize = 3.0;
            double ySize = 3.0;

            C.GridFunc = delegate () {
                //double[] Xnodes = GenericBlas.Linspace(-xSize / 2.0, xSize / 2.0, (int)xSize * kelem + 1);     // + 1 collision at cell boundary
                //double[] Ynodes = GenericBlas.Linspace(-ySize / 2.0, ySize / 2.0, (int)ySize * kelem + 1);


                var _xNodes1 = Grid1D.TanhSpacing(-1.5, -0.1, 20 + 1, 1.5, false); 
                _xNodes1 = _xNodes1.GetSubVector(0, (_xNodes1.Length - 1));
                var _xNodes2 = GenericBlas.Linspace(-0.1, 0.1, 10 + 1); 
                _xNodes2 = _xNodes2.GetSubVector(0, (_xNodes2.Length - 1));
                var _xNodes3 = Grid1D.TanhSpacing(0.1, 1.5, 20 + 1, 1.5, true);

                var xNodes = ArrayTools.Cat(_xNodes1, _xNodes2, _xNodes3);


                var _yNodes1 = Grid1D.TanhSpacing(-1.5, 0.0, 24 + 1, 1.5, false);
                _yNodes1 = _yNodes1.GetSubVector(0, (_yNodes1.Length - 1));
                //var _yNodes2 = GenericBlas.Linspace(-1.2, 1.2, Convert.ToInt32(40 * MeshFactor)); //40
                //_yNodes2 = _yNodes2.GetSubVector(0, (_yNodes2.Length - 1));
                var _yNodes3 = Grid1D.TanhSpacing(0.0, 1.5, 24 + 1, 1.5, true);

                var yNodes = ArrayTools.Cat(_yNodes1, _yNodes3);

                var grd = Grid2D.Cartesian2DGrid(xNodes, yNodes, periodicX: false);


                grd.EdgeTagNames.Add(1, "pressure_outlet_lower");
                grd.EdgeTagNames.Add(2, "pressure_outlet_upper");
                grd.EdgeTagNames.Add(3, "freeslip_left");
                grd.EdgeTagNames.Add(4, "freeslip_right");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + ySize / 2.0) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - ySize / 2.0) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0] + xSize / 2.0) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - xSize / 2.0) <= 1.0e-8)
                        et = 4;

                    return et;
                });

                return grd;
            };

            #endregion


            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("pressure_outlet_lower");
            C.AddBoundaryValue("pressure_outlet_upper");
            C.AddBoundaryValue("freeslip_left");
            C.AddBoundaryValue("freeslip_right");


            #endregion


            // Initial Values
            // ==============
            #region init

            // left droplet
            double[] center_l = new double[] { -0.3, 0.0 };
            double radius_l = 0.25;
            Func<double[], double> bubble_l = (X => ((X[0] - center_l[0]).Pow2() + (X[1] - center_l[1]).Pow2()).Sqrt() - radius_l); // signed-distance form

            // right droplet
            double[] center_r = new double[] { 0.3, 0.0 };
            double radius_r = 0.25;
            Func<double[], double> bubble_r = (X => ((X[0] - center_r[0]).Pow2() + (X[1] - center_r[1]).Pow2()).Sqrt() - radius_r); // signed-distance form

            Func<double[], double> PhiFunc = (X => Math.Min(bubble_l(X), bubble_r(X)));

            C.InitialValues_Evaluators.Add("Phi", PhiFunc);

            double vel_collision = 2.0;

            C.InitialValues_Evaluators.Add("VelocityX#A", X => (X[0] < 0.0) ? vel_collision / 2.0 : -vel_collision / 2.0);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => 0.0);


            #endregion


            // Physical Parameters
            // ===================
            #region physics


            C.PhysicalParameters.rho_A = 1e-2;
            C.PhysicalParameters.rho_B = 1.5e-5;
            C.PhysicalParameters.mu_A = 7e-4;
            C.PhysicalParameters.mu_B = 6e-6;
            C.PhysicalParameters.Sigma = 0.5;

            //// tetradecane(A) in nitrogen(B): in m 
            //C.PhysicalParameters.rho_A = 764;
            //C.PhysicalParameters.rho_B = 1.25;
            //C.PhysicalParameters.mu_A = 2e-3;
            //C.PhysicalParameters.mu_B = 16.6e-6;
            //C.PhysicalParameters.Sigma = 26.56e-3;

            //// tetradecane(A) in nitrogen(B): in mm 
            //C.PhysicalParameters.rho_A = 7.64e-7;
            //C.PhysicalParameters.rho_B = 1.25e-9;
            //C.PhysicalParameters.mu_A = 2e-6;
            //C.PhysicalParameters.mu_B = 16.6e-9;
            //C.PhysicalParameters.Sigma = 26.56e-3;


            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion


            // misc. solver options
            // ====================
            #region solver

            C.LinearSolver.SolverCode =  LinearSolverCode.classic_pardiso;

            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 50;
            C.NonLinearSolver.MaxSolverIterations = 50;
            //C.Solver_MaxIterations = 50;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            //C.Solver_ConvergenceCriterion = 1e-8;
            C.LevelSet_ConvergenceCriterion = 1e-6;

            C.AdvancedDiscretizationOptions.ViscosityMode = ViscosityMode.Standard;


            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.Default;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = SurfaceStressTensor_IsotropicMode.Curvature_Projected;
            C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 1;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.Timestepper_BDFinit = TimeStepperInit.SingleInit;
            //C.dt_increment = 20;
            C.Timestepper_LevelSetHandling = LevelSetHandling.LieSplitting;

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            //C.TimeStepper = XNSE_Control._Timestepper.BDF2;
            double dt = 1e-4; 
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 1000; 
            C.saveperiod = 10;

            #endregion

            return C;

        }


        public static XNSE_Control FreeSlipBCTest(string _DbPath = null, int p = 2) {

            XNSE_Control C = new XNSE_Control();


            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = false;

            #endregion


            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // Physical Parameters
            // ===================
            #region physics

            C.PhysicalParameters.rho_A = 1;
            C.PhysicalParameters.rho_B = 1;
            C.PhysicalParameters.mu_A = 1;
            C.PhysicalParameters.mu_B = 1;
            C.PhysicalParameters.Sigma = 1;
            

            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion


            // grid genration
            // ==============
            #region grid

            double L = 1.0;
            int kelem = 20;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-L, L, 2 * kelem + 1);
                double[] Ynodes = GenericBlas.Linspace(-L, L, 2 * kelem + 1);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: true);

                grd.EdgeTagNames.Add(1, "freeslip_lower");
                grd.EdgeTagNames.Add(2, "freeslip_upper");
                //grd.EdgeTagNames.Add(3, "velocity_inlet_left");
                //grd.EdgeTagNames.Add(4, "velocity_inlet_right");

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + (L)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - (L)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0] + (L)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - (L)) <= 1.0e-8)
                        et = 4;
                    return et;
                });
                return grd;
            };


            #endregion


            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("freeslip_lower");
            C.AddBoundaryValue("freeslip_upper");
            //C.AddBoundaryCondition("velocity_inlet_left", "VelocityX#A", X => 1.0);
            //C.AddBoundaryCondition("velocity_inlet_left", "VelocityX#B", X => 1.0);
            //C.AddBoundaryCondition("velocity_inlet_right", "VelocityX#A", X => 1.0);
            //C.AddBoundaryCondition("velocity_inlet_right", "VelocityX#B", X => 1.0);

            #endregion


            // Initial Values
            // ==============
            #region init

            C.InitialValues_Evaluators.Add("Phi",
                (X => -1)
                );

            //C.InitialValues_Evaluators.Add("VelocityX#A", X => 2.0 * X[1] * (1 - X[0].Pow2()));
            //C.InitialValues_Evaluators.Add("VelocityY#A", X => -2.0 * X[0] * (1 - X[1].Pow2()));

            C.InitialValues_Evaluators.Add("VelocityX#A", X => 0.0);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => 0.0);

            C.InitialValues_Evaluators.Add("GravityX#A", X => 0.1);
            C.InitialValues_Evaluators.Add("GravityX#B", X => 0.1);

            #endregion


            // misc. solver options
            // ====================
            #region solver


            C.ComputeEnergyProperties = false;
            C.Option_LevelSetEvolution = LevelSetEvolution.None;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 50;
            C.NonLinearSolver.MaxSolverIterations = 50;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            C.LevelSet_ConvergenceCriterion = 1e-6;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            double dt = 1e-2; 
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 1000; 

            #endregion

            return C;
        }


        public static XNSE_Control KelvinHelmholtzInstability(string _DbPath = @"\\fdyprime\userspace\smuda\Databases\test_db", int p = 2) {

            XNSE_Control C = new XNSE_Control();

            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = _DbPath != null;
            C.ProjectName = "XNSE/Instability";
            C.ProjectDescription = "Kelvin Helmholtz Instability";

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = 4,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = 8,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion

            // grid genration
            // ==============
            #region grid

            bool xPeriodic = true;

            double xSize = 4.0;
            double ySize = 2.0;

            int xkelem = 41;
            int ykelem = 21;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-xSize, xSize, xkelem + 1);
                double[] Ynodes = GenericBlas.Linspace(-ySize, ySize, ykelem + 1);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: xPeriodic);


                grd.EdgeTagNames.Add(1, "velocity_inlet_lower");
                grd.EdgeTagNames.Add(2, "velocity_inlet_upper");
                if (!xPeriodic) {
                    grd.EdgeTagNames.Add(3, "velocity_inlet_left");
                    grd.EdgeTagNames.Add(4, "velocity_inlet_right");
                }


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + ySize) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - ySize) <= 1.0e-8)
                        et = 2;
                    if (!xPeriodic) {
                        if (Math.Abs(X[0] + xSize) <= 1.0e-8)
                            et = 3;
                        if (Math.Abs(X[0] - xSize) <= 1.0e-8)
                            et = 4;
                    }

                    return et;
                });

                return grd;
            };

            #endregion

            // exact solution (viscous potential flow)
            // =======================================
            #region exact

            // Air-Water (length scale: centimeters)
            double rho_l = 1e-3;      // kg / cm^3
            double rho_a = 1.2e-6;    // kg / cm^3
            double mu_l = 1e-5;       // kg / cm * s
            double mu_a = 17.1e-8;    // kg / cm * s
            double sigma = 72.75e-3;  // kg / s^2     

            double U_l = 100.0;     //undisturbed x-velocity for water phase
            double U_a = 600.0;     //undisturbed x-velocity for air phase

            double h_l = ySize;
            double h_a = ySize;

            double A_0 = 0.01;           //(complex) amplitude of inital disturbance   
            double k = 2 * Math.PI;       //wavenumber of disturbance

            double beta_R = -11.476;        //growth rate: beta = beta_R + i*beta_I
            double beta_I = -520.368;

            double A_lR = (A_0 * beta_R) / (k * Math.Sinh(k * h_l));          //complex amplitude for water potential
            double A_lI = (A_0 * (beta_I + k * U_l)) / (k * Math.Sinh(k * h_l));
            double A_aR = -(A_0 * beta_R) / (k * Math.Sinh(k * h_a));          //complex amplitude for air potential
            double A_aI = -(A_0 * (beta_I + k * U_a)) / (k * Math.Sinh(k * h_a));


            Func<double[], double, double> h = (X, t) => 2 * A_0 * Math.Exp(beta_R * t) * Math.Cos(beta_I * t + k * X[0]);
            Func<double[], double, double> u_l = (X, t) => U_l - 2 * k * Math.Exp(beta_R * t) * Math.Cosh(k * (X[1] + h_l)) * (A_lR * Math.Sin(beta_I * t + k * X[0]) + A_lI * Math.Cos(beta_I * t + k * X[0]));
            Func<double[], double, double> w_l = (X, t) => 2 * Math.Exp(beta_R * t) * Math.Sinh(k * (X[1] + h_l)) * (A_aR * Math.Cos(beta_I * t + k * X[0]) - A_aI * Math.Sin(beta_I * t + k * X[0]));
            Func<double[], double, double> u_a = (X, t) => U_a - 2 * k * Math.Exp(beta_R * t) * Math.Cosh(k * (X[1] - h_a)) * (A_aR * Math.Sin(beta_I * t + k * X[0]) + A_aI * Math.Cos(beta_I * t + k * X[0]));
            Func<double[], double, double> w_a = (X, t) => 2 * Math.Exp(beta_R * t) * Math.Sinh(k * (X[1] - h_a)) * (A_aR * Math.Cos(beta_I * t + k * X[0]) - A_aI * Math.Sin(beta_I * t + k * X[0]));

            #endregion

            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("velocity_inlet_lower", "VelocityX#A", u_l);
            C.AddBoundaryValue("velocity_inlet_lower", "VelocityX#B", u_a);
            C.AddBoundaryValue("velocity_inlet_upper", "VelocityX#A", u_l);
            C.AddBoundaryValue("velocity_inlet_upper", "VelocityX#B", u_a);
            if (!xPeriodic) {
                C.AddBoundaryValue("velocity_inlet_left", "VelocityX#A", u_l);
                C.AddBoundaryValue("velocity_inlet_left", "VelocityX#B", u_a);
                C.AddBoundaryValue("velocity_inlet_right", "VelocityX#A", u_l);
                C.AddBoundaryValue("velocity_inlet_right", "VelocityX#B", u_a);

                //C.AddBoundaryCondition("velocity_inlet_left", "Phi", X => (X[1] - h(X, 0)));
                //C.AddBoundaryCondition("velocity_inlet_right", "Phi", X => (X[1] - h(X, 0)));
            }


            #endregion

            // Initial Values
            // ==============
            #region init

            C.InitialValues_Evaluators.Add("Phi",
                (X => (X[1] - h(X, 0)))
                );

            C.InitialValues_Evaluators.Add("VelocityX#A", X => u_l(X, 0));
            C.InitialValues_Evaluators.Add("VelocityX#B", X => u_a(X, 0));

            C.InitialValues_Evaluators.Add("GravityY#A", X => -9.81e2);
            C.InitialValues_Evaluators.Add("GravityY#B", X => -9.81e2);


            #endregion

            // Physical Parameters
            // ===================
            #region physics


            // Air-Water (length scale: meters)
            //C.PhysicalParameters.rho_A = 1000;      // kg / m^3
            //C.PhysicalParameters.rho_B = 1.2;       // kg / m^3
            //C.PhysicalParameters.mu_A = 1.0e-3;     // kg / m * s
            //C.PhysicalParameters.mu_B = 17.1e-6;    // kg / m * s
            //C.PhysicalParameters.Sigma = 72.75e-3;  // kg / s^2     


            // Air-Water (length scale: centimeters)
            C.PhysicalParameters.rho_A = rho_l;     // kg / cm^3
            C.PhysicalParameters.rho_B = rho_a;     // kg / cm^3
            C.PhysicalParameters.mu_A = mu_l;       // kg / cm * s
            C.PhysicalParameters.mu_B = mu_a;       // kg / cm * s
            C.PhysicalParameters.Sigma = sigma;     // kg / s^2     


            // Air-Oil (length scale: centimeters)
            //C.PhysicalParameters.rho_A = 8.63e-4;
            //C.PhysicalParameters.rho_B = 1.2e-6;
            //C.PhysicalParameters.mu_A = 2e-4;
            //C.PhysicalParameters.mu_B = 17.1e-8;

            //C.PhysicalParameters.Sigma = 0.0;   // free surface boundary condition

            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion

            // misc. solver options
            // ====================
            #region solver

            C.AgglomerationThreshold = 0.1;
            C.AdvancedDiscretizationOptions.ViscosityMode = Solution.XNSECommon.ViscosityMode.FullySymmetric;
            C.LinearSolver.NoOfMultigridLevels = 1;

            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.Default;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = Solution.XNSECommon.SurfaceStressTensor_IsotropicMode.Curvature_Projected;
            C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 0;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            double dt = 1e-5;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 1000;

            #endregion

            return C;

        }


        public static XNSE_Control KH_Instability(string _DbPath = @"\\fdyprime\userspace\smuda\Databases\test_db", int p = 2) {

            XNSE_Control C = new XNSE_Control();

            // basic database options
            // ======================
            #region db

            C.DbPath = _DbPath;
            C.savetodb = C.DbPath != null;
            C.ProjectName = "XNSE/Instability";
            C.ProjectDescription = "Kelvin Helmholtz Instability";
            C.Tags.Add("specialized LevelSet");

            #endregion

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = 4,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = 8,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion

            // grid genration
            // ==============
            #region grid

            double L = 2 * Math.PI;

            double h_l = 0.1;
            double h_a = 5 * h_l;

            int xkelem = 60;
            int ykelem = 10 * 6 + 1;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(0, L, xkelem + 1);
                double[] Ynodes = GenericBlas.Linspace(-h_l, h_a, ykelem + 1);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: true);


                grd.EdgeTagNames.Add(1, "wall_lower");
                grd.EdgeTagNames.Add(2, "velocity_inlet_upper");

                //grd.EdgeTagNames.Add(1, "wall_lower");
                //grd.EdgeTagNames.Add(2, "wall_upper");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + h_l) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - h_a) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0]) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - L) <= 1.0e-8)
                        et = 4;

                    return et;
                });

                return grd;
            };

            #endregion

            // Physical Parameters
            // ===================
            #region physics  

            // Air-Water (length scale: centimeters)
            double rho_l = 1e-3;          // kg / cm^3
            double rho_a = 1.2e-6;        // kg / cm^3
            double mu_l = 1e-5;           // kg / cm * s
            double mu_a = 17.1e-8;        // kg / cm * s
            double sigma = 72.75e-3;      // kg / s^2     


            // Air-Oil (length scale: centimeters)
            //double rho_A = 8.63e-4;
            //double rho_B = 1.2e-6;
            //double mu_A = 2e-4;
            //double mu_B = 17.1e-8;


            C.PhysicalParameters.rho_A = rho_l;          // kg / cm^3
            C.PhysicalParameters.rho_B = rho_a;        // kg / cm^3
            C.PhysicalParameters.mu_A = mu_l;           // kg / cm * s
            C.PhysicalParameters.mu_B = mu_a;       // kg / cm * s
            C.PhysicalParameters.Sigma = sigma;      // kg / s^2     
            //C.PhysicalParameters.Sigma = 0.0;   // free surface boundary condition


            C.PhysicalParameters.IncludeConvection = true;
            C.PhysicalParameters.Material = true;

            #endregion

            // boundary conditions
            // ===================
            #region BC

            double U_l = 0.0;
            double U_a = 0.0;

            C.AddBoundaryValue("wall_lower", "VelocityX#A", X => U_l);
            C.AddBoundaryValue("velocity_inlet_upper", "VelocityX#B", X => U_a);

            //C.AddBoundaryCondition("wall_lower", "VelocityX#A", X => 0.0);
            //C.AddBoundaryCondition("wall_upper", "VelocityX#B", X => 0.0);

            #endregion

            // Initial Values
            // ==============
            #region init

            double A0 = 0.005;
            double k = 2;
            Func<double, double> h = x => A0 * Math.Sin(k * x);

            C.InitialValues_Evaluators.Add("Phi", (X => X[1] - h(X[0])));

            C.InitialValues_Evaluators.Add("VelocityX#A", X => U_l);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => U_a);

            double g = 9.81e2;
            double alpha = Math.PI / 6;

            C.InitialValues_Evaluators.Add("GravityY#A", X => -g * Math.Cos(alpha));
            C.InitialValues_Evaluators.Add("GravityY#B", X => -g * Math.Cos(alpha));

            C.InitialValues_Evaluators.Add("GravityX#A", X => g * Math.Sin(alpha));
            C.InitialValues_Evaluators.Add("GravityX#B", X => g * Math.Sin(alpha));

            //var database = new DatabaseInfo(_DbPath);
            //var sessTank = database.Sessions.Where(s => s.Name.ToLower().Contains("instability"));
            //var latestSession = sessTank.OrderByDescending(e => e.CreationTime).First();
            //C.RestartInfo = new Tuple<Guid, Foundation.IO.TimestepNumber>(latestSession.ID, null);


            #endregion

            // misc. solver options
            // ====================
            #region solver

            C.AgglomerationThreshold = 0.1;
            C.AdvancedDiscretizationOptions.ViscosityMode = Solution.XNSECommon.ViscosityMode.FullySymmetric;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 100;
            C.NonLinearSolver.MaxSolverIterations = 100;
            //C.Solver_MaxIterations = 100;

            C.Option_LevelSetEvolution = LevelSetEvolution.Fourier;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = Solution.XNSECommon.SurfaceStressTensor_IsotropicMode.Curvature_Fourier;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimesteppingMode = AppControl._TimesteppingMode.Transient;
            double dt = 1e-3;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 1000;

            #endregion

            return C;
        }


        public static XNSE_Control RisingBubble_fn_BenchmarkTest()
        {

            XNSE_Control C = new XNSE_Control();

            //C.LogValues = XNSE_Control.LoggingValues.RisingBubble;
            C.PostprocessingModules.Add(new PhysicalBasedTestcases.RisingBubble2DBenchmarkQuantities());

            C.savetodb = false;
            int p = 2;

            // DG degrees
            // ==========
            #region degrees

            C.FieldOptions.Add("VelocityX", new FieldOpts()
            {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts()
            {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts()
            {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts()
            {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts()
            {
                Degree = 2,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts()
            {
                Degree = 2,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // grid genration
            // ==============
            #region grid

            bool xPeriodic = false;

            double size = 1.5;

            C.GridFunc = delegate () {
                double[] Xnodes = GenericBlas.Linspace(-size, size, 19);
                double[] Ynodes = GenericBlas.Linspace(-size, size, 19);
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: xPeriodic);


                grd.EdgeTagNames.Add(1, "wall_lower");
                grd.EdgeTagNames.Add(2, "wall_upper");
                if (!xPeriodic)
                {
                    grd.EdgeTagNames.Add(3, "wall_left");
                    grd.EdgeTagNames.Add(4, "wall_right");
                }

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1] + size) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - size) <= 1.0e-8)
                        et = 2;
                    if (!xPeriodic)
                    {
                        if (Math.Abs(X[0] + size) <= 1.0e-8)
                            et = 3;
                        if (Math.Abs(X[0] - size) <= 1.0e-8)
                            et = 4;
                    }

                    return et;
                });

                return grd;
            };

            #endregion


            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("wall_lower", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_upper", "VelocityX#A", (X, t) => 0.0);
            C.AddBoundaryValue("wall_lower", "VelocityX#B", (X, t) => 0.0);
            C.AddBoundaryValue("wall_upper", "VelocityX#B", (X, t) => 0.0);
            if (!xPeriodic)
            {
                C.AddBoundaryValue("wall_left", "VelocityX#A", (X, t) => 0.0);
                C.AddBoundaryValue("wall_right", "VelocityX#A", (X, t) => 0.0);
                C.AddBoundaryValue("wall_left", "VelocityX#B", (X, t) => 0.0);
                C.AddBoundaryValue("wall_right", "VelocityX#B", (X, t) => 0.0);
            }

            #endregion


            // Initial Values
            // ==============
            #region init

            double x0_c = 0.1;
            double x1_c = -0.3;
            double radius = 0.8;
            Func<double[], double> circle_ls_signd = X => Math.Sqrt((X[0] - x0_c).Pow2() + (X[1] - x1_c).Pow2()) - radius;
            Func<double[], double> circle_ls_quadr = X => ((X[1] - x0_c) / radius).Pow2() + ((X[1] - x1_c) / radius).Pow2() - 1.0;

            C.InitialValues_Evaluators.Add("Phi", (circle_ls_signd));

            C.InitialValues_Evaluators.Add("VelocityX#A", X => 0.0);
            C.InitialValues_Evaluators.Add("VelocityX#B", X => 0.0);

            #endregion



            // Physical Parameters
            // ===================
            #region physics

            C.PhysicalParameters.rho_A = 1.0;
            C.PhysicalParameters.rho_B = 1.0;
            C.PhysicalParameters.mu_A = 1.0;
            C.PhysicalParameters.mu_B = 1.0;
            C.PhysicalParameters.Sigma = 0.0;

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            #endregion


            // misc. solver options
            // ====================
            #region solver

            C.AgglomerationThreshold = 0.1;
            C.AdvancedDiscretizationOptions.ViscosityMode = ViscosityMode.FullySymmetric;
            C.Option_LevelSetEvolution = LevelSetEvolution.None;
            C.LinearSolver.MaxSolverIterations = 50;
            C.NonLinearSolver.MaxSolverIterations = 50;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            //C.Solver_ConvergenceCriterion = 1e-8;

            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.NoFilter;
            C.AdvancedDiscretizationOptions.SST_isotropicMode = SurfaceStressTensor_IsotropicMode.Curvature_Projected;
            C.AdvancedDiscretizationOptions.FilterConfiguration.FilterCurvatureCycles = 0;

            #endregion


            C.TimesteppingMode = AppControl._TimesteppingMode.Steady;


            return C;

        }


        public static XNSE_Control HMF_hangingNodesTest() {

            XNSE_Control C = new XNSE_Control();

            C.savetodb = false;

            int p = 1;

            C.FieldOptions.Add("VelocityX", new FieldOpts()
            {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts()
            {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts()
            {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts()
            {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts()
            {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts()
            {
                Degree = p + 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });


            C.GridFunc = delegate () {

                double[] Xnodes = GenericBlas.Linspace(0, 3, 4);
                double[] Ynodes = GenericBlas.Linspace(0, 3, 4);
                var grid = Grid2D.Cartesian2DGrid(Xnodes, Ynodes);

                //var box_outer_p1 = new double[2] { 0, 0 };
                //var box_outer_p2 = new double[2] { 3, 3 };
                //var box_outer = new GridCommons.GridBox(box_outer_p1, box_outer_p2, 3, 3);

                //var box_inner_p1 = new double[2] { 1, 1 };
                //var box_inner_p2 = new double[2] { 2, 2 };
                //var box_inner = new GridCommons.GridBox(box_inner_p1, box_inner_p2, 2, 2);

                //var grid = Grid2D.HangingNodes2D(box_outer, box_inner);

                grid.EdgeTagNames.Add(1, "wall_lower");
                grid.EdgeTagNames.Add(2, "wall_upper");
                grid.EdgeTagNames.Add(3, "wall_left");
                grid.EdgeTagNames.Add(4, "wall_right");

                grid.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[1]) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[1] - 3) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[0]) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[0] - 3) <= 1.0e-8)
                        et = 4;

                    return et;
                });

                return grid;
            };


            C.AddBoundaryValue("wall_lower");
            C.AddBoundaryValue("wall_upper");
            C.AddBoundaryValue("wall_left");
            C.AddBoundaryValue("wall_right");

            C.InitialValues_Evaluators.Add("Phi", (X => X[1] - X[0] + 0.2));

            C.ComputeEnergyProperties = false;

            C.TimesteppingMode = AppControl._TimesteppingMode.Steady;
            C.Option_LevelSetEvolution = LevelSetEvolution.None;
            C.Timestepper_LevelSetHandling = LevelSetHandling.None;


            return C;

        }


        public static XNSE_Control HMF_3DcontactlineTest() {

            XNSE_Control C = new XNSE_Control();

            int D = 3;

            if(D == 3)
                C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Classic;

            AppControl._TimesteppingMode compMode = AppControl._TimesteppingMode.Steady;

            // basic database options
            // ======================
            #region db

            C.DbPath = null;
            C.savetodb = C.DbPath != null;
            C.ProjectName = "XNSE/HMF3D";

            #endregion


            // DG degrees
            // ==========
            #region degrees

            int p = 2;

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            if(D == 3) {
                C.FieldOptions.Add("VelocityZ", new FieldOpts() {
                    Degree = p,
                    SaveToDB = FieldOpts.SaveToDBOpt.TRUE
                });
            }
            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // Physical Parameters
            // ===================
            #region physics

            C.Tags.Add("Reusken");
            C.PhysicalParameters.rho_A = 1;
            C.PhysicalParameters.rho_B = 1;
            C.PhysicalParameters.mu_A = 1;
            C.PhysicalParameters.mu_B = 1;
            double sigma = 0.0;
            C.PhysicalParameters.Sigma = sigma;

            //C.PhysicalParameters.betaS_A = 0.05;
            //C.PhysicalParameters.betaS_B = 0.05;

            //C.PhysicalParameters.betaL = 0;
            //C.PhysicalParameters.theta_e = Math.PI / 2.0;

            C.PhysicalParameters.IncludeConvection = false;
            C.PhysicalParameters.Material = true;

            #endregion


            // grid generation
            // ===============
            #region grid

            double scale = 0.2;

            int xkelem = 8;
            int ykelem = 8;
            int zkelem = 1;

            double xSize = (scale * (double)xkelem) / 2.0;
            double ySize = (scale * (double)ykelem) / 2.0;
            double zSize = 2.0 * scale * (double)zkelem;

            if(D == 2) {
                C.GridFunc = delegate () {
                    double[] Xnodes = GenericBlas.Linspace(-xSize, xSize, xkelem + 1);
                    double[] Ynodes = GenericBlas.Linspace(-ySize, ySize, ykelem + 1);
                    var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes);

                    grd.EdgeTagNames.Add(1, "navierslip_linear_lower");
                    grd.EdgeTagNames.Add(2, "navierslip_linear_upper");
                    grd.EdgeTagNames.Add(3, "navierslip_linear_left");
                    grd.EdgeTagNames.Add(4, "navierslip_linear_right");

                    grd.DefineEdgeTags(delegate (double[] X) {
                        byte et = 0;
                        if(Math.Abs(X[1] + ySize) <= 1.0e-8)
                            et = 1;
                        if(Math.Abs(X[1] - ySize) <= 1.0e-8)
                            et = 2;
                        if(Math.Abs(X[0] + xSize) <= 1.0e-8)
                            et = 3;
                        if(Math.Abs(X[0] - xSize) <= 1.0e-8)
                            et = 4;

                        return et;
                    });

                    return grd;
                };
            }

            if(D == 3) {
                C.GridFunc = delegate () {
                    double[] Xnodes = GenericBlas.Linspace(-xSize, xSize, xkelem + 1);
                    double[] Ynodes = GenericBlas.Linspace(-ySize, ySize, ykelem + 1);
                    double[] Znodes = GenericBlas.Linspace(0, zSize, zkelem + 1);
                    var grd = Grid3D.Cartesian3DGrid(Xnodes, Ynodes, Znodes);

                    grd.EdgeTagNames.Add(1, "navierslip_linear_lower");
                    grd.EdgeTagNames.Add(2, "navierslip_linear_upper");
                    grd.EdgeTagNames.Add(3, "navierslip_linear_left");
                    grd.EdgeTagNames.Add(4, "navierslip_linear_right");
                    grd.EdgeTagNames.Add(5, "navierslip_linear_front");
                    grd.EdgeTagNames.Add(6, "navierslip_linear_back");

                    grd.DefineEdgeTags(delegate (double[] X) {
                        byte et = 0;
                        if(Math.Abs(X[2]) <= 1.0e-8)
                            et = 1;
                        if(Math.Abs(X[2] - zSize) <= 1.0e-8)
                            et = 2;
                        if(Math.Abs(X[0] + xSize) <= 1.0e-8)
                            et = 3;
                        if(Math.Abs(X[0] - xSize) <= 1.0e-8)
                            et = 4;
                        if(Math.Abs(X[1] + ySize) <= 1.0e-8)
                            et = 5;
                        if(Math.Abs(X[1] - ySize) <= 1.0e-8)
                            et = 6;

                        return et;
                    });

                    return grd;
                };
            }

            #endregion


            // Initial Values
            // ==============
            #region init

            double R = (scale * 5.0) / 2.0;

            Func<double[], double> PhiFunc = X => -1.0;
            if(D == 2) {
                PhiFunc = (X => ((X[0] - 0.0).Pow2() + (X[1] - 0.0).Pow2()).Sqrt() - R);
            }

            if(D == 3) {
                PhiFunc = (X => ((X[0] - 0.0).Pow2() + (X[1] - 0.0).Pow2()).Sqrt() - R);  //zylinder
                //PhiFunc = (X => ((X[0] - 0.0).Pow2() + (X[1] - 0.0).Pow2() + (X[2] - 0.0).Pow2()).Sqrt() - R);  //sphere
            }

            C.InitialValues_Evaluators.Add("Phi", PhiFunc);

            double pJump = sigma / R;
            C.InitialValues_Evaluators.Add("Pressure#A", X => 0.0);
            C.InitialValues_Evaluators.Add("Pressure#B", X => 0.0);

            #endregion


            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("navierslip_linear_lower");
            C.AddBoundaryValue("navierslip_linear_upper");
            C.AddBoundaryValue("navierslip_linear_left");
            C.AddBoundaryValue("navierslip_linear_right");

            if(D == 3) {
                C.AddBoundaryValue("navierslip_linear_front");
                C.AddBoundaryValue("navierslip_linear_back");
            }

            #endregion


            // misc. solver options
            // ====================
            #region solver

            C.ComputeEnergyProperties = false;

            C.LSContiProjectionMethod = Solution.LevelSetTools.ContinuityProjectionOption.None;
            C.LinearSolver.MaxSolverIterations = 50;
            C.NonLinearSolver.MaxSolverIterations = 50;
            //C.Solver_MaxIterations = 50;
            C.NonLinearSolver.ConvergenceCriterion = 1e-8;
            C.LinearSolver.ConvergenceCriterion = 1e-8;
            //C.Solver_ConvergenceCriterion = 1e-8;
            C.LevelSet_ConvergenceCriterion = 1e-6;

            //C.AdvancedDiscretizationOptions.ViscosityMode = ViscosityMode.Standard;

            C.Option_LevelSetEvolution = (compMode == AppControl._TimesteppingMode.Steady) ? LevelSetEvolution.None : LevelSetEvolution.FastMarching;
            C.AdvancedDiscretizationOptions.FilterConfiguration = CurvatureAlgorithms.FilterConfiguration.NoFilter;

            C.AdvancedDiscretizationOptions.SST_isotropicMode = Solution.XNSECommon.SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_ContactLine;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.Timestepper_BDFinit = TimeStepperInit.SingleInit;
            C.Timestepper_LevelSetHandling = (compMode == AppControl._TimesteppingMode.Steady) ? LevelSetHandling.None : LevelSetHandling.LieSplitting;

            C.TimesteppingMode = compMode;
            double dt = 3e-3;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 1000;
            C.saveperiod = 1;

            #endregion


            return C;

        }



        public static XNSE_Control LateralAdhesionForceGrid() {

            XNSE_Control C = new XNSE_Control();

            AppControl._TimesteppingMode compMode = AppControl._TimesteppingMode.Steady;

            // basic database options
            // ======================
            #region db

            C.savetodb = false;

            #endregion


            // DG degrees
            // ==========
            #region degrees

            int p = 2;

            C.FieldOptions.Add("VelocityX", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("VelocityY", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("GravityY", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Pressure", new FieldOpts() {
                Degree = p - 1,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("PhiDG", new FieldOpts() {
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Phi", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            C.FieldOptions.Add("Curvature", new FieldOpts() {
                Degree = p,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });

            #endregion


            // Physical Parameters
            // ===================
            #region physics

            C.PhysicalParameters.IncludeConvection = false;

            #endregion


            // grid generation
            // ===============
            #region grid

            double L = 1.0;
            double H = 1.0;

            double h = 0.3;
            double b = 0.01;

            C.GridFunc = delegate () {

                var TestSecLeft_p1 = new double[2] { -L, 0 };
                var TestSecLeft_p2 = new double[2] { -b/2, H };
                var TestSecLeft = new GridCommons.GridBox(TestSecLeft_p1, TestSecLeft_p2, 10, 10);

                var TestSecCenter_p1 = new double[2] { -b/2, 0 };
                var TestSecCenter_p2 = new double[2] { b/2, h };
                var TestSecCenter = new GridCommons.GridBox(TestSecCenter_p1, TestSecCenter_p2, 1, 6);

                var TestSecRight_p1 = new double[2] { b/2, 0 };
                var TestSecRight_p2 = new double[2] { L, H };
                var TestSecRight = new GridCommons.GridBox(TestSecRight_p1, TestSecRight_p2, 10, 10);


                var grd = Grid2D.HangingNodes2D(TestSecLeft, TestSecCenter, TestSecRight);

                grd.EdgeTagNames.Add(1, "navierslip_linear_lower");
                grd.EdgeTagNames.Add(2, "pressure_outlet_upper");
                grd.EdgeTagNames.Add(3, "pressure_outlet_left");
                grd.EdgeTagNames.Add(4, "pressure_outlet_right");
                grd.EdgeTagNames.Add(5, "navierslip_linear_sensor");

                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if(Math.Abs(X[1] + 0) <= 1.0e-8)
                        et = 1;
                    if(Math.Abs(X[1] - H) <= 1.0e-8 && ((X[0] >= b/2) || (X[0] <= -b/2)) )
                        et = 2;
                    if(Math.Abs(X[0] + L) <= 1.0e-8)
                        et = 3;
                    if(Math.Abs(X[0] - L) <= 1.0e-8)
                        et = 4;
                    if(Math.Abs(X[1] - h) <= 1.0e-8 && (X[0] >= -b/2) && (X[0] <= b/2) )
                        et = 5;
                    if(Math.Abs(X[0] - b/2) <= 1.0e-8 && (X[1] >= h))
                        et = 5;
                    if(Math.Abs(X[0] + b/2) <= 1.0e-8 && (X[1] >= h))
                        et = 5;

                    return et;
                });

                return grd;
            };


            #endregion


            // Initial Values
            // ==============
            #region init

            Func<double[], double> PhiFunc = X => -1.0;

            C.InitialValues_Evaluators.Add("Phi", PhiFunc);

            #endregion


            // boundary conditions
            // ===================
            #region BC

            C.AddBoundaryValue("navierslip_linear_lower");
            C.AddBoundaryValue("pressure_outlet_upper");
            C.AddBoundaryValue("pressure_outlet_left");
            C.AddBoundaryValue("pressure_outlet_right");

            C.AddBoundaryValue("navierslip_linear_sensor");

            #endregion


            // misc. solver options
            // ====================
            #region solver

            C.ComputeEnergyProperties = false;

            C.Option_LevelSetEvolution = LevelSetEvolution.None;

            C.LSContiProjectionMethod = ContinuityProjectionOption.None;

            #endregion


            // Timestepping
            // ============
            #region time

            C.TimeSteppingScheme = TimeSteppingScheme.ImplicitEuler;
            C.Timestepper_BDFinit = TimeStepperInit.SingleInit;
            C.Timestepper_LevelSetHandling = LevelSetHandling.None;

            C.TimesteppingMode = compMode;
            double dt = 1.0;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000;
            C.NoOfTimesteps = 1000;
            C.saveperiod = 1;

            #endregion


            return C;

        }

    }
}
