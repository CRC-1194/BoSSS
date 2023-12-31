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
using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.Control;
using BoSSS.Solution.AdvancedSolvers;
//using BoSSS.Solution.XNSECommon;
using BoSSS.Platform.LinAlg;

namespace BoSSS.Application.XdgTimesteppingTest {

    /// <summary>
    /// Interface treatment during time evolution
    /// </summary>
    public enum InterfaceMode {

        /// <summary>
        /// splitting between interface motion and time integration of XDG fields.
        /// </summary>
        Splitting = 0,

        /// <summary>
        /// Interface is moved concurrently with the timestep
        /// </summary>
        MovingInterface = 1
    }


    public enum Equation {
        ScalarTransport = 1,

        HeatEq = 2,

        Burgers = 3
    }

    /// <summary>
    /// Control object.
    /// </summary>
    [Serializable]
    public class XdgTimesteppingTestControl : AppControlSolver {

        /// <summary>
        /// ctor
        /// </summary>
        public XdgTimesteppingTestControl() {
            NoOfMultigridLevels = 1;
            base.CutCellQuadratureType = XQuadFactoryHelper.MomentFittingVariants.OneStepGaussAndStokes;
        }


        /// <summary>
        /// Utility function for easier user interaction, (should) set all reasonable <see cref="FieldOptions"/>
        /// </summary>
        public override void SetDGdegree(int degree) {
            FieldOptions.Add("Phi", new FieldOpts() {
                Degree = 2,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            FieldOptions.Add("u", new FieldOpts() {
                Degree = degree,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
            FieldOptions.Add("V*", new FieldOpts() {
                Degree = degree,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
        }

        
        /// <summary>
        /// Level-set velocity in normal direction.
        /// </summary>
        public Func<double[], double, double> S; 

        /// <summary>
        /// Level-set over time.
        /// </summary>
        public Func<double[], double, double> Phi;

        /// <summary>
        /// Exact solution, species A.
        /// </summary>
        public Func<double[], double, double> uA_Ex;

        /// <summary>
        /// Exact solution, species B.
        /// </summary>
        public Func<double[], double, double> uB_Ex;

        /// <summary>
        /// Exact solution, based on <see cref="Phi"/>, <see cref="uA_Ex"/> and <see cref="uB_Ex"/>.
        /// </summary>
        public double u_Ex(double[] X, double t) {
            double phi = Phi(X, t);
            if (phi >= 0) {
                return uB_Ex(X, t);
            } else {
                return uA_Ex(X, t);
            }
        }


        /// <summary>
        /// source term, species A.
        /// </summary>
        public Func<double[], double, double> rhsA;

        /// <summary>
        /// source term, species B.
        /// </summary>
        public Func<double[], double, double> rhsB;


        /// <summary>
        /// Switch between splitting and moving interface.
        /// </summary>
        public InterfaceMode InterfaceMode = InterfaceMode.MovingInterface;


        /// <summary>
        /// Type of shit which we want to shit. 
        /// </summary>
        public Equation Eq = Equation.ScalarTransport;

        /// <summary>
        /// In case of Heat equation, the diffusion coefficient for species A.
        /// </summary>
        public double muA;

        /// <summary>
        /// In case of Heat equation, the diffusion coefficient for species B.
        /// </summary>
        public double muB;

        /// <summary>
        /// In the case of Burgers equation, the Pseudo-1D - direction.
        /// </summary>
        public ilPSP.Vector BurgersDirection;

        
        /// <summary>
        /// If <see cref="HMF"/>==<see cref="XQuadFactoryHelper.MomentFittingVariants.ExactCircle"/>, this is the 
        /// radius in dependence of time.
        /// </summary>
        public Func<double, double> CircleRadius;


    }
}
