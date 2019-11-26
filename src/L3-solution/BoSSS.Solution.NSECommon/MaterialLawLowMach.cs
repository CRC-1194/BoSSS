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
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using BoSSS.Foundation;
using ilPSP;

namespace BoSSS.Solution.NSECommon {

    /// <summary>
    /// Mode for material parameters,
    /// i.e. dynamic viscosity and heat conductivity.
    /// </summary>
    public enum MaterialParamsMode {

        /// <summary>
        /// Constant material parameters.
        /// </summary>
        Constant,

        /// <summary>
        /// Using Sutherland's law.
        /// </summary>
        Sutherland,

        /// <summary>
        /// Using Power-Law.
        /// </summary>
        PowerLaw
    }

    /// <summary>
    /// Material law for low Mach number flows.
    /// </summary>
    [DataContract]
    [Serializable]
    public class MaterialLawLowMach : MaterialLaw {

        double T_ref;
        MaterialParamsMode MatParamsMode;
        bool rhoOne;
        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="T_ref">Reference temperature - used in Sutherland's law.</param>
        /// <param name="MatParamsMode"></param>
        public MaterialLawLowMach(double T_ref, MaterialParamsMode MatParamsMode, bool rhoOne)
            : base() {
            this.rhoOne = rhoOne;
            this.T_ref = T_ref;
            this.MatParamsMode = MatParamsMode;
        }

        /// <summary>
        /// 
        /// </summary>
        public override IList<string> ParameterOrdering {
            get {
                return new string[] { VariableNames.Temperature0, VariableNames.Rho };
                //, VariableNames.MassFraction0_0, VariableNames.MassFraction1_0, VariableNames.MassFraction2_0, VariableNames.MassFraction3_0}; 
            }
        }

        /// <summary>
        /// true if the ThermodynamicPressure is already initialized
        /// </summary>
        protected bool IsInitialized {
            get {
                return ThermodynamicPressure != null;
            }
        }
       
        /// <summary>
        /// 
        /// </summary>
        [NonSerialized]
        protected ScalarFieldHistory<SinglePhaseField> ThermodynamicPressure;
        
        /// <summary>
        /// Hack to initalize ThermodynamicPressure - called by NSE_SIMPLE.VariableSet.Initialize()
        /// </summary>
        /// <param name="ThermodynamicPressure"></param>
        public void Initialize(ScalarFieldHistory<SinglePhaseField> ThermodynamicPressure) {
            if (!IsInitialized) {
                this.ThermodynamicPressure = ThermodynamicPressure;
            } else {
                throw new ApplicationException("Initialize() can be called only once.");
            }
        }

        /// <summary>
        /// Dimensionless ideal gas law - returns density as function of
        /// thermodynamic pressure (i.e. p0) and temperature.
        /// </summary>
        /// <param name="phi">Temperature</param>
        /// <returns>
        /// Density
        /// </returns>
        public override double GetDensity(params double[] phi) {
            if (IsInitialized) {
                double rho = this.ThermodynamicPressure.Current.GetMeanValue(0) / phi[0];
                if (rhoOne)  
                rho = 1.0;
                
                return rho;
            } else {
                throw new ApplicationException("ThermodynamicPressure is not initialized.");
            }
        }

        /// <summary>
        /// Dimensionless Sutherland's law.
        /// </summary>
        /// <param name="phi">Temperature</param>
        /// <returns>
        /// Dynamic viscosity
        /// </returns>
        public override double GetViscosity(double phi) {
            switch (this.MatParamsMode) {
                case MaterialParamsMode.Constant:
                    return 1.0;
                case MaterialParamsMode.Sutherland: {
                        double S = 110.5;
                        double viscosity = Math.Pow(phi, 1.5) * (1 + S / T_ref) / (phi + S / T_ref);
                        Debug.Assert(!double.IsNaN(viscosity));
                        Debug.Assert(!double.IsInfinity(viscosity));
                        return viscosity;
                    }
                case MaterialParamsMode.PowerLaw: {
                        double viscosity = Math.Pow(phi, 2.0 / 3.0);
                        return viscosity;
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        ///  The heat conductivity $\lambda. Possibly dependent on the variable <see cref="phi"/> representing the temperature
        /// </summary>
        /// <param name="phi"></param>
        /// <returns></returns>
        public double GetHeatConductivity(double phi) {
            switch (this.MatParamsMode) {
                case MaterialParamsMode.Constant:
                    return 1.0;
                case MaterialParamsMode.Sutherland: {
                        double S = 110.5;
                        double viscosity = Math.Pow(phi, 1.5) * (1 + S / T_ref) / (phi + S / T_ref);
                        Debug.Assert(!double.IsNaN(viscosity));
                        Debug.Assert(!double.IsInfinity(viscosity));
                        double lambda = viscosity; //// using viscosity = lambda for Pr = cte...
                        return lambda;
                    }
                case MaterialParamsMode.PowerLaw: {
                        double viscosity = Math.Pow(phi, 2.0 / 3.0);
                        double lambda = viscosity;
                        return lambda; // using viscosity = lambda for Pr = cte...
                    }
                default:
                    throw new NotImplementedException();
            }
        }
        /// <summary>
        /// The mass diffusivity. 
        /// </summary>
        /// <param name="phi"></param>
        /// <returns></returns>
        public double GetDiffusivity(double phi) {

            switch (this.MatParamsMode) {
                case MaterialParamsMode.Constant:
                    return 1.0;
                case MaterialParamsMode.Sutherland: {
                        double S = 110.5;
                        double viscosity = Math.Pow(phi, 1.5) * (1 + S / T_ref) / (phi + S / T_ref);
                        Debug.Assert(!double.IsNaN(viscosity));
                        Debug.Assert(!double.IsInfinity(viscosity));
                        double diff = viscosity; //// using viscosity = lambda for Sc = 1...
                        return diff; // Using a constant value! 
                    }
                case MaterialParamsMode.PowerLaw: {
                        throw new NotImplementedException();
                    }
                default:
                    throw new NotImplementedException();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="phi"></param>
        /// <returns></returns>
        public double GetPartialHeatCapacity(double phi) {
            switch (this.MatParamsMode) {
                case MaterialParamsMode.Constant:
                    return 1.0;
                case MaterialParamsMode.Sutherland: {
                        //    throw new NotImplementedException();
                        return 1.0; // Using a constant value! 
                    }
                case MaterialParamsMode.PowerLaw: {
                        throw new NotImplementedException();
                    }
                default:
                    throw new NotImplementedException();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="phi"></param>
        /// <returns></returns>
        public double GetHeatCapacity(double phi) {
            double cp = 1.0;
            return cp;
        }


        /// <summary>
        /// Returns the value of the heat capacity ratio Gamma (Cp/Cv)
        /// </summary>
        /// <param name="phi"></param>
        /// <returns></returns>
        public double GetHeatCapacityRatio(double phi) {
            double gamma = 1.4;            
            return gamma;
        }

        /// <summary>
        /// Returns thermodynamic pressure as function of inital mass and temperature.
        /// </summary>
        /// <param name="InitialMass"></param>
        /// <param name="Temperature"></param>
        /// <returns></returns>
        public override double GetMassDeterminedThermodynamicPressure(double InitialMass, SinglePhaseField Temperature) {

            SinglePhaseField omega = new SinglePhaseField(Temperature.Basis);
            omega.ProjectField(1.0,
                delegate (int j0, int Len, NodeSet NS, MultidimensionalArray result) {
                    int K = result.GetLength(1); // No nof Nodes
                    MultidimensionalArray temp = MultidimensionalArray.Create(Len, K);
                    Temperature.Evaluate(j0, Len, NS, temp);
                    for (int j = 0; j < Len; j++) {
                        for (int k = 0; k < K; k++) {                       
                            result[j, k] = 1 / temp[j, k];
                        }
                    }
                }, new Foundation.Quadrature.CellQuadratureScheme(true, null));
            return (InitialMass / omega.IntegralOver(null));

            //SinglePhaseField OneOverTemperature = new SinglePhaseField(Temperature.Basis);
            // OneOverTemperature.ProjectPow(1.0, Temperature, -1.0);
            //return (InitialMass / OneOverTemperature.IntegralOver(null));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="VelocityMean"></param>
        /// <param name="Normal"></param>
        /// <param name="ScalarMean"></param>
        /// <returns></returns>
        public override double GetLambda(double[] VelocityMean, double[] Normal, double ScalarMean) {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="phi"></param>
        /// <returns></returns>
        public override double DiffRho_Temp(double phi) {
            throw new NotImplementedException();
        }
    }
}
