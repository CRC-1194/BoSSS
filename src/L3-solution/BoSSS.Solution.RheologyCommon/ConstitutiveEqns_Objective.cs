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
using BoSSS.Foundation;
using BoSSS.Solution.Utils;
using BoSSS.Solution.NSECommon;

namespace BoSSS.Solution.RheologyCommon
{
    /// <summary>
    /// Volume integral of objective part of constitutive equations.
    /// </summary>
    public class ConstitutiveEqns_Objective : IVolumeForm, IEquationComponent, IEquationComponentCoefficient
    {

        int Component;           // equation index (0: xx, 1: xy, 2: yy)
        BoundaryCondMap<IncompressibleBcType> m_BcMap;
        protected double m_Weissenberg; // relaxation factor lambda_1
        protected double m_ObjectiveParam;
        double m_StressPenalty;

        /// <summary>
        /// Initialize objective
        /// </summary>
        public ConstitutiveEqns_Objective(int Component, BoundaryCondMap<IncompressibleBcType> _BcMap, double Weissenberg, double ObjectiveParam, double Penalty)
        {
            this.Component = Component;
            this.m_BcMap = _BcMap;
            this.m_Weissenberg = Weissenberg;
            this.m_ObjectiveParam = ObjectiveParam;
            this.m_StressPenalty = Penalty;

        }

        /// <summary>
        /// Choosing the required terms for volume form (These Flags control, whether certain terms are evaluated during quadrature of the forms)
        /// </summary>
        public TermActivationFlags VolTerms {
            get { return TermActivationFlags.V | TermActivationFlags.UxV; }
        }

        /// <summary>
        /// Choosing the required terms for boundary edge form (These Flags control, whether certain terms are evaluated during quadrature of the forms)
        /// </summary>
        public TermActivationFlags BoundaryEdgeTerms {
            get { return TermActivationFlags.UxV; }
        }

        /// <summary>
        /// Choosing the required terms for inner edge form (These Flags control, whether certain terms are evaluated during quadrature of the forms)
        /// </summary>
        public TermActivationFlags InnerEdgeTerms {
            get { return TermActivationFlags.UxV; }
        }

        /// <summary>
        /// Ordering of the dependencies
        /// </summary>
        public IList<string> ArgumentOrdering {
            get {
                switch (Component)
                {
                    case 0:
                        return new string[] { VariableNames.StressXX, VariableNames.StressXY, VariableNames.StressXX, VariableNames.StressXY };
                    case 1:
                        return new string[] { VariableNames.StressXY, VariableNames.StressYY, VariableNames.StressXX, VariableNames.StressXY };
                    case 2:
                        return new string[] { VariableNames.StressXY, VariableNames.StressYY, VariableNames.StressXY, VariableNames.StressYY };
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Ordering of the parameters
        /// </summary>
        public IList<string> ParameterOrdering {
            get {
                switch (Component)
                {
                    case 0:
                        return new string[] { VariableNames.VelocityX_GradientX, VariableNames.VelocityX_GradientY, VariableNames.VelocityX_GradientX, VariableNames.VelocityX_GradientY };
                    case 1:
                        return new string[] { VariableNames.VelocityX_GradientX, VariableNames.VelocityX_GradientY, VariableNames.VelocityY_GradientX, VariableNames.VelocityY_GradientY };
                    case 2:
                        return new string[] { VariableNames.VelocityY_GradientX, VariableNames.VelocityY_GradientY, VariableNames.VelocityY_GradientX, VariableNames.VelocityY_GradientY };
                    default:
                        throw new NotImplementedException();
                }
            }
        }


        /// <summary>
        /// Calculating the integral of the volume part
        /// </summary>
        public double VolumeForm(ref CommonParamsVol cpv, double[] T, double[,] Grad_T, double V, double[] GradV)
        {
            double Grad1 = cpv.Parameters[0];
            double Grad2 = cpv.Parameters[1];
            double Grad3 = cpv.Parameters[2];
            double Grad4 = cpv.Parameters[3];

            double res = 0.0;
            res = ((Grad1 * T[0] + Grad2 * T[1]) + (Grad3 * T[2] + Grad4 * T[3]));

            return m_ObjectiveParam * -m_Weissenberg * res * V;
        }

        /// <summary>
        /// Calculating the integral of the inner edge part
        /// </summary>
        public double InnerEdgeForm(ref CommonParams inp, double[] Tin, double[] Tout, double[,] Grad_Tin, double[,] Grad_Tout,
            double Vin, double Vout, double[] Grad_Vin, double[] Grad_Vout)
        {

            double res = 0.0;
            res += (Tin[0] - Tout[0]) + (Tin[1] - Tout[1]) + (Tin[2] - Tout[2]) + (Tin[3] - Tout[3]);
            return -m_Weissenberg * res * (Vin - Vout);
        }

        /// <summary>
        /// Calculating the integral of the boundary edge part
        /// </summary>
        public double BoundaryEdgeForm(ref CommonParamsBnd inp, double[] Tin, double[,] Grad_Tin, double Vin, double[] Grad_Vin)
        {
            return 0.0;
        }

        /// <summary>
        /// update the coefficient such as the current Weissenberg number
        /// </summary>
        public void CoefficientUpdate(CoefficientSet cs, int[] DomainDGdeg, int TestDGdeg)
        {
            if (cs.UserDefinedValues.Keys.Contains("Weissenbergnumber"))
            {
                m_Weissenberg = (double)cs.UserDefinedValues["Weissenbergnumber"];
                //Console.WriteLine("Weissenbergnumber = {0}", m_Weissenberg);
            }
        }

    }
}
