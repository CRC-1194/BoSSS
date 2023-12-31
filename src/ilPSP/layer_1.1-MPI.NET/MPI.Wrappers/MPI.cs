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


namespace MPI.Wrappers {

    /// <summary>
    /// Static access to <see cref="FortranMPIdriver"/>
    /// </summary>
    public static class csMPI {

        /// <summary>
        /// Static instance of the Fortran MPI Driver
        /// </summary>
        static IMPIdriver m_Raw1 = new FortranMPIdriver();

        /// <summary>
        /// Access to raw/unsafe MPI commands
        /// </summary>
        public static IMPIdriver Raw {
            get {
                return m_Raw1;
            }
        }
    }
}
