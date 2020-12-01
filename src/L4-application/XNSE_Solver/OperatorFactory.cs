﻿using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.XNSECommon.Operator;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BoSSS.Foundation.XDG.XSpatialOperatorMk2;

namespace BoSSS.Application.XNSE_Solver
{
    class OperatorFactory
    {
        SystemOfEquations eqSystem;

        ParameterList parameters;

        DelOperatorCoefficients coefficients;

        public OperatorFactory()
        {
            eqSystem = new SystemOfEquations();
            parameters = new ParameterList();
        }

        public void AddEquation(SpatialEquation equation)
        {
            eqSystem.AddEquation(equation);
        }

        public void AddEquation(SurfaceEquation equation)
        {
            eqSystem.AddEquation(equation);
        }

        public void AddEquation(BulkEquation equation)
        {
            eqSystem.AddEquation(equation);
        }

        public void AddParameter(Parameter parameter)
        {
            parameters.AddParameter(parameter);
        }

        public delegate void DelOperatorCoefficients(CoefficientSet r, LevelSetTracker lstrk, SpeciesId spc, int quadOrder, int TrackerHistoryIdx, double time);

        public void AddCoefficient(DelOperatorCoefficients Coeff)
        {
            coefficients += Coeff;
        }

        public XSpatialOperatorMk2 GetSpatialOperator(int quadOrder)
        {
            int QuadOrderFunc(int[] DomvarDegs, int[] ParamDegs, int[] CodvarDegs)
            {
                return quadOrder;
            }
            XSpatialOperatorMk2 XOP =  GetSpatialOperator(QuadOrderFunc);
            return XOP;
        }

        public XSpatialOperatorMk2 GetSpatialOperator(Func<int[], int[], int[], int> QuadOrderFunc)
        {
            var XOP = CreateSpatialOperator(QuadOrderFunc);
            AddEquationComponents(XOP);
            AddSurfaceEquationComponents(XOP);
            AddGhostEquationComponents(XOP);
            AddTemporalOperator(XOP);
            AddParameterDelegates(XOP);
            AddCoefficients(XOP);
            return XOP;
        }

        XSpatialOperatorMk2 CreateSpatialOperator(Func<int[], int[], int[], int> QuadOrderFunc)
        {
            string[] domainVars = eqSystem.DomainVars();
            string[] codomainVars = eqSystem.CoDomainVars();
            string[] parameters = eqSystem.Parameters();
            string[] species = eqSystem.Species();

            var spatialOperator = new XSpatialOperatorMk2(
                domainVars,
                parameters,
                codomainVars,
                QuadOrderFunc,
                species);
            return spatialOperator;
        }

        void AddTemporalOperator(XSpatialOperatorMk2 spatialOperator)
        {
            (string, double[])[] diagonal = eqSystem.MassDiagonal();
            spatialOperator.TemporalOperator = new ConstantXTemporalOperator(spatialOperator, diagonal);
        }

        void AddParameterDelegates(XSpatialOperatorMk2 spatialOperator)
        {
            ICollection<DelParameterFactory> factories = parameters.Factories(spatialOperator.ParameterVar);
            foreach(DelParameterFactory factory in factories)
            {
                spatialOperator.ParameterFactories.Add(factory);
            }
            ICollection<DelPartialParameterUpdate> updates = parameters.ParameterUpdates(spatialOperator.ParameterVar);
            foreach (DelPartialParameterUpdate update in updates)
            {
                spatialOperator.ParameterUpdates.Add(update);
            }
        }

        void AddEquationComponents(XSpatialOperatorMk2 spatialOperator)
        {
            foreach (SpatialEquation equation in eqSystem.SpatialEquations)
            {
                foreach (IEquationComponent component in equation.Components)
                {
                    spatialOperator.EquationComponents[equation.CodomainName].Add(component);
                }
            }
        }

        void AddSurfaceEquationComponents(XSpatialOperatorMk2 spatialOperator)
        {
            foreach (SurfaceEquation equation in eqSystem.InterfaceEquations)
            {
                if (equation.SurfaceComponents != null)
                {
                    foreach (IEquationComponent component in equation.SurfaceComponents)
                    {
                        spatialOperator.SurfaceElementOperator.EquationComponents[equation.CodomainName].Add(component);
                    }
                }
            }
        }

        void AddGhostEquationComponents(XSpatialOperatorMk2 spatialOperator)
        {
            foreach (BulkEquation equation in eqSystem.BulkEquations)
            {
                if (equation.GhostComponents != null)
                {
                    foreach (IEquationComponent component in equation.GhostComponents)
                    {
                        spatialOperator.GhostEdgesOperator.EquationComponents[equation.CodomainName].Add(component);
                    }
                }
            }
        }
        

        void AddCoefficients(XSpatialOperatorMk2 spatialOperator)
        {
            spatialOperator.OperatorCoefficientsProvider = Coefficients;
        }

        CoefficientSet Coefficients(LevelSetTracker lstrk, SpeciesId spc, int quadOrder, int TrackerHistoryIdx, double time)
        {
            var r = new CoefficientSet()
            {
                GrdDat = lstrk.GridDat
            };
            var g = lstrk.GridDat;
            if (g is Foundation.Grid.Classic.GridData cgdat)
            {
                r.CellLengthScales = cgdat.Cells.CellLengthScale;
                r.EdgeLengthScales = cgdat.Edges.h_min_Edge;

            }
            else
            {
                Console.Error.WriteLine("Rem: still missing cell length scales for grid type " + g.GetType().FullName);
            }

            coefficients?.Invoke(r, lstrk, spc, quadOrder, TrackerHistoryIdx, time);

            return r;
        }
    }
}
