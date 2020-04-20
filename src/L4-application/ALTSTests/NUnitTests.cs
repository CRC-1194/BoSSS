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

using BoSSS.Solution;
using NUnit.Framework;
using System.Globalization;
using System.Threading;

namespace ALTSTests {
    /// <summary>
    /// NUnit test class for ALTS
    /// </summary>
    [TestFixture]
    class NUnitTests : Program {

        public static void Main(string[] args) {
            Application._Main(
                args,
                true,
                () => new NUnitTests());
        }


        public static void ALTSDynClustering(int order, int subGrids, double maxEnergyNorm) {
            NUnitTests test = null;

            Application._Main(
                new string[0],
                true,
                delegate () {
                    test = new NUnitTests() {
                        ABOrder = order,
                        numOfSubgrids = subGrids,
                    };
                    return test;
                });

            double energyNorm = test.energyNorm;

            Assert.IsTrue(energyNorm < maxEnergyNorm + 1e-15);
        }

        // Call tests
        [Test]
        // Here, A-LTS gives the same result as LTS because AB order 1 equals Explicit Euler.
        // In this case, restarting a LTS simulation with another clustering is possible
        // because a history is not needed. 
        public static void ALTSDynClust_order1_subgrids3() {
            //3 cells
            //ALTSDynClustering(order: 1, subGrids: 3, maxEnergyNorm: 7.772253056189100E-01);

            //4 cells
            ALTSDynClustering(order: 1, subGrids: 4, maxEnergyNorm: 7.905061733461980E-01);
        }

        [Test]
        public static void ALTSDynClust_order2_subgrids3() {
            //3 cells
            //ALTSDynClustering(order: 2, subGrids: 3, maxEnergyNorm: 7.772253058420590E-01);

            //4 cells
            ALTSDynClustering(order: 2, subGrids: 4, maxEnergyNorm: 7.905061732830720E-01);
        }

        [Test]
        public static void ALTSDynClust_order3_subgrids3() {
            //3 cells
            //ALTSDynClustering(order: 3, subGrids: 3, maxEnergyNorm: 7.772253058420650E-01);

            //4 cells
            ALTSDynClustering(order: 3, subGrids: 4, maxEnergyNorm: 7.905061732830850E-01);
        }
    }
}
