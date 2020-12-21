﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using BoSSS.Foundation.XDG.OperatorFactory;
using BoSSS.Solution.NSECommon;
using BoSSS.Solution.RheologyCommon;
using BoSSS.Solution.XNSECommon.Operator.SurfaceTension;
using ilPSP.Utils;

namespace BoSSS.Solution.XNSECommon {

    /// <summary>
    /// Incompressible, constant density momentum equation in the bulk
    /// </summary>
    public class NavierStokes : BulkEquation {
        string speciesName;

        string codomainName;

        int d;

        int D;

        double rho;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spcName"></param>
        /// <param name="d">
        /// Momentum component index
        /// </param>
        /// <param name="LsTrk"></param>
        /// <param name="D">
        /// Spatial dimension
        /// </param>
        /// <param name="boundaryMap"></param>
        /// <param name="config"></param>
        public NavierStokes(
            string spcName,
            int d,
            LevelSetTracker LsTrk,
            int D,
            IncompressibleMultiphaseBoundaryCondMap boundaryMap,
            INSE_Configuration config) {
            speciesName = spcName;
            codomainName = EquationNames.MomentumEquationComponent(d);
            AddVariableNames(BoSSS.Solution.NSECommon.VariableNames.VelocityVector(D).Cat(BoSSS.Solution.NSECommon.VariableNames.Pressure));
            this.d = d;
            this.D = D;
            if(D != 2 && D != 3)
                throw new ArgumentOutOfRangeException("only supported for 2D and 3D");
            if(d < 0 || d >= D)
                throw new ArgumentOutOfRangeException();

            SpeciesId spcId = LsTrk.GetSpeciesId(spcName);
            PhysicalParameters physParams = config.getPhysParams;
            DoNotTouchParameters dntParams = config.getDntParams;

            // set species arguments
            double rhoSpc, LFFSpc, muSpc;
            switch (spcName) {
                case "A": { rhoSpc = physParams.rho_A; LFFSpc = dntParams.LFFA; muSpc = physParams.mu_A; break; }
                case "B": { rhoSpc = physParams.rho_B; LFFSpc = dntParams.LFFB; muSpc = physParams.mu_B; break; }
                default: throw new ArgumentException("Unknown species.");
            }

            rho = rhoSpc;

            // convective operator
            // ===================
            if (physParams.IncludeConvection && config.isTransport) {
                var conv = new Solution.XNSECommon.Operator.Convection.ConvectionInSpeciesBulk_LLF(D, boundaryMap, spcName, spcId, d, rhoSpc, LFFSpc, LsTrk);
                AddComponent(conv);
                AddParameter(BoSSS.Solution.NSECommon.VariableNames.Velocity0Vector(D)[d]);
                AddParameter(BoSSS.Solution.NSECommon.VariableNames.Velocity0MeanVector(D)[d]);
            }

            // pressure gradient
            // =================
            if (config.isPressureGradient) {
                var pres = new Solution.XNSECommon.Operator.Pressure.PressureInSpeciesBulk(d, boundaryMap, spcName, spcId);
                AddComponent(pres);
            }

            // viscous operator
            // ================
            if (config.isViscous && !(muSpc == 0.0)) {
                AddCoefficient("SlipLengths");
                double penalty = dntParams.PenaltySafety;
                switch (dntParams.ViscosityMode) {
                    case ViscosityMode.Standard:
                    case ViscosityMode.TransposeTermMissing: {
                        // Bulk operator:
                        var Visc1 = new Solution.XNSECommon.Operator.Viscosity.ViscosityInSpeciesBulk_GradUTerm(
                            dntParams.UseGhostPenalties ? 0.0 : penalty, 1.0,
                            boundaryMap, spcName, spcId, d, D, physParams.mu_A, physParams.mu_B);
                        AddComponent(Visc1);

                        if (dntParams.UseGhostPenalties) {
                            if (dntParams.UseGhostPenalties) {
                                var Visc1Penalty = new Solution.XNSECommon.Operator.Viscosity.ViscosityInSpeciesBulk_GradUTerm(
                                    penalty, 0.0,
                                    boundaryMap, spcName, spcId, d, D, physParams.mu_A, physParams.mu_B);
                                AddGhostComponent(Visc1Penalty);
                            }
                        }

                        break;
                    }
                    case ViscosityMode.FullySymmetric: {
                        // Bulk operator
                        var Visc1 = new Solution.XNSECommon.Operator.Viscosity.ViscosityInSpeciesBulk_GradUTerm(
                            dntParams.UseGhostPenalties ? 0.0 : penalty, 1.0,
                            boundaryMap, spcName, spcId, d, D, physParams.mu_A, physParams.mu_B);
                        AddComponent(Visc1);

                        var Visc2 = new Solution.XNSECommon.Operator.Viscosity.ViscosityInSpeciesBulk_GradUtranspTerm(
                            dntParams.UseGhostPenalties ? 0.0 : penalty, 1.0,
                            boundaryMap, spcName, spcId, d, D, physParams.mu_A, physParams.mu_B);
                        AddComponent(Visc2);


                        if (dntParams.UseGhostPenalties) {
                            var Visc1Penalty = new Solution.XNSECommon.Operator.Viscosity.ViscosityInSpeciesBulk_GradUTerm(
                                penalty, 0.0,
                                boundaryMap, spcName, spcId, d, D, physParams.mu_A, physParams.mu_B);
                            var Visc2Penalty = new Solution.XNSECommon.Operator.Viscosity.ViscosityInSpeciesBulk_GradUtranspTerm(
                                penalty, 0.0,
                                boundaryMap, spcName, spcId, d, D, physParams.mu_A, physParams.mu_B);
                            AddGhostComponent(Visc1Penalty);
                            AddGhostComponent(Visc2Penalty);
                        }
                        break;
                    }
                    case ViscosityMode.Viscoelastic: {
                        //set species arguments
                        double ReSpc, betaSpc;
                        switch (spcName) {
                            case "A": { ReSpc = physParams.reynolds_A; betaSpc = physParams.beta_a; break; }
                            case "B": { ReSpc = physParams.reynolds_B; betaSpc = physParams.beta_b; break; }
                            default: throw new ArgumentException("Unknown species.");
                        }

                        // Bulk operator:
                        var Visc1 = new Solution.XNSECommon.Operator.Viscosity.DimensionlessViscosityInSpeciesBulk_GradUTerm(
                            dntParams.UseGhostPenalties ? 0.0 : penalty, 1.0,
                            boundaryMap, spcName, spcId, d, D, physParams.reynolds_A / physParams.beta_a, physParams.reynolds_B / physParams.beta_b);
                        AddComponent(Visc1);

                        var Visc2 = new Solution.XNSECommon.Operator.Viscosity.DimensionlessViscosityInSpeciesBulk_GradUtranspTerm(
                            dntParams.UseGhostPenalties ? 0.0 : penalty, 1.0,
                            boundaryMap, spcName, spcId, d, D, physParams.reynolds_A / physParams.beta_a, physParams.reynolds_B / physParams.beta_b);
                        AddComponent(Visc2);

                        var div = new StressDivergenceInBulk(d, boundaryMap, ReSpc, dntParams.Penalty1, dntParams.Penalty2, spcName, spcId);
                        AddComponent(div);

                        break;
                    }
                    default:
                    throw new NotImplementedException();
                }
            }

            // gravity
            // ================
            if(config.isGravity) {
                string gravity = BoSSS.Solution.NSECommon.VariableNames.GravityVector(D)[d];
                string gravityOfSpecies = gravity + "#" + SpeciesName;
                var gravityComponent = new Solution.XNSECommon.Operator.MultiPhaseSource(gravityOfSpecies, speciesName);
                AddComponent(gravityComponent);
                AddParameter(gravityOfSpecies);
            }
        }

        public override string SpeciesName => speciesName;

        public override double MassScale => rho;

        public override string CodomainName => codomainName;
    }


    /// <summary>
    /// Continuity equation for the incompressible case, for constant density in the bulk.
    /// </summary>
    public class Continuity : BulkEquation {
        //Methode aus der XNSF_OperatorFactory
        string speciesName;

        string codomainName;

        double massScale;

        public Continuity(
            INSE_Configuration config,
            int D,
            string spcName,
            SpeciesId spcId,
            IncompressibleMultiphaseBoundaryCondMap BcMap) {
            codomainName = EquationNames.ContinuityEquation;
            AddVariableNames(BoSSS.Solution.NSECommon.VariableNames.VelocityVector(D));
            speciesName = spcName;
            massScale = 0;

            PhysicalParameters physParams = config.getPhysParams;
            DoNotTouchParameters dntParams = config.getDntParams;

            // set species arguments
            double rhoSpc;
            switch (spcName) {
                case "A": { rhoSpc = physParams.rho_A; break; }
                case "B": { rhoSpc = physParams.rho_B; break; }
                default: throw new ArgumentException("Unknown species.");
            }

            for (int d = 0; d < D; ++d) {
                var src = new Solution.XNSECommon.Operator.Continuity.DivergenceInSpeciesBulk_Volume(d, D, spcName, rhoSpc, dntParams.ContiSign, dntParams.RescaleConti);
                AddComponent(src);
                var flx = new Solution.XNSECommon.Operator.Continuity.DivergenceInSpeciesBulk_Edge(d, BcMap, spcName, spcId, rhoSpc, dntParams.ContiSign, dntParams.RescaleConti);
                AddComponent(flx);
            }
        }

        public override string SpeciesName => speciesName;

        public override double MassScale => massScale;

        public override string CodomainName => codomainName;
    }

    public class InterfaceContinuity : SurfaceEquation {
        string codomainName;

        //Methode aus der XNSF_OperatorFactory
        public InterfaceContinuity(INSE_Configuration config, int D, LevelSetTracker LsTrk, bool isMaterialInterface) {
            codomainName = EquationNames.ContinuityEquation;
            AddVariableNames(BoSSS.Solution.NSECommon.VariableNames.VelocityVector(D));

            PhysicalParameters physParams = config.getPhysParams;
            DoNotTouchParameters dntParams = config.getDntParams;

            // set species arguments
            double rhoA = physParams.rho_A;
            double rhoB = physParams.rho_B;

            // set components
            var divPen = new Solution.XNSECommon.Operator.Continuity.DivergenceAtLevelSet(D, LsTrk, rhoA, rhoB, isMaterialInterface, dntParams.ContiSign, dntParams.RescaleConti);
            AddComponent(divPen);
        }

        public override string FirstSpeciesName => "A";

        public override string SecondSpeciesName => "B";

        public override string CodomainName => codomainName;
    }

    /// <summary>
    /// Incompressible, Newtonian momentum equation, interface part
    /// </summary>
    public class NSEInterface : SurfaceEquation {
        string codomainName;

        //Methode aus der XNSF_OperatorFactory
        public NSEInterface(
            string phaseA,
            string phaseB,
            int d,
            int dimension,
            IncompressibleMultiphaseBoundaryCondMap boundaryMap,
            LevelSetTracker LsTrk,
            INSE_Configuration config,
            bool isMovingMesh) : base() {
            codomainName = EquationNames.MomentumEquationComponent(d);
            AddInterfaceNSE(dimension, d, boundaryMap, LsTrk, config, isMovingMesh);
            AddVariableNames(BoSSS.Solution.NSECommon.VariableNames.VelocityVector(dimension).Cat(BoSSS.Solution.NSECommon.VariableNames.Pressure));
        }

        public override string FirstSpeciesName => "A";

        public override string SecondSpeciesName => "B";

        public override string CodomainName => codomainName;

        //Methode aus der XNSF_OperatorFactory
        void AddInterfaceNSE(
            int dimension,
            int d,
            IncompressibleMultiphaseBoundaryCondMap boundaryMap,
            LevelSetTracker LsTrk,
            INSE_Configuration config,
            bool isMovingMesh) {
            PhysicalParameters physParams = config.getPhysParams;
            DoNotTouchParameters dntParams = config.getDntParams;

            // set species arguments
            double rhoA = physParams.rho_A;
            double rhoB = physParams.rho_B;
            double LFFA = dntParams.LFFA;
            double LFFB = dntParams.LFFB;
            double muA = physParams.mu_A;
            double muB = physParams.mu_B;

            //viscoelastic
            double reynoldsA = physParams.reynolds_A;
            double reynoldsB = physParams.reynolds_B;
            double betaA = physParams.beta_a;
            double betaB = physParams.beta_b;
            double[] penalty1 = dntParams.Penalty1;
            double penalty2 = dntParams.Penalty2;

            // convective operator
            // ===================
            if (physParams.IncludeConvection && config.isTransport) {
                var conv = new Solution.XNSECommon.Operator.Convection.ConvectionAtLevelSet_LLF(d, dimension, LsTrk, rhoA, rhoB, LFFA, LFFB, physParams.Material, boundaryMap, isMovingMesh);
                AddComponent(conv);
            }

            // pressure gradient
            // =================
            if (config.isPressureGradient) {
                var presLs = new Solution.XNSECommon.Operator.Pressure.PressureFormAtLevelSet(d, dimension, LsTrk);
                AddComponent(presLs);
            }

            // viscous operator
            // ================
            if (config.isViscous && (!(muA == 0.0) && !(muB == 0.0))) {

                double penalty = dntParams.PenaltySafety;
                switch (dntParams.ViscosityMode) {
                    case ViscosityMode.Standard:
                    AddComponent(new Solution.XNSECommon.Operator.Viscosity.ViscosityAtLevelSet_Standard(LsTrk, muA, muB, penalty * 1.0, d, true));
                    break;
                    case ViscosityMode.TransposeTermMissing:
                    AddComponent(new Solution.XNSECommon.Operator.Viscosity.ViscosityAtLevelSet_Standard(LsTrk, muA, muB, penalty * 1.0, d, false));
                    break;
                    case ViscosityMode.FullySymmetric:
                    AddComponent(new Solution.XNSECommon.Operator.Viscosity.ViscosityAtLevelSet_FullySymmetric(LsTrk, muA, muB, penalty, d, dntParams.UseWeightedAverages));
                    break;
                    case ViscosityMode.Viscoelastic:
                    //comps.Add(new Operator.Viscosity.ViscosityAtLevelSet_Standard(LsTrk, 1 / reynoldsA, 1 / reynoldsB, penalty * 1.0, d, false));

                    AddComponent(new Solution.XNSECommon.Operator.Viscosity.ViscosityAtLevelSet_FullySymmetric(LsTrk, betaA / reynoldsA, betaB / reynoldsB, penalty, d, dntParams.UseWeightedAverages));
                    AddComponent(new Solution.XNSECommon.Operator.Viscosity.StressDivergenceAtLevelSet(LsTrk, reynoldsA, reynoldsB, penalty1, penalty2, d, dntParams.UseWeightedAverages));

                    break;

                    default:
                    throw new NotImplementedException();
                }
            }
        }

    }

    /// <summary>
    /// 
    /// </summary>
    public class NSESurfaceTensionForce : SurfaceEquation {
        string codomainName;

        int d;

        int D;

        LevelSetTracker LsTrk;

        //Methode aus der XNSF_OperatorFactory
        public NSESurfaceTensionForce(
            string phaseA,
            string phaseB,
            int d,
            int D,
            IncompressibleMultiphaseBoundaryCondMap boundaryMap,
            LevelSetTracker LsTrk,
            INSE_Configuration config) {
            codomainName = EquationNames.MomentumEquationComponent(d);
            AddVariableNames(BoSSS.Solution.NSECommon.VariableNames.VelocityVector(D).Cat(BoSSS.Solution.NSECommon.VariableNames.Pressure));
            this.D = D;
            this.d = d;
            this.LsTrk = LsTrk;

            PhysicalParameters physParams = config.getPhysParams;
            DoNotTouchParameters dntParams = config.getDntParams;

            // set arguments
            double sigma = physParams.Sigma;

            // surface stress tensor
            // =====================
            if (config.isPressureGradient && physParams.Sigma != 0.0) {

                // isotropic part of the surface stress tensor
                // ===========================================

                if (dntParams.SST_isotropicMode == SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Flux
                    || dntParams.SST_isotropicMode == SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Local
                    || dntParams.SST_isotropicMode == SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_ContactLine) {

                    if (dntParams.SST_isotropicMode != SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_ContactLine) {
                        IEquationComponent G = new SurfaceTension_LaplaceBeltrami_Surface(d, sigma * 0.5);
                        AddSurfaceComponent(G);
                        IEquationComponent H = new SurfaceTension_LaplaceBeltrami_BndLine(d, sigma * 0.5, dntParams.SST_isotropicMode == SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Flux);
                        AddSurfaceComponent(H);
                    } else {
                        IEquationComponent isoSurfT = new IsotropicSurfaceTension_LaplaceBeltrami_Parameter(d, D, boundaryMap.EdgeTag2Type, boundaryMap, physParams.theta_e, physParams.betaL);
                        AddSurfaceComponent(isoSurfT);
                        AddParameter(BoSSS.Solution.NSECommon.VariableNames.MaxSigma);
                    }
                    AddParameter(BoSSS.Solution.NSECommon.VariableNames.NormalVector(D)[d]);
                } else if (dntParams.SST_isotropicMode == SurfaceStressTensor_IsotropicMode.Curvature_Projected
                      || dntParams.SST_isotropicMode == SurfaceStressTensor_IsotropicMode.Curvature_ClosestPoint
                      || dntParams.SST_isotropicMode == SurfaceStressTensor_IsotropicMode.Curvature_LaplaceBeltramiMean
                      || dntParams.SST_isotropicMode == SurfaceStressTensor_IsotropicMode.Curvature_Fourier) {
                    AddComponent(new CurvatureBasedSurfaceTension(d, D, LsTrk, sigma));
                    AddParameter(BoSSS.Solution.NSECommon.VariableNames.Curvature);
                } else {
                    throw new NotImplementedException("Not implemented.");
                }


                // dynamic part of the surface stress tensor
                // =========================================

                if (dntParams.SurfStressTensor != SurfaceSressTensor.Isotropic) {

                    double muI = physParams.mu_I;
                    double lamI = physParams.lambda_I;
                    double lamI_t = (physParams.lambdaI_tilde < 0) ? (lamI - muI) : physParams.lambdaI_tilde;


                    double penalty = dntParams.PenaltySafety;

                    // surface dilatational viscosity
                    if (dntParams.SurfStressTensor == SurfaceSressTensor.SurfaceDivergence ||
                        dntParams.SurfStressTensor == SurfaceSressTensor.FullBoussinesqScriven) {

                        var surfDiv = new BoussinesqScriven_SurfaceVelocityDivergence(d, D, lamI_t * 0.5, penalty, boundaryMap.EdgeTag2Type, true);
                        AddSurfaceComponent(surfDiv);

                    }

                    // surface shear viscosity 
                    if (dntParams.SurfStressTensor == SurfaceSressTensor.SurfaceRateOfDeformation ||
                        dntParams.SurfStressTensor == SurfaceSressTensor.SemiImplicit ||
                        dntParams.SurfStressTensor == SurfaceSressTensor.FullBoussinesqScriven) {

                        var surfDeformRate = new BoussinesqScriven_SurfaceDeformationRate_GradU(d, D, muI * 0.5, penalty, true, dntParams.SurfStressTensor == SurfaceSressTensor.SemiImplicit);
                        AddSurfaceComponent(surfDeformRate);

                        if (dntParams.SurfStressTensor != SurfaceSressTensor.SemiImplicit) {
                            var surfDeformRateT = new BoussinesqScriven_SurfaceDeformationRate_GradUTranspose(d, D, muI * 0.5, penalty, true);
                            AddSurfaceComponent(surfDeformRateT);
                        }

                    }
                    AddParameter(BoSSS.Solution.NSECommon.VariableNames.NormalVector(D)[d]);
                }
                // stabilization
                // =============

                switch (dntParams.STFstabilization) {
                    case DoNotTouchParameters.SurfaceTensionForceStabilization.surfaceDeformationRateLocal: {
                        AddSurfaceComponent(new SurfaceDeformationRate_LocalStabilization(d, D, false));
                        break;
                    }
                    case DoNotTouchParameters.SurfaceTensionForceStabilization.GradUxGradV: {
                        AddSurfaceComponent(new LevelSetStabilization(d, D, 0.1, LsTrk));
                        break;
                    }
                    case DoNotTouchParameters.SurfaceTensionForceStabilization.surfaceDivergence: {
                        AddSurfaceComponent(new DynamicSurfaceTension_LB_SurfaceVelocityDivergence(d, D, 0.1));
                        break;
                    }
                    case DoNotTouchParameters.SurfaceTensionForceStabilization.EdgeDissipation: {
                        AddSurfaceComponent(new DynamicSurfaceTension_LB_EdgeDissipation(d, D, sigma, 0.0));
                        break;
                    }
                    case DoNotTouchParameters.SurfaceTensionForceStabilization.None:
                    break;
                    default:
                    throw new NotImplementedException();


                }
            }

            // artificial surface tension force 
            // ================================

            if (config.isPressureGradient && physParams.useArtificialSurfaceForce) {
                AddComponent(new SurfaceTension_ArfForceSrc(d, D, LsTrk));
            }
        }

        public override string FirstSpeciesName => "A";

        public override string SecondSpeciesName => "B";

        public override string CodomainName => codomainName;


    }
}
