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
using BoSSS.Foundation.XDG;
using ilPSP.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BoSSS.Solution.NSECommon {

    /// <summary>
    /// Reaction species source in mass transport equation
    /// </summary>
    public class ReactionSpeciesSourceLinearized : BoSSS.Solution.Utils.LinearSource, IEquationComponentCoefficient {
        private string[] m_ArgumentOrdering;

        private double[] StoichiometricCoefficients;
        private double[] ReactionRateConstants;
        private int SpeciesIndex; //Species index, not to be confused with alpha = SpeciesIndex + 1
        private int NumberOfReactants;
        private string[] MassFractionNames;
        private double OneOverMolarMass0MolarMass1;
        private double[] MolarMasses;
        private double rho;
        private MaterialLaw EoS;
        private double m_Da;

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="ReactionRateConstants">constants[0]=PreExpFactor, constants[1]=ActivationTemperature, constants[2]=MassFraction0Exponent, constants[3]=MassFraction1Exponent</param>
        /// <param name="StoichiometricCoefficients"></param>
        /// <param name="OneOverMolarMass0MolarMass1"> 1/(M_infty^(a + b -1) * MolarMassFuel^a * MolarMassOxidizer^b). M_infty is the reference for the molar mass steming from non-dimensionalisation of the governing equations.</param>
        /// <param name="MolarMasses">Array of molar masses. 0 Fuel. 1 Oxidizer, 2 to ns products.</param>
        /// <param name="EoS">MaterialLawCombustion</param>
        /// <param name="NumberOfReactants">The number of reactants (i.e. ns)</param>
        /// <param name="SpeciesIndex">Index of the species being balanced. (I.e. 0 for fuel, 1 for oxidizer, 2 for CO2, 3 for water)</param>
        public ReactionSpeciesSourceLinearized(double[] ReactionRateConstants, double[] StoichiometricCoefficients, double OneOverMolarMass0MolarMass1, double[] MolarMasses, MaterialLaw EoS, int NumberOfReactants, int SpeciesIndex) {
            MassFractionNames = new string[] { VariableNames.MassFraction0, VariableNames.MassFraction1, VariableNames.MassFraction2, VariableNames.MassFraction3 };
            m_ArgumentOrdering = new string[] { MassFractionNames[SpeciesIndex] };
            this.StoichiometricCoefficients = StoichiometricCoefficients;
            this.ReactionRateConstants = ReactionRateConstants;
            this.SpeciesIndex = SpeciesIndex;
            this.NumberOfReactants = NumberOfReactants;
            this.OneOverMolarMass0MolarMass1 = OneOverMolarMass0MolarMass1;
            this.MolarMasses = MolarMasses;
            this.EoS = EoS;
            this.m_Da = ReactionRateConstants[0];
        }

        /// <summary>
        /// The current argument, if there is one (i.e. when MassFraction0 or MassFraction1 are being balanced)
        /// </summary>
        public override IList<string> ArgumentOrdering {
            get { return m_ArgumentOrdering; }
        }

        /// <summary>
        /// Temperature, MassFraction0, MassFraction1, MassFraction 2, MassFraction 3 at the linearization point.
        /// </summary>
        public override IList<string> ParameterOrdering {
            get { return new string[] { VariableNames.Temperature0, VariableNames.MassFraction0_0, VariableNames.MassFraction1_0, VariableNames.MassFraction2_0, VariableNames.MassFraction3_0 }; }
        }

        /// <summary>
        /// Da number used within the homotopie algorithm
        /// </summary>
        /// <param name="cs"></param>
        /// <param name="DomainDGdeg"></param>
        /// <param name="TestDGdeg"></param>
        public void CoefficientUpdate(CoefficientSet cs, int[] DomainDGdeg, int TestDGdeg) {
            if (cs.UserDefinedValues.Keys.Contains("Damkoehler"))
                m_Da = (double)cs.UserDefinedValues["Damkoehler"];
        }

        protected override double Source(double[] x, double[] parameters, double[] U) {
            double ReactionRate = 0.0;
            double ExponentialPart = m_Da * Math.Exp(-ReactionRateConstants[1] / parameters[0]); // Da*exp(-Ta/T)
            rho = EoS.GetDensity(parameters);
            // 0. MassFraction (fuel) balance species source
            if (SpeciesIndex == 0) {
                // M_alpha/(M_1^a * M_2^b) * Da*exp(-Ta/T)*(rho*Y_f)_(k+1)*(rho*Y_f)_(k)^(a-1)*((rho*Y_o)_(k)^b

                ReactionRate = ExponentialPart * OneOverMolarMass0MolarMass1 * rho * U[0] * Math.Pow(rho * parameters[1], ReactionRateConstants[2] - 1) * Math.Pow(rho * parameters[2], ReactionRateConstants[3]);
                Debug.Assert(!double.IsNaN(ReactionRate));
                Debug.Assert(!double.IsInfinity(ReactionRate));
            }
            // 1. MassFraction (oxididizer) balance species source
            else if (SpeciesIndex == 1) {
                // M_alpha/(M_1^a * M_2^b) * Da*exp(-Ta/T)*(rho*Y_f)_(k)^(a)*(rho*Y_o)_(k+1)*(rho*Y_o)_(k)^(b-1)
                ReactionRate = ExponentialPart * OneOverMolarMass0MolarMass1 * Math.Pow(rho * parameters[1], ReactionRateConstants[2]) * rho * U[0] * Math.Pow(rho * parameters[2], ReactionRateConstants[3] - 1);
                Debug.Assert(!double.IsNaN(ReactionRate));
                Debug.Assert(!double.IsInfinity(ReactionRate));
            }
            // product balance species source
            else if (SpeciesIndex > 1 && SpeciesIndex < NumberOfReactants) {
                // M_alpha/(M_1^a * M_2^b) * Da*exp(-Ta/T)*(rho*Y_f)_(k)^a*(rho*Y_o)_(k)^b
                ReactionRate = ExponentialPart * OneOverMolarMass0MolarMass1 * Math.Pow(rho * parameters[1], ReactionRateConstants[2]) * Math.Pow(rho * parameters[2], ReactionRateConstants[3]);
                Debug.Assert(!double.IsNaN(ReactionRate));
                Debug.Assert(!double.IsInfinity(ReactionRate));
            } else
                throw new System.ArgumentException("Species index cannot be negative or greater than the number of reactants");
            return -MolarMasses[SpeciesIndex] * StoichiometricCoefficients[SpeciesIndex] * ReactionRate;
        }
    }

    /// <summary>
    /// Reaction species source in mass transport equation
    /// </summary>
    public class ReactionSpeciesSourceJacobi : IVolumeForm, IEquationComponentCoefficient, ISupportsJacobianComponent {
        private string[] m_ArgumentOrdering;

        private double[] StoichiometricCoefficients;
        private double[] ReactionRateConstants;
        private int SpeciesIndex; //Species index, not to be confused with alpha = SpeciesIndex + 1
        private double[] MolarMasses;
        private double rho;
        private MaterialLaw_MultipleSpecies EoS;
        private double m_Da;
        private double TRef;
        private double cpRef;
        private bool VariableOneStepParameters;
        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="ReactionRateConstants">constants[0]=PreExpFactor, constants[1]=ActivationTemperature, constants[2]=MassFraction0Exponent, constants[3]=MassFraction1Exponent</param>
        /// <param name="StoichiometricCoefficients"></param>

        /// <param name="MolarMasses">Array of molar masses. 0 Fuel. 1 Oxidizer, 2 to ns products.</param>
        /// <param name="EoS">MaterialLawCombustion</param>
        /// <param name="NumberOfReactants">The number of reactants (i.e. ns)</param>
        /// <param name="SpeciesIndex">Index of the species being balanced. (I.e. 0 for fuel, 1 for oxidizer, 2 for CO2, 3 for water)</param>
        public ReactionSpeciesSourceJacobi(double[] ReactionRateConstants, double[] StoichiometricCoefficients, double[] MolarMasses, MaterialLaw_MultipleSpecies EoS, int NumberOfReactants, int SpeciesIndex, double TRef, double cpRef, bool VariableOneStepParameters) {
            m_ArgumentOrdering = ArrayTools.Cat(new string[] { VariableNames.Temperature }, VariableNames.MassFractions(NumberOfReactants));// Y4 is not a variable!!!!;
            this.StoichiometricCoefficients = StoichiometricCoefficients;
            this.ReactionRateConstants = ReactionRateConstants;
            this.SpeciesIndex = SpeciesIndex;
            this.MolarMasses = MolarMasses;
            this.EoS = EoS;
            this.m_Da = ReactionRateConstants[0];
            this.TRef = TRef;
            this.cpRef = cpRef;
            this.VariableOneStepParameters = VariableOneStepParameters;
            if (VariableOneStepParameters == true)
                Console.WriteLine("Using variable one step parameters!");
        }

        /// <summary>
        /// The current argument, if there is one (i.e. when MassFraction0 or MassFraction1 are being balanced)
        /// </summary>
        public virtual IList<string> ArgumentOrdering {
            get { return m_ArgumentOrdering; }
        }

        /// <summary>
        ///
        /// </summary>
        public virtual IList<string> ParameterOrdering {
            get {
                return new List<string> { }; // no parameters
            }
        }

        public virtual TermActivationFlags VolTerms {
            get {
                return TermActivationFlags.UxV | TermActivationFlags.V;
            }
        }

        /// <summary>
        /// Da number used within the homotopie algorithm
        /// </summary>
        /// <param name="cs"></param>
        /// <param name="DomainDGdeg"></param>
        /// <param name="TestDGdeg"></param>
        public void CoefficientUpdate(CoefficientSet cs, int[] DomainDGdeg, int TestDGdeg) {
            if (cs.UserDefinedValues.Keys.Contains("Damkoehler"))
                m_Da = (double)cs.UserDefinedValues["Damkoehler"];
        }

        public IEquationComponent[] GetJacobianComponents(int SpatialDimension) {
            var SourceDerivVol = new VolumeFormDifferentiator(this, SpatialDimension);
            return new IEquationComponent[] { SourceDerivVol };
        }

        public double VolumeForm(ref CommonParamsVol cpv, double[] U, double[,] GradU, double V, double[] GradV) {
            return this.Source(cpv.Xglobal, cpv.Parameters, U) * V;
        }

        protected double Source(double[] x, double[] parameters, double[] U) {
            double Temperature = U[0];
            double YF = U[1];
            double YO = U[2];
            double Ta = ReactionRateConstants[1];
            double MM_F = MolarMasses[0];
            double MM_O = MolarMasses[1];


            //////===================================================
            ////// Limit value of variables using known bounds
            //////====================================================

            Temperature = Temperature > 10 ? 10 : Temperature;
            Temperature = Temperature < 0.7 ? 0.7 : Temperature;

            YF = YF > 1.0 ? 1.0 : YF;
            YF = YF < 0.0 ? 0.0 : YF;

            YO = YO > 1.0 ? 1.0 : YO;
            YO = YO < 0.0 ? 0.0 : YO;


            if (YF * YO > 1e-8 && VariableOneStepParameters) {//  calculate one-Step model parameters
                Ta = EoS.m_ChemModel.getTa(YF, YO)/TRef;
            }

            rho = EoS.GetDensity(U);

 

            double ReactionRate = m_Da * Math.Exp(-Ta / Temperature) * (rho * YF / MM_F) * (rho * YO / MM_O);

            if (double.IsInfinity(ReactionRate)) {
                Console.WriteLine("Infinite found");
                Console.WriteLine("Temperature: {0}", Temperature);
                Console.WriteLine("rho:{0}", rho);
                Console.WriteLine("ExponentialTerm:{0}", Math.Exp(-Ta / Temperature));
            }

            if (double.IsNaN(ReactionRate)) {
                Console.WriteLine("Nan found");
                Console.WriteLine("Temperature:{0}", Temperature);
                Console.WriteLine("rho:{0}", rho);
                Console.WriteLine("ExponentialTerm:{0}", Math.Exp(-Ta / Temperature));
            }

            return -MolarMasses[SpeciesIndex] * StoichiometricCoefficients[SpeciesIndex] * ReactionRate;
        }
    }
}