﻿using BoSSS.Application.XNSE_Solver;
using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Quadrature;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.Control;
using BoSSS.Solution.Utils;
using BoSSS.Solution.XNSECommon;
using ilPSP;
using ilPSP.Tracing;
using ilPSP.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoSSS.Application.XNSE_Solver
{
    class Velocity0 : Parameter
    {
        int D; 

        public Velocity0(int D)
        {
            this.D = D;
            Factory = Velocity0Factory;
        }

        public override IList<string> ParameterNames => BoSSS.Solution.NSECommon.VariableNames.Velocity0Vector(D);

        (string, DGField)[] Velocity0Factory(IReadOnlyDictionary<string, DGField> DomainVarFields)
        {
            var velocity0 = new (string, DGField)[D];
            for(int d = 0; d < D; ++d)
            {
                string velocityname = BoSSS.Solution.NSECommon.VariableNames.VelocityVector(D)[d];
                DGField velocity = DomainVarFields[velocityname];
                string paramName = BoSSS.Solution.NSECommon.VariableNames.Velocity0Vector(D)[d];
                velocity0[d] = (paramName, velocity);
            }
            return velocity0;
        }
    }

    class Velocity0Prescribed : Parameter {
        int degree;

        IDictionary<string, Func<double[], double>> initial;

        string[] names;

        LevelSetTracker LsTrk;

        public Velocity0Prescribed(LevelSetTracker LsTrk, int d,int D, IDictionary<string, Func<double[], double>> initial, int degree) {
            this.degree = degree;
            this.LsTrk = LsTrk;
            Factory = Velocity0PrescribedFactory;


            names = new string[1];
            string velocity = BoSSS.Solution.NSECommon.VariableNames.Velocity0Vector(D)[d];
            names[0] = velocity;
            this.initial = initial;
        }

        public static Velocity0Prescribed CreateFrom(LevelSetTracker LsTrk, int d, int D, AppControl control) {
            
            string velocity = BoSSS.Solution.NSECommon.VariableNames.VelocityVector(D)[d];

            IDictionary<string, Func<double[], double>> initial = new Dictionary<string, Func<double[], double>>();
            foreach (string species in LsTrk.SpeciesNames) {
                string velocityOfSpecies = velocity + "#" + species;
                Func<double[], double> initialVelocity;
                if (control.InitialValues_Evaluators.TryGetValue(velocityOfSpecies, out Func<double[], double> initialValue)) {
                    initialVelocity = initialValue;
                } else {
                    initialVelocity = X => 0.0;
                }
                initial.Add(species, initialVelocity);
            }

            int velocityDegree;
            if (control.FieldOptions.TryGetValue(velocity, out FieldOpts opts)) {
                velocityDegree = opts.Degree;
            } else if (control.FieldOptions.TryGetValue("Velocity*", out FieldOpts velOpts)) {
                velocityDegree = velOpts.Degree;
            } else {
                velocityDegree = -1;
            }
            return new Velocity0Prescribed(LsTrk, d, D, initial, velocityDegree);            
        }

        public override IList<string> ParameterNames => names;

        (string, DGField)[] Velocity0PrescribedFactory(IReadOnlyDictionary<string, DGField> DomainVarFields) {
            XDGBasis basis = new XDGBasis(LsTrk, degree != -1 ? degree : DomainVarFields.First().Value.Basis.Degree);
            XDGField velocity = new XDGField(basis, names[0]);

            foreach(var species in LsTrk.SpeciesNames)
                velocity.GetSpeciesShadowField(species).ProjectField(initial[species]);

            return new (string, DGField)[] { (names[0], velocity) };
        } 


    }

    class Velocity0MeanPrescribed : Velocity0Mean {
        public Velocity0MeanPrescribed(int D, LevelSetTracker LsTrk, int cutCellQuadOrder) :
            base(D, LsTrk, cutCellQuadOrder) { }        

        protected override void Velocity0MeanUpdate(IReadOnlyDictionary<string, DGField> DomainVarFields, IReadOnlyDictionary<string, DGField> ParameterVarFields) {
            for (int d = 0; d < D; ++d) {
                foreach (string speciesName in SpeciesNames) {
                    XDGField paramMeanVelocity = (XDGField)ParameterVarFields[BoSSS.Solution.NSECommon.VariableNames.Velocity0MeanVector(D)[d]];
                    DGField speciesParam = paramMeanVelocity.GetSpeciesShadowField(speciesName);

                    XDGField velocity = (XDGField)ParameterVarFields[BoSSS.Solution.NSECommon.VariableNames.Velocity0Vector(D)[d]];
                    DGField speciesVelocity = velocity.GetSpeciesShadowField(speciesName);

                    //Uncut
                    speciesParam.SetMeanValueTo(speciesVelocity);

                    //Cut
                    CellMask cutCells = regions.GetSpeciesMask(speciesName);
                    SpeciesId speciesId = speciesMap[speciesName];
                    CellQuadratureScheme scheme = schemeHelper.GetVolumeQuadScheme(speciesId, IntegrationDomain: cutCells);
                    SetMeanValueToMeanOf(speciesParam, speciesVelocity, minvol, cutCellQuadOrder, scheme);
                }
            }
        }

    }

    class Velocity0Mean : Parameter, ILevelSetParameter
    {
        protected int D;

        protected int cutCellQuadOrder;

        protected LevelSetTracker LsTrk;

        public Velocity0Mean(int D, LevelSetTracker LsTrk, int cutCellQuadOrder)
        {
            this.D = D;
            Factory = Velocity0MeanFactory;
            Update = Velocity0MeanUpdate;
            this.cutCellQuadOrder = cutCellQuadOrder;
            this.LsTrk = LsTrk;
        }

        public override IList<string> ParameterNames => BoSSS.Solution.NSECommon.VariableNames.Velocity0MeanVector(D);

        protected (string, DGField)[] Velocity0MeanFactory(IReadOnlyDictionary<string, DGField> DomainVarFields)
        {
            var velocity0Mean = new (string, DGField)[D];
            for(int d = 0; d < D; ++d)
            {
                XDGBasis U0meanBasis = new XDGBasis(LsTrk, 0);
                string paramName = BoSSS.Solution.NSECommon.VariableNames.Velocity0MeanVector(D)[d];
                XDGField U0mean = new XDGField(U0meanBasis, paramName);
                velocity0Mean[d] = (paramName, U0mean);
            }
            return velocity0Mean;
        }

        protected IList<string> SpeciesNames;

        protected LevelSetTracker.LevelSetRegions regions;

        protected IDictionary<string, SpeciesId> speciesMap;

        protected XQuadSchemeHelper schemeHelper;

        protected double minvol;

        public void UpdateParameters(DualLevelSet levelSet, LevelSetTracker lsTrkr, double time, IReadOnlyDictionary<string, DGField> ParameterVarFields)
        {
            SpeciesNames = lsTrkr.SpeciesNames;
            regions = lsTrkr.Regions;
            IList<SpeciesId> speciesIds = lsTrkr.SpeciesIdS;
            schemeHelper = lsTrkr.GetXDGSpaceMetrics(speciesIds.ToArray(), cutCellQuadOrder).XQuadSchemeHelper;
            minvol = Math.Pow(lsTrkr.GridDat.Cells.h_minGlobal, D);

            speciesMap = new Dictionary<string, SpeciesId>(SpeciesNames.Count);
            foreach (string name in SpeciesNames)
            {
                speciesMap.Add(name, lsTrkr.GetSpeciesId(name));
            }
        }

        protected virtual void Velocity0MeanUpdate(IReadOnlyDictionary<string, DGField> DomainVarFields, IReadOnlyDictionary<string, DGField> ParameterVarFields)
        {
            for(int d = 0; d < D; ++d)
            {
                foreach(string speciesName in SpeciesNames)
                {
                    XDGField paramMeanVelocity = (XDGField)ParameterVarFields[BoSSS.Solution.NSECommon.VariableNames.Velocity0MeanVector(D)[d]];
                    DGField speciesParam = paramMeanVelocity.GetSpeciesShadowField(speciesName);

                    XDGField velocity = (XDGField)DomainVarFields[BoSSS.Solution.NSECommon.VariableNames.VelocityVector(D)[d]];
                    DGField speciesVelocity = velocity.GetSpeciesShadowField(speciesName);

                    //Uncut
                    speciesParam.SetMeanValueTo(speciesVelocity);

                    //Cut
                    CellMask cutCells = regions.GetSpeciesMask(speciesName);
                    SpeciesId speciesId = speciesMap[speciesName];
                    CellQuadratureScheme scheme = schemeHelper.GetVolumeQuadScheme(speciesId, IntegrationDomain: cutCells);
                    SetMeanValueToMeanOf(speciesParam, speciesVelocity, minvol, cutCellQuadOrder, scheme);
                }
            }
        }

        protected static void SetMeanValueToMeanOf(DGField target, DGField source, double minvol, int order, CellQuadratureScheme scheme)
        {
            //Cut
            int D = source.GridDat.SpatialDimension;
            var rule = scheme.Compile(source.GridDat, order);
            CellQuadrature.GetQuadrature(new int[] { 2 },
                source.GridDat,
                rule,
                delegate (int i0, int Length, QuadRule QR, MultidimensionalArray EvalResult) {
                    EvalResult.Clear();
                    source.Evaluate(i0, Length, QR.Nodes, EvalResult.ExtractSubArrayShallow(-1, -1, 0));
                    var Vol = EvalResult.ExtractSubArrayShallow(-1, -1, 1);
                    Vol.SetAll(1.0);
                },
                delegate (int i0, int Length, MultidimensionalArray ResultsOfIntegration) {
                    for (int i = 0; i < Length; i++)
                    {
                        int jCell = i + i0;

                        double Volume = ResultsOfIntegration[i, 1];
                        if (Math.Abs(Volume) < minvol * 1.0e-12)
                        {
                            // keep current value
                            // since the volume of species 'Spc' in cell 'jCell' is 0.0, the value in this cell should have no effect
                        }
                        else
                        {
                            double IntVal = ResultsOfIntegration[i, 0];
                            target.SetMeanValue( jCell, IntVal / Volume);
                        }

                    }
                }).Execute();
        }
    }

    class Gravity : Parameter
    {
        int degree;

        Func<double[], double> initial;

        string[] names;

        public Gravity(string species, int d, int D, Func<double[], double> initial, double rho, int degree)
        {
            this.degree = degree;
            Factory = GravityFactory;

            names = new string[1];
            string gravity = BoSSS.Solution.NSECommon.VariableNames.GravityVector(D)[d];
            names[0] = gravity + "#" + species;
            this.initial = X => -initial(X) * rho;
        }

        public static Gravity CreateFrom(string species, int d, int D, AppControl control, double rho)
        {
            string gravity = BoSSS.Solution.NSECommon.VariableNames.GravityVector(D)[d];
            string gravityOfSpecies = gravity + "#" + species;
            Func<double[], double> initialGravity;
            if (control.InitialValues_Evaluators.TryGetValue(gravityOfSpecies, out Func<double[], double> initialValue))
            {
                initialGravity = initialValue;
            }
            else
            {
                initialGravity = X => 0.0;
            }

            int gravityDegree;
            if (control.FieldOptions.TryGetValue(gravityOfSpecies, out FieldOpts opts))
            {
                gravityDegree = opts.Degree;
            }
            else if(control.FieldOptions.TryGetValue("Velocity*", out FieldOpts velOpts))
            {
                gravityDegree = velOpts.Degree;
            }
            else
            {
                gravityDegree = 0;
            }
            return new Gravity(species, d, D, initialGravity, rho, gravityDegree);
        }

        public override IList<string> ParameterNames => names;

        (string, DGField)[] GravityFactory(IReadOnlyDictionary<string, DGField> DomainVarFields)
        {
            Basis basis = new Basis(DomainVarFields.First().Value.GridDat, degree);
            DGField gravity = new SinglePhaseField(basis, names[0]);
            gravity.ProjectField(initial);
            return new (string, DGField)[] { (names[0], gravity) };
        }
    }

    class Normals : Parameter, ILevelSetParameter
    {
        int D;

        int degree;
        
        public Normals(int D, int degree)
        {
            this.D = D;
            this.degree = degree;
            Factory = NormalFactory;
        }

        public override IList<string> ParameterNames => BoSSS.Solution.NSECommon.VariableNames.NormalVector(D);

        public void UpdateParameters(DualLevelSet levelSet, LevelSetTracker lsTrkr, double time, IReadOnlyDictionary<string, DGField> ParameterVarFields)
        {
            LevelSet Phi = levelSet.DGLevelSet;
            DGField[] Normals = new SinglePhaseField[D];
            for (int i = 0; i < D; ++i)
            {
                Normals[i] = ParameterVarFields[BoSSS.Solution.NSECommon.VariableNames.NormalVector(D)[i]];
            }
            VectorField<DGField> normalVector = new VectorField<DGField>(Normals);
            Normals.Clear();
            normalVector.Gradient(1.0, Phi);
        }

        (string, DGField)[] NormalFactory(IReadOnlyDictionary<string, DGField> DomainVarFields)
        {
            IGridData gridData = DomainVarFields.First().Value.GridDat;
            Basis basis = new Basis(gridData, degree);
            VectorField<SinglePhaseField> Normals = new VectorField<SinglePhaseField>(D, basis, SinglePhaseField.Factory);

            (string, DGField)[] normals = new (string, DGField)[D];
            for(int d = 0; d <D; ++d)
            {
                normals[d] = (BoSSS.Solution.NSECommon.VariableNames.NormalVector(D)[d], Normals[d] );
            }
            return normals;
        }
    }
    
    class Curvature : Parameter
    {
        int D;

        LevelSetTracker LsTrk;

        int degree;

        int m_HMForder;

        bool solveKineticEnergyEquation;

        bool isEvaporation;

        DoNotTouchParameters AdvancedDiscretizationOptions;

        public Curvature(LevelSetTracker LsTrkr, int degree, int m_HMForder, bool isEvaporation, bool solveKineticEnergyEquation, DoNotTouchParameters AdvancedDiscretizationOptions)
        {
            this.LsTrk = LsTrkr;
            this.degree = degree;
            this.m_HMForder = m_HMForder;
            this.isEvaporation = isEvaporation;
            this.solveKineticEnergyEquation = solveKineticEnergyEquation;
            this.AdvancedDiscretizationOptions = AdvancedDiscretizationOptions;

            Factory = CurvatureFactory;
        }

        public static Curvature CreateFrom(XNSE_Control control, XNSFE_OperatorConfiguration xopConfig, LevelSetTracker LsTrkr, int m_HMForder)
        {
            string curvature = BoSSS.Solution.NSECommon.VariableNames.Curvature;
            int degree;
            if (control.FieldOptions.TryGetValue(curvature, out FieldOpts opts))
            {
                degree = opts.Degree;
            }
            else
            {
                degree = 0;
            }
            bool solveKineticEnergyEquation = control.solveKineticEnergyEquation;

            bool isEvaporation = xopConfig.isEvaporation;

            DoNotTouchParameters AdvancedDiscretizationOptions = control.AdvancedDiscretizationOptions;
            return new Curvature(LsTrkr, degree, m_HMForder, isEvaporation, solveKineticEnergyEquation, AdvancedDiscretizationOptions);
        }

        public override IList<string> ParameterNames => new string[] { BoSSS.Solution.NSECommon.VariableNames.Curvature };

        (string, DGField)[] CurvatureFactory(IReadOnlyDictionary<string, DGField> DomainVarFields)
        {
            string name = BoSSS.Solution.NSECommon.VariableNames.Curvature;
            Basis basis = new Basis(DomainVarFields.First().Value.GridDat, degree);
            SinglePhaseField curvature = new SinglePhaseField(basis, name);

            return new (string, DGField)[]
            {
                (name, curvature)
            };
        }
    }

    class MaxSigma : Parameter, ILevelSetParameter
    {
        PhysicalParameters physParams;

        DoNotTouchParameters dntParams;

        int cutCellQuadOrder;

        double dt;

        public MaxSigma(PhysicalParameters physParams, DoNotTouchParameters dntParams, int cutCellQuadOrder, double dt)
        {
            this.physParams = physParams;
            this.dntParams = dntParams;
            this.cutCellQuadOrder = cutCellQuadOrder;
            this.dt = dt;
            Factory = MaxSigmaFactory;
        }

        string[] name = new string[] { BoSSS.Solution.NSECommon.VariableNames.MaxSigma };

        public override IList<string> ParameterNames => name;

        (string ParameterName, DGField ParamField)[] MaxSigmaFactory(IReadOnlyDictionary<string, DGField> DomainVarFields)
        {
            string name = BoSSS.Solution.NSECommon.VariableNames.MaxSigma;
            IGridData grid = DomainVarFields.FirstOrDefault().Value.GridDat;
            Basis constant = new Basis(grid, 0);
            SinglePhaseField sigmaField = new SinglePhaseField(constant, name);
            return new (string ParameterName, DGField ParamField)[] { (name, sigmaField) };
        }

        public void UpdateParameters(DualLevelSet levelSet, LevelSetTracker lsTrkr, double time, IReadOnlyDictionary<string, DGField> ParameterVarFields)
        {
            DGField sigmaMax = ParameterVarFields[BoSSS.Solution.NSECommon.VariableNames.MaxSigma];

            IDictionary<SpeciesId, MultidimensionalArray> InterfaceLengths =
                lsTrkr.GetXDGSpaceMetrics(lsTrkr.SpeciesIdS.ToArray(), cutCellQuadOrder).CutCellMetrics.InterfaceArea;

            foreach (Chunk cnk in lsTrkr.Regions.GetCutCellMask())
            {
                for (int i = cnk.i0; i < cnk.JE; i++)
                {
                    double ILen = InterfaceLengths.ElementAt(0).Value[i];
                    //ILen /= LevSet_Deg;
                    double sigmaILen_Max = (this.physParams.rho_A + this.physParams.rho_B)
                           * Math.Pow(ILen, 3) / (2 * Math.PI * dt.Pow2());

                    if (dntParams.SetSurfaceTensionMaxValue && (physParams.Sigma > sigmaILen_Max))
                    {
                        sigmaMax.SetMeanValue(i, sigmaILen_Max * 0.5);
                        //Console.WriteLine("set new sigma value: {0}; {1}", sigmaILen_Max, sigmaILen_Max/physParams.Sigma);
                    }
                    else
                    {
                        sigmaMax.SetMeanValue(i, this.physParams.Sigma * 0.5);
                    }
                }
            }
        }
    }
}

