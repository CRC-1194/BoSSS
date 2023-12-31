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
using BoSSS.Solution.CompressibleFlowCommon.Boundary;

namespace BoSSS.Solution.CompressibleFlowCommon.Convection {

    /// <summary>
    /// Implements the parts of the HLLCFlux specific to the energy equation
    /// </summary>
    public class HLLCEnergyFlux : HLLCFlux {

        /// <summary>
        /// <see cref="HLLCFlux"/>
        /// </summary>
        /// <param name="config"><see cref="HLLCFlux"/></param>
        /// <param name="boundaryMap"><see cref="HLLCFlux"/></param>
        /// <param name="equationComponent"><see cref="HLLCFlux"/></param>
        /// <param name="speciesMap"><see cref="HLLCFlux"/></param>
        public HLLCEnergyFlux(CompressibleControl config, IBoundaryConditionMap boundaryMap, EulerEnergyComponent equationComponent, ISpeciesMap speciesMap)
            : base(config, boundaryMap, equationComponent, speciesMap) {
        }

        /// <summary>
        /// See Toro2009, equation 10.73
        /// </summary>
        /// <param name="state">
        /// <see cref="HLLCFlux.GetModifiedVariableValue"/>
        /// </param>
        /// <param name="cellWaveSpeed">
        /// <see cref="HLLCFlux.GetModifiedVariableValue"/>
        /// </param>
        /// <param name="cellNormalVelocity">
        /// <see cref="HLLCFlux.GetModifiedVariableValue"/>
        /// </param>
        /// <param name="intermediateWaveSpeed">
        /// <see cref="HLLCFlux.GetModifiedVariableValue"/>
        /// </param>
        /// <param name="normal">
        /// <see cref="HLLCFlux.GetModifiedVariableValue"/>
        /// </param>
        /// <returns>See Toro2009, equation 10.73</returns>
        protected override double GetModifiedVariableValue(StateVector state, double cellWaveSpeed, double cellNormalVelocity, double intermediateWaveSpeed, ref ilPSP.Vector normal) {
            // corrected according to dimensionless equations 
            double MachScaling = config.EquationOfState.HeatCapacityRatio * config.MachNumber * config.MachNumber;
            double factor = intermediateWaveSpeed * MachScaling
                + state.Pressure / (state.Density * (cellWaveSpeed - cellNormalVelocity));
            return state.Density
                * (cellWaveSpeed - cellNormalVelocity)
                / (cellWaveSpeed - intermediateWaveSpeed)
                * (state.Energy / state.Density + factor * (intermediateWaveSpeed - cellNormalVelocity));
        }
    }
}
