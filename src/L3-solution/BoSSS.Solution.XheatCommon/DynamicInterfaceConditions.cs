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
using BoSSS.Solution.Utils;
using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using ilPSP.Utils;
using BoSSS.Platform;
using System.Diagnostics;
using BoSSS.Solution.NSECommon;
using ilPSP;
using System.Collections;
using BoSSS.Solution.XheatCommon;

namespace BoSSS.Solution.XheatCommon {


    public class MassFluxAtInterface : EvaporationAtLevelSet {


        /// <summary>
        /// 
        /// </summary>
        /// <param name="_d">spatial direction</param>
        /// <param name="_D">spatial dimension</param>
        /// <param name="LsTrk"></param>
        public MassFluxAtInterface(int _d, int _D, ThermalParameters thermParams, double _sigma, bool _movingMesh) 
            : base(_D, thermParams, _sigma) {

            this.m_d = _d;
            if (m_d >= m_D)
                throw new ArgumentOutOfRangeException();

            this.movingMesh = _movingMesh;
        }

        int m_d;

        bool movingMesh;


        public override double InnerEdgeForm(ref CommonParams cp, double[] uA, double[] uB, double[,] Grad_uA, double[,] Grad_uB, double vA, double vB, double[] Grad_vA, double[] Grad_vB) {

            double[] Normal = cp.Normal;

            double M = ComputeEvaporationMass(cp.Parameters_IN, cp.Parameters_OUT, cp.Normal, cp.jCellIn);
            if (M == 0.0)
                return 0.0;

            double massFlux = M.Pow2() * ((1 / m_rhoA) - (1 / m_rhoB)) * Normal[m_d];
               
            double p_disp = cp.Parameters_IN[1];
            // augmented capillary pressure
            //double acp_jump = 0.0;
            //if(!double.IsNaN(p_disp))
            //    acp_jump = massFlux + p_disp;
            //else
            //    acp_jump = massFlux;


            double FlxNeg = -0.5 * massFlux;
            double FlxPos = +0.5 * massFlux;


            Debug.Assert(!(double.IsNaN(FlxNeg) || double.IsInfinity(FlxNeg)));
            Debug.Assert(!(double.IsNaN(FlxPos) || double.IsInfinity(FlxPos)));

            double Ret = FlxNeg * vA - FlxPos * vB;


            // moving-mesh-contribution
            // ========================

            if (movingMesh) {
                double s = ComputeInterfaceNormalVelocity(cp.Parameters_IN, cp.Parameters_OUT, cp.Normal, cp.jCellIn);
                //Console.WriteLine("interface normal velocity = {0}", s);
                double movingFlux;
                if (s > 0) { // select DOWN-wind!
                    movingFlux = (-s) * cp.Parameters_OUT[m_D + 3 + m_d]; // uB[0];
                } else {
                    movingFlux = (-s) * cp.Parameters_IN[m_D + 3 + m_d]; // uA[0];
                }

                Ret -= movingFlux * Normal[m_d] * 0.5 * (vA + vB);
            }

            return Ret;
        }



        public override IList<string> ParameterOrdering {
            get {
                return ArrayTools.Cat(VariableNames.HeatFlux0Vector(m_D), VariableNames.Temperature0, VariableNames.Curvature, VariableNames.MassFluxExtension, VariableNames.Velocity0Vector(m_D));
            }
        }


    }


}
