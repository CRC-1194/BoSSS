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
using BoSSS.Foundation;
using BoSSS.Solution.Utils;
using BoSSS.Solution.NSECommon;
using ilPSP.Utils;
using BoSSS.Foundation.Grid.Classic;
using ilPSP;

namespace BoSSS.Application.Rheology {

    /// <summary>
    /// Central difference scheme for divergence operator of the extra stress tensor.
    /// </summary>

    public class StressDivergence_Cockburn : LinearFlux {

        int Component;           // spatial dimension of momentum equation
        BoundaryCondMap<IncompressibleBcType> m_BcMap;
        double InverseReynolds;
        double[] pen1;
        double pen2;
        /// <summary>
        /// Velocity Function for Dirichlet BC
        /// </summary>
        protected Func<double[], double, double>[,] VelFunction;
        /// <summary>
        /// Stress Function for Dirichlet BC
        /// </summary>
        protected Func<double[], double, double>[,] StressFunction;
        
        

        /// <summary>
        /// Mapping from edge tags to boundary values.<br/>
        /// 1st index: edge tag;<br/>
        /// 2nd index: spatial direction
        /// </summary>
        public override IList<string> ArgumentOrdering {
            get {
                switch (Component) {
                    case 0:
                        return new string[] { VariableNames.StressXX, VariableNames.StressXY, VariableNames.VelocityX};
                    case 1:
                        return new string[] { VariableNames.StressXY, VariableNames.StressYY, VariableNames.VelocityY};
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Calculating the stress divergence term in the momentum equations following the local DG ansatz by Cockburn (2002)
        /// </summary>
        public StressDivergence_Cockburn(int Component, BoundaryCondMap<IncompressibleBcType> _BcMap, double Reynolds, double[] Penalty1, double Penalty2) {
            this.Component = Component;
            this.m_BcMap = _BcMap;
            this.InverseReynolds =  -1/(Reynolds);
            this.pen1 = Penalty1;
            this.pen2 = Penalty2;

            StressFunction = new Func<double[], double, double>[GridCommons.FIRST_PERIODIC_BC_TAG, 3];

            StressFunction.SetColumn(m_BcMap.bndFunction[VariableNames.StressXX], 0);
            StressFunction.SetColumn(m_BcMap.bndFunction[VariableNames.StressXY], 1);
            StressFunction.SetColumn(m_BcMap.bndFunction[VariableNames.StressYY], 2);

            VelFunction = new Func<double[], double, double>[GridCommons.FIRST_PERIODIC_BC_TAG, 2];

            VelFunction.SetColumn(m_BcMap.bndFunction[VariableNames.VelocityX], 0);
            VelFunction.SetColumn(m_BcMap.bndFunction[VariableNames.VelocityY], 1);
        }

        /// <summary>
        /// Stress divergence volume term
        /// </summary>
        protected override void Flux(ref CommonParamsVol inp, double[] T, double[] output) {
            int D = output.Length;
            Array.Clear(output, 0, D);

            output[0] = InverseReynolds * T[0];
            output[1] = InverseReynolds * T[1];

        }

        /// <summary>
        /// Stress divergence inner edge term
        /// </summary>
        protected override double InnerEdgeFlux(ref CommonParams inp, double[] Tin, double[] Tout) {

            double res = 0;

            int jIn = inp.jCellIn;
            int jOt = inp.jCellOut;

            double h_min_in_loc = inp.GridDat.iGeomCells.h_min[jIn];
            double h_min_ot_loc = inp.GridDat.iGeomCells.h_min[jOt];
            double h_Edge = ((GridData.EdgeData)(inp.GridDat.iGeomEdges)).h_min_Edge[inp.iEdge];
            double h = Math.Min(h_min_in_loc, h_min_ot_loc);
            double h2 = Math.Min(h, h_Edge);
            


            res += 0.5 * (Tin[0] + Tout[0])* inp.Normale[0] + 0.5 * (Tin[1] + Tout[1]) * inp.Normale[1]; // central difference for stress divergence

            switch (Component)
            {
                case 0:
                    res += -pen2 / h2 * (Tin[2] - Tout[2]);
                    break;
                case 1:
                    res += -pen2 / h2 * (Tin[2] - Tout[2]);
                    break;
                default:
                    throw new NotImplementedException();


            }


            return InverseReynolds * res;
        }

        /// <summary>
        /// Stress divergence border edge term
        /// </summary>
        protected override double BorderEdgeFlux(ref CommonParamsBnd inp, double[] Tin) {
            double res = 0;

            int jIn = inp.jCellIn;

            double h = inp.GridDat.iGeomCells.h_min[jIn];

            IncompressibleBcType edgType = m_BcMap.EdgeTag2Type[inp.EdgeTag];

            switch (edgType) {
                case IncompressibleBcType.Outflow:
                case IncompressibleBcType.Pressure_Outlet:


                    // Atmospheric outlet/pressure outflow: hom. Neumann
                    res += Tin[0] * inp.Normale[0];
                    res += Tin[1] * inp.Normale[1];
                break;

                case IncompressibleBcType.FreeSlip:

                    //Free slip wall for symmetry lilne of symmetric channel
                    res += inp.Normale[0] * Tin[0] * inp.Normale[0] * inp.Normale[0];
                    res += inp.Normale[1] * Tin[1] * inp.Normale[1] * inp.Normale[1];
                    break;

                case IncompressibleBcType.Velocity_Inlet:
                case IncompressibleBcType.Wall:

                    double VelocityX = VelFunction[inp.EdgeTag, 0](inp.X, inp.time);
                    double VelocityY = VelFunction[inp.EdgeTag, 1](inp.X, inp.time);

                    switch (Component)
                    {
                        case 0:
                            res += Tin[0]* inp.Normale[0];
                            res += Tin[1]* inp.Normale[1];
                             //alpha penalty for boundary (no beta penalty)
                            res += -pen2 / h * (Tin[2] - VelocityX); 

                            break;
                        case 1:
                            res += Tin[0] * inp.Normale[0];
                            res += Tin[1] * inp.Normale[1];
                            //alpha penalty for boundary (no beta penalty)
                            res += -pen2 / h * (Tin[2] - VelocityY);

                            break;
                        default:
                            throw new NotImplementedException();
                    }
                break;

                default:
                throw new NotImplementedException("unsupported/unknown b.c. - missing implementation;");
            }
            return InverseReynolds * res;
        }
    }
}