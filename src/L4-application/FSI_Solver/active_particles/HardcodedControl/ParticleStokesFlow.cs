﻿/* =======================================================================
Copyright 2019 Technische Universitaet Darmstadt, Fachgebiet fuer Stroemungsdynamik (chair of fluid dynamics)

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
using BoSSS.Platform;
using BoSSS.Solution.Control;
using BoSSS.Foundation.Grid;
using System.Diagnostics;
using BoSSS.Solution.AdvancedSolvers;
using ilPSP.Utils;
using BoSSS.Foundation.Grid.Classic;
using ilPSP;
using BoSSS.Solution.XdgTimestepping;

namespace BoSSS.Application.FSI_Solver {
    public class ParticleStokesFlow : IBM_Solver.HardcodedTestExamples {
        public static FSI_Control ParticleUnderGravity(int k = 2, int MeshFactor = 1) {
            FSI_Control C = new FSI_Control();

            // General scaling parameter
            // =============================
            const double BaseSize = 1.0;

            // basic database options
            // =============================
            C.DbPath = @"\\hpccluster\hpccluster-scratch\deussen\cluster_db\straightChannel";
            C.savetodb = false;
            C.saveperiod = 1;
            C.ProjectName = "ParticleUnderGravity";
            C.ProjectDescription = "Active";
            C.SessionName = C.ProjectName;
            C.Tags.Add("with immersed boundary method");

            // DG degrees
            // =============================
            C.SetDGdegree(k);

            // Grid 
            // =============================
            //Generating grid
            C.GridFunc = delegate {
                int q = new int(); // #Cells in x-dircetion + 1
                int r = new int(); // #Cells in y-dircetion + 1

                q = 30 * MeshFactor;
                r = 60 * MeshFactor;

                double[] Xnodes = GenericBlas.Linspace(-6 * BaseSize, 6 * BaseSize, q);
                double[] Ynodes = GenericBlas.Linspace(-12 * BaseSize, 12 * BaseSize, r);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: true, periodicY: true);

                grd.EdgeTagNames.Add(1, "Pressure_Outlet_left");
                grd.EdgeTagNames.Add(2, "Pressure_Outlet_right");
                grd.EdgeTagNames.Add(3, "Wall_lower");
                grd.EdgeTagNames.Add(4, "Pressure_Outlet_upper");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[0] - (-4 * BaseSize)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] + (-4 * BaseSize)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[1] - (-0 * BaseSize)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[1] + (-16 * BaseSize)) <= 1.0e-8)
                        et = 4;

                    Debug.Assert(et != 0);
                    return et;
                });

                Console.WriteLine("Cells:" + grd.NumberOfCells);

                return grd;
            };


            // Mesh refinement
            // =============================
            C.AdaptiveMeshRefinement = true;
            C.RefinementLevel = 2;
            C.maxCurvature = 2;


            // Boundary conditions
            // =============================
            C.AddBoundaryValue("Pressure_Outlet_left");
            C.AddBoundaryValue("Pressure_Outlet_right");
            C.AddBoundaryValue("Wall_lower");
            C.AddBoundaryValue("Pressure_Outlet_upper");

            // Fluid Properties
            // =============================
            C.PhysicalParameters.rho_A = 1;//pg/(mum^3)
            C.PhysicalParameters.mu_A = 1;//pg(mum*s)
            C.PhysicalParameters.Material = true;


            // Particle Properties
            // =============================   
            // Defining particles
            C.Particles = new List<Particle>();
            int numOfParticles = 1;
            for (int d = 0; d < numOfParticles; d++) {
                C.Particles.Add(new Particle_Sphere( 1, new double[] { 0, 0 }, startAngl: 0) {
                    particleDensity = 11.01,
                    GravityVertical = -9.81,
                    useAddaptiveUnderrelaxation = true,
                    underrelaxation_factor = 5,
                    clearSmallValues = true,
                    UseAddedDamping = false
                });
            }

            // Quadrature rules
            // =============================   
            C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Saye;

            //Initial Values
            // =============================   
            //C.InitialValues_Evaluators.Add("Phi", X => phiComplete(X, 0));
            C.InitialValues_Evaluators.Add("VelocityX", X => 0);
            C.InitialValues_Evaluators.Add("VelocityY", X => 0);


            // For restart
            // =============================  
            //C.RestartInfo = new Tuple<Guid, TimestepNumber>(new Guid("42c82f3c-bdf1-4531-8472-b65feb713326"), 400);
            //C.GridGuid = new Guid("f1659eb6 -b249-47dc-9384-7ee9452d05df");


            // Physical Parameters
            // =============================  
            C.PhysicalParameters.IncludeConvection = false;


            // misc. solver options
            // =============================  
            C.AdvancedDiscretizationOptions.PenaltySafety = 4;
            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            C.LevelSetSmoothing = false;
            C.NonLinearSolver.MaxSolverIterations = 1000;
            C.NonLinearSolver.MinSolverIterations = 1;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 1000;
            C.LinearSolver.MinSolverIterations = 1;
            C.forceAndTorqueConvergenceCriterion = 1e-4;
            C.LSunderrelax = 1.0;


            // Coupling Properties
            // =============================
            C.Timestepper_LevelSetHandling = LevelSetHandling.LieSplitting;
            C.LSunderrelax = 1;
            C.max_iterations_fully_coupled = 100000;


            // Timestepping
            // =============================  
            C.Timestepper_Scheme = IBM_Solver.IBM_Control.TimesteppingScheme.BDF2;
            double dt = 1e-2;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 60000;
            C.NoOfTimesteps = 600000000;

            // haben fertig...
            // ===============

            return C;
        }

        public static FSI_Control TwoParticleUnderGravity(int k = 2, double cellAgg = 0.2, double muA = 1e4, double timestepX = 1e-3) {
            FSI_Control C = new FSI_Control();

            // General scaling parameter
            // =============================
            const double BaseSize = 1.0;

            // basic database options
            // =============================
            C.DbPath = @"\\hpccluster\hpccluster-scratch\deussen\cluster_db\straightChannel";
            C.savetodb = false;
            C.saveperiod = 1;
            C.ProjectName = "ParticleUnderGravity";
            C.ProjectDescription = "Active";
            C.SessionName = C.ProjectName;
            C.Tags.Add("with immersed boundary method");

            // DG degrees
            // =============================
            C.SetDGdegree(k);

            // Grid 
            // =============================
            //Generating grid
            C.GridFunc = delegate {
                int q = new int(); // #Cells in x-dircetion + 1
                int r = new int(); // #Cells in y-dircetion + 1

                q = 40;
                r = 80;

                double[] Xnodes = GenericBlas.Linspace(-4 * BaseSize, 4 * BaseSize, q);
                double[] Ynodes = GenericBlas.Linspace(-8 * BaseSize, 8 * BaseSize, r);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: true, periodicY: true);

                grd.EdgeTagNames.Add(1, "Wall_left");
                grd.EdgeTagNames.Add(2, "Wall_right");
                grd.EdgeTagNames.Add(3, "Wall_lower");
                grd.EdgeTagNames.Add(4, "Pressure_Outlet_upper");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[0] - (-4 * BaseSize)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] + (-4 * BaseSize)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[1] - (-8 * BaseSize)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[1] + (-8 * BaseSize)) <= 1.0e-8)
                        et = 4;

                    Debug.Assert(et != 0);
                    return et;
                });

                Console.WriteLine("Cells:" + grd.NumberOfCells);

                return grd;
            };


            // Mesh refinement
            // =============================
            C.AdaptiveMeshRefinement = false;
            C.RefinementLevel = 2;
            C.maxCurvature = 2;


            // Boundary conditions
            // =============================
            C.AddBoundaryValue("Wall_left");
            C.AddBoundaryValue("Wall_right");
            C.AddBoundaryValue("Wall_lower");
            C.LowerWallFullyPlastic = true;
            C.AddBoundaryValue("Pressure_Outlet_upper");

            // Fluid Properties
            // =============================
            C.PhysicalParameters.rho_A = 1;//pg/(mum^3)
            C.PhysicalParameters.mu_A = 1e-1;//pg(mum*s)
            C.PhysicalParameters.Material = true;


            // Particle Properties
            // =============================   
            // Defining particles
            C.Particles = new List<Particle>();
            int numOfParticles = 1;
            for (int d = 0; d < numOfParticles; d++) {
                C.Particles.Add(new Particle_Sphere( 0.5, new double[] { 0, 6 }, startAngl: 0) {
                    particleDensity = 7.8,
                    GravityVertical = -9.81,
                    useAddaptiveUnderrelaxation = true,
                    underrelaxation_factor = 5,
                    clearSmallValues = true,
                    UseAddedDamping = true
                });
            }
            for (int d = 0; d < numOfParticles; d++) {
                C.Particles.Add(new Particle_Sphere( 0.5, new double[] { 0, 4 }, startAngl: 0) {
                    particleDensity = 7.8,
                    GravityVertical = -9.81,
                    useAddaptiveUnderrelaxation = true,
                    underrelaxation_factor = 5,
                    clearSmallValues = true,
                    UseAddedDamping = true
                });
            }

            // Quadrature rules
            // =============================   
            C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Saye;

            //Initial Values
            // =============================   
            //C.InitialValues_Evaluators.Add("Phi", X => phiComplete(X, 0));
            C.InitialValues_Evaluators.Add("VelocityX", X => 0);
            C.InitialValues_Evaluators.Add("VelocityY", X => 0);


            // For restart
            // =============================  
            //C.RestartInfo = new Tuple<Guid, TimestepNumber>(new Guid("42c82f3c-bdf1-4531-8472-b65feb713326"), 400);
            //C.GridGuid = new Guid("f1659eb6 -b249-47dc-9384-7ee9452d05df");


            // Physical Parameters
            // =============================  
            C.PhysicalParameters.IncludeConvection = true;


            // misc. solver options
            // =============================  
            C.AdvancedDiscretizationOptions.PenaltySafety = 4;
            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            C.LevelSetSmoothing = false;
            C.NonLinearSolver.MaxSolverIterations = 1000;
            C.NonLinearSolver.MinSolverIterations = 1;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 1000;
            C.LinearSolver.MinSolverIterations = 1;
            C.forceAndTorqueConvergenceCriterion = 1e-2;
            C.LSunderrelax = 1.0;


            // Coupling Properties
            // =============================
            C.Timestepper_LevelSetHandling = LevelSetHandling.FSI_LieSplittingFullyCoupled;
            C.LSunderrelax = 1;
            C.max_iterations_fully_coupled = 100000;


            // Timestepping
            // =============================  
            C.instationarySolver = true;
            C.Timestepper_Scheme = IBM_Solver.IBM_Control.TimesteppingScheme.BDF2;
            double dt = 1e-3;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 600;
            C.NoOfTimesteps = 60000000;

            // haben fertig...
            // ===============

            return C;
        }

        public static FSI_Control WetParticleCollision(int k = 3, double DensityFactor = 1) {
            FSI_Control C = new FSI_Control();

            // General scaling parameter
            // =============================
            const double BaseSize = 1.0;

            // basic database options
            // =============================
            C.DbPath = @"D:\BoSSS_databases\wetParticleCollision";
            C.savetodb = true;
            C.saveperiod = 1;
            C.ProjectName = "ParticleUnderGravity";
            C.ProjectDescription = "Active";
            C.SessionName = C.ProjectName;
            C.Tags.Add("with immersed boundary method");

            // DG degrees
            // =============================
            C.SetDGdegree(k);

            // Grid 
            // =============================
            //Generating grid
            C.GridFunc = delegate {
                int q = new int(); // #Cells in x-dircetion + 1
                int r = new int(); // #Cells in y-dircetion + 1

                q = 5;
                r = 5;

                double[] Xnodes = GenericBlas.Linspace(-3 * BaseSize, 3 * BaseSize, q);
                double[] Ynodes = GenericBlas.Linspace(-1 * BaseSize, 5 * BaseSize, r);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false, periodicY: false);

                grd.EdgeTagNames.Add(1, "Pressure_Outlet_left");
                grd.EdgeTagNames.Add(2, "Pressure_Outlet_right");
                grd.EdgeTagNames.Add(3, "Wall_lower");
                grd.EdgeTagNames.Add(4, "Pressure_Outlet_upper");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[0] - (-3 * BaseSize)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] + (-3 * BaseSize)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[1] - (-1 * BaseSize)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[1] + (-5 * BaseSize)) <= 1.0e-8)
                        et = 4;

                    Debug.Assert(et != 0);
                    return et;
                });

                Console.WriteLine("Cells:" + grd.NumberOfCells);

                return grd;
            };


            // Mesh refinement
            // =============================
            C.AdaptiveMeshRefinement = true;
            C.RefinementLevel = 5;


            // Boundary conditions
            // =============================
            C.AddBoundaryValue("Pressure_Outlet_left");
            C.AddBoundaryValue("Pressure_Outlet_right");
            C.AddBoundaryValue("Wall_lower");
            C.AddBoundaryValue("Pressure_Outlet_upper");

            // Fluid Properties
            // =============================
            C.PhysicalParameters.rho_A = 1;//pg/(mum^3)
            C.PhysicalParameters.mu_A = 1;//pg(mum*s)
            C.PhysicalParameters.Material = true;


            // Particle Properties
            // =============================   
            // Defining particles
            C.Particles = new List<Particle>();
            int numOfParticles = 1;
            for (int d = 0; d < numOfParticles; d++) {
                C.Particles.Add(new Particle_Sphere( 0.5, new double[] { 0.0, 0.0 }, startAngl: 0) {
                    particleDensity = 1 * DensityFactor,
                    GravityVertical = -9.81,
                    useAddaptiveUnderrelaxation = true,
                    underrelaxation_factor = 5,
                    clearSmallValues = true,
                    UseAddedDamping = false
                });
            }
            C.Particles[0].translationalVelocity[0][1] = -0.5;
            // Quadrature rules
            // =============================   
            C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Saye;

            //Initial Values
            // =============================   
            //C.InitialValues_Evaluators.Add("Phi", X => phiComplete(X, 0));
            C.InitialValues_Evaluators.Add("VelocityX", X => 0);
            C.InitialValues_Evaluators.Add("VelocityY", X => 0);


            // For restart
            // =============================  
            //C.RestartInfo = new Tuple<Guid, TimestepNumber>(new Guid("42c82f3c-bdf1-4531-8472-b65feb713326"), 400);
            //C.GridGuid = new Guid("f1659eb6 -b249-47dc-9384-7ee9452d05df");


            // Physical Parameters
            // =============================  
            C.PhysicalParameters.IncludeConvection = false;


            // misc. solver options
            // =============================  
            C.AdvancedDiscretizationOptions.PenaltySafety = 4;
            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            C.LevelSetSmoothing = false;
            C.NonLinearSolver.MaxSolverIterations = 1000;
            C.NonLinearSolver.MinSolverIterations = 1;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 1000;
            C.LinearSolver.MinSolverIterations = 1;
            C.forceAndTorqueConvergenceCriterion = 1e-2;
            C.LSunderrelax = 1.0;


            // Coupling Properties
            // =============================
            C.Timestepper_LevelSetHandling = LevelSetHandling.FSI_LieSplittingFullyCoupled;
            C.LSunderrelax = 1;
            C.max_iterations_fully_coupled = 100000;


            // Timestepping
            // =============================  
            C.Timestepper_Scheme = IBM_Solver.IBM_Control.TimesteppingScheme.BDF2;
            double dt = 1e-3;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 60;
            C.NoOfTimesteps = 5000;

            // haben fertig...
            // ===============

            return C;
        }

        public static FSI_Control ActiveParticles_Cylinder(int k = 1) {
            FSI_Control C = new FSI_Control();

            // General scaling parameter
            // =============================
            const double BaseSize = 1.0;

            // basic database options
            // =============================
            C.DbPath = @"\\hpccluster\hpccluster-scratch\deussen\cluster_db\ActiveParticles_Cylinder";
            C.savetodb = false;
            C.saveperiod = 1;
            C.ProjectName = "activeRod_noBackroundFlow";
            C.ProjectDescription = "Active";
            C.SessionName = C.ProjectName;
            C.Tags.Add("with immersed boundary method");

            // DG degrees
            // =============================
            C.SetDGdegree(k);

            // Grid 
            // =============================
            //Generating grid
            C.GridFunc = delegate {
                int q = new int(); // #Cells in x-dircetion + 1
                int r = new int(); // #Cells in y-dircetion + 1

                q = 180;
                r = 90;

                double[] Xnodes = GenericBlas.Linspace(-9 * BaseSize, 9 * BaseSize, q);
                double[] Ynodes = GenericBlas.Linspace(-3 * BaseSize, 3 * BaseSize, r);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false, periodicY: false);

                grd.EdgeTagNames.Add(1, "Velocity_Inlet_left");
                grd.EdgeTagNames.Add(2, "Pressure_Outlet_right");
                grd.EdgeTagNames.Add(3, "Wall_lower");
                grd.EdgeTagNames.Add(4, "Wall_upper");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[0] - (-9 * BaseSize)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] + (-9 * BaseSize)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[1] - (-3 * BaseSize)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[1] + (-3 * BaseSize)) <= 1.0e-8)
                        et = 4;

                    Debug.Assert(et != 0);
                    return et;
                });

                Console.WriteLine("Cells:" + grd.NumberOfCells);

                return grd;
            };


            // Mesh refinement
            // =============================
            C.AdaptiveMeshRefinement = false;
            C.RefinementLevel = 2;
            C.maxCurvature = 2;


            // Boundary conditions
            // =============================
            C.AddBoundaryValue("Velocity_Inlet_left", "VelocityX", X => 1.0);
            C.AddBoundaryValue("Pressure_Outlet_right");
            C.AddBoundaryValue("Wall_lower");
            C.AddBoundaryValue("Wall_upper");

            // Fluid Properties
            // =============================
            C.PhysicalParameters.rho_A = 1;//pg/(mum^3)
            C.PhysicalParameters.mu_A = 1e2;//pg(mum*s)
            C.PhysicalParameters.Material = true;


            // Particle Properties
            // =============================   
            // Defining particles
            C.Particles = new List<Particle>();
            // The cylinder
            C.Particles.Add(new Particle_Sphere(2, new double[] { 0, 0 }, startAngl: 0) {
                particleDensity = 50,
                GravityVertical = 0,
                IncludeRotation = false,
                IncludeTranslation = false,
            });
            int numOfParticles = 3;
            for (int d = 0; d < numOfParticles; d++) {
                C.Particles.Add(new Particle_Sphere( 0.1, new double[] { -6, -2 + 2 * d }, startAngl: 0) {
                    particleDensity = 1,
                    GravityVertical = 0,
                    activeStress = 1000,
                    useAddaptiveUnderrelaxation = true,
                    underrelaxation_factor = 5,
                    clearSmallValues = true,
                    UseAddedDamping = true
                });
            }
            //for (int d = 0; d < numOfParticles + 1; d++)
            //{
            //    C.Particles.Add(new Particle_Ellipsoid(new double[] { -8, -2.5 + 1 * d }, startAngl: 0)
            //    {
            //        particleDensity = 50,
            //        length_P = 0.5,
            //        thickness_P = 0.2,
            //        GravityVertical = 0,
            //        ActiveParticle = true,
            //        ActiveStress = 10,
            //        AddaptiveUnderrelaxation = true,
            //        underrelaxation_factor = 9,
            //        ClearSmallValues = true,
            //        neglectAddedDamping = false
            //    });
            //}

            // Quadrature rules
            // =============================   
            C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Saye;

            //Initial Values
            // =============================   
            //C.InitialValues_Evaluators.Add("Phi", X => phiComplete(X, 0));
            C.InitialValues_Evaluators.Add("VelocityX", X => 0);
            C.InitialValues_Evaluators.Add("VelocityY", X => 0);


            // For restart
            // =============================  
            //C.RestartInfo = new Tuple<Guid, TimestepNumber>(new Guid("42c82f3c-bdf1-4531-8472-b65feb713326"), 400);
            //C.GridGuid = new Guid("f1659eb6 -b249-47dc-9384-7ee9452d05df");


            // Physical Parameters
            // =============================  
            C.PhysicalParameters.IncludeConvection = false;


            // misc. solver options
            // =============================  
            C.AdvancedDiscretizationOptions.PenaltySafety = 4;
            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            C.LevelSetSmoothing = false;
            C.NonLinearSolver.MaxSolverIterations = 1000;
            C.NonLinearSolver.MinSolverIterations = 1;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.LinearSolver.MaxSolverIterations = 1000;
            C.LinearSolver.MinSolverIterations = 1;
            C.forceAndTorqueConvergenceCriterion = 10;
            C.LSunderrelax = 1.0;


            // Coupling Properties
            // =============================
            C.Timestepper_LevelSetHandling = LevelSetHandling.FSI_LieSplittingFullyCoupled;
            C.LSunderrelax = 1;
            C.max_iterations_fully_coupled = 100000;


            // Timestepping
            // =============================  
            C.instationarySolver = true;
            C.Timestepper_Scheme = IBM_Solver.IBM_Control.TimesteppingScheme.BDF2;
            double dt = 1e-3;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 1000000000;
            C.NoOfTimesteps = 1000000000;

            // haben fertig...
            // ===============

            return C;
        }

        public static FSI_Control MultipleDryParticleAgainstWall(string _DbPath = null, bool MeshRefine = false) {
            FSI_Control C = new FSI_Control();

            // basic database options
            // ======================

            C.DbPath = _DbPath;
            C.savetodb = _DbPath != null;
            C.saveperiod = 1;
            C.ProjectName = "ParticleCollisionTest";
            C.ProjectDescription = "Gravity";
            C.SessionName = C.ProjectName;
            C.Tags.Add("with immersed boundary method");
            C.AdaptiveMeshRefinement = true;


            // DG degrees
            // ==========

            C.SetDGdegree(1);

            // grid and boundary conditions
            // ============================

            double[] Xnodes = GenericBlas.Linspace(-10, 10, 200);
            double[] Ynodes = GenericBlas.Linspace(-10, 10, 200);
            double h = Math.Min((Xnodes[1] - Xnodes[0]), (Ynodes[1] - Ynodes[0]));

            C.GridFunc = delegate {
                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false, periodicY: false);
                grd.EdgeTagNames.Add(1, "Wall");
                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 1;
                    return et;
                });

                return grd;
            };

            C.AddBoundaryValue("Wall");

            // Boundary values for level-set
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0.1 * 2 * Math.PI * -Math.Sin(Math.PI * 2 * 1 * t), (t) =>  0};
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0, (t) => 0 };

            // Initial Values
            // ==============

            // Coupling Properties
            C.Timestepper_LevelSetHandling = LevelSetHandling.Coupled_Once;


            // Fluid Properties
            C.PhysicalParameters.rho_A = 1;
            C.PhysicalParameters.mu_A = 0.1;

            // Particles
            // =========
            for (int i = 0; i < 9; i++) {
                for (int j = 0; j < 9; j++) {
                    C.Particles.Add(new Particle_Ellipsoid( 0.8,  0.2, new double[] { -8 + 2 * i, 8 - 2 * j }, startAngl: 23 * i - 12 * j) {
                        particleDensity = 1.0,
                    });
                    C.Particles[i + j * i].translationalVelocity[0][0] = Math.Cos((23 * i - 12 * j) / 2 * Math.PI);
                    C.Particles[i + j * i].translationalVelocity[0][1] = Math.Sin((23 * i - 12 * j) / 2 * Math.PI);
                    C.Particles[i + j * i].rotationalVelocity[0] = 0;
                }
            }


            C.pureDryCollisions = true;
            C.collisionModel = FSI_Control.CollisionModel.MomentumConservation;

            double V = 0;
            foreach (var p in C.Particles) {
                V = Math.Max(V, p.translationalVelocity[0].L2Norm());
            }

            if (V <= 0)
                throw new ArithmeticException();


            // Physical Parameters
            // ===================

            C.PhysicalParameters.IncludeConvection = true;


            // misc. solver options
            // ====================

            C.AdvancedDiscretizationOptions.PenaltySafety = 4;
            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            C.LevelSetSmoothing = false;
            C.LinearSolver.MaxSolverIterations = 10;
            C.NonLinearSolver.MaxSolverIterations = 10;
            C.LinearSolver.NoOfMultigridLevels = 1;
            C.AdaptiveMeshRefinement = MeshRefine;
            C.RefinementLevel = 1;

            // Timestepping
            // ============

            //C.Timestepper_Mode = FSI_Control.TimesteppingMode.Splitting;
            C.Timestepper_Scheme = FSI_Solver.FSI_Control.TimesteppingScheme.BDF2;

            double dt = (h / V) * (MeshRefine ? 0.5 * 0.5 * 0.5 * 0.2 : 0.1);
            C.dtMax = dt;
            C.dtMin = dt;

            C.Endtime = 100000.0 / V;
            C.NoOfTimesteps = 50000;

            // haben fertig...
            // ===============

            return C;

        }

        public static FSI_Control DeriabinaHefezelle(string _DbPath = null, int k = 2, double VelXBase = 0.0, double angle = 0.0) {
            FSI_Control C = new FSI_Control();


            const double BaseSize = 1.0;


            // basic database options
            // ======================

            //C.DbPath = @"\\dc1\userspace\deriabina\bosss_db";
            C.savetodb = false;
            C.saveperiod = 1;
            C.ProjectName = "ParticleCollisionTest";
            C.ProjectDescription = "Gravity";
            C.SessionName = C.ProjectName;
            C.Tags.Add("with immersed boundary method");
            C.AdaptiveMeshRefinement = false;
            C.SessionName = "fjkfjksdfhjk";

            C.pureDryCollisions = true;
            C.SetDGdegree(k);

            // grid and boundary conditions
            // ============================

            C.GridFunc = delegate {

                int q = new int();
                int r = new int();

                q = 20;
                r = 80;

                double[] Xnodes = GenericBlas.Linspace(-3.5 * BaseSize, 3.5 * BaseSize, q + 1);
                double[] Ynodes = GenericBlas.Linspace(1 * BaseSize, 10 * BaseSize, r + 1);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false, periodicY: false);

                grd.EdgeTagNames.Add(1, "Velocity_Inlet_left");
                grd.EdgeTagNames.Add(2, "Velocity_Inlet_right");
                grd.EdgeTagNames.Add(3, "Wall_lower");
                grd.EdgeTagNames.Add(4, "Pressure_Outlet");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[0] - (-3.5 * BaseSize)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] + (-3.5 * BaseSize)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[1] - (1 * BaseSize)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[1] + (-10 * BaseSize)) <= 1.0e-8)
                        et = 4;


                    return et;
                });

                Console.WriteLine("Cells:" + grd.NumberOfCells);

                return grd;
            };

            C.GridPartType = GridPartType.Hilbert;

            C.AddBoundaryValue("Velocity_Inlet_left", "VelocityY", X => 0);
            C.AddBoundaryValue("Velocity_Inlet_right", "VelocityY", X => 0);
            C.AddBoundaryValue("Wall_lower");
            C.AddBoundaryValue("Pressure_Outlet");

            // Boundary values for level-set
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0.1 * 2 * Math.PI * -Math.Sin(Math.PI * 2 * 1 * t), (t) =>  0};
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0, (t) => 0 };

            // Initial Values
            // ==============

            // Coupling Properties
            C.Timestepper_LevelSetHandling = LevelSetHandling.LieSplitting;

            // Fluid Properties
            C.PhysicalParameters.rho_A = 1.0;
            C.PhysicalParameters.mu_A = 0.1;
            C.CoefficientOfRestitution = 0;

            // Particle Properties
            //C.PhysicalParameters.mu_B = 0.1;
            //C.particleMass = 1;

            C.Particles.Add(new Particle_Sphere( 0.25, new double[] { 0.0, 8 }) {
                particleDensity = 1.01,
                GravityVertical = -9.81,
            });

            C.Particles[0].translationalVelocity[0][1] = -1.0;

            /*       
                               C.Particles.Add(new Particle_superEllipsoid(new double[] { 0.55, 4.5 }, startAngl: 45)
                               {
                                   particleDensity = 1,
                                   thickness_P = 0.2,
                                   length_P = 0.4,
                                   superEllipsoidExponent = 4,
                                   GravityVertical = -9.81,
                                   IncludeRotation = false,
                                   IncludeTranslation = false,
                               });

                               C.Particles.Add(new Particle_superEllipsoid(new double[] { -0.55, 4.5 }, startAngl: -45)
                               {
                                   particleDensity = 1,
                                   thickness_P = 0.2,
                                   length_P = 0.4,
                                   superEllipsoidExponent = 4,
                                   GravityVertical = -9.81,
                                   IncludeRotation = false,
                                   IncludeTranslation = false,
                               });
       */


            C.Particles.Add(new Particle_Pentagone( 0.3, new double[] { 0.45, 4.5 }, startAngl: 0) {
                particleDensity = 1,
                GravityVertical = -9.81,
                IncludeRotation = false,
                IncludeTranslation = false,
            });

            C.Particles.Add(new Particle_Pentagone( 0.3, new double[] { -0.45, 4.5 }, startAngl: 0) {
                particleDensity = 1,
                GravityVertical = -9.81,
                IncludeRotation = false,
                IncludeTranslation = false,
            });

            /*

                        C.Particles.Add(new Particle_Falle_Links(new double[] { 0.45, 4.5 }, startAngl: 0)
                        {
                            particleDensity = 1,
                            width_P = 0.1,
                            GravityVertical = -9.81,
                            IncludeRotation = false,
                            IncludeTranslation = false,
                        });


                                            C.Particles.Add(new Particle_Falle_Rechts(new double[] { -0.45, 4.5 }, startAngl: 0)
                                    {
                                        particleDensity = 1,
                                        width_P = 0.1,
                                        GravityVertical = -9.81,
                                        IncludeRotation = false,
                                        IncludeTranslation = false,
                                    });

               */

            //   C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Classic;

            //Func<double[], double, double> phiComplete = delegate (double[] X, double t) {
            //    double r = 1 * (C.Particles[0].Phi_P(X, t));
            //    if (double.IsNaN(r) || double.IsInfinity(r))
            //        throw new ArithmeticException();
            //    return r;
            //};

            //for (int i = 0;i<C.Particles.Count; i++) {
            //    phiComplete = (X,t) => phiComplete(X,t)*C.Particles[i].Phi_P(X,t);
            //}


            //Func<double[], double, double> phi = (X, t) => -(X[0] - t+X[1]);
            //C.MovementFunc = phi;         

            //C.InitialValues_Evaluators.Add("Phi", X => phiComplete(X, 0));
            //C.InitialValues_Evaluators.Add("Phi", X => -1);
            //C.InitialValues.Add("VelocityX#B", X => 1);
            C.InitialValues_Evaluators.Add("VelocityX", X => 0);
            C.InitialValues_Evaluators.Add("VelocityY", X => 0);
            //C.InitialValues.Add("Phi", X => -1);
            //C.InitialValues.Add("Phi", X => (X[0] - 0.41));

            // For restart
            //C.RestartInfo = new Tuple<Guid, TimestepNumber>(new Guid("42c82f3c-bdf1-4531-8472-b65feb713326"), 400);
            //C.GridGuid = new Guid("f1659eb6-b249-47dc-9384-7ee9452d05df");


            // Physical Parameters
            // ===================

            C.PhysicalParameters.IncludeConvection = true;

            // misc. solver options
            // ====================

            C.AdvancedDiscretizationOptions.PenaltySafety = 4;
            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            C.LevelSetSmoothing = false;
            C.LinearSolver.MaxSolverIterations = 10;
            C.NonLinearSolver.MaxSolverIterations = 10;
            C.LinearSolver.NoOfMultigridLevels = 1;


            // Timestepping
            // ============

            //C.Timestepper_Mode = FSI_Control.TimesteppingMode.Splitting;
            C.Timestepper_Scheme = FSI_Solver.FSI_Control.TimesteppingScheme.BDF2;
            double dt = 1e-3;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 15.0;
            C.NoOfTimesteps = 2000;

            // haben fertig...
            // ===============

            return C;
        }

        public static FSI_Control DeriabinaFalle(string _DbPath = null, int k = 2, double VelXBase = 0.0, double angle = 0.0) {
            FSI_Control C = new FSI_Control();


            const double BaseSize = 1.0;


            // basic database options
            // ======================

            //C.DbPath = @"\\dc1\userspace\deriabina\bosss_db";
            C.savetodb = false;
            C.saveperiod = 1;
            C.ProjectName = "ParticleCollisionTest";
            C.ProjectDescription = "Gravity";
            C.SessionName = C.ProjectName;
            C.Tags.Add("with immersed boundary method");
            C.AdaptiveMeshRefinement = false;
            C.SessionName = "fjkfjksdfhjk";

            C.pureDryCollisions = true;
            C.SetDGdegree(k);

            // grid and boundary conditions
            // ============================

            C.GridFunc = delegate {

                int q = new int();
                int r = new int();

                q = 20;
                r = 80;

                double[] Xnodes = GenericBlas.Linspace(-3.5 * BaseSize, 3.5 * BaseSize, q + 1);
                double[] Ynodes = GenericBlas.Linspace(1 * BaseSize, 10 * BaseSize, r + 1);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false, periodicY: false);

                grd.EdgeTagNames.Add(1, "Velocity_Inlet_left");
                grd.EdgeTagNames.Add(2, "Velocity_Inlet_right");
                grd.EdgeTagNames.Add(3, "Wall_lower");
                grd.EdgeTagNames.Add(4, "Pressure_Outlet");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[0] - (-3.5 * BaseSize)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] + (-3.5 * BaseSize)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[1] - (1 * BaseSize)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[1] + (-10 * BaseSize)) <= 1.0e-8)
                        et = 4;


                    return et;
                });

                Console.WriteLine("Cells:" + grd.NumberOfCells);

                return grd;
            };

            C.GridPartType = GridPartType.Hilbert;

            C.AddBoundaryValue("Velocity_Inlet_left", "VelocityY", X => 0);
            C.AddBoundaryValue("Velocity_Inlet_right", "VelocityY", X => 0);
            C.AddBoundaryValue("Wall_lower");
            C.AddBoundaryValue("Pressure_Outlet");

            // Boundary values for level-set
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0.1 * 2 * Math.PI * -Math.Sin(Math.PI * 2 * 1 * t), (t) =>  0};
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0, (t) => 0 };

            // Initial Values
            // ==============

            // Coupling Properties
            C.Timestepper_LevelSetHandling = LevelSetHandling.LieSplitting;

            // Fluid Properties
            C.PhysicalParameters.rho_A = 1.0;
            C.PhysicalParameters.mu_A = 0.1;
            C.CoefficientOfRestitution = 0;

            // Particle Properties
            //C.PhysicalParameters.mu_B = 0.1;
            //C.particleMass = 1;

            C.Particles.Add(new Particle_Sphere( 0.25, new double[] { 0.0, 8 }) {
                particleDensity = 1.01,
                GravityVertical = -9.81,
            });

            C.Particles[0].translationalVelocity[0][1] = -1.0;

            /*       
                               C.Particles.Add(new Particle_superEllipsoid(new double[] { 0.55, 4.5 }, startAngl: 45)
                               {
                                   particleDensity = 1,
                                   thickness_P = 0.2,
                                   length_P = 0.4,
                                   superEllipsoidExponent = 4,
                                   GravityVertical = -9.81,
                                   IncludeRotation = false,
                                   IncludeTranslation = false,
                               });

                               C.Particles.Add(new Particle_superEllipsoid(new double[] { -0.55, 4.5 }, startAngl: -45)
                               {
                                   particleDensity = 1,
                                   thickness_P = 0.2,
                                   length_P = 0.4,
                                   superEllipsoidExponent = 4,
                                   GravityVertical = -9.81,
                                   IncludeRotation = false,
                                   IncludeTranslation = false,
                               });
       */

            /*
                        C.Particles.Add(new Particle_Pentagone(new double[] { 0.45, 4.5 }, startAngl: 0)
                        {
                            particleDensity = 1,
                            width_P = 0.3,
                            GravityVertical = -9.81,
                            IncludeRotation = false,
                            IncludeTranslation = false,
                        });

                        C.Particles.Add(new Particle_Pentagone(new double[] { -0.45, 4.5 }, startAngl: 0)
                        {
                            particleDensity = 1,
                            width_P = 0.3,
                            GravityVertical = -9.81,
                            IncludeRotation = false,
                            IncludeTranslation = false,
                        });
            */


            C.Particles.Add(new Particle_TrapLeft( 0.1, new double[] { 0.45, 4.5 }, startAngl: 0) {
                particleDensity = 1,
                GravityVertical = -9.81,
                IncludeRotation = false,
                IncludeTranslation = false,
            });


            C.Particles.Add(new Particle_TrapRight( 0.1, new double[] { -0.45, 4.5 }, startAngl: 0) {
                particleDensity = 1,
                GravityVertical = -9.81,
                IncludeRotation = false,
                IncludeTranslation = false,
            });



            //   C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Classic;

            //Func<double[], double, double> phiComplete = delegate (double[] X, double t) {
            //    double r = 1 * (C.Particles[0].Phi_P(X, t));
            //    if (double.IsNaN(r) || double.IsInfinity(r))
            //        throw new ArithmeticException();
            //    return r;
            //};

            //for (int i = 0;i<C.Particles.Count; i++) {
            //    phiComplete = (X,t) => phiComplete(X,t)*C.Particles[i].Phi_P(X,t);
            //}


            //Func<double[], double, double> phi = (X, t) => -(X[0] - t+X[1]);
            //C.MovementFunc = phi;         

            //C.InitialValues_Evaluators.Add("Phi", X => phiComplete(X, 0));
            //C.InitialValues_Evaluators.Add("Phi", X => -1);
            //C.InitialValues.Add("VelocityX#B", X => 1);
            C.InitialValues_Evaluators.Add("VelocityX", X => 0);
            C.InitialValues_Evaluators.Add("VelocityY", X => 0);
            //C.InitialValues.Add("Phi", X => -1);
            //C.InitialValues.Add("Phi", X => (X[0] - 0.41));

            // For restart
            //C.RestartInfo = new Tuple<Guid, TimestepNumber>(new Guid("42c82f3c-bdf1-4531-8472-b65feb713326"), 400);
            //C.GridGuid = new Guid("f1659eb6-b249-47dc-9384-7ee9452d05df");


            // Physical Parameters
            // ===================

            C.PhysicalParameters.IncludeConvection = true;

            // misc. solver options
            // ====================

            C.AdvancedDiscretizationOptions.PenaltySafety = 4;
            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            C.LevelSetSmoothing = false;
            C.LinearSolver.MaxSolverIterations = 10;
            C.NonLinearSolver.MaxSolverIterations = 10;
            C.LinearSolver.NoOfMultigridLevels = 1;


            // Timestepping
            // ============

            //C.Timestepper_Mode = FSI_Control.TimesteppingMode.Splitting;
            C.Timestepper_Scheme = FSI_Solver.FSI_Control.TimesteppingScheme.BDF2;
            double dt = 1e-3;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 15.0;
            C.NoOfTimesteps = 2000;

            // haben fertig...
            // ===============

            return C;
        }

        public static FSI_Control DeriabinaPentagoneFalle(string _DbPath = null, int k = 2, double VelXBase = 0.0, double angle = 0.0) {
            FSI_Control C = new FSI_Control();


            const double BaseSize = 1.0;


            // basic database options
            // ======================

            //C.DbPath = @"\\dc1\userspace\deriabina\bosss_db";
            C.savetodb = false;
            C.saveperiod = 1;
            C.ProjectName = "ParticleCollisionTest";
            C.ProjectDescription = "Gravity";
            C.SessionName = C.ProjectName;
            C.Tags.Add("with immersed boundary method");
            C.AdaptiveMeshRefinement = false;
            C.SessionName = "fjkfjksdfhjk";

            C.pureDryCollisions = true;
            C.SetDGdegree(k);

            // grid and boundary conditions
            // ============================

            C.GridFunc = delegate {

                int q = new int();
                int r = new int();

                q = 20;
                r = 80;

                double[] Xnodes = GenericBlas.Linspace(-3.5 * BaseSize, 3.5 * BaseSize, q + 1);
                double[] Ynodes = GenericBlas.Linspace(1 * BaseSize, 10 * BaseSize, r + 1);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false, periodicY: false);

                grd.EdgeTagNames.Add(1, "Velocity_Inlet_left");
                grd.EdgeTagNames.Add(2, "Velocity_Inlet_right");
                grd.EdgeTagNames.Add(3, "Wall_lower");
                grd.EdgeTagNames.Add(4, "Pressure_Outlet");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[0] - (-3.5 * BaseSize)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] + (-3.5 * BaseSize)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[1] - (1 * BaseSize)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[1] + (-10 * BaseSize)) <= 1.0e-8)
                        et = 4;


                    return et;
                });

                Console.WriteLine("Cells:" + grd.NumberOfCells);

                return grd;
            };

            C.GridPartType = GridPartType.Hilbert;

            C.AddBoundaryValue("Velocity_Inlet_left", "VelocityY", X => 0);
            C.AddBoundaryValue("Velocity_Inlet_right", "VelocityY", X => 0);
            C.AddBoundaryValue("Wall_lower");
            C.AddBoundaryValue("Pressure_Outlet");

            // Boundary values for level-set
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0.1 * 2 * Math.PI * -Math.Sin(Math.PI * 2 * 1 * t), (t) =>  0};
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0, (t) => 0 };

            // Initial Values
            // ==============

            // Coupling Properties
            C.Timestepper_LevelSetHandling = LevelSetHandling.LieSplitting;

            // Fluid Properties
            C.PhysicalParameters.rho_A = 1.0;
            C.PhysicalParameters.mu_A = 0.1;
            C.CoefficientOfRestitution = 0;

            // Particle Properties
            //C.PhysicalParameters.mu_B = 0.1;
            //C.particleMass = 1;

            C.Particles.Add(new Particle_Sphere( 0.25, new double[] { 0.0, 8 }) {
                particleDensity = 1.01,
                GravityVertical = -9.81,
            });

            C.Particles[0].translationalVelocity[0][1] = -1.0;

            /*       
                               C.Particles.Add(new Particle_superEllipsoid(new double[] { 0.55, 4.5 }, startAngl: 45)
                               {
                                   particleDensity = 1,
                                   thickness_P = 0.2,
                                   length_P = 0.4,
                                   superEllipsoidExponent = 4,
                                   GravityVertical = -9.81,
                                   IncludeRotation = false,
                                   IncludeTranslation = false,
                               });

                               C.Particles.Add(new Particle_superEllipsoid(new double[] { -0.55, 4.5 }, startAngl: -45)
                               {
                                   particleDensity = 1,
                                   thickness_P = 0.2,
                                   length_P = 0.4,
                                   superEllipsoidExponent = 4,
                                   GravityVertical = -9.81,
                                   IncludeRotation = false,
                                   IncludeTranslation = false,
                               });
       */


            C.Particles.Add(new Particle_Pentagone( 0.3, new double[] { 0.45, 4.5 }, startAngl: 0) {
                particleDensity = 1,
                GravityVertical = -9.81,
                IncludeRotation = false,
                IncludeTranslation = false,
            });

            C.Particles.Add(new Particle_Pentagone( 0.3, new double[] { -0.45, 4.5 }, startAngl: 0) {
                particleDensity = 1,
                GravityVertical = -9.81,
                IncludeRotation = false,
                IncludeTranslation = false,
            });


            /*
                        C.Particles.Add(new Particle_Falle_Links(new double[] { 0.45, 4.5 }, startAngl: 0)
                        {
                            particleDensity = 1,
                            width_P = 0.1,
                            GravityVertical = -9.81,
                            IncludeRotation = false,
                            IncludeTranslation = false,
                        });


                        C.Particles.Add(new Particle_Falle_Rechts(new double[] { -0.45, 4.5 }, startAngl: 0)
                        {
                            particleDensity = 1,
                            width_P = 0.1,
                            GravityVertical = -9.81,
                            IncludeRotation = false,
                            IncludeTranslation = false,
                        });

            */

            //   C.CutCellQuadratureType = Foundation.XDG.XQuadFactoryHelper.MomentFittingVariants.Classic;

            //Func<double[], double, double> phiComplete = delegate (double[] X, double t) {
            //    double r = 1 * (C.Particles[0].Phi_P(X, t));
            //    if (double.IsNaN(r) || double.IsInfinity(r))
            //        throw new ArithmeticException();
            //    return r;
            //};

            //for (int i = 0;i<C.Particles.Count; i++) {
            //    phiComplete = (X,t) => phiComplete(X,t)*C.Particles[i].Phi_P(X,t);
            //}


            //Func<double[], double, double> phi = (X, t) => -(X[0] - t+X[1]);
            //C.MovementFunc = phi;         

            //C.InitialValues_Evaluators.Add("Phi", X => phiComplete(X, 0));
            //C.InitialValues_Evaluators.Add("Phi", X => -1);
            //C.InitialValues.Add("VelocityX#B", X => 1);
            C.InitialValues_Evaluators.Add("VelocityX", X => 0);
            C.InitialValues_Evaluators.Add("VelocityY", X => 0);
            //C.InitialValues.Add("Phi", X => -1);
            //C.InitialValues.Add("Phi", X => (X[0] - 0.41));

            // For restart
            //C.RestartInfo = new Tuple<Guid, TimestepNumber>(new Guid("42c82f3c-bdf1-4531-8472-b65feb713326"), 400);
            //C.GridGuid = new Guid("f1659eb6-b249-47dc-9384-7ee9452d05df");


            // Physical Parameters
            // ===================

            C.PhysicalParameters.IncludeConvection = true;

            // misc. solver options
            // ====================

            C.AdvancedDiscretizationOptions.PenaltySafety = 4;
            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            C.LevelSetSmoothing = true;
            C.LinearSolver.MaxSolverIterations = 10;
            C.NonLinearSolver.MaxSolverIterations = 10;
            C.LinearSolver.NoOfMultigridLevels = 1;


            // Timestepping
            // ============

            //C.Timestepper_Mode = FSI_Control.TimesteppingMode.Splitting;
            C.Timestepper_Scheme = FSI_Solver.FSI_Control.TimesteppingScheme.BDF2;
            double dt = 1e-3;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 15.0;
            C.NoOfTimesteps = 2000;

            // haben fertig...
            // ===============

            return C;
        }

        public static FSI_Control FalleTest(string _DbPath = null, int k = 2, double VelXBase = 0.0, double angle = 0.0) {
            FSI_Control C = new FSI_Control();


            const double BaseSize = 1.0;


            // basic database options
            // ======================

            //C.DbPath = @"\\dc1\userspace\deriabina\bosss_db";
            C.savetodb = false;
            C.saveperiod = 1;
            C.ProjectName = "ParticleCollisionTest";
            C.ProjectDescription = "Gravity";
            C.SessionName = C.ProjectName;
            C.Tags.Add("with immersed boundary method");
            C.AdaptiveMeshRefinement = false;
            C.SessionName = "fjkfjksdfhjk";
            C.RefinementLevel = 1;

            C.pureDryCollisions = false;
            C.SetDGdegree(k);

            // grid and boundary conditions
            // ============================

            C.GridFunc = delegate {

                int q = new int();
                int r = new int();

                q = 40;
                r = 40;

                double[] Xnodes = GenericBlas.Linspace(-4.0 * BaseSize, 4.0 * BaseSize, q + 1);
                double[] Ynodes = GenericBlas.Linspace(2 * BaseSize, 10 * BaseSize, r + 1);

                var grd = Grid2D.Cartesian2DGrid(Xnodes, Ynodes, periodicX: false, periodicY: false);

                grd.EdgeTagNames.Add(1, "Pressure_Outlet_left");
                grd.EdgeTagNames.Add(2, "Pressure_Outlet_right");
                grd.EdgeTagNames.Add(3, "Pressure_Outlet_lower");
                grd.EdgeTagNames.Add(4, "Pressure_Outlet_upper");


                grd.DefineEdgeTags(delegate (double[] X) {
                    byte et = 0;
                    if (Math.Abs(X[0] - (-4 * BaseSize)) <= 1.0e-8)
                        et = 1;
                    if (Math.Abs(X[0] + (-4 * BaseSize)) <= 1.0e-8)
                        et = 2;
                    if (Math.Abs(X[1] - (2 * BaseSize)) <= 1.0e-8)
                        et = 3;
                    if (Math.Abs(X[1] + (-10 * BaseSize)) <= 1.0e-8)
                        et = 4;


                    return et;
                });

                Console.WriteLine("Cells:" + grd.NumberOfCells);

                return grd;
            };

            C.GridPartType = GridPartType.Hilbert;

            C.AddBoundaryValue("Pressure_Outlet_left");
            C.AddBoundaryValue("Pressure_Outlet_right");
            C.AddBoundaryValue("Pressure_Outlet_lower");
            C.AddBoundaryValue("Pressure_Outlet_upper");

            // Boundary values for level-set
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0.1 * 2 * Math.PI * -Math.Sin(Math.PI * 2 * 1 * t), (t) =>  0};
            //C.BoundaryFunc = new Func<double, double>[] { (t) => 0, (t) => 0 };

            // Initial Values
            // ==============

            // Coupling Properties
            C.Timestepper_LevelSetHandling = LevelSetHandling.FSI_LieSplittingFullyCoupled;

            // Fluid Properties
            C.PhysicalParameters.rho_A = 1.0;
            C.PhysicalParameters.mu_A = 1.0;
            C.CoefficientOfRestitution = 1.0;

            // Particle Properties
            //C.PhysicalParameters.mu_B = 0.1;
            //C.particleMass = 1;

            C.Particles.Add(new Particle_Sphere( 0.35, new double[] { 0.0, 6.00 }) {
                particleDensity = 1.1,
                GravityVertical = -9.81,
                useAddaptiveUnderrelaxation = true,
                underrelaxation_factor = 5,
                clearSmallValues = true,
                UseAddedDamping = true
            });
            C.Particles[0].translationalVelocity[0][1] = -0.25;
            C.Particles.Add(new Particle_TrapLeft( 0.15, new double[] { 0.975, 4.75 }, startAngl: 0) {
                particleDensity = 1,
                GravityVertical = 0,
                IncludeRotation = false,
                IncludeTranslation = false,
            });


            C.Particles.Add(new Particle_TrapRight( 0.15, new double[] { -0.975, 4.75 }, startAngl: 0) {
                particleDensity = 1,
                GravityVertical = 0,
                IncludeRotation = false,
                IncludeTranslation = false,
            });

            C.InitialValues_Evaluators.Add("VelocityX", X => 0);
            C.InitialValues_Evaluators.Add("VelocityY", X => 0);
            C.forceAndTorqueConvergenceCriterion = 1e-4;


            // Physical Parameters
            // ===================

            C.PhysicalParameters.IncludeConvection = false;

            // misc. solver options
            // ====================

            C.AdvancedDiscretizationOptions.PenaltySafety = 4;
            C.AdvancedDiscretizationOptions.CellAgglomerationThreshold = 0.2;
            C.LevelSetSmoothing = false;
            C.LinearSolver.MaxSolverIterations = 10;
            C.NonLinearSolver.MaxSolverIterations = 10;
            C.LinearSolver.NoOfMultigridLevels = 1;


            // Timestepping
            // ============

            //C.Timestepper_Mode = FSI_Control.TimesteppingMode.Splitting;
            C.Timestepper_Scheme = FSI_Solver.FSI_Control.TimesteppingScheme.BDF2;
            double dt = 1e-2;
            C.dtMax = dt;
            C.dtMin = dt;
            C.Endtime = 150.0;
            C.NoOfTimesteps = 1000000;

            // haben fertig...
            // ===============

            return C;
        }


    }
}
