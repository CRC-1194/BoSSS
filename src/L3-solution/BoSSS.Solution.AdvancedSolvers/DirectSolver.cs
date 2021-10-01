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

using ilPSP;
using System;
using System.Collections.Generic;
using System.Linq;
using ilPSP.LinSolvers;
using ilPSP.LinSolvers.PARDISO;
using ilPSP.LinSolvers.MUMPS;
using ilPSP.Utils;
using MPI.Wrappers;
using BoSSS.Platform;
using System.IO;
using ilPSP.Tracing;
using ilPSP.Connectors.Matlab;
using System.Diagnostics;
using ilPSP.LinSolvers.monkey;
using BoSSS.Solution.Control;

namespace BoSSS.Solution.AdvancedSolvers {

    /// <summary>
    /// Sparse direct solver. 
    /// This class is a wrapper around either 
    /// - PARDISO (<see cref="PARDISOSolver"/>) or 
    /// - MUMPS (<see cref="MUMPSSolver"/>) or
    /// - LAPACK.
    /// </summary>
    public class DirectSolver : ISolverSmootherTemplate, ISolverWithCallback {


        /// <summary>
        /// Set the type of Parallelism to be used for the linear Solver.
        /// You may define a comma separated list out of the following: "SEQ","MPI","OMP"
        /// </summary>
        public Parallelism SolverVersion = Parallelism.SEQ;

        /// <summary>
        /// Switch between PARDISO and MUMPS.
        /// </summary>
        public _whichSolver WhichSolver = _whichSolver.PARDISO;

       
        /// <summary>
        /// 
        /// </summary>
        public enum _whichSolver {

            /// <summary>
            /// Using <see cref="PARDISOSolver"/>.
            /// </summary>
            PARDISO,

            /// <summary>
            /// Using <see cref="MUMPSSolver"/>.
            /// </summary>
            MUMPS,

            /// <summary>
            /// Conversion to dense matrix, solution 
            /// via LU-decomposition from LAPACK, see also <see cref="IMatrixExtensions.Solve{T}(T, double[], double[])"/>.
            /// Only suitable for small systems (less than 10000 DOF).
            /// </summary>
            Lapack,

            /// <summary>
            /// MATLAB 'backslash' solver, see <see cref="ilPSP.Connectors.Matlab.Extensions.SolveMATLAB{T1, T2}(IMutableMatrixEx, T1, T2, string)"/> 
            /// </summary>
            Matlab
        }





        public void Init(MultigridOperator op) {
            using(var tr = new FuncTrace()) {
                var Mtx = op.OperatorMatrix;
                var MgMap = op.Mapping;
                m_MultigridOp = op;

                if(!Mtx.RowPartitioning.EqualsPartition(MgMap.Partitioning))
                    throw new ArgumentException("Row partitioning mismatch.");
                if(!Mtx.ColPartition.EqualsPartition(MgMap.Partitioning))
                    throw new ArgumentException("Column partitioning mismatch.");

                Mtx.CheckForNanOrInfM(typeof(DirectSolver) + ", matrix definition: ");

                m_Mtx = Mtx;
            }
        }

        MultigridOperator m_MultigridOp;

        class MatlabSolverWrapper : ISparseSolver {

            MsrMatrix Mtx;

            public void DefineMatrix(IMutableMatrixEx M) {
                Mtx = M.ToMsrMatrix();
            }

            public void Dispose() {
                Mtx = null;
            }

            public SolverResult Solve<Tunknowns, Trhs>(Tunknowns x, Trhs rhs)
                where Tunknowns : IList<double>
                where Trhs : IList<double> {
                var StartTime = DateTime.Now;

                Mtx.SolveMATLAB(x, rhs);

                return new SolverResult() {
                    Converged = true,
                    NoOfIterations = 1,
                    RunTime = DateTime.Now - StartTime
                };
            }
        }

        class DenseSolverWrapper : ISparseSolver {

            MultidimensionalArray FullMatrix;

            public void DefineMatrix(IMutableMatrixEx M) {
                FullMatrix = M.ToFullMatrixOnProc0();
            }

            public void Dispose() {
                FullMatrix = null;
            }

            public SolverResult Solve<Tunknowns, Trhs>(Tunknowns x, Trhs rhs)
                where Tunknowns : IList<double>
                where Trhs : IList<double> {
                var StartTime = DateTime.Now;

                double[] Int_x = x as double[];
                bool writeBack = false;
                if(Int_x == null) {
                    Int_x = new double[x.Count];
                    writeBack = true;
                }

                double[] Int_rhs = rhs as double[];
                if(Int_rhs == null)
                    Int_rhs = rhs.ToArray();

                FullMatrix.Solve(Int_x, Int_rhs);

                if(writeBack)
                    x.SetV(Int_x);

                return new SolverResult() {
                    Converged = true,
                    NoOfIterations = 1,
                    RunTime = DateTime.Now - StartTime
                };
            }
        }



        ISparseSolver GetSolver(IMutableMatrixEx Mtx) {
            ISparseSolver solver;
            
            switch(WhichSolver) {
                case _whichSolver.PARDISO:
                solver = new PARDISOSolver() {
                    CacheFactorization = true,
                    UseDoublePrecision = true,
                    Parallelism = this.SolverVersion
                };
                break;

                case _whichSolver.MUMPS:
                solver = new MUMPSSolver() {
                    Parallelism = this.SolverVersion
                };
                break;

                case _whichSolver.Matlab:
                solver = new MatlabSolverWrapper();
                break;

                case _whichSolver.Lapack:
                solver = new DenseSolverWrapper();
                break;

                default:
                throw new NotImplementedException();

            }

            solver.DefineMatrix(Mtx);

            return solver;
        }

        BlockMsrMatrix m_Mtx;
        int IterCnt = 1;


        long m_UsedMemoryInLastCall = 0; 

        /// <summary>
        /// %
        /// </summary>
        public void Solve<U, V>(U X, V B)
            where U : IList<double>
            where V : IList<double> //
        {
            using(var tr = new FuncTrace()) {
                B.CheckForNanOrInfV(true, true, true, typeof(DirectSolver).Name + ", RHS on entry: ");
                
                double[] Residual = this.TestSolution ? B.ToArray() : null;

                string SolverName = "NotSet";
                using(var solver = GetSolver(m_Mtx)) {
                    Converged = false;
                    SolverName = solver.GetType().FullName;
                    //Console.Write("Direct solver run {0}, using {1} ... ", IterCnt, solver.GetType().Name);
                    IterCnt++;
                    solver.Solve(X, B);
                    //Console.WriteLine("done.");

                    if(solver is PARDISOSolver pslv) {
                        m_UsedMemoryInLastCall = pslv.UsedMemory();
                    }
                    Converged = true;
                }
                X.CheckForNanOrInfV(true, true, true, typeof(DirectSolver).Name + ", solution after solver call: ");


                m_ThisLevelIterations++;

                if(Residual != null) {
                    //Console.Write("Checking residual (run {0}) ... ", IterCnt - 1);
                    double RhsNorm = Residual.L2NormPow2().MPISum().Sqrt();
                    double MatrixInfNorm = m_Mtx.InfNorm();
                    m_Mtx.SpMV(-1.0, X, 1.0, Residual);

                    double ResidualNorm = Residual.L2NormPow2().MPISum().Sqrt();
                    double SolutionNorm = X.L2NormPow2().MPISum().Sqrt();
                    double Denom = Math.Max(MatrixInfNorm, Math.Max(RhsNorm, Math.Max(SolutionNorm, Math.Sqrt(BLAS.MachineEps))));
                    double RelResidualNorm = ResidualNorm / Denom;

                    //Console.WriteLine("done: Abs.: {0}, Rel.: {1}", ResidualNorm, RelResidualNorm);

                    if(RelResidualNorm > 1.0e-10) {

                        //Console.WriteLine("High residual from direct solver: abs {0}, rel {1}", ResidualNorm , ResidualNorm / SolutionNorm);
#if TEST
                        m_Mtx.SaveToTextFileSparse("Mtx.txt");
                        X.SaveToTextFile("X.txt");
                        B.SaveToTextFile("B.txt");
#endif

                        string ErrMsg;
                        using(var stw = new StringWriter()) {
                            stw.WriteLine("High residual from direct solver (using {0}).", SolverName);
                            stw.WriteLine("    L2 Norm of RHS:         " + RhsNorm);
                            stw.WriteLine("    L2 Norm of Solution:    " + SolutionNorm);
                            stw.WriteLine("    L2 Norm of Residual:    " + ResidualNorm);
                            stw.WriteLine("    Relative Residual norm: " + RelResidualNorm);
                            stw.WriteLine("    Matrix Inf norm:        " + MatrixInfNorm);
#if TEST
                            stw.WriteLine("Dumping text versions of Matrix, Solution and RHS.");
#endif
                            ErrMsg = stw.ToString();
                        }
                        Console.Error.WriteLine(ErrMsg);
                        this.Converged = false;
                    }
                }

                if(this.IterationCallback != null) {
                    double[] _xl = X.ToArray();
                    double[] _bl = B.ToArray();
                    m_Mtx.SpMV(-1.0, _xl, 1.0, _bl);
                    this.IterationCallback(1, _xl, _bl, this.m_MultigridOp);
                }
            }
        }

        int m_ThisLevelIterations;

        bool m_TestSolution = true;

        /// <summary>
        /// If set to true, the solution returned by the direct solver is tested by computing the residual norm.
        /// Currently, the default is true, since the direct solvers seem unreliable.
        /// </summary>
        public bool TestSolution {
            get {
                return m_TestSolution;
            }
            set {
                m_TestSolution = value;
            }
        }

        public int IterationsInNested {
            get { return 0; }
        }

        public int ThisLevelIterations {
            get { return m_ThisLevelIterations; }
        }

        public bool Converged {
            get;
            private set;
        }

        public Action<int, double[], double[], MultigridOperator> IterationCallback {
            get;
            set;
        }

        public void ResetStat() {
            m_ThisLevelIterations = 0;
        }

        public object Clone() {
            throw new NotImplementedException("Clone of " + this.ToString() + " TODO");
        }

        /// <summary>
        /// Release internal memory
        /// </summary>
        public void Dispose() {
            this.m_Mtx = null;
        }

        public long UsedMemory() {
            return m_UsedMemoryInLastCall;
        }

    }



}
