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

using ilPSP;
using ilPSP.Connectors.Matlab;
using ilPSP.LinSolvers;
using ilPSP.Tracing;
using ilPSP.Utils;

using BoSSS.Platform;

using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.Grid.RefElements;
using BoSSS.Foundation.IO;
using BoSSS.Foundation.Quadrature;
using BoSSS.Foundation.XDG;
using BoSSS.Foundation.Grid.Aggregation;

using BoSSS.Solution;
using BoSSS.Solution.AdvancedSolvers;
using BoSSS.Solution.NSECommon;
using BoSSS.Solution.Tecplot;
using BoSSS.Solution.Utils;
using BoSSS.Solution.XdgTimestepping;
using BoSSS.Solution.RheologyCommon;
using BoSSS.Solution.Gnuplot;

using MPI.Wrappers;
using NUnit.Framework;
using BoSSS.Foundation.SpecFEM;
using System.IO;

namespace BoSSS.Application.Rheology {

    /// <summary>
    /// Solver for calculation with viscoelastic extra stresses using the Oldroyd B model or the upper convected Maxwell model (UCM).
    /// </summary>
    public class Rheology : BoSSS.Solution.Application<RheologyControl> {
        static void Main(string[] args) {
            Rheology._Main(args, false, () => new Rheology());
        }

        #region instantiation

        // Attributes for fields (Names), initialization of DG fields
        //==============================================================

        /// <summary>
        /// Velocity domain
        /// </summary>
        [InstantiateFromControlFile(new string[] { VariableNames.VelocityX, VariableNames.VelocityY, VariableNames.VelocityZ },
            null,
            true, true,
            IOListOption.ControlFileDetermined)]
        public VectorFieldHistory<SinglePhaseField> Velocity;

        /// <summary>
        /// Velocities codomain: Residuum in momentum equation
        /// </summary>
        [InstantiateFromControlFile(new string[] { "ResidualMomentumX", "ResidualMomentumY", "ResidualMomentumZ" },
            new string[] { VariableNames.VelocityX, VariableNames.VelocityY, VariableNames.VelocityZ },
            true, true,
            IOListOption.ControlFileDetermined)]
        public VectorField<SinglePhaseField> ResidualMomentum;

        /// <summary>
        /// Pressure domain
        /// </summary>
        [InstantiateFromControlFile(VariableNames.Pressure, null, IOListOption.ControlFileDetermined)]
        public SinglePhaseField Pressure;

        /// <summary>
        /// Pressure codomain: Residuum in continuity equation
        /// </summary>
        [InstantiateFromControlFile("ResidualConti", VariableNames.Pressure, IOListOption.ControlFileDetermined)]
        public SinglePhaseField ResidualConti;

        ///// <summary>
        ///// Extra stress domain (2D): StressXX
        ///// </summary>
        //[InstantiateFromControlFile("StressXX", null, IOListOption.ControlFileDetermined)]
        //public SinglePhaseField StressXX;

        /// <summary>
        /// Extra stress domain (2D): StressXX
        /// </summary>
        [InstantiateFromControlFile(VariableNames.StressXX, null, IOListOption.ControlFileDetermined)]
        public SinglePhaseField StressXX;

        /// <summary>
        /// Extra stress domain (2D): StressXY
        /// </summary>
        [InstantiateFromControlFile(VariableNames.StressXY, null, IOListOption.ControlFileDetermined)]
        public SinglePhaseField StressXY;

        /// <summary>
        /// Extra stress domain (2D): StressYY
        /// </summary>
        [InstantiateFromControlFile(VariableNames.StressYY, null, IOListOption.ControlFileDetermined)]
        public SinglePhaseField StressYY;

        /// <summary>
        /// Extra stress codomain (2D): StressXX
        /// </summary>
        [InstantiateFromControlFile("ResidualStressXX", VariableNames.StressXX, IOListOption.ControlFileDetermined)]
        public SinglePhaseField ResidualStressXX;

        /// <summary>
        /// Extra stresses codomain (2D): StressXY
        /// </summary>
        [InstantiateFromControlFile("ResidualStressXY", VariableNames.StressXY, IOListOption.ControlFileDetermined)]
        public SinglePhaseField ResidualStressXY;

        /// <summary>
        /// Extra stresses codomain (2D): StressYY
        /// </summary>
        [InstantiateFromControlFile("ResidualStressYY", VariableNames.StressYY, IOListOption.ControlFileDetermined)]
        public SinglePhaseField ResidualStressYY;

        /// <summary>
        /// Extra stresses parameter (2D): StressXX
        /// </summary>
        [InstantiateFromControlFile("StressXXP", VariableNames.StressXX, IOListOption.ControlFileDetermined)]
        public SinglePhaseField StressXXP;

        /// <summary>
        /// Extra stresses parameter (2D): StressXY
        /// </summary>
        [InstantiateFromControlFile("StressXYP", VariableNames.StressXY, IOListOption.ControlFileDetermined)]
        public SinglePhaseField StressXYP;

        /// <summary>
        /// Extra stresses parameter (2D): StressXY
        /// </summary>
        [InstantiateFromControlFile("StressYYP", VariableNames.StressYY, IOListOption.ControlFileDetermined)]
        public SinglePhaseField StressYYP;

        /// <summary>
        /// Extra source (e.g. gravity)
        /// </summary>
        [InstantiateFromControlFile(new string[] { VariableNames.GravityX, VariableNames.GravityY, VariableNames.GravityZ },
                    new string[] { VariableNames.VelocityX, VariableNames.VelocityY, VariableNames.VelocityZ },
                    true, true,
                    IOListOption.ControlFileDetermined)]
        public VectorField<SinglePhaseField> Gravity;

        // Gravity source constitutive
        [InstantiateFromControlFile("GravityXX", "StressXX", IOListOption.ControlFileDetermined)]
        public SinglePhaseField GravityXX;

        [InstantiateFromControlFile("GravityXY", "StressXY", IOListOption.ControlFileDetermined)]
        public SinglePhaseField GravityXY;

        [InstantiateFromControlFile("GravityYY", "StressYY", IOListOption.ControlFileDetermined)]
        public SinglePhaseField GravityYY;

        //Gravity source for divergence of u
        [InstantiateFromControlFile("GravityDiv", VariableNames.Pressure, IOListOption.ControlFileDetermined)]
        public SinglePhaseField GravityDiv;

        // Parameters: Velocity Gradient
        VectorField<SinglePhaseField> VelocityXGradient;
        VectorField<SinglePhaseField> VelocityYGradient;


        //Parameters: external analytical velocity
        SinglePhaseField U;
        SinglePhaseField V;

        // LEVEL-SET - not needed for non-Level-set calculations
        //_______________________________________________________________________________________________

        /// <summary>
        /// Levelset tracker
        /// </summary>
        [LevelSetTracker("-:A +:B", 1)]
        public LevelSetTracker LevSetTrk;

        /// <summary>
        /// The  continuous level set field which defines the XDG space; 
        /// it is obtained from the projection of the discontinuous DG Level set onto the 
        /// continuous element space.
        /// </summary>
        [InstantiateFromControlFile("Phi", "Phi", IOListOption.ControlFileDetermined)]
        public LevelSet LevSet;

        /// <summary>
        /// Species which represents the flow domain.
        /// </summary>
        protected SpeciesId[] FluidSpecies {
            get {
                return new SpeciesId[] { LsTrk.GetSpeciesId("A") }; // wir rechnen nur species A
            }
        }

        /// <summary>
        /// Actual type of cut cell quadrature to use; If no XDG is used, resp. no cut cells are present,
        /// this setting has no effect.
        /// </summary>
        protected XQuadFactoryHelper.MomentFittingVariants momentFittingVariant = XQuadFactoryHelper.MomentFittingVariants.OneStepGauss;
        //_______________________________________________________________________________________________


        // Some initialisation of variables
        //============================================
        IncompressibleBoundaryCondMap BcMap;

        /// <summary>
        /// Current Weissenberg number,
        /// if the solver is used in Weissenberg-increment mode
        /// (<see cref="RheologyControl.RaiseWeissenberg"/>, <see cref="RheologyControl.WeissenbergIncrement"/>)
        /// </summary>
        public double currentWeissenberg;

        /// <summary>
        /// restart value for the Weissenberg-increment mode
        /// </summary>
        private double restartWeissenberg = 0.0;


        bool ChangeMesh = true;

        /// <summary>
        /// Spatial operator 
        /// </summary>
        SpatialOperator XOP;

        /// <summary>
        /// Linearization of <see cref="XOP"/>
        /// </summary>
        SpatialOperator JacobiOp;

        /// <summary>
        /// Timestepping object
        /// </summary>
        protected XdgBDFTimestepping m_BDF_Timestepper;

        // Persson sensor and artificial viscosity
        //=============================================
        /// <summary>
        /// Instance of Persson sensor
        /// </summary>
        protected PerssonSensor perssonsensor;

        /// <summary>
        /// Instance of artificial viscosity
        /// </summary>
        protected SinglePhaseField artificalViscosity;

        /// <summary>
        /// Instance of max value of artificial viscosity
        /// </summary>
        protected double artificialMaxViscosity;


        // Settings for calculation
        //===============================================
        /// <summary>
        /// Set true if Navier Stokes is solved, then the mean velocities as parameters for calculation of convective terms are needed
        /// </summary>
        protected bool U0MeanRequired {
            get {
                return (!this.Control.Stokes);
            }
        }

        /// <summary>
        /// Block scaling of the mass matrix: for each species $\frakS$, a vector $(\rho_\frakS, \ldots, \rho_frakS, 0 )$.
        /// </summary>
        protected IDictionary<SpeciesId, IEnumerable<double>> MassScale {
            get {
                double rho = 1; // this.Control.PhysicalParameters.rho_A;

                int D = this.GridData.SpatialDimension;

                double[] _rho = new double[D + 4];
                _rho.SetAll(rho);
                //No MassMatrix for the pressure
                _rho[D] = 0;

                _rho[D + 1] = 1;
                _rho[D + 2] = 1;
                _rho[D + 3] = 1;
                Dictionary<SpeciesId, IEnumerable<double>> R = new Dictionary<SpeciesId, IEnumerable<double>>();
                R.Add(this.LsTrk.GetSpeciesId("A"), _rho);

                return R;
            }
        }

        CoordinateVector m_CurrentSolution = null;

        /// <summary>
        /// Current solution vector
        /// </summary>
        public CoordinateVector CurrentSolution {
            get {
                if (m_CurrentSolution == null) {
                    m_CurrentSolution = new CoordinateVector(ArrayTools.Cat(this.Velocity.Current, this.Pressure, this.StressXX, this.StressXY, this.StressYY));
                }
                return m_CurrentSolution;
            }
        }

        CoordinateVector m_CurrentResidual = null;

        /// <summary>
        /// Current residual vector
        /// </summary>
        public CoordinateVector CurrentResidual {
            get {
                if (m_CurrentResidual == null) {
                    m_CurrentResidual = new CoordinateVector(ArrayTools.Cat(this.ResidualMomentum, this.ResidualConti, this.ResidualStressXX, this.ResidualStressXY, this.ResidualStressYY));
                }
                return m_CurrentResidual;
            }
        }

        /// <summary>
        /// DG Field instantiation.
        /// </summary>
        protected override void CreateFields() {
            base.CreateFields();
            base.LsTrk = this.LevSetTrk;
            if (Control.CutCellQuadratureType != base.LsTrk.CutCellQuadratureType)
                throw new ApplicationException();
            if (Control.UsePerssonSensor == true) {
                perssonsensor = new PerssonSensor(StressXX);
                this.IOFields.Add(perssonsensor.GetField());
            }
            if (Control.UseArtificialDiffusion == true) {
                artificalViscosity = new SinglePhaseField(new Basis(GridData, 1), "artificalViscosity");
                this.IOFields.Add(artificalViscosity);

            }
        }


        /// <summary>
        /// Step 1 of 2 for dynamic load balancing: creating a backup of this objects 
        /// status in the load-balancing thing <paramref name="L"/>
        /// </summary>
        public override void DataBackupBeforeBalancing(GridUpdateDataVaultBase L) {
            m_BDF_Timestepper.DataBackupBeforeBalancing(L);
        }

        #endregion

        /// <summary>
        /// Initialize Calculation, Create Equations
        /// </summary>
        protected override void CreateEquationsAndSolvers(GridUpdateDataVaultBase L) {
            int D = this.GridData.SpatialDimension;

            if (XOP != null && L == null && Control.Weissenberg == 0.0)
                return;

            if (m_BDF_Timestepper != null) {
                if (L != null) {

                    m_BDF_Timestepper.DataRestoreAfterBalancing(L,
                        ArrayTools.Cat<DGField>(Velocity.Current, Pressure, StressXX, StressXY, StressYY),
                        ArrayTools.Cat<DGField>(ResidualMomentum, ResidualConti, ResidualStressXX, ResidualStressXY, ResidualStressYY),
                        this.LsTrk, this.MultigridSequence);

                    m_CurrentSolution = null;
                    m_CurrentResidual = null;

                }
            } else {

                using (new FuncTrace()) {

                    D = this.GridData.SpatialDimension;
                    BcMap = new IncompressibleBoundaryCondMap(this.GridData, this.Control.BoundaryValues, PhysicsMode.Viscoelastic);

                    string[] CodName = new string[] { "momX", "momY", "div", "constitutiveXX", "constitutiveXY", "constitutiveYY" };

                    string[] Params = new string[] { };

                    if (this.Control.useFDJacobianForOperatorMatrix == true) {
                        Params = ArrayTools.Cat(VariableNames.Velocity0Vector(D), VariableNames.Velocity0MeanVector(D), VariableNames.VelocityX_GradientVector(), VariableNames.VelocityY_GradientVector(), VariableNames.StressXXP, VariableNames.StressXYP, VariableNames.StressYYP, "artificialViscosity");
                    } else {
                        Params = this.Control.UseArtificialDiffusion ? new string[] { "artificialViscosity" } : null;
                    }

                    string[] DomName = ArrayTools.Cat(VariableNames.VelocityVector(D), VariableNames.Pressure, VariableNames.StressXX, VariableNames.StressXY, VariableNames.StressYY);

                    XOP = new SpatialOperator(DomName, Params, CodName, QuadOrderFunc.NonLinearWithoutParameters(2));

                    // Development switches to turn specific components on or off, 
                    // for the sake of iterative solver testing:
                    bool MomContinuitycoupling = true;
                    bool ConstitutiveEqs = true;



                    // Momentum equation
                    //================================================================================
                    for (int d = 0; d < D; d++) {
                        var comps = XOP.EquationComponents[CodName[d]];

                        // convective part:
                        if (!this.Control.StokesConvection || !this.Control.Stokes) {
                            comps.Add(new LocalLaxFriedrichsConvection(D, BcMap, d, 1.0));
                            
                        } else {
                            Console.WriteLine("Using Stokes Equation - no convective term.");
                        }


                        // pressure part:
                        var pres = new PressureGradientLin_d(d, BcMap);
                        comps.Add(pres);
                        //Console.WriteLine("!!!Warning!!!: no pressure grad.");

                        //if periodic boundary conditions are applied a fixed pressure gradient drives the flow
                        if (this.Control.FixedStreamwisePeriodicBC) {
                            var pressSource = new SrcPressureGradientLin_d(this.Control.SrcPressureGrad[d]);
                            comps.Add(pressSource);
                        }


                        // viscous part:
                        //Type GridType = GridData.iGeomCells.RefElements[0].GetType();

                        if (this.Control.beta < 0.0) {
                            throw new ArithmeticException("Illegal setting in control object: 'beta' is out of range, must be non-negative.");
                        }
                        if (this.Control.Reynolds <= 0.0) {
                            throw new ArithmeticException("Illegal setting in control object: 'Reynolds' is out of range, must be strictly positive.");
                        }
                        if (this.Control.beta > 0.0) {
                            var Visc = new swipViscosity_Term1(
                                this.Control.ViscousPenaltyScaling,
                                d,
                                D,
                                BcMap,
                                ViscosityOption.ConstantViscosityDimensionless,
                                reynolds: this.Control.Reynolds / this.Control.beta);
                            comps.Add(Visc);
                        }

                        if (ConstitutiveEqs) {
                            // extra stress divergence part:
                            comps.Add(new StressDivergence_Cockburn(d, BcMap, this.Control.Reynolds, this.Control.Penalty1, this.Control.Penalty2));
                        } else {
                            Console.WriteLine("!!!Warning!!!: stress divergence deactivated");
                        }
                    }


                    // Continuum equation
                    // ===============================================================================
                    if (MomContinuitycoupling) {
                        for (int d = 0; d < D; d++) {
                            XOP.EquationComponents["div"].Add(new Divergence_DerivativeSource(d, D));
                            XOP.EquationComponents["div"].Add(new Divergence_DerivativeSource_Flux(d, BcMap));

                            //Pressure stabilization for LDG
                            //var presStab = new PressureStabilization(this.Control.PresPenalty2, this.Control.Reynolds);
                            //Console.WriteLine("PresPenalty2 = " + this.Control.PresPenalty2);
                            //XOP.EquationComponents["div"].Add(presStab);
                        }
                    } else {
                        for (int d = 0; d < D; d++) {
                            Console.WriteLine("!!!Warning!!!: Continuity Equation deactivated.");
                            XOP.EquationComponents["div"].Add(new Idsource(VariableNames.Pressure));
                        }
                    }

                    // Constitutive equations
                    // ===============================================================================

                    // Identity part
                    XOP.EquationComponents["constitutiveXX"].Add(new ConstitutiveEqns_Identity(0, this.Control.giesekusfactor, this.Control.Weissenberg, this.Control.beta));
                    XOP.EquationComponents["constitutiveXY"].Add(new ConstitutiveEqns_Identity(1, this.Control.giesekusfactor, this.Control.Weissenberg, this.Control.beta));
                    XOP.EquationComponents["constitutiveYY"].Add(new ConstitutiveEqns_Identity(2, this.Control.giesekusfactor, this.Control.Weissenberg, this.Control.beta));

                    //XOP.EquationComponents["constitutiveXX"].Add(new ConstitutiveEqns_Identity(0));
                    //XOP.EquationComponents["constitutiveXY"].Add(new ConstitutiveEqns_Identity(1));
                    //XOP.EquationComponents["constitutiveYY"].Add(new ConstitutiveEqns_Identity(2));



                    if (ConstitutiveEqs) {
                        Console.WriteLine($"configuring Weissenberg number: {this.Control.Weissenberg:#.##e+00}");

                        //Convective part
                        XOP.EquationComponents["constitutiveXX"].Add(new ConstitutiveEqns_Convective(0, BcMap, this.Control.Weissenberg, this.Control.useFDJacobianForOperatorMatrix, this.Control.alpha));
                        XOP.EquationComponents["constitutiveXY"].Add(new ConstitutiveEqns_Convective(1, BcMap, this.Control.Weissenberg, this.Control.useFDJacobianForOperatorMatrix, this.Control.alpha));
                        XOP.EquationComponents["constitutiveYY"].Add(new ConstitutiveEqns_Convective(2, BcMap, this.Control.Weissenberg, this.Control.useFDJacobianForOperatorMatrix, this.Control.alpha));

                        //Objective Part
                        XOP.EquationComponents["constitutiveXX"].Add(new ConstitutiveEqns_Objective(0, BcMap, this.Control.Weissenberg, this.Control.StressPenalty, this.Control.useFDJacobianForOperatorMatrix));
                        XOP.EquationComponents["constitutiveXY"].Add(new ConstitutiveEqns_Objective(1, BcMap, this.Control.Weissenberg, this.Control.StressPenalty, this.Control.useFDJacobianForOperatorMatrix));
                        XOP.EquationComponents["constitutiveYY"].Add(new ConstitutiveEqns_Objective(2, BcMap, this.Control.Weissenberg, this.Control.StressPenalty, this.Control.useFDJacobianForOperatorMatrix));

                        // Viscous Part
                        XOP.EquationComponents["constitutiveXX"].Add(new ConstitutiveEqns_Viscosity(0, BcMap, this.Control.beta, this.Control.Penalty1));
                        XOP.EquationComponents["constitutiveXY"].Add(new ConstitutiveEqns_Viscosity(1, BcMap, this.Control.beta, this.Control.Penalty1));
                        XOP.EquationComponents["constitutiveYY"].Add(new ConstitutiveEqns_Viscosity(2, BcMap, this.Control.beta, this.Control.Penalty1));

                        // artificial diffusion part
                        if (this.Control.UseArtificialDiffusion == true) {
                            XOP.EquationComponents["constitutiveXX"].Add(new ConstitutiveEqns_Diffusion(this.StressXX.Basis.Degree, Grid.SpatialDimension, ((GridData)GridData).Cells.cj, VariableNames.StressXX));
                            XOP.EquationComponents["constitutiveXY"].Add(new ConstitutiveEqns_Diffusion(this.StressXY.Basis.Degree, Grid.SpatialDimension, ((GridData)GridData).Cells.cj, VariableNames.StressXY));
                            XOP.EquationComponents["constitutiveYY"].Add(new ConstitutiveEqns_Diffusion(this.StressYY.Basis.Degree, Grid.SpatialDimension, ((GridData)GridData).Cells.cj, VariableNames.StressYY));
                        }
                    } else {
                        Console.WriteLine("!!!Warning!!!: Constitutive Equation deactivated.");
                    }


                    // Build spatial operator
                    XOP.Commit();

                    JacobiOp = XOP.GetJacobiOperator(2);

                    // create timestepper
                    //===============================================================

                    // level set - Not needed for non-Level-set calculations
                    LevelSetHandling lsh = LevelSetHandling.None;


                    int bdfOrder;
                    if (this.Control.Timestepper_Scheme == RheologyControl.TimesteppingScheme.CrankNicolson)
                        bdfOrder = -1;
                    //else if (this.Control.Timestepper_Scheme == RheologyControl.TimesteppingScheme.ExplicitEuler)
                    //    bdfOrder = 0;
                    else if (this.Control.Timestepper_Scheme == RheologyControl.TimesteppingScheme.ImplicitEuler)
                        bdfOrder = 1;
                    else if (this.Control.Timestepper_Scheme.ToString().StartsWith("BDF"))
                        bdfOrder = Convert.ToInt32(this.Control.Timestepper_Scheme.ToString().Substring(3));
                    else
                        throw new NotImplementedException("The chosen timestepper is not implemented!");


                    m_BDF_Timestepper = new XdgBDFTimestepping(
                        CurrentSolution.Fields,
                        CurrentResidual.Fields,
                        LsTrk, false,
                        DelComputeOperatorMatrix, null, DelUpdateLevelset,
                        bdfOrder,
                        lsh,
                        MassMatrixShapeandDependence.IsTimeDependent,
                        SpatialOperatorType.Nonlinear,
                        MassScale,
                        this.MultigridOperatorConfig, base.MultigridSequence,
                        this.FluidSpecies, 1, // no hmf order required.
                        0, false,
                        this.Control.NonLinearSolver, this.Control.LinearSolver); //HARDCODED AGGLOMERATION FACTOR -> NOT NEEDED FOR NON-LEVELSET
                    m_BDF_Timestepper.m_ResLogger = base.ResLogger;
                    m_BDF_Timestepper.m_ResidualNames = ArrayTools.Cat(this.ResidualMomentum.Select(f => f.Identification),
                        ResidualConti.Identification, ResidualStressXX.Identification, ResidualStressXY.Identification, ResidualStressYY.Identification);
                }

                //m_BDF_Timestepper.Config_UnderRelax = this.Control.UnderRelax;
                //m_BDF_Timestepper.CustomIterationCallback += this.PlotOnIterationCallback;
                //m_BDF_Timestepper.CustomIterationCallback += this.CoupledIterationCallback;

            }
        }

        bool solveVelocity = true;

        double VelocitySolver_ConvergenceCriterion = 1e-5;

        double StressSolver_ConvergenceCriterion = 1e-5;


        /// <summary>
        /// customizable callback routine for the handling of the coupled level-set iteration
        /// </summary>
        /// <param name="iterIndex"></param>
        /// <param name="currentSol"></param>
        /// <param name="currentRes"></param>
        /// <param name="Mgop"></param>
        protected void CoupledIterationCallback(int iterIndex, double[] currentSol, double[] currentRes, MultigridOperator Mgop) {

            var R = new CoordinateVector(this.CurrentSolution.Mapping.Fields.ToArray());
            Mgop.TransformRhsFrom(R, currentRes);
            int NF = R.Mapping.Fields.Count();

            double VelocityL2Res = 0.0;
            double StressL2Res = 0.0;

            for (int i = 0; i < NF; i++) {
                double L2Res = R.Mapping.Fields[i].L2Norm();
                if (i < 3) {
                    VelocityL2Res += L2Res;
                } else {
                    StressL2Res += L2Res;
                }
            }

            if (solveVelocity && VelocityL2Res < this.VelocitySolver_ConvergenceCriterion) {
                this.solveVelocity = false;
            } else if (!solveVelocity && StressL2Res < this.StressSolver_ConvergenceCriterion) {
                this.solveVelocity = true;
            }

        }

        /// <summary>
        /// Computation of operator matrix used by the timestepper (<see cref="m_BDF_Timestepper"/>).
        /// </summary>
        protected virtual void DelComputeOperatorMatrix(BlockMsrMatrix OpMatrix, double[] OpAffine, UnsetteledCoordinateMapping Mapping, DGField[] CurrentState, Dictionary<SpeciesId, MultidimensionalArray> AgglomeratedCellLengthScales, double phystime) {

            BlockMsrMatrix OutputMatrix;
            double[] OutputAffine;

            // parameters...
            int D = this.LsTrk.GridDat.SpatialDimension;


            AssembleMatrix(out OutputMatrix, out OutputAffine, CurrentState, OpMatrix != null);
            if (OpMatrix != null) {
                OpMatrix.Clear();
                OpMatrix.Acc(1.0, OutputMatrix);
            }

            OpAffine.Clear();
            OpAffine.AccV(1.0, OutputAffine);

            // Gravity Source (default should be zero!)
            if (Control.GravitySource == true) {
                bool test = false;

                if (this.Control.GravityX != null && this.Control.GravityY != null) {
                    Gravity[0].ProjectField(this.Control.GravityX.Vectorize(0.0));
                    Gravity[1].ProjectField(this.Control.GravityY.Vectorize(0.0));
                    int[] MomEqIdx = this.CurrentSolution.Mapping.GetSubvectorIndices(true, 0, 1);
                    OpAffine.AccV(-1.0, this.Gravity.CoordinateVector, MomEqIdx, default(int[]));
                    test = true;
                }

                if (this.Control.GravityXX != null && this.Control.GravityXY != null && this.Control.GravityYY != null) {
                    GravityXX.ProjectField(this.Control.GravityXX.Vectorize(0.0));
                    int[] ConstEqIdx1 = this.CurrentSolution.Mapping.GetSubvectorIndices(true, 3);
                    OpAffine.AccV(-1.0, this.GravityXX.CoordinateVector, ConstEqIdx1, default(int[]));

                    GravityXY.ProjectField(this.Control.GravityXY.Vectorize(0.0));
                    int[] ConstEqIdx2 = this.CurrentSolution.Mapping.GetSubvectorIndices(true, 4);
                    OpAffine.AccV(-1.0, this.GravityXY.CoordinateVector, ConstEqIdx2, default(int[]));

                    GravityYY.ProjectField(this.Control.GravityYY.Vectorize(0.0));
                    int[] ConstEqIdx3 = this.CurrentSolution.Mapping.GetSubvectorIndices(true, 5);
                    OpAffine.AccV(-1.0, this.GravityYY.CoordinateVector, ConstEqIdx3, default(int[]));
                    test = true;
                }

                if (this.Control.GravityDiv != null) {
                    GravityDiv.ProjectField(this.Control.GravityDiv.Vectorize(0.0));
                    int[] ContiEqIdx = this.CurrentSolution.Mapping.GetSubvectorIndices(true, 2);
                    OpAffine.AccV(-1.0, this.GravityDiv.CoordinateVector, ContiEqIdx, default(int[]));
                    test = true;
                }

                if (!test) {
                    throw new ApplicationException("Gravity is true, but no values set!");
                }
            }

            //if (OpMatrix != null) {//base.MPIRank == 0)
            //    var OpWrite = OpMatrix.ToMsrMatrix();
            //    OpWrite.ToFullMatrixOnProc0();
            //    OpWrite.SaveToTextFile("OpMatrix.txt");
            //}
        }

        /// <summary>
        /// Dummy function for level-set update, not used in this application, but required by the timestepper (<see cref="m_BDF_Timestepper"/>).
        /// </summary>
        public virtual double DelUpdateLevelset(DGField[] CurrentState, double phystime, double dt, double UnderRelax, bool incremental) {
            return 0.0;
        }


        // Build and solve system
        //=================================================================
        /// <summary>
        /// Depending on settings, computes either one timestep or a steady-state solution.
        /// </summary>
        protected override double RunSolverOneStep(int TimestepInt, double phystime, double dt) {
            using (new FuncTrace()) {
                /*
                if (this.Control.OperatorMatrixAnalysis == true) {

                    OpAnalysisBase myAnalysis = new OpAnalysisBase(DelComputeOperatorMatrix, CurrentSolution.Mapping, CurrentSolution.Mapping.Fields.ToArray(), null, phystime);
                    myAnalysis.VarGroup = new int[] { 0, 1, 2 };
                    double[] cond = myAnalysis.CondNum();//Analyse();
                    Console.WriteLine("Condition number for full matrix is " + cond[0] + ". Condition number for inner matrix is " + cond[1] + ".");
                    base.QueryHandler.ValueQuery("condFull", cond[0], true);
                    base.QueryHandler.ValueQuery("condInner", cond[1], true);
                }
                */

                TimestepNumber TimestepNo = new TimestepNumber(TimestepInt, 0);
                int D = this.GridData.SpatialDimension;

                //if (TimestepNo[0] > 1) {
                //    this.Control.RaiseWeissenberg = false;
                //}

                base.ResLogger.TimeStep = TimestepInt;

                dt = base.GetFixedTimestep();


                int NoIncrementTimestep;

                Console.WriteLine("Instationary solve, timestep #{0}, dt = {1} ...", TimestepNo, dt);
                var overallstart = DateTime.Now;
                bool m_SkipSolveAndEvaluateResidual = this.Control.SkipSolveAndEvaluateResidual;

                if (Control.RaiseWeissenberg == true) {
                    


                    currentWeissenberg = restartWeissenberg;
                    restartWeissenberg = 0.0; // make sure the restart value is used only once
                    Console.WriteLine("current Weissenberg at " + currentWeissenberg);


                    if (Control.Weissenberg != 0.0) {

                        if (Control.WeissenbergIncrement != 0.0) {
                            NoIncrementTimestep = (int)(Control.Weissenberg / Control.WeissenbergIncrement);
                        } else {
                            throw new ArgumentException("Raise Weissenberg is turned on, but WeissenbergIncrement is zero!");
                        }

                    } else {
                        throw new ArgumentException("Raise Weissenberg is turned on, but aim Weissenberg is 0.0 (Newtonian)!");
                    }

                    for (int WeIncCounter = 0; WeIncCounter <= NoIncrementTimestep; WeIncCounter++) {

                        if (Control.UseArtificialDiffusion == true) {
                            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++
                            // USING Weissenberg increase, USING artificial viscosity
                            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++

                            artificialMaxViscosity = 1.0;

                            for (int j = 0; j < 3; j++) {




                                if (Control.UsePerssonSensor == true) {
                                    perssonsensor.Update(StressXX);
                                } else {
                                    throw new ArgumentException("artificial viscosity is turned on, but Persson sensor is turned off!");
                                }

                                m_BDF_Timestepper.Solve(phystime, dt, m_SkipSolveAndEvaluateResidual);

                                //this.ResLogger.NextTimestep(false);

                                // this evaluation must later out of this loop. now here for comparing resluts with  
                                PlotCurrentState(phystime, new TimestepNumber(TimestepNo.MajorNumber, WeIncCounter));
                                SaveToDatabase(new TimestepNumber(TimestepNo.MajorNumber, WeIncCounter), phystime);

                                if (Control.Bodyforces == true) {
                                    if (Log != null) {
                                        WriteLogLine(TimestepNo.MajorNumber, phystime);
                                    } else {
                                        double[] force = IBMSolverUtils.GetForces_BoundaryFitted(Velocity.Current, StressXX, StressXY, StressYY, Pressure, LevSetTrk, 1 / Control.Reynolds, Control.beta, "Wall_cylinder");
                                        Console.WriteLine();
                                        Console.WriteLine("Force in x:" + force[0] + ", force in y:" + force[1]);
                                        Console.WriteLine();
                                    }
                                }

                                artificialMaxViscosity = artificialMaxViscosity - 0.5;
                            }
                        } else {
                            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++
                            // USING Weissenberg increase, NO artificial viscosity
                            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++

                            m_BDF_Timestepper.Solve(phystime, dt, m_SkipSolveAndEvaluateResidual);

                            //this.ResLogger.NextTimestep(false);

                            // this evaluation must later out of this loop. now here for comparing results with  
                            //PlotCurrentState(phystime, new TimestepNumber(TimestepNo.MajorNumber, WeIncCounter));
                            SaveToDatabase(new TimestepNumber(TimestepNo.MajorNumber, WeIncCounter), phystime);

                            if (Control.Bodyforces == true) {
                                if (Log != null) {
                                    WriteLogLine(TimestepNo.MajorNumber, phystime);
                                } else {
                                    double[] force = IBMSolverUtils.GetForces_BoundaryFitted(Velocity.Current, StressXX, StressXY, StressYY, Pressure, LevSetTrk, 1 / Control.Reynolds, Control.beta, "Wall_cylinder");
                                    Console.WriteLine();
                                    Console.WriteLine("Force in x:" + force[0] + ", force in y:" + force[1]);
                                    Console.WriteLine();
                                }
                            }
                        }

                        ChangeMesh = Control.AdaptiveMeshRefinement;
                        while (ChangeMesh == true) {
                            this.MpiRedistributeAndMeshAdapt(TimestepNo.MajorNumber, phystime);
                            perssonsensor.Update(StressXX);
                            PlotCurrentState(phystime, TimestepNo);
                            SaveToDatabase(TimestepNo, phystime);
                        }

                        if (currentWeissenberg < Control.Weissenberg) {
                            currentWeissenberg = currentWeissenberg + Control.WeissenbergIncrement;
                        } else {
                            WeIncCounter = int.MaxValue; // breaks the Weissenberg-increase loop.
                        }

                        currentWeissenberg = Math.Min(currentWeissenberg, Control.Weissenberg); // prevents any overshoot
                        Console.WriteLine("Raise Weissenberg number to " + currentWeissenberg);
                    }
                } else {

                    currentWeissenberg = Control.Weissenberg;

                    if (Control.UseArtificialDiffusion == true) {
                        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++
                        // USING artificial viscosity, but NO Weissenberg increase
                        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++


                        artificialMaxViscosity = 1.0;

                        for (int j = 0; j < 3; j++) {

                            if (Control.UsePerssonSensor == true) {
                                perssonsensor.Update(StressXX);
                            } else {
                                throw new ArgumentException("artificial viscosity is turned on, but Persson sensor is turned off!");
                            }

                            m_BDF_Timestepper.Solve(phystime, dt, m_SkipSolveAndEvaluateResidual);

                            // this evaluation must later out of this loop. now here for comparing resluts with  
                            //PlotCurrentState(phystime, new TimestepNumber(TimestepNo.MajorNumber, i));
                            //SaveToDatabase(new TimestepNumber(TimestepNo.MajorNumber, i), phystime);

                            if (Control.Bodyforces == true) {
                                if (Log != null) {
                                    WriteLogLine(TimestepNo.MajorNumber, phystime);
                                } else {
                                    double[] force = IBMSolverUtils.GetForces_BoundaryFitted(Velocity.Current, StressXX, StressXY, StressYY, Pressure, LevSetTrk, 1 / Control.Reynolds, Control.beta, "Wall_cylinder");
                                    Console.WriteLine();
                                    Console.WriteLine("Force in x:" + force[0] + ", force in y:" + force[1]);
                                    Console.WriteLine();
                                }
                            }

                            artificialMaxViscosity = artificialMaxViscosity - 0.5;
                        }

                        ChangeMesh = Control.AdaptiveMeshRefinement;
                        while (ChangeMesh == true) {
                            this.MpiRedistributeAndMeshAdapt(TimestepNo.MajorNumber, phystime);
                        }
                    } else {
                        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++
                        // Using only timestepper, NO ADDITIONAL LOOP
                        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++

                        m_BDF_Timestepper.Solve(phystime, dt, m_SkipSolveAndEvaluateResidual);

                        // simple implicit Euler solve for debugging and excluding the bdf timestepper
                        //____________________________________________________________________________________
                        //Console.WriteLine("CAREFUL! Simple implicit Euler unsteady solve for Debugging!");
                        //var map = this.CurrentSolution.Mapping;
                        //var Mtx = new BlockMsrMatrix(map, map);
                        //double[] b = new double[map.LocalLength];
                        //this.DelComputeOperatorMatrix(Mtx, b, map, map.Fields.ToArray(), null, phystime + dt);

                        //double[] RHS = new double[map.LocalLength];
                        //RHS.AccV(-1, b);
                        //int J = this.GridData.iLogicalCells.NoOfLocalUpdatedCells;
                        //int Np = this.Velocity.Current[0].Basis.Length;
                        //double oodt = 1.0 / dt;
                        //for (int j = 0; j < J; j++) { // loop over cells
                        //    for (int iVar = 0; iVar < 2; iVar++) { // loop over VelX, VelY
                        //        for (int n = 0; n < Np; n++) {
                        //            int iLoc = map.LocalUniqueCoordinateIndex(iVar, j, n);
                        //            RHS[iLoc] += oodt * this.CurrentSolution[iLoc];

                        //            int iGlob = map.GlobalUniqueCoordinateIndex(iVar, j, n);
                        //            Mtx[iGlob, iGlob] += oodt;
                        //        }
                        //    }
                        //}

                        //Mtx.Solve_Direct(this.CurrentSolution, RHS);
                        //____________________________________________________________________________________________

                        if (Control.Bodyforces == true) {
                            if (Log != null) {
                                WriteLogLine(TimestepNo.MajorNumber, phystime);
                            } else {
                                double[] force = IBMSolverUtils.GetForces_BoundaryFitted(Velocity.Current, StressXX, StressXY, StressYY, Pressure, LevSetTrk, 1 / Control.Reynolds, Control.beta, "Wall_cylinder");
                                Console.WriteLine();
                                Console.WriteLine("Force in x:" + force[0] + ", force in y:" + force[1]);
                                Console.WriteLine();
                            }
                        }
                    }
                }

                var overallstop = DateTime.Now;
                var overallduration = overallstop - overallstart;

                Console.WriteLine("Duration of this timestep: " + overallduration);

                if (Control.ComputeL2Error == true) {
                    this.ComputeL2Error();
                }

                this.ResLogger.NextTimestep(false);

                return dt;


            }
        }


        void ParameterUpdate(IEnumerable<DGField> CurrentState, IEnumerable<DGField> ParameterVar) {
            /*
            int D = this.GridData.SpatialDimension;

            var U0 = new VectorField<SinglePhaseField>(CurrentState.Take(D).Select(F => (SinglePhaseField)F).ToArray());
            var Stress0 = new VectorField<SinglePhaseField>(CurrentState.Skip(D + 1).Take(3).Select(F => (SinglePhaseField)F).ToArray());

            if (this.U0MeanRequired) {

                SinglePhaseField[] __U0mean = ParameterVar.Skip(D).Take(D).Select(f => f as SinglePhaseField).ToArray();
                VectorField<SinglePhaseField> U0mean = new VectorField<SinglePhaseField>(__U0mean);

                U0mean.Clear();
                ComputeAverageU(U0, U0mean);

                SinglePhaseField[] __U0 = ParameterVar.Take(D).Select(f => f as SinglePhaseField).ToArray();
                Debug.Assert(ArrayTools.AreEqual(__U0, U0.ToArray(), (fa, fb) => object.ReferenceEquals(fa, fb)));
            } else {
                Debug.Assert(ParameterVar.Take(2 * D).Where(f => f != null).Count() == 0);
            }

            if (this.Control.SetParamsAnalyticalSol == false) {
                SinglePhaseField[] __VelocityXGradient = ParameterVar.Skip(2 * D).Take(D).Select(f => f as SinglePhaseField).ToArray();
                SinglePhaseField[] __VelocityYGradient = ParameterVar.Skip(3 * D).Take(D).Select(f => f as SinglePhaseField).ToArray();
                Debug.Assert(ArrayTools.AreEqual(__VelocityXGradient, VelocityXGradient.ToArray(), (fa, fb) => object.ReferenceEquals(fa, fb)));
                Debug.Assert(ArrayTools.AreEqual(__VelocityYGradient, VelocityYGradient.ToArray(), (fa, fb) => object.ReferenceEquals(fa, fb)));

                VelocityXGradient.Clear();
                VelocityXGradient.GradientByFlux(1.0, U0[0]);
                VelocityYGradient.Clear();
                VelocityYGradient.GradientByFlux(1.0, U0[1]);
            }
            */
            if (this.Control.UseArtificialDiffusion == true) {

                //SinglePhaseField __ArtificialViscosity = ParameterVar.Skip(5 * D + 1).Take(1).Select(f => f as SinglePhaseField).ToArray()[0];
                SinglePhaseField __ArtificialViscosity = ParameterVar.ElementAt(0) as SinglePhaseField;
                if (!object.ReferenceEquals(this.artificalViscosity, __ArtificialViscosity))
                    throw new ApplicationException();

                ArtificialViscosity.ProjectArtificalViscosityToDGField(__ArtificialViscosity, perssonsensor, this.Control.SensorLimit, artificialMaxViscosity);
            }
        }

        /// <summary>
        /// Only for testing / NUnit:
        /// checks whether the finite difference approximation of the Jacobian of <see cref="XOP"/>
        /// and the Jacobian operator (<see cref="JacobiOp"/>)
        /// provide approximately the same matrix and affine vector.
        /// </summary>
        internal void CheckJacobian() {
            // Parameters
            DGField[] Params;
            if (this.Control.UseArtificialDiffusion) {
                Params = new[] { artificalViscosity };
            } else {
                Params = null;
            }

            // initialize linearization point with random numbers
            var CurrentState = this.CurrentSolution.Fields.Select(f => f.CloneAs()).ToArray();
            var CurrentVector = new CoordinateVector(CurrentState);
            Random r = new Random(0); // seed of 0 guarantees the same random numbers on every run
            int L = CurrentVector.Count;
            for (int i = 0; i < L; i++) {
                CurrentVector[i] = r.NextDouble();
            }

            var domMap = CurrentVector.Mapping;
            var codMap = domMap;
            Assert.IsTrue(codMap.EqualsPartition(this.CurrentResidual.Mapping));

            // Finite Difference Linearization
            var FDbuilder = XOP.GetFDJacobianBuilder(domMap, null, codMap, null);
            FDbuilder.OperatorCoefficients.UserDefinedValues.Add("Weissenbergnumber", currentWeissenberg);
            var JacobianFD = new BlockMsrMatrix(codMap, domMap);
            var AffineFD = new double[JacobianFD.NoOfRows];
            FDbuilder.ComputeMatrix(JacobianFD, AffineFD);

            // Jacobian Operator
            var JacParams = JacobiOp.ParameterUpdate;
            var TmpParams = JacParams.AllocateParameters(CurrentState, Params);
            var map = new CoordinateMapping(CurrentState);
            var JacBuilder = JacobiOp.GetMatrixBuilder(map, TmpParams, map);
            JacBuilder.OperatorCoefficients.UserDefinedValues.Add("Weissenbergnumber", currentWeissenberg);
            this.ParameterUpdate(CurrentState, TmpParams);
            JacParams.ParameterUpdate(CurrentState, TmpParams);
            var JacobiDX = new BlockMsrMatrix(map);
            var AffineDX = new double[map.LocalLength];
            JacBuilder.ComputeMatrix(JacobiDX, AffineDX);

            // Comparison
            Console.WriteLine("Comparison of finite difference and direct Jacobian matrix:");
            var ErrMtx = JacobianFD.CloneAs();
            ErrMtx.Acc(-1.0, JacobiDX);
            double InfNorm_ErrMtx = ErrMtx.InfNorm();
            Console.WriteLine("  Jacobian Matrix Delta Norm: " + InfNorm_ErrMtx);

            var ErrAff = AffineFD.CloneAs();
            ErrAff.AccV(-1.0, AffineDX);
            double InfNorm_ErrAff = ErrAff.MPI_L2Norm();
            Console.WriteLine("  Affine Vector Delta Norm: " + InfNorm_ErrAff);

            // Error Threshold checks
            double DenomM = (JacobianFD.InfNorm(), JacobiDX.InfNorm()).Max();
            Assert.Less(InfNorm_ErrMtx / DenomM, 0.01, "Mismatch in between finite difference Jacobi matrix and direct Jacobi matrix");

            double DenomA = (CurrentVector.MPI_L2Norm(), AffineFD.MPI_L2Norm(), AffineDX.MPI_L2Norm()).Max();
            Assert.Less(InfNorm_ErrAff / DenomA, 0.01, "Mismatch in Affine Vector between finite difference Jacobi and direct Jacobi");
        }

        static int counter = 1;

        /// <summary>
        /// Computation of operator matrix to be used by DelComputeOperatorMatrix, the SpatialOperatorAnalysis and some unit tests(<see cref="m_BDF_Timestepper"/>).
        /// </summary>
        public void AssembleMatrix(out BlockMsrMatrix OpMatrix, out double[] OpAffine, DGField[] CurrentState, bool Linearization) {
            int D = this.GridData.SpatialDimension;

            var U0 = new VectorField<SinglePhaseField>(CurrentState.Take(D).Select(F => (SinglePhaseField)F).ToArray());
            var Stress0 = new VectorField<SinglePhaseField>(CurrentState.Skip(D + 1).Take(3).Select(F => (SinglePhaseField)F).ToArray());

            if (U0.Count != D)
                throw new ArgumentException("Spatial dimension and number of velocity parameter components does not match!");

            if (Stress0.Count != (D * D + D) / 2)
                throw new ArgumentException("Spatial dimension and number of stress parameter components does not match!");


            // parameters
            //============================================================
            SinglePhaseField[] U0_U0mean;
            if (this.U0MeanRequired) {
                Basis U0meanBasis = new Basis(GridData, 0);
                VectorField<SinglePhaseField> U0mean = new VectorField<SinglePhaseField>(D, U0meanBasis, "U0mean_", SinglePhaseField.Factory);
                U0mean.Clear();

                U0_U0mean = ArrayTools.Cat<SinglePhaseField>(U0, U0mean);
            } else {
                U0_U0mean = new SinglePhaseField[2 * D];
            }

            DGField[] Params;

            if (this.Control.useFDJacobianForOperatorMatrix) {
                Params = ArrayTools.Cat<DGField>(U0_U0mean, VelocityXGradient, VelocityYGradient, Stress0, artificalViscosity);
            } else {
                if (this.Control.UseArtificialDiffusion) {
                    Params = new[] { artificalViscosity };
                } else {
                    Params = null;
                }
            }


            // create mappings
            //==========================================================
            var codMap = this.CurrentResidual.Mapping;
            var domMap = this.CurrentSolution.Mapping;


            // provide a linearization of the operator
            //===========================================================
            if (Linearization) {

                bool useFDJacobianForOperatorMatrix = this.Control.useFDJacobianForOperatorMatrix;

                //if (this.Control.NonLinearSolver.SolverCode == )
                //    useJacobianForOperatorMatrix = false;

                // create matrix and affine vector:
                OpMatrix = new BlockMsrMatrix(codMap, domMap);
                OpAffine = new double[codMap.LocalLength];


                // 'custom' Linearization 
                if (!useFDJacobianForOperatorMatrix) {
                    //var Mbuilder = XOP.GetMatrixBuilder(domMap, Params, codMap);
                    //this.ParameterUpdate(domMap.Fields, Params);
                    //Mbuilder.ComputeMatrix(OpMatrix, OpAffine);
                    //Mbuilder.OperatorCoefficients.UserDefinedValues.Add("Weissenbergnumber", currentWeissenberg);

                    // Jacobian
                    var JacParams = JacobiOp.ParameterUpdate;
                    var TmpParams = JacParams.AllocateParameters(CurrentState, Params);
                    var map = new CoordinateMapping(CurrentState);
                    var JacBuilder = JacobiOp.GetMatrixBuilder(map, TmpParams, map);
                    JacBuilder.OperatorCoefficients.UserDefinedValues.Add("Weissenbergnumber", currentWeissenberg);
                    ParameterUpdate(CurrentState, TmpParams);
                    JacParams.ParameterUpdate(CurrentState, TmpParams);
                    JacBuilder.ComputeMatrix(OpMatrix, OpAffine);


                } else {

                    // Finite Difference Linearization

                    if (Params != null) {
                        goto SkipToEnd;
                    } else {
                        throw new ArgumentException("FD Jacobian needs the parameter list, but it was null.");
                    }

                SkipToEnd:
                    var FDbuilder = XOP.GetFDJacobianBuilder(domMap, Params, codMap, this.ParameterUpdate);

                    FDbuilder.OperatorCoefficients.UserDefinedValues.Add("Weissenbergnumber", currentWeissenberg);
                    FDbuilder.ComputeMatrix(OpMatrix, OpAffine);

                    // FDJacobian has (Mx +b) as RHS, for unsteady calc. we must subtract Mx for real affine Vector!
                    OpMatrix.SpMV(-1.0, new CoordinateVector(CurrentState), 1.0, OpAffine);

                }

                //int[] Idx = domMap.GetSubvectorIndices(((GridData)(this.GridData)).BoundaryCells.Complement(), false, new[] { 0, 1, 2, 3, 4, 5 });
                //Idx.SaveToTextFile("InnerIdx.txt");

                //OpMatrix.SaveToTextFileSparse("OpMatrix-" + counter + ".txt");
                //OpAffine.SaveToTextFile("RHS-" + counter + ".txt");
                //counter++;

                // Set Pressure Reference Point
                //======================================================
                if (!this.BcMap.DirichletPressureBoundary) {
                    if (OpMatrix != null) {

                        IBMSolverUtils.SetPressureReferencePoint(
                            CurrentSolution.Mapping,
                            this.GridData.SpatialDimension,
                            this.LsTrk,
                            OpMatrix, OpAffine);
                    } else {
                        IBMSolverUtils.SetPressureReferencePointResidual(
                            new CoordinateVector(CurrentState),
                            this.GridData.SpatialDimension,
                            this.LsTrk,
                            OpAffine);
                    }
                }

                OpMatrix.CheckForNanOrInfM();
                OpAffine.CheckForNanOrInfV();
            } else {

                // explicit evaluation of the operator
                //========================================================
                OpMatrix = null;
                OpAffine = new double[codMap.LocalLength];
                var eval = XOP.GetEvaluatorEx(CurrentState, Params, codMap);
                this.ParameterUpdate(eval.DomainFields.Fields, Params);
                eval.OperatorCoefficients.UserDefinedValues.Add("Weissenbergnumber", currentWeissenberg);

                eval.Evaluate(1.0, 1.0, OpAffine);

            }

        }

        /// <summary>
        /// configuration options for <see cref="MultigridOperator"/>.
        /// </summary>
        public MultigridOperator.ChangeOfBasisConfig[][] MultigridOperatorConfig {
            get {
                int pVel = this.Velocity.Current[0].Basis.Degree;
                int pPrs = this.Pressure.Basis.Degree;
                int pStr = this.StressXX.Basis.Degree;
                int D = this.GridData.SpatialDimension;
                // set the MultigridOperator configuration for each level:
                // it is not necessary to have exactly as many configurations as actual multigrid levels:
                // the last configuration entry will be used for all higher level
                MultigridOperator.ChangeOfBasisConfig[][] configs = new MultigridOperator.ChangeOfBasisConfig[3][];
                for (int iLevel = 0; iLevel < configs.Length; iLevel++) {
                    int pVelLv = Math.Max(1, pVel - iLevel);
                    int pPreLv = Math.Max(1, pPrs - iLevel);
                    int pStrLv = Math.Max(1, pStr - iLevel);

                    /*
                    configs[iLevel] = new MultigridOperator.ChangeOfBasisConfig[2];
                    configs[iLevel][0] = new MultigridOperator.ChangeOfBasisConfig() {
                            DegreeS = new int[] { pVelLv, pVelLv, pStrLv, pStrLv, pStrLv },
                            //mode = this.Control.VelocityBlockPrecondMode,
                            mode = MultigridOperator.Mode.SymPart_DiagBlockEquilib,
                            VarIndex = new int[] { 0, 1, 3, 4, 5 }
                        };
                    configs[iLevel][1] = new MultigridOperator.ChangeOfBasisConfig() {
                        DegreeS = new int[] { Math.Max(0, pPrs - iLevel) },
                        mode = this.Control.PressureBlockPrecondMode,
                        VarIndex = new int[] { D }
                    };
                    //*/




                    configs[iLevel] = new MultigridOperator.ChangeOfBasisConfig[1];
                    configs[iLevel][0] = new MultigridOperator.ChangeOfBasisConfig() {
                        mode = MultigridOperator.Mode.LeftInverse_DiagBlock,
                        VarIndex = new int[] { 0, 1, 2, 3, 4, 5 },
                        DegreeS = new int[] { pVel, pVel, pPrs, pStr, pStr, pStr }
                    };
                    //*/

                    /*
                    configs[iLevel] = new MultigridOperator.ChangeOfBasisConfig[D + 4];
                    
                    // configurations for velocity
                    for (int d = 0; d < D; d++) {
                        configs[iLevel][d] = new MultigridOperator.ChangeOfBasisConfig() {
                            DegreeS = new int[] { Math.Max(1, pVel - iLevel) },
                            //mode = this.Control.VelocityBlockPrecondMode,
                            mode = MultigridOperator.Mode.SymPart_DiagBlockEquilib,
                            VarIndex = new int[] { d }
                        };
                    }
                    // configuration for pressure
                    configs[iLevel][D] = new MultigridOperator.ChangeOfBasisConfig() {
                        DegreeS = new int[] { Math.Max(0, pPrs - iLevel) },
                        mode = this.Control.PressureBlockPrecondMode,
                        VarIndex = new int[] { D }
                    };

                    
                    // configurations for stresses
                    for (int d = 3; d < 6; d++) {
                        configs[iLevel][d] = new MultigridOperator.ChangeOfBasisConfig() {
                            DegreeS = new int[] { Math.Max(1, pStr - iLevel) },
                            mode = MultigridOperator.Mode.SymPart_DiagBlockEquilib,
                            VarIndex = new int[] { d }
                        };
                    }
                    //*/
                }

                return configs;
            }
        }

        /// <summary>
        /// Plotting the current state
        /// </summary>
        protected override void PlotCurrentState(double physTime, Foundation.IO.TimestepNumber timestepNo, int superSampling = 0) {
            // Standard
            DGField[] myFields = ArrayTools.Cat<DGField>(Velocity.Current, ResidualMomentum, ResidualConti, Pressure, StressXX, StressXY, StressYY, LevSet, ResidualStressXX, ResidualStressXY, ResidualStressYY); //, VelocityXGradient, VelocityYGradient, Gravity

            //Add sensor field only if Persson sensor exists
            if (perssonsensor != null) {
                myFields = ArrayTools.Cat<DGField>(myFields, perssonsensor.GetField());
            }

            //Add field only if artificial viscosity is turned on
            if (artificalViscosity != null) {
                myFields = ArrayTools.Cat<DGField>(myFields, artificalViscosity);
            }

            Tecplot.PlotFields(myFields, "Rheology-" + timestepNo.ToString(), physTime, superSampling);
        }

        /*
        /// <summary>
        /// Plotting the in interation callback
        /// </summary>
        protected void PlotOnIterationCallback(int iterIndex, double[] currentSol, double[] currentRes, MultigridOperator Mgop) {
            // Standard
            DGField[] myFields = ArrayTools.Cat<DGField>(Velocity.Current, ResidualMomentum, ResidualConti, Pressure, StressXX, StressXY, StressYY, LevSet, ResidualStressXX, ResidualStressXY, ResidualStressYY); //, VelocityXGradient, VelocityYGradient, Gravity,

            //Add sensor field only if Persson sensor exists
            if (perssonsensor != null) {
                myFields = ArrayTools.Cat<DGField>(myFields, perssonsensor.GetField());
            }

            //Add field only if artificial viscosity is turned on
            if (artificalViscosity != null) {
                myFields = ArrayTools.Cat<DGField>(myFields, artificalViscosity);
            }

            Tecplot.PlotFields(myFields, "Rheology-" + iterIndex.ToString(), 0.0, 2);
        }
        */
        /// <summary>
        /// Initialising the DG fields
        /// </summary>
        protected override void SetInitial() {
            int D = GridData.SpatialDimension;
            if (D != 2)
                throw new NotImplementedException("currently only support for 2 dimensions.");

            base.SetInitial();
            this.LsTrk.UpdateTracker();
            CreateEquationsAndSolvers(null);
            m_BDF_Timestepper.SingleInit();
            VelocityXGradient = new VectorField<SinglePhaseField>(D, Velocity.Current[0].Basis, "VelocityX_Gradient", SinglePhaseField.Factory);
            VelocityYGradient = new VectorField<SinglePhaseField>(D, Velocity.Current[1].Basis, "VelocityY_Gradient", SinglePhaseField.Factory);

            if (this.Control.SetParamsAnalyticalSol == true) {
                U = new SinglePhaseField(new Basis(this.GridData, Velocity.Current[0].Basis.Degree), "UAnalytical");
                V = new SinglePhaseField(new Basis(this.GridData, Velocity.Current[0].Basis.Degree), "VAnalytical");
                U.ProjectField(this.Control.VelFunctionU);
                V.ProjectField(this.Control.VelFunctionV);

                VelocityXGradient.Clear();
                VelocityXGradient.Gradient(1.0, U);
                VelocityYGradient.Clear();
                VelocityYGradient.Gradient(1.0, V);
            }

            Console.WriteLine("Total number of cells:    {0}", Grid.NumberOfCells);
            Console.WriteLine("Total number of DOFs:     {0}", CurrentSolution.Mapping.TotalLength);

            if (this.CurrentSessionInfo.ID != Guid.Empty && base.MPIRank == 0) {
                InitLogFile(this.CurrentSessionInfo.ID);
            }

        }


        /// <summary>
        /// performs restart
        /// </summary>
        /// <param name="Time">
        /// on exit, the physical time associated with the field state
        /// </param>
        /// <param name="TimestepNo">
        /// on exit, the physical time associated with the field state
        /// </param>
        protected override void LoadRestart(out double Time, out TimestepNumber TimestepNo) {
            base.LoadRestart(out Time, out TimestepNo);

            this.LsTrk.UpdateTracker();
        }

        /// <summary>
        /// overriding the method to implement any user-specific tasks which
        /// should be carried out after a restart file has been loaded (e.g.,
        /// setting the correct time for a time-stepper)
        /// </summary>
        public override void PostRestart(double time, TimestepNumber timestep) {
            base.PostRestart(time, timestep);

            VelocityXGradient = new VectorField<SinglePhaseField>(this.GridData.SpatialDimension, Velocity.Current[0].Basis, "VelocityX_Gradient", SinglePhaseField.Factory);
            VelocityYGradient = new VectorField<SinglePhaseField>(this.GridData.SpatialDimension, Velocity.Current[1].Basis, "VelocityY_Gradient", SinglePhaseField.Factory);
        }


        /// <summary>
        /// Computes average velocity in case of Navier-Stokes Equations
        /// </summary>
        /// <param name="U0"></param>
        /// <param name="U0mean"></param>
        private void ComputeAverageU(VectorField<SinglePhaseField> U0, VectorField<SinglePhaseField> U0mean) {
            using (FuncTrace ft = new FuncTrace()) {
                var CC = this.LsTrk.Regions.GetCutCellMask();
                int D = this.LsTrk.GridDat.SpatialDimension;
                double minvol = Math.Pow(this.LsTrk.GridDat.Cells.h_minGlobal, D);

                int QuadDegree = this.HMForder;

                var qh = LsTrk.GetXDGSpaceMetrics(this.FluidSpecies, QuadDegree, 1).XQuadSchemeHelper;
                foreach (var Spc in this.FluidSpecies) { // loop over species...
                    //var Spc = this.LsTrk.GetSpeciesId("B"); {
                    // shadow fields
                    var U0_Spc = U0.ToArray();
                    var U0mean_Spc = U0mean.ToArray();


                    // normal cells:
                    for (int d = 0; d < D; d++) {
                        U0mean_Spc[d].AccLaidBack(1.0, U0_Spc[d], this.LsTrk.Regions.GetSpeciesMask(Spc));
                    }


                    // cut cells
                    var scheme = qh.GetVolumeQuadScheme(Spc, IntegrationDomain: this.LsTrk.Regions.GetCutCellMask());

                    var rule = scheme.Compile(this.LsTrk.GridDat, QuadDegree);
                    CellQuadrature.GetQuadrature(new int[] { D + 1 }, // vector components: ( avg_vel[0], ... , avg_vel[D-1], cell_volume )
                        this.LsTrk.GridDat,
                        rule,
                        delegate (int i0, int Length, QuadRule QR, MultidimensionalArray EvalResult) {
                            EvalResult.Clear();
                            for (int d = 0; d < D; d++)
                                U0_Spc[d].Evaluate(i0, Length, QR.Nodes, EvalResult.ExtractSubArrayShallow(-1, -1, d));
                            var Vol = EvalResult.ExtractSubArrayShallow(-1, -1, D);
                            Vol.SetAll(1.0);
                        },
                        delegate (int i0, int Length, MultidimensionalArray ResultsOfIntegration) {
                            for (int i = 0; i < Length; i++) {
                                int jCell = i + i0;

                                double Volume = ResultsOfIntegration[i, D];
                                if (Math.Abs(Volume) < minvol * 1.0e-12) {
                                    // keep current value
                                    // since the volume of species 'Spc' in cell 'jCell' is 0.0, the value in this cell should have no effect
                                } else {
                                    for (int d = 0; d < D; d++) {
                                        double IntVal = ResultsOfIntegration[i, d];
                                        U0mean_Spc[d].SetMeanValue(jCell, IntVal / Volume);
                                    }
                                }

                            }
                        }
                        ).Execute();
                }
                U0mean.ForEach(F => F.CheckForNanOrInf(true, true, true));
            }
        }

        /// <summary>
        /// Computes the L2 Error of all Fields compared to exact solution specified in the control file
        /// </summary>
        protected void ComputeL2Error() {
            if (this.Control.ExSol_Velocity == null && this.Control.ExSol_Pressure == null && this.Control.ExSol_Stress == null) {
                // nothing to do
                return;
            }


            int D = this.GridData.SpatialDimension;

            int order = Velocity.Current[0].Basis.Degree * 2;
            var SchemeHelper = LsTrk.GetXDGSpaceMetrics(this.FluidSpecies, order).XQuadSchemeHelper;

            // Velocity error
            // ===============================================
            if (this.Control.ExSol_Velocity != null) {
                Dictionary<string, double[]> L2Error_Species = new Dictionary<string, double[]>();
                double[] L2Error = new double[D];

                foreach (var spId in this.FluidSpecies) {
                    string spc = this.LsTrk.GetSpeciesName(spId);

                    L2Error_Species.Add(spc, new double[D]);

                    var scheme = SchemeHelper.GetVolumeQuadScheme(spId);

                    for (int d = 0; d < D; d++) {
                        L2Error_Species[spc][d] = this.Velocity.Current[d].L2Error(this.Control.ExSol_Velocity[d].Vectorize(0.0), order, scheme);
                        L2Error[d] += L2Error_Species[spc][d].Pow2();

                        base.QueryHandler.ValueQuery("L2err_" + VariableNames.Velocity_d(d) + "#" + spc, L2Error_Species[spc][d], true);

                    }
                }
                L2Error = L2Error.Select(x => x.Sqrt()).ToArray();

                for (int d = 0; d < D; d++) {
                    base.QueryHandler.ValueQuery("L2err_" + VariableNames.Velocity_d(d), L2Error[d], true);
                    Console.WriteLine("L2err " + VariableNames.Velocity_d(d) + " is " + L2Error[d]);
                }
            }


            // pressure error
            // =============================================================
            if (this.Control.ExSol_Pressure != null) {

                double L2Error = 0;

                L2Error = this.Pressure.L2Error(this.Control.ExSol_Pressure.Vectorize(0.0), order - 1);
                base.QueryHandler.ValueQuery("L2err_" + VariableNames.Pressure, L2Error, true);
                Console.WriteLine("L2err " + VariableNames.Pressure + " is " + L2Error);
            }

            // Stress error
            // =============================================================
            if (this.Control.ExSol_Stress != null) {
                double[] L2Error = new double[3];

                L2Error[0] = this.StressXX.L2Error(this.Control.ExSol_Stress[0].Vectorize(0.0), order);
                L2Error[1] = this.StressXY.L2Error(this.Control.ExSol_Stress[1].Vectorize(0.0), order);
                L2Error[2] = this.StressYY.L2Error(this.Control.ExSol_Stress[2].Vectorize(0.0), order);

                base.QueryHandler.ValueQuery("L2err_" + VariableNames.StressXX, L2Error[0], true);
                base.QueryHandler.ValueQuery("L2err_" + VariableNames.StressXY, L2Error[1], true);
                base.QueryHandler.ValueQuery("L2err_" + VariableNames.StressYY, L2Error[2], true);

                Console.WriteLine("L2err " + VariableNames.StressXX + " is " + L2Error[0]);
                Console.WriteLine("L2err " + VariableNames.StressXY + " is " + L2Error[1]);
                Console.WriteLine("L2err " + VariableNames.StressYY + " is " + L2Error[2]);
            }
        }



        /// <summary>
        /// Integration degree of HMF used throughout the application: this should ensure that
        /// only one HMF rule is created.
        /// </summary>
        public int HMForder {
            get {
                int VelDeg = this.Velocity.Current.Max(field => field.Basis.Degree);
                int Order = (VelDeg * (!this.Control.Stokes ? 3 : 2));
                Order += 2; // safety factor
                return Order;
            }
        }

        //ADAPTIVE MESH REFINEMENT
        //======================================================================

        /// <summary>
        /// refinement indicator
        /// </summary>
        int LevelIndicator(int j, int CurrentLevel) {

            if (this.Control.UsePerssonSensor) {

                double maxVal = this.perssonsensor.GetValue(j);

                double[] coord = this.GridData.iGeomCells.GetCenter(j);

                //bound for perssonsensor should be around 1e-7 - 1e-8 that there is refinement behind the cylinder!
                double upperbound = this.Control.SensorLimit;
                double lowerbound = upperbound * 0.001;

                int DesiredLevel_j = CurrentLevel;

                if (maxVal != 0.0) {
                    if (maxVal > upperbound && DesiredLevel_j < this.Control.RefinementLevel) {

                        DesiredLevel_j = DesiredLevel_j + 1;

                    } else if (maxVal < lowerbound && DesiredLevel_j > 0) {
                        DesiredLevel_j = DesiredLevel_j - 1;
                    }
                } else {
                    if (Math.Abs(coord[0] - 10) < 2 && DesiredLevel_j < 2) // this.Control.RefinementLevel)
                        DesiredLevel_j = DesiredLevel_j + 1;
                }

                return DesiredLevel_j;

            } else {

                double celllength = ((GridData)GridData).Cells.cj[j];
                double maxVal = this.StressXX.GetMeanValue(j) / celllength;  // this.perssonsensor.GetValue(j);

                //bound for perssonsensor should be around 1e-7 - 1e-8 that there is refinement behind the cylinder!
                double upperbound = this.Control.SensorLimit / celllength;
                double lowerbound = -1 * this.Control.SensorLimit / celllength;

                int DesiredLevel_j = CurrentLevel;

                if (maxVal > upperbound && DesiredLevel_j < this.Control.RefinementLevel) {

                    DesiredLevel_j = DesiredLevel_j + 1;

                } else if (maxVal < lowerbound && DesiredLevel_j > 0) {
                    DesiredLevel_j = DesiredLevel_j - 1;
                }

                return DesiredLevel_j;
            }

        }

        /// <summary>
        /// Adaptation of the current mesh.
        /// </summary>
        protected override void AdaptMesh(int TimestepNo, out GridCommons newGrid, out GridCorrelation old2NewGrid) {

            if (this.Control.AdaptiveMeshRefinement) {

                bool AnyChange = GridRefinementController.ComputeGridChange((GridData)(this.GridData), null, LevelIndicator, out List<int> CellsToRefineList, out List<int[]> Coarsening);
                ChangeMesh = AnyChange;
                int NoOfCellsToRefine = 0;
                int NoOfCellsToCoarsen = 0;
                if (AnyChange) {
                    int[] glb = (new int[] {
                    CellsToRefineList.Count,
                    Coarsening.Sum(L => L.Length),
                }).MPISum();
                    NoOfCellsToRefine = glb[0];
                    NoOfCellsToCoarsen = glb[1];
                }
                int oldJ = this.GridData.CellPartitioning.TotalLength;

                // Update Grid
                // ===========

                if (AnyChange) {

                    //PlotCurrentState(hack_Phystime, new TimestepNumber(new int[] { hack_TimestepIndex, 1 }), 2);
                    Console.WriteLine();
                    Console.WriteLine("       Refining " + NoOfCellsToRefine + " of " + oldJ + " cells");
                    Console.WriteLine("       Coarsening " + NoOfCellsToCoarsen + " of " + oldJ + " cells");

                    newGrid = ((GridData)(this.GridData)).Adapt(CellsToRefineList, Coarsening, out old2NewGrid);

                    //PlotCurrentState(hack_Phystime, new TimestepNumber(new int[] { hack_TimestepIndex, 2 }), 2);#

                } else {

                    newGrid = null;
                    old2NewGrid = null;
                }
            } else {

                newGrid = null;
                old2NewGrid = null;
            }
        }

        /// <summary>
        /// Appends the <see cref="currentWeissenberg"/> number to the timestep
        /// </summary>
        protected override TimestepInfo GetCurrentTimestepInfo(TimestepNumber timestepno, double t) {
            var Rtsi = new RheologyTimestepInfo(t, CurrentSessionInfo, timestepno, IOFields, currentWeissenberg);
            return Rtsi;
        }

        /// <summary>
        /// sets Weissenberg number from timestep-info 
        /// </summary>
        protected override void OnRestartTimestepInfo(TimestepInfo tsi) {
            if (this.Control.RaiseWeissenberg) {

                var Rtsi = tsi as RheologyTimestepInfo;
                if (Rtsi != null) {
                    Console.Write("Restoring Weissenberg number form database...  ");
                    Console.Write($" Weissenberg = {Rtsi.currentWeissenbergNumber}");
                    this.restartWeissenberg = Rtsi.currentWeissenbergNumber;
                    Console.WriteLine();
                } else {
                    Console.WriteLine($"No Weissenberg number contained in time-step; starting with pre-set.");
                }
            }
        }


        #region logging

        TextWriter Log;
        string header;

        public void InitLogFile(Guid sessionID) {

            if (this.Control.Bodyforces) {
                Log = base.DatabaseDriver.FsDriver.GetNewLog("BodyForces", sessionID);
                header = String.Format("{0}\t{1}\t{2}\t{3}\t{4}", "#timestep", "#time","Wi", "ForceX", "ForceY");
            }
        }

        public void WriteLogLine(TimestepNumber TimestepNo, double phystime) {

            double[] force = IBMSolverUtils.GetForces_BoundaryFitted(Velocity.Current, StressXX, StressXY, StressYY, Pressure, LevSetTrk, 1 / Control.Reynolds, Control.beta, "Wall_cylinder");
            string logline = String.Format("{0}\t{1}\t{2}\t{3}\t{4}", TimestepNo, phystime, currentWeissenberg, force[0], force[1]);
            Log.WriteLine(logline);
            Log.Flush();
        }

        #endregion
    }  

    class Idsource : LinearSource {
        public Idsource(string _var) {
            m_var = _var;
        }

        string m_var;

        public override IList<string> ArgumentOrdering {
            get {
                return new string[] { m_var };
            }
        }

        protected override double Source(double[] x, double[] parameters, double[] U) {
            return U[0];
        }
    }

}


