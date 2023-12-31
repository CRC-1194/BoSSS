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

namespace BoSSS.Solution.CompressibleFlowCommon {

    /// <summary>
    /// Static environment of a program run. Defines some global variables
    /// which never change at runtime.
    /// </summary>
    public static class CompressibleEnvironment {

        /// <summary>
        /// The number of spatial dimensions of the considered problem.
        /// </summary>
        public static int NumberOfDimensions {
            get;
            private set;
        }

        /// <summary>
        /// Defines a mapping between each primal argument of the compressible
        /// Navier-Stokes equations (density, momentum components and energy)
        /// and their positions in a one-dimensional array which represents the
        /// current flow configuration.
        /// { "rho" -> 0,
        ///   "m0" -> 1,
        ///   ["m1" -> 2,
        ///   ["m2" -> 3,]]
        ///   "rhoE" -> numberOfDimensions + 1 }
        /// </summary>
        public static Dictionary<Variable, int> PrimalArgumentToIndexMap {
            get;
            private set;
        }

        /// <summary>
        /// Utility method to retrieve the keys part of
        /// <see cref="CompressibleEnvironment.PrimalArgumentToIndexMap"/>
        /// </summary>
        public static string[] PrimalArgumentOrdering {
            get;
            private set;
        }

        /// <summary>
        /// Mapping between the index of a spatial direction and the name of
        /// the associated independent variable. In particular, this is
        /// { "x" [, "y" [ "z" ]] }. 
        /// </summary>
        public static string[] IndependentVariables {
            get;
            private set;
        }

        /// <summary>
        /// Initializes the constants.
        /// </summary>
        /// <param name="numberOfDimensions">
        /// The number of spatial dimensions of the considered problem.
        /// </param>
        /// <param name="_Program">
        /// The corresponding application object
        /// </param>
        /// <remarks>
        /// May only be called once, even though this is not checked.
        /// </remarks>
        public static void Initialize(int numberOfDimensions) {
            NumberOfDimensions = numberOfDimensions;

            switch (numberOfDimensions) {
                case 1:
                    IndependentVariables = new string[] { "x" };
                    break;

                case 2:
                    IndependentVariables = new string[] { "x", "y" };
                    break;

                case 3:
                    IndependentVariables = new string[] { "x", "y", "z" };
                    break;

                default:
                    throw new Exception("Invalid number of dimensions");
            }

            PrimalArgumentToIndexMap = new Dictionary<Variable, int>();
            PrimalArgumentToIndexMap.Add(CompressibleVariables.Density, 0);
            for (int d = 0; d < numberOfDimensions; d++) {
                PrimalArgumentToIndexMap.Add(CompressibleVariables.Momentum[d], d + 1);
            }
            PrimalArgumentToIndexMap.Add(CompressibleVariables.Energy, numberOfDimensions + 1);

            PrimalArgumentOrdering = PrimalArgumentToIndexMap.Keys.Select(v => v.Name).ToArray();
        }
    }
}
