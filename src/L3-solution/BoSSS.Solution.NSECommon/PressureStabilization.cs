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
using BoSSS.Foundation;
using BoSSS.Solution.Utils;
using ilPSP;

namespace BoSSS.Solution.NSECommon {

    /// <summary>
    /// Pressure stabilization for incompressible flows using an equal-order formulation.
    /// D. A. D. Pietro and A. Ern, Mathematical Aspects of Discontinuous Galerkin Methods. Springer Berlin Heidelberg, 2012.
    /// (Chapter 6.2.4.2)
    /// </summary>
    public class PressureStabilization : BoSSS.Solution.Utils.LinearDualValueFlux, IEquationComponentCoefficient {

        double PressureStabilizationFactor;
        protected double Reynolds;

        /// <summary>
        /// Ctor.
        /// </summary>
        public PressureStabilization(double PressureStabilizationFactor, double Reynolds) {
            this.PressureStabilizationFactor = PressureStabilizationFactor;
            this.Reynolds = Reynolds;
        }

        public override IList<string> ArgumentOrdering {
            get {
                return new string[] { VariableNames.Pressure };
            }
        }

        MultidimensionalArray h_max_Edge;

        protected override void InnerEdgeFlux(ref BoSSS.Foundation.CommonParams inp, double[] Uin, double[] Uout, out double FluxInCell, out double FluxOutCell) {
            double h_max = this.h_max_Edge[inp.jCellIn];
            double penalty = PressureStabilizationFactor * Reynolds * h_max;
            FluxInCell = penalty * (Uin[0] - Uout[0]);
            FluxOutCell = -FluxInCell;
        }

        protected override void BorderEdgeFlux_(ref BoSSS.Foundation.CommonParamsBnd inp, double[] Uin, out double FluxInCell) {
            FluxInCell = 0.0;
        }

        public void CoefficientUpdate(CoefficientSet cs, int[] DomainDGdeg, int TestDGdeg) {
            h_max_Edge = cs.CellLengthScales;
        }
    }
}
