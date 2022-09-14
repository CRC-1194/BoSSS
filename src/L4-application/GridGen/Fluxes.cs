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
using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.Utils;

namespace BoSSS.Application.ZwoLsTest {

    class DxBroken : IVolumeForm {
        public TermActivationFlags VolTerms => TermActivationFlags.AllOn;

        public IList<string> ArgumentOrdering => new string[] { "u" };

        public IList<string> ParameterOrdering => null;

        public double VolumeForm(ref CommonParamsVol cpv, double[] U, double[,] GradU, double V, double[] GradV) {
            return GradU[0, 0] * V;
        }
    }

    /// <summary>
    /// fluss fuer du/dx; (Ableitung nach 1. Raumrichtung), bulk-Phase;
    /// </summary>
    class DxFlux : LinearFlux {

        public override IList<string> ArgumentOrdering {
            get {
                return new string[] { "u" };
            }
        }

        protected override double BorderEdgeFlux(ref Foundation.CommonParamsBnd inp, double[] Uin) {
            return Uin[0]*inp.Normal[0];
        }

        protected override double InnerEdgeFlux(ref Foundation.CommonParams inp, double[] Uin, double[] Uout) {
            return 0.5*(Uin[0] + Uout[0])*inp.Normal[0];
        }

        protected override void Flux(ref Foundation.CommonParamsVol inp, double[] U, double[] output) {
            output[0] = U[0];
        }
    }

    /// <summary>
    /// Fluss fuer du/dx; (Ableitung nach 1. Raumrichtung), common parts for both level-sets;
    /// </summary>
    abstract class LevSetFlx : ILevelSetForm {

        //protected LevelSetTracker m_LsTrk;

        public LevSetFlx() {
            //m_LsTrk = _LsTrk;
        }
        
        public IList<string> ArgumentOrdering {
            get {
                return new string[] { "u" };
            }
        }

        abstract public int LevelSetIndex {
            get;
        }

        public TermActivationFlags LevelSetTerms {
            get {
                return TermActivationFlags.UxV;
            }
        }

        abstract public string NegativeSpecies {
            get;
        }

        public IList<string> ParameterOrdering {
            get {
                return null;
            }
        }


        virtual public string PositiveSpecies {
            get;
            private set;
        }

        abstract public double InnerEdgeForm(ref CommonParams inp, double[] uA, double[] uB, double[,] Grad_uA, double[,] Grad_uB, double vA, double vB, double[] Grad_vA, double[] Grad_vB);
    }

    /// <summary>
    /// fluss fuer du/dx; (Ableitung nach 1. Ruamrichtung), level-set #0;
    /// </summary>
    class LevSetFlx_phi0 : LevSetFlx {

        public LevSetFlx_phi0() : base() { }

        public override double InnerEdgeForm(ref CommonParams inp, double[] U_Neg, double[] U_Pos, double[,] Grad_uA, double[,] Grad_uB, double vA, double vB, double[] Grad_vA, double[] Grad_vB) { 
            double FlxPos = 0; // we are not interested in "A"
            double FlxNeg = U_Neg[0]*inp.Normal[0];

            return FlxNeg * vA - FlxPos * vB;
        }

        public override int LevelSetIndex {
            get { return 0; }
        }

        public override string PositiveSpecies {
            get { return "A"; }
        }

        public override string NegativeSpecies {
            get { return "B"; }
        }
    }

    /// <summary>
    /// fluss fuer du/dx; (Ableitung nach 1. Ruamrichtung), level-set #1;
    /// </summary>
    class LevSetFlx_phi1 : LevSetFlx {

        public LevSetFlx_phi1() : base() { }

        public override double InnerEdgeForm(ref CommonParams inp, double[] U_Neg, double[] U_Pos, double[,] Grad_uA, double[,] Grad_uB, double vA, double vB, double[] Grad_vA, double[] Grad_vB) {
            
            double FlxNeg = 0; // we are not interested in "A"
            double FlxPos = U_Pos[0]*inp.Normal[0];

            return FlxNeg * vA - FlxPos * vB;
        }

        public override int LevelSetIndex {
            get { return 1; }
        }

        public override string PositiveSpecies {
            get { return "B"; }
        }

        public override string NegativeSpecies {
            get { return "A"; }
        }
    }
}
