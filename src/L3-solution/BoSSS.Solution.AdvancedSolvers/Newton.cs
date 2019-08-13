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
using System.Threading.Tasks;
using BoSSS.Foundation;
using ilPSP.Utils;
using MPI.Wrappers;
using ilPSP;
using ilPSP.LinSolvers;
using ilPSP.Connectors.Matlab;
using ilPSP.Tracing;
using System.IO;
using System.Diagnostics;

namespace BoSSS.Solution.AdvancedSolvers {
    /// <summary>
    /// Implementation based on presudocode from Kelley, C. 
    /// Solving Nonlinear Equations with Newton’s Method. Fundamentals of Algorithms. 
    /// Society for Industrial and Applied Mathematics, 2003. https://doi.org/10.1137/1.9780898718898.
    /// </summary>
    public class Newton : NonlinearSolver {
        public Newton(OperatorEvalOrLin __AssembleMatrix, IEnumerable<AggregationGridBasis[]> __AggBasisSeq, MultigridOperator.ChangeOfBasisConfig[][] __MultigridOperatorConfig) :
            base(__AssembleMatrix, __AggBasisSeq, __MultigridOperatorConfig) //
        {
        }

        /// <summary>
        /// Maximum number of Newton iterations
        /// </summary>
        public int MaxIter = 50;

        /// <summary>
        /// Minimum number of Newton iterations
        /// </summary>
        public int MinIter = 1;

        /// <summary>
        /// Maximum number of GMRES(m) restarts
        /// </summary>
        public int restart_limit = 1000;


        /// <summary>
        /// Maximum dimension of the krylov subspace. Equals m in GMRES(m)
        /// </summary>
        public int maxKrylovDim = 30;

        /// <summary>
        /// Convergence criterium for nonlinear iteration
        /// </summary>
        public double ConvCrit = 1e-6;

        /// <summary>
        /// Maximum number of step-length iterations
        /// </summary>
        public double maxStep = 30;

        /// <summary>
        /// Convergence for Krylov and GMRES iterations
        /// </summary>
        public double GMRESConvCrit = 1e-6;

        public CoordinateVector m_SolutionVec;

        public enum ApproxInvJacobianOptions { GMRES = 1, DirectSolver = 2 }

        public ApproxInvJacobianOptions ApproxJac = ApproxInvJacobianOptions.GMRES;


        public string m_SessionPath;

        public ISolverSmootherTemplate linsolver;

        //bool solveVelocity = true;

        //double VelocitySolver_ConvergenceCriterion = 1e-5;

        //double StressSolver_ConvergenceCriterion = 1e-5;

        public override void SolverDriver<S>(CoordinateVector SolutionVec, S RHS) {

            using (var tr = new FuncTrace()) {

                m_SolutionVec = SolutionVec;

                int itc;
                itc = 0;
                double[] x, xt, xOld, f0, deltaX, ft;
                double rat;
                double alpha = 1E-4;
                //double sigma0 = 0.1;
                double sigma1 = 0.5;
                //double maxarm = 20;
                //double gamma = 0.9;

                // Eval_F0 
                
                using (new BlockTrace("Slv Init", tr)) {
                    base.Init(SolutionVec, RHS, out x, out f0);
                };
                //Console.WriteLine("Residual base.init:   " + f0.L2NormPow2().MPISum().Sqrt());


                deltaX = new double[x.Length];
                xt = new double[x.Length];
                ft = new double[x.Length];


                this.CurrentLin.TransformSolFrom(SolutionVec, x);
                EvaluateOperator(1, SolutionVec.Mapping.ToArray(), f0);

                Console.WriteLine("Residual base.init:   " + f0.L2NormPow2().MPISum().Sqrt());
               //base.EvalResidual(x, ref f0);

                // fnorm
                double fnorm = f0.L2NormPow2().MPISum().Sqrt();
                double fNormo = 1;
                double errstep;
                double[] step = new double[x.Length];
                double[] stepOld = new double[x.Length];
                //BlockMsrMatrix CurrentJac;

                OnIterationCallback(itc, x.CloneAs(), f0.CloneAs(), this.CurrentLin);

                using (new BlockTrace("Slv Iter", tr)) {
                    while (fnorm > ConvCrit && itc < MaxIter) {
                        rat = fnorm / fNormo;
                        fNormo = fnorm;
                        itc++;

                        // How should the inverse of the Jacobian be approximated?
                        if (ApproxJac == ApproxInvJacobianOptions.GMRES) {
                            if (Precond != null) {
                                Precond.Init(CurrentLin);
                            }
                            //base.EvalResidual(x, ref f0); 
                            f0.ScaleV(-1.0);
                            step = Krylov(SolutionVec, x, f0, out errstep);
                        }
                        else if (ApproxJac == ApproxInvJacobianOptions.DirectSolver) {
                            /*
                            double[] _step = step.ToArray();
                            double[] _f0 = f0.ToArray();
                            double _check_norm;
                            {
                                var CurrentJac = CurrentLin.OperatorMatrix;
                                var _solver = new ilPSP.LinSolvers.PARDISO.PARDISOSolver();      // .MUMPS.MUMPSSolver();
                                _solver.DefineMatrix(CurrentJac);
                                _step.ClearEntries();
                                var _check = _f0.CloneAs();
                                _f0.ScaleV(-1.0);
                                _solver.Solve(_step, _f0);

                                CurrentLin.OperatorMatrix.SpMV(-1.0, _step, -1.0, _check);
                                _check_norm = _check.L2Norm();
                            }
                            */

                            var solver = linsolver;
                            //var mgo = new MultigridOperator(m_AggBasisSeq, SolutionVec.Mapping, CurrentJac, null, m_MultigridOperatorConfig);
                            solver.Init(CurrentLin);
                            step.ClearEntries();
                            var check = f0.CloneAs();
                            f0.ScaleV(-1.0);
                            solver.ResetStat();
                            solver.Solve(step, f0);
                            /*
                            double check_norm; 
                            {
                                CurrentLin.OperatorMatrix.SpMV(-1.0, step, -1.0, check);
                                check_norm = check.L2Norm();
                            }


                            double dist = GenericBlas.L2Dist(step, _step);


                            Console.WriteLine("    conv: " + solver.Converged + " /iter = " + solver.ThisLevelIterations + " /dist  = " + dist + " /resNrm = " + check_norm + " /parresNrm = " + _check_norm);
                            */
                            //step.SetV(step);

                            //if (solver.Converged == false)
                            //    Debugger.Launch();
                            
                        }
                        else {
                            throw new NotImplementedException("Your approximation option for the jacobian seems not to be existent.");
                        }


                        // Start line search
                        xOld = x;
                        double lambda = 1;
                        double lamm = 1;
                        double lamc = lambda;
                        double iarm = 0;
                        xt = x.CloneAs();
                        xt.AccV(lambda, step);
                        this.CurrentLin.TransformSolFrom(SolutionVec, xt);

                        EvaluateOperator(1, SolutionVec.Mapping.Fields, ft);

                        double nft = ft.L2NormPow2().MPISum().Sqrt();
                        double nf0 = f0.L2NormPow2().MPISum().Sqrt();
                        double ff0 = nf0 * nf0;
                        double ffc = nft * nft;
                        double ffm = nft * nft;

                        // Control of the the step size
                        while (nft >= (1 - alpha * lambda) * nf0 && iarm < maxStep) {

                            // Line search starts here
                            if (iarm == 0)
                                lambda = sigma1 * lambda;
                            else
                                lambda = parab3p(lamc, lamm, ff0, ffc, ffm);

                            // Update x;
                            xt = x.CloneAs();
                            xt.AccV(lambda, step);
                            lamm = lamc;
                            lamc = lambda;

                            this.CurrentLin.TransformSolFrom(SolutionVec, xt);

                            EvaluateOperator(1, SolutionVec.Mapping.Fields, ft);

                            nft = ft.L2NormPow2().MPISum().Sqrt();
                            ffm = ffc;
                            ffc = nft * nft;
                            iarm++;
#if DEBUG
                            Console.WriteLine("Step size:  " + lambda + "with Residuum:  " + nft);
#endif 
                        }
                        // transform solution back to 'original domain'
                        // to perform the linearization at the new point...
                        // (and for Level-Set-Updates ...)
                        this.CurrentLin.TransformSolFrom(SolutionVec, xt);

                        // update linearization
                        base.Update(SolutionVec.Mapping.Fields, ref xt);

                        // residual evaluation & callback
                        base.EvalResidual(xt, ref ft);

                        EvaluateOperator(1, SolutionVec.Mapping.Fields, ft);

                        fnorm = ft.L2NormPow2().MPISum().Sqrt();

                        x = xt;
                        f0 = ft.CloneAs();

                        OnIterationCallback(itc, x.CloneAs(), f0.CloneAs(), this.CurrentLin);


                    }
                }

                SolutionVec = m_SolutionVec;

            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="SolutionVec">Current Point</param>
        /// <param name="f0">Function at current point</param>
        /// <param name="xinit">initial iterate</param>
        /// <param name="errstep">error of step</param>
        /// <returns></returns>
        double[] GMRES(CoordinateVector SolutionVec, double[] currentX, double[] f0, double[] xinit, out double errstep) {
            using (var tr = new FuncTrace()) {
                int n = f0.Length;

                int reorth = 1; // Orthogonalization method -> 1: Brown/Hindmarsh condition, 3: Always reorthogonalize

                // RHS of the linear equation system 
                double[] b = new double[n];
                b.AccV(1, f0);

                double[] x = new double[n];
                double[] r = new double[n];

                int Nloc = base.CurrentLin.OperatorMatrix.RowPartitioning.LocalLength;
                int Ntot = base.CurrentLin.OperatorMatrix.RowPartitioning.TotalLength;

                r = b;

                //Initial solution
                if (xinit.L2Norm() != 0) {
                    x = xinit.CloneAs();
                    r.AccV(-1, dirder(SolutionVec, currentX, x, f0));
                }
               // Precond = null;
                if (Precond != null) {
                    var temp2 = r.CloneAs();
                    r.ClearEntries();
                    //this.OpMtxRaw.InvertBlocks(OnlyDiagonal: false, Subblocks: false).SpMV(1, temp2, 0, r);
                    Precond.Solve(r, temp2);
                }

                int m = maxKrylovDim;
                double[][] V = (m + 1).ForLoop(i => new double[Nloc]); //   V(1:n,1:m+1) = zeros(n,m);
                MultidimensionalArray H = MultidimensionalArray.Create(m + 1, m + 1); //   H(1:m,1:m) = zeros(m,m);
                double[] c = new double[m + 1];
                double[] s = new double[m + 1];
                double[] y;
                double rho = r.L2NormPow2().MPISum().Sqrt();
                errstep = rho;
                double[] g = new double[m + 1];
                g[0] = rho;

                Console.WriteLine("Error NewtonGMRES:   " + rho);

                // Termination of entry
                if (rho < GMRESConvCrit)
                    return SolutionVec.ToArray();

                V[0].SetV(r, alpha: (1.0 / rho));
                double beta = rho;
                int k = 1;

                while ((rho > GMRESConvCrit) && k <= m) {
                    V[k].SetV(dirder(SolutionVec, currentX, V[k - 1], f0));
                    //CurrentLin.OperatorMatrix.SpMV(1.0, V[k-1], 0.0, temp3);
                    // Call directional derivative
                    //V[k].SetV(f0);

                    if (Precond != null) {                   
                        var temp3 = V[k].CloneAs();
                        V[k].ClearEntries();
                        //this.OpMtxRaw.InvertBlocks(false,false).SpMV(1, temp3, 0, V[k]);
                        Precond.Solve(V[k], temp3);
                    }

                    double normav = V[k].L2NormPow2().MPISum().Sqrt();

                    // Modified Gram-Schmidt
                    for (int j = 1; j <= k; j++) {
                        H[j - 1, k - 1] = GenericBlas.InnerProd(V[k], V[j - 1]).MPISum();
                        V[k].AccV(-H[j - 1, k - 1], V[j - 1]);
                    }
                    H[k, k - 1] = V[k].L2NormPow2().MPISum().Sqrt();
                    double normav2 = H[k, k - 1];


                    // Reorthogonalize ?
                    if ((reorth == 1 && Math.Round(normav + 0.001 * normav2, 3) == Math.Round(normav, 3)) || reorth == 3) {
                        for (int j = 1; j <= k; j++) {
                            double hr = GenericBlas.InnerProd(V[k], V[j - 1]).MPISum();
                            H[j - 1, k - 1] = H[j - 1, k - 1] + hr;
                            V[k].AccV(-hr, V[j - 1]);
                        }
                        H[k, k - 1] = V[k].L2NormPow2().MPISum().Sqrt();
                    }

                    // Watch out for happy breakdown
                    if (H[k, k - 1] != 0)
                        V[k].ScaleV(1 / H[k, k - 1]);



                    // Form and store the information for the new Givens rotation
                    //if (k > 1) {
                    //    // for (int i = 1; i <= k; i++) {
                    //    H.SetColumn(k - 1, givapp(c.GetSubVector(0, k - 1), s.GetSubVector(0, k - 1), H.GetColumn(k - 1), k - 1));
                    //    //}
                    //}

                    // Givens rotation from SoftGMRES
                    double temp;
                    for (int l = 1; l <= k - 1; l++) {
                        // apply Givens rotation, H is Hessenbergmatrix
                        temp = c[l - 1] * H[l - 1, k - 1] + s[l - 1] * H[l + 1 - 1, k - 1];
                        H[l + 1 - 1, k - 1] = -s[l - 1] * H[l - 1, k - 1] + c[l - 1] * H[l + 1 - 1, k - 1];
                        H[l - 1, k - 1] = temp;
                    }
                    //	 [cs(i),sn(i)] = rotmat( H(i,i), H(i+1,i) ); % form i-th rotation matrix
                    rotmat(out c[k - 1], out s[k - 1], H[k - 1, k - 1], H[k + 1 - 1, k - 1]);
                    temp = c[k - 1] * g[k - 1]; //                       % approximate residual norm
                    H[k - 1, k - 1] = c[k - 1] * H[k - 1, k - 1] + s[k - 1] * H[k + 1 - 1, k - 1];
                    H[k + 1 - 1, k - 1] = 0.0;


                    // Don't divide by zero if solution has  been found
                    var nu = (H[k - 1, k - 1].Pow2() + H[k, k - 1].Pow2()).Sqrt();
                    if (nu != 0) {
                        //c[k - 1] = H[k - 1, k - 1] / nu;
                        //s[k - 1] = H[k, k - 1] / nu;
                        //H[k - 1, k - 1] = c[k - 1] * H[k - 1, k - 1] - s[k - 1] * H[k, k - 1];
                        //H[k, k - 1] = 0;

                        // givapp for g
                        g[k + 1 - 1] = -s[k - 1] * g[k - 1];
                        g[k - 1] = temp;

                        //var w1 = c[k - 1] * g[k - 1] - s[k - 1] * g[k];
                        //var w2 = s[k - 1] * g[k - 1] + c[k - 1] * g[k];
                        //g[k - 1] = w1;
                        //g[k] = w2;
                    }

                    rho = Math.Abs(g[k]);

                    Console.WriteLine("Error NewtonGMRES:   " + rho);

                    k++;

                }

                Console.WriteLine("GMRES completed after:   " + k + "steps");

                k--;

           

                // update approximation and exit
                //y = H(1:i,1:i) \ g(1:i);    
                y = new double[k];
                H.ExtractSubArrayShallow(new int[] { 0, 0 }, new int[] { k - 1, k - 1 })
                    .Solve(y, g.GetSubVector(0, k));

                int totalIter = k;

                // x = x + V(:,1:i)*y;
                for (int ii = 0; ii < k; ii++) {
                    x.AccV(y[ii], V[ii]);
                }

                // update approximation and exit
                //using (StreamWriter writer = new StreamWriter(m_SessionPath + "//GMRES_Stats.txt", true)) {
                //    writer.WriteLine("");
                //}

                errstep = rho;

                return x;
            }
        }

        public double[] Krylov(CoordinateVector SolutionVec, double[] currentX, double[] f0, out double errstep) {
            //this.m_AssembleMatrix(out OpMtxRaw, out OpAffineRaw, out MassMtxRaw, SolutionVec.Mapping.Fields.ToArray());
            double[] step = GMRES(SolutionVec, currentX, f0, new double[currentX.Length], out errstep);
            int kinn = 0;
            Console.WriteLine("Error Krylov:   " + errstep);

            while (kinn < restart_limit && errstep > GMRESConvCrit) {
                kinn++;

                step = GMRES(SolutionVec, currentX, f0, step, out errstep);

                Console.WriteLine("Error Krylov:   " + errstep);
            }

            return step;

        }

        /// <summary>
        /// Finite difference directional derivative Approximate f'(x) w
        /// C.T.Kelley, April 1, 2003
        /// This code comes with no guarantee or warranty of any kind.
        /// </summary>
        /// <param name="SolutionVec">Solution point</param>
        /// <param name="w">Direction</param>
        /// <param name="f0">f0, usually has been calculated earlier</param>
        /// <param name="linearization">True if the Operator should be linearized and evaluated afterwards</param>
        /// <returns></returns>
        public double[] dirder(CoordinateVector SolutionVec, double[] currentX, double[] w, double[] f0, bool linearization = false) {
            using (var tr = new FuncTrace()) {
                double epsnew = 1E-7;

                int n = SolutionVec.Length;
                double[] fx = new double[f0.Length];

                // Scale the step
                if (w.L2NormPow2().MPISum().Sqrt() == 0) {
                    fx.Clear();
                    return fx;
                }

                var normw = w.L2NormPow2().MPISum().Sqrt();

                double xs = GenericBlas.InnerProd(currentX, w).MPISum() / normw;

                if (xs != 0) {
                    epsnew = epsnew * Math.Max(Math.Abs(xs), 1) * Math.Sign(xs);
                }
                epsnew = epsnew / w.L2NormPow2().MPISum().Sqrt();

                var del = currentX.CloneAs();

                del.AccV(epsnew, w);

                double[] temp = new double[SolutionVec.Length];

                temp.CopyEntries(SolutionVec);

                this.CurrentLin.TransformSolFrom(SolutionVec, del);

                // Just evaluate linearized operator
                //var OpAffineRaw = this.LinearizationRHS.CloneAs();
                //this.CurrentLin.OperatorMatrix.SpMV(1.0, new CoordinateVector(SolutionVec.Mapping.Fields.ToArray()), 1.0, OpAffineRaw);
                //CurrentLin.TransformRhsInto(OpAffineRaw, fx);
                if (linearization == false) {
                    EvaluateOperator(1.0, SolutionVec.Mapping.Fields, fx);
                } 
                //else {
                //    this.m_AssembleMatrix(out OpMtxRaw, out OpAffineRaw, out MassMtxRaw, SolutionVec.Mapping.Fields.ToArray(), true);
                //    OpMtxRaw.SpMV(1.0, new CoordinateVector(SolutionVec.Mapping.Fields.ToArray()), 1.0, OpAffineRaw);
                //    CurrentLin.TransformRhsInto(OpAffineRaw, fx);
                //}

                SolutionVec.CopyEntries(temp);

                // (f1 - f0) / epsnew
                fx.AccV(1, f0);
                fx.ScaleV(1 / epsnew);

                return fx;

            }

        }

        /// <summary>
        /// Apply a sequence of k Givens rotations, used within gmres codes.  
        /// C.T.Kelley, April 1, 2003
        /// This code comes with no guarantee or warranty of any kind.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="s"></param>
        /// <param name="vin"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public double[] givapp(double[] c, double[] s, double[] vin, int k) {
            double[] vrot = vin;
            double w1, w2;

            for (int i = 1; i < k; i++) {
                w1 = c[i - 1] * vrot[i - 1] - s[i - 1] * vrot[i];
                w2 = s[i - 1] * vrot[i - 1] + c[i - 1] * vrot[i];
                vrot[i - 1] = w1;
                vrot[i] = w2;
            }
            return vrot;
        }

        /// <summary>
        /// Compute the Givens rotation matrix parameters for a and b.
        /// </summary>
        static void rotmat(out double c, out double s, double a, double b) {
            double temp;
            if (b == 0.0) {
                c = 1.0;
                s = 0.0;
            } else if (Math.Abs(b) > Math.Abs(a)) {
                temp = a / b;
                s = 1.0 / Math.Sqrt(1.0 + temp * temp);
                c = temp * s;
            } else {
                temp = b / a;
                c = 1.0 / Math.Sqrt(1.0 + temp * temp);
                s = temp * c;
            }
        }
        /// <summary>
        /// Apply three-point safeguarded parabolic model for a line search.
        /// C.T.Kelley, April 1, 2003
        /// This code comes with no guarantee or warranty of any kind.
        /// function lambdap = parab3p(lambdac, lambdam, ff0, ffc, ffm)
        /// input:
        ///        lambdac = current steplength
        ///        lambdam = previous steplength
        ///        ff0 = value of \| F(x_c) \|^2
        ///        ffc = value of \| F(x_c + \lambdac d) \|^2
        ///        ffm = value of \| F(x_c + \lambdam d) \|^2
        ///        
        /// output:
        /// lambdap = new value of lambda given parabolic model
        /// 
        /// internal parameters:
        /// sigma0 = .1, sigma1 = .5, safeguarding bounds for the linesearch
        /// </summary>
        /// <param name="lambdac"></param>
        /// <param name="lambdam"></param>
        /// <param name="ff0"></param>
        /// <param name="ffc"></param>
        /// <param name="ffm"></param>
        /// <returns></returns>
        public double parab3p(double lambdac, double lambdam, double ff0, double ffc, double ffm) {
            double sigma0 = 0.1;
            double sigma1 = 0.5;

            double c2 = lambdam * (ffc - ff0) - lambdac * (ffm - ff0);
            if (c2 >= 0)
                return sigma1 * lambdac;
            double c1 = lambdac * lambdac * (ffm - ff0) - lambdam * lambdam * (ffc - ff0);
            double lambdap = -c1 * 0.5 / c2;
            if (lambdap < sigma0 * lambdac) lambdap = sigma0 * lambdac;
            if (lambdap > sigma1 * lambdac) lambdap = sigma1 * lambdac;

            return lambdap;
        }


       

        /// <summary>
        /// Computes a forward difference jacobian and returns the dense jacobian
        /// </summary>
        /// <param name="SolutionVec"></param>
        /// <param name="currentX"></param>
        /// <param name="f0"></param>
        /// <returns></returns>
        public BlockMsrMatrix diffjac(CoordinateVector SolutionVec, double[] currentX, double[] f0) {
            int n = currentX.Length;
            BlockMsrMatrix jac = new BlockMsrMatrix(SolutionVec.Mapping);

            var temp = new double[n];

            for (int i = 0; i < n; i++) {
                var zz = new double[n];
                zz[i] = 1;
                temp = dirder(SolutionVec, currentX, zz, f0);
                for (int j = 0; j < n; j++) {
                    jac[j, i] = temp[j];
                }
            }

            return jac;
        }

        public int Bandwidth(double[] currentX) {
            int beta;
            int dim = currentX.Length;
            int full_bandwidth_row;
            BlockMsrMatrix OpMatrix = CurrentLin.OperatorMatrix;
            double[] b = new double[dim];

            for (int j = 0; j < dim; j++) {
                for (int i = dim - 1; i >= 0; i--) {
                    if (OpMatrix[j, i] != 0) {

                        b[j] = i - j;
                        break;

                    }

                }

            }

            beta = (int)b.Max();
            full_bandwidth_row = b.FirstIndexWhere(c => c == beta);

            return beta;

        }

        public BlockMsrMatrix freebandeddiffjac(CoordinateVector SolutionVec, double[] currentX, double[] f0) {
            int n = currentX.Length;
            BlockMsrMatrix jac = new BlockMsrMatrix(SolutionVec.Mapping);
            int beta = Bandwidth(currentX);
            int number_Cells = n / beta;

            var temp = new double[n];

            for (int k = 0; k < beta; k++) {
                var zz = new double[n];

                for (int i = 0; i < number_Cells; i++) {
                    zz[i * beta + k] = 1;

                }
                temp = dirder(SolutionVec, currentX, zz, f0);


                for (int i = 0; i < number_Cells; i++) {
                    for (int j = i * beta + k; j < (i + 1) * beta; j++) {
                        jac[j, i * beta + k] = temp[j];
                    }
                }
            }

            return jac;
        }

        public BlockMsrMatrix bandeddiffjac(CoordinateVector SolutionVec, double[] currentX, double[] f0) {
            int dimension = SolutionVec.Mapping.GridDat.SpatialDimension;

            int degree_VelocityX = SolutionVec.Mapping.Fields[0].Basis.Degree;
            int degree_VelocityY = SolutionVec.Mapping.Fields[1].Basis.Degree;
            int degree_VelocityZ = new int();
            int degree_Pressure = new int();
            int degree_StressXX = new int();
            int degree_StressXY = new int();
            int degree_StressYY = new int();

            int NoVariables = SolutionVec.Mapping.NoOfVariables;



            if (dimension == 2) {
                degree_Pressure = SolutionVec.Mapping.Fields[2].Basis.Degree;

                if (NoVariables > 4) {
                    degree_StressXX = SolutionVec.Mapping.Fields[3].Basis.Degree;
                    degree_StressXY = SolutionVec.Mapping.Fields[4].Basis.Degree;
                    degree_StressYY = SolutionVec.Mapping.Fields[5].Basis.Degree;
                }
            } else if (dimension == 3) {
                degree_VelocityZ = SolutionVec.Mapping.Fields[2].Basis.Degree;
                degree_Pressure = SolutionVec.Mapping.Fields[3].Basis.Degree;
            } else throw new ArgumentException();


            int factor = 1;
            int PI_VelocityX = 1;
            int PI_VelocityY = 1;
            int PI_VelocityZ = 1;
            int PI_Pressure = 1;
            int PI_StressXX = 1;
            int PI_StressXY = 1;
            int PI_StressYY = 1;

            for (int i = 1; i <= dimension; i++) {
                factor *= factor * i;
                PI_VelocityX *= degree_VelocityX + i;
                PI_VelocityY *= degree_VelocityY + i;
                PI_Pressure *= degree_Pressure + i;

                if (dimension == 3) {
                    PI_VelocityZ *= degree_VelocityZ + i;
                }

                if (NoVariables > 4) {
                    PI_StressXX *= degree_StressXX + i;
                    PI_StressXY *= degree_StressXY + i;
                    PI_StressYY *= degree_StressYY + i;
                }
            }

            int number_Polynomials = (PI_VelocityX + PI_VelocityY + PI_Pressure) / factor;

            if (dimension == 3) {
                number_Polynomials += PI_VelocityZ / factor;
            }

            if (NoVariables > 4) {
                number_Polynomials += (PI_StressXX + PI_StressXY + PI_StressYY) / factor;
            }

            int n = currentX.Length;
            int number_Cells = n / number_Polynomials;

            BlockMsrMatrix jac = new BlockMsrMatrix(SolutionVec.Mapping);

            var temp = new double[n];

            for (int k = 0; k < number_Polynomials; k++) {
                var zz = new double[n];

                for (int i = 0; i < number_Cells; i++) {
                    zz[i * number_Polynomials + k] = 1;

                }
                temp = dirder(SolutionVec, currentX, zz, f0);


                for (int i = 0; i < number_Cells; i++) {
                    for (int j = i * number_Polynomials + k; j < (i + 1) * number_Polynomials; j++) {
                        jac[j, i * number_Polynomials + k] = temp[j];
                    }
                }
            }


            return jac;
        }


    }


}
