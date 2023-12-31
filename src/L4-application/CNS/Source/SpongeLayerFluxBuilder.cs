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

using BoSSS.Solution.CompressibleFlowCommon;
using BoSSS.Solution.CompressibleFlowCommon.Boundary;
using CNS.EquationSystem;

namespace CNS.Source {

    /// <summary>
    /// Builder for <see cref="SpongeLayerSource"/>
    /// </summary>
    public class SpongeLayerFluxBuilder : FluxBuilder {

        private SpongeLayerConfig config;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control"></param>
        /// <param name="boundaryMap"></param>
        /// <param name="speciesMap"></param>
        public SpongeLayerFluxBuilder(CNSControl control, CompressibleBoundaryCondMap boundaryMap, ISpeciesMap speciesMap)
            : base(control, boundaryMap, speciesMap) {
            this.config = control.SpongeLayerConfig;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mapping"></param>
        public override void BuildFluxes(Operator mapping) {
            mapping.DensityComponents.Add(new SpongeLayerSource(
                CompressibleEnvironment.PrimalArgumentToIndexMap[CompressibleVariables.Density],
                config.referenceState.Density,
                config.layerDirection,
                config.layerStart,
                config.layerEnd,
                config.strength));
            for (int d = 0; d < CompressibleEnvironment.NumberOfDimensions; d++) {
                mapping.MomentumComponents[d].Add(new SpongeLayerSource(
                    CompressibleEnvironment.PrimalArgumentToIndexMap[CompressibleVariables.Momentum[d]],
                    config.referenceState.Momentum[d],
                    config.layerDirection,
                    config.layerStart,
                    config.layerEnd,
                    config.strength));
            }
            mapping.EnergyComponents.Add(new SpongeLayerSource(
                CompressibleEnvironment.PrimalArgumentToIndexMap[CompressibleVariables.Energy],
                config.referenceState.Energy,
                config.layerDirection,
                config.layerStart,
                config.layerEnd,
                config.strength));
        }
    }
}
