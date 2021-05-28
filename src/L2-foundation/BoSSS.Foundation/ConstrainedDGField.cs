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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ilPSP;
using ilPSP.Connectors.Matlab;
using ilPSP.Utils;
using ilPSP.LinSolvers;
using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.Quadrature;
using System.Diagnostics;
using MPI.Wrappers;
using ilPSP.Kraypis;

namespace BoSSS.Foundation {

    public class myCG : IDisposable {
        public void Init(BlockMsrMatrix M) {
            m_Matrix = M;
//#if TEST
//            var tmp=M.ToFullMatrixOnProc0();
            M.SaveToTextFileSparse("M");
//            if(M.RowPartitioning.MpiRank==0)
//                tmp.SaveToTextFile("fullM");
//#endif
            //var M_test = M.CloneAs();
            //M_test.Acc(-1.0, M.Transpose());
            //Console.WriteLine("Symm-test: " + M_test.InfNorm());
            //Console.WriteLine("Inf-Norm: " + M.InfNorm());
            //m_Matrix.SaveToTextFileSparse("M");
            PrecondInit();
        }

        private BlockMsrMatrix ILU_M;
        public void GetLocalMatrix() {
            int rank;
            MPI.Wrappers.csMPI.Raw.Comm_Rank(MPI.Wrappers.csMPI.Raw._COMM.WORLD, out rank);
            
            // extraction of local Matrix block, ILU will be executed process local
            var LocBlocki0s = new List<long>();
            var LocBlockLen = new List<int>();
            long IdxOffset = m_Matrix._RowPartitioning.i0;
            for (int i=0;i< m_Matrix._RowPartitioning.LocalNoOfBlocks; i++) {
                long iBlock = i + m_Matrix._RowPartitioning.FirstBlock;
                long i0 = m_Matrix._RowPartitioning.GetBlockI0(iBlock)- IdxOffset;
                int Len = m_Matrix._RowPartitioning.GetBlockLen(iBlock);
                LocBlocki0s.Add(i0);
                LocBlockLen.Add(Len);
            }
            long[] RowISrc = m_Matrix._RowPartitioning.LocalLength.ForLoop(i => i + IdxOffset);

            var part = new BlockPartitioning(m_Matrix._RowPartitioning.LocalLength, LocBlocki0s, LocBlockLen, csMPI.Raw._COMM.SELF);
            BlockMsrMatrix localMatrix = new BlockMsrMatrix(part);

            m_Matrix.WriteSubMatrixTo(localMatrix, RowISrc, default(long[]), RowISrc, default(long[]));
            ILU_M = localMatrix;
        }

        ISparseSolver dirsolver;

        private void DirectInit() {
            dirsolver = new ilPSP.LinSolvers.PARDISO.PARDISOSolver() { 
                CacheFactorization = true,
                UseDoublePrecision = false
            };
            dirsolver.DefineMatrix(ILU_M);
        }

        private void DirectSolve<P, Q>(P X, Q B)
            where P : IList<double>
            where Q : IList<double> {
            dirsolver.Solve(X,B);
        }

        private void ILUDecomposition() {
            

            if (this.ILU_M.RowPartitioning.MpiSize != 1)
                throw new NotSupportedException();
            long n = ILU_M.RowPartitioning.LocalLength;

            // Zeros on diagonal elements because of saddle point structure
            for (int bla = 0; bla < n; bla++) {
                if (ILU_M.GetDiagonalElement(bla) == 0)
                    ILU_M.SetDiagonalElement(bla, 1);
            }

            Func<double, bool> ZeroPattern = delegate (double e) {
                return e == 0;
                //return false;
            };

            // ++++++++++++++++++++
            // ILU(0) decomposition
            // ++++++++++++++++++++

            for (int k = 0; k < n - 1; k++) {
                for (int i = k + 1; i < n; i++) {
                    if (ZeroPattern(ILU_M[i, k])) continue;
                    ILU_M[i, k] = ILU_M[i, k] / ILU_M[k, k];
                    for (int j = k + 1; j < n; j++) {
                        if (ZeroPattern(ILU_M[i, j])) continue;
                        ILU_M[i, j] = ILU_M[i, j] - ILU_M[i, k] * ILU_M[k, j];
                    }
                }
            }

            //var part = ILU_M._RowPartitioning;
            //var L = new BlockMsrMatrix(part);
            //var U = new BlockMsrMatrix(part);

            //for (int i = 0; i < n; i++) {
            //    for (int j = 0; j < i; j++) {
            //        L[i, j] = ILU_M[i, j];
            //        U[j, i] = ILU_M[j, i];
            //    }
            //    L[i, i] = 1;
            //    U[i, i] = ILU_M[i, i];
            //}
        }

        private void ICholDecomposition() {
            var part = ILU_M._RowPartitioning;

            if (this.ILU_M.RowPartitioning.MpiSize != 1)
                throw new NotSupportedException();
            long n = ILU_M.RowPartitioning.LocalLength;

            // Zeros on diagonal elements because of saddle point structure
            for (int bla = 0; bla < n; bla++) {
                if (ILU_M.GetDiagonalElement(bla) == 0)
                    ILU_M.SetDiagonalElement(bla, 1);
            }

            Func<double, bool> ZeroPattern = delegate (double e) {
                return e == 0;
                //return false;
            };

            // ++++++++++++++++++++
            // ICHOL(0) decomposition
            // ++++++++++++++++++++

            for (int k = 0; k < n; k++) {
                ILU_M[k, k] = Math.Sqrt(ILU_M[k, k]);
                for (int i = k + 1; i < n; i++) {
                    if (ZeroPattern(ILU_M[i, k])) continue;
                    ILU_M[i, k] = ILU_M[i, k] / ILU_M[k, k];
                }
                for (int j = k + 1; j < n; j++) {
                    for (int i = j; i < n; i++) {
                        if (ZeroPattern(ILU_M[i, j])) continue;
                        ILU_M[i, j] = ILU_M[i, j] - ILU_M[i, k] * ILU_M[j, k];
                    }
                }
            }

            for(int i = 0;i < n; i++) {
                for (int j=i+1;j<n;j++) {
                    ILU_M[i, j] = ILU_M[j, i];
                    if (Double.IsNaN(ILU_M[i, j]) || Double.IsInfinity(ILU_M[i, j]))
                        throw new Exception("Ich habs doch gewusst");
                }
            }
        }

        public void DecompSolve<P, Q>(P X, Q B)
            where P : IList<double>
            where Q : IList<double> {

            long n = ILU_M.RowPartitioning.LocalLength;
            double buffer = 0;

            // find solution of Ly = b
            double[] y = new double[n];
            for (int i = 0; i < n; i++) {
                buffer = 0;
                for (int k = 0; k < i; k++)
                    buffer += ILU_M[i, k] * y[k];
                //y[i] = (1/ ILU_M[i, i])*(B[i] - buffer);
                y[i] = (B[i] - buffer);
            }
            // find solution of Ux = y
            for (long i = n - 1; i >= 0; i--) {
                buffer = 0;
                for (long k = i + 1; k < n; k++)
                    buffer += ILU_M[i, k] * X[(int)k]; // index into Mtx and X must be different for more than 1 MPI process.
                X[(int)i] = (1 / ILU_M[i, i]) * (y[i] - buffer);
            }
        }

        public void BlockJacInit() {
            BlockMsrMatrix M = m_Matrix;

            int L = M.RowPartitioning.LocalLength;

            Diag = new BlockMsrMatrix(M._RowPartitioning, M._ColPartitioning);
            invDiag = new BlockMsrMatrix(M._RowPartitioning, M._ColPartitioning);
            int Jloc = M._RowPartitioning.LocalNoOfBlocks;
            long j0 = M._RowPartitioning.FirstBlock;
            MultidimensionalArray temp = null;
            for (int j = 0; j < Jloc; j++) {
                long jBlock = j + j0;
                int Nblk = M._RowPartitioning.GetBlockLen(jBlock);
                long i0 = M._RowPartitioning.GetBlockI0(jBlock);

                if (temp == null || temp.NoOfCols != Nblk)
                    temp = MultidimensionalArray.Create(Nblk, Nblk);

                M.ReadBlock(i0, i0, temp);
                Diag.AccBlock(i0, i0, 1.0, temp, 0.0);
                temp.InvertInPlace();
                invDiag.AccBlock(i0, i0, 1.0, temp, 0.0);
            }
        }
        private double omega=0.5;

        public void BlockJacSolve<U, V>(U xl, V bl)
            where U : IList<double>
            where V : IList<double> {
            int L = xl.Count;
            double[] ql = new double[L];

            double iter0_ResNorm = 0;

            for (int iIter = 0; iIter<10; iIter++) {
                ql.SetV(bl);
                m_Matrix.SpMV(-1.0, xl, 1.0, ql);
                double ResNorm = ql.L2NormPow2().MPISum().Sqrt();

                if (iIter == 0) {
                    iter0_ResNorm = ResNorm;
                }

                Diag.SpMV(1.0, xl, 1.0, ql);
                invDiag.SpMV(omega, ql, 1.0 - omega, xl);

            }
        }

        BlockMsrMatrix m_Matrix;
        BlockMsrMatrix Diag;
        BlockMsrMatrix invDiag;

        private void PrecondInit() {
            //BlockJacInit();
            GetLocalMatrix();
            //DirectInit();
            ILUDecomposition();
            //ICholDecomposition();
        }

        private void PrecondSolve<U, V>(U xl, V bl) where U : IList<double>
            where V : IList<double> {
            //BlockJacSolve(xl,bl);
            DecompSolve(xl, bl);
            //DirectSolve(xl,bl);
        }

        public void Solve<Vec1, Vec2>(Vec1 _x, Vec2 _R)
            where Vec1 : IList<double>
            where Vec2 : IList<double> //
        {
            
                double[] x, R;
                if (_x is double[]) {
                    x = _x as double[];
                } else {
                    x = _x.ToArray();
                }
                if (_R is double[]) {
                    R = _R as double[];
                } else {
                    R = _R.ToArray();
                }

                int L = x.Length;

            double[] P = new double[L];
                double[] V = new double[L];
                double[] Z = new double[L];

            R.SaveToTextFile("R");

            // compute P0, R0
            // ==============
            GenericBlas.dswap(L, x, 1, P, 1);
                m_Matrix.SpMV(-1.0, P, 1.0, R);

                GenericBlas.dswap(L, x, 1, P, 1);

            //P.SetV(R);
            PrecondSolve(Z, R);
            P.SetV(Z);

            double alpha = R.InnerProd(P).MPISum();
            double alpha_0 = alpha;
            Console.WriteLine(alpha);
            double ResNorm;

            var ResReal = _R.ToArray();
            var Xdummy = new double[R.Length];

            ResNorm = Math.Sqrt(alpha);
                double ResNorm0 = ResNorm;

            

                // iterate
                // =======
                for (int n = 1; true; n++) {

                if (n % 1 == 0) {

                    Xdummy.SetV(x);
                    var theResidual = new double[R.Length];
                    theResidual.SetV(ResReal);
                    m_Matrix.SpMV(-1.0, Xdummy, 1.0, theResidual);
                    Console.WriteLine("Res real at n"+n+":"+ theResidual.MPI_L2Norm());
                }

                Console.WriteLine("ResNorm at n"+n+":"+ResNorm);
                    if (ResNorm / ResNorm0 + ResNorm < 1E-8 || ResNorm < 1E-8 || n > 100) {
                        if (n > 1000) Console.WriteLine("maximum number of iterations reached. Solution maybe not converged.");    
                        break;
                    }

                    if (Math.Abs(alpha) <= double.Epsilon) {
                        // numerical breakdown
                        break;
                    }


                    m_Matrix.SpMV(1.0, P, 0, V);
                    double VxP = V.InnerProd(P).MPISum();
                    if (double.IsNaN(VxP) || double.IsInfinity(VxP))
                        throw new ArithmeticException();
                    double lambda = alpha / VxP;
                Console.WriteLine(lambda);
                if (double.IsNaN(lambda) || double.IsInfinity(lambda))
                        throw new ArithmeticException();


                    x.AccV(lambda, P);

                    R.AccV(-lambda, V);

                //Z.SetV(R);
                Z.Clear();
                PrecondSolve(Z, R);

                double alpha_neu = R.InnerProd(Z).MPISum();
                Console.WriteLine(alpha_neu);
                    // compute residual norm
                    ResNorm = R.MPI_L2Norm();

                    P.ScaleV(alpha_neu / alpha);
                    P.AccV(1.0, Z);

                    alpha = alpha_neu;
                }

                if (!object.ReferenceEquals(_x, x))
                    _x.SetV(x);
                if (!object.ReferenceEquals(_R, R))
                    _R.SetV(R);


                return;
            
        }

        public void Dispose() {
            m_Matrix.Clear();
        }
    }


    /// <summary>
    /// Abstract base class, template for different strategies 
    /// </summary>
    public abstract class BlockingStrategy {

        /// <summary>
        /// Returns lists which form the blocks of the additive-Schwarz domain decomposition.
        /// </summary>
        /// <param name="op"></param>
        /// <returns>
        /// - outer enumeration: corresponds to domain-decomposition blocks
        /// - inner index: indices within the sub-blocks
        /// - content: local cell indices which form the respective additive-Schwarz block (<see cref="MultigridOperator"/>
        /// </returns>
        abstract internal IEnumerable<List<int>> GetBlocking(GridData grdDat, CellMask mask);

        /// <summary>
        /// Number of blocs returned by <see cref="GetBlocking(MultigridOperator)"/>
        /// </summary>
        internal abstract int GetNoOfBlocks();
    }


    /// <summary>
    /// creates a fixed number of blocks by using METIS
    /// </summary>
    public class METISBlockingStrategy : BlockingStrategy {

        /// <summary>
        /// Number of parts/additive Schwarz blocks on current MPI process (can be different on other processors)
        /// </summary>
        public int NoOfPartsOnCurrentProcess = 1;

        internal override IEnumerable<List<int>> GetBlocking(GridData grdDat, CellMask mask) {

            //if (cache != null) {
            //    return cache.Select(orgList => new List<int>(orgList)).ToArray();
            //}

            SubGrid sbgrd = new SubGrid(mask);

            int JComp = mask.NoOfItemsLocally; // number of local cells
            int[] xadj = new int[JComp + 1];
            List<int> adjncy = new List<int>();
            for (int j = 0; j < JComp; j++) {
                Debug.Assert(xadj[j] == adjncy.Count);

                int j_loc = sbgrd.SubgridIndex2LocalCellIndex[j];
                int[] neigh_j = grdDat.iLogicalCells.CellNeighbours[j_loc]; 
                int nCnt = 0;
                foreach (int jNeigh in neigh_j) {
                    //adjncy.AddRange(neigh_j);
                    if (jNeigh < JComp) {
                        adjncy.Add(jNeigh);
                        nCnt++;
                    } else {
                        //Console.WriteLine("Skipping external cell");
                    }
                }
                xadj[j + 1] = xadj[j] + nCnt;
            }
            Debug.Assert(xadj[JComp] == adjncy.Count);

            int MPIrank, MPIsize;
            MPI.Wrappers.csMPI.Raw.Comm_Rank(MPI.Wrappers.csMPI.Raw._COMM.WORLD, out MPIrank);
            MPI.Wrappers.csMPI.Raw.Comm_Size(MPI.Wrappers.csMPI.Raw._COMM.WORLD, out MPIsize);
            //if (MPIrank == 1)
            //    NoOfParts = 1;
            //Debugger.Launch();

            int[] part = new int[JComp];
            {
                if (NoOfPartsOnCurrentProcess > 1) {
                    int ncon = 1;
                    int edgecut = 0;
                    int[] options = new int[METIS.METIS_NOPTIONS];
                    METIS.SETDEFAULTOPTIONS(options);

                    options[(int)METIS.OptionCodes.METIS_OPTION_NCUTS] = 1; // 
                    options[(int)METIS.OptionCodes.METIS_OPTION_NITER] = 10; // This is the default refinement iterations
                    options[(int)METIS.OptionCodes.METIS_OPTION_UFACTOR] = 30; // Maximum imbalance of 3 percent (this is the default kway clustering)
                    options[(int)METIS.OptionCodes.METIS_OPTION_NUMBERING] = 0;

                    Debug.Assert(xadj.Where(idx => idx > adjncy.Count).Count() == 0);
                    Debug.Assert(adjncy.Where(j => j >= JComp).Count() == 0);

                    METIS.PARTGRAPHKWAY(
                            ref JComp, ref ncon,
                            xadj,
                            adjncy.ToArray(),
                            null,
                            null,
                            null,
                            ref NoOfPartsOnCurrentProcess,
                            null,
                            null,
                            options,
                            ref edgecut,
                            part);
                } else {
                    part.SetAll(0);
                }
            }

            {
                List<List<int>> _Blocks = NoOfPartsOnCurrentProcess.ForLoop(i => new List<int>((int)Math.Ceiling(1.1 * JComp / NoOfPartsOnCurrentProcess))).ToList();
                for (int j = 0; j < JComp; j++) {
                    _Blocks[part[j]].Add(j);
                }

                for (int iB = 0; iB < _Blocks.Count; iB++) {
                    if (_Blocks[iB].Count <= 0) {
                        _Blocks.RemoveAt(iB);
                        iB--;
                    }
                }

                if (_Blocks.Count < NoOfPartsOnCurrentProcess)
                    Console.WriteLine("METIS WARNING: requested " + NoOfPartsOnCurrentProcess + " blocks, but got " + _Blocks.Count);

                //cache = _Blocks.ToArray();
                //Console.WriteLine("MetisBlocking Testcode active !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                return _Blocks.ToArray().Select(orgList => new List<int>(orgList)).ToArray();
            }
        }

        //List<int>[] cache;


        /// <summary>
        /// %
        /// </summary>
        internal override int GetNoOfBlocks() {
            return NoOfPartsOnCurrentProcess;
        }
    }


    /// <summary>
    /// continuous DG field via L2-projection with continuity constraints
    /// </summary>
    public class ConstrainedDGField {


        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="b"></param>
        public ConstrainedDGField(Basis b) {
            m_Basis = b;
            m_grd = (GridData)b.GridDat;
            m_Mapping = new UnsetteledCoordinateMapping(b);
            m_Coordinates = MultidimensionalArray.Create(m_Mapping.LocalLength);
        }

        Basis m_Basis;

        public Basis Basis {
            get {
                return m_Basis;
            }
        }

        GridData m_grd;

        UnsetteledCoordinateMapping m_Mapping;

        MultidimensionalArray m_Coordinates;

        public MultidimensionalArray Coordinates {
            get {
                return m_Coordinates;
            }
        }

        ISparseSolver m_OpSolver;


        //int NoOfPatchesPerProcess = 0;  // if < 1 the number of patches is determined by 
        int maxNoOfCoordinates = 10000; 


        int[] GlobalVert2Local;
    


        /// <summary>
        /// linear solver for the quadratic optimization problem, matrix A has to be defined! 
        /// </summary>
        public ilPSP.LinSolvers.ISparseSolver OpSolver {
            get {
                if (m_OpSolver == null) {
                    //var solver = new ilPSP.LinSolvers.monkey.PCG();
                    //solver.MatrixType = ilPSP.LinSolvers.monkey.MatrixType.Auto;
                    //solver.DevType = ilPSP.LinSolvers.monkey.DeviceType.CPU;
                    //solver.Tolerance = 1.0e-12;
                    //var solver = new ilPSP.LinSolvers.HYPRE.PCG();
                    //var precond = new ilPSP.LinSolvers.HYPRE.Euclid();
                    //solver.NestedPrecond = precond;
                    var solver = new ilPSP.LinSolvers.PARDISO.PARDISOSolver();
                    //var solver = new ilPSP.LinSolvers.MUMPS.MUMPSSolver();
                    m_OpSolver = solver;
                }

                return m_OpSolver;
            }
        }


        /// <summary>
        /// Projects some DG field <paramref name="DGField"/> onto the internal, continuous representation
        /// </summary>
        /// <param name="DGField">
        /// input; unchanged on exit
        /// </param>
        /// <param name="mask"></param>
        public DGField[] ProjectDGField(ConventionalDGField DGField, CellMask mask = null) {

            int NoOfFixedPatches = 4;

            return ProjectDGField_patchwise(DGField, mask, NoOfFixedPatches);

        }


        public DGField[] ProjectDGField_patchwise(ConventionalDGField DGField, CellMask mask = null, int NoOfPatchesPerProcess = 0) {

            if (DGField.Basis.Degree > this.m_Basis.Degree)
                throw new ArgumentException("continuous projection on a lower degree basis is not recommended");
            this.Coordinates.Clear(); // clear internal state, to get the same result for the same input every time

            if (mask == null) {
                mask = CellMask.GetFullMask(m_grd);
            }
            int J = mask.NoOfItemsLocally;

            // determine number of patches
            int NoPatches = 1;
            if (NoOfPatchesPerProcess > 0) {
                NoPatches = NoOfPatchesPerProcess;
            } else {
                int NoOfCoordOnProc = mask.NoOfItemsLocally * DGField.Mapping.MaxTotalNoOfCoordinatesPerCell;
                if (NoOfCoordOnProc > maxNoOfCoordinates) {
                    NoPatches = (NoOfCoordOnProc % maxNoOfCoordinates > 0) ? NoOfCoordOnProc / maxNoOfCoordinates : NoOfCoordOnProc / maxNoOfCoordinates + 1;
                }
            }

            // divide projection domain into non-overlapping patches
            var m_BlockingStrategy = new METISBlockingStrategy() {
                NoOfPartsOnCurrentProcess = NoPatches
            };
            var blocking = m_BlockingStrategy.GetBlocking(m_grd, mask);


            // constrained projection on all patches
            List<CellMask> patches = new List<CellMask>();
            int pC = 0;
            foreach (List<int> block in blocking) {

                BitArray patchBA = new BitArray(J);
                foreach (int j in block) {
                    patchBA[j] = true;
                }
                CellMask patch = new CellMask(m_grd, patchBA);
                patches.Add(patch);

                Console.WriteLine("======================");
                Console.WriteLine("project patch {0}:", pC);
                this.ProjectDGFieldOnPatch(DGField, patch);
                pC++;
            }


            if (NoPatches > 1 || m_grd.MpiSize > 1) {

                // determine merging domain and 
                // corresponding inner edges and on the boundary 
                SubGrid maskSG = new SubGrid(mask);
                EdgeMask innerEM = maskSG.InnerEdgesMask;
                EdgeMask mergeEM = innerEM;
                foreach (CellMask patch in patches) {
                    SubGrid maskPatch = new SubGrid(patch);
                    EdgeMask innerPatch = maskPatch.InnerEdgesMask;
                    mergeEM = mergeEM.Except(innerPatch);
                }

                BitArray mergePatchBA = new BitArray(J);
                foreach (var chunk in mergeEM) {
                    int j0 = chunk.i0;
                    int jE = chunk.JE;
                    for (int j = j0; j < jE; j++) {
                        int cell1 = m_grd.Edges.CellIndices[j, 0];
                        mergePatchBA[cell1] = true;
                        int cell2 = m_grd.Edges.CellIndices[j, 1];
                        mergePatchBA[cell2] = true;
                    }
                }
                CellMask mergePatchCM = new CellMask(mask.GridData, mergePatchBA);
                SubGrid mergePatch = new SubGrid(mergePatchCM);
                EdgeMask mergeBoundary = mergePatch.BoundaryEdgesMask.Intersect(innerEM);

                // projection of local projection on separate patches
                DGField localProj = new SinglePhaseField(m_Basis, "localProjection");
                int stride = DGField.Mapping.MaxTotalNoOfCoordinatesPerCell;
                localProj._Acc(1.0, m_Coordinates.To1DArray(), 0, stride, true);

                //Console.WriteLine("project transition patch:");
                //this.ProjectDGFieldOnPatch(DGField, mergePatchCM, mergeBoundary, true);

                // plot patches for debugging
                SinglePhaseField patchField = new SinglePhaseField(m_Basis, "Patches");
                int pColor = 1;
                foreach (CellMask patch in patches) {
                    patchField.AccConstant(pColor, patch);
                    pColor++;
                }

                SinglePhaseField mergePatchField = new SinglePhaseField(m_Basis, "Merging-Patch");
                mergePatchField.AccConstant(1.0, mergePatchCM);

                List<DGField> returnFields = new List<DGField>();
                returnFields.Add(patchField);
                returnFields.Add(mergePatchField);
                returnFields.Add(localProj);



                // 1. merge between two adjacent patches
                // =====================================


                for (int p1 = 0; p1 < NoPatches; p1++) {

                    SubGrid patch1 = new SubGrid(patches.ElementAt(p1));
                    EdgeMask allEM1 = patch1.AllEdgesMask;
                    EdgeMask innerEM1 = patch1.InnerEdgesMask;

                    for (int p2 = p1 + 1; p2 < NoPatches; p2++) {

                        SubGrid patch2 = new SubGrid(patches.ElementAt(p2));
                        EdgeMask allEM2 = patch2.AllEdgesMask;
                        EdgeMask innerEM2 = patch2.InnerEdgesMask;

                        // inner edges between patch 1 and patch 2
                        EdgeMask mergeEM12 = allEM1.Intersect(allEM2);

                        // 
                        BitArray merge12PatchBA = new BitArray(J);
                        foreach (var chunk in mergeEM12) {
                            int j0 = chunk.i0;
                            int jE = chunk.JE;
                            for (int j = j0; j < jE; j++) {
                                int cell1 = m_grd.Edges.CellIndices[j, 0];
                                merge12PatchBA[cell1] = true;
                                int cell2 = m_grd.Edges.CellIndices[j, 1];
                                merge12PatchBA[cell2] = true;
                            }
                        }
                        CellMask merge12PatchCM = new CellMask(mask.GridData, merge12PatchBA);
                        SubGrid merge12Patch = new SubGrid(merge12PatchCM);
                        EdgeMask merge12Boundary = merge12Patch.BoundaryEdgesMask.Intersect(innerEM);
                        EdgeMask innerMergeEM = mergePatch.InnerEdgesMask;
                        merge12Boundary = merge12Boundary.Except(innerMergeEM);

                        if (!merge12PatchCM.IsEmptyOnRank) {
                            Console.WriteLine("======================");
                            Console.WriteLine("project merging patch {0}-{1}:", p1, p2);
                            Console.WriteLine("number of cells in merge patch: {0}", merge12PatchCM.NoOfItemsLocally);
                            Console.WriteLine("number of edges in merge boundary: {0}", merge12Boundary.NoOfItemsLocally);
                            this.ProjectDGFieldOnPatch(DGField, merge12PatchCM, merge12Boundary, true);

                            string mergePatchName = "Merging-Patch_" + p1 + p2;
                            SinglePhaseField mergePatch12Field = new SinglePhaseField(m_Basis, mergePatchName);
                            mergePatch12Field.AccConstant(1.0, merge12PatchCM);
                            returnFields.Add(mergePatch12Field);

                        } else {
                            Console.WriteLine("======================");
                            Console.WriteLine("no projection on merging patch {0}-{1}:", p1, p2);
                        }
                    }
                }

                // projection of local projections on separate merge patches
                localProj = new SinglePhaseField(m_Basis, "localMergeProjection");
                localProj._Acc(1.0, m_Coordinates.To1DArray(), 0, stride, true);
                returnFields.Add(localProj);


                return returnFields.ToArray();
            }

            return null;
        }


        public void ProjectDGFieldOnPatch(ConventionalDGField DGField, CellMask mask = null, 
            EdgeMask fixedBoundaryMask = null, bool onInternalValues = false) {

            //if (DGField.Basis.Degree > this.m_Basis.Degree)
            //    throw new ArgumentException("continuous projection on a lower degree basis is not recommended");
            //this.Coordinates.Clear(); // clear internal state, to get the same result for the same input every time

            int D = this.Basis.GridDat.SpatialDimension;

            if (mask == null) {
                mask = CellMask.GetFullMask(m_grd);
            }
            if (mask.NoOfItemsLocally.MPISum() <= 0) {
                throw new ArgumentOutOfRangeException("Domain mask cannot be empty.");
            }

            // list of masked vertices inside domain mask
            List<GeometricVerticeForProjection> maskedVert = new List<GeometricVerticeForProjection>();
            List<GeometricEdgeForProjection> maskedEdges = new List<GeometricEdgeForProjection>();
            foreach (var chunk in mask) {
                int j0 = chunk.i0;
                int jE = chunk.JE;
                for (int j = j0; j < jE; j++) {
                    int[] vertAtCell = m_grd.Cells.CellVertices[j];
                    foreach (int vert in vertAtCell) {
                        GeometricVerticeForProjection gVert = new GeometricVerticeForProjection(vert);
                        if (!maskedVert.Contains(gVert)) {
                            maskedVert.Add(gVert);
                        }
                    }
                    List<GeometricEdgeForProjection> edgesAtCell = GetGeometricEdgesForCell(vertAtCell);
                    foreach (var gEdge in edgesAtCell) {
                        if (!maskedEdges.Contains(gEdge)) {
                            maskedEdges.Add(gEdge);
                        }
                    }
                }
            }


            // get DG-coordinates (change of basis for projection on a higher polynomial degree)
            if (!onInternalValues) {
                foreach (var chunk in mask) {
                    int j0 = chunk.i0;
                    int jE = chunk.JE;
                    for (int j = j0; j < jE; j++) {
                        int N = DGField.Basis.GetLength(j);
                        for (int n = 0; n < N; n++) {
                            m_Coordinates[m_Mapping.LocalUniqueCoordinateIndex(0, j, n)] = DGField.Coordinates[j, n];
                        }
                    }
                }
            }


            // construction of constraints matrix A
            // ====================================

            SubGrid maskSG = new SubGrid(mask);
            EdgeMask innerEM = maskSG.InnerEdgesMask;

            if (innerEM.NoOfItemsLocally.MPISum() <= 0) {
                Console.WriteLine("no inner edges: return without changes");
                return;
            }

            //List<int> AcceptedEdges = new List<int>();
            List<NodeSet> AcceptedNodes = new List<NodeSet>();


            if (fixedBoundaryMask != null) {
                // in case of fixed boundary projection (higher priority regarding constrains)
                determineConstrainsNodes(fixedBoundaryMask, maskedVert, maskedEdges, AcceptedNodes, true, mask);

                // inner edges on merge domain
                determineConstrainsNodes(innerEM, maskedVert, maskedEdges, AcceptedNodes, false, mask);

            } else {

                // local inner edges per process
                determineConstrainsNodes(innerEM, maskedVert, maskedEdges, AcceptedNodes);  // check if switch for 2D/3D is possible (overdetermined vertices in 3D?)

            }


            List<long> BlockI0 = new List<long>();
            List<int> BlockLen = new List<int>();
            long i0 = 0;
            int nodeCount = 0;
            foreach (NodeSet ns in AcceptedNodes) {
                if (ns != null) {
                    BlockI0.Add(i0);
                    BlockLen.Add(ns.Lengths[0]);
                    i0 += ns.Lengths[0];
                    nodeCount += ns.NoOfNodes;
                }
            }

            BlockPartitioning rowBlockPart = new BlockPartitioning(nodeCount, BlockI0, BlockLen, m_Mapping.MPI_Comm, true);
            BlockMsrMatrix A = new BlockMsrMatrix(rowBlockPart, m_Mapping);

            int count = 0;
            long _nodeCount = A.RowPartitioning.i0; // start at global index
            double[] RHS_non0c = new double[nodeCount];   // non-zero equality constrains

            if (fixedBoundaryMask != null) {

                DGField internalProj = new SinglePhaseField(m_Basis);
                int stride = DGField.Mapping.MaxTotalNoOfCoordinatesPerCell;
                internalProj._Acc(1.0, m_Coordinates.To1DArray(), 0, stride, true);

                var retCount = assembleConstrainsMatrix_nonZeroConstrains(A, fixedBoundaryMask, AcceptedNodes, mask, internalProj, RHS_non0c, count, _nodeCount);
                count = retCount.Item1;
                _nodeCount = retCount.Item2;

            }

            assembleConstrainsMatrix(A, innerEM, AcceptedNodes, count, _nodeCount);


            // test with matlab
            MultidimensionalArray output = MultidimensionalArray.Create(1, 2);
            Console.WriteLine("Calling MATLAB/Octave...");
            using (BatchmodeConnector bmc = new BatchmodeConnector()) {
                bmc.PutSparseMatrix(A, "A");
                bmc.Cmd("rank_A = rank(full(A))");
                bmc.Cmd("rank_AT = rank(full(A'))");
                bmc.GetMatrix(output, "[rank_A, rank_AT]");

                bmc.Execute(false);
            }

            Console.WriteLine("A: No of Rows = {0}; rank = {1}", A.NoOfRows, output[0, 0]);
            Console.WriteLine("AT: No of Rows = {0}; rank = {1}", A.Transpose().NoOfRows, output[0, 1]);



            // solve system
            // ============

            BlockMsrMatrix AT = A.Transpose();

            BlockMsrMatrix AAT = new BlockMsrMatrix(rowBlockPart, rowBlockPart);
            BlockMsrMatrix.Multiply(AAT, A, AT);

            double[] RHS = new double[rowBlockPart.LocalLength];           
            A.SpMV(1.0, m_Coordinates.To1DArray(), 0.0, RHS);
            RHS.AccV(-1.0, RHS_non0c);

            double[] v = new double[rowBlockPart.LocalLength];
            double[] x = new double[m_Coordinates.Length];          

            OpSolver.DefineMatrix(AAT);
            OpSolver.Solve(v, RHS);
            OpSolver.Dispose();

            // x = RHS - ATv
            AT.SpMV(-1.0, v, 0.0, x);
            m_Coordinates.AccVector(1.0, x);          

        }


        void determineConstrainsNodes(EdgeMask constrainsEdges, List<GeometricVerticeForProjection> maskedVert,
            List<GeometricEdgeForProjection> maskedEdges, List<NodeSet> AcceptedNodes, 
            bool onFixedBoundary = false, CellMask mergeDomain = null) {

            int D = m_grd.SpatialDimension;

            foreach (var chunk in constrainsEdges) {
                int j0 = chunk.i0;
                int jE = chunk.JE;
                for (int j = j0; j < jE; j++) {

                    var edgeInfo = m_grd.Edges.Info[j];

                    int cell1 = m_grd.Edges.CellIndices[j, 0];
                    int cell2 = m_grd.Edges.CellIndices[j, 1];

                    int trafoIdx1 = m_grd.Edges.Edge2CellTrafoIndex[j, 0];
                    int trafoIdx2 = m_grd.Edges.Edge2CellTrafoIndex[j, 1];

                    int[] vertAtCell1 = m_grd.Cells.CellVertices[cell1];
                    int[] vertAtCell2 = m_grd.Cells.CellVertices[cell2];

                    // get geometric vertices/edges(3d) at considered edge/(face)
                    //List<GeometricVerticeForProjection> geomVertAtEdge = new List<GeometricVerticeForProjection>();
                    //List<GeometricEdgeForProjection> geomEdgeAtEdge = new List<GeometricEdgeForProjection>();


                    if (onFixedBoundary) {
                        if (mergeDomain != null) {
                            if (mergeDomain.Contains(cell2)) {
                                cell1 = cell2;
                                trafoIdx1 = trafoIdx2;
                                vertAtCell1 = vertAtCell2;
                            } else if (!mergeDomain.Contains(cell1))
                                throw new ArgumentException("fixed boundary not within mask for projection");

                            cell2 = int.MinValue;
                            trafoIdx2 = int.MinValue;
                            vertAtCell2 = null;
                        } else {
                            throw new ArgumentException("no mask given for fixed boundary projection");
                        }
                    }


                    // 
                    int maxCondAtVert = (D == 2) ? 4 : 12;
                    int OverdeterminedCondAtVertice = 0;
                    for (int i = 0; i < vertAtCell1.Length; i++) {
                        int vert = vertAtCell1[i];
                        if (onFixedBoundary || vertAtCell2.Contains(vert)) {
                            GeometricVerticeForProjection gVert = maskedVert.Find(vrt => vrt.Equals(vert));
                            //geomVertAtEdge.Add(gVert);
                            gVert.IncreaseNoOfConditions();
                            int condAtVert = gVert.GetNoOfConditions();
                            //Console.WriteLine("conditions at vertice {0}: {1}", vert, condAtVert);
                            Debug.Assert(condAtVert <= maxCondAtVert);
                            if (condAtVert == maxCondAtVert)
                                OverdeterminedCondAtVertice++;
                        }
                    }
                    //Debug.Assert(geomVertAtEdge.Count() == (D - 1) * 2);

                    // 
                    int OverdeterminedCondAtGeomEdge = 0;
                    int[] OverdeterminedEdgeDirection = new int[m_Basis.Degree + 1];
                    if (D == 3) {
                        List<GeometricEdgeForProjection> edgesAtFace1 = GetGeometricEdgesForCell(vertAtCell1);
                        List<GeometricEdgeForProjection> edgesAtFace2 = GetGeometricEdgesForCell(vertAtCell2);
                        foreach (var gEdge1 in edgesAtFace1) {
                            if (onFixedBoundary || edgesAtFace2.Contains(gEdge1)) {
                                GeometricEdgeForProjection gEdge = maskedEdges.Find(edg => edg.Equals(gEdge1));
                                //geomEdgeAtEdge.Add(gEdge);
                                gEdge.IncreaseNoOfConditions();
                                int condAtEdge = gEdge.GetNoOfConditions();
                                //Console.WriteLine("conditions at edge ({0}/{1}): {2}", gEdge1.VerticeInd1, gEdge1.VerticeInd2, condAtEdge);
                                Debug.Assert(condAtEdge <= 4);
                                if (condAtEdge == 4) {
                                    //int dir1 = gEdge.GetRefDirection(m_grd, cell1, trafoIdx1);
                                    //int dir2 = gEdge.GetRefDirection(m_grd, cell2, trafoIdx2);
                                    //if (dir1 != dir2)
                                    //    throw new ArgumentException("constrainedDG field: dir1 != dir2");
                                    OverdeterminedEdgeDirection[OverdeterminedCondAtGeomEdge] = gEdge.GetRefDirection(m_grd, cell1, trafoIdx1);
                                    OverdeterminedCondAtGeomEdge++;
                                }
                            }
                        }
                    }


                    {
                        if (!onFixedBoundary && mergeDomain != null)
                            OverdeterminedCondAtVertice++;

                        NodeSet qNds = getEdgeInterpolationNodes(OverdeterminedCondAtVertice, 
                            OverdeterminedCondAtGeomEdge, OverdeterminedEdgeDirection);
                        //NodeSet qNds = getEdgeInterpolationNodes(0, 0);
                        AcceptedNodes.Add(qNds);

                        //if (qNds != null) {
                        //    Console.WriteLine("continuity at edge {0}: numVcond = {1}, numEcond = {2}, nodeCount = {3}",
                        //        j, OverdeterminedCondAtVertice, OverdeterminedCondAtGeomEdge, qNds.NoOfNodes);
                        //} else {
                        //    Console.WriteLine("no continuity at edge {0}: numVcond = {1}, numEcond = {2}",
                        //        j, OverdeterminedCondAtVertice, OverdeterminedCondAtGeomEdge);
                        //}
                    }

                }
            }


        }

        Tuple<int, long> assembleConstrainsMatrix(BlockMsrMatrix A, EdgeMask constrainsEdges, List<NodeSet> AcceptedNodes,
            int countOffset = 0, long _nodeCountOffset = 0) {

            int count = countOffset;
            long _nodeCount = _nodeCountOffset;
            foreach (var chunk in constrainsEdges) {
                foreach (int j in chunk.Elements) {

                    int cell1 = m_grd.Edges.CellIndices[j, 0];
                    int cell2 = m_grd.Edges.CellIndices[j, 1];

                    // set continuity constraints
                    NodeSet qNodes = AcceptedNodes.ElementAt(count);
                    if (qNodes == null)
                        break;

                    var results = m_Basis.EdgeEval(qNodes, j, 1);

                    for (int qN = 0; qN < qNodes.NoOfNodes; qN++) {
                        // Cell1   
                        for (int p = 0; p < this.m_Basis.GetLength(cell1); p++) {
                            A[_nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell1, p)] = results.Item1[0, qN, p];
                        }
                        // Cell2
                        for (int p = 0; p < this.m_Basis.GetLength(cell2); p++) {
                            A[_nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell2, p)] = -results.Item2[0, qN, p];
                        }
                    }
                    count++;
                    _nodeCount += qNodes.NoOfNodes;
                }
            }

            return new Tuple<int, long>(count, _nodeCount);
        }

        Tuple<int, long> assembleConstrainsMatrix_nonZeroConstrains(BlockMsrMatrix A, EdgeMask constrainsEdges, List<NodeSet> AcceptedNodes,
            CellMask mask, DGField internalProj, double[] RHS_non0c, int countOffset = 0, long _nodeCountOffset = 0) {

            int count = countOffset;
            long _nodeCount = _nodeCountOffset;
            foreach (var chunk in constrainsEdges) {
                foreach (int j in chunk.Elements) {

                    int cell1 = m_grd.Edges.CellIndices[j, 0];
                    int cell2 = m_grd.Edges.CellIndices[j, 1];

                    bool isCell1;
                    if (mask.Contains(cell1)) {
                        isCell1 = true;
                    } else if (mask.Contains(cell2)) {
                        isCell1 = false;
                    } else
                        throw new ArgumentException("fixed boundary not within mask for projection");

                    // set continuity constraints
                    NodeSet qNodes = AcceptedNodes.ElementAt(count);
                    if (qNodes == null)
                        break;

                    var resultsA = m_Basis.EdgeEval(qNodes, j, 1);

                    MultidimensionalArray valueIN = MultidimensionalArray.Create(1, qNodes.NoOfNodes);
                    MultidimensionalArray valueOT = MultidimensionalArray.Create(1, qNodes.NoOfNodes);

                    internalProj.EvaluateEdge(j, 1, qNodes, valueIN, valueOT, null, null, null, null, 0, 0.0);

                    for (int qN = 0; qN < qNodes.NoOfNodes; qN++) {
                        // jCell   
                        for (int p = 0; p < this.m_Basis.GetLength(cell1); p++) {
                            if (isCell1)
                                A[_nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell1, p)] = resultsA.Item1[0, qN, p];
                            else
                                A[_nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell2, p)] = resultsA.Item2[0, qN, p];
                        }
                        // non-zero equality constrain
                        RHS_non0c[_nodeCount + qN] = valueIN[0, qN];
                    }
                    count++;
                    _nodeCount += qNodes.NoOfNodes;
                }
            }

            return new Tuple<int, long>(count, _nodeCount);
        }



        class GeometricVerticeForProjection {

            int VerticeIndex;

            int NoOfConditions;

            public GeometricVerticeForProjection(int vertInd) {
                VerticeIndex = vertInd;
                NoOfConditions = 0;
            }

            public void IncreaseNoOfConditions() {
                this.NoOfConditions++;
            }

            public int GetNoOfConditions() {
                return this.NoOfConditions;
            }

            public override bool Equals(Object obj) {

                if (obj is GeometricVerticeForProjection) {
                    return (this.VerticeIndex - ((GeometricVerticeForProjection)obj).VerticeIndex) == 0;
                } else if (obj is int) {
                    return (this.VerticeIndex - (int)obj) == 0;
                } else
                    throw new ArgumentException("wrong type of object");
             
            }

        }

        class GeometricEdgeForProjection {

            public int VerticeInd1;
            public int VerticeInd2;

            List<int> edgeList;
            int NoOfConditions;

            public GeometricEdgeForProjection(int vertInd1, int vertInd2) {
                this.VerticeInd1 = vertInd1;
                this.VerticeInd2 = vertInd2;
                edgeList = new List<int>();
                NoOfConditions = 0;
            }

            public override bool Equals(object obj) {

                if (obj is GeometricEdgeForProjection) {

                    if ((this.VerticeInd1 == ((GeometricEdgeForProjection)obj).VerticeInd1 
                        && this.VerticeInd2 == ((GeometricEdgeForProjection)obj).VerticeInd2)
                        || (this.VerticeInd1 == ((GeometricEdgeForProjection)obj).VerticeInd2 
                        && this.VerticeInd2 == ((GeometricEdgeForProjection)obj).VerticeInd1))
                        return true;
                    else
                        return false;

                } else
                    throw new ArgumentException("wrong type of object");
            }

            public void AddEdge(int jEdge) {
                if (edgeList.Count == 4)
                    throw new InvalidOperationException("edgeList: maximum count of 4 elements reached");
                edgeList.Add(jEdge);
            }

            public List<int> GetEdgeList() {
                return edgeList;
            }

            public void IncreaseNoOfConditions() {
                this.NoOfConditions++;
            }

            public int GetNoOfConditions() {
                return this.NoOfConditions;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public int GetRefDirection(GridData m_grd, int jCell, int iFace) {

                double[] vertCoord1 = m_grd.Vertices.Coordinates.ExtractSubArrayShallow(this.VerticeInd1, -1).To1DArray();
                double[] vertCoord2 = m_grd.Vertices.Coordinates.ExtractSubArrayShallow(this.VerticeInd2, -1).To1DArray();

                int D = vertCoord1.Length;
                MultidimensionalArray vertCoord1_glb = MultidimensionalArray.Create(1, D);
                MultidimensionalArray vertCoord2_glb = MultidimensionalArray.Create(1, D);
                vertCoord1_glb.SetRow<double[]>(0, vertCoord1);
                vertCoord2_glb.SetRow<double[]>(0, vertCoord2);

                MultidimensionalArray vertCoord1_loc = MultidimensionalArray.Create(1, 1, D);
                MultidimensionalArray vertCoord2_loc = MultidimensionalArray.Create(1, 1, D);

                m_grd.TransformGlobal2Local(vertCoord1_glb, vertCoord1_loc, jCell, 1, 0);
                m_grd.TransformGlobal2Local(vertCoord2_glb, vertCoord2_loc, jCell, 1, 0);
                double[] vertCellCoord1 = vertCoord1_loc.ExtractSubArrayShallow(0, 0, -1).To1DArray();
                double[] vertCellCoord2 = vertCoord2_loc.ExtractSubArrayShallow(0, 0, -1).To1DArray();

                // identify face ref vertices
                NodeSet refV = m_grd.Edges.EdgeRefElements[0].Vertices;
                NodeSet refVvol = m_grd.Edges.EdgeRefElements[0].Vertices.GetVolumeNodeSet(m_grd, iFace);
                //var trafo = m_grd.Edges.Edge2CellTrafos[iFace];
                int idx1 = -1; int idx2 = -1;
                for (int i = 0; i < refV.Lengths[0]; i++) {
                    double dist1 = 0.0; double dist2 = 0.0;
                    for (int d = 0; d < vertCoord1.Length; d++) {
                        dist1 += (vertCellCoord1[d] - refVvol[i,d]).Pow2();
                        dist2 += (vertCellCoord2[d] - refVvol[i,d]).Pow2();
                    }
                    dist1 = dist1.Sqrt();
                    if (dist1 < 1.0e-12)
                        idx1 = i;
                    dist2 = dist2.Sqrt();
                    if (dist2 < 1.0e-12)
                        idx2 = i;            
                }
                Debug.Assert(idx1 != idx2);

                // identify direction of edge in ref element
                double dist = 0.0;
                for (int d = 0; d < refV.Lengths[1]; d++) {
                    dist += (refV[idx1,d] - refV[idx2,d]).Pow2();
                }
                dist = dist.Sqrt();
                int dir = -1;
                for (int d = 0; d < refV.Lengths[1]; d++) {
                    if (Math.Abs(Math.Abs(refV[idx1, d] - refV[idx2, d]) - dist) < 1.0e-15) {
                        dir = d;
                    }
                }

                return dir;

            }

        }


        List<GeometricEdgeForProjection> GetGeometricEdgesForCell(int[] vertAtCell) {

            List<GeometricEdgeForProjection> geomEdges = new List<GeometricEdgeForProjection>();

            //int[] vertAtCell = m_grd.Cells.CellVertices[jCell];
            for (int v1 = 0; v1 < vertAtCell.Length; v1++) {
                double[] vertCoord1 = m_grd.Vertices.Coordinates.ExtractSubArrayShallow(vertAtCell[v1], -1).To1DArray();
                for (int v2 = v1+1; v2 < vertAtCell.Length; v2++) {
                    double[] vertCoord2 = m_grd.Vertices.Coordinates.ExtractSubArrayShallow(vertAtCell[v2], -1).To1DArray();
                    double dist = 0.0;
                    for (int d = 0; d < vertCoord1.Length; d++) {
                        dist += (vertCoord2[d] - vertCoord1[d]).Pow2();
                    }
                    dist = dist.Sqrt();
                    bool isEdge = false;
                    //int dir = -1;
                    for (int d = 0; d < vertCoord1.Length; d++) {
                        if (Math.Abs(Math.Abs(vertCoord2[d] - vertCoord1[d]) - dist) < 1.0e-15) {
                            isEdge = true;
                            //dir = d;
                        }
                    }
                    if (isEdge) {
                        GeometricEdgeForProjection gEdge = new GeometricEdgeForProjection(vertAtCell[v1], vertAtCell[v2]);
                        //Console.WriteLine("define edge ({0},{1}) in direction {2}", gEdge.VerticeInd1, gEdge.VerticeInd2, dir);
                        geomEdges.Add(gEdge);
                    }
                }
            }

            return geomEdges;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="numVCond"></param>
        /// <param name="numECond"></param>
        /// <returns></returns>
        private NodeSet getEdgeInterpolationNodes(int numVcond, int numEcond, int[] edgeOrientation = null) {

            int degree = m_Basis.Degree;

            switch (m_grd.SpatialDimension) {
                case 2: {
                    if (numVcond > degree) {
                        return null;
                    } else {
                        QuadRule quad = m_grd.Edges.EdgeRefElements[0].GetQuadratureRule((degree - numVcond) * 2);
                        return quad.Nodes;
                    }
                }
                case 3: {

                    QuadRule quad1D = m_grd.Edges.EdgeRefElements[0].FaceRefElement.GetQuadratureRule(degree * 2);
                    NodeSet qNodes = quad1D.Nodes;
                    int Nnds = ((degree + 1) * (degree + 2) / 2); 

                    int degreeR = degree - numEcond; 
                    int NoNdsR = ((degreeR + 1) * (degreeR + 2) / 2);
                    if (NoNdsR <= 0) {
                        return null;

                    } else {
                        if (edgeOrientation == null && numEcond > 0)
                            throw new ArgumentException();
                        if (edgeOrientation == null && numEcond < 1)
                            edgeOrientation = new int[degree + 1];

                        MultidimensionalArray nds = MultidimensionalArray.Create(Nnds, 2);
                        int node = 0;
                        int[] dirCount = new int[2];
                        for (int dirIdx = 0; dirIdx < edgeOrientation.Length; dirIdx++) {
                            int dir = edgeOrientation[dirIdx];
                            int m = dirCount[dir];
                            int n0 = dirCount[(dir == 0) ? 1 : 0];
                            int nL = quad1D.NoOfNodes - dirIdx;
                            for (int n = n0; n < n0 + nL; n++) {
                                if (dir == 0) {
                                    nds[node, 0] = qNodes[n, 0];
                                    nds[node, 1] = qNodes[m, 0];
                                }
                                if (dir == 1) {
                                    nds[node, 0] = qNodes[m, 0];
                                    nds[node, 1] = qNodes[n, 0];
                                }
                                node++;
                            }
                            dirCount[dir]++;
                        }
                        MultidimensionalArray ndsR = nds.ExtractSubArrayShallow(new int[] {Nnds - NoNdsR, 0 }, new int[] { Nnds - 1, 1 });
                        //Console.WriteLine("No ndsR = {0}", ndsR.Lengths[0]);
                        return new NodeSet(m_grd.Edges.EdgeRefElements[0], ndsR);
                    }
                }
                default:
                    throw new NotSupportedException("spatial dimension not supported");
            }

        }


        /// <summary>
        /// Accumulate the internal continuous field to a DG Field
        /// </summary>
        /// <param name="alpha">Scaling factor</param>
        /// <param name="DGField">output</param>
        /// <param name="mask"></param>
        public void AccToDGField(double alpha, ConventionalDGField DGField, CellMask mask = null) {
            if (!DGField.Basis.Equals(this.m_Basis))
                throw new ArgumentException("Basis does not match.");

            if (mask == null) {
                mask = CellMask.GetFullMask(m_grd);
            }

            foreach (var chunk in mask) {
                int j0 = chunk.i0;
                int JE = chunk.JE;
                for (int j = j0; j < JE; j++) {
                    // collect coordinates for cell j
                    int N = DGField.Basis.GetLength(j);
                    double[] CDGcoord = new double[N];
                    for (int n = 0; n < N; n++) {
                        CDGcoord[n] = m_Coordinates[m_Mapping.LocalUniqueCoordinateIndex(0, j, n)];
                        //CDGcoord[n] = m_Coordinates[CellMask2Coord[j] + n];
                    }

                    double[] DGcoord = new double[N];
                    DGField.Coordinates.GetRow(j, DGcoord);

                    DGcoord.AccV(alpha, CDGcoord);

                    DGField.Coordinates.SetRow(j, DGcoord);

                }
            }

        }
    }


    /// <summary>
    /// continuous DG field via L2-projection with continuity constraints
    /// old variant without patch-wise procedure (old geometric approach)
    /// </summary>
    public class ConstrainedDGField2 {


        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="b"></param>
        public ConstrainedDGField2(Basis b) {
            m_Basis = b;
            m_grd = (GridData)b.GridDat;
            m_Mapping = new UnsetteledCoordinateMapping(b);
            m_Coordinates = MultidimensionalArray.Create(m_Mapping.LocalLength);
        }

        Basis m_Basis;

        public Basis Basis {
            get {
                return m_Basis;
            }
        }

        GridData m_grd;

        UnsetteledCoordinateMapping m_Mapping;

        MultidimensionalArray m_Coordinates;

        public MultidimensionalArray Coordinates {
            get {
                return m_Coordinates;
            }
        }

        ISparseSolver m_OpSolver;

        int[] GlobalVert2Local;



        /// <summary>
        /// linear solver for the quadratic optimization problem, matrix A has to be defined! 
        /// </summary>
        public ilPSP.LinSolvers.ISparseSolver OpSolver {
            get {
                if (m_OpSolver == null) {
                    var solver = new ilPSP.LinSolvers.PARDISO.PARDISOSolver();
                    m_OpSolver = solver;
                }

                return m_OpSolver;
            }
        }

        /// <summary>
        /// Projects some DG field <paramref name="DGField"/> onto the internal, continuous representation
        /// </summary>
        /// <param name="DGField">
        /// input; unchanged on exit
        /// </param>
        /// <param name="mask"></param>
        public void ProjectDGField(ConventionalDGField DGField, CellMask mask = null) {
            if (DGField.Basis.Degree > this.m_Basis.Degree)
                throw new ArgumentException("continuous projection on a lower degree basis is not recommended");
            this.Coordinates.Clear(); // clear internal state, to get the same result for the same input every time

            if (mask == null) {
                mask = CellMask.GetFullMask(m_grd);
            }
            if (mask.NoOfItemsLocally.MPISum() <= 0) {
                throw new ArgumentOutOfRangeException("Domain mask cannot be empty.");
            }

            // hack
            //CellMask2Coord = new int[m_grd.Cells.NoOfLocalUpdatedCells];
            List<int> maskedVert = new List<int>();
            //int numCoord = 0;
            foreach (var chunk in mask) {
                int j0 = chunk.i0;
                int jE = chunk.JE;
                for (int j = j0; j < jE; j++) {
                    //CellMask2Coord[j] = numCoord;
                    //numCoord += DGField.Basis.GetLength(j);
                    int[] vertAtCell = m_grd.Cells.CellVertices[j];
                    foreach (int vert in vertAtCell) {
                        if (!maskedVert.Contains(vert)) {
                            maskedVert.Add(vert);
                        }
                    }
                }
            }
            //m_Coordinates = MultidimensionalArray.Create(numCoord);
            GlobalVert2Local = new int[m_grd.Vertices.NoOfNodes4LocallyUpdatedCells];
            int localVertInd = 0;
            foreach (int vert in maskedVert) {
                GlobalVert2Local[vert] = localVertInd;
                localVertInd++;
            }


            int degree = m_Basis.Degree;

            // get DG-coordinates (change of basis for projection on a higher polynomial degree)
            foreach (var chunk in mask) {
                int j0 = chunk.i0;
                int jE = chunk.JE;
                for (int j = j0; j < jE; j++) {
                    int N = DGField.Basis.GetLength(j);
                    for (int n = 0; n < N; n++) {
                        m_Coordinates[m_Mapping.LocalUniqueCoordinateIndex(0, j, n)] = DGField.Coordinates[j, n];
                        //m_Coordinates[CellMask2Coord[j] + n] = DGField.Coordinates[j, n];
                    }
                }
            }

            // construction of constraints matrix A
            // ====================================

            SubGrid maskSG = new SubGrid(mask);
            EdgeMask innerEM = maskSG.InnerEdgesMask;


            // get interpolation points for the continuity constraints
            NodeSet qNodes = getEdgeInterpolationNodes(0, 0);


            //MultidimensionalArray B = MultidimensionalArray.Create(innerEM.NoOfItemsLocally * qNodes.NoOfNodes, (int)m_Mapping.GlobalCount);
            //MultidimensionalArray B = MultidimensionalArray.Create(innerEM.NoOfItemsLocally * qNodes.NoOfNodes, m_Coordinates.Length);
            List<int> AcceptedEdges = new List<int>();
            List<NodeSet> AcceptedNodes = new List<NodeSet>();



            int nodeCount = 0;
            //int NumVert = m_grd.Vertices.NoOfNodes4LocallyUpdatedCells;
            //MultidimensionalArray CondAtVert = MultidimensionalArray.Create(NumVert, 3);    // 1. index: local vertice index / 2. index: 0 = actual cond, 1 = potential own cond, 2 = potential cond
            //MultidimensionalArray CondIncidenceMatrix = MultidimensionalArray.Create(NumVert, NumVert, 3);  // upper triangular matrix in the first two indices
            int NumVert = localVertInd;
            MultidimensionalArray CondAtVert = MultidimensionalArray.Create(NumVert, 3);
            MultidimensionalArray CondIncidenceMatrix = MultidimensionalArray.Create(NumVert, NumVert, 3);

            int[][] EdgeSend = m_grd.Edges.EdgeSendLists;
            int[][] EdgeInsert = m_grd.Edges.EdgeInsertLists;
            List<int> InterprocEdges = new List<int>();
            List<List<int>> VertAtInterprocEdges = new List<List<int>>();
            bool isInterprocEdge;
            int neighbourProc;
            int[] local2Interproc = new int[NumVert];
            local2Interproc.SetAll(-1);
            List<List<int>> ProcsAtInterprocVert = new List<List<int>>();
            //bool nonConformEdge;
            //BitArray CellsAtNonConform = new BitArray(m_grd.Cells.NoOfLocalUpdatedCells);
            //List<int> nonConformEdges = new List<int>();
            //List<List<int>> VertAtNonConformEdges = new List<List<int>>();

            // local edges per process
            foreach (var chunk in innerEM) {
                int j0 = chunk.i0;
                int jE = chunk.JE;
                for (int j = j0; j < jE; j++) {

                    var edgeInfo = m_grd.Edges.Info[j];

                    // check for interprocess edges to be processed in a second step
                    isInterprocEdge = false;
                    neighbourProc = -1;
                    if (edgeInfo.HasFlag(EdgeInfo.Interprocess)) {
                        isInterprocEdge = true;
                        InterprocEdges.Add(j);
                        //foreach (int[] list in externalEdges) {
                        //    if (list != null && list.Contains(j)) {
                        //        InterprocEdges.Remove(j);
                        //    }
                        //}
                        for (int iL = 0; iL < EdgeSend.Count(); iL++) {
                            int[] sendList = EdgeSend[iL];
                            int[] insertList = EdgeInsert[iL];
                            if ((sendList != null && sendList.Contains(j)) || (insertList != null && insertList.Contains(j))) {
                                if (sendList != null && sendList.Contains(j)) {     // find owned edge
                                    InterprocEdges.Remove(j);
                                }
                                neighbourProc = iL;
                                break;
                            }
                        }
                    }


                    int cell1 = m_grd.Edges.CellIndices[j, 0];
                    int cell2 = m_grd.Edges.CellIndices[j, 1];


                    // check for cells which are located at a non-conformal cell (hanging nodes)
                    //nonConformEdge = false;
                    //if (edgeInfo.HasFlag(EdgeInfo.Cell1_Nonconformal)) {
                    //    CellsAtNonConform[cell2] = true;
                    //    nonConformEdges.Add(j);
                    //    nonConformEdge = true;
                    //} else if (edgeInfo.HasFlag(EdgeInfo.Cell2_Nonconformal)) {
                    //    CellsAtNonConform[cell1] = true;
                    //    nonConformEdges.Add(j);
                    //    nonConformEdge = true;
                    //};


                    // check the conditions at vertices (actual, own, potential)
                    int[] vertAtCell1 = m_grd.Cells.CellVertices[cell1];
                    int[] vertAtCell2 = m_grd.Cells.CellVertices[cell2];
                    int numVCond = 0;
                    List<int> VertAtEdge = new List<int>();
                    for (int i = 0; i < vertAtCell1.Length; i++) {
                        int vert = vertAtCell1[i];
                        if (vertAtCell2.Contains(vert)) {
                            VertAtEdge.Add(vert);
                            CondAtVert[GlobalVert2Local[vert], 2] += 1;   // potential condition at vert
                            if (!isInterprocEdge) { // && !nonConformEdge) {
                                CondAtVert[GlobalVert2Local[vert], 0] += 1;   // actual condition at vert 
                                CondAtVert[GlobalVert2Local[vert], 1] += 1;   // potential own cond at vert (local edge)
                            } else {
                                if (InterprocEdges.Contains(j)) {
                                    CondAtVert[GlobalVert2Local[vert], 1] += 1;   // potential own cond at vert (interproc edge)
                                }
                                if (local2Interproc[GlobalVert2Local[vert]] == -1) {
                                    local2Interproc[GlobalVert2Local[vert]] = ProcsAtInterprocVert.Count();
                                    List<int> procsAtVert = new List<int>();
                                    procsAtVert.Add(neighbourProc);
                                    ProcsAtInterprocVert.Add(procsAtVert);
                                } else {
                                    List<int> procsAtVert = ProcsAtInterprocVert.ElementAt(local2Interproc[GlobalVert2Local[vert]]);
                                    if (!procsAtVert.Contains(neighbourProc)) {
                                        procsAtVert.Add(neighbourProc);
                                    }
                                }
                            }
                            if (CondAtVert[GlobalVert2Local[vert], 0] == 4) {
                                numVCond++;
                            }
                        }
                    }
                    //if (nonConformEdge) {
                    //    VertAtNonConformEdges.Add(VertAtEdge);
                    //}
                    if (isInterprocEdge && InterprocEdges.Contains(j)) {
                        VertAtInterprocEdges.Add(VertAtEdge);
                    }


                    // check for overdetermined edges (additional for 3D)
                    int numECond = 0;
                    int[] edgeOrientation = new int[2];
                    if (m_grd.SpatialDimension == 3) {
                        int i0 = 0;
                        for (int indV1 = 0; indV1 < 4; indV1++) {
                            i0++;
                            for (int indV2 = i0; indV2 < 4; indV2++) {
                                int m, n;
                                if (VertAtEdge.ElementAt(indV1) <= VertAtEdge.ElementAt(indV2)) {
                                    m = VertAtEdge.ElementAt(indV1);
                                    n = VertAtEdge.ElementAt(indV2);
                                } else {
                                    m = VertAtEdge.ElementAt(indV2);
                                    n = VertAtEdge.ElementAt(indV1);
                                }
                                int m_loc = GlobalVert2Local[m];
                                int n_loc = GlobalVert2Local[n];
                                CondIncidenceMatrix[m_loc, n_loc, 2] += 1;  // potential condition at edge
                                if (!isInterprocEdge) {
                                    CondIncidenceMatrix[m_loc, n_loc, 0] += 1;   // actual condition at edge 
                                    CondIncidenceMatrix[m_loc, n_loc, 1] += 1;   // potential own cond at edge (local edge/face)
                                } else {
                                    if (InterprocEdges.Contains(j)) {
                                        CondIncidenceMatrix[m_loc, n_loc, 1] += 1;   // potential own cond at vert (interproc edge)
                                    }
                                }
                                if (CondIncidenceMatrix[m_loc, n_loc, 0] == 4) {
                                    numECond++;
                                    if ((m_grd.Vertices.Coordinates[m, 0] - m_grd.Vertices.Coordinates[n, 0]) < 1.0e-15) {
                                        edgeOrientation[0] = 1;
                                    } else if ((m_grd.Vertices.Coordinates[m, 1] - m_grd.Vertices.Coordinates[n, 1]) < 1.0e-15) {
                                        edgeOrientation[1] = 1;
                                    } else {
                                        throw new ApplicationException("should not occur");
                                    }
                                }
                            }
                        }
                    }


                    if (!isInterprocEdge) { // && !nonConformEdge) {

                        AcceptedEdges.Add(j);

                        //qNodes = getEdgeInterpolationNodes(numVCond, numECond, edgeOrientation);
                        qNodes = getEdgeInterpolationNodes(numVCond, numECond);
                        AcceptedNodes.Add(qNodes);

                        //if (qNodes != null) {
                        //    Console.WriteLine("proc {0}: numECond = {1}, No of qNodes = {2}", m_grd.MpiRank, numECond, qNodes.NoOfNodes);
                        //} else {
                        //    Console.WriteLine("proc {0}: numECond = {1}, No qNodes", m_grd.MpiRank, numECond);
                        //}

                        if (qNodes != null) {

                            //// set continuity constraints
                            //var results = m_Basis.EdgeEval(qNodes, j, 1);

                            //for (int qN = 0; qN < qNodes.NoOfNodes; qN++) {
                            //    // Cell1
                            //    for (int p = 0; p < this.m_Basis.GetLength(cell1); p++) {
                            //        //B[nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell1, p)] = results.Item1[0, qN, p];
                            //        B[nodeCount + qN, CellMask2Coord[cell1]] = results.Item1[0, qN, p];
                            //    }
                            //    // Cell2
                            //    for (int p = 0; p < this.m_Basis.GetLength(cell2); p++) {
                            //        //B[nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell2, p)] = -results.Item2[0, qN, p];
                            //        B[nodeCount + qN, CellMask2Coord[cell2]] = results.Item1[0, qN, p];
                            //    }
                            //}
                            nodeCount += qNodes.NoOfNodes;
                        }
                    }

                }
            }

            #region hanging nodes

            //BitArray ishangingNode = new BitArray(m_grd.Vertices.NoOfNodes4LocallyUpdatedCells);

            //// get edges at hanging nodes
            //CellMask CaNCmsk = new CellMask(m_grd, CellsAtNonConform);
            //SubGrid CaNCsgrd = new SubGrid(CaNCmsk);
            //EdgeMask hangingEdges = CaNCsgrd.InnerEdgesMask;

            //// find corresponding hanging nodes
            //int[] hangVert2NonConformCell = new int[m_grd.Vertices.NoOfNodes4LocallyUpdatedCells];
            //hangVert2NonConformCell.SetAll(-1);
            //foreach (var chunk in hangingEdges) {
            //    int j0 = chunk.i0;
            //    int jE = chunk.JE;
            //    for (int j = j0; j < jE; j++) {

            //        int cell1 = m_grd.Edges.CellIndices[j, 0];
            //        int cell2 = m_grd.Edges.CellIndices[j, 1];
            //        int[] cell1_neighbours = m_grd.Cells.CellNeighbours[cell1];
            //        int[] cell2_neighbours = m_grd.Cells.CellNeighbours[cell2];
            //        int NonConformCell = -1;
            //        for (int i = 0; i < cell1_neighbours.Length; i++) {
            //            int cell = cell1_neighbours[i];
            //            if (cell2_neighbours.Contains(cell)) {
            //                NonConformCell = cell;
            //                break;
            //            }
            //        }
            //        if (NonConformCell > -1) {

            //            int[] vertAtCell1 = m_grd.Cells.CellVertices[cell1];
            //            int[] vertAtCell2 = m_grd.Cells.CellVertices[cell2];
            //            for (int i = 0; i < vertAtCell1.Length; i++) {
            //                int vert = vertAtCell1[i];
            //                if (vertAtCell2.Contains(vert) && m_grd.Vertices.VerticeToCell[vert].Length == 2) {
            //                    ishangingNode[vert] = true;
            //                    hangVert2NonConformCell[vert] = NonConformCell;
            //                    break;
            //                }
            //            }

            //        }
            //    }
            //}


            //// non-confrom edges (only for single core in 2D so far!)
            //foreach (int j in nonConformEdges) {

            //    int cell1 = m_grd.Edges.CellIndices[j, 0];
            //    int cell2 = m_grd.Edges.CellIndices[j, 1];
            //    int hangCell;
            //    int NonConformCell;
            //    if (CellsAtNonConform[cell1]) {
            //        hangCell = cell1;
            //        NonConformCell = cell2;
            //    } else {
            //        hangCell = cell2;
            //        NonConformCell = cell1;
            //    }

            //    int[] vertAtHCell = m_grd.Cells.CellVertices[hangCell];
            //    int[] vertAtNcCell = m_grd.Cells.CellVertices[NonConformCell];
            //    int numVCond = 0;
            //    foreach (int vert in vertAtHCell) {
            //        if (vertAtNcCell.Contains(vert)) {
            //            CondAtVert[vert, 0] += 1;
            //            CondAtVert[vert, 1] += 1;
            //            CondAtVert[vert, 2] += 1;
            //            if (CondAtVert[vert, 0] > maxVCond) {
            //                numVCond++;
            //            }
            //        }
            //        if (ishangingNode[vert] && hangVert2NonConformCell[vert] == NonConformCell) {
            //            CondAtVert[vert, 0] += 1;
            //            CondAtVert[vert, 1] += 1;
            //            CondAtVert[vert, 2] += 1;
            //            if (CondAtVert[vert, 0] == maxVCond) {
            //                numVCond++;
            //            }
            //        }
            //    }


            //    qNodes = getEdgeInterpolationNodes(numVCond, 0);

            //    if (qNodes != null) {

            //        // set continuity constraints
            //        var results = m_Basis.EdgeEval(qNodes, j, 1);

            //        for (int qN = 0; qN < qNodes.NoOfNodes; qN++) {
            //            // Cell1
            //            for (int p = 0; p < this.m_Basis.GetLength(cell1); p++) {
            //                B[nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell1, p)] = results.Item1[0, qN, p];
            //            }
            //            // Cell2
            //            for (int p = 0; p < this.m_Basis.GetLength(cell2); p++) {
            //                B[nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell2, p)] = -results.Item2[0, qN, p];
            //            }
            //        }
            //        nodeCount += qNodes.NoOfNodes;
            //    }


            //}



            // additional conditions for hanging nodes
            //ishangingNode = new BitArray(m_grd.Vertices.NoOfNodes4LocallyUpdatedCells);

            // get edges at hanging nodes
            //CellMask CaNCmsk = new CellMask(m_grd, CellsAtNonConform);
            //SubGrid CaNCsgrd = new SubGrid(CaNCmsk);
            //EdgeMask hangingEdges = CaNCsgrd.InnerEdgesMask;

            //// find corresponding hanging nodes
            //hangVert2hangEdge = new int[m_grd.Vertices.NoOfNodes4LocallyUpdatedCells];
            //hangVert2hangEdge.SetAll(-1);         
            //hangVert2NonConformCell = new int[m_grd.Vertices.NoOfNodes4LocallyUpdatedCells];
            //hangVert2NonConformCell.SetAll(-1);
            //int[] hangEdge2hangVert = new int[hangingEdges.NoOfItemsLocally];
            //int Eind = 0;
            //int HNcount = 0;
            //foreach (var chunk in hangingEdges) {
            //    int j0 = chunk.i0;
            //    int jE = chunk.JE;
            //    for (int j = j0; j < jE; j++) {

            //        int cell1 = m_grd.Edges.CellIndices[j, 0];
            //        int cell2 = m_grd.Edges.CellIndices[j, 1];
            //        int[] cell1_neighbours = m_grd.Cells.CellNeighbours[cell1];
            //        int[] cell2_neighbours = m_grd.Cells.CellNeighbours[cell2];
            //        int NonConformCell = -1;
            //        for (int i = 0; i < cell1_neighbours.Length; i++) {
            //            int cell = cell1_neighbours[i];
            //            if (cell2_neighbours.Contains(cell)) {
            //                NonConformCell = cell;
            //                break;
            //            }
            //        }
            //        if (NonConformCell > -1) {

            //            int[] vertAtCell1 = m_grd.Cells.CellVertices[cell1];
            //            int[] vertAtCell2 = m_grd.Cells.CellVertices[cell2];
            //            for (int i = 0; i < vertAtCell1.Length; i++) {
            //                int vert = vertAtCell1[i];
            //                if (vertAtCell2.Contains(vert) && m_grd.Vertices.VerticeToCell[vert].Length == 2) {
            //                    ishangingNode[vert] = true;
            //                    hangVert2hangEdge[vert] = j;
            //                    hangVert2NonConformCell[vert] = NonConformCell;
            //                    hangEdge2hangVert[Eind] = vert;
            //                    HNcount++;
            //                    break;
            //                }
            //            }

            //        } else {
            //            hangEdge2hangVert[Eind] = -1;
            //        }
            //        Eind++;
            //    }
            //}

            // compute derivatives of the basis polynomials along on spatial direction
            //PolynomialList[,] edgeDeriv = ComputePartialDerivatives(m_Basis);

            // compute additional constraints at hanging edges
            //var Trafo = m_grd.ChefBasis.Scaling;
            //MultidimensionalArray B2 = MultidimensionalArray.Create(HNcount * m_Basis.Degree, (int)m_Mapping.GlobalCount);
            //Eind = 0;
            //HNcount = 0;
            //foreach (var chunk in hangingEdges) {
            //    int j0 = chunk.i0;
            //    int jE = chunk.JE;
            //    for (int j = j0; j < jE; j++) {

            //        if (hangEdge2hangVert[Eind] != -1) {

            //            int cell1 = m_grd.Edges.CellIndices[j, 0];
            //            int cell2 = m_grd.Edges.CellIndices[j, 1];

            //            int trf1 = m_grd.Edges.Edge2CellTrafoIndex[j, 0];
            //            int trf2 = m_grd.Edges.Edge2CellTrafoIndex[j, 1];

            //            MultidimensionalArray hNd_global = m_grd.Vertices.Coordinates.ExtractSubArrayShallow(new int[] { hangEdge2hangVert[Eind], 0 },
            //                                                                        new int[] { hangEdge2hangVert[Eind], m_grd.SpatialDimension - 1 });

            //            MultidimensionalArray hND_local1 = MultidimensionalArray.Create(1, 1, m_grd.SpatialDimension);
            //            MultidimensionalArray hND_local2 = MultidimensionalArray.Create(1, 1, m_grd.SpatialDimension);

            //            m_grd.TransformGlobal2Local(hNd_global, hND_local1, cell1, 1, 0);
            //            m_grd.TransformGlobal2Local(hNd_global, hND_local2, cell2, 1, 0);

            //            NodeSet hangNode1 = new NodeSet(m_grd.Grid.GetRefElement(0), hND_local1.ExtractSubArrayShallow(0, -1, -1));
            //            NodeSet hangNode2 = new NodeSet(m_grd.Grid.GetRefElement(0), hND_local2.ExtractSubArrayShallow(0, -1, -1));

            //            int derivInd = -1;
            //            for (int d = 0; d < m_grd.SpatialDimension; d++) {
            //                if (m_grd.Edges.NormalsForAffine[j, d] != 0)
            //                    derivInd = d;
            //            }

            //            for (int drv = 0; drv < m_Basis.Degree; drv++) {
            //                MultidimensionalArray Res = MultidimensionalArray.Create(1, m_Basis.Polynomials[0].Count);
            //                edgeDeriv[drv, derivInd].Evaluate(hangNode1, Res);
            //                for (int p = 0; p < m_Basis.Polynomials[0].Count; p++) {
            //                    B2[HNcount * m_Basis.Degree + drv, m_Mapping.GlobalUniqueCoordinateIndex(0, cell1, p)] = Res[0, p] * Trafo[cell1];
            //                }
            //                Res.Clear();
            //                edgeDeriv[drv, derivInd].Evaluate(hangNode2, Res);
            //                for (int p = 0; p < m_Basis.Polynomials[0].Count; p++) {
            //                    B2[HNcount * m_Basis.Degree + drv, m_Mapping.GlobalUniqueCoordinateIndex(0, cell2, p)] = -Res[0, p] * Trafo[cell2];
            //                }
            //            }
            //            HNcount++;
            //        }
            //        Eind++;
            //    }
            //}


            //// non-confrom edges (only for single core in 2D so far!)
            //int edge = 0;
            //foreach (int j in nonConformEdges) {

            //    List<int> vertAtEdge = VertAtNonConformEdges.ElementAt(edge);
            //    bool internalNonConformEdge = true;
            //    if (vertAtEdge.Count > 0) {
            //        internalNonConformEdge = false;
            //    }

            //    if (!internalNonConformEdge) {

            //        int cell1 = m_grd.Edges.CellIndices[j, 0];
            //        int cell2 = m_grd.Edges.CellIndices[j, 1];
            //        int hangCell;
            //        int NonConformCell;
            //        if (CellsAtNonConform[cell1]) {
            //            hangCell = cell1;
            //            NonConformCell = cell2;
            //        } else {
            //            hangCell = cell2;
            //            NonConformCell = cell1;
            //        }

            //        int hangNode = -1;
            //        foreach (var vert in m_grd.Cells.CellVertices[hangCell]) {
            //            if (ishangingNode[vert] && hangVert2NonConformCell[vert] == NonConformCell) {
            //                hangNode = vert;
            //            }
            //        }

            //        if (hangNode == -1) {
            //            throw new ArgumentOutOfRangeException("should not happen");
            //        }

            //        bool NonConfromEdgeIsProcessed = false;
            //        if (CondAtVert[hangNode, 0] == maxVCond) {
            //            NonConfromEdgeIsProcessed = true;
            //        }


            //        if (!NonConfromEdgeIsProcessed) {

            //            int numVCond = 0;
            //            foreach (int vert in vertAtEdge) {
            //                CondAtVert[vert, 0] += 1;
            //                CondAtVert[vert, 1] += 1;
            //                CondAtVert[vert, 2] += 1;
            //                if (CondAtVert[vert, 0] > maxVCond) {
            //                    numVCond++;
            //                }
            //            }

            //            CondAtVert[hangNode, 0] += 1;
            //            CondAtVert[hangNode, 1] += 1;
            //            CondAtVert[hangNode, 2] += 1;

            //            ProcessNonConformEdge(hangNode, hangCell, NonConformCell,  ref numVCond);


            //            qNodes = getEdgeInterpolationNodes(numVCond, 0);

            //            if (qNodes != null) {

            //                // set continuity constraints
            //                var results = m_Basis.EdgeEval(qNodes, j, 1);

            //                for (int qN = 0; qN < qNodes.NoOfNodes; qN++) {
            //                    // Cell1
            //                    for (int p = 0; p < this.m_Basis.GetLength(cell1); p++) {
            //                        B[nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell1, p)] = results.Item1[0, qN, p];
            //                    }
            //                    // Cell2
            //                    for (int p = 0; p < this.m_Basis.GetLength(cell2); p++) {
            //                        B[nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell2, p)] = -results.Item2[0, qN, p];
            //                    }
            //                }
            //                nodeCount += qNodes.NoOfNodes;
            //            }

            //        }
            //    }
            //    edge++;
            //}
            #endregion

            //int nodeCount_OnProc = nodeCount;


            // interprocess edges 
            int edge = 0;
            foreach (int j in InterprocEdges) {


                int cell1 = m_grd.Edges.CellIndices[j, 0];
                int cell2 = m_grd.Edges.CellIndices[j, 1];

                int numVCond = 0;
                int numECond = 0;
                List<int> VertAtEdge = VertAtInterprocEdges.ElementAt(edge);

                switch (m_grd.SpatialDimension) {
                    case 1: {
                        throw new NotImplementedException("TODO");
                    }
                    case 2: {
                        // condtions at vertices
                        foreach (int vert in VertAtEdge) {
                            int lvert = GlobalVert2Local[vert];// = vert, vorher IndexOutOfRange exeption
                            numVCond += NegotiateNumVCond(lvert, CondAtVert, ProcsAtInterprocVert[local2Interproc[lvert]]);
                            CondAtVert[lvert, 0] += 1;
                        }
                        break;
                    }
                    case 3: {
                        // conditions at edges 
                        //Console.WriteLine("proc {0}: edge {1}", m_grd.MpiRank, j);
                        int i0 = 0;
                        for (int indV1 = 0; indV1 < VertAtEdge.Count; indV1++) {
                            i0++;
                            for (int indV2 = i0; indV2 < VertAtEdge.Count; indV2++) {
                                int v1 = GlobalVert2Local[VertAtEdge.ElementAt(indV1)];
                                int v2 = GlobalVert2Local[VertAtEdge.ElementAt(indV2)];
                                int m, n;
                                if (local2Interproc[v1] >= 0 && local2Interproc[v2] >= 0) {
                                    if (v1 <= v2) {
                                        m = v1;
                                        n = v2;
                                    } else {
                                        m = v2;
                                        n = v1;
                                    }
                                } else {
                                    throw new ArgumentException("should not occur");
                                }

                                numECond += NegotiateNumECond(m, n, CondIncidenceMatrix, ProcsAtInterprocVert[local2Interproc[m]]);
                                CondIncidenceMatrix[m, n, 0] += 1;
                            }
                        }
                        break;
                    }
                }


                //if (VertAtEdge.Count == 4) {
                //    int i0 = 0;
                //    for (int indV1 = 0; indV1 < VertAtEdge.Count; indV1++) {
                //        i0++;
                //        for (int indV2 = i0; indV2 < VertAtEdge.Count; indV2++) {
                //            int m, n;
                //            if (VertAtEdge.ElementAt(indV1) <= VertAtEdge.ElementAt(indV2)) {
                //                m = VertAtEdge.ElementAt(indV1);
                //                n = VertAtEdge.ElementAt(indV2);
                //            } else {
                //                m = VertAtEdge.ElementAt(indV2);
                //                n = VertAtEdge.ElementAt(indV1);
                //            }
                //            CondIncidenceMatrix[m, n, 0] += 1;
                //        }
                //    }
                //}

                AcceptedEdges.Add(j);

                qNodes = getEdgeInterpolationNodes(numVCond, numECond);
                AcceptedNodes.Add(qNodes);

                //if (qNodes != null) {
                //    Console.WriteLine("proc {0}: numECond = {1}, No of qNodes = {2} - interproc Edge", m_grd.MpiRank, numECond, qNodes.NoOfNodes);
                //} else {
                //    Console.WriteLine("proc {0}: numECond = {1}, No qNodes - interproc Edge", m_grd.MpiRank, numECond);
                //}

                if (qNodes != null) {

                    // set continuity constraints
                    //var results = m_Basis.EdgeEval(qNodes, j, 1);

                    //for (int qN = 0; qN < qNodes.NoOfNodes; qN++) {
                    //    // Cell1
                    //    for (int p = 0; p < this.m_Basis.GetLength(cell1); p++) {
                    //        B[nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell1, p)] = results.Item1[0, qN, p];
                    //    }
                    //    // Cell2
                    //    for (int p = 0; p < this.m_Basis.GetLength(cell2); p++) {
                    //        B[nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell2, p)] = -results.Item2[0, qN, p];
                    //    }
                    //}
                    nodeCount += qNodes.NoOfNodes;
                }
                edge++;

            }


            Partitioning rowPart = new Partitioning(nodeCount);
            MsrMatrix A = new MsrMatrix(rowPart, m_Mapping);

            int count = 0;
            long _nodeCount = A.RowPartitioning.i0; // start at global index
            foreach (int j in AcceptedEdges) {

                int cell1 = m_grd.Edges.CellIndices[j, 0];
                int cell2 = m_grd.Edges.CellIndices[j, 1];


                // set continuity constraints
                qNodes = AcceptedNodes.ElementAt(count);

                var results = m_Basis.EdgeEval(qNodes, j, 1);

                for (int qN = 0; qN < qNodes.NoOfNodes; qN++) {
                    // Cell1       
                    for (int p = 0; p < this.m_Basis.GetLength(cell1); p++) {
                        A[_nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell1, p)] = results.Item1[0, qN, p];
                    }
                    // Cell2
                    for (int p = 0; p < this.m_Basis.GetLength(cell2); p++) {
                        A[_nodeCount + qN, m_Mapping.GlobalUniqueCoordinateIndex(0, cell2, p)] = -results.Item2[0, qN, p];
                    }
                }
                count++;
                _nodeCount += qNodes.NoOfNodes;



            }


            //Partitioning rowPart = new Partitioning(nodeCount);
            //MsrMatrix A = new MsrMatrix(rowPart, m_Mapping);
            //A.AccBlock(rowPart.i0, 0, 1.0, B.ExtractSubArrayShallow(new int[] { 0, 0 }, new int[] { nodeCount - 1, (int)m_Mapping.GlobalCount - 1 }));
            //MsrMatrix A = new MsrMatrix(nodeCount, m_Coordinates.Length, 1, 1);
            //A.AccBlock(0, 0, 1.0, B.ExtractSubArrayShallow(new int[] { 0, 0 }, new int[] { nodeCount - 1, m_Coordinates.Length - 1 }));
            //A.AccBlock(rowPart.i0 + nodeCount, 0, 1.0, B2);

            //A.SaveToTextFile("C:\\tmp\\AMatrix.txt");

            //// test with matlab
            //MultidimensionalArray output = MultidimensionalArray.Create(1, 2);
            //Console.WriteLine("Calling MATLAB/Octave...");
            //using (BatchmodeConnector bmc = new BatchmodeConnector()) {
            //    bmc.PutSparseMatrix(A, "A");
            //    bmc.Cmd("rank_A = rank(full(A))");
            //    bmc.Cmd("rank_AT = rank(full(A'))");
            //    bmc.GetMatrix(output, "[rank_A, rank_AT]");

            //    bmc.Execute(false);
            //}

            //Console.WriteLine("A: No of Rows = {0}; rank = {1}", A.NoOfRows, output[0, 0]);
            //Console.WriteLine("AT: No of Rows = {0}; rank = {1}", A.Transpose().NoOfRows, output[0, 1]);


            //var r = IMatrixExtensions.ReducedRowEchelonForm(Amtx);
            //Console.WriteLine("rank of Amtx = {0}", r.Item4);
            //Console.WriteLine("No of rows of reduced row-echelon form = {0}", r.Item1.Lengths[0]);
            //Console.WriteLine("No of columns of reduced row-echelon form = {0}", r.Item1.Lengths[1]);


            // solve
            MsrMatrix AAT = A * A.Transpose();

            double condNum = AAT.condest();
            Console.WriteLine("Condition Number of AAT is " + condNum);

            double[] RHS = new double[rowPart.LocalLength];
            A.SpMVpara(1.0, m_Coordinates.To1DArray(), 0.0, RHS);

            double[] v = new double[rowPart.LocalLength];
            double[] x = new double[m_Coordinates.Length];

            //OpSolver = new ilPSP.LinSolvers.PARDISO.PARDISOSolver();

            OpSolver.DefineMatrix(AAT);
            OpSolver.Solve(v, RHS);
            OpSolver.Dispose();

            A.Transpose().SpMVpara(-1.0, v, 0.0, x);

            m_Coordinates.AccVector(1.0, x);

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="numVCond"></param>
        /// <param name="numECond"></param>
        /// <returns></returns>
        private NodeSet getEdgeInterpolationNodes(int numVcond, int numEcond, int[] edgeOrientation = null) {

            int degree = m_Basis.Degree;

            switch (m_grd.SpatialDimension) {
                case 2: {
                    if (numVcond > degree) {
                        return null;
                    } else {
                        QuadRule quad = m_grd.Edges.EdgeRefElements[0].GetQuadratureRule((degree - numVcond) * 2);
                        return quad.Nodes;
                    }
                }
                case 3: {

                    QuadRule quad1D = m_grd.Edges.EdgeRefElements[0].FaceRefElement.GetQuadratureRule(degree * 2);
                    NodeSet qNodes = quad1D.Nodes;
                    int Nnds = ((degree + 1) * (degree + 2) / 2);

                    int degreeR = degree - numEcond;
                    int NoNdsR = ((degreeR + 1) * (degreeR + 2) / 2);
                    if (NoNdsR <= 0) {
                        return null;

                    } else {
                        if (edgeOrientation == null && numEcond > 0)
                            throw new ArgumentException();
                        if (edgeOrientation == null && numEcond < 1)
                            edgeOrientation = new int[degree + 1];

                        MultidimensionalArray nds = MultidimensionalArray.Create(Nnds, 2);
                        int node = 0;
                        int[] dirCount = new int[2];
                        for (int dirIdx = 0; dirIdx < edgeOrientation.Length; dirIdx++) {
                            int dir = edgeOrientation[dirIdx];
                            int m = dirCount[dir];
                            int n0 = dirCount[(dir == 0) ? 1 : 0];
                            int nL = quad1D.NoOfNodes - dirIdx;
                            for (int n = n0; n < n0 + nL; n++) {
                                if (dir == 0) {
                                    nds[node, 0] = qNodes[n, 0];
                                    nds[node, 1] = qNodes[m, 0];
                                }
                                if (dir == 1) {
                                    nds[node, 0] = qNodes[m, 0];
                                    nds[node, 1] = qNodes[n, 0];
                                }
                                node++;
                            }
                            dirCount[dir]++;
                        }
                        MultidimensionalArray ndsR = nds.ExtractSubArrayShallow(new int[] { Nnds - NoNdsR, 0 }, new int[] { Nnds - 1, 1 });
                        //Console.WriteLine("No ndsR = {0}", ndsR.Lengths[0]);
                        return new NodeSet(m_grd.Edges.EdgeRefElements[0], ndsR);
                    }
                }
                default:
                    throw new NotSupportedException("spatial dimension not supported");
            }

        }


        private int NegotiateNumVCond(int vert, MultidimensionalArray CondAtVert, List<int> procsAtVert = null) {


            if (CondAtVert[vert, 2] == 2) {

                if (procsAtVert.Count == 1) {
                    if (CondAtVert[vert, 1] == 2) {
                        if (CondAtVert[vert, 0] == 1) {
                            return 1;
                        }
                    }
                } else if (procsAtVert.Count == 2) {
                    if (procsAtVert.Min() > m_grd.MpiRank) {
                        if (CondAtVert[vert, 0] == CondAtVert[vert, 1] - 1) {
                            return 1;
                        }
                    }
                }
            }

            if (CondAtVert[vert, 2] == 3) {

                if (procsAtVert.Count == 1) {
                    if (CondAtVert[vert, 1] == 3) {
                        if (CondAtVert[vert, 0] == 2) {
                            return 1;
                        }
                    } else if (CondAtVert[vert, 1] == 2) {
                        if (procsAtVert.Min() > m_grd.MpiRank) {
                            return 1;
                        }
                    }

                } else if (procsAtVert.Count == 2) {
                    if (CondAtVert[vert, 0] == CondAtVert[vert, 1] - 1) {
                        return 1;
                    }
                }

            }

            if (CondAtVert[vert, 2] == 4) {
                if (CondAtVert[vert, 1] == 4) {
                    if (CondAtVert[vert, 0] == 3) {
                        return 1;
                    }
                }
                if (CondAtVert[vert, 1] == 3) {
                    if (CondAtVert[vert, 0] == 2) {
                        return 1;
                    }
                }
            }


            return 0;
        }

        private int NegotiateNumECond(int m, int n, MultidimensionalArray CondIncidenceMatrix, List<int> procsAtVertm = null, List<int> procsAtVertn = null) {

            //int numECond = 0;

            //int i0 = 0;
            //for (int indV1 = 0; indV1 < VertAtEdge.Count; indV1++) {
            //    i0++;
            //    for (int indV2 = i0; indV2 < VertAtEdge.Count; indV2++) {
            //        int m, n;
            //        if (VertAtEdge.ElementAt(indV1) <= VertAtEdge.ElementAt(indV2)) {
            //            m = VertAtEdge.ElementAt(indV1);
            //            n = VertAtEdge.ElementAt(indV2);
            //        } else {
            //            m = VertAtEdge.ElementAt(indV2);
            //            n = VertAtEdge.ElementAt(indV1);
            //        }

            if (CondIncidenceMatrix[m, n, 2] > 1) {
                //Console.WriteLine("proc {0}: condAtEdge ({1}/{2}/{3})", m_grd.MpiRank, CondIncidenceMatrix[m, n, 0], CondIncidenceMatrix[m, n, 1], CondIncidenceMatrix[m, n, 2]);
            }

            if (CondIncidenceMatrix[m, n, 2] == 2) {
                if (CondIncidenceMatrix[m, n, 1] == 2) {
                    if (CondIncidenceMatrix[m, n, 0] == 1) {
                        //Console.WriteLine("numVCond++");
                        return 1;
                    }
                }
            }


            if (CondIncidenceMatrix[m, n, 2] == 3) {
                if (CondIncidenceMatrix[m, n, 1] == 3) {
                    if (CondIncidenceMatrix[m, n, 0] == 2) {
                        //Console.WriteLine("numVCond++");
                        return 1;
                    }
                } else if (CondIncidenceMatrix[m, n, 1] == 2) {
                    if (procsAtVertm.Min() > m_grd.MpiRank) {
                        //Console.WriteLine("numVCond++");
                        return 1;
                    }
                    //if (m < m_grd.Vertices.NodePartitioning.LocalLength && n < m_grd.Vertices.NodePartitioning.LocalLength) {   // proc owns both vert
                    //    return 1;
                    //} else if (m < m_grd.Vertices.NodePartitioning.LocalLength && n >= m_grd.Vertices.NodePartitioning.LocalLength) {
                    //    int proc = 0;
                    //    foreach (int[] list in m_grd.Vertices.VertexInsertLists) {
                    //        if (list != null && list.Contains(n)) {
                    //            if (m_grd.MpiRank < proc) {
                    //                return 1;
                    //            }
                    //        }
                    //        proc++;
                    //    }
                    //} else if (m >= m_grd.Vertices.NodePartitioning.LocalLength && n < m_grd.Vertices.NodePartitioning.LocalLength) {
                    //    int proc = 0;
                    //    foreach (int[] list in m_grd.Vertices.VertexInsertLists) {
                    //        if (list != null && list.Contains(m)) {
                    //            if (m_grd.MpiRank < proc) {
                    //                return 1;
                    //            }
                    //        }
                    //        proc++;
                    //    }
                    //}
                }
            }


            if (CondIncidenceMatrix[m, n, 2] == 4) {
                if (CondIncidenceMatrix[m, n, 1] == 4) {
                    if (CondIncidenceMatrix[m, n, 0] == 3) {
                        //Console.WriteLine("numVCond++");
                        return 1;
                    }
                }
                if (CondIncidenceMatrix[m, n, 1] == 3) {
                    if (CondIncidenceMatrix[m, n, 0] == 2) {
                        //Console.WriteLine("numVCond++");
                        return 1;
                    }
                }
            }

            //    }
            //}

            return 0;
        }


        /// <summary>
        /// Accumulate the internal continuous field to a DG Field
        /// </summary>
        /// <param name="alpha">Scaling factor</param>
        /// <param name="DGField">output</param>
        /// <param name="mask"></param>
        public void AccToDGField(double alpha, ConventionalDGField DGField, CellMask mask = null) {
            if (!DGField.Basis.Equals(this.m_Basis))
                throw new ArgumentException("Basis does not match.");

            if (mask == null) {
                mask = CellMask.GetFullMask(m_grd);
            }

            foreach (var chunk in mask) {
                int j0 = chunk.i0;
                int JE = chunk.JE;
                for (int j = j0; j < JE; j++) {
                    // collect coordinates for cell j
                    int N = DGField.Basis.GetLength(j);
                    double[] CDGcoord = new double[N];
                    for (int n = 0; n < N; n++) {
                        CDGcoord[n] = m_Coordinates[m_Mapping.LocalUniqueCoordinateIndex(0, j, n)];
                        //CDGcoord[n] = m_Coordinates[CellMask2Coord[j] + n];
                    }

                    double[] DGcoord = new double[N];
                    DGField.Coordinates.GetRow(j, DGcoord);

                    DGcoord.AccV(alpha, CDGcoord);

                    DGField.Coordinates.SetRow(j, DGcoord);

                }
            }

        }


    }
}
