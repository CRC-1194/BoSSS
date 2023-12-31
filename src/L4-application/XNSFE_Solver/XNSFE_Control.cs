﻿using BoSSS.Application.XNSE_Solver;
using BoSSS.Foundation;
using BoSSS.Solution.Control;
using BoSSS.Solution.NSECommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace BoSSS.Application.XNSFE_Solver {

    /// <summary>
    /// control object for <see cref="XNSFE"/>
    /// </summary>
    public class XNSFE_Control : XNSE_Control {


        public XNSFE_Control() : base() {
            this.NonLinearSolver.SolverCode = NonLinearSolverCode.Newton; // XNSFE should always use Newton solver
        }

        /// <summary>
        /// type for solver factory
        /// </summary>
        public override Type GetSolverType() {
            return typeof(XNSFE<XNSFE_Control>);
        }

        /// <summary>
        /// Time dependent heat source (either A or B (or in case of IBM) C).
        /// </summary>
        public ScalarFunctionTimeDep GetHeatSource(string species) {
            bool bfound = this.InitialValues_EvaluatorsVec.TryGetValue(VariableNames.HeatSource + "#" + species, out var ret);
            if (!bfound)
                this.InitialValues_EvaluatorsVec.TryGetValue(VariableNames.HeatSource, out ret);
            return ret;
        }

        /// <summary>
        /// Setting time dependent heat source (either A or B (or in case of IBM) C).
        /// </summary>
        public void SetHeatSource(string species, IBoundaryAndInitialData g) {
            this.InitialValues[VariableNames.HeatSource + "#" + species] = g;
        }

        /// <summary>
        /// Setting time dependent heat source (either A or B (or in case of IBM) C).
        /// </summary>
        public void SetHeatSource(string species, Func<double[], double, double> g) {
            this.InitialValues_Evaluators_TimeDep[VariableNames.HeatSource + "#" + species] = g;
        }

        /// <summary>
        /// include recoil pressure.
        /// </summary>
        [DataMember]
        public bool IncludeRecoilPressure = true;

        /// <summary>
        /// let temperature gradient vanish at contactline 
        /// => switch to Robin B.C. on interface, highly experimental,
        /// careful "slip" value is hardcoded 10/2021
        /// </summary>
        [DataMember]
        public bool MaterialAtContactLine = false;

        /// <summary>
        /// Set Field Options, i.e. the DG degrees
        /// </summary>
        public override void SetDGdegree(int Degree) {
            if (Degree < 1)
                throw new ArgumentOutOfRangeException("Degree must be 1 at minimum.");

            base.SetDGdegree(Degree);

            FieldOptions.Add("Temperature", new FieldOpts() {
                Degree = Degree,
                SaveToDB = FieldOpts.SaveToDBOpt.TRUE
            });
        }
    }
}
