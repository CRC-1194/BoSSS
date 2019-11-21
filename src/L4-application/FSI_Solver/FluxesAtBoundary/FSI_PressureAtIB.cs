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
using System.Threading.Tasks;
using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.NSECommon;

namespace BoSSS.Solution.NSECommon.Operator.Pressure {
    public class FSI_PressureAtIB : ILevelSetForm {
        public FSI_PressureAtIB(int currentDim, int spatialDim, LevelSetTracker LsTrk) {
            m_d = currentDim;
            m_LsTrk = LsTrk;
            if (currentDim >= spatialDim)
                throw new ArgumentException();
        }

        private readonly LevelSetTracker m_LsTrk;
        private readonly int m_d;

        public IList<string> ArgumentOrdering {
            get {
                return new string[] { VariableNames.Pressure };
            }
        }

        public int LevelSetIndex {
            get {
                return 0;
            }
        }

        public SpeciesId NegativeSpecies {
            get {
                return this.m_LsTrk.GetSpeciesId("A");
            }
        }

        public SpeciesId PositiveSpecies {
            get {
                return this.m_LsTrk.GetSpeciesId("B");
            }
        }

        public TermActivationFlags LevelSetTerms {
            get {
                return TermActivationFlags.UxV;
            }
        }

        public IList<string> ParameterOrdering {
            get {
                return null;
            }
        }

        public double LevelSetForm(ref CommonParamsLs inp, double[] pA, double[] pB, double[,] Grad_pA, double[,] Grad_pB, double vA, double vB, double[] Grad_vA, double[] Grad_vB) {
            return vA * pA[0] * inp.Normal[m_d];
        }
    }
}
