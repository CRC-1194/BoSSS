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
using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.Utils;
using BoSSS.Solution.NSECommon;
using System.Diagnostics;
using ilPSP;

namespace BoSSS.Solution.RheologyCommon {

    /// <summary>
    /// Objective part of constitutive equations at interface for multiphase.
    /// </summary>
    public class ObjectiveAtLevelSet : BoSSS.Foundation.XDG.ILevelSetForm, ILevelSetEquationComponentCoefficient {

        //LevelSetTracker m_LsTrk;

        int Component;           // equation index (0: xx, 1: xy, 2: yy)
        BoundaryCondMap<IncompressibleBcType> m_BcMap;
        protected double WeissenbergA;
        protected double WeissenbergB;

        /// <summary>
        /// Initialize viscosity part
        /// </summary>
        public ObjectiveAtLevelSet(int Component, double _WeissenbergA, double _WeissenbergB) {
            //this.m_LsTrk = lstrk;
            this.Component = Component;
            this.WeissenbergA = _WeissenbergA;
            this.WeissenbergB = _WeissenbergB;
        }

        /// <summary>
        /// Ordering of the dependencies
        /// </summary>
        public IList<string> ArgumentOrdering {
            get {
                switch (Component) {
                    case 0:
                        return new string[] { VariableNames.StressXX, VariableNames.StressXY, VariableNames.StressXX, VariableNames.StressXY };
                    case 1:
                        return new string[] { VariableNames.StressXY, VariableNames.StressYY, VariableNames.StressXX, VariableNames.StressXY };
                    case 2:
                        return new string[] { VariableNames.StressXY, VariableNames.StressYY, VariableNames.StressXY, VariableNames.StressYY };
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Ordering of the parameters
        /// </summary>
        public IList<string> ParameterOrdering {
            get {
                return null;
            }
        }


        /// <summary>
        /// default-implementation
        /// </summary>
        public double InnerEdgeForm(ref CommonParams inp,
            double[] TA, double[] TB, double[,] Grad_uA, double[,] Grad_uB,
            double VA, double VB, double[] Grad_vA, double[] Grad_vB) {
            double[] N = inp.Normal;


            double PosCellLengthScale = PosLengthScaleS[inp.jCellOut];
            double NegCellLengthScale = NegLengthScaleS[inp.jCellIn];

            Debug.Assert(TA.Length == this.ArgumentOrdering.Count);
            Debug.Assert(TB.Length == this.ArgumentOrdering.Count);

            double res = 0.0;
            res += (TA[0] * WeissenbergA - TB[0] * WeissenbergB) + (TA[1] * WeissenbergA - TB[1] * WeissenbergB) + (TA[2] * WeissenbergA - TB[2] * WeissenbergB) + (TA[3] * WeissenbergA - TB[3] * WeissenbergB);
            return res * (VA - VB);
        }


        MultidimensionalArray PosLengthScaleS;
        MultidimensionalArray NegLengthScaleS;

        public void CoefficientUpdate(CoefficientSet csA, CoefficientSet csB, int[] DomainDGdeg, int TestDGdeg) {
            NegLengthScaleS = csA.CellLengthScales;
            PosLengthScaleS = csB.CellLengthScales;

            if (csA.UserDefinedValues.Keys.Contains("Weissenbergnumber")) {
                WeissenbergA = (double)csA.UserDefinedValues["Weissenbergnumber"];
            }

            if (csB.UserDefinedValues.Keys.Contains("Weissenbergnumber")) {
                WeissenbergB = (double)csB.UserDefinedValues["Weissenbergnumber"];
            }
        }

        public int LevelSetIndex {
            get { return 0; }
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
    }
}
