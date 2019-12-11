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
using ilPSP;
using ilPSP.LinSolvers;
using ilPSP.Utils;

namespace BoSSS.Solution.LevelSetTools.EllipticExtension {

    /// <summary>
    /// Penalty Operator for the Zero Boundary Condition at the Interface
    /// \f[ 
    /// a(u,v) = \alpha \int_{\Gamma} u v   \mathrm{dS}
    /// \f]
    /// </summary>
    public class SingleComponentInterfaceForm : ILevelSetForm, ILevelSetEquationComponentCoefficient {
        double PenaltyBase;
        LevelSetTracker LSTrk;

        public SingleComponentInterfaceForm(double PenaltyBase, LevelSetTracker  LSTrk) {
            this.PenaltyBase = PenaltyBase;
            this.LSTrk = LSTrk;
            
        }

        MultidimensionalArray NegCellLengthScaleS;
        MultidimensionalArray PosCellLengthScaleS;

        public void CoefficientUpdate(CoefficientSet csA, CoefficientSet csB, int[] DomainDGdeg, int TestDGdeg) {
            NegCellLengthScaleS = csA.CellLengthScales;
            if (csB != null)
                PosCellLengthScaleS = csB.CellLengthScales;
        }



        /// <summary>
        /// Penalty Term enforcing the Dirichlet value at the interface
        /// Note: this Form is written only in terms of uA, since there is no XDG-field involved
        /// </summary>
        /// <param name="inp">inp.ParamsNeg[0] is the Dirichlet value from the parameter-field</param>
        /// <param name="uA">the unknown</param>
        /// <param name="uB">not needed</param>
        /// <param name="Grad_uA">not needed</param>
        /// <param name="Grad_uB">not needed</param>
        /// <param name="vA">test function</param>
        /// <param name="vB">not needed</param>
        /// <param name="Grad_vA">not needed</param>
        /// <param name="Grad_vB">not needed</param>
        /// <returns>the evaluated penalty flux</returns>
        public double LevelSetForm(ref CommonParams inp, double[] uA, double[] uB, double[,] Grad_uA, double[,] Grad_uB, double vA, double vB, double[] Grad_vA, double[] Grad_vB) {
            double NegCellLengthScale = NegCellLengthScaleS[inp.jCellIn];
            double PosCellLengthScale = (PosCellLengthScaleS != null) ? PosCellLengthScaleS[inp.jCellOut] : NegCellLengthScaleS[inp.jCellOut];


            double hmin;
            if (NegCellLengthScale.IsNaN()) {
                hmin = PosCellLengthScale;
            }
            else if (PosCellLengthScale.IsNaN()) {
                hmin = NegCellLengthScale;
            }
            else {
                hmin = Math.Min(NegCellLengthScale, PosCellLengthScale);
            }

            //return PenaltyBase * 2 / hmin * (uA[0] + uB[0] - inp.ParamsNeg[0] - inp.ParamsPos[0]) * (vA+vB) /4 ;
            return PenaltyBase * 2 / hmin * (uA[0] - inp.Parameters_IN[0]) * (vA);
            //return PenaltyBase * 2 / hmin * (uB[0] - inp.ParamsPos[0]) * (vB);
        }

        

        public IList<string> ArgumentOrdering {
            get {
                return new string[] { "Extension" };
            }
        }

        public IList<string> ParameterOrdering {
            get {
                return new string[] { "InterfaceValue" };
            }
        }

        public int LevelSetIndex
        {
            get { return 0; }
        }

        public SpeciesId PositiveSpecies
        {
            get { return this.LSTrk.GetSpeciesId("B"); }
        }

        public SpeciesId NegativeSpecies
        {
            get { return this.LSTrk.GetSpeciesId("A"); }
        }

        public TermActivationFlags LevelSetTerms
        {
            get
            {
                return (TermActivationFlags.UxV | TermActivationFlags.V);
            }
        }
    }
}
