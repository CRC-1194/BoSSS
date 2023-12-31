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

namespace BoSSS.Solution.CompressibleFlowCommon.MaterialProperty {

    /// <summary>
    /// For materials with a constant viscosity
    /// </summary>
    [System.Serializable]
    public class ConstantViscosity : IViscosityLaw {

        #region IViscosityLaw Members

        /// <summary>
        /// Returns the constant dimensionless viscosity 1.
        /// </summary>
        /// <param name="temperature">Ignored</param>
        /// <param name="cellIndex"></param>
        /// <returns>1</returns>
        public double GetViscosity(double temperature, int cellIndex) {
            return 1.0;
        }

        #endregion
    }
}
