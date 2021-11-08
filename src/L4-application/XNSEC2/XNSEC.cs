﻿using BoSSS.Application.XNSE_Solver;
using BoSSS.Application.XNSFE_Solver;
using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.IO;
using BoSSS.Foundation.XDG;
using BoSSS.Foundation.XDG.OperatorFactory;
using BoSSS.Solution;
using BoSSS.Solution.AdvancedSolvers;
using BoSSS.Solution.Control;
using BoSSS.Solution.LevelSetTools.SolverWithLevelSetUpdater;
using BoSSS.Solution.NSECommon;
using BoSSS.Solution.XheatCommon;
using BoSSS.Solution.XNSECommon;
using CommandLine;
using ilPSP;
using ilPSP.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BoSSS.Application.XNSEC {

    public partial class XNSEC : SolverWithLevelSetUpdater<XNSEC_Control> {

        //===========
        // Main file
        //===========
        private static void Main(string[] args) {
            //InitMPI();
            //DeleteOldPlotFiles();

            //NUnitTest.COMBUSTION_TEST();
            //NUnitTest.COMBUSTION_CoFlowFlame_TEST();
            //NUnit.Framework.Assert.AreEqual(true, false, "remove me");



            //BoSSS.Application.XNSFE_Solver.Tests.ASUnitTest.SteadyStateEvaporationTest(0.0, 3, 0.0, true, XQuadFactoryHelper.MomentFittingVariants.Saye, SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Flux, NonLinearSolverCode.Newton);
            //NUnit.Framework.Assert.AreEqual(true, false, "remove me");



            //NUnitTest.IncompressibleSteadyPoiseuilleFlowTest(); //
            //NUnitTest.CavityNaturalConvection();
            //NUnitTest.IncompressibleSteadyPoiseuilleFlowTest(); //
            //NUnitTest.LowMachSteadyCouetteWithTemperatureGradientTest(); //
            //NUnitTest.ManufacturedSolutionLowMachCombustionTest(); //
            //NUnitTest.IncompressibleUnsteadyTaylorVortexTest(); //

            //NUnitTest.PolynomialTestForConvectionTest(2, 3, 0.0, false, XQuadFactoryHelper.MomentFittingVariants.Saye, SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Flux);
            //NUnit.Framework.Assert.AreEqual(true, false, "remove me");
            //NUnitTest.NuNit_ChannelTest(2, 0.0, ViscosityMode.FullySymmetric, 60.0 * Math.PI / 180.0, XQuadFactoryHelper.MomentFittingVariants.Saye);

            //NUnitTest.LevelSetAdvectionTest2D(2, 0, Solution.LevelSetTools.LevelSetEvolution.FastMarching, Solution.XdgTimestepping.LevelSetHandling.LieSplitting, false);
            //NUnitTest.BcTest_PressureOutletTest(2, 2, 0.0, XQuadFactoryHelper.MomentFittingVariants.Saye, SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Flux, false);
            //NUnitTest.TranspiratingChannelTest(2, 0.1, 0.2, ViscosityMode.FullySymmetric, false, XQuadFactoryHelper.MomentFittingVariants.OneStepGaussAndStokes, NonLinearSolverCode.Newton);
            //NUnit.Framework.Assert.AreEqual(true, false, "remove me");

            //NUnitTest.PseudoTwoDimensionalTwoPhaseFlow(2, 0, false, XQuadFactoryHelper.MomentFittingVariants.OneStepGaussAndStokes, SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Flux, differentFluids: false);
            //NUnit.Framework.Assert.AreEqual(true, false, "remove me");


            //Console.WriteLine("tests passed!!!!!!!!!!!");
            //NUnit.Framework.Assert.AreEqual(true, false, "remove me");

            //NUnitTest.ViscosityJumpTest(2, 2, 0.0, ViscosityMode.FullySymmetric, XQuadFactoryHelper.MomentFittingVariants.Saye, SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Local);

            //Console.WriteLine("tests passed!!!!!!!!!!!");
            //NUnit.Framework.Assert.AreEqual(true, false, "remove me");
            //NUnitTest.CavityNaturalConvectionTest_Homotopy(); //TODO
            //   NUnitTest.CavityNaturalConvection_AMR_eachNewtonIteration();TODO
            //NUnitTest.TwoPhaseIncompressibleSteadyPoiseuilleFlowTest(); // TODO!
            ////NUnitTest.ThermodynamicPressureTest(); // TODO

            //BoSSS.Solution.Application<XNSEC_Control>._Main(new string[] { "--control", "cs:BoSSS.Application.XNSEC.FullNSEControlExamples.PseudoTwoDimensionalTwoPhaseFlow()", "--delplt" }, false, delegate () {
            //    var p = new XNSEC();
            //    return p;
            //});
            //NUnit.Framework.Assert.AreEqual(true, false, "remove me");

            //-n 4./ XNSEC.exe - c "cs:BoSSS.Application.XNSEC.FullNSEControlExamples.NaturalConvectionSquareCavityTest()"

            //System.Threading.Thread.Sleep(10000);

            bool MixtureFractionCalculation = false;

            try {
                // peek at control file and select correct solver depending on controlfile type
                // parse arguments
                args = ArgsFromEnvironmentVars(args);
                CommandLineOptions opt = new CommandLineOptions();
                var parser = new CommandLine.CommandLineParser(new CommandLineParserSettings(Console.Error));
                bool argsParseSuccess;
                argsParseSuccess = parser.ParseArguments(args, opt);

                if (!argsParseSuccess) {
                    System.Environment.Exit(-1);
                }

                if (opt.ControlfilePath != null) {
                    opt.ControlfilePath = opt.ControlfilePath.Trim();
                }

                LoadControlFile(opt.ControlfilePath, out XNSEC_Control ctrlV2, out XNSEC_Control[] ctrlV2_ParameterStudy);
                MixtureFractionCalculation = ctrlV2 is XNSEC_MF_Control | ctrlV2_ParameterStudy is XNSEC_MF_Control[];
            } catch {
                Console.WriteLine("Error while determining control type, using default behavior for 'XNSEC_Control'");
            }

            if (MixtureFractionCalculation) {
                _Main(args, false, delegate () {
                    var p = new XNSEC_MixtureFraction();
                    return p;
                });
            } else {
                _Main(args, false, delegate () {
                    var p = new XNSEC();
                    return p;
                });
            }

            //_Main(args, false, delegate () {
            //    var p = new XNSEC();
            //    return p;
            //});
        }

        #region Operator configuration

        protected override ILevelSetParameter GetLevelSetVelocity(int iLevSet) {
            if (iLevSet == 0) {
                // Main Difference to base implementation:
                //var levelSetVelocity = new LevelSetVelocityEvaporative("Phi", GridData.SpatialDimension, VelocityDegree(), Control.InterVelocAverage, Control.PhysicalParameters, new XNSFE_OperatorConfiguration(Control);

                var config = new XNSEC_OperatorConfiguration(Control);
                var levelSetVelocity = config.isEvaporation ?
                    new LevelSetVelocityGeneralNonMaterial(VariableNames.LevelSetCG, GridData.SpatialDimension, VelocityDegree(), Control.InterVelocAverage, Control.PhysicalParameters, config) :
                    new LevelSetVelocity(VariableNames.LevelSetCG, GridData.SpatialDimension, VelocityDegree(), Control.InterVelocAverage, Control.PhysicalParameters);
                return levelSetVelocity;
            } else {
                throw new NotImplementedException("");
            }
        }

        /// <summary>
        /// - 4x the velocity degree if convection is included (quadratic term in convection times
        ///   density times test function yields quadruple order)
        /// - 2x the velocity degree in the Stokes case
        /// </summary>
        /// <remarks>
        /// Note: Sayes algorithm can be regarded as a nonlinear transformation to the [-1,1]
        /// reference Element. We transform $`\int f dx $` to the reference Element, $`\int f dx =
        /// \int f(T) |det D(T)| d\hat{x} $` Suppose $`f$` has degree $`n$` and suppose the
        /// transformation $`T$` has degree $`p$`, then the integrand in reference space has
        /// approximately degree $`\leq n * p + (p - 1) $` This is problematic, because we need to
        /// find $`\sqrt(n * p + (p - 1))$` roots of the level set function, if we want to integrate
        /// $`f$` exactly. This goes unnoticed when verifying the quadrature method via
        /// volume/surface integrals with constant $`f = 1$`. When evaluating a constant function,
        /// $`n = 0$`, the degree of the integrand immensely simplifies to $`(p - 1)$`.
        /// </remarks>
        override public int QuadOrder() {
            if (Control.CutCellQuadratureType != XQuadFactoryHelper.MomentFittingVariants.Saye
               && Control.CutCellQuadratureType != XQuadFactoryHelper.MomentFittingVariants.OneStepGaussAndStokes) {
                throw new ArgumentException($"The XNSE solver is only verified for cut-cell quadrature rules " +
                    $"{XQuadFactoryHelper.MomentFittingVariants.Saye} and {XQuadFactoryHelper.MomentFittingVariants.OneStepGaussAndStokes}; " +
                    $"you have set {Control.CutCellQuadratureType}, so you are notified that you reach into unknown territory; " +
                    $"If you do not know how to remove this exception, you should better return now!");
            }

            //QuadOrder

            int degU = VelocityDegree();
            int quadOrder = degU * (this.Control.PhysicalParameters.IncludeConvection ? 4 : 2);
            if (this.Control.CutCellQuadratureType == XQuadFactoryHelper.MomentFittingVariants.Saye) {
                //See remarks
                quadOrder *= 2;
                quadOrder += 1;
            }
            return quadOrder;
        }

        protected override int NoOfLevelSets {
            get {
                return 1;
            }
        }

        /// <summary>
        /// Either fluids A and B; or A, B and solid C.
        /// </summary>
        protected override Array SpeciesTable {
            get {
                return new[] { "A", "B" };
            }
        }

        /// <summary>
        /// Usually, the term "DG order of the calculation" means the velocity degree.
        /// </summary>
        protected int VelocityDegree() {
            int pVel;
            if (this.Control.FieldOptions.TryGetValue("Velocity*", out FieldOpts v)) {
                pVel = v.Degree;
            } else if (this.Control.FieldOptions.TryGetValue(BoSSS.Solution.NSECommon.VariableNames.VelocityX, out FieldOpts v1)) {
                pVel = v1.Degree;
            } else {
                throw new Exception("MultigridOperator.ChangeOfBasisConfig: Degree of Velocity not found");
            }
            return pVel;
        }

        protected override void AddMultigridConfigLevel(List<MultigridOperator.ChangeOfBasisConfig> configsLevel, int iLevel) {
            int D = this.GridData.SpatialDimension;
            int pVel = VelocityDegree();
            int pPrs = this.Control.FieldOptions[BoSSS.Solution.NSECommon.VariableNames.Pressure].Degree;
            int pTemp = this.Control.FieldOptions[BoSSS.Solution.NSECommon.VariableNames.Temperature].Degree;
            int pMassFraction = this.Control.FieldOptions[BoSSS.Solution.NSECommon.VariableNames.MassFraction0].Degree;

            // configurations for velocity
            for (int d = 0; d < D; d++) {
                var configVel_d = new MultigridOperator.ChangeOfBasisConfig() {
                    DegreeS = new int[] { pVel },
                    mode = MultigridOperator.Mode.SymPart_DiagBlockEquilib_DropIndefinite,
                    //mode = MultigridOperator.Mode.Eye,
                    VarIndex = new int[] { this.XOperator.DomainVar.IndexOf(VariableNames.VelocityVector(D)[d]) }
                };
                configsLevel.Add(configVel_d);
            }
            // configuration for pressure
            var configPres = new MultigridOperator.ChangeOfBasisConfig() {
                DegreeS = new int[] { pPrs },
                mode = MultigridOperator.Mode.IdMass_DropIndefinite,
                //mode = MultigridOperator.Mode.Eye,
                VarIndex = new int[] { this.XOperator.DomainVar.IndexOf(VariableNames.Pressure) }
            };
            configsLevel.Add(configPres);

            // configuration for Temperature
            var confTemp = new MultigridOperator.ChangeOfBasisConfig() {
                DegreeS = new int[] { pTemp },
                mode = MultigridOperator.Mode.SymPart_DiagBlockEquilib,
                //mode = MultigridOperator.Mode.Eye,
                VarIndex = new int[] { this.XOperator.DomainVar.IndexOf(VariableNames.Temperature) }
            };
            configsLevel.Add(confTemp);

            // configurations for Mass fractions
            int NumberOfSpecies = Control.NumberOfChemicalSpecies;
            var massFractionNames = VariableNames.MassFractions(NumberOfSpecies);
            for (int i = 0; i < (NumberOfSpecies); i++) {
                var configMF = new MultigridOperator.ChangeOfBasisConfig() {
                    DegreeS = new int[] { pMassFraction },
                    //mode = MultigridOperator.Mode.Eye,
                    mode = MultigridOperator.Mode.SymPart_DiagBlockEquilib,
                    VarIndex = new int[] { this.XOperator.DomainVar.IndexOf(massFractionNames[i]) }
                };
                configsLevel.Add(configMF);
            }
        }

        #endregion Operator configuration

        #region Operator definition

        protected IncompressibleBoundaryCondMap boundaryMap;
        private ThermalMultiphaseBoundaryCondMap m_thermBoundaryMap;

        protected override IncompressibleBoundaryCondMap GetBcMap() {
            if (boundaryMap == null)
                boundaryMap = new LowMachCombustionMultiphaseBoundaryCondMap(this.GridData, this.Control.BoundaryValues, new string[] { "A", "B" }, Control.NumberOfChemicalSpecies);

            //if(m_thermBoundaryMap == null) {
            //    List<string> SpeciesList = new List<string>() { "A", "B" };
            //    if(this.Control.UseImmersedBoundary)
            //        SpeciesList.Add("C");
            //    m_thermBoundaryMap = new ThermalMultiphaseBoundaryCondMap(this.GridData, this.Control.BoundaryValues, SpeciesList.ToArray());
            //}

            return boundaryMap;
        }

        protected virtual void DefineSystem(int D, OperatorFactory opFactory, LevelSetUpdater lsUpdater) {
            int quadOrder = QuadOrder();
            XNSEC_OperatorConfiguration config = new XNSEC_OperatorConfiguration(this.Control);

            GetBcMap();

            #region Equations of state

            var ChemicalModel = new OneStepChemicalModel(Control.VariableOneStepParameters, Control.YFuelInlet, Control.YOxInlet);

            if (boundaryMap.PhysMode == PhysicsMode.Combustion) {
                EoS_A = new MaterialLaw_MultipleSpecies(Control.MolarMasses, Control.MatParamsMode, Control.rhoOne, Control.R_gas, Control.T_ref_Sutherland, ChemicalModel, Control.cpRef, Control.HeatCapacityMode);
                EoS_B = new MaterialLaw_MultipleSpecies(Control.MolarMasses, Control.MatParamsMode, Control.rhoOne, Control.R_gas, Control.T_ref_Sutherland, ChemicalModel, Control.cpRef, Control.HeatCapacityMode);
            } else if (boundaryMap.PhysMode == PhysicsMode.MixtureFraction) {
                EoS_A = new MaterialLawMixtureFractionNew(Control.T_ref_Sutherland, Control.MolarMasses, Control.MatParamsMode, Control.rhoOne, Control.R_gas, Control.HeatRelease, Control.TOxInlet, Control.TFuelInlet, Control.YOxInlet, Control.YFuelInlet, Control.zSt, Control.CC, ChemicalModel, Control.cpRef, Control.smoothingFactor);
                EoS_B = new MaterialLawMixtureFractionNew(Control.T_ref_Sutherland, Control.MolarMasses, Control.MatParamsMode, Control.rhoOne, Control.R_gas, Control.HeatRelease, Control.TOxInlet, Control.TFuelInlet, Control.YOxInlet, Control.YFuelInlet, Control.zSt, Control.CC, ChemicalModel, Control.cpRef, Control.smoothingFactor);
            } else {
                throw new Exception("Wrong configuration");
            }

            //initialize EoS
            EoS_A.Initialize(Control.AmbientPressure);
            EoS_B.Initialize(Control.AmbientPressure);

            EoS_B.ConstantDensityValue = config.physParams.rho_B;
            EoS_A.ConstantDensityValue = config.physParams.rho_A;
            EoS_B.ConstantViscosityValue = config.physParams.mu_B;
            EoS_A.ConstantViscosityValue = config.physParams.mu_A;

            #endregion Equations of state

            // ============================
            // Momentum
            // ============================
            for (int d = 0; d < D; ++d) {
                DefineMomentumEquations(opFactory, config, d, D, lsUpdater);
                //Add Gravitation
                if (config.isGravity) {
                    var GravA = Gravity.CreateFrom("A", d, D, Control, Control.PhysicalParameters.rho_A, Control.GetGravity("A", d));
                    opFactory.AddParameter(GravA);
                    var GravB = Gravity.CreateFrom("B", d, D, Control, Control.PhysicalParameters.rho_B, Control.GetGravity("B", d));
                    opFactory.AddParameter(GravB);
                }
            }

            // ============================
            // Other Parameters
            // ============================
            DefineAditionalParameters(opFactory, config, D, lsUpdater, quadOrder);

            // ============================
            // Continuity
            // ============================
            if (config.isContinuity) {
                DefineContinuityEquation(opFactory, config, D, lsUpdater);
            }

            // ============================
            // Scalar Equations
            // ============================
            DefineScalarEquations(opFactory, config, D, lsUpdater);
        }

        virtual protected void DefineAditionalParameters(OperatorFactory opFactory, XNSEC_OperatorConfiguration config, int D, LevelSetUpdater lsUpdater, int quadOrder) {
            opFactory.AddParameter(new Density(EoS_A, EoS_B, config.NoOfChemicalSpecies));
            opFactory.AddParameter(new Viscosity(EoS_A, EoS_B));
            opFactory.AddParameter(new HeatCapacity(EoS_A));

            // === additional parameters === //
            opFactory.AddCoefficient(new SlipLengths(config, VelocityDegree()));
            Velocity0Mean v0Mean = new Velocity0Mean(D, LsTrk, quadOrder);
            if (((config.physParams.IncludeConvection && config.isTransport) | (config.thermParams.IncludeConvection)) & this.Control.NonLinearSolver.SolverCode == NonLinearSolverCode.Picard) {
                opFactory.AddParameter(new Velocity0(D));
                opFactory.AddParameter(v0Mean);
            }

            // === level set related parameters === //
            Normals normalsParameter = new Normals(D, ((LevelSet)lsUpdater.Tracker.LevelSets[0]).Basis.Degree);
            opFactory.AddParameter(normalsParameter);

            lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, v0Mean);
            lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, normalsParameter);
            switch (Control.AdvancedDiscretizationOptions.SST_isotropicMode) {
                case SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_ContactLine:
                    MaxSigma maxSigmaParameter = new MaxSigma(Control.PhysicalParameters, Control.AdvancedDiscretizationOptions, QuadOrder(), Control.dtFixed);
                    opFactory.AddParameter(maxSigmaParameter);
                    lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, maxSigmaParameter);
                    BeltramiGradient lsBGradient = FromControl.BeltramiGradient(Control, "Phi", D);
                    lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, lsBGradient);
                    break;

                case SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Flux:
                case SurfaceStressTensor_IsotropicMode.LaplaceBeltrami_Local:
                    BeltramiGradient lsGradient = FromControl.BeltramiGradient(Control, "Phi", D);
                    lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, lsGradient);
                    break;

                case SurfaceStressTensor_IsotropicMode.Curvature_ClosestPoint:
                case SurfaceStressTensor_IsotropicMode.Curvature_Projected:
                case SurfaceStressTensor_IsotropicMode.Curvature_LaplaceBeltramiMean:
                    BeltramiGradientAndCurvature lsGradientAndCurvature =
                        FromControl.BeltramiGradientAndCurvature(Control, "Phi", quadOrder, D);
                    opFactory.AddParameter(lsGradientAndCurvature);
                    lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, lsGradientAndCurvature);
                    break;

                case SurfaceStressTensor_IsotropicMode.Curvature_Fourier:
                    FourierLevelSet ls = (FourierLevelSet)lsUpdater.LevelSets[VariableNames.LevelSetCG].DGLevelSet;
                    var fourier = new FourierEvolver(
                        VariableNames.LevelSetCG,
                        ls,
                        Control.FourierLevSetControl,
                        Control.FieldOptions[BoSSS.Solution.NSECommon.VariableNames.Curvature].Degree);
                    lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, fourier);
                    //lsUpdater.AddEvolver(VariableNames.LevelSetCG, fourier);
                    opFactory.AddParameter(fourier);
                    break;

                default:
                    throw new NotImplementedException($"option {Control.AdvancedDiscretizationOptions.SST_isotropicMode} is not handled.");

            }

            if (config.isEvaporation) {
                //Console.WriteLine("Including mass transfer.");

                if (this.Control.NonLinearSolver.SolverCode != NonLinearSolverCode.Newton) throw new ApplicationException("Evaporation only implemented with use of Newton-solver!");

                var MassFluxExt = new MassFluxExtension_Evaporation(config);
                lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, MassFluxExt);
                if (this.Control.NonLinearSolver.SolverCode == NonLinearSolverCode.Picard) {
                    opFactory.AddParameter(MassFluxExt);
                }
            }

            opFactory.AddCoefficient(new ReynoldsNumber(config));

            opFactory.AddCoefficient(new BoSSS.Application.XNSFE_Solver.EvapMicroRegion());

            if (config.prescribedMassflux != null)
                opFactory.AddCoefficient(new PrescribedMassFlux(config));
        }

        virtual protected void DefineContinuityEquation(OperatorFactory opFactory, XNSEC_OperatorConfiguration config, int D, LevelSetUpdater lsUpdater) {
            opFactory.AddEquation(new LowMachContinuity(D, "A", config, boundaryMap, EoS_A, Control.dtFixed));
            opFactory.AddEquation(new LowMachContinuity(D, "B", config, boundaryMap, EoS_B, Control.dtFixed));
            opFactory.AddEquation(new InterfaceContinuityLowMach(config, D, LsTrk, config.isMatInt));

            // === evaporation extension === //
            if (config.isEvaporation) {
                opFactory.AddEquation(new InterfaceContinuity_Evaporation_Newton("A", "B", D, config));
            }

            var rho0 = new Density_t0(config.NoOfChemicalSpecies, (MaterialLaw_MultipleSpecies)EoS_A);
            opFactory.AddParameter(rho0);
            //lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, rho0);
        }

        virtual protected void DefineMomentumEquations(OperatorFactory opFactory, XNSEC_OperatorConfiguration config, int d, int D, LevelSetUpdater lsUpdater) {
            opFactory.AddEquation(new LowMachNavierStokes("A", d, D, boundaryMap, config, EoS_A));
            opFactory.AddEquation(new LowMachNavierStokes("B", d, D, boundaryMap, config, EoS_B));
            opFactory.AddEquation(new NSEInterface_LowMach("A", "B", d, D, boundaryMap, config, EoS_A, EoS_B, config.isMovingMesh));
            // opFactory.AddEquation(new NSESurfaceTensionForce("A", "B", d, D, boundaryMap, LsTrk, config)); // Maybe later...

            // === evaporation extension === //
            if (config.isEvaporation) {
                opFactory.AddEquation(new InterfaceNSE_Evaporation_Newton("A", "B", D, d, config));
            }
        }

        /// <summary>
        /// Scalar equations used in the lowMach equations, namely Temperature equation and Mass Fractions equations
        /// </summary>
        public virtual void DefineScalarEquations(OperatorFactory opFactory, XNSEC_OperatorConfiguration config, int D, LevelSetUpdater lsUpdater) {
            //================================
            // Energy equations (Temperature)
            //================================

            if (config.TemperatureEquationOK) {
                opFactory.AddEquation(new LowMachEnergy("A", D, boundaryMap, config, EoS_A, Control.HeatRelease, Control.ReactionRateConstants, Control.MolarMasses, Control.TRef, Control.cpRef, Control.dtFixed, Control.myThermalWallType));
                opFactory.AddEquation(new LowMachEnergy("B", D, boundaryMap, config, EoS_B, Control.HeatRelease, Control.ReactionRateConstants, Control.MolarMasses, Control.TRef, Control.cpRef, Control.dtFixed, Control.myThermalWallType));

                if (config.isEvaporation) {
                    //opFactory.AddEquation(new HeatInterface_Evaporation_Newton("A", "B", D, thermBoundaryMap, config));
                } else {
                    opFactory.AddEquation(new HeatInterface_LowMach("A", "B", D, boundaryMap, config));
                }
                opFactory.AddParameter(new dp0dt(EoS_A, Control.Reynolds, Control.Prandtl));

                opFactory.AddParameter(new ThermodynamicPressure(1.0, Control.ThermodynamicPressureMode, EoS_A));
            } else {
                opFactory.AddEquation(new IdentityEquation("A", VariableNames.Temperature, EquationNames.HeatEquation));
                opFactory.AddEquation(new IdentityEquation("B", VariableNames.Temperature, EquationNames.HeatEquation));
                opFactory.AddParameter(new ThermodynamicPressure(1.0, Control.ThermodynamicPressureMode, EoS_A));
            }
            //================================
            // Mass Fractions equations
            //================================

            for (int s = 0; s < config.NoOfChemicalSpecies; s++) {
                if (config.MassFractionEquationsOK) {
                    int chemicalSpeciesCounter = s;
                    opFactory.AddEquation(new LowMachMassFraction("A", D, boundaryMap, config, EoS_A, chemicalSpeciesCounter, Control.ReactionRateConstants, Control.StoichiometricCoefficients, Control.MolarMasses));
                    opFactory.AddEquation(new LowMachMassFraction("B", D, boundaryMap, config, EoS_B, chemicalSpeciesCounter, Control.ReactionRateConstants, Control.StoichiometricCoefficients, Control.MolarMasses));
                    if (Control.ChemicalReactionActive) {
                        opFactory.AddParameter(new ReactionRate(EoS_A));
                    }
                } else {// Add identity equation for each MF
                    opFactory.AddEquation(new IdentityEquation("A", VariableNames.MassFractions(config.NoOfChemicalSpecies)[s], EquationNames.SpeciesMassBalanceName(s)));
                    opFactory.AddEquation(new IdentityEquation("B", VariableNames.MassFractions(config.NoOfChemicalSpecies)[s], EquationNames.SpeciesMassBalanceName(s)));
                }
            }

            //var p0_old = new ThermodynamicPressure_Oldtimestep(1.0, Control.ThermodynamicPressureMode, EoS_A);
            //opFactory.AddParameter(p0_old);
            //lsUpdater.AddLevelSetParameter(VariableNames.LevelSetCG, p0_old);
        }

        /// <summary>
        /// Low-Mach unsteady part definition
        /// </summary>
        /// <param name="D"></param>
        /// <param name="opFactory"></param>
        /// <param name="lsUpdater"></param>
        protected void DefineTemporalTerm(int D, OperatorFactory opFactory) {
            //  var EoS = base.Control.EoS;
            int NoOfChemSpecies = Control.NumberOfChemicalSpecies;

            // Momentum
            // ============================
            for (int d = 0; d < D; d++) {
                opFactory.AddEquation(new LowMachUnsteadyEquationPart("A", D, VariableNames.VelocityVector(D)[d], EquationNames.MomentumEquationComponent(d), NoOfChemSpecies, EoS_A));
                opFactory.AddEquation(new LowMachUnsteadyEquationPart("B", D, VariableNames.VelocityVector(D)[d], EquationNames.MomentumEquationComponent(d), NoOfChemSpecies, EoS_B));
            }

            // Continuity
            // ============================
            opFactory.AddEquation(new LowMachUnsteadyEquationPart("A", D, VariableNames.Pressure, EquationNames.ContinuityEquation, NoOfChemSpecies, EoS_A, massScale: 0.0));
            opFactory.AddEquation(new LowMachUnsteadyEquationPart("B", D, VariableNames.Pressure, EquationNames.ContinuityEquation, NoOfChemSpecies, EoS_B, massScale: 0.0));

            // Energy (Temperature)
            // ============================
            opFactory.AddEquation(new LowMachUnsteadyEquationPart("A", D, VariableNames.Temperature, EquationNames.HeatEquation, NoOfChemSpecies, EoS_A, massScale: 1.0, heatCapacityRatio: Control.HeatCapacityRatio));
            opFactory.AddEquation(new LowMachUnsteadyEquationPart("B", D, VariableNames.Temperature, EquationNames.HeatEquation, NoOfChemSpecies, EoS_B, massScale: 1.0, heatCapacityRatio: Control.HeatCapacityRatio));

            // Mass Fractions
            // ============================
            for (int s = 0; s < NoOfChemSpecies; s++) {
                opFactory.AddEquation(new LowMachUnsteadyEquationPart("A", D, VariableNames.MassFractions(NoOfChemSpecies)[s], EquationNames.SpeciesMassBalanceName(s), NoOfChemSpecies, EoS_A));
                opFactory.AddEquation(new LowMachUnsteadyEquationPart("B", D, VariableNames.MassFractions(NoOfChemSpecies)[s], EquationNames.SpeciesMassBalanceName(s), NoOfChemSpecies, EoS_B));
            }
        }

        #endregion Operator definition

        public MaterialLaw_MultipleSpecies EoS_A;
        public MaterialLaw_MultipleSpecies EoS_B;

        private XSpatialOperatorMk2 XOP;
        /// <summary>
        /// Low-Mach system of equations definition
        /// </summary>
        /// <param name="D"></param>
        /// <param name="opFactory"></param>
        /// <param name="lsUpdater"></param>

        protected override XSpatialOperatorMk2 GetOperatorInstance(int D, LevelSetUpdater levelSetUpdater) {
            OperatorFactory opFactory = new OperatorFactory();

            DefineSystem(D, opFactory, levelSetUpdater);

            /*XSpatialOperatorMk2*/
            XOP = opFactory.GetSpatialOperator(QuadOrder());
            //final settings
            XOP.FreeMeanValue[VariableNames.Pressure] = !GetBcMap().DirichletPressureBoundary;

            if (Control.NonLinearSolver.SolverCode == NonLinearSolverCode.Newton) {
                Console.WriteLine("Linearization Hint:" + LinearizationHint.GetJacobiOperator.ToString());
                XOP.LinearizationHint = LinearizationHint.GetJacobiOperator;
            } else {
                throw new NotImplementedException("LowMach solver supports only Newton as NonLinearSolver");
            }

            XOP.ParameterUpdates.Add(PlotNewtonIterationsHack);

            XOP.IsLinear = false;
            XOP.AgglomerationThreshold = this.Control.AgglomerationThreshold;

            // ============================
            // Self made temporal operator
            // ============================
            if (Control.UseSelfMadeTemporalOperator && (base.Control.TimesteppingMode == AppControl._TimesteppingMode.Transient)) {
                Console.WriteLine("Using low Mach temporal operator");
                OperatorFactory temporalOperatorFactory = new OperatorFactory();
                DefineTemporalTerm(D, temporalOperatorFactory);
                XSpatialOperatorMk2 temporalXOP = temporalOperatorFactory.GetSpatialOperator(QuadOrder());
                temporalXOP.Commit();

                var DependentTemporalOp = new DependentXTemporalOperator(XOP);

                foreach (var c in temporalXOP.CodomainVar) {
                    foreach (var d in temporalXOP.EquationComponents[c]) {
                        DependentTemporalOp.EquationComponents[c].Add(d);
                    }
                }

                XOP.TemporalOperator = DependentTemporalOp;
            }

            ////============================
            //// Solver-Controlled Homotopy<
            ////============================

            if (this.Control.HomotopyApproach == XNSEC_Control.HomotopyType.Automatic) {
                if (Control.HomotopyVariable == XNSEC_Control.HomotopyVariableEnum.Reynolds) {
                    this.CurrentHomotopyValue = Control.Reynolds;
                    XOP.HomotopyUpdate.Add(delegate (double HomotopyScalar) {
                        if (HomotopyScalar < 0.0)
                            throw new ArgumentOutOfRangeException();
                        if (HomotopyScalar > 1.0)
                            throw new ArgumentOutOfRangeException();

                        // Using a linear function to prescribe the homotopy  path
                        // If HomotopyScalar = 0 => Reynolds = StartingValue
                        // If HomotopyScalar = 1 => Reynolds = Control.Reynolds
                        double StartingValue = Control.StartingHomotopyValue; // this should be an "easy" value for finding a solution

                        //double StartingValue = Control.Reynolds/10; // this should be an "easy" value for finding a solution
                        double AimedValue = Control.Reynolds;

                        //Linear
                        double slope = (AimedValue - StartingValue) / (1 - 0);
                        double val = slope * (HomotopyScalar - 0) + StartingValue;
                        this.CurrentHomotopyValue = val;

                        ////Exponential
                        //double slope = (Math.Log10(AimedValue) - Math.Log10(StartingValue)) / (1 - 0);
                        //double reExponent = slope * (HomotopyScalar - 0) + Math.Log10(StartingValue);
                        //this.CurrentHomotopyValue = Math.Pow(10, reExponent);

                        Console.WriteLine("HomotopyScalar:" + HomotopyScalar);
                        Console.WriteLine("HomotopyValue:" + CurrentHomotopyValue);
                    });
                    var defaultcoefficients = XOP.OperatorCoefficientsProvider;
                    XOP.OperatorCoefficientsProvider = delegate (LevelSetTracker lstrk, SpeciesId spc, int quadOrder, int TrackerHistoryIdx, double time) {
                        CoefficientSet cs = defaultcoefficients(lstrk, spc, quadOrder, TrackerHistoryIdx, time);
                        cs.UserDefinedValues["Reynolds"] = this.CurrentHomotopyValue;
                        return cs;
                    };
                }

                if (Control.HomotopyVariable == XNSEC_Control.HomotopyVariableEnum.VelocityInletMultiplier) {
                    this.CurrentHomotopyValue = Control.homotopieAimedValue;
                    XOP.HomotopyUpdate.Add(delegate (double HomotopyScalar) {
                        if (HomotopyScalar < 0.0)
                            throw new ArgumentOutOfRangeException();
                        if (HomotopyScalar > 1.0)
                            throw new ArgumentOutOfRangeException();

                        ////Linear
                        //double StartingValue = 1.0; // this should be an "easy" value for finding a solution
                        //double AimedValue = Control.homotopieAimedValue;
                        //double slope = (AimedValue - StartingValue) / (1 - 0);
                        //double val = slope * (HomotopyScalar - 0) + StartingValue;

                        double StartingValue = 1.0 / Control.homotopieAimedValue; // this should be an "easy" value for finding a solution
                        double AimedValue = 1.0;
                        double slope = (AimedValue - StartingValue) / (1 - 0);
                        double val = slope * (HomotopyScalar - 0) + StartingValue;

                        this.CurrentHomotopyValue = val;

                        Console.WriteLine("HomotopyScalar:" + HomotopyScalar);
                        Console.WriteLine("HomotopyValue:" + CurrentHomotopyValue);
                    });
                    var defaultcoefficients = XOP.OperatorCoefficientsProvider;
                    XOP.OperatorCoefficientsProvider = delegate (LevelSetTracker lstrk, SpeciesId spc, int quadOrder, int TrackerHistoryIdx, double time) {
                        CoefficientSet cs = defaultcoefficients(lstrk, spc, quadOrder, TrackerHistoryIdx, time);
                        cs.UserDefinedValues["VelocityMultiplier"] = this.CurrentHomotopyValue;
                        return cs;
                    };
                }

                if (Control.HomotopyVariable == XNSEC_Control.HomotopyVariableEnum.HeatOfCombustion) {
                    this.CurrentHomotopyValue = Control.homotopieAimedValue;
                    XOP.HomotopyUpdate.Add(delegate (double HomotopyScalar) {
                        if (HomotopyScalar < 0.0)
                            throw new ArgumentOutOfRangeException();
                        if (HomotopyScalar > 1.0)
                            throw new ArgumentOutOfRangeException();

                        ////Linear

                        double StartingValue = 0.0; // this should be an "easy" value for finding a solution
                        double AimedValue = Control.HeatRelease;
                        double slope = (AimedValue - StartingValue) / (1 - 0);
                        double val = slope * (HomotopyScalar - 0) + StartingValue;

                        this.CurrentHomotopyValue = val;
                        ((MaterialLawMixtureFractionNew)EoS_A).Q = val;
                        Console.WriteLine("HomotopyScalar:" + HomotopyScalar);
                        Console.WriteLine("HomotopyValue:" + CurrentHomotopyValue);
                    });
                    var defaultcoefficients = XOP.OperatorCoefficientsProvider;
                    XOP.OperatorCoefficientsProvider = delegate (LevelSetTracker lstrk, SpeciesId spc, int quadOrder, int TrackerHistoryIdx, double time) {
                        CoefficientSet cs = defaultcoefficients(lstrk, spc, quadOrder, TrackerHistoryIdx, time);
                        cs.UserDefinedValues["HeatOfReaction"] = this.CurrentHomotopyValue;
                        return cs;
                    };
                }
            }
            if (Control.VariableBounds != null) {
                Console.WriteLine("Using solver safe guard!");
                XOP.SolverSafeguard = DelValidationCombustion;
            }
            XOP.Commit();

            PrintConfiguration();
            return XOP;
        }

        /// <summary>
        /// duh
        /// </summary>
        public double CurrentHomotopyValue {
            get {
                return (double)XOP.UserDefinedValues["A"][Control.homotopieVariableName];
            }
            set {
                double oldVal;
                if (XOP.UserDefinedValues["A"].ContainsKey(Control.homotopieVariableName))
                    oldVal = CurrentHomotopyValue;
                else
                    oldVal = double.NegativeInfinity;

                if (oldVal != value)
                    Console.WriteLine("setting" + Control.homotopieVariableName + " to " + value);
                XOP.UserDefinedValues["A"][Control.homotopieVariableName] = value;
            }
        }

        protected override double RunSolverOneStep(int TimestepNo, double phystime, double dt) {
            //Update Calls
            dt = GetTimestep();

            if (Control.timeDerivativeEnergyp0_OK) {
                //    var p0_old = this.Parameters.Where(f => f.Identification == VariableNames.ThermodynamicPressure + "_t0").Single();
                //    var p0 = this.Parameters.Where(f => f.Identification == VariableNames.ThermodynamicPressure).Single();
                //    p0_old.Clear();
                //    p0_old.Acc(1.0, p0);

                var rho0_old = this.Parameters.Where(f => f.Identification == VariableNames.Rho + "_t0").Single();
                var rho = this.Parameters.Where(f => f.Identification == VariableNames.Rho).Single();
                rho0_old.Clear();
                rho0_old.Acc(1.0, rho);
            }

            var overallstart = DateTime.Now;
            Console.WriteLine($"Starting time step {TimestepNo}, dt = {dt}");
            bool SolverSuccess = Timestepping.Solve(phystime, dt, Control.SkipSolveAndEvaluateResidual);
            var overallstop = DateTime.Now;
            var overallduration = overallstop - overallstart;
            Console.WriteLine("Duration of this timestep: " + overallduration);
            Console.WriteLine($"done with time step {TimestepNo}");

            if (Control.AnalyticsolutionSwitch || !Control.ExactSolutionVelocity.IsNullOrEmpty()) {
                CalcErrors();
            }

            //Calculate nusselt number
            if ((Control.EdgeTagsNusselt != null)) {
                Console.WriteLine("Calculating nusselt numbers!");
                var temperatureXdg = (XDGField)(CurrentStateVector.Fields.Where(f => f.Identification == VariableNames.Temperature).SingleOrDefault());
                var temp = temperatureXdg.ProjectToSinglePhaseField(4);
                var NusseltResults = CalculateNusselt(TimestepNo, base.GridData, temp, Control);
                this.CurrentSessionInfo.KeysAndQueries.Add("NusseltNumber0", NusseltResults[0]);
                this.CurrentSessionInfo.KeysAndQueries.Add("NusseltNumber1", NusseltResults[1]);
                this.CurrentSessionInfo.KeysAndQueries.Add("NusseltNumber2", NusseltResults[2]);

                Console.WriteLine("Nusselt0:" + NusseltResults[0]);
                Console.WriteLine("Nusselt1:" + NusseltResults[1]);
                Console.WriteLine("Nusselt2:" + NusseltResults[2]);
            }

            //for(int i = 0; i < this.CurrentStateVector.Fields.Count(); i++) {
            //    Console.WriteLine("Field " + this.CurrentStateVector.Fields[i].Identification + "has " + this.CurrentStateVector.Fields[0].Coordinates.NoOfCols + " cols and " + this.CurrentStateVector.Fields[0].Coordinates.NoOfRows + "rows"
            //        );
            //}

            if (Control.TimesteppingMode == AppControl._TimesteppingMode.Steady /*&& Control.NoOfTimesteps == TimestepNo*/) {
                if (!SolverSuccess) {
                    //Console.WriteLine("SOLVER_ERROR!");
                    base.CurrentSessionInfo.AddTag("NOTCONVERGED");
                    //throw new Exception("Solver couldnt find a solution");
                }
            }

            //sensor.Update(CurrentState.Fields.Where(f => f.Identification == VariableNames.Temperature).Single());

            return dt;
        }

        protected override void Bye() {
            // base.PostprocessingModules.Add();
            base.Bye();
        }

        private int hack_TimestepIndex = 0;
        //private PerssonSensor sensor;

        public override void PostRestart(double time, TimestepNumber timestep) {
            base.PostRestart(time, timestep);

            if (Control.UseMixtureFractionsForCombustionInitialization) {
                var ChemicalModel = new OneStepChemicalModel(Control.VariableOneStepParameters, Control.YFuelInlet, Control.YOxInlet);
                var m_EoS = new MaterialLawMixtureFractionNew(Control.T_ref_Sutherland, Control.MolarMasses, Control.MatParamsMode, Control.rhoOne, Control.R_gas, Control.HeatRelease, Control.TOxInlet, Control.TFuelInlet, Control.YOxInlet, Control.YFuelInlet, Control.zSt, Control.CC, ChemicalModel, Control.cpRef, Control.smoothingFactor);

                string[] names = ArrayTools.Cat(new string[] { VariableNames.Temperature }, VariableNames.MassFractions(Control.NumberOfChemicalSpecies));

                // Start the combustion calculation with the mixture fraction
                var MixtureFraction = this.m_IOFields.Where(f => f.Identification == VariableNames.MixtureFraction).Single();

                //var basis = this.m_RegisteredFields.Where(f => f.Identification == VariableNames.Temperature).First().Basis;
                //var MixtureFraction = new XDGField((XDGBasis)basis, VariableNames.MixtureFraction);
                base.RegisterField(MixtureFraction, IOListOption.Always);

                foreach (var id in names) {
                    var fieldToTransform = FindField(this.m_RegisteredFields.ToArray(), id);
                    var fieldToTransform_A = ((XDGField)fieldToTransform).GetSpeciesShadowField("A");
                    fieldToTransform_A.Clear();
                    fieldToTransform_A.ProjectField(1.0,
                    delegate (int j0, int Len, NodeSet NS, MultidimensionalArray result) {
                        int K = result.GetLength(1);
                        MultidimensionalArray ZArr = MultidimensionalArray.Create(Len, K);
                        MixtureFraction.Evaluate(j0, Len, NS, ZArr);
                        for (int j = 0; j < Len; j++) {
                            for (int k = 0; k < K; k++) {
                                result[j, k] = m_EoS.getVariableFromZ(ZArr[j, k], id);
                            }
                        }
                    }, new BoSSS.Foundation.Quadrature.CellQuadratureScheme(true, null));
                }
            }
        }

        protected override void CreateFields() {
            base.CreateFields();

            if (Control.UseMixtureFractionsForCombustionInitialization) {
                base.RegisterField(new XDGField((XDGBasis)(this.m_RegisteredFields.Where(f => f.Identification == VariableNames.Temperature).First().Basis), VariableNames.MixtureFraction), IOListOption.Always);
            }

            // Create fields for analytical solution and errors
            if (Control.AnalyticsolutionSwitch || !Control.ExactSolutionVelocity.IsNullOrEmpty()) {
                // Errors:
                var errFields = InstantiateErrorFields();
                foreach (var f in errFields) {
                    base.RegisterField(f, IOListOption.Always);
                }
                // Analytical Solutions
                var AnSolFields = InstantiateAnalyticalSolFields();
                foreach (var f in AnSolFields) {
                    base.RegisterField(f, IOListOption.Always);
                }
            }
        }

        /// <summary>
        /// Operator stability analysis
        /// </summary>
        override public IDictionary<string, double> OperatorAnalysis() {
            return this.Operator.OperatorAnalysis(this.CurrentStateVector.Mapping, this.MultigridOperatorConfig);
        }

        /// <summary>
        /// User-defined validation of a solver step, e.g. to prevent the solver to iterate out-of-bounds,
        /// e.g. to avoid un-physical 'solutions' (e.g. negative density).
        /// ('safeguard' for the solver)
        /// </summary>
        /// <param name="varIn"></param>
        /// <param name="varOut"></param>
        private void DelValidationCombustion(DGField[] varIn, DGField[] varOut) {
            CellMask AllCells = CellMask.GetFullMask(GridData);
            var Bounds = Control.VariableBounds;
            varOut.Clear();
            varOut = varIn.CloneAs();
            int idx = 0;
            foreach (var f in varIn) { // Loop over each DGField of the solution array
                foreach (var varName in Bounds.Keys) { // Iterate over dictionary with fields to be repaired
                    if (f.Identification == varName) {
                        double MinBound = Bounds[varName].Item1;
                        double MaxBound = Bounds[varName].Item2;

                        double[] mins = new double[AllCells.NoOfItemsLocally];
                        double[] maxs = new double[AllCells.NoOfItemsLocally];
                        f.GetCellwiseExtremalValues(mins, maxs);

                        // now check if values are inside bounds. If not, repair them
                        int lowcount = 0; int topcount = 0;
                        foreach (var cell in AllCells.ItemEnum) {
                            bool BoundedLow = (mins[cell] > MinBound);
                            bool BoundedTop = (maxs[cell] < MaxBound);

                            if (!BoundedLow) { // repair
                                ((XDGField)(varOut[idx])).SetMeanValueAB(cell, MinBound);
                                //varOut[idx].SetMeanValue(cell, MinBound);
                                lowcount++;
                            }
                            if (!BoundedTop) { // repair
                                ((XDGField)(varOut[idx])).SetMeanValueAB(cell, MaxBound);
                                //varOut[idx].SetMeanValue(cell, MaxBound);
                                topcount++;
                            }
                        }
                        if (lowcount > 0 || topcount > 0) {
                            Console.WriteLine(f.Identification + " is out of bounds in {0} cells.", lowcount + topcount);
                        }
                    }
                }
                idx++;
            }

            return;
        }
    }
}