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

using BoSSS.Solution.Utils;
using BoSSS.Platform.LinAlg;
using System.Collections.Generic;

namespace ALTSTests {

    /// <summary>
    /// Flux to a 2D scalar transport equation
    /// </summary>
    class ScalarTransportFlux2D : NonlinearFlux {

        double inflow;

        public ScalarTransportFlux2D(double inflow) {
            this.inflow = inflow;
        }

        /// <summary>
        /// the predefined, div-free flow field
        /// </summary>
        ilPSP.Vector FlowField(double[] x, double[] Uin, double[] Uot) {
            ilPSP.Vector u = new ilPSP.Vector(2);
            u.x = 0.5 * (Uin[1] + Uot[1]);
            u.y = 0.5 * (Uin[2] + Uot[2]);
            return u;
        }

        protected override double BorderEdgeFlux(double time, double[] x, double[] normal, byte EdgeTag, double[] Uin, int jEdge) {
            ilPSP.Vector n = new ilPSP.Vector(2);
            n.x = normal[0];
            n.y = normal[1];

            var vel = FlowField(x, Uin, Uin);

            if (n * vel >= 0) {
                // flow from inside 
                return (vel * Uin[0]) * n;
            } else {
                // flow from outside into the domain
                //return (vel * Uin[0]) * n;
                return (vel * inflow) * n;
            }
        }

        /// <summary>
        /// calculating the inner edge fluxes by using a first oder upwind scheme
        /// </summary>
        protected override double InnerEdgeFlux(double time, double[] x, double[] normal, double[] Uin, double[] Uout, int jEdge) {
            ilPSP.Vector n= new ilPSP.Vector(2);
            n.x = normal[0];
            n.y = normal[1];

            var vel = FlowField(x, Uin, Uout);

            if (vel * n > 0)
                return (vel * Uin[0]) * n;
            else
                return (vel * Uout[0]) * n;
        }

        protected override void Flux(double time, double[] x, double[] U, double[] output) {
            ilPSP.Vector o;
            o = FlowField(x, U, U) * U[0];
            output[0] = o.x;
            output[1] = o.y;
        }

        /// <summary>
        /// 
        /// </summary>
        public override IList<string> ArgumentOrdering {
            get {
                return new string[] { "c" };
            }
        }

        /// <summary>
        /// the transport velocity
        /// </summary>
        public override IList<string> ParameterOrdering {
            get {
                return BoSSS.Solution.NSECommon.VariableNames.VelocityVector(2);
            }
        }
    }
}