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

using BoSSS.Application.XNSE_Solver.Tests;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Solution.Control;
using BoSSS.Solution.NSECommon;
using BoSSS.Solution.Utils;
using ilPSP.Utils;
using System;
using System.Collections.Generic;

namespace BoSSS.Application.XNSEC {

    /// <summary>
    /// This tests the correctness of the strain jump condition:
    /// \f[
    ///   \left\llbracket \mu \left( \vec{u} + \vec{u}^T \right) \vec{n}_{\mathfrak{I}} \right\rrbracket = 0
    /// \f]
    /// Important for this testcase is,
    /// the individual terms of the jump condition are nonzero and only the sum of them cancels out, i.e.
    /// \f[
    ///   \left\llbracket \mu \vec{u} \cdot \vec{n}_{\mathfrak{I}} \right\rrbracket \neq 0
    ///   \quad \textrm{and} \quad
    ///   \left\llbracket \mu \vec{u}^T \cdot \vec{n}_{\mathfrak{I}} \right\rrbracket \neq 0,
    /// \f]
    /// which is not the case in many other test-cases.
    /// Atention: Test modified to be used with XNSEC! The \nabla\cdot \vec{u} term will be zero because
    /// constant density is asumed.
    /// </summary>
    internal class ViscosityJumpTest : IXNSECTest {
        private int m_SpatialDimension;

        public ViscosityJumpTest(int _SpatialDimension) {
            m_SpatialDimension = _SpatialDimension;
        }

        public int SpatialDimension {
            get {
                return m_SpatialDimension;
            }
        }

        /// <summary>
        /// 45 degree
        /// </summary>
        public Func<double[], double, double> GetPhi() {
            switch(m_SpatialDimension) {
                case 2:
                return ((_3D)((time, x, y) => (x + y) * 1 - 0)).Convert_txy2Xt();

                case 3:
                return ((_4D)((time, x, y, z) => x + y + z)).Convert_txyz2Xt();

                default:
                throw new ArgumentOutOfRangeException();
            }
        }

        public int LevelsetPolynomialDegree {
            get {
                return 1;
            }
        }

        public Func<double[], double, double> GetU(string species, int d) {
            switch(m_SpatialDimension) {
                case 2:
                if(d == 0) {
                    return ((_3D)((t, x, y) => y)).Convert_txy2Xt();
                } else if(d == 1) {
                    return ((_3D)((t, x, y) => -x)).Convert_txy2Xt();
                } else {
                    throw new ArgumentOutOfRangeException();
                }
                case 3:
                if(d == 0) {
                    return ((_4D)((t, x, y, z) => y)).Convert_txyz2Xt();
                } else if(d == 1) {
                    return ((_4D)((t, x, y, z) => -(x + z))).Convert_txyz2Xt();
                } else if(d == 2) {
                    return ((_4D)((t, x, y, z) => y)).Convert_txyz2Xt();
                } else {
                    throw new ArgumentOutOfRangeException();
                }
                default:
                throw new ArgumentOutOfRangeException();
            }
        }

        public Func<double[], double, double> GetMassFractions(string species, int q) {
            switch(m_SpatialDimension) {
                case 2:
                if(q == 0) {
                    return ((_3D)((t, x, y) => 1.0)).Convert_txy2Xt();
                } else {
                    throw new ArgumentOutOfRangeException();
                }                
                default:
                throw new ArgumentOutOfRangeException();
            }
        }


        

        public double dt {
            get {
                return 0.0;
            }
        }

        /// <summary>
        /// arranged so that the level-set passes through the corners
        /// </summary>
        public GridCommons CreateGrid(int Resolution) {
            if(Resolution < 1)
                throw new ArgumentException();

            GridCommons grd;
            switch(m_SpatialDimension) {
                case 2:
                grd = Grid2D.Cartesian2DGrid(GenericBlas.Linspace(-2, 2, 4 * Resolution + 1), GenericBlas.Linspace(-2, 2, 4 * Resolution + 1));
                //var grd = Grid2D.UnstructuredTriangleGrid(GenericBlas.Linspace(-2, 2, 6), GenericBlas.Linspace(-2, 2, 5));
                //var grd = Grid2D.Cartesian2DGrid(GenericBlas.Linspace(-2, 2, 3), GenericBlas.Linspace(-2, 2, 3));
                break;

                case 3:
                grd = Grid3D.Cartesian3DGrid(GenericBlas.Linspace(-2, 2, 4 * Resolution + 1),
                    GenericBlas.Linspace(-2, 2, 4 * Resolution + 1), GenericBlas.Linspace(-2, 2, 4 * Resolution + 1));
                break;

                default:
                throw new ArgumentOutOfRangeException();
            }

            grd.EdgeTagNames.Add(1, "Velocity_Inlet");
            grd.DefineEdgeTags(delegate (double[] _X) {
                return 1;
            });

            return grd;
        }

        public IDictionary<string, AppControl.BoundaryValueCollection> GetBoundaryConfig() {
            var config = new Dictionary<string, AppControl.BoundaryValueCollection>();

            config.Add("Velocity_Inlet", new AppControl.BoundaryValueCollection());
            switch(m_SpatialDimension) {
                case 2:
                config["Velocity_Inlet"].Evaluators.Add(
                    VariableNames.Velocity_d(0) + "#A",
                    (X, t) => X[1]);
                config["Velocity_Inlet"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#A",
                    (X, t) => -X[0]);
                config["Velocity_Inlet"].Evaluators.Add(
                      VariableNames.Temperature + "#A",
                      (X, t) => 1.0);
                config["Velocity_Inlet"].Evaluators.Add(
                       VariableNames.MassFraction0 + "#A",
                       (X, t) => 1.0);

                config["Velocity_Inlet"].Evaluators.Add(
                        VariableNames.Velocity_d(0) + "#B",
                        (X, t) => X[1]);
                config["Velocity_Inlet"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#B",
                    (X, t) => -X[0]);
                config["Velocity_Inlet"].Evaluators.Add(
                   VariableNames.Temperature + "#B",
                   (X, t) => 1.0);
                config["Velocity_Inlet"].Evaluators.Add(
                       VariableNames.MassFraction0 + "#B",
                       (X, t) => 1.0);
                break;

                case 3:
                config["Velocity_Inlet"].Evaluators.Add(
                    VariableNames.Velocity_d(0) + "#A",
                    (X, t) => X[1]);
                config["Velocity_Inlet"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#A",
                    (X, t) => -(X[0] + X[2]));
                config["Velocity_Inlet"].Evaluators.Add(
                    VariableNames.Velocity_d(2) + "#A",
                    (X, t) => X[1]);
                config["Velocity_Inlet"].Evaluators.Add(
                    VariableNames.Velocity_d(0) + "#B",
                    (X, t) => X[1]);
                config["Velocity_Inlet"].Evaluators.Add(
                    VariableNames.Velocity_d(1) + "#B",
                    (X, t) => -(X[0] + X[2]));
                config["Velocity_Inlet"].Evaluators.Add(
                    VariableNames.Velocity_d(2) + "#B",
                    (X, t) => X[1]);
                break;

                default:
                throw new ArgumentOutOfRangeException();
            }

            return config;
        }

        public Func<double[], double, double> GetPress(string species) {
            switch(m_SpatialDimension) {
                case 2:
                return ((_3D)((t, x, y) => 0)).Convert_txy2Xt();

                case 3:
                return ((_4D)((t, x, y, z) => 0)).Convert_txyz2Xt();

                default:
                throw new ArgumentOutOfRangeException();
            }
        }

        public Func<double[], double, double> GetTemperature(string species) {
            switch(m_SpatialDimension) {
                case 2:
                return ((_3D)((t, x, y) => 1.0)).Convert_txy2Xt();

                case 3:
                return ((_4D)((t, x, y, z) => 1.0)).Convert_txyz2Xt();

                default:
                throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// the density has no effect in this test (steady-state, no convection terms => density does not appear in the eq.
        /// and should have no effect on the matrix)
        /// </summary>
        public double rho_A {
            get {
                return 1.0;
            }
        }

        /// <summary>
        /// the density has no effect in this test (steady-state, no convection terms => density does not appear in the eq.
        /// and should have no effect on the matrix)
        /// </summary>
        public double rho_B {
            get {
                return 1.0;
            }
        }

        /// <summary>
        /// this test will work for all combinations of viscosities
        /// </summary>
        public double mu_A {
            get {
                return 1.0;
            }
        }

        /// <summary>
        /// this test will work for all combinations of viscosities
        /// </summary>
        public double mu_B {
            get {
                return 1.0*3;
            }
        }

        public Foundation.ScalarFunction GetS(double time) {
            switch(m_SpatialDimension) {
                case 2:
                return ((_2D)((x, y) => 0)).Vectorize();

                case 3:
                return ((_3D)((x, y, z) => 0)).Vectorize();

                default:
                throw new ArgumentOutOfRangeException();
            }
        }

        public Func<double[], double> GetF(string species, int d) {
            return (X => 0.0);
        }

        public Foundation.ScalarFunction GetSF(double time, int d) {
            switch(m_SpatialDimension) {
                case 2:
                return ((_2D)((x, y) => 0)).Vectorize();

                case 3:
                return ((_3D)((x, y, z) => 0)).Vectorize();

                default:
                throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// the surface tension force has no effect due to the flat interface
        /// </summary>
        public double Sigma {
            get {
                return 0.0;
            }
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

        public bool IncludeConvection {
            get {
                return false;
            }
        }

        public double[] AcceptableL2Error {
            get {
                switch(m_SpatialDimension) {
                    case 2:
                    return new double[] { 5.0e-9, 5.0e-9, 1.0e-7, 1.0e-7, 1.0e-7 };

                    case 3:
                    return new double[] { 5.0e-9, 5.0e-9, 5.0e-9, 1.0e-7, 1.0e-7, 1.0e-7 };

                    default:
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        public double[] AcceptableResidual {
            get {
                switch(m_SpatialDimension) {
                    case 2:
                    return new double[] { 1.0e-7, 1.0e-7, 1.0e-7, 1.0e-7, 1.0e-7 };

                    case 3:
                    return new double[] { 1.0e-7, 1.0e-7, 1.0e-7, 1.0e-7, 1.0e-7, 1.0e-7 };

                    default:
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        public bool TestImmersedBoundary => false;

        public int NumberOfChemicalComponents {
            get {
                return 1;
            }
        }

        public bool ChemicalReactionTermsActive => false;

        public double[] GravityDirection {
            get {
                return new double[] { 0.0, 0.0, 0.0 };
            }
        }

        /// <summary>
        /// nix
        /// </summary>
        public Func<double[], double, double> GetPhi2() {
            throw new NotImplementedException(); // will never be called, as long as 'TestImmersedBoundary' == false;
        }

        public Func<double[], double, double> GetPhi2U(int d) {
            throw new NotImplementedException();
        }

        public bool EnableMassFractions => false;

        public bool EnableTemperature => false;
    }
}