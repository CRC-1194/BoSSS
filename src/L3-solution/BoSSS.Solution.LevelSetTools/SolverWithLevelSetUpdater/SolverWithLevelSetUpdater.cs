﻿using BoSSS.Foundation;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.IO;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.AdvancedSolvers;
using BoSSS.Solution.NSECommon;
using BoSSS.Solution.XdgTimestepping;
using BoSSS.Solution.Tecplot;
using ilPSP;
using ilPSP.Utils;
using System;
using System.Collections.Generic;
using System.Linq;


namespace BoSSS.Solution.LevelSetTools.SolverWithLevelSetUpdater {
    
    /// <summary>
    /// The major contribution of this class in addition to the base class is that 
    /// it formalizes the evolution of the Level-Sets by using a <see cref="LevelSetUpdater"/>, cf. <see cref="LevelSetUpdater"/>
    /// </summary>
    /// <remarks>
    /// - created by Lauritz Beck, dec 2020
    /// - further modified and reworked by F Kummer, jan 2021
    /// </remarks>
    public abstract class SolverWithLevelSetUpdater<T> : XdgApplicationWithSolver<T> where T : SolverWithLevelSetUpdaterControl, new() {
        
        /// <summary>
        /// 
        /// </summary>
        public LevelSetUpdater LsUpdater;

        /// <summary>
        /// Quadrature order for everything;
        /// This central computation of the quadrature order should ensure that the cut-cell quadrature rules are only 
        /// constructed for a single order.
        /// </summary>
        abstract public int QuadOrder();


        protected override MultigridOperator.ChangeOfBasisConfig[][] MultigridOperatorConfig {
            get {
                // set the MultigridOperator configuration for each level:
                // it is not necessary to have exactly as many configurations as actual multigrid levels:
                // the last configuration enty will be used for all higher level
                MultigridOperator.ChangeOfBasisConfig[][] configs = new MultigridOperator.ChangeOfBasisConfig[3][];
                for(int iLevel = 0; iLevel < configs.Length; iLevel++) {
                    var configsLevel = new List<MultigridOperator.ChangeOfBasisConfig>();

                    AddMultigridConfigLevel(configsLevel);

                    configs[iLevel] = configsLevel.ToArray();
                }
                return configs;
            }
        }

        /// <summary>
        /// Configuration of operator pre-pre-conditioning (not a typo), cf. <see cref="MultigridOperatorConfig"/>.
        /// </summary>
        protected abstract void AddMultigridConfigLevel(List<MultigridOperator.ChangeOfBasisConfig> configsLevel);


        protected override XSpatialOperatorMk2 GetOperatorInstance(int D) {
            XSpatialOperatorMk2 xOperator = GetOperatorInstance(D, LsUpdater);
            return xOperator;
        }

        protected abstract XSpatialOperatorMk2 GetOperatorInstance(int D, LevelSetUpdater levelSetUpdater);

        /// <summary>
        /// Setup of the level-set system and the tracker
        /// </summary>
        protected override LevelSetTracker InstantiateTracker() {
            LsUpdater = InstantiateLevelSetUpdater();

            // register all managed LevelSets
            foreach (DualLevelSet LevSet in LsUpdater.LevelSets.Values) {
                base.RegisterField(LevSet.CGLevelSet);
                base.RegisterField(LevSet.DGLevelSet);
            }

            // register internal fields, e.g. extension velocity etc.
            foreach (var field in LsUpdater.InternalFields.Values) {
                base.RegisterField(field);
            }

            return LsUpdater.Tracker;
        }


        /// <summary>
        /// 
        /// </summary>
        //protected override void CreateTrackerHack() {
        //    base.CreateTrackerHack();

        //    foreach (DualLevelSet ls in LsUpdater.LevelSets.Values) {
        //        if (ls.DGLevelSet is DGField f) {
        //            base.RegisterField(ls.DGLevelSet);
        //        }
        //    }

        //}


        /// <summary>
        /// Number of different interfaces 
        /// </summary>
        protected abstract int NoOfLevelSets {
            get;
        }

        /// <summary>
        /// Species table for the initialization of the <see cref="LevelSetTracker"/>,
        /// see <see cref="LevelSetTracker.SpeciesTable"/>.
        /// Dimension of array must be equal to <see cref="NoOfLevelSets"/>.
        /// </summary>
        protected abstract Array SpeciesTable {
            get;
        }

        /// <summary>
        /// Predefined level-set names; this can be overridden, but is not recommended.
        /// The recommended practice for an app is to 
        /// override <see cref="NoOfLevelSets"/>; then, default names for the level-set-fields are chosen.
        /// </summary>
        protected virtual (string ContLs, string DgLs)[] LevelSetNames {
            get {
                var ret = new ValueTuple<string, string>[NoOfLevelSets];
                for(int i = 0; i < NoOfLevelSets; i++) {
                    ret[i] = (VariableNames.LevelSetCGidx(i), VariableNames.LevelSetDGidx(i));
                }
                return ret;
            }
        }

        /// <summary>
        /// The evolution velocity for the <paramref name="iLevSet"/>-th level-set;
        /// - can be null, if the level-set should not be moved
        /// - The <see cref="ILevelSetParameter.ParameterNames"/> must comply with the following convention:
        ///   <see cref="VariableNames.AsLevelSetVariable"/>( s , <see cref="VariableNames.VelocityVector"/> ),
        ///   where s is the first item of <see cref="LevelSetNames"/>
        /// </summary>
        abstract protected ILevelSetParameter GetLevelSetVelocity(int iLevSet);

        /// <summary>
        /// boundary condition mapping, mainly required for the Stokes extension, where a velocity boundary condition is required.
        /// </summary>
        protected abstract IncompressibleBoundaryCondMap GetBcMap();

        /// <summary>
        /// Instantiate the level-set-system (fields for storing, evolution operators, ...) 
        /// Before creating XDG-fields one need to
        /// initialize the level-set fields <see cref="InitializeLevelSets"/>
        /// </summary>
        protected virtual LevelSetUpdater InstantiateLevelSetUpdater() {
            int D = this.Grid.SpatialDimension;
            var lsNames = this.LevelSetNames;
            int NoOfLevelSets = lsNames.Length;
            if(NoOfLevelSets != this.NoOfLevelSets)
                throw new ApplicationException();


            // phase 1: create DG level-sets
            // ======================================
            LevelSet[] DGlevelSets = new LevelSet[NoOfLevelSets];
            for (int iLevSet = 0; iLevSet < this.LevelSetNames.Length; iLevSet++) {
                var LevelSetCG = lsNames[iLevSet].ContLs;
                var LevelSetDG = lsNames[iLevSet].DgLs;

                int levelSetDegree = Control.FieldOptions[LevelSetCG].Degree;    // need to change naming convention of old XNSE_Solver

                LevelSet levelSetDG = new LevelSet(new Basis(GridData, levelSetDegree), LevelSetDG);
                DGlevelSets[iLevSet] = levelSetDG;
            }


            // phase 2: create updater
            // =======================
            LevelSetUpdater lsUpdater;
            switch(NoOfLevelSets) {
                case 1:
                lsUpdater = new LevelSetUpdater((GridData)GridData, Control.CutCellQuadratureType, Control.LS_TrackerWidth, 
                    (string[]) this.SpeciesTable, DGlevelSets[0], lsNames[0].ContLs);
                break;

                case 2:
                lsUpdater = new LevelSetUpdater((GridData)GridData, Control.CutCellQuadratureType, Control.LS_TrackerWidth, 
                    (string[,]) this.SpeciesTable, DGlevelSets[0], lsNames[0].ContLs, DGlevelSets[1], lsNames[1].ContLs);
                break;

                default:
                throw new NotImplementedException("Unsupported number of level-sets: " + NoOfLevelSets);
            }


            // phase 3: instantiate evolvers
            // ============================
            for(int iLevSet = 0; iLevSet < this.LevelSetNames.Length; iLevSet++) {
                var LevelSetCG = lsNames[iLevSet].ContLs;
                var LevelSetDG = lsNames[iLevSet].DgLs;

                // create evolver:
                switch(Control.Get_Option_LevelSetEvolution(iLevSet)) {
                    case LevelSetEvolution.Fourier: {
                        break;
                    }
                    case LevelSetEvolution.FastMarching: {
                        var fastMarcher = new FastMarchingEvolver(LevelSetCG, QuadOrder(), D);
                        lsUpdater.AddEvolver(LevelSetCG, fastMarcher);
                        break;
                    }
                    case LevelSetEvolution.StokesExtension: {
                        var stokesExtEvo = new StokesExtensionEvolver(LevelSetCG, QuadOrder(), D,
                            GetBcMap(),
                            this.Control.AgglomerationThreshold, this.GridData);
                        lsUpdater.AddEvolver(LevelSetCG, stokesExtEvo);
                        break;
                    }
                    case LevelSetEvolution.Phasefield: {
                        var PhasefieldEvolver = new PhasefieldEvolver(LevelSetCG, QuadOrder(), D,
                            GetBcMap(), this.Control,
                            this.Control.AgglomerationThreshold, this.GridData);

                        lsUpdater.AddEvolver(LevelSetCG, PhasefieldEvolver);
                        break;
                    }
                    case LevelSetEvolution.SplineLS: {
                        int nodeCount = 30;
                        Console.WriteLine("Achtung, Spline node count ist hart gesetzt. Was soll hier hin?");
                        var SplineEvolver = new SplineLevelSetEvolver(LevelSetCG, (GridData)(this.GridData));
                        break;
                    }
                    case LevelSetEvolution.Prescribed: {
                        var prescrEvo = new PrescribedEvolver(this.Control.InitialValues_EvaluatorsVec[LevelSetCG]);
                        break;
                    }
                    case LevelSetEvolution.None: {
                        break;
                    }
                    default:
                    throw new NotImplementedException($"Unknown option for level-set evolution: {Control.Option_LevelSetEvolution}");
                }

                // add velocity parameter:
                var levelSetVelocity = GetLevelSetVelocity(iLevSet);
                if(levelSetVelocity != null) {
                    if(!ArrayTools.ListEquals(levelSetVelocity.ParameterNames,
                        BoSSS.Solution.NSECommon.VariableNames.AsLevelSetVariable(LevelSetCG, BoSSS.Solution.NSECommon.VariableNames.VelocityVector(D)))) {
                        throw new ApplicationException($"Parameter names for the level-set velocity provider for level-set #{iLevSet} ({LevelSetCG}) does not comply with convention.");
                    }
                    lsUpdater.AddLevelSetParameter(LevelSetCG, levelSetVelocity);
                }

            }

            // return
            // ======
            return lsUpdater;
        }


        /// <summary>
        /// Corresponding to <see cref="LevelSetEvolution"/> initialization of LevelSetDG
        /// and projection on continous LevelSetCG
        /// calls <see cref="LevelSetTracker.UpdateTracker(double, int, bool, int[])">
        /// </summary>
        protected virtual void InitializeLevelSets(LevelSetUpdater lsUpdater, double time) {

            var lsNames = this.LevelSetNames;
            int NoOfLevelSets = lsNames.Length;
            if (NoOfLevelSets != lsUpdater.LevelSets.Count)
                throw new ApplicationException();


            for (int iLevSet = 0; iLevSet < this.LevelSetNames.Length; iLevSet++) {
                string LevelSetDG = lsNames[iLevSet].DgLs;
                string LevelSetCG = lsNames[iLevSet].ContLs;
                DualLevelSet pair = LsUpdater.LevelSets[LevelSetCG];

                int levelSetDegree = Control.FieldOptions[LevelSetCG].Degree;    // need to change naming convention of old XNSE_Solver
                if (levelSetDegree != pair.DGLevelSet.Basis.Degree)
                    throw new ApplicationException();

                switch (Control.Get_Option_LevelSetEvolution(iLevSet)) {
                    case LevelSetEvolution.Fourier: {
                        FourierLevelSet fourierLevelSet = new FourierLevelSet(Control.FourierLevSetControl, new Basis(GridData, levelSetDegree), VariableNames.LevelSetDG);
                        fourierLevelSet.Clear();
                        fourierLevelSet.ProjectField(Control.InitialValues_EvaluatorsVec[LevelSetCG].SetTime(time));
                        pair.DGLevelSet = fourierLevelSet;
                        break;
                    }
                    case LevelSetEvolution.Prescribed:
                    case LevelSetEvolution.StokesExtension:
                    case LevelSetEvolution.FastMarching:
                    case LevelSetEvolution.None: {
                        pair.DGLevelSet.Clear();
                        pair.DGLevelSet.ProjectField(Control.InitialValues_EvaluatorsVec[LevelSetCG].SetTime(time));  
                        break;
                    }
                    case LevelSetEvolution.SplineLS: {
                        int nodeCount = 30;
                        Console.WriteLine("Achtung, Spline node count ist hart gesetzt. Was soll hier hin?");
                        SplineLevelSet SplineLevelSet = new SplineLevelSet(Control.Phi0Initial, new Basis(GridData, levelSetDegree), VariableNames.LevelSetDG, nodeCount);
                        if (time != 0.0)
                            Console.WriteLine("Warning: no time dependent initial value");
                        pair.DGLevelSet = SplineLevelSet;
                        break;
                    }
                    default:
                        throw new NotImplementedException($"Unknown option for level-set evolution: {Control.Option_LevelSetEvolution}");
                }

                if (pair.DGLevelSet.L2Norm() == 0.0) {
                    Console.WriteLine($"Level-Set field {LevelSetCG} is **exactly** zero: setting entire field to -1.");
                    pair.DGLevelSet.AccConstant(-1.0);
                }

                pair.CGLevelSet.Clear();
                pair.CGLevelSet.AccLaidBack(1.0, pair.DGLevelSet);

            }

            LsUpdater.Tracker.UpdateTracker(time);

            LsUpdater.Tracker.PushStacks();

        }


        /// <summary>
        /// <see cref="XdgTimestepping.LevelSetHandling"/>
        /// </summary>
        protected override LevelSetHandling LevelSetHandling {
            get {
                return this.Control.Timestepper_LevelSetHandling;
            }
        }
        

        /// <summary>
        /// The base implementation <see cref="Solution.Application{T}.SetInitial"/>
        /// must be overridden, since it does not preform the continuity projection, see <see cref="DualLevelSet"/>,
        /// but it may overwrite the continuous level set.
        ///
        /// This implementation, however, ensures continuity of the level-set at the cell boundaries.
        /// </summary>
        protected override void SetInitial(double t) {
            base.SetInitial(t); // base implementation does not considers the DG/CG pair.

            //foreach (var NamePair in this.LevelSetNames) {
            //    string LevelSetCG = NamePair.ContLs;

            //    // we just overwrite the DG-level-set, continuity projection is set later when the operator is fully set-up
            //    var pair1 = LsUpdater.LevelSets[LevelSetCG];
            //    pair1.DGLevelSet.Clear();
            //    pair1.DGLevelSet.ProjectField(Control.InitialValues_EvaluatorsVec[LevelSetCG].SetTime(t));
            //}

            this.InitializeLevelSets(LsUpdater, t);

        }



        /// <summary>
        /// - Matches <see cref="DelUpdateLevelset"/>, used by the <see cref="ApplicationWithSolver{T}.Timestepping"/> to advance the interfaces
        /// - Uses the <see cref="LsUpdater"/>
        /// </summary>
        public override double UpdateLevelset(DGField[] domainFields, double time, double dt, double UnderRelax, bool incremental) {
            var DomainVarsDict = new Dictionary<string, DGField>(domainFields.Length);
            for (int iVar = 0; iVar < domainFields.Length; iVar++) {
                DomainVarsDict.Add(Operator.DomainVar[iVar], domainFields[iVar]);
            }

            var parameterFields = Timestepping.Parameters;

            var ParameterVarsDict = new Dictionary<string, DGField>(parameterFields.Count());
            for (int iVar = 0; iVar < parameterFields.Count(); iVar++) {
                ParameterVarsDict.Add(Operator.ParameterVar[iVar], parameterFields[iVar]);
            }
            double residual = LsUpdater.UpdateLevelSets(DomainVarsDict, ParameterVarsDict, time, dt, UnderRelax, incremental);
            Console.WriteLine("Residual of level-set update: " + residual);
            return 0.0;
        }


        //protected override void CreateAdditionalFields() {
        //    base.CreateAdditionalFields();

        //    // Level Set Parameters
        //    var domainFields = CurrentState.Fields;
        //    var DomainVarsDict = new Dictionary<string, DGField>(domainFields.Count);
        //    for (int iVar = 0; iVar < domainFields.Count; iVar++) {
        //        DomainVarsDict.Add(Operator.DomainVar[iVar], domainFields[iVar]);
        //    }

        //    var parameterFields = base.Parameters;
        //    var ParameterVarsDict = new Dictionary<string, DGField>(parameterFields.Count());
        //    for (int iVar = 0; iVar < parameterFields.Count(); iVar++) {
        //        ParameterVarsDict.Add(Operator.ParameterVar[iVar], parameterFields[iVar]);
        //    }
        //    LsUpdater.InitializeParameters(DomainVarsDict, ParameterVarsDict);

        //    foreach (var f in LsUpdater.Parameters.Values) {
        //        base.RegisterField(f);
        //    }
        //}


        protected override void CreateEquationsAndSolvers(GridUpdateDataVaultBase L) {
            base.CreateEquationsAndSolvers(L);

            // Level Set Parameters
            var domainFields = CurrentState.Fields;
            var DomainVarsDict = new Dictionary<string, DGField>(domainFields.Count);
            for (int iVar = 0; iVar < domainFields.Count; iVar++) {
                DomainVarsDict.Add(Operator.DomainVar[iVar], domainFields[iVar]);
            }

            var parameterFields = base.Parameters;
            var ParameterVarsDict = new Dictionary<string, DGField>(parameterFields.Count());
            for (int iVar = 0; iVar < parameterFields.Count(); iVar++) {
                ParameterVarsDict.Add(Operator.ParameterVar[iVar], parameterFields[iVar]);
            }
            LsUpdater.InitializeParameters(DomainVarsDict, ParameterVarsDict);

            foreach (var f in LsUpdater.Parameters.Values) {
                base.RegisterField(f);
            }

            // enforce continuity
            // ------------------

            if (L == null) {
                var pair1 = LsUpdater.LevelSets.First().Value;
                var oldCoords1 = pair1.DGLevelSet.CoordinateVector.ToArray();
                UpdateLevelset(this.CurrentState.Fields.ToArray(), 0.0, 0.0, 1.0, false); // enforces the continuity projection upon the initial level set
                double dist1 = pair1.DGLevelSet.CoordinateVector.L2Distance(oldCoords1);
                if (dist1 != 0)
                    throw new Exception("illegal modification of DG level-set when evolving for dt = 0.");
                UpdateLevelset(this.CurrentState.Fields.ToArray(), 0.0, 0.0, 1.0, false); // und doppelt hält besser ;)
                double dist2 = pair1.DGLevelSet.CoordinateVector.L2Distance(oldCoords1);
                if (dist2 != 0)
                    throw new Exception("illegal modification of DG level-set when evolving for dt = 0.");
            }

        }


        // Hack to set the correct time for the levelset tracker on restart
        double restartTime = 0.0;


        public override void PostRestart(double time, TimestepNumber timestep) {
            base.PostRestart(time, timestep);

            if (!this.Control.AdaptiveMeshRefinement) {

                Console.WriteLine("PostRestart temporarily switched off for AMR");

                // Set DG LevelSet by CG LevelSet, if for some reason only the CG is loaded
                if (this.LsUpdater.LevelSets[VariableNames.LevelSetCG].DGLevelSet.L2Norm() == 0.0 && this.LsUpdater.LevelSets[VariableNames.LevelSetCG].CGLevelSet.L2Norm() != 0.0)
                    this.LsUpdater.LevelSets[VariableNames.LevelSetCG].DGLevelSet.AccLaidBack(1.0, this.LsUpdater.LevelSets[VariableNames.LevelSetCG].CGLevelSet);

                // set restart time, used later in the intial tracker updates
                restartTime = time;

                // push stacks, otherwise we get a problem when updating the tracker, parts of the xdg fields are cleared or something
                this.LsUpdater.Tracker.PushStacks();
            }
        }

    }
}
