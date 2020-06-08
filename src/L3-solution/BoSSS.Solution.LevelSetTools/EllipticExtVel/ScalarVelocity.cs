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
using BoSSS.Solution.LevelSetTools.EllipticExtension;
using BoSSS.Solution.NSECommon;
using ilPSP;
using ilPSP.LinSolvers;
using ilPSP.Utils;


namespace BoSSS.Solution.LevelSetTools.EllipticExtension {

    public class ScalarVelocityInterfaceForm : ILevelSetForm, ILevelSetEquationComponentCoefficient {
        int D;
        LevelSetTracker LSTrk;

        public ScalarVelocityInterfaceForm(double PenaltyBase, LevelSetTracker LSTrk) {
            this.PenaltyBase = PenaltyBase;
            this.LSTrk = LSTrk;
            this.D = LSTrk.GridDat.SpatialDimension;
        }
        double PenaltyBase;

        MultidimensionalArray NegCellLengthScaleS;
        MultidimensionalArray PosCellLengthScaleS;

        public void CoefficientUpdate(CoefficientSet csA, CoefficientSet csB, int[] DomainDGdeg, int TestDGdeg) {
            NegCellLengthScaleS = csA.CellLengthScales;
            PosCellLengthScaleS = csB.CellLengthScales;
        }

        public double InnerEdgeForm(ref CommonParams inp, double[] uA, double[] uB, double[,] Grad_uA, double[,] Grad_uB, double vA, double vB, double[] Grad_vA, double[] Grad_vB) {
            double S0 = 0; // velocity in normal direction, at the interface
            for(int d = 0; d < D; d++) {
                S0 += inp.Normal[d] * inp.Parameters_IN[d];
            }

            double NegCellLengthScale = NegCellLengthScaleS[inp.jCellIn];
            double PosCellLengthScale = PosCellLengthScaleS[inp.jCellOut];

            double hmin;
            if(NegCellLengthScale.IsNaN()) {
                hmin = PosCellLengthScale;
            } else if(PosCellLengthScale.IsNaN()) {
                hmin = NegCellLengthScale;
            } else {
                hmin = Math.Min(NegCellLengthScale, PosCellLengthScale);
            }

            double penalty = PenaltyBase / hmin;

            return penalty * (uA[0] - S0) * vA;
        }

        public IList<string> ArgumentOrdering {
            get {
                return new string[] { "Extension" };
            }
        }

        public IList<string> ParameterOrdering {
            get {

                return ArrayTools.Cat<string>(VariableNames.VelocityVector(D));
            }
        }

        public int LevelSetIndex {
            get { return 0; }
        }

        public SpeciesId PositiveSpecies {
            get { return this.LSTrk.GetSpeciesId("B"); }
        }

        public SpeciesId NegativeSpecies {
            get { return this.LSTrk.GetSpeciesId("A"); }
        }

        public TermActivationFlags LevelSetTerms {
            get {
                return (TermActivationFlags.UxV | TermActivationFlags.V);
            }
        }

        public double BoundaryEdgeForm(ref CommonParamsBnd inp, double[] _uA, double[,] _Grad_uA, double _vA, double[] _Grad_vA) {
            throw new NotSupportedException();
        }

        public TermActivationFlags BoundaryEdgeTerms {
            get { return TermActivationFlags.None; }
        }

        public TermActivationFlags InnerEdgeTerms {
            get { return TermActivationFlags.None; }
        }
    }


}
