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
using System.Threading.Tasks;

using ilPSP;
using ilPSP.Utils;

using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.NSECommon;
using System.Collections;

namespace BoSSS.Solution.XheatCommon {


    public class HeatConvectionAtLevelSet_LLF : ILevelSetForm, ILevelSetEquationComponentCoefficient {

        //LevelSetTracker m_LsTrk;

        bool movingmesh;

        public HeatConvectionAtLevelSet_LLF(int _D, double _capA, double _capB, double _LFFA, double _LFFB, 
            ThermalMultiphaseBoundaryCondMap _bcmap, bool _movingmesh, double _Tsat) {

            m_D = _D;

            //m_LsTrk = LsTrk;

            //MaterialInterface = _MaterialInterface;
            movingmesh = _movingmesh;

            NegFlux = new HeatConvectionInBulk(_D, _bcmap, _capA, _capB, _LFFA, double.NaN);
            NegFlux.SetParameter("A");
            PosFlux = new HeatConvectionInBulk(_D, _bcmap, _capA, _capB, double.NaN, _LFFB);
            PosFlux.SetParameter("B");


            //DirichletCond = _DiriCond;
            Tsat = _Tsat;

            capA = _capA;
            capB = _capB;
            LFFA = _LFFA;
            LFFB = _LFFB;

        }

        //bool MaterialInterface;
        int m_D;

        double capA;
        double capB;
        double LFFA;
        double LFFB;

        //bool DirichletCond;
        double Tsat;

        // Use Fluxes as in Bulk Convection
        HeatConvectionInBulk NegFlux;
        HeatConvectionInBulk PosFlux;

        

        void TransformU(ref double[] U_Neg, ref double[] U_Pos, out double[] U_NegFict, out double[] U_PosFict) {

                U_NegFict = U_Pos;
                U_PosFict = U_Neg;
        }


        public double InnerEdgeForm(ref CommonParams cp, double[] U_Neg, double[] U_Pos, double[,] Grad_uA, double[,] Grad_uB, double v_Neg, double v_Pos, double[] Grad_vA, double[] Grad_vB) {
            double[] U_NegFict, U_PosFict;


            this.TransformU(ref U_Neg, ref U_Pos, out U_NegFict, out U_PosFict);

            double[] ParamsNeg = cp.Parameters_IN;
            double[] ParamsPos = cp.Parameters_OUT;
            double[] ParamsPosFict, ParamsNegFict;
            this.TransformU(ref ParamsNeg, ref ParamsPos, out ParamsNegFict, out ParamsPosFict);

            //Flux for negativ side
            double FlxNeg, FlxPos;
            if (Tsat != Double.MaxValue) {
                //if (!evapMicroRegion[cp.jCellIn]) {
                {
                    double r = 0.0;

                    // Calculate central part
                    // ======================
                    double Tavg = Tsat; // 0.5 * (U_Neg[0] + Tsat);
                    r += Tavg * (ParamsNeg[0] * cp.Normal[0] + ParamsNeg[1] * cp.Normal[1]);
                    if (m_D == 3) {
                        r += Tavg * ParamsNeg[2] * cp.Normal[2];
                    }

                    // Calculate dissipative part
                    // ==========================

                    double[] VelocityMeanIn = new double[m_D];
                    for (int d = 0; d < m_D; d++) {
                        VelocityMeanIn[d] = ParamsNeg[m_D + d];
                    }

                    double LambdaIn = LambdaConvection.GetLambda(VelocityMeanIn, cp.Normal, false);

                    double uJump = U_Neg[0] - Tavg;

                    r += LambdaIn * uJump * LFFA;

                    FlxNeg = capA * r;

                }
                //} else {

                //    BoSSS.Foundation.CommonParams inp = cp;
                //    inp.Parameters_OUT = ParamsNegFict;

                //    FlxNeg = this.NegFlux.IEF(ref inp, U_Neg, U_NegFict);
                //    //Console.WriteLine("FlxNeg = {0}", FlxNeg);
                //}

                // Flux for positive side

                //if (!evapMicroRegion[cp.jCellIn]) {
                {
                    double r = 0.0;

                    // Calculate central part
                    // ======================
                    double Tavg = Tsat; // 0.5 * (Tsat +  U_Pos[0]);

                    r += Tavg * (ParamsPos[0] * cp.Normal[0] + ParamsPos[1] * cp.Normal[1]);
                    if (m_D == 3) {
                        r += Tavg * ParamsPos[2] * cp.Normal[2];
                    }

                    // Calculate dissipative part
                    // ==========================

                    double[] VelocityMeanOut = new double[m_D];
                    for (int d = 0; d < m_D; d++) {
                        VelocityMeanOut[d] = ParamsPos[m_D + d];
                    }


                    double LambdaOut = LambdaConvection.GetLambda(VelocityMeanOut, cp.Normal, false);

                    double uJump = Tavg - U_Pos[0];

                    r += LambdaOut * uJump * LFFB;

                    FlxPos = capB * r;

                }
            } else {                
                {
                    double r = 0.0;

                    // Calculate central part
                    // ======================
                    double Tavg = 0.5 * (U_Neg[0] + U_Pos[0]);
                    r += Tavg * (ParamsNeg[0] * cp.Normal[0] + ParamsNeg[1] * cp.Normal[1]);
                    if (m_D == 3) {
                        r += Tavg * ParamsNeg[2] * cp.Normal[2];
                    }

                    // Calculate dissipative part
                    // ==========================

                    double[] VelocityMeanIn = new double[m_D];
                    for (int d = 0; d < m_D; d++) {
                        VelocityMeanIn[d] = ParamsNeg[m_D + d];
                    }

                    double LambdaIn = LambdaConvection.GetLambda(VelocityMeanIn, cp.Normal, false);

                    double uJump = U_Neg[0] - Tavg;

                    r += LambdaIn * uJump * LFFA;

                    FlxNeg = capA * r;

                }                
                // Flux for positive side

                
                {
                    double r = 0.0;

                    // Calculate central part
                    // ======================
                    double Tavg = 0.5 * (U_Neg[0] + U_Pos[0]);

                    r += Tavg * (ParamsPos[0] * cp.Normal[0] + ParamsPos[1] * cp.Normal[1]);
                    if (m_D == 3) {
                        r += Tavg * ParamsPos[2] * cp.Normal[2];
                    }

                    // Calculate dissipative part
                    // ==========================

                    double[] VelocityMeanOut = new double[m_D];
                    for (int d = 0; d < m_D; d++) {
                        VelocityMeanOut[d] = ParamsPos[m_D + d];
                    }


                    double LambdaOut = LambdaConvection.GetLambda(VelocityMeanOut, cp.Normal, false);

                    double uJump = Tavg - U_Pos[0];

                    r += LambdaOut * uJump * LFFB;

                    FlxPos = capB * r;

                }
            }
            //} else {

            //    BoSSS.Foundation.CommonParams inp = cp;
            //    inp.Parameters_IN = ParamsPosFict;

            //    FlxPos = this.PosFlux.IEF(ref inp, U_PosFict, U_Pos);
            //    //Console.WriteLine("FlxPos = {0}", FlxPos);
            //}

            if (movingmesh)
                return 0.0;
            else
                return FlxNeg * v_Neg - FlxPos * v_Pos;
        }


        public void CoefficientUpdate(CoefficientSet csA, CoefficientSet csB, int[] DomainDGdeg, int TestDGdeg) {
            this.NegFlux.CoefficientUpdate(csA, DomainDGdeg, TestDGdeg);
            this.PosFlux.CoefficientUpdate(csB, DomainDGdeg, TestDGdeg);
            if (csA.UserDefinedValues.Keys.Contains("EvapMicroRegion"))
                evapMicroRegion = (BitArray)csA.UserDefinedValues["EvapMicroRegion"];

        }

        BitArray evapMicroRegion;


        public IList<string> ArgumentOrdering {
            get {
                return new string[] { VariableNames.Temperature };
            }
        }

        public IList<string> ParameterOrdering {
            get {
                return ArrayTools.Cat(VariableNames.Velocity0Vector(m_D), VariableNames.Velocity0MeanVector(m_D));
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
                return TermActivationFlags.UxV | TermActivationFlags.V;
            }
        }
    }


    public class HeatConvectionAtLevelSet_WithEvaporation : EvaporationAtLevelSet {


        public HeatConvectionAtLevelSet_WithEvaporation(int _D, LevelSetTracker lsTrk, double _LFFA, double _LFFB,
            ThermalParameters thermParams, double _sigma)
            : base(_D, thermParams, _sigma) {

            this.LFFA = _LFFA;
            this.LFFB = _LFFB;

            this.capA = thermParams.c_A * thermParams.rho_A;
            this.capB = thermParams.c_B * thermParams.rho_B;

        }
 
        double capA;
        double capB;

        double LFFA;
        double LFFB;


        public override TermActivationFlags LevelSetTerms {
            get {
                return TermActivationFlags.V; // | TermActivationFlags.V;
            }
        }


        void TransformU(ref double[] U_Neg, ref double[] U_Pos, out double[] U_NegFict, out double[] U_PosFict) {

            U_NegFict = U_Pos;
            U_PosFict = U_Neg;
        }

        public override double InnerEdgeForm(ref CommonParams cp,
            double[] U_Neg, double[] U_Pos, double[,] Grad_uA, double[,] Grad_uB,
            double vA, double vB, double[] Grad_vA, double[] Grad_vB) {

            double[] U_NegFict, U_PosFict;
            this.TransformU(ref U_Neg, ref U_Pos, out U_NegFict, out U_PosFict);

            double[] ParamsNeg = cp.Parameters_IN;
            double[] ParamsPos = cp.Parameters_OUT;
            double[] ParamsPosFict, ParamsNegFict;
            this.TransformU(ref ParamsNeg, ref ParamsPos, out ParamsNegFict, out ParamsPosFict);


            double s = 1; // ComputeInterfaceNormalVelocity(cp.Parameters_IN.GetSubVector(2* m_D, m_D + 3), cp.Parameters_OUT.GetSubVector(2*m_D, m_D + 3), cp.Normal, cp.jCellIn);
            //Console.WriteLine("interfaceNormalVelocity = {0}", s);

            return -s * m_Tsat * (vA - vB);

            /*
            // Flux for negative side
            // ======================
            double FlxNeg = 0.0;

            // Calculate central part
            FlxNeg += m_Tsat * (ParamsNeg[0] * cp.Normal[0] + ParamsNeg[1] * cp.Normal[1]);
            if (m_D == 3) {
                FlxNeg += m_Tsat * ParamsNeg[2] * cp.Normal[2];
            }
            //FlxNeg -= Tsat * s;

            // Calculate dissipative part
            double[] VelocityMeanIn = new double[m_D];
            for (int d = 0; d < m_D; d++) {
                VelocityMeanIn[d] = ParamsNeg[m_D + d];
            }

            //double LambdaIn = LambdaConvection.GetLambda(VelocityMeanIn, cp.n, false);
            double VA_n = 0.0;
            for (int d = 0; d < VelocityMeanIn.Length; d++)
                VA_n += VelocityMeanIn[d] * cp.Normal[d];

            double LambdaIn = Math.Abs(VA_n - s);

            double uJumpA = U_Neg[0] - m_Tsat;

            FlxNeg += LambdaIn * uJumpA * LFFA;

            FlxNeg *= capA;


            // Flux for positive side
            // ======================
            double FlxPos = 0.0;

            // Calculate central part
            FlxPos += m_Tsat * (ParamsPos[0] * cp.Normal[0] + ParamsPos[1] * cp.Normal[1]);
            if (m_D == 3) {
                FlxPos += m_Tsat * ParamsPos[2] * cp.Normal[2];
            }
            //FlxPos -= Tsat * s;

            // Calculate dissipative part
            double[] VelocityMeanOut = new double[m_D];
            for (int d = 0; d < m_D; d++) {
                VelocityMeanOut[d] = ParamsPos[m_D + d];
            }

            //double LambdaOut = LambdaConvection.GetLambda(VelocityMeanOut, cp.n, false);
            double VB_n = 0.0;
            for (int d = 0; d < VelocityMeanOut.Length; d++)
                VB_n += VelocityMeanOut[d] * cp.Normal[d];

            double LambdaOut = Math.Abs(VB_n - s);

            double uJumpB = m_Tsat - U_Pos[0];

            FlxPos += LambdaOut * uJumpB * LFFB;

            FlxPos *= capB;


            return FlxNeg * vA - FlxPos * vB;
            */
        }


        public override IList<string> ArgumentOrdering {
            get {
                return new string[] { VariableNames.Temperature };
            }
        }

        public override IList<string> ParameterOrdering {
            get {
                return ArrayTools.Cat(VariableNames.Velocity0Vector(m_D), VariableNames.Velocity0MeanVector(m_D),
                    VariableNames.HeatFlux0Vector(m_D), VariableNames.Temperature0, VariableNames.Curvature, VariableNames.MassFluxExtension);
            }
        }


    }



    public class HeatConvectionAtLevelSet_MassFlux : EvaporationAtLevelSet {


        /// <summary>
        /// 
        /// </summary>
        /// <param name="_d">spatial direction</param>
        /// <param name="_D">spatial dimension</param>
        /// <param name="LsTrk"></param>
        public HeatConvectionAtLevelSet_MassFlux(int _D, LevelSetTracker LsTrk, ThermalParameters thermParams, double _sigma)
            : base(_D, thermParams, _sigma) {

            m_capA = thermParams.c_A;
            m_capB = thermParams.c_B;
        }

        double m_capA;
        double m_capB;

        public override double InnerEdgeForm(ref CommonParams cp, double[] uA, double[] uB, double[,] Grad_uA, double[,] Grad_uB, double vA, double vB, double[] Grad_vA, double[] Grad_vB) {


            double M = ComputeEvaporationMass(cp.Parameters_IN, cp.Parameters_OUT, cp.Normal, cp.jCellIn);
            if (M == 0.0)
                return 0.0;

            double massFlux = M * ((1 / m_rhoA) - (1 / m_rhoB)) * m_Tsat;


            double FlxNeg = -0.5 * m_rhoA * m_capA * massFlux;
            double FlxPos = +0.5 * m_rhoB * m_capB * massFlux;


            Debug.Assert(!(double.IsNaN(FlxNeg) || double.IsInfinity(FlxNeg)));
            Debug.Assert(!(double.IsNaN(FlxPos) || double.IsInfinity(FlxPos)));

            double Ret = FlxNeg * vA - FlxPos * vB;

            return Ret;
        }



        //public override IList<string> ParameterOrdering {
        //    get {
        //        return ArrayTools.Cat(VariableNames.HeatFlux0Vector(m_D), VariableNames.Temperature0, VariableNames.Curvature, VariableNames.DisjoiningPressure);
        //    }
        //}


    }



    public class HeatConvectionAtLevelSet_Upwind : EvaporationAtLevelSet {


        public HeatConvectionAtLevelSet_Upwind(int _D,  LevelSetTracker lsTrk, double _capA, double _capB, 
            ThermalParameters thermParams, bool _movingmesh, bool _DiriCond, double _Tsat, double _sigma)
            : base(_D, thermParams, _sigma) {


            this.capA = thermParams.c_A * thermParams.rho_A;
            this.capB = thermParams.c_B * thermParams.rho_B;

            movingmesh = _movingmesh;

            DirichletCond = _DiriCond;

        }

        double capA;
        double capB;

        bool movingmesh;

        bool DirichletCond;        



        public override double InnerEdgeForm(ref CommonParams inp,
            double[] uA, double[] uB, double[,] Grad_uA, double[,] Grad_uB,
            double vA, double vB, double[] Grad_vA, double[] Grad_vB) {

            throw new NotImplementedException("check for consistency");

            //double cNeg = 0.0;
            //double cPos = 0.0;
            //for (int d = 0; d < D; d++) {
            //    cNeg += inp.ParamsNeg[d] * inp.n[d];
            //    cPos += inp.ParamsPos[d] * inp.n[d];
            //}


            //double s = ComputeInterfaceNormalVelocity(inp.ParamsNeg, inp.ParamsPos, inp.n, inp.jCell);
            //double RelSpeedNeg = cNeg - s;
            //double RelSpeedPos = cPos - s;

            //double FluxNeg;
            //if (RelSpeedNeg >= 0) { // UP-wind with respect to relative speed
            //    FluxNeg = RelSpeedNeg * m_Tsat;
            //} else {
            //    FluxNeg = RelSpeedNeg * m_Tsat;
            //}
            //FluxNeg *= capA;

            //double FluxPos;
            //if (RelSpeedPos >= 0) { // UP-wind with respect to relative speed
            //    FluxPos = RelSpeedPos * m_Tsat;
            //} else {
            //    FluxPos = RelSpeedPos * m_Tsat;
            //}
            //FluxPos *= capB;

            //return FluxNeg * vA - FluxPos * vB;
        }





        public override IList<string> ArgumentOrdering {
            get {
                return new string[] { VariableNames.Temperature };
            }
        }

        public override IList<string> ParameterOrdering {
            get {
                return ArrayTools.Cat(VariableNames.Velocity0Vector(m_D), VariableNames.HeatFlux0Vector(m_D), VariableNames.Temperature0, VariableNames.Curvature, VariableNames.MassFluxExtension);
            }
        }


        public override TermActivationFlags LevelSetTerms {
            get {
                return TermActivationFlags.V;
            }
        }


    }
}
