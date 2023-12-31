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
using BoSSS.Foundation.XDG;
using System.Diagnostics;
using BoSSS.Solution.NSECommon;
using ilPSP.Utils;
using BoSSS.Platform;
using ilPSP;
using BoSSS.Foundation;
using System.Collections;
using static BoSSS.Foundation.Grid.Classic.GridData;

namespace BoSSS.Solution.XNSECommon.Operator.Viscosity {

    /// <summary>
    /// 
    /// </summary>
    public class StressDivergenceAtLevelSet : BoSSS.Foundation.XDG.ILevelSetForm, ILevelSetEquationComponentCoefficient {

        //LevelSetTracker m_LsTrk;

        public StressDivergenceAtLevelSet(double _reynoldsA, double _reynoldsB, double[] _penalty1, double _penalty2, int D, int _component, bool _staticInt = false) {
            //this.m_LsTrk = lstrk;
            this.muA = 1 / _reynoldsA;
            this.muB = 1 / _reynoldsB;
            this.penalty1 = _penalty1;
            this.penalty2 = _penalty2;
            this.component = _component;
            this.m_D = D;//lstrk.GridDat.SpatialDimension;
            this.staticInt = _staticInt;
        }

        double muA;
        double muB;
        double[] penalty1;
        double penalty2;
        int component;
        int m_D;

        bool staticInt;

        /// <summary>
        /// default-implementation
        /// </summary>
        public double InnerEdgeForm(ref CommonParams inp,
            double[] TA, double[] TB, double[,] Grad_uA, double[,] Grad_uB,
            double vA, double vB, double[] Grad_vA, double[] Grad_vB) {
            double[] N = inp.Normal;
            double hCellMin = LengthScales[inp.jCellIn];//this.m_LsTrk.GridDat.Cells.h_min[inp.jCellIn];

            int D = N.Length;
            Debug.Assert(this.ArgumentOrdering.Count == 3);

            double PosCellLengthScale = PosLengthScaleS[inp.jCellOut];
            double NegCellLengthScale = NegLengthScaleS[inp.jCellIn];

            double hCutCellMin = Math.Min(NegCellLengthScale, PosCellLengthScale);
            if (hCutCellMin <= 1.0e-10 * hCellMin)
                // very small cell -- clippling
                hCutCellMin = hCellMin;

            Debug.Assert(TA.Length == this.ArgumentOrdering.Count);
            Debug.Assert(TB.Length == this.ArgumentOrdering.Count);

            double wA;
            double wB;
            double wPenalty;
            wA = 0.5;
            wB = 0.5;
            wPenalty = (Math.Abs(muA) > Math.Abs(muB)) ? muA : muB;
            

            double res = 0;

            //res -= ((TA[0] * muA * wA + TB[0] * muB * wB) * N[0] + (TA[1] * muA * wA + TB[1] * muB * wB) * N[1]) * (vA - vB); // central difference for stress divergence
            //res += penalty2 / hCutCellMin * (TA[2] * muA - TB[2] * muB) * (vA - vB) * wPenalty;

            res -= 0.5 * ((TA[0] * muA+ TB[0] * muB) * N[0] + (TA[1] * muA + TB[1] * muB) * N[1]) * (vA - vB); // central difference for stress divergence
            res += penalty2 / hCutCellMin * (TA[2] * muA - TB[2] * muB) * (vA - vB);

            return  res;
        }


        MultidimensionalArray PosLengthScaleS;
        MultidimensionalArray NegLengthScaleS;
        MultidimensionalArray LengthScales;

        public void CoefficientUpdate(CoefficientSet csA, CoefficientSet csB, int[] DomainDGdeg, int TestDGdeg) {
            LengthScales = csA.GrdDat.iGeomCells.h_min; // can use either csA or csB GridData should be equal
            NegLengthScaleS = csA.CellLengthScales;
            PosLengthScaleS = csB.CellLengthScales;
        }

        //private static bool rem = true;

        public int LevelSetIndex {
            get { return 0; }
        }

        public IList<string> ArgumentOrdering {
            get {
                switch (component) {
                    case 0:
                        return new string[] { VariableNames.StressXX, VariableNames.StressXY, VariableNames.VelocityX };
                    case 1:
                        return new string[] { VariableNames.StressXY, VariableNames.StressYY, VariableNames.VelocityY };
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public string PositiveSpecies {
            get { return "B"; }
        }

        public string NegativeSpecies {
            get { return "A"; }
        }

        public TermActivationFlags LevelSetTerms {
            get {
                return TermActivationFlags.UxV | TermActivationFlags.UxGradV | TermActivationFlags.GradUxV;
            }
        }

        public IList<string> ParameterOrdering {
            get { return null; }
        }

    }
}
