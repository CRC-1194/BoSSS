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
using ilPSP;
using NUnit.Framework;
using BoSSS.Foundation.XDG;
using MPI.Wrappers;

namespace BoSSS.Application.XdgNastyLevsetLocationTest {

    /// <summary>
    /// Nunit entry point
    /// </summary>
    [TestFixture]
    public static class AllUpTest {

        /// <summary>
        /// MPI init
        /// </summary>
        [OneTimeSetUp]
        public static void SetUp() {
            BoSSS.Solution.Application.InitMPI();
        }

        /// <summary>
        /// MPI shutdown.
        /// </summary>
        [OneTimeTearDown]
        public static void OneTimeTearDown() {
            csMPI.Raw.mpiFinalize();
        }


        /// <summary>
        /// not the smartest way to define such a test...
        /// </summary>
        [Test]
        public static void AllUp(
            [Values(XQuadFactoryHelper.MomentFittingVariants.OneStepGauss, XQuadFactoryHelper.MomentFittingVariants.OneStepGaussAndStokes)]
            XQuadFactoryHelper.MomentFittingVariants variant
            ) {
            //static void Main(string[] args) {

           
            var Tests = new ITest[] { new Schraeg(XdgNastyLevsetLocationTest.GetTestRange(), XdgNastyLevsetLocationTest.GetTestRange()),
                new Parallel(XdgNastyLevsetLocationTest.GetTestRange(), XdgNastyLevsetLocationTest.GetTestRange()) };

            XQuadFactoryHelper.MomentFittingVariants[] Variants = new[] {
                XQuadFactoryHelper.MomentFittingVariants.OneStepGauss,
                XQuadFactoryHelper.MomentFittingVariants.OneStepGaussAndStokes };


            foreach(var tst in Tests) {


                XdgNastyLevsetLocationTest p = null;

                tst.ResetTest();

                BoSSS.Solution.Application._Main(new string[0], true, delegate () {
                    p = new XdgNastyLevsetLocationTest();
                    p.test = tst;
                    p.momentFittingVariant = variant;
                    return p;
                });

                Assert.IsTrue(p.IsPassed);

            }

        }

    }
}
