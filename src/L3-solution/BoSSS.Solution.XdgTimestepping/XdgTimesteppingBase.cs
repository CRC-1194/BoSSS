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
using BoSSS.Solution.AdvancedSolvers;
using BoSSS.Platform;
using ilPSP.LinSolvers;
using ilPSP.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ilPSP;
using MPI.Wrappers;
using BoSSS.Foundation.Grid.Aggregation;
using ilPSP.Tracing;

namespace BoSSS.Solution.XdgTimestepping {

    /// <summary>
    /// Callback-template for level-set updates.
    /// </summary>
    /// <param name="OpMtx">
    /// Output for the linear part; the operator matrix must be stored in the valeu that is passes to the function, i.e. the caller allocates memory;
    /// if null, an explixit evaluation of the operator is required, which should be stored the affine part.
    /// </param>
    /// <param name="OpAffine">
    /// Output for the affine part.
    /// </param>
    /// <param name="Mapping">
    /// Corresponds with row and columns of <paramref name="OpMtx"/>, resp. with <paramref name="OpAffine"/>.
    /// </param>
    /// <param name="CurrentState"></param>
    /// <param name="AgglomeratedCellLengthScales">
    /// Length scale *of agglomerated grid* for each cell, e.g. to set penalty parameters. 
    /// </param>
    /// <param name="time"></param>
    public delegate void DelComputeOperatorMatrix(BlockMsrMatrix OpMtx, double[] OpAffine, UnsetteledCoordinateMapping Mapping, DGField[] CurrentState, Dictionary<SpeciesId, MultidimensionalArray> AgglomeratedCellLengthScales, double time);
        
    ///// <summary>
    ///// Callback-Template for the mass matrix update.
    ///// </summary>
    //public delegate void DelComputeMassMatrix(BlockMsrMatrix MassMtx, UnsetteledCoordinateMapping Mapping, DGField[] CurrentState, double time);
 
    /// <summary>
    /// Callback-template for level-set updates.
    /// </summary>
    /// <param name="CurrentState"></param>
    /// <param name="time">
    /// Actual simulation time for the known value;
    /// </param>
    /// <param name="dt">
    /// Timestep size.
    /// </param>
    /// <param name="UnderRelax">
    /// </param>
    /// <param name="incremental">
    /// true for Splitting schemes with subdivided level-set evolution (e.g. Strang-Splitting)
    /// </param>
    /// <returns>
    /// Some kind of level-set-residual in order to check convergence in a fully coupled simulation
    /// (see <see cref="LevelSetHandling.Coupled_Iterative"/>)
    /// </returns>
    public delegate double DelUpdateLevelset(DGField[] CurrentState, double time, double dt, double UnderRelax, bool incremental);

    /// <summary>
    /// Callback-template for pushing the level-set in case of increment timestepping
    /// </summary>
    public delegate void DelPushLevelSetRelatedStuff();

    /// <summary>
    /// Controls the updating of the mass-matrix, resp. the temporal operator.
    /// </summary>
    public enum MassMatrixShapeandDependence {
        /// <summary>
        /// May be useful e.g. if one runs the FSI solver as a pure single-phase solver,
        /// i.e. if the Level-Set is outside the domain.
        /// </summary>
        IsIdentity = 0,

        /// <summary>
        /// For a mass-matrix which is not the identity, but constant over time.
        /// E.g., if the level-set is constant in time, the mass matrix does not change.
        /// </summary>
        IsNonIdentity = 1,

        /// <summary>
        /// For a mass matrix which changes over time, but does not depend on the solution.
        /// E.g., the interface motion is prescribed.
        /// </summary>
        IsTimeDependent = 2,

        /// <summary>
        /// The mass matrix depends on time and the solution, e.g. for multiphase flows,
        /// where the position of the fluid interface is influenced by the flow field.
        /// </summary>
        IsTimeAndSolutionDependent = 3
    }
    

    /// <summary>
    /// Treatment of the level-set.
    /// </summary>
    public enum LevelSetHandling {
        /// <summary>
        /// Level-Set remains constant.
        /// </summary>
        None = 0,

        /// <summary>
        /// Level-Set is handled using Lie-Splitting.
        /// </summary>
        LieSplitting = 1,

        /// <summary>
        /// Level-Set is handled using Strang-Splitting.
        /// </summary>
        StrangSplitting = 2,

        /// <summary>
        /// Level-Set is updated once per time-step.
        /// </summary>
        Coupled_Once = 3,

        /// <summary>
        /// Level-Set is updated in every iteration, until convergence is reached.
        /// </summary>
        Coupled_Iterative = 4,

        /// <summary>
        /// Level-Set is handled using Lie-Splitting. Use this for the fully coupled FSI-Solver
        /// </summary>
        FSI_LieSplittingFullyCoupled = 5,

        /// <summary>
        /// Level-Set is handled using Lie-Splitting. Use this for the fully coupled FSI-Solver
        /// </summary>
        FSI_Coupled_Iterative = 6,
    }

    public enum SpatialOperatorType {

        /// <summary>
        /// The spatial operator is linear, but time dependent (e.g. a Diffusion operator with time-dependent coefficient).
        /// </summary>
        LinearTimeDependent = 1,

        /// <summary>
        /// The spatial operator is non-linear, therefore it is also considered to be time-dependent;
        /// however, a linearization must be available.
        /// </summary>
        Nonlinear = 2
    }

    /// <summary>
    /// Common base-class for XDG timesteppers.
    /// </summary>
    abstract public class XdgTimesteppingBase {

        protected XdgTimesteppingBase(
            Control.NonLinearSolverConfig nonlinconfig,
            Control.LinearSolverConfig linearconfig) {
            XdgSolverFactory = new SolverFactory(nonlinconfig, linearconfig);
        }

        /// <summary>
        /// Configuration sanity checks, to be used by constructors of derived classes.
        /// </summary>
        protected void CommonConfigurationChecks() {
            if (this.Config_MassMatrixShapeandDependence == MassMatrixShapeandDependence.IsNonIdentity
                && this.Config_LevelSetHandling != LevelSetHandling.None)
                throw new ArgumentOutOfRangeException("illegal configuration");

            
        }


        public MassMatrixShapeandDependence Config_MassMatrixShapeandDependence {
            get;
            protected set;
        }

        /// <summary>
        /// How the interface motion should be integrated
        /// </summary>
        public LevelSetHandling Config_LevelSetHandling {
            get;
            protected set;
        }

 
        /// <summary>
        /// in case of <see cref="Config_LevelSetHandling"/> set to <see cref="LevelSetHandling.Coupled_Iterative"/>,
        /// the factor for under-relaxing the level set movement 
        /// </summary>
        public double IterUnderrelax = 1.0;


        public double Config_LevelSetConvergenceCriterion = 1.0e-6;
            

        /// <summary>
        /// Session path for writing in database
        /// </summary>
        public string SessionPath = "";

        
        ///// <summary>
        ///// Scaling of the mass matrix, for each species and each variable.
        ///// </summary>
        //public IDictionary<SpeciesId, IEnumerable<double>> Config_MassScale {
        //    get;
        //    protected set;
        //}


        /// <summary>
        /// Species to compute, must be a subset of <see cref="LevelSetTracker.SpeciesIdS"/>
        /// </summary>
        public SpeciesId[] Config_SpeciesToCompute {
            get;
            protected set;
        }

        /// <summary>
        /// Quadrature order on cut cells.
        /// </summary>
        public int Config_CutCellQuadratureOrder {
            get;
            protected set;
        }



        /// <summary>
        /// Whether the operator is linear, nonlinear.
        /// </summary>
        public SpatialOperatorType Config_SpatialOperatorType {
            get;
            protected set;
        }

        /// <summary>
        /// Callback routine to update the operator matrix.
        /// </summary>
        public DelComputeOperatorMatrix ComputeOperatorMatrix {
            get;
            protected set;
        }


        /// <summary>
        /// For the computation of the mass matrix
        /// </summary>
        public ITemporalOperator TemporalOperator {
            get {
                return AbstractOperator.TemporalOperator;
            }
        }


        protected void ComputeMassMatrixImpl(BlockMsrMatrix MassMatrix, double time) {
            using(new FuncTrace()) {
                if(!MassMatrix._RowPartitioning.EqualsPartition(CurrentStateMapping))
                    throw new ArgumentException("Internal error.");
                if(!MassMatrix._ColPartitioning.EqualsPartition(CurrentStateMapping))
                    throw new ArgumentException("Internal error.");

                if(this.Config_MassMatrixShapeandDependence == MassMatrixShapeandDependence.IsIdentity) {
                    MassMatrix.AccEyeSp(1.0);
                } else {
                    if(TemporalOperator is ConstantXTemporalOperator cxt) {
                        cxt.SetTrackerHack(this.m_LsTrk);
                    }

                    var builder = TemporalOperator.GetMassMatrixBuilder(CurrentStateMapping, CurrentParameters, this.Residuals.Mapping);
                    builder.time = time;
                    builder.ComputeMatrix(MassMatrix, default(double[]), 1.0); // Remark: 1/dt - scaling is applied somewhere else
                }
            }
        }

        /// <summary>
        /// Callback routine to update the level set.
        /// </summary>
        public DelUpdateLevelset UpdateLevelset {
            get;
            protected set;
        }
                
        /// <summary>
        /// As usual the threshold for cell agglomeration.
        /// </summary>
        public double Config_AgglomerationThreshold {
            get;
            set;
        }

        /// <summary>
        /// True, if a nonlinear solver has to be used.
        /// </summary>
        protected virtual bool RequiresNonlinearSolver {
            get {
                // ask before changing


                if (Config_SpatialOperatorType == SpatialOperatorType.Nonlinear)
                    return true;

                if (Config_LevelSetHandling == LevelSetHandling.Coupled_Iterative)
                    return true;

                //if (Config_MassMatrixShapeandDependence == MassMatrixShapeandDependence.IsTimeAndSolutionDependent)
                //    return true;

                return false;
            }

        }

        protected LevelSetTracker m_LsTrk;

        /// <summary>
        /// The sequence of aggregation grids, on which multi grid solvers work.
        /// </summary>
        public AggregationGridData[] MultigridSequence {
            get;
            protected set;
        }

        /// <summary>
        /// The aggregation grid basis, initialized by <see cref="InitMultigrid(DGField[], bool)"/>
        ///  - 1st index: grid level
        ///  - 2nd index: variable
        /// </summary>
        protected AggregationGridBasis[][] MultigridBasis;

        /// <summary>
        /// Last solver residuals.
        /// </summary>
        public CoordinateVector Residuals {
            get;
            protected set;
        }

        

        /// <summary>
        /// Initialization of the solver/preconditioner.
        /// </summary>
        protected void InitMultigrid(DGField[] Fields, bool useX) {
            Basis[] bs;
            using (new FuncTrace("Aggregation_basis_init")) {
                if (useX) {
                    bs = new Basis[Fields.Length];
                    for (int i = 0; i < bs.Length; i++)
                        bs[i] = new XDGBasis(m_LsTrk, Fields[i].Basis.Degree);
                } else {
                    bs = Fields.Select(f => f.Basis).ToArray();
                }

                this.MultigridBasis = AggregationGridBasis.CreateSequence(this.MultigridSequence, bs);
            }
        }

        /// <summary>
        /// Agglomerator for the currently set level-set position . 
        /// </summary>
        protected MultiphaseCellAgglomerator m_CurrentAgglomeration;

        /// <summary>
        /// Agglomerated length scales, input for <see cref="ComputeOperatorMatrix"/>.
        /// </summary>
        protected Dictionary<SpeciesId, MultidimensionalArray> GetAgglomeratedLengthScales() {
            if(m_CurrentAgglomeration != null) {
                //
                // agglomerated length scales are available from 
                //
                return m_CurrentAgglomeration.CellLengthScales;
            } else {
                //
                // 'Notlösung' -- no actual agglomeration available - use length scales form a temporary agglomerator.
                //
                var agg = this.m_LsTrk.GetAgglomerator(this.Config_SpeciesToCompute, this.Config_CutCellQuadratureOrder, this.Config_AgglomerationThreshold);
                return agg.CellLengthScales;
            }
        }

        /// <summary>
        /// Set when calling constructor,
        /// </summary>

        public SolverFactory XdgSolverFactory;

        private Control.NonLinearSolverConfig m_nonlinconfig {
            get {
                return XdgSolverFactory.GetNonLinearConfig;
            }
        }

        private Control.LinearSolverConfig m_linearconfig {
            get {
                return XdgSolverFactory.GetLinearConfig;
            }
        }


        /// <summary>
        /// Returns either a solver for the Navier-Stokes or the Stokes system.
        /// E.g. for testing purposes, one might also use a nonlinear solver on a Stokes system.
        /// </summary>
        /// <param name="nonlinSolver"></param>
        /// <param name="linearSolver"></param>
        protected virtual string GetSolver(out NonlinearSolver nonlinSolver, out ISolverSmootherTemplate linearSolver) {
            nonlinSolver = null;
            linearSolver = null;

            if (Config_SpatialOperatorType != SpatialOperatorType.Nonlinear)
                m_nonlinconfig.SolverCode = BoSSS.Solution.Control.NonLinearSolverCode.Picard;

            XdgSolverFactory.GenerateNonLin(out nonlinSolver, out linearSolver, this.AssembleMatrixCallback, this.MultigridBasis, Config_MultigridOperator, SessionPath, MultigridSequence);
            
            string ls_strg = String.Format("{0}", m_linearconfig.SolverCode);
            string nls_strg = String.Format("{0}", m_nonlinconfig.SolverCode);

            if ((this.Config_LevelSetHandling == LevelSetHandling.Coupled_Iterative)&&(nonlinSolver.Equals(typeof(FixpointIterator)))) {
                ((FixpointIterator)nonlinSolver).CoupledIteration_Converged = LevelSetConvergenceReached;
            }

            // set callback for diagnostic output
            // ----------------------------------
            if (nonlinSolver != null) {
                nonlinSolver.IterationCallback += this.LogResis;
                if (linearSolver != null && linearSolver is ISolverWithCallback) {
                    //((ISolverWithCallback)linearSolver).IterationCallback = this.MiniLogResi;
                }
            } else {
                if (linearSolver != null && linearSolver is ISolverWithCallback) {
                    ((ISolverWithCallback)linearSolver).IterationCallback = this.LogResis;
                }
            }

            return String.Format("nonlinear Solver: {0}, linear Solver: {1}", nls_strg, ls_strg);
        }


        //void MiniLogResi(int iterIndex, double[] currentSol, double[] currentRes, MultigridOperator Mgop) {
        //    double resiNorm = currentRes.MPI_L2Norm();
        //    Console.WriteLine("    lin slv: " + iterIndex + "  "+ resiNorm);
        //}


        /// <summary>
        /// Configuration for residual logging (provisional), see <see cref="LogResis(int, double[], double[], MultigridOperator)"/>.
        /// </summary>
        public ResidualLogger m_ResLogger;

        /// <summary>
        /// Configuration for residual logging (provisional), see <see cref="LogResis(int, double[], double[], MultigridOperator)"/>.
        /// 
        /// Names for the residual of each variable.
        /// </summary>
        public string[] m_ResidualNames;

        /// <summary>
        /// Configuration for residual logging (provisional), see <see cref="LogResis(int, double[], double[], MultigridOperator)"/>.
        /// 
        /// If true, the residual will we transformed back to the original XDG basis (before agglomeration and block preconditioning)
        /// before the L2-norm is computed.
        /// </summary>
        public bool m_TransformedResi = true;

        /// <summary>
        /// Logging of residuals (provisional).
        /// </summary>
        virtual protected void LogResis(int iterIndex, double[] currentSol, double[] currentRes, MultigridOperator Mgop) {

            if (m_ResLogger != null) {
                int NF = this.CurrentStateMapping.Fields.Count;
                m_ResLogger.IterationCounter = iterIndex;

                if (m_TransformedResi) {
                    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
                    // transform current solution and residual back to the DG domain
                    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

                    //var X = new CoordinateVector(this.CurrentStateMapping.Fields.Select(f => f.CloneAs()).ToArray());
                    var R = this.Residuals;
                    R.Clear();

                    Mgop.TransformRhsFrom(R, currentRes);
                    //Mgop.TransformSolFrom(X, currentSol);
                    //this.m_Agglomerator.Extrapolate(X, X.Mapping);
                    this.m_CurrentAgglomeration.Extrapolate(R.Mapping);

                    for (int i = 0; i < NF; i++) {
                        double L2Res = R.Mapping.Fields[i].L2Norm();
                        m_ResLogger.CustomValue(L2Res, m_ResidualNames[i]);
                    }
                } else {

                    // +++++++++++++++++++++++
                    // un-transformed residual
                    // +++++++++++++++++++++++

                    var VarIdx = NF.ForLoop(i => Mgop.Mapping.GetSubvectorIndices(i));

                    for (int i = 0; i < VarIdx.Length; i++) {
                        double L2Res = 0.0;

                        foreach (int idx in VarIdx[i])
                            L2Res += currentRes[idx - Mgop.Mapping.i0].Pow2();
                        L2Res = L2Res.MPISum().Sqrt(); // would be better to do the MPISum for all L2Res together,
                                                       //                                but this implementation is anyway inefficient....

                        m_ResLogger.CustomValue(L2Res, m_ResidualNames[i]);
                    }
                }

                if (Config_LevelSetHandling == LevelSetHandling.Coupled_Iterative) {
                    m_ResLogger.CustomValue(m_LastLevelSetResidual, "LevelSet");
                }

                m_ResLogger.NextIteration(true);
            }
        }
        
        public double m_LastLevelSetResidual;

        protected bool LevelSetConvergenceReached() {

            return (m_LastLevelSetResidual < Config_LevelSetConvergenceCriterion);
        }
        
        /// <summary>
        /// Sets <see cref="Config_MultigridOperator"/> to a default configuration.
        /// </summary>
        protected void SetConfig_MultigridOperator_Default(IEnumerable<DGField> Fields) {
            int NF = Fields.Count();

            // set the MultigridOperator configuration for each level:
            // it is not necessary to have exactly as many configurations as actual multigrid levels:
            // the last configuration entry will be used for all higher level
            MultigridOperator.ChangeOfBasisConfig[][] configs = new MultigridOperator.ChangeOfBasisConfig[3][];
            for (int iLevel = 0; iLevel < configs.Length; iLevel++) {
                configs[iLevel] = new MultigridOperator.ChangeOfBasisConfig[NF];

                // configurations for velocity
                for (int d = 0; d < NF; d++) {
                    int p = Fields.ElementAt(d).Basis.Degree;

                    configs[iLevel][d] = new MultigridOperator.ChangeOfBasisConfig() {
                        DegreeS = new[] { Math.Max(0, p - iLevel) },
                        mode = MultigridOperator.Mode.IdMass_DropIndefinite,
                        VarIndex = new int[] { d }
                    };
                }
            }

            this.Config_MultigridOperator = configs;
        }


        /// <summary>
        /// Configuration options for <see cref="MultigridOperator"/>.
        /// </summary>
        public virtual MultigridOperator.ChangeOfBasisConfig[][] Config_MultigridOperator {
            get;
            protected set;
        }

        /// <summary>
        /// Les spatial operateur 
        /// </summary>
        public virtual ISpatialOperator AbstractOperator {
            get;
            protected set;
        }

        /// <summary>
        /// Coordinate mapping of the current solution.
        /// </summary>
        abstract public CoordinateMapping CurrentStateMapping {
            get;
        }

        /// <summary>
        /// 
        /// </summary>
        public DGField[] CurrentParameters {
            get;
            protected set;
        }

        /// <summary>
        /// Callback-routine (<see cref="OperatorEvalOrLin"/>) to update the linear resp. linearized system, 
        /// see <see cref="OperatorEvalOrLin"/> resp. <see cref="NonlinearSolver.m_AssembleMatrix"/>.
        /// </summary>
        /// <param name="argCurSt">Input, current state of solution.</param>
        /// <param name="System">Output.</param>
        /// <param name="Affine">Output.</param>
        /// <param name="MassMatrix">
        /// Mass matrix including agglomeration, without any scaling,
        /// required for block-precond.
        /// </param>
        /// <param name="Linearization">
        /// - true: assemble matrix and affine vector
        /// - false: evaluate operator (<paramref name="System"/> will be null)
        /// </param>
        /// <param name="abstractOperator">
        ///  the original operator that somehow produced the matrix; yes, this API is convoluted piece-of-shit
        /// </param>
        abstract protected void AssembleMatrixCallback(out BlockMsrMatrix System, out double[] Affine, out BlockMsrMatrix MassMatrix, DGField[] argCurSt, bool Linearization, out ISpatialOperator abstractOperator);

        /// <summary>
        /// Unscaled, agglomerated mass matrix used by the preconditioner.
        /// </summary>
        protected BlockMsrMatrix m_PrecondMassMatrix;

        /// <summary>
        /// Returns a collection of local and global condition numbers in order to assess the operators stability
        /// </summary>
        public IDictionary<string, double> OperatorAnalysis(IEnumerable<int[]> VarGroups = null) {
            AssembleMatrixCallback(out BlockMsrMatrix System, out double[] Affine, out BlockMsrMatrix MassMatrix, this.CurrentStateMapping.Fields.ToArray(), true, out var Dummy);

            
            if(VarGroups == null) {
                int NoOfVar = this.CurrentStateMapping.Fields.Count;
                VarGroups = new int[][] { NoOfVar.ForLoop(i => i) };
            }

            var Ret = new Dictionary<string, double>();
            foreach(int[] varGroup in VarGroups) {
                var ana = new BoSSS.Solution.AdvancedSolvers.Testing.OpAnalysisBase(this.m_LsTrk, System, Affine, this.CurrentStateMapping, this.m_CurrentAgglomeration, MassMatrix, this.Config_MultigridOperator, this.AbstractOperator);
                ana.VarGroup = varGroup;
                var Table = ana.GetNamedProperties();
                
                foreach(var kv in Table) {
                    if(!Ret.ContainsKey(kv.Key)) {
                        Ret.Add(kv.Key, kv.Value);
                    }
                }
            }

            return Ret;
        }
    }
}
