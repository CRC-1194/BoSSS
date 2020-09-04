﻿using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Aggregation;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.AdvancedSolvers;
using BoSSS.Solution.Control;
using BoSSS.Solution.Timestepping;
using ilPSP;
using ilPSP.LinSolvers;
using ilPSP.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoSSS.Solution.XdgTimestepping {

    /// <summary>
    /// Codes for various time integration schemes of <see cref="XdgBDFTimestepping"/> and <see cref="XdgRKTimestepping"/>
    /// </summary>
    public enum TimeSteppingScheme {
        
        /// <summary>
        /// Explicit Euler using the <see cref="XdgBDFTimestepping"/> implementation, <see cref="BDFSchemeCoeffs.ExplicitEuler"/>
        /// </summary>
        ExplicitEuler = 0,

        /// <summary>
        /// equivalent of BDF1, using the BDF-implementation, <see cref="BDFSchemeCoeffs.BDF(int)"/>
        /// </summary>
        ImplicitEuler = 1,

        /// <summary>
        /// (Implicit) Crank-Nicholson using the BDF-implementation (<see cref="XdgBDFTimestepping"/>), <see cref="BDFSchemeCoeffs.CrankNicolson"/>
        /// </summary>
        CrankNicolson = 2000,

        /// <summary>
        /// backward differentiation formula of order 2, <see cref="BDFSchemeCoeffs.BDF(int)"/>
        /// </summary>
        BDF2 = 2,

        /// <summary>
        /// backward differentiation formula of order 3, <see cref="BDFSchemeCoeffs.BDF(int)"/>
        /// </summary>
        BDF3 = 3,

        /// <summary>
        /// backward differentiation formula of order 4, <see cref="BDFSchemeCoeffs.BDF(int)"/>
        /// </summary>
        BDF4 = 4,

        /// <summary>
        /// backward differentiation formula of order 5, <see cref="BDFSchemeCoeffs.BDF(int)"/>
        /// </summary>
        BDF5 = 5,

        /// <summary>
        /// backward differentiation formula of order 6, <see cref="BDFSchemeCoeffs.BDF(int)"/>
        /// </summary>
        BDF6 = 6,

        /// <summary>
        /// explicit Runge-Kutta, <see cref="RungeKuttaScheme.RungeKutta1901"/>
        /// </summary>
        RK4 = 104,

        /// <summary>
        /// explicit Runge-Kutta, <see cref="RungeKuttaScheme.TVD3"/>
        /// </summary>
        RK3 = 103,

        /// <summary>
        /// explicit Runge-Kutta, <see cref="RungeKuttaScheme.Heun2"/> 
        /// </summary>
        RK2 = 102,

        /// <summary>
        /// explicit Runge-Kutta, <see cref="RungeKuttaScheme.ExplicitEuler"/>
        /// </summary>
        RK1 = 101,

        /// <summary>
        /// explicit Runge-Kutta, <see cref="RungeKuttaScheme.ExplicitEuler2"/>
        /// </summary>
        RK1u1 = 110,

        /// <summary>
        /// Implicit Euler using the implicit Runge-Kutta implementation, <see cref="RungeKuttaScheme.ImplicitEuler"/> 
        /// </summary>
        RK_ImplicitEuler = 201,

        /// <summary>
        /// (Implicit) Crank-Nicholson using the implicit Runge-Kutta (<see cref="XdgRKTimestepping"/>) implementation, <see cref="RungeKuttaScheme.CrankNicolson"/>
        /// </summary>
        RK_CrankNic = 202,

        /// <summary>
        /// Implicit, <see cref="RungeKuttaScheme.IMEX3"/>
        /// </summary>
        RK_IMEX3 = 203
    }


    /// <summary>
    /// Driver class which provides a simplified interface to <see cref="XdgBDFTimestepping"/> and <see cref="XdgRKTimestepping"/>
    /// </summary>
    public class XdgTimestepping {

        public TimeSteppingScheme Scheme {
            get;
            private set;
        }

        public XSpatialOperatorMk2 XdgOperator {
            get;
            private set;
        }

        XSpatialOperatorMk2 m_JacobiXdgOperator;

        XSpatialOperatorMk2 GetJacobiXdgOperator() {
            if(m_JacobiXdgOperator == null) {
                m_JacobiXdgOperator = XdgOperator.GetJacobiOperator(GridDat.SpatialDimension) as XSpatialOperatorMk2;
            }
            return m_JacobiXdgOperator;
        }

        
        public SpatialOperator DgOperator {
            get;
            private set;
        }

        SpatialOperator m_JacobiDgOperator;

        SpatialOperator GetJacobiDgOperator() {
            if(m_JacobiDgOperator == null) {
                m_JacobiDgOperator = DgOperator.GetJacobiOperator(GridDat.SpatialDimension) as SpatialOperator;
            }
            return m_JacobiDgOperator;
        }



        public ISpatialOperator Operator {
            get {

                if(XdgOperator != null)
                    return XdgOperator;
                if(DgOperator != null)
                    return DgOperator;

                throw new NotImplementedException();
            }
        }


        /// <summary>
        /// <see cref="XSpatialOperatorMk2.Species"/>
        /// </summary>
        public SpeciesId[] UsedSpecies {
            get {
                if(XdgOperator != null)
                    return XdgOperator.Species.Select(spcName => LsTrk.GetSpeciesId(spcName)).ToArray();
                else
                    return null;
            }
        }

        /// <summary>
        /// Initial Value
        /// </summary>
        public CoordinateMapping CurrentState {
            get {
                return TimesteppingBase.CurrentStateMapping;
            }
            
        }

        /// <summary>
        /// Residual vector during/after linear/nonlinear solver iterations
        /// </summary>
        public CoordinateMapping IterationResiduals {
            get {
                return TimesteppingBase.Residuals.Mapping;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IList<DGField> Parameters {
            get;
            private set;
        }


        public LevelSetTracker LsTrk {
            get;
            private set;
        }
             
        public IGridData GridDat {
            get {
                return LsTrk.GridDat;
            }
        }

        /// <summary>
        /// Constructor for an XDG operator
        /// </summary>
        public XdgTimestepping(
            XSpatialOperatorMk2 op,
            IEnumerable<DGField> Fields,
            IEnumerable<DGField> __Parameters,
            IEnumerable<DGField> IterationResiduals,
            TimeSteppingScheme __Scheme,
            DelUpdateLevelset _UpdateLevelset,
            LevelSetHandling _LevelSetHandling,
            MultigridOperator.ChangeOfBasisConfig[][] _MultigridOperatorConfig,
            AggregationGridData[] _MultigridSequence,
            double _AgglomerationThreshold,
            LinearSolverConfig LinearSolver, NonLinearSolverConfig NonLinearSolver) //
        {
            this.Scheme = __Scheme;
            this.XdgOperator = op;

            if(__Parameters == null)
                this.Parameters = new DGField[0];
            else
                this.Parameters = __Parameters.ToArray();


            foreach(var f in Fields.Cat(IterationResiduals).Cat(Parameters)) {
                if(f is XDGField xf) {
                    if(LsTrk == null) {
                        LsTrk = xf.Basis.Tracker;
                    } else {
                        if(!object.ReferenceEquals(LsTrk, xf.Basis.Tracker))
                            throw new ArgumentException();
                    }
                }
            }
            if(LsTrk == null)
                throw new ArgumentException("unable to get Level Set Tracker reference");

            bool UseX = Fields.Any(f => f is XDGField) || IterationResiduals.Any(f => f is XDGField);

            ConstructorCommon(op, UseX,
                Fields, this.Parameters, IterationResiduals, 
                myDelComputeXOperatorMatrix, 
                _UpdateLevelset, 
                _LevelSetHandling,
                _MultigridOperatorConfig, 
                _MultigridSequence, 
                _AgglomerationThreshold,
                LinearSolver, NonLinearSolver);

        }

        private void ConstructorCommon(ISpatialOperator op, bool UseX, IEnumerable<DGField> Fields, IEnumerable<DGField> __Parameters, IEnumerable<DGField> IterationResiduals, DelComputeOperatorMatrix __delComputeOperatorMatrix, 
            DelUpdateLevelset _UpdateLevelset, LevelSetHandling _LevelSetHandling, 
            MultigridOperator.ChangeOfBasisConfig[][] _MultigridOperatorConfig, AggregationGridData[] _MultigridSequence, 
            double _AgglomerationThreshold,
            LinearSolverConfig LinearSolver, NonLinearSolverConfig NonLinearSolver) //
        {
            RungeKuttaScheme rksch;
            int bdfOrder;
            DecodeScheme(this.Scheme, out rksch, out bdfOrder);

            SpatialOperatorType _SpatialOperatorType = SpatialOperatorType.Nonlinear;


            int quadOrder = op.QuadOrderFunction(
                Fields.Select(f => f.Basis.Degree).ToArray(),
                Parameters.Select(f => f != null ? f.Basis.Degree : 0).ToArray(),
                IterationResiduals.Select(f => f.Basis.Degree).ToArray());



            if(bdfOrder > -1000) {
                m_BDF_Timestepper = new XdgBDFTimestepping(Fields, __Parameters, IterationResiduals,
                    LsTrk, true,
                    __delComputeOperatorMatrix, op.TemporalOperator, _UpdateLevelset,
                    bdfOrder,
                    _LevelSetHandling,
                    MassMatrixShapeandDependence.IsTimeDependent,
                    _SpatialOperatorType,
                    _MultigridOperatorConfig, _MultigridSequence,
                    this.LsTrk.SpeciesIdS.ToArray(), quadOrder,
                    _AgglomerationThreshold, UseX,
                    NonLinearSolver,
                    LinearSolver);

                m_BDF_Timestepper.Config_AgglomerationThreshold = _AgglomerationThreshold;
            } else {
                m_RK_Timestepper = new XdgRKTimestepping(Fields.ToArray(), __Parameters, IterationResiduals.ToArray(),
                    LsTrk,
                    __delComputeOperatorMatrix, op.TemporalOperator, _UpdateLevelset,
                    rksch,
                    _LevelSetHandling,
                    MassMatrixShapeandDependence.IsTimeDependent,
                    _SpatialOperatorType,
                    _MultigridOperatorConfig, _MultigridSequence,
                    this.LsTrk.SpeciesIdS.ToArray(), quadOrder,
                    _AgglomerationThreshold, UseX,
                    NonLinearSolver,
                    LinearSolver);

                m_RK_Timestepper.Config_AgglomerationThreshold = _AgglomerationThreshold;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Scheme"></param>
        /// <param name="rksch">if <paramref name="Scheme"/> denotes a Runge-Kutta scheme, well, the Runge-Kutta scheme</param>
        /// <param name="bdfOrder">if <paramref name="Scheme"/> denotes a BDF-scheme, its order</param>
        static public void DecodeScheme(TimeSteppingScheme Scheme, out RungeKuttaScheme rksch, out int bdfOrder) {
            rksch = null;
            bdfOrder = -1000;
            if(Scheme == TimeSteppingScheme.CrankNicolson)
                bdfOrder = -1;
            else if(Scheme == TimeSteppingScheme.ExplicitEuler)
                bdfOrder = 0;
            else if(Scheme == TimeSteppingScheme.ImplicitEuler)
                bdfOrder = 1;
            else if(Scheme.ToString().StartsWith("BDF"))
                bdfOrder = Convert.ToInt32(Scheme.ToString().Substring(3));
            else if(Scheme == TimeSteppingScheme.RK1)
                rksch = RungeKuttaScheme.ExplicitEuler;
            else if(Scheme == TimeSteppingScheme.RK1u1)
                rksch = RungeKuttaScheme.ExplicitEuler2;
            else if(Scheme == TimeSteppingScheme.RK2)
                rksch = RungeKuttaScheme.Heun2;
            else if(Scheme == TimeSteppingScheme.RK3)
                rksch = RungeKuttaScheme.TVD3;
            else if(Scheme == TimeSteppingScheme.RK4)
                rksch = RungeKuttaScheme.RungeKutta1901;
            else if(Scheme == TimeSteppingScheme.RK_ImplicitEuler)
                rksch = RungeKuttaScheme.ImplicitEuler;
            else if(Scheme == TimeSteppingScheme.RK_CrankNic)
                rksch = RungeKuttaScheme.CrankNicolson;
            else if(Scheme == TimeSteppingScheme.RK_IMEX3)
                rksch = RungeKuttaScheme.IMEX3;
            else
                throw new NotImplementedException();
        }

        /// <summary>
        /// Constructor for conventional (single-phase, non-X) DG
        /// </summary>
        public XdgTimestepping(
            SpatialOperator op,
            IEnumerable<DGField> Fields,
            IEnumerable<DGField> __Parameters,
            IEnumerable<DGField> IterationResiduals,
            TimeSteppingScheme __Scheme,
            MultigridOperator.ChangeOfBasisConfig[][] _MultigridOperatorConfig,
            AggregationGridData[] _MultigridSequence,
            LinearSolverConfig LinearSolver, NonLinearSolverConfig NonLinearSolver) //
        {
            this.Scheme = __Scheme;
            this.DgOperator = op;

            if(__Parameters == null)
                this.Parameters = new DGField[0];
            else
                this.Parameters = __Parameters.ToArray();

            CreateDummyTracker(Fields);
                       
            ConstructorCommon(op, false,
                Fields, this.Parameters, IterationResiduals,
                myDelComputeDgOperatorMatrix,
                UpdateLevelsetWithNothing,
                LevelSetHandling.None,
                _MultigridOperatorConfig,
                _MultigridSequence,
                0.0,
                LinearSolver, NonLinearSolver);
        }

        private void CreateDummyTracker(IEnumerable<DGField> Fields) {
            var gDat = Fields.First().GridDat as GridData;
            var DummyLevSet = new LevelSet(new Basis(gDat, 1), "DummyPhi");
            DummyLevSet.AccConstant(-1.0);
            LsTrk = new LevelSetTracker(gDat, XQuadFactoryHelper.MomentFittingVariants.Classic, 1, new[] { "A", "B" }, DummyLevSet);
        }

        double UpdateLevelsetWithNothing(DGField[] CurrentState, double time, double dt, double UnderRelax, bool incremental) {
            this.LsTrk.UpdateTracker(incremental: true);
            return 0.0;
        }

        DGField[] JacobiParameterVars = null;

        void myDelComputeXOperatorMatrix(BlockMsrMatrix OpMtx, double[] OpAffine, UnsetteledCoordinateMapping Mapping, DGField[] CurrentState, Dictionary<SpeciesId, MultidimensionalArray> AgglomeratedCellLengthScales, double time) {
            // compute operator
            Debug.Assert(OpAffine.L2Norm() == 0.0);

            if(OpMtx != null) {
                // +++++++++++++++++++++++++++++
                // Solver requires linearization
                // +++++++++++++++++++++++++++++

                Debug.Assert(OpMtx.InfNorm() == 0.0);
                switch(XdgOperator.LinearizationHint) {

                    case LinearizationHint.AdHoc: {
                        if(this.XdgOperator.ParameterUpdate != null) {
                            this.XdgOperator.ParameterUpdate.PerformUpdate(CurrentState, this.Parameters);
                        }

                        var mtxBuilder = XdgOperator.GetMatrixBuilder(LsTrk, Mapping, this.Parameters, Mapping);
                        mtxBuilder.time = time;
                        mtxBuilder.MPITtransceive = true;
                        mtxBuilder.ComputeMatrix(OpMtx, OpAffine);
                        return;
                    }

                    case LinearizationHint.FDJacobi: {
                        var mtxBuilder = XdgOperator.GetFDJacobianBuilder(LsTrk, CurrentState, this.Parameters, Mapping);
                        mtxBuilder.time = time;
                        mtxBuilder.MPITtransceive = true;
                        mtxBuilder.ComputeMatrix(OpMtx, OpAffine);
                        return;
                    }

                    case LinearizationHint.GetJacobiOperator: {
                        var op = GetJacobiXdgOperator();

                        if(JacobiParameterVars == null)
                            JacobiParameterVars = op.ParameterUpdate.AllocateParameters(this.CurrentState.Fields, this.Parameters);

                        op.ParameterUpdate.PerformUpdate(this.CurrentState.Fields, JacobiParameterVars);

                        var mtxBuilder = op.GetMatrixBuilder(LsTrk, Mapping, this.JacobiParameterVars, Mapping);
                        mtxBuilder.time = time;
                        mtxBuilder.MPITtransceive = true;
                        mtxBuilder.ComputeMatrix(OpMtx, OpAffine);
                        return;
                    }
                }
            } else {
                // ++++++++++++++++++++++++
                // only operator evaluation
                // ++++++++++++++++++++++++

                if(this.XdgOperator.ParameterUpdate != null) {
                    this.XdgOperator.ParameterUpdate.PerformUpdate(CurrentState, this.Parameters);
                }

                var eval = XdgOperator.GetEvaluatorEx(CurrentState, this.Parameters, Mapping);
                eval.time = time;
                eval.MPITtransceive = true;
                eval.Evaluate(1.0, 0.0, OpAffine);
            }

        }

        void myDelComputeDgOperatorMatrix(BlockMsrMatrix OpMtx, double[] OpAffine, UnsetteledCoordinateMapping Mapping, DGField[] CurrentState, Dictionary<SpeciesId, MultidimensionalArray> AgglomeratedCellLengthScales, double time) {
            // compute operator
            Debug.Assert(OpAffine.L2Norm() == 0.0);

            if(OpMtx != null) {
                // +++++++++++++++++++++++++++++
                // Solver requires linearization
                // +++++++++++++++++++++++++++++

                Debug.Assert(OpMtx.InfNorm() == 0.0);
                switch(DgOperator.LinearizationHint) {

                    case LinearizationHint.AdHoc: {
                        if(this.DgOperator.ParameterUpdate != null) {
                            this.DgOperator.ParameterUpdate.PerformUpdate(CurrentState, this.Parameters);
                        }

                        var mtxBuilder = DgOperator.GetMatrixBuilder(Mapping, this.Parameters, Mapping);
                        mtxBuilder.time = time;
                        mtxBuilder.MPITtransceive = true;
                        mtxBuilder.ComputeMatrix(OpMtx, OpAffine);
                        return;
                    }

                    case LinearizationHint.FDJacobi: {
                        var mtxBuilder = DgOperator.GetFDJacobianBuilder_(CurrentState, this.Parameters, Mapping,
                            DgOperator.ParameterUpdate != null ? DgOperator.ParameterUpdate.PerformUpdate : default(DelPartialParameterUpdate));
                        mtxBuilder.time = time;
                        mtxBuilder.MPITtransceive = true;
                        mtxBuilder.ComputeMatrix(OpMtx, OpAffine);
                        return;
                    }

                    case LinearizationHint.GetJacobiOperator: {
                        var op = GetJacobiDgOperator();

                        if(JacobiParameterVars == null)
                            JacobiParameterVars = op.ParameterUpdate.AllocateParameters(this.CurrentState.Fields, this.Parameters);
                        op.ParameterUpdate.PerformUpdate(this.CurrentState.Fields, JacobiParameterVars);

                        var mtxBuilder = op.GetMatrixBuilder(Mapping, this.JacobiParameterVars, Mapping);
                        mtxBuilder.time = time;
                        mtxBuilder.MPITtransceive = true;
                        mtxBuilder.ComputeMatrix(OpMtx, OpAffine);
                        return;
                    }
                }
            } else {
                // ++++++++++++++++++++++++
                // only operator evaluation
                // ++++++++++++++++++++++++

                if(this.DgOperator.ParameterUpdate != null) {
                    this.DgOperator.ParameterUpdate.PerformUpdate(CurrentState, this.Parameters);
                }

                var eval = DgOperator.GetEvaluatorEx(CurrentState, this.Parameters, Mapping);
                eval.time = time;
                eval.MPITtransceive = true;
                eval.Evaluate(1.0, 0.0, OpAffine);
            }
        }



        XdgTimesteppingBase TimesteppingBase {
            get {
                Debug.Assert((m_BDF_Timestepper == null) != (m_RK_Timestepper == null));
                if(m_BDF_Timestepper != null)
                    return m_BDF_Timestepper;
                if(m_RK_Timestepper != null)
                    return m_RK_Timestepper;
                throw new ApplicationException("internal error");
            }
        }


        public XdgBDFTimestepping m_BDF_Timestepper;

        public XdgRKTimestepping m_RK_Timestepper;


        /// <summary>
        /// Add logging of iteration residuals.
        /// </summary>
        public void RegisterResidualLogger(ResidualLogger _ResLogger) {
            TimesteppingBase.m_ResidualNames = this.IterationResiduals.Fields.Select(f => f.Identification).ToArray();
            TimesteppingBase.m_ResLogger = _ResLogger;
        }


        /// <summary>
        /// driver for solver calls
        /// </summary>
        public void Solve(double phystime, double dt) {
            if((m_BDF_Timestepper == null) == (m_RK_Timestepper == null))
                throw new ApplicationException();

            if(m_BDF_Timestepper != null) {
                m_BDF_Timestepper.Solve(phystime, dt);
            } else {
                m_RK_Timestepper.Solve(phystime, dt);
            }

            JacobiParameterVars = null; 
        }

        /// <summary>
        /// Step 2 of 2 for dynamic load balancing: restore this objects 
        /// status after the grid has been re-distributed.
        /// </summary>
        public void DataRestoreAfterBalancing(GridUpdateDataVaultBase L,
            IEnumerable<DGField> Fields,
            IEnumerable<DGField> Params,
            IEnumerable<DGField> IterationResiduals,
            LevelSetTracker LsTrk,
            AggregationGridData[] _MultigridSequence) //
        {
            if(LsTrk != null) {
                this.LsTrk = LsTrk;
            } else {
                CreateDummyTracker(Fields);
            }

            
            if(Params != null) {
                Parameters = Params.ToArray();
            } else {
                Parameters = new DGField[0];
            }
            
            if(m_BDF_Timestepper != null) {
                m_BDF_Timestepper.DataRestoreAfterBalancing(L, Fields, IterationResiduals, LsTrk, _MultigridSequence);
            } else if(m_RK_Timestepper != null) {
                throw new NotImplementedException("Load balancing and adaptive mesh refinement are not supported for Runge-Kutta XDG timestepping.");
            } else {
                throw new NotImplementedException();
            }


        }

        /// <summary>
        /// Step 1 of 2 for dynamic load balancing: creating a backup of this objects 
        /// status in the load-balancing thing <paramref name="L"/>
        /// </summary>
        public void DataBackupBeforeBalancing(GridUpdateDataVaultBase L) {
            
            if(m_BDF_Timestepper != null) {
                m_BDF_Timestepper.DataBackupBeforeBalancing(L);
            } else if(m_RK_Timestepper != null) {
                throw new NotImplementedException("Load balancing and adaptive mesh refinement are not supported for Runge-Kutta XDG timestepping.");
            } else {
                throw new NotImplementedException();
            }
                        
            this.LsTrk = null;
            this.Parameters = null;
        }

        
        // <summary>
        /// Number of timesteps required for restart, e.g. 1 for Runge-Kutta and implicit/explicit Euler, 2 for BDF2, etc.
        /// </summary>
        public int BurstSave {
            get {
                if(m_RK_Timestepper != null) {
                    Debug.Assert(m_BDF_Timestepper == null);
                    return 1;
                } else if(m_BDF_Timestepper != null) {
                    return m_BDF_Timestepper.GetNumberOfStages;
                } else {
                    throw new NotImplementedException();
                }

            }
        }


    }
}
