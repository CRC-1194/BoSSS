﻿using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using ilPSP;
using ilPSP.LinSolvers;
using ilPSP.LinSolvers.PARDISO;
using ilPSP.Tracing;
using ilPSP.Utils;
using MPI.Wrappers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoSSS.Solution.AdvancedSolvers {

    /// <summary>
    /// p-Multigrid on a single grid level
    /// </summary>
    public class LevelPmg : ISolverSmootherTemplate, ISolverWithCallback {

        public bool UseDiagonalPmg = true;

        public bool EqualOrder = false;

        /// <summary>
        /// ctor
        /// </summary>
        public LevelPmg() {
            UseHiOrderSmoothing = true;
        }

        /// <summary>
        /// always 0, because there is no nested solver
        /// </summary>
        public int IterationsInNested {
            get {
                return 0;
            }
        }

        /// <summary>
        /// ~
        /// </summary>
        public int ThisLevelIterations {
            get;
            private set;
        }

        /// <summary>
        /// ~
        /// </summary>
        public bool Converged {
            get;
            private set;
        }

        /// <summary>
        /// If true, cell-local solvers will be used to approximate a solution to high-order modes
        /// </summary>
        public bool UseHiOrderSmoothing {
            get;
            set;
        }

        public object Clone() {
            throw new NotImplementedException();
        }

        private MultigridOperator m_op;

        /// <summary>
        /// - 1st index: cell
        /// </summary>
        MultidimensionalArray[] HighOrderBlocks_LU;
        int[][] HighOrderBlocks_LUpivots;

        public int OrderOfCoarseSystem {
            get { return m_LowOrder; }
            set { m_LowOrder = value; }
        }


        /// <summary>
        /// DG degree at low order blocks. This degree is the border, which divides into low order and high order blocks
        /// </summary>
        private int m_LowOrder = 1;

        /// <summary>
        /// If true blocks/cells containing more than one species are completely assigned to low order block solver.
        /// This hopefully is better than the default approach
        /// </summary>
        public bool FullSolveOfCutcells {
            get;
            set;
        }

        private bool AnyHighOrderTerms {
            get {
                Debug.Assert(m_op != null, "there is no matrix given yet!");
                return m_op.Mapping.DgDegree.Any(p => p > m_LowOrder);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Init(MultigridOperator op) {

            //            //System.Threading.Thread.Sleep(10000);
            //            //ilPSP.Environment.StdoutOnlyOnRank0 = false;
            m_op = op;

            if (m_LowOrder > m_op.Mapping.DgDegree.Max())
                throw new ArgumentOutOfRangeException("CoarseLowOrder is higher than maximal DG degree");

#if TEST
            var debugerSW = new StreamWriter(String.Concat("debug_of_", ilPSP.Environment.MPIEnv.MPI_Rank));
            Console.WriteLine("variable TEST is defined");
            //debugerSW.WriteLine("proc {0} reporting Num of Blocks {1}", ilPSP.Environment.MPIEnv.MPI_Rank, HighOrderBlocks_LUpivots.Length);
#endif

            int D = this.m_op.GridData.SpatialDimension;


            var DGlowSelect = new SubBlockSelector(op.Mapping);
            Func<int, int, int, int, bool> lowFilter = (int iCell, int iVar, int iSpec, int pDeg) => pDeg <= (iVar != D && !EqualOrder ? OrderOfCoarseSystem : OrderOfCoarseSystem - 1); // containd the pressure hack
            DGlowSelect.ModeSelector(lowFilter);

            if (FullSolveOfCutcells)
                ModifyLowSelector(DGlowSelect, op);

            lMask = new BlockMask(DGlowSelect);
            int m_lowMaskLen = lMask.NoOfMaskedRows;

            if (UseHiOrderSmoothing && AnyHighOrderTerms) {
                var DGhighSelect = new SubBlockSelector(op.Mapping);
                Func<int, int, int, int, bool> highFilter = (int iCell, int iVar, int iSpec, int pDeg) => pDeg > (iVar != D && !EqualOrder ? OrderOfCoarseSystem : OrderOfCoarseSystem - 1);
                //Func<int, int, int, int, bool> highFilter = (int iCell, int iVar, int iSpec, int pDeg) => pDeg >= 0;
                DGhighSelect.ModeSelector(highFilter);

                if (FullSolveOfCutcells)
                    ModifyHighSelector(DGhighSelect, op);

                hMask = new BlockMask(DGhighSelect);
                int m_highMaskLen = hMask.NoOfMaskedRows;

                BlockMsrMatrix P01HiMatrix = null;

                if (UseDiagonalPmg) {
                    HighOrderBlocks_LU = hMask.GetDiagonalBlocks(op.OperatorMatrix, false, false);
                    int NoOfBlocks = HighOrderBlocks_LU.Length;
                    HighOrderBlocks_LUpivots = new int[NoOfBlocks][];

                    for (int jLoc = 0; jLoc < NoOfBlocks; jLoc++) {
                        int len = HighOrderBlocks_LU[jLoc].NoOfRows;
                        HighOrderBlocks_LUpivots[jLoc] = new int[len];
                        HighOrderBlocks_LU[jLoc].FactorizeLU(HighOrderBlocks_LUpivots[jLoc]);
                    }
                } else {
                    P01HiMatrix = hMask.GetSubBlockMatrix(op.OperatorMatrix, csMPI.Raw._COMM.SELF);

                    hiSolver = new PARDISOSolver() {
                        CacheFactorization = true,
                        UseDoublePrecision = true, // keep it true, experiments showed, that this leads to fewer iterations
                        Parallelism = Parallelism.OMP
                    };
                    hiSolver.DefineMatrix(P01HiMatrix);
                }
            }

            var P01SubMatrix = lMask.GetSubBlockMatrix(op.OperatorMatrix, csMPI.Raw._COMM.WORLD);

            lowSolver = new PARDISOSolver() {
                CacheFactorization = true,
                UseDoublePrecision = false, // no difference towards =true observed for XDGPoisson
                Parallelism = Parallelism.OMP
            };
            lowSolver.DefineMatrix(P01SubMatrix);


            Debug.Assert(UseDiagonalPmg && lowSolver != null);
            Debug.Assert(UseDiagonalPmg || (!UseDiagonalPmg && hiSolver != null));
            Debug.Assert(m_lowMaskLen > 0);
            //Debug.Assert(AnyHighOrderTerms && m_highMaskLen > 0);
#if TEST
            P01SubMatrix.SaveToTextFileSparseDebug("lowM");
            P01SubMatrix.SaveToTextFileSparse("lowM_full");
            if (!UseDiagonalPmg) {
                //P01HiMatrix.SaveToTextFileSparseDebug("hiM");
                //P01HiMatrix.SaveToTextFileSparse("hiM_full");
            }
            m_op.OperatorMatrix.SaveToTextFileSparseDebug("M");
            m_op.OperatorMatrix.SaveToTextFileSparse("M_full");
            debugerSW.Flush();
            debugerSW.Close();

            long[] bla = m_op.BaseGridProblemMapping.GridDat.CurrentGlobalIdPermutation.Values;
            bla.SaveToTextFileDebug("permutation_");

            List<int> BlockI0 = new List<int>();
            List<int> Block_N = new List<int>();
            foreach (long Block in bla) {
                BlockI0.Add(m_op.Mapping.GetBlockI0((int)Block));
                Block_N.Add(m_op.Mapping.GetBlockLen((int)Block));
            }
            BlockI0.SaveToTextFileDebug("BlockI0");
            Block_N.SaveToTextFileDebug("Block_N");
#endif
        }

        private void ModifyLowSelector(SubBlockSelector sbs, MultigridOperator op) {
            AssignXdgBlocksModification(sbs, op, true);
        }

        private void ModifyHighSelector(SubBlockSelector sbs, MultigridOperator op) {
            AssignXdgBlocksModification(sbs, op, false);
        }

        private void AssignXdgBlocksModification(SubBlockSelector sbs, MultigridOperator op, bool IsLowSelector) {
            var Filter = sbs.ModeFilter;
            //var Mask = (op.BaseGridProblemMapping.BasisS[0] as XDGBasis).Tracker.Regions.GetNearFieldMask(0);
            //var bMask = Mask.GetBitMask();
            //Console.WriteLine($"Fine solution in {Mask.NoOfItemsLocally} of {Mask.GridData.iLogicalCells.NoOfLocalUpdatedCells} cells.");

            Func<int, int, int, int, bool> Modification = delegate (int iCell, int iVar, int iSpec, int pDeg) {
                //if(bMask[iCell])
                //    return true;

                int NoOfSpec = op.Mapping.AggBasis[0].GetNoOfSpecies(iCell);
                if (NoOfSpec >= 2)
                    return IsLowSelector;
                else
                    return Filter(iCell, iVar, iSpec, pDeg);
            };
            sbs.ModeSelector(Modification);
        }

        /// <summary>
        /// Solver of low order system.
        /// The low order system is defined by <see cref="OrderOfCoarseSystem"/>
        /// </summary>
        private ISparseSolver lowSolver;

        /// <summary>
        /// experimental, used if <see cref="UseDiagonalPmg"/> is not set.
        /// Then low order and high order blocks are both solved by direct solver.
        /// </summary>
        private ISparseSolver hiSolver;

        int m_Iter = 0;
                

        private BlockMask hMask;
        private BlockMask lMask;

        /// <summary>
        /// ~
        /// </summary>
        public void ResetStat() {
            Converged = false;
            ThisLevelIterations = 0;
        }


        /// <summary>
        /// Computes the coarse-grid correction
        /// </summary>
        /// <param name="x_out">output: coarse level solution, prolongated to fine level</param>
        /// <param name="in_rhs">input: RHS on fine level</param>
        void CoarseSolve(double[] x_out, double[] in_rhs) {
            // project to low-p/coarse
            double[] rhs_c = lMask.GetSubVec(in_rhs);

            // low-p solve
            double[] x_c = new double[rhs_c.Length];
            lowSolver.Solve(x_c, rhs_c);

            // accumulate low-p correction
            lMask.AccSubVec(x_c, x_out);

            //// test: if we use an exact solution, we should terminate in one iteration!
            //double[] xtest = new double[in_rhs.Length];
            //m_op.OperatorMatrix.Solve_Direct(xtest, in_rhs);
            //x_out.AccV(1.0, xtest);

        }

        /// <summary>
        /// smoothing/solving on high level
        /// </summary>
        void FineSolve(double[] x_in_out, double[] in_rhs) {
            using(var tr = new FuncTrace()) {


                // compute residual
                double[] Res_f = in_rhs.CloneAs();
                this.m_op.OperatorMatrix.SpMV(-1.0, x_in_out, 1.0, Res_f);


                // test: if we use an exact solution, we should terminate in one iteration!
                /*
                double[] xtest = new double[in_rhs.Length];
                m_op.OperatorMatrix.Solve_Direct(xtest, Res_f);
                x_in_out.AccV(1.0, xtest);
                Res_f.ClearEntries();
                */
    

                if(UseHiOrderSmoothing && AnyHighOrderTerms) {
                    // solver high-order 

                    tr.Info("UseDiagonalPmg: " + UseDiagonalPmg);
                    if(UseDiagonalPmg) {
                        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
                        // solve the high-order blocks diagonally, i.e. use a DENSE direct solver LOCALLY IN EACH CELL
                        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

                        int J = HighOrderBlocks_LU.Length;

                        for(int j = 0; j < J; j++) { // loop over cells

                            if(HighOrderBlocks_LU[j] != null) {
                                int NpTotHi = HighOrderBlocks_LU[j].NoOfRows;
                                var x_hi = new double[NpTotHi];

                                double[] b_f = hMask.GetSubVecOfCell(Res_f, j);
                                Debug.Assert(b_f.Length == NpTotHi);
                                HighOrderBlocks_LU[j].BacksubsLU(HighOrderBlocks_LUpivots[j], x_hi, b_f);
                                hMask.AccSubVecOfCell(x_hi, j, x_in_out);
                            }

                        }
                    } else {
                        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
                        // solver the high-order system at once, using a SPARSE direct solver for all high-order modes
                        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

                        int Hc = (hMask?.NoOfMaskedRows) ?? 0;

                        if(Hc > 0) {
                            // project to low-p/coarse
                            double[] hi_Res_c = hMask.GetSubVec(Res_f);
                            Debug.Assert(hi_Res_c.Length == Hc);
                            double[] hi_Cor_c = new double[Hc];
                            hiSolver.Solve(hi_Cor_c, hi_Res_c);
                            hMask.AccSubVec(hi_Cor_c, x_in_out);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Algorithm 3 in:
        /// 
        /// p-Multigrid matrix-free discontinuous Galerkin solution strategies for the under-resolved simulation of incompressible turbulent flows
        /// M. Franciolini, L. Botti, A. Colombo, A. Crivellini
        /// </summary>
        double[] MGfull(int l, double[] gl, double[] wl) {
            if(l >= 1) {
                // 
                throw new Exception("should not happen");
            } else {
                double[] wl_hat = new double[gl.Length];
                CoarseSolve(wl_hat, gl.CloneAs());

                var dl = gl.CloneAs();
                m_op.OperatorMatrix.SpMV(-1.0, wl_hat, 1.0, dl);

                var el = MGv(l, dl, new double[gl.Length]);

                // wl_dash = wl_hat + el
                var wl_dash = el.CloneAs();
                wl_dash.AccV(1, wl_hat);

                return wl_dash;
            }
        }

        /// <summary>
        /// Algorithm 2 in:
        /// 
        /// p-Multigrid matrix-free discontinuous Galerkin solution strategies for the under-resolved simulation of incompressible turbulent flows
        /// M. Franciolini, L. Botti, A. Colombo, A. Crivellini
        /// </summary>
        double[] MGv(int l, double[] gl, double[] wl) {
            if(l >= 1) {
                throw new Exception("should not happen");
            } else {
                var wl_dash = wl.CloneAs();
                FineSolve(wl_dash, gl);

                var dl = gl.CloneAs();
                m_op.OperatorMatrix.SpMV(-1.0, wl_dash, 1.0, dl);

                double[] el = new double[wl.Length];
                CoarseSolve(el, dl);
                double[] wl_hat = wl_dash.CloneAs();
                wl_hat.AccV(1.0, el);


                FineSolve(wl_hat, gl);
                wl_dash = wl_hat;
                return wl_dash;
            }
        }


        /// <summary>
        /// ~
        /// </summary>
        public void Solve<U, V>(U X, V B)
            where U : IList<double>
            where V : IList<double> // 
        {
            using(var tr = new FuncTrace()) {
                tr.InfoToConsole = true;
                int Lf = m_op.Mapping.LocalLength; // DOF's in entire system
                //int Lc = this.lMask.NoOfMaskedRows; // DOF's in low-order system

                
                var Mtx = m_op.OperatorMatrix;

                // compute fine residual: Res_f = B - Mtx*X
                double[] Res_f = new double[Lf];
                Res_f.SetV(B);
                Mtx.SpMV(-1.0, X, 1.0, Res_f);
                
                // solve for coarse correction
                var Cor_f = new double[Lf];
                CoarseSolve(Cor_f, Res_f);

                // accumulate smoothing 
                FineSolve(Cor_f, Res_f);
                
                // solution update
                X.AccV(1.0, Cor_f);
                m_Iter++;
                
              

                if(IterationCallback != null) {
                    Res_f.SetV(B);
                    Mtx.SpMV(-1.0, X, 1.0, Res_f);
                    IterationCallback(m_Iter, X.ToArray(), Res_f, m_op);
                }


                //var Res_f_0 = Res_f.CloneAs();

               

                /*
                if(!SkipLowOrderSolve) {
                    // project to low-p/coarse
                    double[] Res_c = lMask.GetSubVec(Res_f);

                    // low-p solve
                    lowSolver.Solve(Cor_c, Res_c);

                    // accumulate low-p correction
                    lMask.AccSubVec(Cor_c, Cor_f);

                    // compute residual of low-order solution (on fine Level)
                    Res_f.SetV(B);
                    Mtx.SpMV(-1.0, Cor_f, 1.0, Res_f); 
                }
                */


                

                m_Iter++;
            }
        }

        double[] GetVariableDOFs(double[] X, int SelVar) {
            var DGSelect = new SubBlockSelector(m_op.Mapping);
            DGSelect.VariableSelector(SelVar);
                      

            var lMask = new BlockMask(DGSelect);

            var X_SelVar = lMask.GetSubVec(X);

            double[] Ret = new double[X.Length];
            lMask.AccSubVec(X_SelVar, Ret);

            return Ret;
        }

        



        /// <summary>
        /// Called upon each iteration
        /// </summary>
        public Action<int, double[], double[], MultigridOperator> IterationCallback {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose() {
            if(lowSolver != null) {
                lowSolver.Dispose();
                lowSolver = null;
            }
            if(hiSolver != null) {
                hiSolver.Dispose();
                hiSolver = null;
            }


        }

        /// <summary>
        /// 
        /// </summary>
        public long UsedMemory() {
            long r = 0;

            foreach(var mda in this.HighOrderBlocks_LU) {
                if(mda != null) {
                    r += mda.Length * sizeof(double);
                }
            }

            foreach(var ia in this.HighOrderBlocks_LUpivots) {
                if(ia != null) {
                    r += ia.Length * sizeof(int);
                }
            }


            return r;
        }
    }
}
