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
using System.Linq;
using System.Text;
using System.IO;
using BoSSS.Foundation;
using BoSSS.Solution;
using MPI.Wrappers;
using NUnit.Framework;
using BoSSS.Platform.LinAlg;
using ilPSP;

namespace BoSSS.Application.FSI_Solver {

    [TestFixture]
    static class TestProgram {

        [TestFixtureSetUp]
        public static void Init() {
            ilPSP.Environment.Bootstrap(
                new string[0],
                BoSSS.Solution.Application<FSI_Control>.GetBoSSSInstallDir(),
                out _);
        }

        [TestFixtureTearDown]
        public static void Cleanup() {
            //Console.Out.Dispose();
            csMPI.Raw.mpiFinalize();
        }

        [Test]
        public static void Test_ParticleParameter()
        {
            using (FSI_SolverMain p = new FSI_SolverMain())
            {

                var ctrl = BoSSS.Application.FSI_Solver.HardcodedTestExamples.Test_ParticleParameter();
                //ctrl.ImmediatePlotPeriod = 1;
                //ctrl.SuperSampling = 2;
                p.Init(ctrl);
                p.RunSolverMode();

                double p0_area = p.GetParticles()[0].Area_P();
                double p0_area_soll = Math.PI;
                double p0_Mass = p.GetParticles()[0].Motion.Mass_P;
                double p1_area = p.GetParticles()[1].Area_P();


                double diff_Area1 = Math.Abs(p0_area - p0_area_soll);
                double diff_Area2 = Math.Abs(p0_area - p1_area);
                double diff_Mass = Math.Abs(2 * p0_area - p0_Mass);
                Assert.LessOrEqual(diff_Area1, 1e-12, "Error in calculation of particle area");
                Assert.LessOrEqual(diff_Area2, 1e-12, "Error in calculation of particle area");
                Assert.LessOrEqual(diff_Mass, 1e-12, "Error in calculation of particle mass");

            }
        }

        [Test]
        public static void TestFlowRotationalCoupling() {
            using (FSI_SolverMain p = new FSI_SolverMain()) {

                var ctrl = BoSSS.Application.FSI_Solver.HardcodedTestExamples.Test_ParticleInShearFlow(k: 1);
                //ctrl.ImmediatePlotPeriod = 1;
                //ctrl.SuperSampling = 2;
                p.Init(ctrl);
                p.RunSolverMode();

                double angularVelocity_Sol = 0.00487;

                double angularVelocity = (double)p.QueryHandler.QueryResults["Angular_Velocity"];

                double diff_Velocity = Math.Abs(angularVelocity - angularVelocity_Sol);
                Console.WriteLine("   angular velocity is " + angularVelocity);
                Console.WriteLine("         should be     " + angularVelocity_Sol);
                Console.WriteLine("         difference is " + diff_Velocity);


                Assert.LessOrEqual(diff_Velocity, 0.00025, "Error in expected angular velocity is to high");

            }
        }

        [Test]
        public static void SingleDryParticleAgainstWall([Values(false, true)]  bool MeshRefine) { 
            using (FSI_SolverMain p = new FSI_SolverMain()) {

                var ctrl = BoSSS.Application.FSI_Solver.HardcodedTestExamples.Test_SingleDryParticleAgainstWall(MeshRefine:MeshRefine);
                p.Init(ctrl);
                p.RunSolverMode();


                Vector Dest_Should;
                if (MeshRefine)
                    Dest_Should = new Vector(0.0867851149899939, -0.709888525146848); 
                else
                    Dest_Should = new Vector(-0.0505473360771058, 0.751747291863557); 

                Vector Dest_Is = new Vector((double[])p.GetParticles()[0].Motion.Position[0]);

                double dist = (Dest_Should - Dest_Is).L2Norm();

                Console.WriteLine("Particle reached position " + Dest_Is + ", expected at " + Dest_Should + ", distance is " + dist);

                Assert.Less(dist, 0.1, "Particle to far from expected position");
            }
        }

        [Test]
        public static void DryParticleBounce()
        {
            using (FSI_SolverMain p = new FSI_SolverMain())
            {

                var ctrl = BoSSS.Application.FSI_Solver.HardcodedTestExamples.Test_DryParticleBounce();
                p.Init(ctrl);
                p.RunSolverMode();

                Vector Dest_Should;
                Dest_Should = new Vector(0.0, 0.865886176125762);

                Vector Dest_Is = new Vector((double[])p.GetParticles()[0].Motion.Position[0]);

                double dist = (Dest_Should - Dest_Is).L2Norm();

                Console.WriteLine("Particle reached position " + Dest_Is + ", expected at " + Dest_Should + ", distance is " + dist);

                Assert.Less(dist, 0.1, "Particle to far from expected position");
            }
        }


        [Test]
        public static void StickyTrap()
        {
            using (FSI_SolverMain p = new FSI_SolverMain())
            {

                var ctrl = BoSSS.Application.FSI_Solver.HardcodedTestExamples.Test_StickyTrap();
                p.Init(ctrl);
                p.RunSolverMode();

                Vector Dest_Should;
                Dest_Should = new Vector(0.0, 0.0761459999999999);
                double VelY_Should = 0;

                Vector Dest_Is = new Vector((double[])p.GetParticles()[0].Motion.Position[0]);
                double VelY_Is = p.GetParticles()[0].Motion.TranslationalVelocity[0][0];

                double dist = (Dest_Should - Dest_Is).L2Norm();
                double Vel_Div = Math.Abs(VelY_Should - VelY_Is);

                Console.WriteLine("Particle reached position " + Dest_Is + ", expected at " + Dest_Should + ", distance is " + dist);
                Console.WriteLine("Particle end velocitiy " + VelY_Is + ", expected velocity: " + VelY_Should + ", difference is " + Vel_Div);

                Assert.Less(dist, 0.1, "Particle to far from expected position.");
                Assert.Less(Vel_Div, 0.05, "Particle is moving.");
            }
        }

        [Test]
        public static void Test_ActiveForce()
        {
            using (FSI_SolverMain p = new FSI_SolverMain())
            {
                var ctrl = HardcodedTestExamples.Test_ActiveForce();
                p.Init(ctrl);
                p.RunSolverMode();

                double ForcesSoll = 30753.7101679592;

                double Forces = p.GetParticles()[0].Motion.HydrodynamicForces[0][0];

                double DiffForces = Math.Abs(ForcesSoll - Forces); 

                Assert.LessOrEqual(DiffForces, 20);
            }
        }

        [Test]
        public static void Test_HydrodynamicForces()
        {
            using (FSI_SolverMain p = new FSI_SolverMain())
            {
                var ctrl = HardcodedTestExamples.Test_HydrodynamicForces();
                p.Init(ctrl);
                p.RunSolverMode();

                double ForcesSoll = 5.60683953785339;

                double Forces = p.GetParticles()[0].Motion.HydrodynamicForces[0][0];

                double DiffForces = Math.Abs(ForcesSoll - Forces);

                Assert.LessOrEqual(DiffForces, 1e-3);
            }
        }
    }
}
