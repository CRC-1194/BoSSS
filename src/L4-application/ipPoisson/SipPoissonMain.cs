/* =======================================================================
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
using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.IO;
using BoSSS.Foundation.Quadrature;
using BoSSS.Solution;
using BoSSS.Solution.Utils;
using ilPSP.LinSolvers;
using ilPSP.Tracing;
using ilPSP.Utils;
using System.Diagnostics;
using MPI.Wrappers;
using BoSSS.Platform;
using ilPSP;
using System.Linq;
using NUnit.Framework;
using BoSSS.Solution.AdvancedSolvers;
using ilPSP.Connectors.Matlab;
using BoSSS.Foundation.Grid.Aggregation;
using BoSSS.Solution.Control;
using BoSSS.Solution.Statistic;
using System.IO;

namespace BoSSS.Application.SipPoisson {

    /// <summary>
    /// Benchmark application, solves a Poisson problem using the symmetric interior penalty (SIP) method.
    /// </summary>
    public class SipPoissonMain : Application<SipControl> {

#pragma warning disable 649
        /// <summary>
        /// dependent variable
        /// </summary>
        [InstantiateFromControlFile("T", "T", IOListOption.ControlFileDetermined)]
        protected SinglePhaseField T;

        /// <summary>
        /// exact solution, to determine L2-Error, see also <see cref="SipControl.ExactSolution_provided"/>.
        /// </summary>
        [InstantiateFromControlFile("Tex", "Tex", IOListOption.Never)]
        protected SinglePhaseField Tex;

        /// <summary>
        /// dependent variable
        /// </summary>
        [InstantiateFromControlFile("RHS", "T", IOListOption.ControlFileDetermined)]
        protected SinglePhaseField RHS;

        
#pragma warning restore 649

        /// <summary>
        /// Solver residual, for a DG discretization which is one degree higher than the degree of the solution
        /// </summary>
        private SinglePhaseField ResiualKP1;

        /// <summary>
        /// error of the numerical solution
        /// </summary>
        private SinglePhaseField Error;

        /// <summary>
        /// MPI rank coloring
        /// </summary>
        private SinglePhaseField MPIrank;

        /// <summary>
        /// DG field instantiation
        /// </summary>
        protected override void CreateFields() {
            base.CreateFields();

            ResiualKP1 = new SinglePhaseField(new Basis(this.GridData, T.Basis.Degree + 1), "ResidualKP1");
            base.IOFields.Add(ResiualKP1);

            Error = new SinglePhaseField(new Basis(this.GridData, Math.Max(T.Basis.Degree + 1, Tex.Basis.Degree)), "Error");
            base.m_IOFields.Add(Error);

            // MPI rank coloring
            MPIrank = new SinglePhaseField(new Basis(this.GridData, 0), "MPIRank");
            MPIrank.AccConstant(this.MPIRank);

            // mg coloring
            int iLevel = 0;
            this.MGColoring.Clear();
            foreach (var MgL in this.MultigridSequence) {
                SinglePhaseField c = new SinglePhaseField(new Basis(this.GridData, 0), "MgLevel_" + iLevel);
                Foundation.Grid.Aggregation.CoarseningAlgorithms.ColorDGField(MgL, c);
                this.MGColoring.Add(c);
                base.IOFields.Add(c);
                iLevel++;
            }

            Console.WriteLine("Available multi-grid levels: " + this.MGColoring.Count);
        }

        /*
        unsafe static void my_dgemm(int TRANSA, int TRANSB,
                                        int M, int N, int K,
                                        double ALPHA,
                                        double* A, int LDA,
                                        double* B, int LDB,
                                        double BETA,
                                        double* C, int LDC) {
            for(int m = 0; m < M; m++) {
                for(int n = 0; n < N; n++) {
                    double acc = 0;
                    for(int k = 0; k < K; k++) {
                        acc += A[m * K + k] * B[k * N + n];
                    }
                    C[m * N + n] = BETA * C[m * N + n] + ALPHA * acc;
                }
            }

        }
        */

#if !DEBUG
        static void MyHandler(object sender, UnhandledExceptionEventArgs args) {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("MyHandler caught : " + e.Message);
            Console.WriteLine("Runtime terminating: {0}", args.IsTerminating);
            System.Environment.Exit(-1234);
        }
#endif

        /// <summary>
        /// Ensures availability of <see cref="BoSSS.Solution.Statistic.ForeignGridValue"/>
        /// </summary>
        public Type EnsureReference = typeof(ForeignGridValue);


        /// <summary>
        /// Main routine
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args) {
            //BoSSS.Application.SipPoisson.Tests.TestProgram.Init();
            //BoSSS.Application.SipPoisson.Tests.TestProgram.TestOperatorScaling2D(1);
            //BoSSS.Application.SipPoisson.Tests.TestProgram.TestIterativeSolver(3, 8, 3, LinearSolverCode.exp_Kcycle_schwarz);
            //BoSSS.Application.SipPoisson.Tests.TestProgram.TestIterativeSolver(3, 8, 3, LinearSolverCode.exp_softpcg_schwarz_directcoarse);
            //BoSSS.Application.SipPoisson.Tests.TestProgram.Cleanup();
            //BoSSS.Application.SipPoisson.Tests.TestProgram.TestIterativeSolver(2, 40, 2, LinearSolverCode.exp_Kcycle_schwarz);
            //Assert.AreEqual(1, 2, "Remove Me!!");

            string si3 = System.Environment.GetEnvironmentVariable ("BOSSS_INSTALL");
            string pp = System.Environment.GetEnvironmentVariable ("PATH");
            si3 = si3 != null ? si3 : "NIX";
            pp = pp != null ? pp : "NIX";
            Console.WriteLine ("BOSSS_INSTALL : " + si3);

            


            /*
            //Some performance testing - don't delete, I still need this!
            //Florian


            ilPSP.Environment.Bootstrap(
                args,
                GetBoSSSInstallDir(),
                out bool _MustFinalizeMPI);
            {
                int rank, size;
                csMPI.Raw.Comm_Rank(csMPI.Raw._COMM.WORLD, out rank);
                csMPI.Raw.Comm_Size(csMPI.Raw._COMM.WORLD, out size);
                if (rank == 0) {
                    Console.WriteLine("BoSSS: Running with " + size + " MPI process(es)");
                }
            }


            //MultidimensionalArray.MultiplyProgram mp = MultidimensionalArray.MultiplyProgram.Compile("imn", "kma", "ikna"); // original sort
            MultidimensionalArray.MultiplyProgram mp = MultidimensionalArray.MultiplyProgram.Compile("mni", "mka", "kani"); // re-sort for GEMM

            int I = 1;
            int D = 3;
            int K = 343;
            int N = 84;

            Console.WriteLine("Complexity: " + (I*N*N*K*D));

            //var R = MultidimensionalArray.Create(I, N, N); //    original sort
            //var V = MultidimensionalArray.Create(K, N, D); //    original sort
            //var Z = MultidimensionalArray.Create(I, K, N, D); // original sort
            var R = MultidimensionalArray.Create(N, N, I); //    re-sort for GEMM
            var V = MultidimensionalArray.Create(N, K, D); //    re-sort for GEMM
            var Z = MultidimensionalArray.Create(K, D, N, I); // re-sort for GEMM

            var rnd = new Random();

            R.ApplyAll(delegate (int[] index, ref double entry) {
                entry = rnd.NextDouble();
            });

            Stopwatch myDings = new Stopwatch();
            Stopwatch blasDings = new Stopwatch();

            int Ntests = 500000;

            for (int l = 0; l < 2; l++) {
                for (int i = 0; i < Ntests; i++) {

                    if (l > 0)
                        myDings.Start();
                    R.Multiply(0.5, V, Z, 0.5, ref mp);
                    //R.Multiply(0.5, V, Z, 0.5, "imn", "kma", "ikna");
                    if (l > 0)
                        myDings.Stop();

                    if (l > 0)
                        blasDings.Start();
                    unsafe {
                        fixed (double* pR = R.Storage, pV = V.Storage, pZ = Z.Storage) {
                            BLAS.dgemm(
                            //my_dgemm(
                                'N', 'N', 
                                N, N * I, K * D, 
                                0.5, 
                                pV, N, 
                                pZ, K * D, 
                                0.5, 
                                pR, N);
                        }
                    }
                    if (l > 0)
                        blasDings.Stop();

                    R.CheckForNanOrInf();
                }
            }

            double MyDing = myDings.Elapsed.TotalSeconds / Ntests;
            double BlasGing = blasDings.Elapsed.TotalSeconds / Ntests;

            Console.WriteLine("BoSSS [sec]: " + MyDing);
            Console.WriteLine("BLAS [sec]:  " + BlasGing + "   (" + (MyDing/BlasGing) + " faster)");

            return;
            */


            _Main(args, false, delegate () {
                SipPoissonMain p = new SipPoissonMain();
                Console.WriteLine("ipPoisson: " + ilPSP.Environment.MPIEnv.MPI_Rank + " of " + ilPSP.Environment.MPIEnv.MPI_Size
                    + " on compute node '" + ilPSP.Environment.MPIEnv.Hostname + "';");
                return p;
            });
        }

        /// <summary>
        /// Sets the multigrid coloring
        /// </summary>
        protected override void SetInitial() {
#if !DEBUG
            //this will suppress exception prompts
            //Workaround to prevent disturbance while executing batch-client
            if (this.Control.SuppressExceptionPrompt) {
                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);
            }
#endif

            base.SetInitial();

            

            //TexactFine = (SinglePhaseField)(GetDatabase().Sessions.First().Timesteps.Last().Fields.Where(fi => fi.Identification == "T"));
        }

        ///// <summary>
        ///// Hack - some precise solution on a finer grid.
        ///// </summary>
        //SinglePhaseField TexactFine;

        /// <summary>
        /// LHS of the equation <see cref="LaplaceMtx"/>*<see cref="T"/> + <see cref="LaplaceAffine"/> = <see cref="RHS"/>.
        /// </summary>
        BlockMsrMatrix LaplaceMtx;

        /// <summary>
        /// Part of the RHS which contains e.g. boundary conditions; still on LHS, must be subtracted from RHS of the equation.
        /// <see cref="LaplaceMtx"/>*<see cref="T"/> + <see cref="LaplaceAffine"/> = <see cref="RHS"/>
        /// </summary>
        double[] LaplaceAffine;

        /// <summary>
        /// Spatial operator to assemble <see cref="LaplaceMtx"/> and <see cref="LaplaceAffine"/>.
        /// </summary>
        SpatialOperator LapaceIp;

        /// <summary>
        /// Includes assembly of the matrix.
        /// </summary>
        /// <param name="L"></param>
        protected override void CreateEquationsAndSolvers(GridUpdateDataVaultBase L) {
            using (FuncTrace tr = new FuncTrace()) {

                // create operator
                // ===============
                {
                    double D = this.GridData.SpatialDimension;
                    double penalty_base = (T.Basis.Degree + 1) * (T.Basis.Degree + D) / D;
                    double penalty_factor = base.Control.penalty_poisson;

                    BoundaryCondMap<BoundaryType> PoissonBcMap = new BoundaryCondMap<BoundaryType>(this.GridData, this.Control.BoundaryValues, "T");

                    LapaceIp = new SpatialOperator(1, 1, QuadOrderFunc.SumOfMaxDegrees(), "T", "T");

                    MultidimensionalArray LengthScales;
                    if (this.GridData is GridData) {
                        LengthScales = ((GridData)GridData).Cells.cj;
                    } else if (this.GridData is AggregationGridData) {
                        LengthScales = ((AggregationGridData)GridData).AncestorGrid.Cells.cj;
                    } else {
                        throw new NotImplementedException();
                    }

                    var flux = new ipFlux(penalty_base * base.Control.penalty_poisson, LengthScales, PoissonBcMap);

                    LapaceIp.EquationComponents["T"].Add(flux);

                    LapaceIp.Commit();
                }



                //double condNo = LaplaceMtx.condest(BatchmodeConnector.Flavor.Octave);
                //Console.WriteLine("condition number: {0:0.####E-00} ",condNo);

            }
        }

        /// <summary>
        /// computes <see cref="LaplaceMtx"/> and <see cref="LaplaceAffine"/>
        /// </summary>
        private void UpdateMatrices() {
            using (var tr = new FuncTrace()) {
                             
                // time measurement for matrix assembly
                Stopwatch stw = new Stopwatch();
                stw.Start();

                // Stats:
                {
                    int BlkSize = T.Mapping.MaxTotalNoOfCoordinatesPerCell;
                    int NoOfMtxBlocks = 0;
                    foreach (int[] Neigs in this.GridData.iLogicalCells.CellNeighbours) {
                        NoOfMtxBlocks++; //               diagonal block
                        NoOfMtxBlocks += Neigs.Length; // off-diagonal block
                    }
                    NoOfMtxBlocks = NoOfMtxBlocks.MPISum();

                    int MtxBlockSize = BlkSize * BlkSize;
                    int MtxSize = MtxBlockSize * NoOfMtxBlocks;

                    double MtxStorage = MtxSize * (8.0 + 4.0) / (1024 * 1024); // 12 bytes (double+int) per entry

                    Console.WriteLine("   System size:                 {0}", T.Mapping.TotalLength);
                    Console.WriteLine("   No of blocks:                {0}", T.Mapping.TotalNoOfBlocks);
                    Console.WriteLine("   No of blocks in matrix:      {0}", NoOfMtxBlocks);
                    Console.WriteLine("   DG coordinates per cell:     {0}", BlkSize);
                    Console.WriteLine("   Non-zeros per matrix block:  {0}", MtxBlockSize);
                    Console.WriteLine("   Total non-zeros in matrix:   {0}", MtxSize);
                    Console.WriteLine("   Approx. matrix storage (MB): {0}", MtxStorage);


                    base.QueryHandler.ValueQuery("MtxBlkSz", MtxBlockSize, true);
                    base.QueryHandler.ValueQuery("NNZMtx", MtxSize, true);
                    base.QueryHandler.ValueQuery("NNZblk", NoOfMtxBlocks, true);
                    base.QueryHandler.ValueQuery("MtxMB", MtxStorage, true);
                }


                Console.WriteLine("creating sparse system for {0} DOF's ...", T.Mapping.Ntotal);


                // quadrature domain
                var volQrSch = new CellQuadratureScheme(true, CellMask.GetFullMask(this.GridData, MaskType.Geometrical));
                var edgQrSch = new EdgeQuadratureScheme(true, EdgeMask.GetFullMask(this.GridData, MaskType.Geometrical));

#if DEBUG
                // in DEBUG mode, we compare 'MsrMatrix' (old, reference implementation) and 'BlockMsrMatrix' (new standard)
                var RefLaplaceMtx = new MsrMatrix(T.Mapping);
#endif
                using (new BlockTrace("SipMatrixAssembly", tr)) {
                    LaplaceMtx = new BlockMsrMatrix(T.Mapping);
                    LaplaceAffine = new double[T.Mapping.LocalLength];

                    LapaceIp.ComputeMatrixEx(T.Mapping, null, T.Mapping,
                                             LaplaceMtx, LaplaceAffine,
                                             volQuadScheme: volQrSch, edgeQuadScheme: edgQrSch);
                }
#if DEBUG
                LaplaceAffine.ClearEntries();
                LapaceIp.ComputeMatrixEx(T.Mapping, null, T.Mapping,
                                         RefLaplaceMtx, LaplaceAffine,
                                         volQuadScheme: volQrSch, edgeQuadScheme: edgQrSch);
                MsrMatrix ErrMtx = RefLaplaceMtx.CloneAs();
                ErrMtx.Acc(-1.0, LaplaceMtx);
                double err = ErrMtx.InfNorm();
                double infNrm = LaplaceMtx.InfNorm();
                Assert.Less(err, infNrm * 1e-10, "MsrMatrix2 comparison failed.");
#endif
                stw.Stop();
                Console.WriteLine("done {0} sec.", stw.Elapsed.TotalSeconds);

                LaplaceMtx.GetMemoryInfo(out long AllocatedMem, out long UsedMem);
                Console.WriteLine("   Used   matrix storage (MB): {0}", UsedMem /(1024.0*1024));
                Console.WriteLine("   Alloc. matrix storage (MB): {0}", AllocatedMem/(1024.0*1024));
            }
        }

        /// <summary>
        /// Ad-hoc performance measurement routines for <see cref="BlockMsrMatrix"/> operations
        /// </summary>
        void MatrixOpPerf() {
            var M = LaplaceMtx;
            var M2 = M.CloneAs();

            double MatlabSpMMtime = 0.0, MatlabSpMVtime = 0.0;
            /*
            using (var MatlabRef = new BatchmodeConnector()) {
                MultidimensionalArray CheckRes = MultidimensionalArray.Create(1, 4);

                MatlabRef.PutSparseMatrix(M, "M");
                MatlabRef.Cmd("M2 = M;");

                // bench SpMM
                MatlabRef.Cmd("Mprod1 = M * M2;");
                MatlabRef.Cmd("tic");
                MatlabRef.Cmd("Mprod = M * M2;");
                MatlabRef.Cmd("SpMMtime = toc;");

                // bench SpMV
                MatlabRef.Cmd("[L,I] = size(M);");
                MatlabRef.Cmd("x = sin(1:L)';");
                MatlabRef.Cmd("a1 = M*x;");
                MatlabRef.Cmd("tic");
                MatlabRef.Cmd("a = M*x;");
                MatlabRef.Cmd("SpMVtime = toc;");

                MatlabRef.Cmd("CheckRes = [0, 0, SpMVtime, SpMMtime];");
                MatlabRef.Cmd("CheckRes");
                MatlabRef.GetMatrix(CheckRes, "CheckRes");

                MatlabRef.Execute();

                MatlabSpMMtime = CheckRes[0, 3];
                MatlabSpMVtime = CheckRes[0, 2];



            }
            */

            //BlockMsrMatrix.Multiply(M, M2);


            Stopwatch BoSSsSpMMtime = new Stopwatch();
            BoSSsSpMMtime.Start();
            //BlockMsrMatrix.Multiply(M, M2);
            BoSSsSpMMtime.Stop();


            double[] accu = new double[M.RowPartitioning.LocalLength];
            double[] x = new double[M.ColPartition.LocalLength];
            for (int i = 0; i < x.Length; i++) {
                x[i] = Math.Sin(i);
            }
            M.SpMV(1.0, x, 0.0, accu);

            Stopwatch BoSSsSpMVtime = new Stopwatch();
            BoSSsSpMVtime.Start();
            M.SpMV(1.0, x, 0.0, accu);
            BoSSsSpMVtime.Stop();


            Console.WriteLine("Matlab SpMM time: [sec]   " + MatlabSpMMtime);
            Console.WriteLine("BoSSS  SpMM time: [sec]   " + BoSSsSpMMtime.Elapsed.TotalSeconds);

            Console.WriteLine("Matlab SpMV time: [sec]   " + MatlabSpMVtime);
            Console.WriteLine("BoSSS  SpMV time: [sec]   " + BoSSsSpMVtime.Elapsed.TotalSeconds);

            Console.WriteLine("    SpMV total      : [msec] " + BlockMsrMatrix.SPMV_tot.Elapsed.TotalMilliseconds);
            Console.WriteLine("    SpMV   sending  : [msec]    " + BlockMsrMatrix.SpMV_initSending.Elapsed.TotalMilliseconds);
            Console.WriteLine("    SpMV   local    : [msec]    " + BlockMsrMatrix.SpMV_local.Elapsed.TotalMilliseconds);
            Console.WriteLine("    SpMV     inner  : [msec]        " + BlockMsrMatrix.SPMV_inner.Elapsed.TotalMilliseconds);
            Console.WriteLine("    SpMV   receive  : [msec]    " + BlockMsrMatrix.SpMV_receive.Elapsed.TotalMilliseconds);
            Console.WriteLine("    SpMV   external : [msec]    " + BlockMsrMatrix.SpMV_external.Elapsed.TotalMilliseconds);
            Console.WriteLine("    SpMV     idx trf: [msec]        " + BlockMsrMatrix.SpMV_indextrans.Elapsed.TotalMilliseconds);


            Console.WriteLine("entering infinte loop...");
            while (true) ;
            
        }

        /*
        /// <summary>
        /// Deprecated utility, writes matrices for spectral element method.
        /// </summary>
        public void WriteSEMMatrices() {
            int kSEM; // nodes per edge

            if (this.Grid.RefElements[0] is Triangle) {
                kSEM = this.T.Basis.Degree + 1;
            } else if (this.Grid.RefElements[0] is Square) {
                switch (this.T.Basis.Degree) {
                    case 2: kSEM = 2; break;
                    case 3: kSEM = 2; break;
                    case 4: kSEM = 3; break;
                    case 5: kSEM = 3; break;
                    case 6: kSEM = 4; break;
                    default: throw new NotSupportedException();
                }
            } else {
                throw new NotImplementedException();
            }

            SpecFemBasis SEM_basis = new SpecFemBasis((GridData)(this.GridData), kSEM);

            SEM_basis.CellNodes[0].SaveToTextFile("NODES_SEM" + kSEM + ".txt");
            SEM_basis.MassMatrix.SaveToTextFileSparse("MASS_SEM" + kSEM + ".txt");

            var Mod2Nod = SEM_basis.GetModal2NodalOperator(this.T.Basis);
            var Nod2Mod = SEM_basis.GetNodal2ModalOperator(this.T.Basis);

            Mod2Nod.SaveToTextFileSparse("MODAL" + this.T.Basis.Degree + "_TO_NODAL" + kSEM + ".txt");
            Nod2Mod.SaveToTextFileSparse("NODAL" + kSEM + "_TO_MODAL" + this.T.Basis.Degree + ".txt");

            this.LaplaceMtx.SaveToTextFileSparse("OPERATOR" + this.T.Basis.Degree + ".txt");


            {
                var TEST = this.T.CloneAs();
                TEST.Clear();
                TEST.ProjectField((_2D)((x, y) => x * y));

                int J = this.GridData.iGeomCells.Count;
                int N = SEM_basis.NodesPerCell[0];
                MultidimensionalArray TEST_at_NODES = MultidimensionalArray.Create(J, N);

                TEST.Evaluate(0, J, SEM_basis.CellNodes[0], TEST_at_NODES);

                double[] CompareAtNodes = new double[J * N];
                Mod2Nod.SpMVpara(1.0, TEST.CoordinateVector, 0.0, CompareAtNodes);

                double[] gretchen = TEST_at_NODES.ResizeShallow(J * N).To1DArray();

                double fdist = GenericBlas.L2Dist(gretchen, CompareAtNodes);
                Debug.Assert(fdist < 1.0e-8);

                double[] bak = new double[TEST.Mapping.LocalLength];
                Nod2Mod.SpMVpara(1.0, CompareAtNodes, 0.0, bak);

                double hdist = GenericBlas.L2Dist(bak, TEST.CoordinateVector);
                Debug.Assert(hdist < 1.0e-8);
            }


            var Scatter = SEM_basis.GetNodeScatterMatrix();

            Scatter.SaveToTextFileSparse("SCATTER_SEM" + kSEM + ".txt");


            {
                var SEMTEST = new SpecFemField(SEM_basis);
                var csem = SEMTEST.Coordinates;

                Random r = new Random(666);
                for (int i = 0; i < csem.GetLength(0); i++) {
                    csem[i] = r.NextDouble();
                }


                var DG_TEST0 = new SinglePhaseField(SEMTEST.Basis.ContainingDGBasis);
                var DG_TEST = this.T.CloneAs();
                DG_TEST.Clear();
                SEMTEST.AccToDGField(1.0, DG_TEST0);
                DG_TEST.AccLaidBack(1.0, DG_TEST0);

                double[] S2 = new double[Scatter.RowPartitioning.LocalLength];
                double[] S3 = new double[DG_TEST.Mapping.LocalLength];

                Scatter.SpMVpara(1.0, csem.To1DArray(), 0.0, S2);
                Nod2Mod.SpMVpara(1.0, S2, 0.0, S3);

                double gdist = GenericBlas.L2Dist(S3, DG_TEST.CoordinateVector);
                Debug.Assert(gdist < 1.0e-8);
            }
        }

        */
        /// <summary>
        /// control of mesh adaptation
        /// </summary>
        protected override void AdaptMesh(int TimestepNo, out GridCommons newGrid, out GridCorrelation old2NewGrid) {
            if (this.Control.AdaptiveMeshRefinement && TimestepNo > 1) {

                // compute error against fine solution
                if (Control.ExactSolution_provided) {
                    //Error.Clear();
                    //Error.AccLaidBack(1.0, T);

                    /*
                    var eval = new FieldEvaluation((GridData)(TexactFine.GridDat));

                    void FineEval(MultidimensionalArray input, MultidimensionalArray output) {
                        int L = input.GetLength(0);
                        Debug.Assert(output.GetLength(0) == L);

                        eval.Evaluate(1.0, new DGField[] { TexactFine }, input, 0.0, output.ResizeShallow(L, 1));
                    }

                    Error.ProjectField(-1.0, FineEval);
                    */
                    //Error.AccLaidBack(-1.0, Tex);
                }

                int oldJ = this.GridData.CellPartitioning.TotalLength;

                double LocNormPow2 = this.ResiualKP1.CoordinateVector.L2NormPow2(); // norm of residual on this processor
                double TotNormPow2 = LocNormPow2.MPISum(); //                          norm of residual over all processors
                double MeanNormPow2PerCell = TotNormPow2 / oldJ; //                    mean norm per cell

                double maxSoFar = 0;
                int jMax = -1;
                for (int j = 0; j < oldJ; j++) {
                    double CellNorm = Error.Coordinates.GetRow(j).L2NormPow2();

                    if (CellNorm > maxSoFar) {
                        jMax = j;
                        maxSoFar = CellNorm;
                    }
                }


                int MyLevelIndicator(int j, int CurrentLevel) {
                    double CellNorm = this.ResiualKP1.Coordinates.GetRow(j).L2NormPow2();


                    //if (j == 0)
                    //    CurrentLevel = CurrentLevel + 1;

                    //if (CellNorm > MeanNormPow2PerCell * 1.1)
                    //    return CurrentLevel + 1;
                    //else
                    //    return CurrentLevel;
                    if (j == jMax)
                        return CurrentLevel + 1;
                    else
                        return CurrentLevel;
                }


                bool AnyChange = GridRefinementController.ComputeGridChange((GridData)(this.GridData), null, MyLevelIndicator, out List<int> CellsToRefineList, out List<int[]> Coarsening);
                int NoOfCellsToRefine = 0;
                int NoOfCellsToCoarsen = 0;
                if (AnyChange) {
                    int[] glb = (new int[] {
                        CellsToRefineList.Count,
                        Coarsening.Sum(L => L.Length),
                        //0, 0
                    }).MPISum();

                    NoOfCellsToRefine = glb[0];
                    NoOfCellsToCoarsen = glb[1];
                }
                //*/


                // Update Grid
                // ===========

                if (AnyChange) {


                    Console.WriteLine("       Refining " + NoOfCellsToRefine + " of " + oldJ + " cells");
                    Console.WriteLine("       Coarsening " + NoOfCellsToCoarsen + " of " + oldJ + " cells");

                    newGrid = ((GridData)(this.GridData)).Adapt(CellsToRefineList, Coarsening, out old2NewGrid);

                } else {

                    newGrid = null;
                    old2NewGrid = null;
                }
            } else {

                newGrid = null;
                old2NewGrid = null;
            }
        }

        

        protected void CustomItCallback(int iterIndex, double[] currentSol, double[] currentRes, MultigridOperator Mgop) {
            //+1 because of startindex=0 and +1 because lowest level, does not count as mlevel
            
        }

        /*
        protected void GimmeKondnumber(MultigridOperator Mgop, out double[] condests, out int[] DOFs, out int[] Level) {
            MultigridOperator Mgop_ptr = Mgop;

            List<double> _condests = new List<double>();
            List<int> _DOFs = new List<int>();
            List<int> _Level = new List<int>();

            while (Mgop_ptr != null) {
                BatchmodeConnector bmc = new BatchmodeConnector();
                //MultidimensionalArray cond = MultidimensionalArray.Create(1, 1);
                MultidimensionalArray condest = MultidimensionalArray.Create(1, 1);

                //bmc.PutSparseMatrix(Mgop_ptr.OperatorMatrix.ToMsrMatrix(), "Matrix_A");
                //bmc.Cmd("m_condest=condest(Matrix_A)");
                //bmc.GetMatrix(condest, "m_condest");
                //bmc.Execute(false);

                _condests.Add(condest[0, 0]);
                _DOFs.Add(Mgop_ptr.Mapping.LocalLength.MPISum());
                _Level.Add(Mgop_ptr.LevelIndex);

                Mgop_ptr = Mgop_ptr.CoarserLevel;
            }

            condests = _condests.ToArray();
            DOFs = _DOFs.ToArray();
            Level = _Level.ToArray();
        }
        */

        /// <summary>
        /// Single run of the solver
        /// </summary>
        protected override double RunSolverOneStep(int TimestepNo, double phystime, double dt) {
            using (new FuncTrace()) {
                //this.WriteSEMMatrices();

                if (Control.ExactSolution_provided) {
                    Tex.Clear();
                    Tex.ProjectField(this.Control.InitialValues_Evaluators["Tex"]);

                    RHS.Clear();
                    RHS.ProjectField(this.Control.InitialValues_Evaluators["RHS"]);
                }

                if (Control.AdaptiveMeshRefinement == false) {
                    base.NoOfTimesteps = -1;
                    if (TimestepNo > 1)
                        throw new ApplicationException("steady-state-equation.");
                    base.TerminationKey = true;
                }

                // Update matrices
                // ---------------

                UpdateMatrices();


                // call solver
                // -----------
                double mintime, maxtime;
                bool converged;
                int NoOfIterations;

                LinearSolverCode solvercodes = this.Control.LinearSolver.SolverCode;
                switch (solvercodes) {

                    case LinearSolverCode.classic_cg:
                    case LinearSolverCode.classic_mumps:
                    case LinearSolverCode.classic_pardiso:
                        ClassicSolve(out mintime, out maxtime, out converged, out NoOfIterations);
                        break;

                    //case SolverCodes.nix:
                    //NoOfIterations = 0;
                    //mintime = 0;
                    //maxtime = 0;
                    //converged = false;
                    //break;

                    default:
                        ExperimentalSolve(out mintime, out maxtime, out converged, out NoOfIterations);
                        break;
                }

                Console.WriteLine("finished; " + NoOfIterations + " iterations.");
                Console.WriteLine("converged? " + converged);
                Console.WriteLine("Timespan: " + mintime + " to " + maxtime + " seconds");


                base.QueryHandler.ValueQuery("minSolRunT", mintime, true);
                base.QueryHandler.ValueQuery("maxSolRunT", maxtime, true);
                base.QueryHandler.ValueQuery("Conv", converged ? 1.0 : 0.0, true);
                base.QueryHandler.ValueQuery("NoIter", NoOfIterations, true);
                base.QueryHandler.ValueQuery("NoOfCells", this.GridData.CellPartitioning.TotalLength, true);
                base.QueryHandler.ValueQuery("DOFs", T.Mapping.TotalLength, true);
                base.QueryHandler.ValueQuery("BlockSize", T.Basis.Length, true);

                

                if (base.Control.ExactSolution_provided) {
                    Error.Clear();
                    Error.AccLaidBack(1.0, Tex);
                    Error.AccLaidBack(-1.0, T);

                    double L2_ERR = Error.L2Norm();
                    Console.WriteLine("\t\tL2 error on " + this.Grid.NumberOfCells + ": " + L2_ERR);
                    base.QueryHandler.ValueQuery("SolL2err", L2_ERR, true);

                }

                // evaluate residual
                // =================
                {
                    this.ResiualKP1.Clear();
                    if (this.Control.InitialValues_Evaluators.ContainsKey("RHS")) {
                        this.ResiualKP1.ProjectField(this.Control.InitialValues_Evaluators["RHS"]);
                    }

                    var ev = this.LapaceIp.GetEvaluator(T.Mapping, ResiualKP1.Mapping);
                    ev.Evaluate(-1.0, 1.0, ResiualKP1.CoordinateVector);
                }

                // return
                // ======

                return 0.0;
            }
        }

        /// <summary>
        /// Solution of the system
        /// <see cref="LaplaceMtx"/>*<see cref="T"/> + <see cref="LaplaceAffine"/> = <see cref="RHS"/>
        /// using a black-box solver
        /// </summary>
        private void ClassicSolve(out double mintime, out double maxtime, out bool Converged, out int NoOfIter) {

            mintime = double.MaxValue;
            maxtime = double.MinValue;
            Converged = false;
            NoOfIter = int.MaxValue;

            for (int i = 0; i < base.Control.NoOfSolverRuns; i++) {

                // create sparse solver
                // --------------------
                ISparseSolver ipSolver;
                LinearSolverCode solvercodes = this.Control.LinearSolver.SolverCode;

                switch (solvercodes) {
                    case LinearSolverCode.classic_pardiso:
                        ipSolver = new ilPSP.LinSolvers.PARDISO.PARDISOSolver() {
                            CacheFactorization = true,
                            UseDoublePrecision = true
                        };
                        break;

                    case LinearSolverCode.classic_mumps:
                        ipSolver = new ilPSP.LinSolvers.MUMPS.MUMPSSolver();
                        break;

                    case LinearSolverCode.classic_cg:
                        ipSolver = new ilPSP.LinSolvers.monkey.CG() {
                            MaxIterations = 1000000,
                            Tolerance = 1.0e-10,
                            DevType = ilPSP.LinSolvers.monkey.DeviceType.Cuda
                        };
                        break;

                    default:
                        throw new ArgumentException();
                }
                ipSolver.DefineMatrix(LaplaceMtx);

                // call solver
                // -----------

                int NoOfSolverRuns = base.Control.NoOfSolverRuns;
                T.Clear();

                Console.WriteLine("RUN " + i + ": solving system...");

                var RHSvec = RHS.CoordinateVector.ToArray();
                //RHSvec.SaveToTextFile("DG" + this.T.Basis.Degree + "_RHS.txt");
                BLAS.daxpy(RHSvec.Length, -1.0, this.LaplaceAffine, 1, RHSvec, 1);

                T.Clear();
                var solRes = default(SolverResult);
                solRes = ipSolver.Solve(T.CoordinateVector, RHSvec);
                mintime = Math.Min(solRes.RunTime.TotalSeconds, mintime);
                maxtime = Math.Max(solRes.RunTime.TotalSeconds, maxtime);

                //T.CoordinatesAsVector.SaveToTextFile("DG" + this.T.Basis.Degree + "_SOLUTION.txt");

                Converged = solRes.Converged;
                NoOfIter = solRes.NoOfIterations;

                Console.WriteLine("Pardiso phase 11: " + ilPSP.LinSolvers.PARDISO.PARDISOSolver.Phase_11.Elapsed.TotalSeconds);
                Console.WriteLine("Pardiso phase 22: " + ilPSP.LinSolvers.PARDISO.PARDISOSolver.Phase_22.Elapsed.TotalSeconds);
                Console.WriteLine("Pardiso phase 33: " + ilPSP.LinSolvers.PARDISO.PARDISOSolver.Phase_33.Elapsed.TotalSeconds);

                ipSolver.Dispose();
            }
        }

        List<DGField> MGColoring = new List<DGField>();


        MultigridOperator.ChangeOfBasisConfig[][] MgConfig {
            get {
                //Console.WriteLine("Polynomgrad wird nicht mehr reduziert!!!");
                int p = this.T.Basis.Degree;
                int NoOfLevels = this.MultigridSequence.Length;
                var config = new MultigridOperator.ChangeOfBasisConfig[NoOfLevels][];

                for (int iLevel = 0; iLevel < NoOfLevels; iLevel++) {

                    config[iLevel] = new MultigridOperator.ChangeOfBasisConfig[] {
                        new MultigridOperator.ChangeOfBasisConfig() {
                            VarIndex = new int[] {0},
                            mode = MultigridOperator.Mode.DiagBlockEquilib,
                            DegreeS = new int[] { p }
                            //Degree = Math.Max(1, p - iLevel)
                        }
                    };

                }

                return config;
            }

        }

        /*
        private string m_AnalyseOutputpath;

        private string AnalyseOutputpath {
            get {
                return m_AnalyseOutputpath;
            }
            set {
                if (!Directory.Exists(value))
                {
                    m_AnalyseOutputpath = String.Concat(Directory.GetCurrentDirectory(), @"\");
                }
                else
                {
                    m_AnalyseOutputpath = value;
                }
            }
        }
        */

        /// <summary>
        /// Solution of the system
        /// <see cref="LaplaceMtx"/>*<see cref="T"/> + <see cref="LaplaceAffine"/> = <see cref="RHS"/>
        /// using the modular solver framework.
        /// </summary>
        private void ExperimentalSolve(out double mintime, out double maxtime, out bool Converged, out int NoOfIter) {
            using (var tr = new FuncTrace()) {
                
                int p = this.T.Basis.Degree;
                var MgSeq = this.MultigridSequence;
                mintime = double.MaxValue;
                maxtime = 0;
                Converged = false;
                NoOfIter = int.MaxValue;

                Console.WriteLine("Construction of Multigrid basis...");
                Stopwatch mgBasis = new Stopwatch();
                mgBasis.Start();
                AggregationGridBasis[][] AggBasis;
                using (new BlockTrace("Aggregation_basis_init", tr)) {
                    AggBasis = AggregationGridBasis.CreateSequence(MgSeq, new Basis[] { this.T.Basis });
                }
                mgBasis.Stop();
                Console.WriteLine("done. (" + mgBasis.Elapsed.TotalSeconds + " sec)");
          
               
                for (int irun = 0; irun < base.Control.NoOfSolverRuns; irun++) {
                    Stopwatch stw = new Stopwatch();
                    stw.Reset();
                    stw.Start();

                    Console.WriteLine("Setting up multigrid operator...");
                    var mgsetup = new Stopwatch();
                    mgsetup.Start();
                    var MultigridOp = new MultigridOperator(AggBasis, this.T.Mapping, this.LaplaceMtx, null, MgConfig);
                    //double[] condests;
                    //int[] DOFs, Level;
                    //GimmeKondnumber(MultigridOp, out condests, out DOFs, out Level);
                    mgsetup.Stop();
                    Console.WriteLine("done. (" + mgsetup.Elapsed.TotalSeconds + " sec)");


                    Console.WriteLine("Setting up solver...");
                    var solverSetup = new Stopwatch();
                    solverSetup.Start();
                    ISolverSmootherTemplate solver;

                    SolverFactory SF = new SolverFactory(this.Control.NonLinearSolver, this.Control.LinearSolver);

                    T.Clear();
                    T.AccLaidBack(1.0, Tex);

                    
                    ConvergenceObserver CO = null;
                    List<Action<int, double[], double[], MultigridOperator>> ItCallbacks_Kollekte=new List<Action<int, double[], double[], MultigridOperator>>();
                    ItCallbacks_Kollekte.Add(CustomItCallback);

                    ////Check if output analysis path is set, if invalid change to current directory ...
                    //if (this.Control.WriteMeSomeAnalyse != null)
                    //{
                    //    Console.WriteLine("===Analysis-Setup===");
                    //    AnalyseOutputpath = this.Control.WriteMeSomeAnalyse;
                    //    CO = new ConvergenceObserver(MultigridOp, null, T.CoordinateVector.ToArray(), SF);
                    //    CO.TecplotOut = String.Concat(AnalyseOutputpath, "Poisson");
                    //    ItCallbacks_Kollekte.Add(CO.ResItCallbackAtDownstep);
                    //    DeletePreviousOutput();
                    //    Console.WriteLine("Analysis output will be written to: {0}", AnalyseOutputpath);
                    //    Console.WriteLine("====================");
                    //}
                    
                    SF.GenerateLinear(out solver, AggBasis, MgConfig, ItCallbacks_Kollekte);

                    using (new BlockTrace("Solver_Init", tr)) {
                        solver.Init(MultigridOp);
                    }
                    solverSetup.Stop();
                    Console.WriteLine("done. (" + solverSetup.Elapsed.TotalSeconds + " sec)");

                    MultigridOp.GetMemoryInfo(out long AllocMem, out long UsedMem);
                    Console.WriteLine("  Memory reserved|used by multi-grid operator {0:F2} | {1:F2} MB", (double)AllocMem / (1024.0 * 1024.0), (double)UsedMem / (1024.0 * 1024.0));


                    Console.WriteLine("Running solver...");
                    var solverIteration = new Stopwatch();
                    solverIteration.Start();
                    T.Clear();
                    double[] T2 = this.T.CoordinateVector.ToArray();
                    using (new BlockTrace("Solver_Run", tr)) {
                        solver.ResetStat();
                        //Random rnd = new Random();
                        //for(int i = 0; i < T2.Length; i++) {
                        //    T2[i] = rnd.NextDouble();
                        //}
                        //T2.ClearEntries();
                        
                        var RHSvec = RHS.CoordinateVector.ToArray();
                        BLAS.daxpy(RHSvec.Length, -1.0, this.LaplaceAffine, 1, RHSvec, 1);
                        MultigridOp.UseSolver(solver, T2, RHSvec);
                        T.CoordinateVector.SetV(T2);
                    }
                    solverIteration.Stop();
                    Console.WriteLine("done. (" + solverIteration.Elapsed.TotalSeconds + " sec)");

                    Console.WriteLine("  Pardiso phase 11: " + ilPSP.LinSolvers.PARDISO.PARDISOSolver.Phase_11.Elapsed.TotalSeconds);
                    Console.WriteLine("  Pardiso phase 22: " + ilPSP.LinSolvers.PARDISO.PARDISOSolver.Phase_22.Elapsed.TotalSeconds);
                    Console.WriteLine("  Pardiso phase 33: " + ilPSP.LinSolvers.PARDISO.PARDISOSolver.Phase_33.Elapsed.TotalSeconds);
                    Console.WriteLine("  spmm total " + BlockMsrMatrix.multiply.Elapsed.TotalSeconds);
                    Console.WriteLine("  spmm core " + BlockMsrMatrix.multiply_core.Elapsed.TotalSeconds);
                    Console.WriteLine("  spmv total " + BlockMsrMatrix.SPMV_tot.Elapsed.TotalSeconds);
                    Console.WriteLine("  spmv inner " + BlockMsrMatrix.SPMV_inner.Elapsed.TotalSeconds);
                    Console.WriteLine("  spmv outer " + BlockMsrMatrix.SpMV_local.Elapsed.TotalSeconds);

                    // time measurement, statistics
                    stw.Stop();
                    mintime = Math.Min(stw.Elapsed.TotalSeconds, mintime);
                    maxtime = Math.Max(stw.Elapsed.TotalSeconds, maxtime);
                    Converged = solver.Converged;
                    NoOfIter = solver.ThisLevelIterations;

                }
            }
        }

        /*
        private void DeletePreviousOutput() {
            DirectoryInfo Dinfo = new DirectoryInfo(AnalyseOutputpath);
            IEnumerable<FileInfo> Files1 = Dinfo.GetFiles("*.plt");
            IEnumerable<FileInfo> Files2 = Dinfo.GetFiles("*condnum*.txt");
            IEnumerable<FileInfo> Files=Files1.Concat(Files2);
            FileInfo[] FilesToDelete=Files.ToArray();
            Console.Write("delete previous analysisfiles: ");
            foreach (FileInfo file in FilesToDelete)
            {
                File.Delete(file.FullName);
                Console.Write("{0}, ",file.Name);
            }
        }

        /*
        private void HierStimmtWasNichtJens(ConvergenceObserver CO, double[] condests, int[] DOFs, int[] Level) {
            //Do the Convergency Analysis plz ...
            string identify = String.Format("_Res{0}_p{1}_{2}", T.Mapping.TotalLength, T.Basis.Degree, this.Control.LinearSolver.SolverCode.ToString());
            //string analysedatapath = @"E:\Analysis\CCpoisson\Study0_vary_Mlevel_n_blocks\";
            string bla = String.Concat(AnalyseOutputpath, "SIP", identify);
            Console.WriteLine("plotting convergency data ...");
            if (CO != null) {
                //CO.PlotTrend(false, false, true);
                //CO.PlotTrend(true, false, true);
                //CO.PlotDecomposition(T.CoordinateVector.ToArray(),"decomposition"+bla);
                //CO.WriteTrendToCSV(false, true, true, bla + "_res");
                //CO.WriteTrendToCSV(true, true, true, bla + "_err");
            }
           
            string condfile = String.Concat(AnalyseOutputpath, "condnum", identify, ".txt");

            using (StreamWriter CondStream = new StreamWriter(condfile)) {
                string header = String.Format("Level estCond total_DOFs");
                CondStream.WriteLine(header);
                for (int i = 0; i < condests.Length; i++) {
                    string line = String.Format("{0} {1} {2}", Level[i], condests[i], DOFs[i]);
                    Console.WriteLine(line);
                    CondStream.WriteLine(line);
                }
            }
            TimestepNumber tsn = new TimestepNumber(new int[]{0});
            PlotCurrentState(0.0,tsn,0);
        }
        */
        
        /// <summary>
        /// Shutdown function
        /// </summary>
        protected override void Bye() {
            object SolL2err;
            if (this.QueryHandler != null) {
                if (this.QueryHandler.QueryResults.TryGetValue("SolL2err", out SolL2err)) {
                    Console.WriteLine("Value of Query 'SolL2err' " + SolL2err.ToString());
                } else {
                    Console.WriteLine("query 'SolL2err' not found.");
                }
            }
        }

        /// <summary>
        /// Operator stability analysis
        /// </summary>
        override public IDictionary<string,double> OperatorAnalysis() {
            using(new FuncTrace()) {
                var ana = new BoSSS.Solution.AdvancedSolvers.Testing.OpAnalysisBase(
                    this.LaplaceMtx, this.LaplaceAffine,
                    this.T.Mapping,
                    this.MgConfig);

                return ana.GetNamedProperties();
            }
        }

        /// <summary>
        /// default plotting
        /// </summary>
        protected override void PlotCurrentState(double phystime, TimestepNumber timestepNo, int superSampling = 0) {
            string caseStr = "";
            if (base.Control.Paramstudy_CaseIdentification != null) {
                var pstudy_case = base.Control.Paramstudy_CaseIdentification.FirstOrDefault(tt => tt.Item1 == "pstudy_case");
                if (pstudy_case != null) {
                    caseStr = "." + pstudy_case.Item2;
                }
            }

            DGField[] Fields = new DGField[] { T, Tex, RHS, ResiualKP1, Error };
            Fields = Fields.Cat(this.MGColoring);
            BoSSS.Solution.Tecplot.Tecplot.PlotFields(Fields, "poisson_MG_coloring" + timestepNo + caseStr, phystime, superSampling);
            //BoSSS.Solution.Tecplot.Tecplot.PlotFields(Fields, Path.Combine(AnalyseOutputpath, "poisson_MG_coloring" + timestepNo + caseStr), phystime, superSampling);
        }

    }

    /// <summary>
    /// Interior Penalty Flux
    /// </summary>
    class ipFlux : BoSSS.Solution.NSECommon.SIPLaplace {

        public ipFlux(double penalty_const, MultidimensionalArray cj, BoundaryCondMap<BoundaryType> __boundaryCondMap)
            : base(penalty_const, cj, "T") //
        {
            m_boundaryCondMap = __boundaryCondMap;
            m_bndFunc = m_boundaryCondMap.bndFunction["T"];
        }

        BoundaryCondMap<BoundaryType> m_boundaryCondMap;
        Func<double[], double, double>[] m_bndFunc;

        protected override double g_Diri(ref CommonParamsBnd inp) {
            double v = m_bndFunc[inp.EdgeTag](inp.X, inp.time);
            return v;
        }

        protected override double g_Neum(ref CommonParamsBnd inp) {
            double v = m_bndFunc[inp.EdgeTag](inp.X, inp.time);
            return v;
        }

        protected override bool IsDirichlet(ref CommonParamsBnd inp) {
            BoundaryType edgeType = m_boundaryCondMap.EdgeTag2Type[inp.EdgeTag];
            switch (edgeType) {
                case BoundaryType.Dirichlet:
                    return true;

                case BoundaryType.Neumann:
                    return false;

                default:
                    throw new NotImplementedException();
            }
        }
    }


}
