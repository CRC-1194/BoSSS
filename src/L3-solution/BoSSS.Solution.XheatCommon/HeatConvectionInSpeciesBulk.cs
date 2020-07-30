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
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.NSECommon;
using BoSSS.Solution.Utils;


namespace BoSSS.Solution.XheatCommon {

    public class HeatConvectionInSpeciesBulk_LLF : LinearizedHeatConvection, ISpeciesFilter, IEquationComponentCoefficient {

        

        public HeatConvectionInSpeciesBulk_LLF(int SpatDim, ThermalMultiphaseBoundaryCondMap _bcmap, string spcName, SpeciesId spcId, 
            double _cap, double _LFF, LevelSetTracker _lsTrk)
            : base(SpatDim, _bcmap) {

            this.cap = _cap;
            this.m_spcId = spcId;
            validSpeciesId = spcName;

            this.lsTrk = _lsTrk;
            this.LFF = _LFF;
            this.m_bcmap = _bcmap;


            //base.VelFunction = new Func<double[], double, double>[GridCommons.FIRST_PERIODIC_BC_TAG, SpatDim];
            //for (int d = 0; d < SpatDim; d++)
            //    base.VelFunction.SetColumn(m_bcmap.bndFunction[VariableNames.Velocity_d(d) + "#" + spcName], d);

            base.TempFunction = m_bcmap.bndFunction[VariableNames.Temperature + "#" + spcName];

            //SubGrdMask = lsTrk.Regions.GetSpeciesSubGrid(spcId).VolumeMask.GetBitMaskWithExternal();
        }

        ThermalMultiphaseBoundaryCondMap m_bcmap;
        LevelSetTracker lsTrk;

        double LFF;

        double cap;

        SpeciesId m_spcId;

        public string validSpeciesId {
            get;
            private set;
        }


        protected System.Collections.BitArray SubGrdMask;



        internal double IEF(ref BoSSS.Foundation.CommonParams inp, double[] Uin, double[] Uout) {
            return this.InnerEdgeFlux(ref inp, Uin, Uout);
        }

        protected bool basecall = false;

        protected override double InnerEdgeFlux(ref BoSSS.Foundation.CommonParams inp, double[] Uin, double[] Uout) {
            if (basecall) {
                return base.InnerEdgeFlux(ref inp, Uin, Uout);
            } else {

                double UinBkUp = Uin[0];
                double UoutBkUp = Uout[0];
                double[] InParamsBkup = inp.Parameters_IN;
                double[] OutParamsBkup = inp.Parameters_OUT;


                // subgrid boundary handling
                // -------------------------

                if (inp.iEdge >= 0 && inp.jCellOut >= 0) {

                    bool CellIn = SubGrdMask[inp.jCellIn];
                    bool CellOut = SubGrdMask[inp.jCellOut];
                    Debug.Assert(CellIn || CellOut, "at least one cell must be in the subgrid!");

                    if (CellOut == true && CellIn == false) {
                        // IN-cell is outside of subgrid: extrapolate from OUT-cell!
                        Uin[0] = Uout[0];
                        inp.Parameters_IN = inp.Parameters_OUT.CloneAs();

                    }
                    if (CellIn == true && CellOut == false) {
                        // ... and vice-versa
                        Uout[0] = Uin[0];
                        inp.Parameters_OUT = inp.Parameters_IN.CloneAs();
                    }
                }

                // evaluate flux function
                // ----------------------

                var flx = base.InnerEdgeFlux(ref inp, Uin, Uout);
                flx *= cap;

                // cleanup mess and return
                // -----------------------

                Uout[0] = UoutBkUp;
                Uin[0] = UinBkUp;
                inp.Parameters_IN = InParamsBkup;
                inp.Parameters_OUT = OutParamsBkup;

                return flx;
            }
        }


        protected override double BorderEdgeFlux(ref BoSSS.Foundation.CommonParamsBnd inp, double[] Uin) {

            this.basecall = true;
            double flx = base.BorderEdgeFlux(ref inp, Uin);
            this.basecall = false;

            flx *= cap;

            return flx;
        }


        protected override void Flux(ref Foundation.CommonParamsVol inp, double[] U, double[] output) {
            base.Flux(ref inp, U, output);
            output.ScaleV(cap);
        }


        public void CoefficientUpdate(CoefficientSet cs, int[] DomainDGdeg, int TestDGdeg) {
            SubGrdMask = lsTrk.Regions.GetSpeciesSubGrid(m_spcId).VolumeMask.GetBitMaskWithExternal();
        }


    }


    public class HeatConvectionInSpeciesBulk_Upwind : LinearFlux, ISpeciesFilter {

        /// <summary>
        /// Spatial dimension;
        /// </summary>
        protected int m_SpatialDimension;

        ThermalMultiphaseBoundaryCondMap m_bcmap;

        /// <summary>
        /// Mapping from edge tags to boundary values.<br/>
        /// 1st index: edge tag;<br/>
        /// 2nd index: spatial direction
        /// </summary>
        protected Func<double[], double, double>[,] VelFunction;

        protected Func<double[], double, double>[] TempFunction;

        public HeatConvectionInSpeciesBulk_Upwind(int SpatDim, ThermalMultiphaseBoundaryCondMap _bcmap, string spcName, SpeciesId spcId, double _cap) {

            this.m_SpatialDimension = SpatDim;

            this.cap = _cap;
            this.m_spcId = spcId;
            this.validSpeciesId = spcName;
            this.m_bcmap = _bcmap;

            this.VelFunction = new Func<double[], double, double>[GridCommons.FIRST_PERIODIC_BC_TAG, SpatDim];
            for (int d = 0; d < SpatDim; d++)
                this.VelFunction.SetColumn(m_bcmap.bndFunction[VariableNames.Velocity_d(d) + "#" + spcName], d);

            this.TempFunction = m_bcmap.bndFunction[VariableNames.Temperature + "#" + spcName];
        }

        double cap;

        SpeciesId m_spcId;

        public string validSpeciesId {
            get;
            private set;
        }


        public override IList<string> ArgumentOrdering {
            get {
                return new string[] { VariableNames.Temperature };
            }
        }

        public override IList<string> ParameterOrdering {
            get {
                return ArrayTools.Cat(VariableNames.Velocity0Vector(m_SpatialDimension)); //, VariableNames.Velocity0MeanVector(m_SpatialDimension));
            }
        }


        protected override void Flux(ref CommonParamsVol inp, double[] U, double[] output) {

            for(int d = 0; d < m_SpatialDimension; d++)
                output[d] = cap * U[0] * inp.Parameters[d];

        }


        protected override double BorderEdgeFlux(ref CommonParamsBnd inp, double[] Uin) {

            double c = 0.0;
            for (int d = 0; d < m_SpatialDimension; d++)
                c += inp.Parameters_IN[d] * inp.Normal[d];

            ThermalBcType edgeType = m_bcmap.EdgeTag2Type[inp.EdgeTag];

            switch (edgeType) {
                case ThermalBcType.ConstantTemperature: {
                        return (c * cap * TempFunction[inp.EdgeTag](inp.X, inp.time));
                    }
                case ThermalBcType.ZeroGradient:
                case ThermalBcType.ConstantHeatFlux: {
                        return (c * cap * Uin[0]);
                    }
                default:
                    throw new NotImplementedException("Boundary condition not implemented!");
            }
        }


        protected override double InnerEdgeFlux(ref CommonParams inp, double[] Uin, double[] Uout) {

            double c = 0.0;
            for (int d = 0; d < m_SpatialDimension; d++)
                c += 0.5 * (inp.Parameters_IN[d] + inp.Parameters_OUT[d]) * inp.Normal[d];

            if (c > 0)
                return (c * cap * Uin[0]);
            else
                return (c * cap * Uout[0]);


        }
    }
}
