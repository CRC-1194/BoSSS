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
using BoSSS.Solution.CompressibleFlowCommon.Convection;
using BoSSS.Solution.CompressibleFlowCommon.Boundary;

namespace CNS.Convection {

    /// <summary>
    /// Implementation of the HLL flux as described by BattenEtAl1997
    /// </summary>
    public class HLLFlux : EulerFlux {

        /// <summary>
        /// <see cref="EulerFlux"/>
        /// </summary>
        /// <param name="config"><see cref="EulerFlux"/></param>
        /// <param name="boundaryMap"><see cref="EulerFlux"/></param>
        /// <param name="equationComponent"><see cref="EulerFlux"/></param>
        /// <param name="speciesMap"><see cref="EulerFlux"/></param>
        public HLLFlux(CompressibleControl config, IBoundaryConditionMap boundaryMap, IEulerEquationComponent equationComponent, ISpeciesMap speciesMap)
            : base(config, boundaryMap, equationComponent, speciesMap.GetMaterial(double.NaN)) {
        }

        /// <summary>
        /// Evaluates the HLL flux as described in BattenEtAl1997 in the form
        /// of equation 24.
        /// </summary>
        public override double InnerEdgeFlux(double[] x, double time, StateVector stateIn, StateVector stateOut, ref ilPSP.Vector normal, int edgeIndex) {
            double waveSpeedIn;
            double waveSpeedOut;
            EstimateWaveSpeeds(stateIn, stateOut, ref normal, out waveSpeedIn, out waveSpeedOut);

            if (waveSpeedIn > 0.0) {
                return equationComponent.Flux(stateIn) * normal;
            } else if (waveSpeedOut < 0.0) {
                return equationComponent.Flux(stateOut) * normal;
            } else {
                double valueIn = equationComponent.VariableValue(stateIn);
                double valueOut = equationComponent.VariableValue(stateOut);
                double fluxIn = equationComponent.Flux(stateIn) * normal;
                double fluxOut = equationComponent.Flux(stateOut) * normal;
                return (waveSpeedOut * fluxIn - waveSpeedIn * fluxOut + waveSpeedIn * waveSpeedOut * (valueOut - valueIn)) / (waveSpeedOut - waveSpeedIn);
            }
        }
    }
}
