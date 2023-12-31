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

using BoSSS.Platform.LinAlg;

namespace BoSSS.Solution.CompressibleFlowCommon.Convection {

    /// <summary>
    /// Represents the energy equation which is part of the Euler system
    /// </summary>
    public class EulerEnergyComponent : IEulerEquationComponent {

        /// <summary>
        /// Calculates the convective flux associated with the energy equation
        /// </summary>
        /// <param name="state">The flow state inside a cell</param>
        /// <returns>\f$ \vec{u} (\rho E + p)\f$ </returns>
        public ilPSP.Vector Flux(StateVector state) {
            return state.Velocity * (state.Energy + state.Pressure);
        }

        /// <summary>
        /// Returns the energy
        /// </summary>
        /// <param name="state">The flow state inside a cell</param>
        /// <returns>\f$ \rho E\f$ </returns>
        public double VariableValue(StateVector state) {
            return state.Energy;
        }
    }
}
