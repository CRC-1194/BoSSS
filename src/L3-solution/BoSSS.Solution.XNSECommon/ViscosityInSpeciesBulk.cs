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

using BoSSS.Foundation.XDG;
using BoSSS.Solution.NSECommon;
using ilPSP;
using ilPSP.Utils;
using System;

namespace BoSSS.Solution.XNSECommon.Operator.Viscosity {

    public class ViscosityInSpeciesBulk_GradUTerm : BoSSS.Solution.NSECommon.SipViscosity_GradU, ISpeciesFilter {

        public ViscosityInSpeciesBulk_GradUTerm(double penalty_safety, double sw, IncompressibleMultiphaseBoundaryCondMap bcMap, string spcName, int _d, int _D,
            double _muA, double _muB, double _betaS = 0.0)
            : base(penalty_safety, _d, _D, bcMap, NSECommon.ViscosityOption.ConstantViscosity, constantViscosityValue: double.NegativeInfinity) {
            base.m_alpha = sw;
            this.m_bcMap = bcMap;

            //double complementMu;
            ValidSpecies = spcName;
            switch(spcName) {
                case "A": currentMu = _muA; 
                //complementMu = _muB; 
                break;
                case "B": 
                currentMu = _muB; 
                //complementMu = _muA;
                break;
                default: throw new ArgumentException("Unknown species.");
            }

            //double muFactor = Math.Max(currentMu, complementMu) / currentMu;
            //Console.WriteLine("muFactor is: " + muFactor + " in species " + spcName);
            //base.m_penalty_base = penalty_safety * muFactor; // totally wrong scaling; fk, 16feb22, 04:05 AM

            int D = base.m_D;
            base.velFunction = D.ForLoop(d => this.m_bcMap.bndFunction[VariableNames.Velocity_d(d) + "#" + spcName]);
            base.g_Neu_GradU = D.ForLoop(d => this.m_bcMap.bndFunction[VariableNames.Velocity_GradientVector(_D).GetRow(_d)[d] + "#" + spcName]);

            m_beta = _betaS;
        }

        public string ValidSpecies {
            get;
            private set;
        }

        private double currentMu;


        private IncompressibleBoundaryCondMap m_bcMap;

        protected override double Viscosity(double[] Parameters, double[] Velocity, double[,] VelocityGrad) {
            return currentMu;
        }
    }

    public class ViscosityInSpeciesBulk_GradUtranspTerm : BoSSS.Solution.NSECommon.SipViscosity_GradUtransp, ISpeciesFilter {

        public ViscosityInSpeciesBulk_GradUtranspTerm(double penalty, double sw, IncompressibleBoundaryCondMap bcMap, string spcName, int _d, int _D,
            double _muA, double _muB, double _betaS = 0.0)
            : base(penalty, _d, _D, bcMap, NSECommon.ViscosityOption.ConstantViscosity, constantViscosityValue: double.NegativeInfinity) {
            base.m_alpha = sw;
            this.m_bcMap = bcMap;

            ValidSpecies = spcName;
            switch(spcName) {
                case "A": currentMu = _muA;  break;
                case "B": currentMu = _muB;  break;
                default: throw new ArgumentException("Unknown species.");
            }

            //double muFactor = Math.Max(currentMu, complementMu) / currentMu;
            //base.m_penalty_base = penalty * muFactor; // totally wrong scaling; fk, 16feb22, 04:05 AM

            int D = base.m_D;
            base.velFunction = D.ForLoop(d => this.m_bcMap.bndFunction[VariableNames.Velocity_d(d) + "#" + spcName]);
            base.g_Neu_GradU = D.ForLoop(d => this.m_bcMap.bndFunction[VariableNames.Velocity_GradientVector(_D).GetRow(d)[_d]]);

            m_beta = _betaS;
        }

        public string ValidSpecies {
            get;
            private set;
        }

        private double currentMu;

        private IncompressibleBoundaryCondMap m_bcMap;

        protected override double Viscosity(double[] Parameters, double[] Velocity, double[,] VelocityGrad) {
            return currentMu;
        }
    }

    public class DimensionlessViscosityInSpeciesBulk_GradUTerm : BoSSS.Solution.NSECommon.SipViscosity_GradU, ISpeciesFilter {

        public DimensionlessViscosityInSpeciesBulk_GradUTerm(double penalty, double sw, IncompressibleBoundaryCondMap bcMap, string spcName, int _d, int _D,
            double _reynoldsA, double _reynoldsB)
            : base(penalty, _d, _D, bcMap, NSECommon.ViscosityOption.ConstantViscosityDimensionless, reynolds: 0.0) {
            base.m_alpha = sw;
            this.m_bcMap = bcMap;

            ValidSpecies = spcName;
            switch (spcName) {
                case "A": base.m_reynolds = _reynoldsA; break;
                case "B": base.m_reynolds = _reynoldsB; break;
                default: throw new ArgumentException("Unknown species.");
            }

            int D = base.m_D;
            base.velFunction = D.ForLoop(d => this.m_bcMap.bndFunction[VariableNames.Velocity_d(d) + "#" + spcName]);
        }

        public string ValidSpecies {
            get;
            private set;
        }

        private IncompressibleBoundaryCondMap m_bcMap;
    }

    public class DimensionlessViscosityInSpeciesBulk_GradUtranspTerm : BoSSS.Solution.NSECommon.SipViscosity_GradUtransp, ISpeciesFilter {

        public DimensionlessViscosityInSpeciesBulk_GradUtranspTerm(double penalty, double sw, IncompressibleBoundaryCondMap bcMap, string spcName, int _d, int _D,
            double _reynoldsA, double _reynoldsB)
            : base(penalty, _d, _D, bcMap, NSECommon.ViscosityOption.ConstantViscosityDimensionless, reynolds: 0.0) {
            base.m_alpha = sw;
            this.m_bcMap = bcMap;

            ValidSpecies = spcName;
            switch (spcName) {
                case "A": base.m_reynolds = _reynoldsA; break;
                case "B": base.m_reynolds = _reynoldsB; break;
                default: throw new ArgumentException("Unknown species.");
            }

            int D = base.m_D;
            base.velFunction = D.ForLoop(d => this.m_bcMap.bndFunction[VariableNames.Velocity_d(d) + "#" + spcName]);
        }

        public string ValidSpecies {
            get;
            private set;
        }

        private IncompressibleBoundaryCondMap m_bcMap;
    }

    public class LowMachSpeciesBalanceDiffusionBulk : SIPDiffusionMassFractions, ISpeciesFilter {

        public LowMachSpeciesBalanceDiffusionBulk(string spcName, double PenaltyBase,
                                     IncompressibleBoundaryCondMap BcMap,
                                     MaterialLaw EoS,
                                     double Reynolds,
                                     double Prandtl,
                                     double[] Lewis,
                                     int massFractionComponent,
                                     int NoOfComponents) : base(PenaltyBase, BcMap, EoS, Reynolds, Prandtl, Lewis, massFractionComponent, NoOfComponents) {
            ValidSpecies = spcName;
        }

        public string ValidSpecies {
            get;
            private set;
        }
    }

    /// <summary>
    /// Heat conduction term of the energy equation for LowMach solver.
    /// \/ *(k \/ T)
    /// </summary>
    public class LowMachEnergyConductionBulk : SIPDiffusionTemperature, ISpeciesFilter {

        public LowMachEnergyConductionBulk(string spcName, double PenaltyBase,
                                     IncompressibleBoundaryCondMap BcMap,
                                     MaterialLaw EoS,
                                     double Reynolds,
                                     double Prandtl,
                                     bool prmsOK,
                                     ThermalWallType thermalwalltype) : base(PenaltyBase, BcMap, EoS, Reynolds, Prandtl, prmsOK, thermalwalltype ) {
            ValidSpecies = spcName;
        }

        public string ValidSpecies {
            get;
            private set;
        }
    }

    public class LowMachViscosityInSpeciesBulk_AllTerms : SipViscosity_Variable, ISpeciesFilter {

        public LowMachViscosityInSpeciesBulk_AllTerms(string spcName, double _penalty, int iComp, int D, IncompressibleBoundaryCondMap bcmap, ViscosityOption _ViscosityMode, double constantViscosityValue = double.NaN, double reynolds = double.NaN, MaterialLaw EoS = null, bool ignoreVectorized = false) : base(_penalty, iComp, D, (ViscosityTermsSwitch.grad_u | ViscosityTermsSwitch.grad_uT | ViscosityTermsSwitch.divU), bcmap, _ViscosityMode, constantViscosityValue, reynolds, EoS, ignoreVectorized) {
            ValidSpecies = spcName;
        }

        public string ValidSpecies {
            get;
            private set;
        }
    }
}