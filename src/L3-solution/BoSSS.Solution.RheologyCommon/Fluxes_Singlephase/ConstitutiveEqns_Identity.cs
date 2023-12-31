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

using System.Collections.Generic;
using System.Linq;
using BoSSS.Foundation;
using BoSSS.Solution.NSECommon;

namespace BoSSS.Solution.RheologyCommon {

    /// <summary>
    /// Identity part of constitutive equations for singlephase flow.
    /// </summary>
    public class ConstitutiveEqns_Identity : IVolumeForm, IEquationComponentCoefficient, ISupportsJacobianComponent {

        private int component; // equation index (0: xx, 1: xy, 2: yy)
        private double alpha;
        private double weissenberg;
        private double beta;

        /// <summary>
        /// Initialize identity
        /// </summary>
        public ConstitutiveEqns_Identity(int Component, double Alpha, double Weissenberg, double Beta) {
            component = Component;
            alpha = Alpha;
            weissenberg = Weissenberg;
            beta = Beta;

        }
        //public ConstitutiveEqns_Identity(int component) {
        //    this.component = component;
        //}

        /// <summary>
        /// Choosing the required terms
        /// </summary>
        public TermActivationFlags VolTerms {
            get { return TermActivationFlags.V | TermActivationFlags.UxV; }
        }

        /// <summary>
        /// Ordering of the dependencies as whole string
        /// </summary>
        static string[] allArg = new string[] { VariableNames.StressXX, VariableNames.StressXY, VariableNames.StressYY };

        /// <summary>
        /// Ordering of the dependencies
        /// </summary>
        public IList<string> ArgumentOrdering {
            get { return new string[] { allArg[component] }; }
        }

        /// <summary>
        /// Ordering of the parameters - null at identity part
        /// </summary>
        public IList<string> ParameterOrdering { get; }


        /// <summary>
        /// Calculating the integral of the volume part
        /// </summary>
        public virtual double VolumeForm(ref CommonParamsVol cpv, double[] T, double[,] Grad_T, double V, double[] GradV) {

            double res = 0;

            if (beta != 1) {
                res += (1 + weissenberg * alpha / (1 - beta) * T[0]) * T[0];
            } else {
                res += T[0];
            }

            return res * V;
        }

        /// <summary>
        /// update the coefficient such as the current Weissenberg number
        /// </summary>
        public void CoefficientUpdate(CoefficientSet cs, int[] DomainDGdeg, int TestDGdeg) {
            if (cs.UserDefinedValues.Keys.Contains("Weissenbergnumber")) {
                weissenberg = (double)cs.UserDefinedValues["Weissenbergnumber"];
            }
        }
        /// <summary>
        /// Linear component / just the flux itself
        /// </summary>
        public IEquationComponent[] GetJacobianComponents(int SpatialDimension) {
            return new[] { this };
        }
    }
}
