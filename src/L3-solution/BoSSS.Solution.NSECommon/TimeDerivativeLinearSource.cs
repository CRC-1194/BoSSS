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
using System.Diagnostics;
using System.Linq;
using System.Text;
using BoSSS.Foundation;
using BoSSS.Solution.Utils;
using ilPSP.Utils;

namespace BoSSS.Solution.NSECommon {

    /// <summary>
    /// Implementation of the time derivative as a linearized source term in the low-Mach combustion solver.
    /// Based on the implicit Euler scheme.
    /// </summary>
    public class TimeDerivativeLinearSource : BoSSS.Solution.Utils.LinearSource {
        string[] m_ArgumentOrdering;
        //string[] m_ParameterOrdering;
        MaterialLawLowMach EoS;
        bool m_energy;
        double rho;
        double dt;

        /// <summary>
        /// Ctor.
        /// </summary> 
        /// <param name="EoS">The material law</param>
        /// <param name="conti">Set conti: true for the continuity equation</param>
        /// <param name="ArgumentOrdering"></param>
        /// <param name="TimeStepSize"></param>
        public TimeDerivativeLinearSource(MaterialLawLowMach EoS, double TimeStepSize, String[] ArgumentOrdering, bool energy = false) {
            m_ArgumentOrdering = ArgumentOrdering;//.Cat(VariableNames.Rho);
            this.EoS = EoS;
            dt = TimeStepSize;
            m_energy = energy;

        }


        /// <summary>
        /// The argument
        /// </summary>
        public override IList<string> ArgumentOrdering {
            get { return m_ArgumentOrdering; }
        }

        /// <summary>
        /// Paramaters used to compute the density
        /// </summary>
        public override IList<string> ParameterOrdering {
            get { return EoS.ParameterOrdering; }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="parameters"></param>
        /// <param name="U"></param>
        /// <returns></returns>
        protected override double Source(double[] x, double[] parameters, double[] U) {
            Debug.Assert(ParameterOrdering[1] == VariableNames.Rho);
            //rho = EoS.GetDensity(U[0]);
            rho = parameters[1];
            double mult = 1.0;

            if (m_energy == true)
                mult = 1 / EoS.GetHeatCapacity(parameters[0]);

            return mult * rho * U[0];  
        }
    }
}
