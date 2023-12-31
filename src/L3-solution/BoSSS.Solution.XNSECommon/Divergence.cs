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

using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using BoSSS.Platform;
using BoSSS.Solution.NSECommon;
using ilPSP;
using ilPSP.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BoSSS.Solution.XNSECommon.Operator.Continuity {


    /// <summary>
    /// velocity jump penalty for the divergence operator, on edges
    /// </summary>
    public class DivergenceInBulk_Edge : Divergence_DerivativeSource_Flux, IEquationComponentSpeciesNotification {

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="_component">
        /// component of the divergence
        /// </param>
        /// <param name="_bcmap"></param>
        public DivergenceInBulk_Edge(int _component, IncompressibleMultiphaseBoundaryCondMap _bcmap, double _rhoA, double _rhoB,
            //EquationAndVarMode _varmode,
            double _vorZeichen, bool _RescaleConti)
            : base(_component, _bcmap) {
            rhoA = _rhoA;
            rhoB = _rhoB;
            //varmode = _varmode;
            this.RescaleConti = _RescaleConti;

            //if (_vorZeichen != Math.Sign(_vorZeichen))
            //    throw new ArgumentOutOfRangeException();

            vorZeichen = _vorZeichen;
            base.bndFunction = null;
            this.m_bcmap = _bcmap;
        }

        IncompressibleMultiphaseBoundaryCondMap m_bcmap;

        double rhoA;
        double rhoB;
        //EquationAndVarMode varmode;
        bool RescaleConti;
        double vorZeichen;

        double rho;

        public void SetParameter(string speciesName) {
            switch (speciesName) {
                case "A": rho = rhoA; scale = vorZeichen / ((RescaleConti) ? rhoA : 1.0); SetBndfunction("A"); break;
                case "B": rho = rhoB; scale = vorZeichen / ((RescaleConti) ? rhoB : 1.0); SetBndfunction("B"); break;
                default: throw new ArgumentException("Unknown species.");
            }
        }

        void SetBndfunction(string S) {
            int d = base.component;
            base.bndFunction = this.m_bcmap.bndFunction[VariableNames.Velocity_d(d) + "#" + S];
        }

        protected override void InnerEdgeFlux(ref Foundation.CommonParams inp, double[] Uin, double[] Uout, out double FluxInCell, out double FluxOutCell) {
            base.InnerEdgeFlux(ref inp, Uin, Uout, out FluxInCell, out FluxOutCell);
            FluxInCell *= scale;
            FluxOutCell *= scale;
        }

        protected override void BorderEdgeFlux_(ref BoSSS.Foundation.CommonParamsBnd inp, double[] Uin, out double FluxInCell) {
            Debug.Assert(Uin.Length == 1);
            Debug.Assert(base.ArgumentOrdering.Count == 1);

            //if (varmode == EquationAndVarMode.mom_p)
            //    Uin[0] /= rho;

            base.BorderEdgeFlux_(ref inp, Uin, out FluxInCell);


            //if (varmode == EquationAndVarMode.mom_p)
            //    FluxInCell *= rho;


            FluxInCell *= scale;
        }


        double scale = double.NaN;
    }

    /*
    /// <summary>
    /// volume term for the Divergence / Continuity equation
    /// </summary>
    public class DivergenceInBulk_Volume : Divergence_DerivativeSource, IEquationComponentSpeciesNotification {

        public DivergenceInBulk_Volume(int _component, int _D, double _rhoA, double _rhoB, double _vorZeichen, bool _RescaleConti)
            : base(_component, _D) {
            //if (_vorZeichen != Math.Sign(_vorZeichen))
            //    throw new ArgumentOutOfRangeException();

            vorZeichen = _vorZeichen;
            rhoA = _rhoA;
            rhoB = _rhoB;
            RescaleConti = _RescaleConti;
        }

        double rhoA;
        double rhoB;
        bool RescaleConti;
        double vorZeichen;

        public void SetParameter(string speciesName) {
            switch (speciesName) {
                case "A": scale = vorZeichen / ((RescaleConti) ? rhoA : 1.0); break;
                case "B": scale = vorZeichen / ((RescaleConti) ? rhoB : 1.0); break;
                default: throw new ArgumentException("Unknown species.");
            }
        }

        public override double _DerivativeSource(ilPSP.Vector x, double[] Parameters, double[,] GradientU) {
            return base._DerivativeSource(x, Parameters, GradientU) * scale;
        }

        double scale = double.NaN;
    }
    */
    

    /// <summary>
    /// velocity jump penalty for the divergence operator, on the level set
    /// </summary>
    public class DivergenceAtLevelSet : ILevelSetForm, ISupportsJacobianComponent {

        //LevelSetTracker m_lsTrk;

        public DivergenceAtLevelSet(int _D, double _rhoA, double _rhoB,
            bool _MaterialInterface, double vorZeichen, bool RescaleConti, 
            double _wA = 1.0, double _wB = 1.0) {
            this.D = _D;
            this.rhoA = _rhoA;
            this.rhoB = _rhoB;
            //this.m_lsTrk = lsTrk;
            MaterialInterface = _MaterialInterface;

            scaleA = vorZeichen;
            scaleB = vorZeichen;

            if (RescaleConti) {
                scaleA /= rhoA;
                scaleB /= rhoB;
            }

            this.wA = _wA;
            this.wB = _wB;
        }

        bool MaterialInterface;
        int D;
        double rhoA;
        double rhoB;

        double scaleA;
        double scaleB;

        double wA;
        double wB;

        public TermActivationFlags LevelSetTerms {
            get {
                return TermActivationFlags.UxV;
            }
        }

        public double InnerEdgeForm(ref CommonParams cp,
            double[] U_Neg, double[] U_Pos, double[,] Grad_uA, double[,] Grad_uB,
            double vA, double vB, double[] Grad_vA, double[] Grad_vB) {

            double uAxN = GenericBlas.InnerProd(U_Neg, cp.Normal);
            double uBxN = GenericBlas.InnerProd(U_Pos, cp.Normal);

            //double s = 0;//cp.ParamsNeg[0];
            //if (!MaterialInterface) {
            //    Debug.Assert(cp.ParamsNeg[0] == cp.ParamsPos[0], "The interface velocity must be continuous across the level set!");
            //    throw new NotImplementedException();
            //}

            double rhoJmp = rhoB - rhoA;

            // transform from species B to A: we call this the "A-fictitious" value
            double uAxN_fict;
            //if(!MaterialInterface)
            //    uAxN_fict = (1 / rhoA) * (rhoB * uBxN + (-s) * rhoJmp);
            //else
            uAxN_fict = uBxN;


            // transform from species A to B: we call this the "B-fictitious" value
            double uBxN_fict;
            //if(!MaterialInterface)
            //    uBxN_fict = (1 / rhoB) * (rhoA * uAxN - (-s) * rhoJmp);
            //else
            uBxN_fict = uAxN;

            // compute the fluxes: note that for the continuity equation, we use not a real flux,
            // but some kind of penalization, therefore the fluxes have opposite signs!
            double FlxNeg;
            double FlxPos;
            FlxNeg = -Flux(uAxN, uAxN_fict, 1.0, 1.0); // flux on A-side
            FlxPos = +Flux(uBxN_fict, uBxN, 1.0, 1.0);  // flux on B-side

            FlxNeg *= scaleA;
            FlxPos *= scaleB;


            return FlxNeg * vA - FlxPos * vB;
        }

        
        /// <summary>
        /// the penalty flux
        /// </summary>
        static double Flux(double UxN_in, double UxN_out, double w_in, double w_out) {
                return (UxN_in - UxN_out) * w_in / (w_in + w_out);
        }

        public IEquationComponent[] GetJacobianComponents(int SpatialDimension) {
            return new IEquationComponent[] { this };
        }

        /*
       public void DerivativVar_LevelSetFlux(out double FlxNeg, out double FlxPos,
           ref CommonParams cp,
           double[] U_Neg, double[] U_Pos, double[,] GradU_Neg, double[,] GradU_Pos) {

           double uAxN = GenericBlas.InnerProd(U_Neg, cp.n);
           double uBxN = GenericBlas.InnerProd(U_Pos, cp.n);

           //if (varMode == EquationAndVarMode.mom_p) {
           //    uAxN /= rhoA;
           //    uBxN /= rhoB;
           //}

           double s = 0;//cp.ParamsNeg[0];
           if (!MaterialInterface) {
               Debug.Assert(cp.ParamsNeg[0] == cp.ParamsPos[0], "The interface velocity must be continuous across the level set!");
               throw new NotImplementedException();
           }

           //{
           //    double _x = cp.x[0];
           //    double _y = cp.x[1];

           //    s = -_x/Math.Sqrt(_x.Pow2() + _y.Pow2());

           //}

           double rhoJmp = rhoB - rhoA;

           // transform from species B to A: we call this the "A-fictitious" value
           double uAxN_fict;
           if( !MaterialInterface)
               uAxN_fict = (1 / rhoA) * (rhoB * uBxN + (-s) * rhoJmp);
           else
               uAxN_fict = uBxN;


           // transform from species A to B: we call this the "B-fictitious" value
           double uBxN_fict;
           if (!MaterialInterface)
               uBxN_fict = (1 / rhoB) * (rhoA * uAxN - (-s) * rhoJmp);
           else 
               uBxN_fict = uAxN;

           // compute the fluxes: note that for the continuity equation, we use not a real flux,
           // but some kind of penalization, therefore the fluxes have opposite signs!
           FlxNeg = -Flux(uAxN, uAxN_fict); // flux on A-side
           FlxPos = +Flux(uBxN_fict, uBxN);  // flux on B-side

           //if (varMode == EquationAndVarMode.mom_p) {
           //    FlxNeg *= rhoA;
           //    FlxPos *= rhoB;
           //}

           FlxNeg *= scaleA;
           FlxPos *= scaleB;
       }

       /*
       public override void PrimalVar_LevelSetFlux(out double FlxNeg, out double FlxPos,
           ref CommonParams cp, 
           double[] U_Neg, double[] U_Pos) {
           FlxNeg = 0;
           FlxPos = 0;
       }

       public override void FluxPotential(out double G, double[] U) {
           G = 0;
       }

       public override void Nu(out double NuNeg, out double NuPos, ref CommonParams cp) {
           NuNeg = 1.0;
           NuPos = 1.0;
       }
       */

        public IList<string> ArgumentOrdering {
            get {
                return VariableNames.VelocityVector(this.D);
            }
        }

        public IList<string> ParameterOrdering {
            get { return null; }
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

      
    }


}
