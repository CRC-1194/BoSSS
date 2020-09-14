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
using ilPSP.LinSolvers;
using BoSSS.Foundation.XDG;
using BoSSS.Platform;
using BoSSS.Platform.Utils;
using ilPSP;
using ilPSP.Utils;
using System.Diagnostics;

namespace BoSSS.Solution.AdvancedSolvers {

    /// <summary>
    /// Evaluation or linearization/matrix assembly of the operator
    /// </summary>
    /// <param name="Matrix"></param>
    /// <param name="Affine"></param>
    /// <param name="Linearization">
    /// - false: operator evaluation
    /// - true: linearization
    /// </param>
    /// <param name="MassMatrix"></param>
    /// <param name="CurrentState">
    /// Current state of the solution
    /// </param>
    /// <param name="OberFrickelHack">
    /// the original operator that somehow produced the matrix; yes, this API is convoluted piece-of-shit
    /// </param>
    public delegate void OperatorEvalOrLin(out BlockMsrMatrix Matrix, out double[] Affine, out BlockMsrMatrix MassMatrix, DGField[] CurrentState, bool Linearization, out ISpatialOperator OberFrickelHack);
              

    /// <summary>
    /// base-class for non-linear solvers
    /// </summary>
    public abstract class NonlinearSolver {

        /// <summary>
        /// ctor
        /// </summary>
        public NonlinearSolver(OperatorEvalOrLin __AssembleMatrix, IEnumerable<AggregationGridBasis[]> __AggBasisSeq, MultigridOperator.ChangeOfBasisConfig[][] __MultigridOperatorConfig) {
            m_AssembleMatrix = __AssembleMatrix;
            m_AggBasisSeq = __AggBasisSeq.ToArray();
            m_MultigridOperatorConfig = __MultigridOperatorConfig;
        }

        /// <summary>
        /// Evaluation and linearization of PDE to solve
        /// </summary>
        protected OperatorEvalOrLin m_AssembleMatrix;
        
        /// <summary>
        /// Multigrid basis
        /// - 1st index: Multigrid level
        /// - 2nd index: variable index
        /// </summary>
        protected AggregationGridBasis[][] m_AggBasisSeq;
                
        /// <summary>
        /// required for construction of <see cref="CurrentLin"/>
        /// </summary>
        protected MultigridOperator.ChangeOfBasisConfig[][] m_MultigridOperatorConfig;

        /// <summary>
        /// Called at every iteration; the arguments are 
        ///  - iteration index 
        ///  - current solution 
        ///  - current residual 
        ///  - current multigrid operator
        /// </summary>
        public event Action<int, double[], double[], MultigridOperator> IterationCallback;

        /// <summary>
        /// Triggers <see cref="IterationCallback"/>.
        /// </summary>
        protected void OnIterationCallback(int iterIndex, double[] currentSol, double[] currentRes, MultigridOperator Mgop) {
            if (IterationCallback != null)
                IterationCallback(iterIndex, currentSol, currentRes, Mgop);
        }

        

        /// <summary>
        /// Helper routine for the initial phase of <see cref="SolverDriver{S}"/>
        /// </summary>
        /// <param name="X">initial guess</param>
        /// <param name="RHS"></param>
        /// <param name="Sol1">
        /// The initial solution, transformed to the aggregation multigrid basis, see <see cref="m_AggBasisSeq"/>.
        /// </param>
        /// <param name="Res1">
        /// The residual of the initial solution
        /// </param>
        protected void Init<S>(CoordinateVector X, S RHS, out double[] Sol1, out double[] Res1)
            where S : IList<double> //
        {
            this.ProblemMapping = X.Mapping;

            int Lraw = X.Mapping.LocalLength;  // length of Solution/RHS in original space
            if(RHS != null) {
                if(RHS.Count != Lraw)
                    throw new ArgumentException();
                this.RHSRaw = RHS.ToArray();
            } else {
                this.RHSRaw = null;
            }
            this.UpdateLinearization(X.Mapping.Fields);
            
            int Ltrf = this.CurrentLin.Mapping.LocalLength;


            // set initial guess (input) as first approximation to the solution ...
            Sol1 = new double[Ltrf];
            this.CurrentLin.TransformSolInto(X, Sol1);

            // ... and evaluate its residual
            Res1 = new double[Ltrf];
            this.EvalLinearizedResidual(Sol1, ref Res1);
        }


        /// <summary>
        /// Template for implementation of the solver routine.
        /// </summary>
        /// <typeparam name="S"></typeparam>
        /// <param name="X">
        /// On entry, an initial guess to the linear system.
        /// On exit, hopefully the solution to the nonlinear system.
        /// </param>
        /// <param name="RHS">
        /// If not equal null, must be passed to <see cref="RHSRaw"/>.
        /// </param>
        abstract public void SolverDriver<S>(CoordinateVector X, S RHS)
           where S : IList<double>;


        /// <summary>
        /// Preconditioner/solver for the linearized problem
        /// </summary>
        public ISolverSmootherTemplate Precond;

        /// <summary>
        /// Current linearization of the nonlinear operator: the linearized
        /// system is given as 
        /// <see cref="CurrentLin"/>*X = <see cref="LinearizationRHS"/>.
        /// </summary>
        protected MultigridOperator CurrentLin;

        /// <summary>
        /// Optional RHS to the nonlinear system, 
        /// </summary>
        protected double[] RHSRaw;

        /// <summary>
        /// Current affine part of the nonlinear operator.
        /// </summary>
        protected double[] LinearizationRHS;

        /// <summary>
        /// Evaluation of the nonlinear operator.
        /// </summary>
        /// <param name="alpha"></param>
        /// <param name="CurrentState">
        /// Current state of DG fields
        /// </param>
        /// <param name="beta">
        /// Pre-scaling of <paramref name="Output"/>.
        /// </param>
        /// <param name="Output"></param>
        protected void EvaluateOperator(double alpha, IEnumerable<DGField> CurrentState, double[] Output) {
            double[] OpAffineRaw;
            this.m_AssembleMatrix(out BlockMsrMatrix OpMtxRaw, out OpAffineRaw, out BlockMsrMatrix MassMtxRaw, CurrentState.ToArray(), false, out var Dummy);
            Debug.Assert(OpMtxRaw == null); // only evaluation ==> OpMatrix must be null

            CurrentLin.TransformRhsInto(OpAffineRaw, Output, false);

        }



        /// <summary>
        /// Updating the <see cref="CurrentLin"/> -- operator;
        /// </summary>
        /// <param name="CurrentState">input, linearization point</param>
        /// <param name="U0">output, linearization point, after external update, transorfmed back</param>
        protected void Update(IEnumerable<DGField> CurrentState, ref double[] U0) {
            /*
            DGField[] U0fields = this.ProblemMapping.BasisS.Select(
                delegate(Basis b) {
                    DGField ret;
                    if (b is XDGBasis) {
                        XDGField xf = new XDGField(b as XDGBasis);
                        xf.UpdateBehaviour = BehaveUnder_LevSetMoovement.AutoExtrapolate;
                        ret = xf;
                    } else {
                        ret = new SinglePhaseField(b);
                    }
                    return ret;
                }).ToArray();

            CoordinateVector u0Raw = new CoordinateVector(U0fields);

            CurrentLin.TransformSolFrom(u0Raw, U0);
            */
           
            this.UpdateLinearization(CurrentState);

            CoordinateVector u0Raw = new CoordinateVector(CurrentState.ToArray());
            int Ltrf = this.CurrentLin.Mapping.LocalLength;
            if (U0 == null || U0.Length != Ltrf)
                U0 = new double[Ltrf];
            CurrentLin.TransformSolInto(u0Raw, U0);
        }


        /// <summary>
        /// Updating the <see cref="CurrentLin"/> -- operator;
        /// </summary>
        /// <param name="CurrentState">linearization point</param>
        protected void UpdateLinearization(IEnumerable<DGField> CurrentState) {
            if(!(this.ProblemMapping.BasisS.Count == CurrentState.Count()))
                throw new ArgumentException("mismatch in number of fields.");

            BlockMsrMatrix OpMtxRaw, MassMtxRaw;
            double[] OpAffineRaw;
            this.m_AssembleMatrix(out OpMtxRaw, out OpAffineRaw, out MassMtxRaw, CurrentState.ToArray(), true, out ISpatialOperator abstractOperator);

            CurrentLin = new MultigridOperator(this.m_AggBasisSeq, this.ProblemMapping,
                OpMtxRaw.CloneAs(), MassMtxRaw,
                this.m_MultigridOperatorConfig,
                abstractOperator.DomainVar.Select(varName => abstractOperator.FreeMeanValue[varName]).ToArray()); 

            OpAffineRaw = OpAffineRaw.CloneAs();
            if (this.RHSRaw != null)
                OpAffineRaw.AccV(-1.0, this.RHSRaw);
            if(LinearizationRHS == null || LinearizationRHS.Length != this.CurrentLin.Mapping.LocalLength)
                LinearizationRHS = new double[this.CurrentLin.Mapping.LocalLength];
            else
                LinearizationRHS.ClearEntries();
            CurrentLin.TransformRhsInto(OpAffineRaw, this.LinearizationRHS, true);
            this.LinearizationRHS.ScaleV(-1.0);

            //this.CurrentLin.OperatorMatrix.SaveToTextFileSparse("PcMatrix-" + counter + ".txt");
            //counter++;
        }

        //static int counter = 1;


        protected UnsetteledCoordinateMapping ProblemMapping {
            get;
            set;
        }

        /// <summary>
        /// Residual of linearized system, i.e.
        /// <paramref name="out_Resi"/> := RHS - M*<paramref name="in_U"/>
        /// </summary>
        protected void EvalLinearizedResidual(double[] in_U, ref double[] out_Resi) {
            if (this.LinearizationRHS.Length != in_U.Length)
                throw new ApplicationException("internal error");
            if (out_Resi.Length != in_U.Length)
                out_Resi = new double[in_U.Length];
            out_Resi.SetV(this.LinearizationRHS, 1.0);
            CurrentLin.OperatorMatrix.SpMV(-1.0, in_U, 1.0, out_Resi);
        }



    }
}
