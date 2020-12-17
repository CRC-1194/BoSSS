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
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Quadrature;
using BoSSS.Foundation.XDG;
using BoSSS.Platform;
using BoSSS.Solution.NSECommon;
using ilPSP.LinSolvers;
using ilPSP.Utils;
using ilPSP;

namespace BoSSS.Solution.LevelSetTools.EllipticReInit {

    /// <summary>
    /// The Laplace Operator for the Elliptic Reinitialization
    /// \f$ \operatorname{div}(\operatorname{grad} \varphi) \f$
    /// No Boundary Conditions are set -> Boundary Conditions are determined by Interface only
    /// </summary>
    public class CentralDifferencesLHSForm : SIPLaplace {

        /// <summary>
        /// ctor
        /// </summary>
        public CentralDifferencesLHSForm(double PenaltyBase) : base(PenaltyBase, VariableNames.LevelSet) {
            //Do nothing
        }

        /// <summary>
        /// Here is some more Code doing nothing for performance reasons
        /// like this, the boundary terms are not even evaluated
        /// </summary>
        public new TermActivationFlags BoundaryEdgeTerms {
            get {
                return (TermActivationFlags.None);
            }
        }

        /// <summary>
        /// nix
        /// </summary>
        public new double BoundaryEdgeForm(ref CommonParamsBnd inp, double[] uA, double[,] Grad_uA, double vA, double[] Grad_vA) {
            return 0;
        }

        /// <summary>
        /// All boundaries are Neumann; this returns false
        /// </summary>
        protected override bool IsDirichlet(ref CommonParamsBnd inp) {
            return false;
        }
    }

}
