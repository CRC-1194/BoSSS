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

using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Solution.CompressibleFlowCommon;
using BoSSS.Solution.CompressibleFlowCommon.MaterialProperty;
using BoSSS.Solution.CompressibleFlowCommon.Residual;
using BoSSS.Solution.Queries;
using CNS.Convection;
using CNS.EquationSystem;
using CNS.Source;
using ilPSP;
using ilPSP.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CNS.Tests.BoundaryConditions {

    /// <summary>
    /// A set of test cases to verify the correct implementation of different
    /// types of boundary conditions for the Euler equations. To separate the
    /// influences of different types of errors, most of them use
    /// SupersonicInlet at some edges, i.e. a non-physical
    /// Dirichlet boundary condition. This speeds up convergence and does not
    /// spoil the exact solution, as long as the Dirichlet values do not
    /// contradict the analytical solution
    /// </summary>
    public static class ControlFiles {

        // Exact solutions
        private static readonly double Ma = 1.0;
        private static readonly double MaSquared = 1.0;
        private static readonly double gamma = 1.4;
        private static Func<double[], double> exactDensity = X => (2.0 + Math.Cos(2 * X[0])) / 2.0;
        private static Func<double[], double> exactMomentum = X => (7.0 - Math.Cos(4.0 * X[0])) / 16.0;
        private static Func<double[], double> exactEnergy = X => (25.0 + 7.0 / 16.0 * MaSquared * (-2.0 + Math.Cos(2.0 * X[0])) * (-2.0 + Math.Cos(2.0 * X[0]))) * (2.0 + Math.Cos(2.0 * X[0])) / 20.0;
        private static Func<double[], double> exactVelocity = X => exactMomentum(X) / exactDensity(X);
        private static Func<double[], double> exactPressure = X => 1 + Math.Cos(2.0 * X[0]) / 2;
        private static Func<double[], double> localMach = X => Ma * exactVelocity(X) / (Math.Sqrt(exactPressure(X) / exactDensity(X)));
        // Spurk: p305, eq(9.100)
        private static Func<double[], double> totalPressure = X => exactPressure(X) * Math.Pow((gamma - 1.0) / 2 * localMach(X) * localMach(X) + 1.0, gamma / (gamma - 1.0));
        // Spurk: p305, eq(9.101)
        private static Func<double[], double> totalTemperature = X => exactPressure(X) / exactDensity(X) * ((gamma - 1.0) / 2 * localMach(X) * localMach(X) + 1.0);

        // Control templates
        private static CNSControl[] GetTemplates() {
            int minDivisions = 0;
            int maxDivisions = 2;
            int minDegree = 0;
            int maxDegree = 3;

            List<CNSControl> result = new List<CNSControl>();
            for (int dgDegree = minDegree; dgDegree <= maxDegree; dgDegree++) {
                for (int divisions = minDivisions; divisions <= maxDivisions; divisions++) {

                    CNSControl c = new CNSControl();

                    //c.DbPath = @"c:\bosss_dbv2\exp";
                    c.savetodb = false;
                    int noOfCells = (2 << divisions) * 8;

                    c.PrintInterval = 1000;
                    c.ResidualInterval = 100;
                    c.saveperiod = 100000;

                    c.ActiveOperators = Operators.Convection | Operators.CustomSource;
                    c.ConvectiveFluxType = ConvectiveFluxTypes.OptimizedHLLC;
                    c.ExplicitScheme = ExplicitSchemes.RungeKutta;
                    c.ExplicitOrder = 1;
                    c.EquationOfState = IdealGas.Air;
                    c.MachNumber = 1.0;

                    c.AddVariable(CompressibleVariables.Density, dgDegree);
                    c.AddVariable(CompressibleVariables.Momentum.xComponent, dgDegree);
                    c.AddVariable(CompressibleVariables.Energy, dgDegree);
                    c.AddVariable(CNSVariables.Velocity.xComponent, dgDegree);
                    c.AddVariable(CNSVariables.Pressure, dgDegree);
                    c.AddVariable(CNSVariables.LocalMachNumber, dgDegree);

                    c.InitialValues_Evaluators.Add(CompressibleVariables.Density, exactDensity);
                    c.InitialValues_Evaluators.Add(CNSVariables.Velocity.xComponent, exactVelocity);
                    c.InitialValues_Evaluators.Add(CNSVariables.Pressure, exactPressure);

                    c.CustomContinuitySources.Add(map => new AdHocSourceTerm(map, (x, t, state) => -Math.Sin(4.0 * x[0]) / 4.0));
                    c.CustomMomentumSources[0].Add(map => new AdHocSourceTerm(map, (x, t, state) => (160.0 - 35.0 * MaSquared - 56.0 * MaSquared * Math.Cos(2.0 * x[0]) + 21.0 * MaSquared * Math.Cos(4.0 * x[0])) * Math.Sin(2.0 * x[0]) / (224.0 * MaSquared)));
                    c.CustomEnergySources.Add(map => new AdHocSourceTerm(map, (x, t, state) => -(7.0 / 640.0) * Math.Sin(2 * x[0]) * ((160.0 + 3.0 * MaSquared) * Math.Cos(2 * x[0]) + MaSquared * (10.0 - 6.0 * Math.Cos(4.0 * x[0]) + Math.Cos(6.0 * x[0])))));

                    c.Queries.Add("densityError", QueryLibrary.L2Error(CompressibleVariables.Density, exactDensity, 10));
                    c.Queries.Add("momentumError", QueryLibrary.L2Error(CompressibleVariables.Momentum[0], exactMomentum, 10));
                    c.Queries.Add("energyError", QueryLibrary.L2Error(CompressibleVariables.Energy, exactEnergy, 10));

                    c.Paramstudy_CaseIdentification.AddRange(new Tuple<string, object>("divisions", divisions));
                    c.Paramstudy_CaseIdentification.AddRange(new Tuple<string, object>("dgDegree", dgDegree));

                    c.ResidualLoggerType = ResidualLoggerTypes.ChangeRate | ResidualLoggerTypes.Query;
                    c.ResidualBasedTerminationCriteria.Add("changeRate_abs_rhoE", 1E-9);

                    c.dtMin = 0.0;
                    c.dtMax = 1.0;
                    c.CFLFraction = 0.1;
                    c.Endtime = 10000;
                    c.NoOfTimesteps = 1000000;

                    c.ProjectName = "MMS1D_Euler_Mach=" + c.MachNumber + "_dg=" + dgDegree + "_cells=" + noOfCells;

                    result.Add(c);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Test using SupersonicInlet (Dirichlet) everywhere
        /// </summary>
        /// <returns></returns>
        public static CNSControl[] EulerSupersonicInlet1D() {
            CNSControl[] templates = GetTemplates();

            foreach (CNSControl c in templates) {
                int divisions = (int)c.Paramstudy_CaseIdentification.Single(t => t.Item1 == "divisions").Item2;

                int noOfCells = (2 << divisions) * 8;
                c.GridFunc = delegate {
                    GridCommons grid = Grid1D.LineGrid(
                        GenericBlas.Linspace(0.0, Math.PI / 2.0 + 0.0, noOfCells + 1));
                    grid.EdgeTagNames.Add(1, "supersonicInlet");
                    grid.DefineEdgeTags((Vector X) => 1);
                    return grid;
                };
                c.ProjectName += "_supersonicAll";

                c.AddBoundaryValue("supersonicInlet", CompressibleVariables.Density, exactDensity);
                c.AddBoundaryValue("supersonicInlet", CNSVariables.Velocity[0], exactVelocity);
                c.AddBoundaryValue("supersonicInlet", CNSVariables.Pressure, exactPressure);
            }

            return templates.ToArray();
        }

        /// <summary>
        /// Test using SupersonicInlet (Dirichlet) at the inlet
        /// and SubsonicOutlet at the outlet
        /// </summary>
        /// <returns></returns>
        public static CNSControl[] EulerSubsonicOutlet1D() {
            CNSControl[] templates = GetTemplates();

            foreach (CNSControl c in templates) {
                int divisions = (int)c.Paramstudy_CaseIdentification.Single(t => t.Item1 == "divisions").Item2;

                int noOfCells = (2 << divisions) * 8;
                c.GridFunc = delegate {
                    GridCommons grid = Grid1D.LineGrid(
                        GenericBlas.Linspace(0.0, Math.PI / 2.0 + 0.0, noOfCells + 1));
                    grid.EdgeTagNames.Add(1, "supersonicInlet");
                    grid.EdgeTagNames.Add(2, "subsonicOutlet");
                    grid.DefineEdgeTags((Vector x) => Math.Abs(x[0]) < 1e-14 ? (byte)1 : (byte)2);
                    return grid;
                };
                c.ProjectName += "_subsonicOutlet";

                c.AddBoundaryValue("supersonicInlet", CompressibleVariables.Density, exactDensity);
                c.AddBoundaryValue("supersonicInlet", CNSVariables.Velocity[0], exactVelocity);
                c.AddBoundaryValue("supersonicInlet", CNSVariables.Pressure, exactPressure);

                c.AddBoundaryValue("subsonicOutlet", CNSVariables.Pressure, exactPressure);
            }

            return templates;
        }

        /// <summary>
        /// Test using subsonicInlet at the inlet and
        /// supersonicInlet (Dirichlet) at the outlet
        /// </summary>
        /// <returns></returns>
        public static CNSControl[] EulerSubsonicInlet1D() {
            CNSControl[] templates = GetTemplates();

            foreach (CNSControl c in templates) {
                int divisions = (int)c.Paramstudy_CaseIdentification.Single(t => t.Item1 == "divisions").Item2;

                int noOfCells = (2 << divisions) * 8;
                c.GridFunc = delegate {
                    GridCommons grid = Grid1D.LineGrid(
                        GenericBlas.Linspace(0.0, Math.PI / 2.0 + 0.0, noOfCells + 1));
                    grid.EdgeTagNames.Add(1, "supersonicInlet");
                    grid.EdgeTagNames.Add(2, "subsonicInlet");
                    grid.DefineEdgeTags((Vector x) => Math.Abs(x[0]) < 1e-14 ? (byte)2 : (byte)1);
                    return grid;
                };
                c.ProjectName += "_subsonicInlet2";

                c.AddBoundaryValue("subsonicInlet", CompressibleVariables.Density, exactDensity);
                c.AddBoundaryValue("subsonicInlet", CNSVariables.Velocity[0], exactVelocity);

                c.AddBoundaryValue("supersonicInlet", CompressibleVariables.Density, exactDensity);
                c.AddBoundaryValue("supersonicInlet", CNSVariables.Velocity[0], exactVelocity);
                c.AddBoundaryValue("supersonicInlet", CNSVariables.Pressure, exactPressure);
            }

            return templates;
        }

        /// <summary>
        /// Test using SubsonicInlet at the inlet and
        /// SubsonicOutlet at the outlet. That is, this test case
        /// uses physically correct boundary conditions
        /// </summary>
        /// <returns></returns>
        public static CNSControl[] EulerSubsonicInletAndOutlet1D() {
            CNSControl[] templates = GetTemplates();

            foreach (CNSControl c in templates) {
                int divisions = (int)c.Paramstudy_CaseIdentification.Single(t => t.Item1 == "divisions").Item2;

                int noOfCells = (2 << divisions) * 8;
                c.GridFunc = delegate {
                    GridCommons grid = Grid1D.LineGrid(
                        GenericBlas.Linspace(0.0, Math.PI / 2.0 + 0.0, noOfCells + 1));
                    grid.EdgeTagNames.Add(1, "subsonicInlet");
                    grid.EdgeTagNames.Add(2, "subsonicOutlet");
                    grid.DefineEdgeTags((Vector x) => Math.Abs(x[0]) < 1e-14 ? (byte)1 : (byte)2);
                    return grid;
                };
                c.ProjectName += "_subsonicAll";

                c.AddBoundaryValue("subsonicInlet", CompressibleVariables.Density, exactDensity);
                c.AddBoundaryValue("subsonicInlet", CNSVariables.Velocity[0], exactVelocity);

                c.AddBoundaryValue("subsonicOutlet", CNSVariables.Pressure, exactPressure);
            }

            return templates;
        }

        /// <summary>
        /// Uses SubsonicPressureInlet at the inlet and
        /// SupersonicInlet (Dirichlet) at the outlet
        /// </summary>
        /// <returns></returns>
        public static CNSControl[] EulerSubsonicPressureInletTest1D() {
            CNSControl[] templates = GetTemplates();

            foreach (CNSControl c in templates) {
                int divisions = (int)c.Paramstudy_CaseIdentification.Single(t => t.Item1 == "divisions").Item2;

                int noOfCells = (2 << divisions) * 8;
                c.GridFunc = delegate {
                    GridCommons grid = Grid1D.LineGrid(
                        GenericBlas.Linspace(0.0, Math.PI / 2.0 + 0.0, noOfCells + 1));
                    grid.EdgeTagNames.Add(1, "supersonicInlet");
                    grid.EdgeTagNames.Add(2, "subsonicPressureInlet");
                    grid.DefineEdgeTags((Vector x) => Math.Abs(x[0]) < 1e-14 ? (byte)2 : (byte)1);
                    return grid;
                };

                c.AddBoundaryValue("subsonicPressureInlet", "p0", totalPressure);
                c.AddBoundaryValue("subsonicPressureInlet", "T0", totalTemperature);

                c.AddBoundaryValue("supersonicInlet", CompressibleVariables.Density, exactDensity);
                c.AddBoundaryValue("supersonicInlet", CNSVariables.Velocity[0], exactVelocity);
                c.AddBoundaryValue("supersonicInlet", CNSVariables.Pressure, exactPressure);
            }

            return templates;
        }

        /// <summary>
        /// Test using SubsonicPressureInlet at the inlet and
        /// SubsonicOutlet at the outlet. That is, this test case
        /// uses physically correct boundary conditions
        /// </summary>
        /// <returns></returns>
        public static CNSControl[] EulerSubsonicPressureInletAndOutletTest1D() {
            CNSControl[] templates = GetTemplates();

            foreach (CNSControl c in templates) {
                int divisions = (int)c.Paramstudy_CaseIdentification.Single(t => t.Item1 == "divisions").Item2;

                int noOfCells = (2 << divisions) * 8;
                c.GridFunc = delegate {
                    GridCommons grid = Grid1D.LineGrid(
                        GenericBlas.Linspace(0.0, Math.PI / 2.0 + 0.0, noOfCells + 1));
                    grid.EdgeTagNames.Add(1, "subsonicPressureInlet");
                    grid.EdgeTagNames.Add(2, "subsonicOutlet");
                    grid.DefineEdgeTags((Vector x) => Math.Abs(x[0]) < 1e-14 ? (byte)1 : (byte)2);
                    return grid;
                };

                c.AddBoundaryValue("subsonicPressureInlet", "p0", totalPressure);
                c.AddBoundaryValue("subsonicPressureInlet", "T0", totalTemperature);

                c.AddBoundaryValue("subsonicOutlet", CNSVariables.Pressure, exactPressure);
            }

            return templates;
        }
    }
}
