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
using System.Linq;
using System.Collections.Generic;
using BoSSS.Solution;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.XDG;
using BoSSS.Foundation;
using BoSSS.Solution.Utils;
using BoSSS.Solution.XNSECommon;
using ilPSP.LinSolvers;
using BoSSS.Platform;
using MPI.Wrappers;
using System.Diagnostics;
using BoSSS.Foundation.Quadrature;
using BoSSS.Foundation.XDG.Quadrature.HMF;
using BoSSS.Solution.Tecplot;
using System.Globalization;
using System.IO;
using ilPSP.Utils;
using ilPSP.Connectors.Matlab;
using System.Text;
using ilPSP;
using BoSSS.Solution.AdvancedSolvers;
using NUnit.Framework;
using BoSSS.Foundation.IO;
using System.Collections;
using Newtonsoft.Json;
using BoSSS.Solution.Timestepping;
using BoSSS.Solution.XdgTimestepping;

namespace BoSSS.Application.XdgTimesteppingTest {
    public class XdgTimesteppingMain : BoSSS.Solution.XdgTimestepping.XdgApplicationWithSolver<XdgTimesteppingTestControl> {

        /// <summary>
        /// Les main routine.
        /// </summary>
        static void Main(string[] args) {
            //InitMPI();
            //BoSSS.Application.XdgTimesteppingTest.TestProgram.TestConvection_MovingInterface_MultiinitHighOrder(1, 0.2);
            //DeleteOldPlotFiles();
            //BoSSS.Application.XdgTimesteppingTest.TestProgram.TestConvection_MovingInterface_SingleInitLowOrder_BDF_dt023(TimeSteppingScheme.BDF2, 8);
            //BoSSS.Application.XdgTimesteppingTest.TestProgram.TestConvection_MovingInterface_SingleInitLowOrder_BDF_dt02(TimeSteppingScheme.ExplicitEuler, 8);
            //BoSSS.Application.XdgTimesteppingTest.TestProgram.TestConvection_MovingInterface_MultiinitHighOrder(1, 0.23);
            //FinalizeMPI();
            //throw new ApplicationException("deactivate me");
            //return;

            BoSSS.Solution.Application<XdgTimesteppingTestControl>._Main(args, false, delegate () {
                return new XdgTimesteppingMain();
            });
        }
#pragma warning disable 649

        [LevelSetTracker("-:A +:B", 1)]
        LevelSetTracker MyLsTrk;

        [InstantiateFromControlFile("Phi", "Phi", IOListOption.ControlFileDetermined)]
        LevelSet Phi;

        [InstantiateFromControlFile("u", "u", IOListOption.ControlFileDetermined)]
        XDGField u;

        [InstantiateFromControlFile("Residual", "u", IOListOption.ControlFileDetermined)]
        XDGField Residual;

        [InstantiateFromControlFile(new string[] { "Vx", "Vy" }, null, true, true, IOListOption.ControlFileDetermined)]
        VectorField<SinglePhaseField> V;

        [InstantiateFromControlFile("rhs", "u", IOListOption.ControlFileDetermined)]
        XDGField rhs;

        SinglePhaseField CutMarker;

        SinglePhaseField NearMarker;

        SinglePhaseField DOFMarker;

#pragma warning restore 649

        protected override LevelSetTracker InstantiateTracker() {
            return MyLsTrk;
        }

        protected override IEnumerable<DGField> InstantiateSolutionFields() {
            this.u.UpdateBehaviour = BehaveUnder_LevSetMoovement.AutoExtrapolate;
            return new DGField[] { this.u };
        }

        public override IEnumerable<DGField> InstantiateResidualFields() {
            return new DGField[] { this.Residual };
        }

        protected override void CreateAdditionalFields() {
            base.LsTrk = MyLsTrk;
            
            if(Control.CutCellQuadratureType != base.LsTrk.CutCellQuadratureType)
                throw new ApplicationException();

            CutMarker = new SinglePhaseField(new Basis(this.GridData, 0), "CutMarker");
            NearMarker = new SinglePhaseField(new Basis(this.GridData, 0), "NearMarker");
            DOFMarker = new SinglePhaseField(new Basis(this.GridData, 0), "DOFMarker");
            base.RegisterField(CutMarker, IOListOption.Always);
            base.RegisterField(NearMarker, IOListOption.Always);
            base.RegisterField(DOFMarker, IOListOption.Always);
        }

        //static int PlotCont = 1;

        void UpdateMarkerFields() {
            CutMarker.Clear();
            NearMarker.Clear();
            DOFMarker.Clear();
            foreach (int j in this.LsTrk.Regions.GetCutCellMask4LevSet(0).ItemEnum) {
                CutMarker.SetMeanValue(j, 1);
            }
            foreach (int j in this.LsTrk.Regions.GetNearFieldMask(1).ItemEnum) {
                NearMarker.SetMeanValue(j, 1);
            }
            int J = this.GridData.iLogicalCells.NoOfLocalUpdatedCells;
            for (int j = 0; j < J; j++) {
                DOFMarker.SetMeanValue(j, this.u.Basis.GetLength(j));
            }

            /*
            Tecplot.PlotFields(new DGField[] { CutMarker, NearMarker }, "Markers-" + PlotCont + ".csv", 0.0, 1);
            LsTrk.Regions.GetCutCellMask().SaveToTextFile("Cut-" + PlotCont + ".csv", false);
            LsTrk.Regions.GetSpeciesMask("A").SaveToTextFile("SpcA-" + PlotCont + ".csv", false);
            LsTrk.Regions.GetSpeciesMask("B").SaveToTextFile("SpcB-" + PlotCont + ".csv", false);

            int qOrd = this.LinearQuadratureDegree;
            var sch = LsTrk.GetXDGSpaceMetrics(LsTrk.SpeciesIdS.ToArray(), qOrd, 1).XQuadSchemeHelper;
            
            var schCut = sch.GetLevelSetquadScheme(0, LsTrk.Regions.GetCutCellMask());
            var RuleCut = schCut.SaveCompile(this.GridData, qOrd);
            ICompositeQuadRule_Ext.SumOfWeightsToTextFileVolume(RuleCut, this.GridData, "CutRule-" + PlotCont + ".csv");

            var schB = sch.GetVolumeQuadScheme(LsTrk.GetSpeciesId("B"));
            var RuleB = schB.SaveCompile(this.GridData, qOrd);
            ICompositeQuadRule_Ext.SumOfWeightsToTextFileVolume(RuleB, this.GridData, "B_Rule-" + PlotCont + ".csv");

            var schA = sch.GetVolumeQuadScheme(LsTrk.GetSpeciesId("A"));
            var RuleA = schA.SaveCompile(this.GridData, qOrd);
            ICompositeQuadRule_Ext.SumOfWeightsToTextFileVolume(RuleA, this.GridData, "A_Rule-" + PlotCont + ".csv");

            var eschB = sch.GetEdgeQuadScheme(LsTrk.GetSpeciesId("B"));
            var ERuleB = eschB.SaveCompile(this.GridData, qOrd);
            ICompositeQuadRule_Ext.SumOfWeightsToTextFileEdge(ERuleB, this.GridData, "Be_Rule-" + PlotCont + ".csv");

            var eschA = sch.GetEdgeQuadScheme(LsTrk.GetSpeciesId("A"));
            var ERuleA = eschA.SaveCompile(this.GridData, qOrd);
            ICompositeQuadRule_Ext.SumOfWeightsToTextFileEdge(ERuleA, this.GridData, "Ae_Rule-" + PlotCont + ".csv");

            PlotCont++;
            */
        }

        protected override void SetInitial(double t) {
            base.SetInitial(t);
            
            this.CreateEquationsAndSolvers(null);

            if (this.Control.MultiStepInit == true) {
                int CallCount = 0;

                

                if (base.Timestepping.m_RK_Timestepper != null)
                    throw new NotSupportedException();
                
                base.Timestepping.m_BDF_Timestepper.MultiInit(t, 0, this.Control.GetFixedTimestep(),
                    delegate (int TimestepIndex, double Time, DGField[] St) {

                        Console.WriteLine("Timestep index {0}, time {1} ", TimestepIndex, Time);

                        // level-set
                        // ---------

                        this.Phi.ProjectField(X => this.Control.Phi(X, Time));

                        // HMF hacks
                        if ((this.Control.CircleRadius != null) != (this.Control.CutCellQuadratureType == XQuadFactoryHelper.MomentFittingVariants.ExactCircle))
                            throw new ApplicationException("Illegal HMF configuration.");
                        if (this.Control.CircleRadius != null) {
                            ExactCircleLevelSetIntegration.RADIUS = new double[] { this.Control.CircleRadius(Time) };
                        }

                        if (CallCount == 0) {
                            this.LsTrk.UpdateTracker(Time);
                        } else {
                            this.LsTrk.UpdateTracker(Time, incremental: true);
                        }

                        CallCount++;

                        // solution
                        // --------

                        XDGField _u = (XDGField)St[0];
                        _u.Clear();
                        _u.GetSpeciesShadowField("A").ProjectField((X => this.Control.uA_Ex(X, Time)));
                        _u.GetSpeciesShadowField("B").ProjectField((X => this.Control.uB_Ex(X, Time)));

                    });
            } else {
                this.Phi.ProjectField(X => this.Control.Phi(X, 0.0));
                this.LsTrk.UpdateTracker(0.0);
                u.Clear();
                u.GetSpeciesShadowField("A").ProjectField((X => this.Control.uA_Ex(X, 0.0)));
                u.GetSpeciesShadowField("B").ProjectField((X => this.Control.uB_Ex(X, 0.0)));

                if(base.Timestepping.m_BDF_Timestepper != null)
                    base.Timestepping.m_BDF_Timestepper.SingleInit();
            }
        }


       
        int LinearQuadratureDegree {
            get {
                return Math.Max(2, 2 * this.u.Basis.Degree + V[0].Basis.Degree);
            }
        }

        int NonlinearQuadratureDegree {
            get {
                return Math.Max(2, 3 * this.u.Basis.Degree);
            }
        }

        protected override LevelSetHandling LevelSetHandling {
            get {
                LevelSetHandling lsh;
                switch(this.Control.InterfaceMode) {
                    case InterfaceMode.MovingInterface:
                    lsh = LevelSetHandling.Coupled_Once;
                    break;

                    case InterfaceMode.Splitting:
                    lsh = LevelSetHandling.LieSplitting;
                    break;

                    default:
                    throw new NotImplementedException();
                }

                return lsh;
            }
        }


        protected override XSpatialOperatorMk2 GetOperatorInstance(int D) {
            // create operator
            // ---------------

            Func<double[], double, double> S;
            switch (this.Control.InterfaceMode) {
                case InterfaceMode.MovingInterface:
                S = this.Control.S;
                break;

                case InterfaceMode.Splitting:
                S = (X, t) => 0.0;
                break;

                default:
                throw new NotImplementedException();
            }


             if (this.Control.Eq == Equation.ScalarTransport) {
                
                Func<double[], double, double>[] uBnd = new Func<double[], double, double>[this.Grid.EdgeTagNames.Keys.Max() + 1];
                for (int iEdgeTag = 1; iEdgeTag < uBnd.Length; iEdgeTag++) {
                    string nameEdgeTag;
                    if (this.Grid.EdgeTagNames.TryGetValue((byte)iEdgeTag, out nameEdgeTag)) {
                        if (!this.Control.BoundaryValues[nameEdgeTag].Evaluators.TryGetValue("u", out uBnd[iEdgeTag])) {
                            uBnd[iEdgeTag] = (X, t) => 0.0;
                        }
                    }
                }

                var Operator = new XSpatialOperatorMk2(1, 2, 1, (A, B, C) => this.LinearQuadratureDegree, LsTrk.SpeciesNames , "u", "Vx", "Vy", "Cod1");
                Operator.EquationComponents["Cod1"].Add(new TranportFlux_Bulk() { Inflow = uBnd });
                Operator.EquationComponents["Cod1"].Add(new TransportFlux_Interface(S));

                //delegate (string ParameterName, DGField ParamField)[] DelParameterFactory(IReadOnlyDictionary<string, DGField> DomainVarFields)
                Operator.ParameterFactories.Add(delegate (IReadOnlyDictionary<string, DGField> DomainVarFields) {
                    return new ValueTuple<string, DGField>[] {
                        ("Vx", this.V[0] as DGField),
                        ("Vy", this.V[1] as DGField)
                    };
                });
                // no update of the parameter is required since it stays constant.

                Operator.TemporalOperator = new ConstantXTemporalOperator(Operator, 1.0);
                
                Operator.LinearizationHint = LinearizationHint.AdHoc;
                Operator.AgglomerationThreshold = this.Control.AgglomerationThreshold;
                Operator.IsLinear = true;
                Operator.Commit();

                return Operator;
            } else if (this.Control.Eq == Equation.HeatEq) {
                
                var Operator = new XSpatialOperatorMk2(1, 0, 1, (A, B, C) => this.LinearQuadratureDegree, LsTrk.SpeciesNames, "u", "Cod1");

                var bulkFlx = new HeatFlux_Bulk() { m_muA = this.Control.muA, m_muB = this.Control.muB, m_rhsA = this.Control.rhsA, m_rhsB = this.Control.rhsB };
                var intfFlx = new HeatFlux_Interface(this.LsTrk, S) { m_muA = this.Control.muA, m_muB = this.Control.muB };

                Operator.EquationComponents["Cod1"].Add(bulkFlx);
                Operator.EquationComponents["Cod1"].Add(intfFlx);

                Operator.TemporalOperator = new ConstantXTemporalOperator(Operator, 1.0);
                
                Operator.LinearizationHint = LinearizationHint.AdHoc;
                Operator.AgglomerationThreshold = this.Control.AgglomerationThreshold;
                Operator.Commit();

                return Operator;

            } else if (this.Control.Eq == Equation.Burgers) {
                
                var Operator = new XSpatialOperatorMk2(1, 1, 1, (A, B, C) => this.NonlinearQuadratureDegree, LsTrk.SpeciesNames, "u", "u0", "Cod1");
                Operator.EquationComponents["Cod1"].Add(new BurgersFlux_Bulk() { Direction = this.Control.BurgersDirection, Inflow = this.Control.u_Ex });
                Operator.EquationComponents["Cod1"].Add(new BurgersFlux_Interface(S, this.Control.BurgersDirection));
                Operator.TemporalOperator = new ConstantXTemporalOperator(Operator, 1.0);

                Operator.LinearizationHint = LinearizationHint.AdHoc;
                Operator.AgglomerationThreshold = this.Control.AgglomerationThreshold;
                Operator.Commit();

                return Operator;
            } else {
                throw new NotImplementedException();
            }
        }

        /*
        protected override IEnumerable<DGField> InstantiateParameterFields() {
            if (this.Control.Eq == Equation.ScalarTransport)
                return this.V.ToArray();
            else if (this.Control.Eq == Equation.HeatEq)
                return null;
            else if (this.Control.Eq == Equation.Burgers)
                return CurrentState;
            else
                throw new NotImplementedException();
        }
        */


        public override double UpdateLevelset(DGField[] CurrentState, double phystime, double dt, double UnderRelax, bool incremental) {
            LevsetEvo(phystime, dt, null);

            return 0.0;
        }

    


        protected override double RunSolverOneStep(int TimestepNo, double phystime, double dt) {



            // get dt and check timestepping configuation
            // ------------------------------------------

            if (base.Control.TimesteppingMode == Solution.Control.AppControl._TimesteppingMode.Transient) {
                dt = base.GetFixedTimestep();
                Console.WriteLine("Timestep {0}, dt = {1} ...", TimestepNo, dt);
            } else {
                throw new NotSupportedException();
            }

            int oldPush = LsTrk.PushCount;

            
            var InitialVal = this.LsTrk.BackupTimeLevel(0);
            double[] u0 = CurrentStateVector.ToArray();
            Debug.Assert(InitialVal.time == phystime);

            List<double[]> u1s = new();
            List<(int Nsub, double delta1, double delta2, double L2Err)> deltas = new();
            int iSubDiv = 2;
            int cnt = 1;
            while(cnt <= 2) {

                Console.WriteLine();
                Console.WriteLine("######################################");
                Console.WriteLine("Doing " + iSubDiv + " timestep(s)...");


                double dtSub = dt / iSubDiv;

                if(cnt > 1) {
                    base.Timestepping = null;
                    base.InitSolver();
                    LsTrk.PopStacks();
                    LsTrk.ReplaceCurrentTimeLevel(InitialVal.CloneAs());
                    LsTrk.UpdateTracker(phystime);

                    CurrentStateVector.SetV(u0);
                    
                    Debug.Assert(ArrayTools.ListEquals(CurrentStateVector.Fields, base.Timestepping.CurrentState.Fields, (a, b) => object.ReferenceEquals(a, b)));
                    Debug.Assert(GenericBlas.L2Dist(u0, new CoordinateVector(base.Timestepping.CurrentState)) == 0);
                }


                int nSub = 0;
                for(; nSub < iSubDiv; nSub++) {
                    CurrentStateVector.SaveToTextFile($"u0out-{cnt - 1}.txt");
                    base.Timestepping.Solve(phystime + dtSub*nSub, dtSub);
                    this.PlotCurrentState(phystime + dtSub*(nSub + 1), new TimestepNumber(2 + cnt, nSub + 1), 3);
                    break;
                }
                var errS = ComputeL2Error(phystime + dtSub*(nSub + 1), true);

                var u1 = this.CurrentStateVector.ToArray();
                u1s.Add(u1);
                
                if(u1s.Count >= 3) {
                    int L = u1s.Count - 1;
                    Console.WriteLine("**********************");
                    double delta1 = GenericBlas.L2Dist(u1s[L], u1s[L - 1]);
                    double delta2 = GenericBlas.L2Dist(u1s[L], u1s[L - 2]);
                    Console.WriteLine("time refinement difference: " + delta1 + " --- " + delta2);
                    
                    deltas.Add((iSubDiv, delta1, delta2, errS.totErr));
                }

                Console.WriteLine("======================================");
                Console.WriteLine();

                using(var stw = new StreamWriter("delta.txt")) {
                    foreach(var delta in deltas) {
                        stw.WriteLine($"   {delta.Nsub},  {delta.delta1}, {delta.delta2},  {delta.L2Err};");
                    }
                }

                //iSubDiv *= 2;
                cnt++;
            }

            throw new Exception("remove me");
            base.Timestepping.Solve(phystime, dt);


            //dt = 0.0; 

            int newPush = LsTrk.PushCount;
            Console.WriteLine($"Old Push cnt {oldPush} -- New push cnt {newPush}");

           
            //throw new Exception("remove me");

            // return
            // ------

            if (TimestepNo == this.Control.NoOfTimesteps) {
                this.ComputeL2Error(phystime + dt);
            }

            Console.WriteLine();

            return dt;

        }

        private void LevsetEvo(double phystime, double dt, double[][] AdditionalVectors) {
            if (this.Control.InterfaceMode == InterfaceMode.MovingInterface
                || this.Control.InterfaceMode == InterfaceMode.Splitting) {

                // project new level-set
                this.Phi.ProjectField(X => this.Control.Phi(X, phystime + dt));
                this.LsTrk.UpdateTracker(phystime + dt, incremental: true);
                UpdateMarkerFields();

                // HMF hacks
                if ((this.Control.CircleRadius != null) != (this.Control.CutCellQuadratureType == XQuadFactoryHelper.MomentFittingVariants.ExactCircle))
                    throw new ApplicationException("Illegal HMF configuration.");
                if (this.Control.CircleRadius != null) {
                    ExactCircleLevelSetIntegration.RADIUS = new double[] { this.Control.CircleRadius(phystime + dt) };
                }
            } else {
                throw new NotImplementedException();
            }
        }

        (double totErr, double phaseAerr, double phaseBerr, double JmpL2Err) ComputeL2Error(double PhysTime, bool OverWriteIfExistent = false) {
            Console.WriteLine("Phystime = " + PhysTime);

            if ((this.Control.CircleRadius != null) != (this.Control.CutCellQuadratureType == XQuadFactoryHelper.MomentFittingVariants.ExactCircle))
                throw new ApplicationException("Illegal HMF configuration.");
            if (this.Control.CircleRadius != null) {
                ExactCircleLevelSetIntegration.RADIUS = new double[] { this.Control.CircleRadius(PhysTime) };
            }

            int order = Math.Max(this.u.Basis.Degree * 3, 3);
            XQuadSchemeHelper schH = LsTrk.GetXDGSpaceMetrics(this.LsTrk.SpeciesIdS.ToArray(), order).XQuadSchemeHelper;
            
            var uNum_A = this.u.GetSpeciesShadowField("A");
            var uNum_B = this.u.GetSpeciesShadowField("B");

            double uA_Err = uNum_A.L2Error(this.Control.uA_Ex.Vectorize(PhysTime), order, schH.GetVolumeQuadScheme(this.LsTrk.GetSpeciesId("A")));
            double uB_Err = uNum_B.L2Error(this.Control.uB_Ex.Vectorize(PhysTime), order, schH.GetVolumeQuadScheme(this.LsTrk.GetSpeciesId("B")));

            
            Func<double[], double, double> uJmp_Ex = ((X, t) => this.Control.uA_Ex(X, t) - this.Control.uB_Ex(X, t));

            SinglePhaseField uNumJump = new SinglePhaseField(uNum_A.Basis, "Jump");
            var CC = LsTrk.Regions.GetCutCellMask();
            uNumJump.Acc(+1.0, uNum_A, CC);
            uNumJump.Acc(-1.0, uNum_B, CC);
            double JmpL2Err = uNumJump.L2Error(uJmp_Ex.Vectorize(PhysTime), order, schH.GetLevelSetquadScheme(0, CC));
            double totErr = Math.Sqrt(uA_Err.Pow2() + uB_Err.Pow2());

            base.QueryHandler.ValueQuery("uA_Err", uA_Err, OverWriteIfExistent);
            base.QueryHandler.ValueQuery("uB_Err", uB_Err, OverWriteIfExistent);
            base.QueryHandler.ValueQuery("uJmp_Err", JmpL2Err, OverWriteIfExistent);

            Console.WriteLine("L2-err at t = {0}, bulk:      {1}", PhysTime, totErr);
            Console.WriteLine("L2-err at t = {0}, species A: {1}", PhysTime, uA_Err);
            Console.WriteLine("L2-err at t = {0}, species B: {1}", PhysTime, uB_Err);
            Console.WriteLine("L2-err at t = {0}, Jump:      {1}", PhysTime, JmpL2Err);

            double uA_min, uA_max, uB_min, uB_max;
            uNum_A.GetExtremalValues(out uA_min, out uA_max, out _, out _, this.LsTrk.Regions.GetSpeciesMask("A"));
            uNum_B.GetExtremalValues(out uB_min, out uB_max, out _, out _, this.LsTrk.Regions.GetSpeciesMask("B"));

            base.QueryHandler.ValueQuery("uA_Min", uA_min, OverWriteIfExistent);
            base.QueryHandler.ValueQuery("uA_Max", uA_max, OverWriteIfExistent);
            base.QueryHandler.ValueQuery("uB_Min", uB_min, OverWriteIfExistent);
            base.QueryHandler.ValueQuery("uB_Max", uB_max, OverWriteIfExistent);

            return (totErr, uA_Err, uB_Err, JmpL2Err);
        }

        protected override void PlotCurrentState(double physTime, TimestepNumber timestepNo, int susamp) {
            var Fields = new DGField[] { this.Phi, this.u, this.rhs, this.Residual, this.V[0], this.V[1], this.CutMarker, this.DOFMarker, this.NearMarker };
            Tecplot.PlotFields(Fields, "XdgTimesteppingTest" + timestepNo.ToString(), physTime, susamp);           
        }


    }
}
