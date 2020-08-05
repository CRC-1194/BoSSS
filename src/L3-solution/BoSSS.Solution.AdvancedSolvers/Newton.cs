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
using BoSSS.Foundation.XDG;

namespace BoSSS.Solution.AdvancedSolvers
{
    /// <summary>
    /// Implementation based on presudocode from Kelley, C. 
    /// Solving Nonlinear Equations with Newton’s Method. Fundamentals of Algorithms. 
    /// Society for Industrial and Applied Mathematics, 2003. https://doi.org/10.1137/1.9780898718898.
    /// </summary>
    public class Newton : NonlinearSolver
    {
        /// <summary>
        /// ctor
        /// </summary>
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
        /// Number of iterations, where Jacobi is not updated. Also known as constant newton method. Default 1, means regular newton.
        /// </summary>
        public int constant_newton_it = 1;

        /// <summary>
        /// Maximum dimension of the krylov subspace. Equals m in GMRES(m)
        /// </summary>
        public int maxKrylovDim = 30;

        /// <summary>
        /// Convergence criterion for nonlinear iteration
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

        public bool UsePresRefPoint;

        /// <summary>
        /// Prints the step reduction factor
        /// </summary>
        public bool printLambda = false; 


        /// <summary>
        /// Main solver routine
        /// </summary>
        public override void SolverDriver<S>(CoordinateVector SolutionVec, S RHS) {

            using (var tr = new FuncTrace()) {

                m_SolutionVec = SolutionVec;

                int itc;
                itc = 0;
                double[] CurSol, // "current (approximate) solution", i.e. 
                    TempSol, 
                    PrevSol, // previous iteration  (approximate) solution
                    CurRes, // residual associated with 'CurSol'
                    deltaX, TempRes;
                double rat;
                double alpha = 1E-4;
                //double sigma0 = 0.1;
                double sigma1 = 0.5;
                //double maxarm = 20;
                //double gamma = 0.9;

                // Eval_F0 

                using (new BlockTrace("Slv Init", tr)) {
                    base.Init(SolutionVec, RHS, out CurSol, out CurRes);
                };
                //Console.WriteLine("Residual base.init:   " + f0.L2NormPow2().MPISum().Sqrt());


                deltaX = new double[CurSol.Length];
                TempSol = new double[CurSol.Length];
                TempRes = new double[CurSol.Length];


                this.CurrentLin.TransformSolFrom(SolutionVec, CurSol);
                EvaluateOperator(1, SolutionVec.Mapping.ToArray(), CurRes);

                Console.WriteLine("Residual base.init:   " + CurRes.L2NormPow2().MPISum().Sqrt());
                //base.EvalResidual(x, ref f0);

                // fnorm
                double fnorm = CurRes.L2NormPow2().MPISum().Sqrt();
                double fNormo = 1;
                double errstep;
                double[] step = new double[CurSol.Length];
                double[] stepOld = new double[CurSol.Length];
                //BlockMsrMatrix CurrentJac;
                //bool secondCriteriumConverged = false;
                OnIterationCallback(itc, CurSol.CloneAs(), CurRes.CloneAs(), this.CurrentLin);
                double fnorminit = fnorm;
                using (new BlockTrace("Slv Iter", tr)) {
                    while (
                        (fnorm > ConvCrit * fnorminit + ConvCrit && /*secondCriteriumConverged == false &&*/ itc < MaxIter)   
                        || itc < MinIter) {
                        //Console.WriteLine("The convergence criterion is {0}", ConvCrit * fnorminit + ConvCrit);
                        rat = fnorm / fNormo;
                        //if (Math.Abs(fNormo - fnorm) < 1e-12)
                        //    break;
                        fNormo = fnorm;
                        itc++;

                        // How should the inverse of the Jacobian be approximated?
                        if (ApproxJac == ApproxInvJacobianOptions.GMRES) {
                            // ++++++++++++++++++++++++++
                            // Option: Matrix-Free GMRES
                            // ++++++++++++++++++++++++++



                            if (Precond != null) {
                                Precond.Init(CurrentLin);
                            }
                            //base.EvalResidual(x, ref f0); 
                            CurRes.ScaleV(-1.0);
                            step = Krylov(SolutionVec, CurSol, CurRes, out errstep);
                        } else if (ApproxJac == ApproxInvJacobianOptions.DirectSolver) {
                            // +++++++++++++++++++++++++++++
                            // Option: use 'external' solver
                            // +++++++++++++++++++++++++++++



                            var solver = this.linsolver;
                            solver.Init(CurrentLin);
                            step.ClearEntries();
                            var check = CurRes.CloneAs();
                            CurRes.ScaleV(-1.0);
                            solver.ResetStat();

                            if(solver is IProgrammableTermination pt) {
                                // iterative solver with programmable termination is used - 
                                
                                double f0_L2 = CurRes.MPI_L2Norm();
                                double thresh = f0_L2 * 1e-5;
                                Console.WriteLine($"Inexact Newton: setting convergence threshold to {thresh:0.##E-00}");
                                pt.TerminationCriterion = (iter, R0_l2, R_l2) => {
                                    return (R_l2 > thresh) && (iter < 100);
                                };
 

                            }

                            
                            solver.Solve(step, CurRes);
                            
                        } else {
                            throw new NotImplementedException($"approximation option {ApproxJac} for the Jacobian seems not to be existent.");
                        }

                        // Start line search
                        PrevSol = CurSol;
                        double lambda = 1;
                        double lamm = 1;
                        double lamc = lambda;
                        double iarm = 0;
                        TempSol = CurSol.CloneAs();
                        TempSol.AccV(lambda, step);
                        this.CurrentLin.TransformSolFrom(SolutionVec, TempSol);
                        EvaluateOperator(1, SolutionVec.Mapping.Fields, TempRes);
                        
                        double nft = TempRes.L2NormPow2().MPISum().Sqrt();
                        double nf0 = CurRes.L2NormPow2().MPISum().Sqrt();
                        double ff0 = nf0 * nf0; // residual norm of current solution ^2
                        double ffc = nft * nft; // 
                        double ffm = nft * nft; //


                        // Control of the the step size
                        while (nft >= (1 - alpha * lambda) * nf0 && iarm < maxStep) {

                            // Line search starts here
                            if (iarm == 0)
                                lambda = sigma1 * lambda;
                            else
                                lambda = parab3p(lamc, lamm, ff0, ffc, ffm); // ff0: curent sol, ffc: most recent reduction, ffm: previous reduction

                            // Update x;
                            TempSol = CurSol.CloneAs();
                            TempSol.AccV(lambda, step);
                            lamm = lamc;
                            lamc = lambda;

                            this.CurrentLin.TransformSolFrom(SolutionVec, TempSol);
                            EvaluateOperator(1, SolutionVec.Mapping.Fields, TempRes);

                            nft = TempRes.L2NormPow2().MPISum().Sqrt();
                            ffm = ffc;
                            ffc = nft * nft;
                            iarm++;

                            if (printLambda)
                                Console.WriteLine("    Residuum:  " + nft + " lambda = " + lambda);

                        }
                        // transform solution back to 'original domain'
                        // to perform the linearization at the new point...
                        // (and for Level-Set-Updates ...)
                        this.CurrentLin.TransformSolFrom(SolutionVec, TempSol);

                        if (UsePresRefPoint == false) {

                            if (this.m_SolutionVec.Mapping.Fields[2] is XDGField  Xpres) {
                                DGField presSpA = Xpres.GetSpeciesShadowField("A");
                                DGField presSpB = Xpres.GetSpeciesShadowField("B");
                                var meanpres = presSpB.GetMeanValueTotal(null);
                                presSpA.AccConstant(-1.0 * meanpres);
                                presSpB.AccConstant(-1.0 * meanpres);
                            } else {
                                DGField pres = this.m_SolutionVec.Mapping.Fields[2];
                                var meanpres = pres.GetMeanValueTotal(null);
                                pres.AccConstant(-1.0 * meanpres);
                            }
                        }


                        // update linearization
                        if (itc  % constant_newton_it == 0) {
                            base.Update(SolutionVec.Mapping.Fields, ref TempSol);
                            if(constant_newton_it !=1) { 
                            Console.WriteLine("Jacobian is updated: it {0}", itc);
                            }
                        }

                        // residual evaluation & callback
                        base.EvalResidual(TempSol, ref TempRes);

                        EvaluateOperator(1, SolutionVec.Mapping.Fields, TempRes);

                        fnorm = TempRes.L2NormPow2().MPISum().Sqrt();

                        CurSol = TempSol;
                        CurRes = TempRes.CloneAs();

                        OnIterationCallback(itc, CurSol.CloneAs(), CurRes.CloneAs(), this.CurrentLin);

                        #region second criterium
                        // Just testing. According to "Pawlowski et al. - 2006 - Globalization Techniques for Newton–Krylov Methods"
                        // this criterium is useful to "ensure that even finer physical details of the flow and are resolved"
                        
                        /*
                        double[] WMat_s = new double[x.Length];
                        double psi_r = 1e-3;
                        double psi_a = 1e-8;
                        double[] truestep = step;
                        truestep.ScaleV(lambda);
                        for(int i = 0; i < WMat_s.Length; i++) {
                            WMat_s[i] = 1 / (psi_r * x[i] + psi_a) * truestep[i];
                        }
                        WMat_s.CheckForNanOrInfV();

                        double secondCriterium = WMat_s.L2Norm()/x.Length;
                    
                        if(secondCriterium < 1) {
                            secondCriteriumConverged = true;
                        }
                        //Console.WriteLine("Norm Of the second criterium {0}", secondCriterium);
                        //if((fnorm < ConvCrit * fnorminit * 0 + ConvCrit))
                        //    Console.WriteLine("Criterium 1 fulfilled");
                        //if((secondCriteriumConverged == true))
                        //    Console.WriteLine("Criterium 2 fulfilled");
                        //if(itc > MaxIter)
                        //    Console.WriteLine("Criterium 3 fulfilled");
                    */
                        #endregion

                    }
                }


            }
        }



        /// <summary>
        /// Preconditioned GMRES, using <see cref="NonlinearSolver.Precond"/> as a preconditioner
        /// </summary>
        /// <param name="SolutionVec">Current Point</param>
        /// <param name="f0">Function at current point</param>
        /// <param name="xinit">initial iterate</param>
        /// <param name="errstep">error of step</param>
        /// <param name="currentX"></param>
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

        /// <summary>
        /// Driver routine
        /// </summary>
        double[] Krylov(CoordinateVector SolutionVec, double[] currentX, double[] f0, out double errstep) {
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
        static double[] givapp(double[] c, double[] s, double[] vin, int k) {
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
            }
            else if (Math.Abs(b) > Math.Abs(a)) {
                temp = a / b;
                s = 1.0 / Math.Sqrt(1.0 + temp * temp);
                c = temp * s;
            }
            else {
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
        static double parab3p(double lambdac, double lambdam, double ff0, double ffc, double ffm) {
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


    }


}
