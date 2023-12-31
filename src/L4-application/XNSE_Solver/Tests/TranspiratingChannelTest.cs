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

using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Solution.Control;
using BoSSS.Solution.NSECommon;
using BoSSS.Solution.Utils;
using ilPSP.Utils;
using System;
using System.Collections.Generic;

namespace BoSSS.Application.XNSE_Solver.Tests {

    /// <summary>
    /// The transpirating channel: this is a test for the convection operator;
    /// It is, however, only valid for equal fluid parameters in both phases.
    /// </summary>
    public class TranspiratingChannelTest : IXNSETest {

        public bool TestImmersedBoundary => false;

        /// <summary>
        /// nix
        /// </summary>
        public Func<double[], double, double> GetPhi2() {
            throw new NotImplementedException(); // will never be called, as long as 'TestImmersedBoundary' == false;
        }

        public Func<double[], double, double> GetPhi2U(int d) {
            throw new NotImplementedException();
        }

        public bool Material {
            get {
                return true;
            }
        }

        public bool steady {
            get {
                return true;
            }
        }

        /// <summary>
        /// this test won't work without convection!
        /// </summary>
        public bool IncludeConvection {
            get {
                return true;
            }
        }

        /// <summary>
        /// Since the material parameters of both phases are the same, we can test arbitrary level-set shapes,
        /// as long as they are continuous and polynomial.
        /// </summary>
        public Func<double[], double, double> GetPhi() {
            switch (m_spatialDimension) {
                case 2:
                    return ((_2D)(delegate (double x, double y) {
                        if (x > 2.0)
                            x -= 4.0;

                        return -y - (2.0 / 5.0) * x + (1.0 / 10.0) * x * x * x;
                        //return -1;
                    })).Convert_xy2X().Convert_X2Xt();
                case 3:
                    return ((_3D)(delegate (double x, double y, double z) {
                        if (x > 2.0)
                            x -= 4.0;

                        return -y - (2.0 / 5.0) * x + (1.0 / 10.0) * x * x * x;

                    })).Convert_xyz2X().Convert_X2Xt();
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }
                

        public int LevelsetPolynomialDegree {
            get {
                return 3;
            }
        }

        int m_spatialDimension;

        public TranspiratingChannelTest(double _U2, bool periodic = false, int spatDim = 2) {
            this.U2 = _U2;
            this.m_periodic = periodic;
            this.m_spatialDimension = spatDim;
        }

        public Func<double[], double> GetF(string species, int d) {
            switch (m_spatialDimension) {
                case 2:
                    if (m_periodic) {
                        if (d == 0) {
                            return ((_2D)((x, y) => 1)).Convert_xy2X();
                        } else if (d == 1) {
                            return ((_2D)((x, y) => 0.0)).Convert_xy2X();
                        } else {
                            throw new ArgumentOutOfRangeException();
                        }
                    } else {
                        return ((_2D)((x, y) => 0.0)).Convert_xy2X();
                    }
                case 3:
                    if (m_periodic) {
                        if (d == 0) {
                            return ((_3D)((x, y, z) => 1)).Convert_xyz2X();
                        } else if (d == 1) {
                            return ((_3D)((x, y, z) => 0.0)).Convert_xyz2X();
                        } else if (d == 2) {
                            return ((_3D)((x, y, z) => 0.0)).Convert_xyz2X();
                        } else {
                            throw new ArgumentOutOfRangeException();
                        }
                    } else {
                        return ((_3D)((x, y, z) => 0.0)).Convert_xyz2X();
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

   
        /// <summary>
        /// see "A SIMPLE based discontinuous Galerkin solver for steady
        /// incompressible flows" by Benedikt Klein, Florian Kummer and Martin Oberlack,
        /// Journal of Computational Physics 237 (2013) 235–250
        /// </summary>
        public Func<double[], double, double> GetU(string species, int d) {
            switch (m_spatialDimension) {
                case 2:
                    if (d == 0) {
                        if (Math.Abs(U2) >= 1.0e-10) {
                            return ((_2D)((x, y) =>
                                ((y + 1) / U2 - (2.0 * (1.0 - Math.Exp(U2 * (y + 1) * Rey))) / (U2 * (1 - Math.Exp(2.0 * U2 * Rey))))
                                )).Convert_xy2X().Convert_X2Xt();
                        } else {
                            return ((_2D)((x, y) =>
                                ((Rey / 2.0) * (1 - y * y))
                                )).Convert_xy2X().Convert_X2Xt();
                        }
                    } else if (d == 1) {
                        return ((_2D)((x, y) => U2)).Convert_xy2X().Convert_X2Xt();
                    } else {
                        throw new ArgumentOutOfRangeException();
                    }
                case 3:
                    if (d == 0) {
                        if (Math.Abs(U2) >= 1.0e-10) {
                            return ((_3D)((x, y, z) =>
                                ((y + 1) / U2 - (2.0 * (1.0 - Math.Exp(U2 * (y + 1) * Rey))) / (U2 * (1 - Math.Exp(2.0 * U2 * Rey))))
                                )).Convert_xyz2X().Convert_X2Xt();
                        } else {
                            return ((_3D)((x, y, z) =>
                                ((Rey / 2.0) * (1 - y * y))
                                )).Convert_xyz2X().Convert_X2Xt();
                        }
                    } else if (d == 1) {
                        return ((_3D)((x, y, z) => U2)).Convert_xyz2X().Convert_X2Xt();
                    } else if (d == 2) {
                        return ((_3D)((x, y, z) => 0.0)).Convert_xyz2X().Convert_X2Xt();
                    } else {
                        throw new ArgumentOutOfRangeException();
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


       public double dt {
            get {
                return 1.0;
            }
        }

        
        public bool m_periodic = true;

        public GridCommons CreateGrid(int Resolution) {
            if (Resolution < 1)
                throw new ArgumentException();

            GridCommons grd;

            switch (m_spatialDimension) {
                case 2:
                    var yNodes = GenericBlas.Linspace(-1, 1, 15 * Resolution + 1);
                    var xNodes = GenericBlas.Linspace(-2, 6, 24 * Resolution + 1);
                    //var yNodes = GenericBlas.Linspace(-1, 1, 6);
                    //var xNodes = GenericBlas.Linspace(-2, 2, 4);
                    grd = Grid2D.Cartesian2DGrid(xNodes, yNodes, periodicX: m_periodic);
                    break;
                case 3:
                    grd = Grid3D.Cartesian3DGrid(GenericBlas.Linspace(-2, 6, 8 * Resolution + 1),
                        GenericBlas.Linspace(-1, 1, 5 * Resolution + 1), 
                        GenericBlas.Linspace(-1, 1, 5 * Resolution + 1), periodicX: m_periodic);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            if (m_periodic) {
                grd.EdgeTagNames.Add(1, "Velocity_Inlet_top");
                grd.EdgeTagNames.Add(2, "Velocity_Inlet_bottom");
                if (m_spatialDimension == 3) {
                    grd.EdgeTagNames.Add(3, "Velocity_Inlet_front");
                    grd.EdgeTagNames.Add(4, "Velocity_Inlet_back");
                }

                grd.DefineEdgeTags(delegate (double[] _X) {
                    var X = _X;
                    double x = X[0];
                    double y = X[1];

                    if (Math.Abs(y - (-1)) < 1.0e-6)
                        // bottom wall
                        return 2;
                    if (Math.Abs(y - (+1)) < 1.0e-6)
                        // top wall
                        return 1;

                    if (m_spatialDimension == 3) {
                        double z = X[2];
                        if (Math.Abs(z - (-1)) < 1.0e-6)
                            // back wall
                            return 4;
                        if (Math.Abs(z - (+1)) < 1.0e-6)
                            // front wall
                            return 3;
                    }

                    throw new ArgumentOutOfRangeException();
                    //return 1;
                });

                Console.WriteLine("TranspiratingChannelTest, periodic.");

            } else {

                grd.EdgeTagNames.Add(1, "Velocity_Inlet_top");
                grd.EdgeTagNames.Add(2, "Velocity_Inlet_bottom");
                grd.EdgeTagNames.Add(3, "Velocity_Inlet_left");
                grd.EdgeTagNames.Add(4, "Pressure_Outlet");
                if (m_spatialDimension == 3) {
                    grd.EdgeTagNames.Add(5, "Velocity_Inlet_front");
                    grd.EdgeTagNames.Add(6, "Velocity_Inlet_back");
                }

                grd.DefineEdgeTags(delegate (double[] _X) {
                    var X = _X;
                    double x = X[0];
                    double y = X[1];

                    if (Math.Abs(y - (-1)) < 1.0e-6)
                        // bottom wall
                        return 2;
                    if (Math.Abs(y - (+1)) < 1.0e-6)
                        // top wall
                        return 1;

                    if (Math.Abs(x - (-2)) < 1.0e-6)
                        return 3;
                    if (Math.Abs(x - (+6)) < 1.0e-6)
                        return 4;

                    if (m_spatialDimension == 3) {
                        double z = X[2];
                        if (Math.Abs(z - (-1)) < 1.0e-6)
                            // back wall
                            return 6;
                        if (Math.Abs(z - (+1)) < 1.0e-6)
                            // front wall
                            return 5;
                    }

                    throw new ArgumentOutOfRangeException();
                    //return 1;
                });

                Console.WriteLine("TranspiratingChannelTest, NON-periodic.");
            }

            return grd;
        }

        double U2 = 0.1;

        public IDictionary<string, AppControl.BoundaryValueCollection> GetBoundaryConfig() {
            var config = new Dictionary<string, AppControl.BoundaryValueCollection>();

            config.Add("Velocity_Inlet_top", new AppControl.BoundaryValueCollection());
            config["Velocity_Inlet_top"].Evaluators.Add(
                VariableNames.Velocity_d(1) + "#A",
                (X, t) => U2);
            config["Velocity_Inlet_top"].Evaluators.Add(
                VariableNames.Velocity_d(1) + "#B",
                (X, t) => U2);

            config.Add("Velocity_Inlet_bottom", new AppControl.BoundaryValueCollection());
            config["Velocity_Inlet_bottom"].Evaluators.Add(
                VariableNames.Velocity_d(1) + "#A",
                (X, t) => U2);
            config["Velocity_Inlet_bottom"].Evaluators.Add(
                VariableNames.Velocity_d(1) + "#B",
                (X, t) => U2);


            if (!m_periodic) {
                Func<double[], double, double> u1Inlet;
                if (Math.Abs(U2) >= 1.0e-10) {
                    u1Inlet = (X, t) => ((X[1] + 1) / U2 - (2.0 * (1.0 - Math.Exp(U2 * (X[1] + 1) * Rey))) / (U2 * (1 - Math.Exp(2.0 * U2 * Rey))));
                } else {
                    u1Inlet = (X, t) => (Rey * 0.5) * (1.0 - X[1] * X[1]);
                }

                config.Add("Velocity_Inlet_left", new AppControl.BoundaryValueCollection());
                config["Velocity_Inlet_left"].Evaluators.Add(
                    VariableNames.Velocity_d(0) + "#A",
                    u1Inlet);
                config["Velocity_Inlet_left"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#A",
                    (X, t) => U2);
                config["Velocity_Inlet_left"].Evaluators.Add(
                    VariableNames.Velocity_d(0) + "#B",
                    u1Inlet);
                config["Velocity_Inlet_left"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#B",
                    (X, t) => U2);

                config.Add("Pressure_Outlet", new AppControl.BoundaryValueCollection());
            }


            if (m_spatialDimension == 3) {
                config.Add("Velocity_Inlet_front", new AppControl.BoundaryValueCollection());
                config["Velocity_Inlet_front"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#A",
                    (X, t) => U2);
                config["Velocity_Inlet_front"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#B",
                    (X, t) => U2);

                config.Add("Velocity_Inlet_back", new AppControl.BoundaryValueCollection());
                config["Velocity_Inlet_back"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#A",
                    (X, t) => U2);
                config["Velocity_Inlet_back"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#B",
                    (X, t) => U2);
            }

            return config;
        }

         
        public Func<double[], double, double> GetPress(string species) {
            switch (m_spatialDimension) {
                case 2:
                    if (m_periodic) {
                        return ((_2D)((x, y) => 0)).Convert_xy2X().Convert_X2Xt();
                    } else {
                        return ((_2D)((x, y) => Math.Pow((-x + 6) * Rho, 1))).Convert_xy2X().Convert_X2Xt();
                    }
                case 3:
                    if (m_periodic) {
                        return ((_3D)((x, y, z) => 0)).Convert_xyz2X().Convert_X2Xt();
                    } else {
                        return ((_3D)((x, y, z) => Math.Pow((-x + 6) * Rho, 1))).Convert_xyz2X().Convert_X2Xt();
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        double Rho {
            get {
                if (rho_A != rho_B)
                    throw new NotSupportedException();
                if (mu_A != mu_B)
                    throw new NotSupportedException();

                return rho_A;
            }
        }

        double Rey {
            get {
                if (rho_A != rho_B)
                    throw new NotSupportedException();
                if (mu_A != mu_B)
                    throw new NotSupportedException();

                return rho_A / mu_A;
            }
        }

        /// <summary>
        /// specific weight, fluid A
        /// </summary>
        public double rho_A {
            get {
                return 5;
            }
        }

        /// <summary>
        /// specific weight, fluid B
        /// </summary>
        public double rho_B {
            get {
                return 5;
            }
        }

        /// <summary>
        /// dynamic viscosity, fluid A
        /// </summary>
        public double mu_A {
            get {
                return 0.1;
            }
        }

        /// <summary>
        /// dynamic viscosity, fluid B
        /// </summary>
        public double mu_B {
            get {
                return 0.1;
            }
        }

        /// <summary>
        /// no surface tension
        /// </summary>
        public double Sigma {
            get {
                return 0.0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public double[] AcceptableL2Error {
            get {
                return (m_spatialDimension == 2) ?
                    new double[] { 5.0e-2, 5.0e-2, 5.0e-1 } : new double[] { 5.0e-2, 5.0e-2, 5.0e-2, 5.0e-1 };
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public double[] AcceptableResidual {
            get {
                double[] Resi = (m_spatialDimension == 2) ? 
                    new double[] { 5e-8, 5e-8, 5e-8 } : new double[] { 5e-8, 5e-8, 5e-8, 5e-8 };

                if (m_periodic)
                    Resi.ScaleV(5);
                return Resi;
            }
        }

        /// <summary>
        /// 2
        /// </summary>
        public int SpatialDimension {
            get {
                return m_spatialDimension;
            }
        }

    }
}
