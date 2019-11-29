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
using BoSSS.Solution.CompressibleFlowCommon;

namespace BoSSS.Solution.CompressibleFlowCommon.Convection {

    /// <summary>
    /// Represents the momentum equations which are part of the Euler system
    /// </summary>
    public class EulerMomentumComponent : IEulerEquationComponent {

        /// <summary>
        /// The index of the component of the momentum vector represented by a
        /// specific instance
        /// </summary>
        public readonly int MomentumComponent;

        /// <summary>
        /// The <see cref="MomentumComponent"/>'s basis vector
        /// </summary>
        public readonly ilPSP.Vector ComponentVector;


        double heatCapacityRatio;

        double MachNumber;

        /// <summary>
        /// <see cref="IEulerEquationComponent"/>
        /// </summary>
        /// <param name="momentumComponent">
        /// The index of the component of the momentum vector (0, 1 or 2)
        /// represented by a specific instance
        /// </param>
        /// <param name="heatCapacityRatio">
        /// The heat capacity ratio of the material
        /// </param>
        /// <param name="MachNumber">
        /// The reference Mach number
        /// </param>
        /// <param name="Dim">
        /// Spatial dimension
        /// </param>
        public EulerMomentumComponent(int momentumComponent, double heatCapacityRatio, double MachNumber, int Dim) {
            this.MomentumComponent = momentumComponent;
            ComponentVector = ilPSP.Vector.StdBasis(momentumComponent, Dim);
            this.heatCapacityRatio = heatCapacityRatio;
            this.MachNumber = MachNumber;
        }

        /// <summary>
        /// Calculates the convective flux associated with the momentum
        /// equation
        /// </summary>
        /// <param name="state">The flow state inside a cell</param>
        /// <returns>
        /// \f$ \rho (\vec{u} \cdot \vec{e_i}) \vec{u} + p \vec{e_i}\f$ 
        /// </returns>
        public ilPSP.Vector Flux(StateVector state) {
            return state.Momentum[MomentumComponent] * state.Velocity
                + 1 / (heatCapacityRatio * MachNumber * MachNumber) * state.Pressure * ComponentVector;
        }

        /// <summary>
        /// Returns the <see cref="MomentumComponent"/>'s component of the
        /// momentum vector
        /// </summary>
        /// <param name="state">The flow state in a cell</param>
        /// <returns>\f$ \rho \vec{u} \cdot \vec{e_i}\f$ </returns>
        public double VariableValue(StateVector state) {
            return state.Momentum[MomentumComponent];
        }
    }
}
