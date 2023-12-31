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

using ilPSP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Linq;
using BoSSS.Solution.Utils;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace BoSSS.Solution.Control {

    /// <summary>
    /// This class encapsulates the representation of a mathematical formula (dependent on space and time)
    /// \f[
    ///   (\vec{x},t) \mapsto f(\vec{x},t)
    ///   \ \text{ resp. } \
    ///   (\vec{x}) \mapsto f(\vec{x})
    /// \f]
    /// which is used to provide boundary or initial values.
    /// The mathematical expression is compiled from C#-code on the fly.
    /// In contrast to delegates, this is representation of mathematical formulas is serializeable.
    /// </summary>
    [Serializable]
    [DataContract]
    public class Formula : IBoundaryAndInitialData {

        /// <summary>
        /// Empty ctor for de-serialization.
        /// </summary>
        private Formula() {
            //Console.WriteLine("ctor private");
        }


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="code">
        /// C#-code which represents the mathematical formula,
        ///  - if <paramref name="TimeDep"/> is true, a <see cref="Func{Space, Time, val}"/>, representing an expression \f$ (\vec{x},t) \mapsto f(\vec{x},t) \f$
        ///  - if <paramref name="TimeDep"/> is false, a <see cref="Func{Space, val}"/>, representing an expression \f$ (\vec{x}) \mapsto f(\vec{x}) \f$
        /// </param>
        /// <param name="AdditionalPrefixCode">
        /// Optional, additional C#-statements, e.g. auxiliary definitions, which is entered before <paramref name="code"/>.
        /// </param>
        /// <param name="TimeDep">
        /// Whether the function is time dependent or no, see <paramref name="TimeDep"/>.
        /// </param>
        public Formula(string code, bool TimeDep = false, string AdditionalPrefixCode = "") {
            m_Code = code;
            m_TimeDep = TimeDep;
            m_AdditionalPrefixCode = AdditionalPrefixCode;
            Compile();
        }

       
        [DataMember]
        bool m_TimeDep;

        [DataMember]
        string m_Code;

        [DataMember]
        string m_AdditionalPrefixCode;

        [NonSerialized]
        Func<double[], double, double> m_Xt_Del;

        [NonSerialized]
        Func<double[], double> m_X__Del;

        void Compile() {
            if (m_Xt_Del == null && m_X__Del == null) {
                /*
                using (var err = new StringWriter()) {
                    var Settings = new CompilerSettings();
#if DEBUG
                    Settings.Optimize = false;
#else
                    Settings.Optimize = false;
#endif
                    var Printer = new StreamReportPrinter(err);


                    CompilerContext cmpCont = new CompilerContext(Settings, Printer);


                    Evaluator eval = new Evaluator(cmpCont);
                    eval.InteractiveBaseClass = typeof(Object);

                    Assembly[] allAssis = BoSSS.Solution.Application.GetAllAssemblies().ToArray();

                    foreach (var assi in allAssis) {
                        eval.ReferenceAssembly(assi);
                    }

                    eval.Compile(@"
                                using System;
                                using System.Collections.Generic;
                                using System.Linq;
                                using ilPSP;
                                using ilPSP.Utils;
                                using BoSSS.Platform;
                                using BoSSS.Platform.Utils;
                                using BoSSS.Foundation;
                                using BoSSS.Foundation.Grid;
                                using BoSSS.Foundation.IO;
                                using BoSSS.Solution;
                                using BoSSS.Solution.Utils;
                      ");

                    if (!m_AdditionalPrefixCode.IsEmptyOrWhite()) {
                        try {
                            object figdi;
                            bool dummy;
                            eval.Evaluate(m_AdditionalPrefixCode, out figdi, out dummy);
                        } catch (Exception e) {
                            throw new AggregateException(e.GetType().Name + " during the interpretation of code snippet '"
                                + m_AdditionalPrefixCode + "'" + err.NewLine + "Error(s): " + err.NewLine + err.ToString(),
                                e);
                        }
                    }

                    object formula;
                    {
                        try {
                            string Prefix = m_TimeDep ? "Func<double[], double, double>" : "Func<double[], double>";
                            Prefix = Prefix + " myfunc = ";
                            object result;
                            bool result_set;
                            string ans = eval.Evaluate(Prefix + m_Code + ";", out result, out result_set);
                            formula = eval.Evaluate("myfunc;");
                        } catch (Exception e) {
                            throw new AggregateException(e.GetType().Name + " during the interpretation of code snippet '"
                                + m_Code + "'" + err.NewLine + "Error(s): " + err.NewLine + err.ToString(),
                                e);
                        }
                    } 

                    if (formula != null && cmpCont.Report.Errors == 0) {
                        if (formula is Func<double[], double, double>) {
                            m_Xt_Del = (Func<double[], double, double>)formula;
                            return;
                        }

                        if (formula is Func<double[], double>) {
                            m_X__Del = (Func<double[], double>)formula;
                            return;
                        }

                        throw new ArgumentException("Unable to cast result of code snippet '" + m_Code + " to a valid expression (Func<double[],double,double> or Func<double[],double>)." + err.NewLine + "Error(s): " + err.NewLine + err.ToString());
                    }
                }
                */

                string script;
                using(var scriptWrt = new StringWriter()) {
                    scriptWrt.WriteLine("using System;");
                    scriptWrt.WriteLine("using System.Collections.Generic;");
                    scriptWrt.WriteLine("using System.Linq;");
                    scriptWrt.WriteLine("using ilPSP;");
                    scriptWrt.WriteLine("using ilPSP.Utils;");
                    scriptWrt.WriteLine("using BoSSS.Platform;");
                    scriptWrt.WriteLine("using BoSSS.Platform.Utils;");
                    scriptWrt.WriteLine("using BoSSS.Foundation;");
                    scriptWrt.WriteLine("using BoSSS.Foundation.Grid;");
                    scriptWrt.WriteLine("using BoSSS.Foundation.IO;");
                    scriptWrt.WriteLine("using BoSSS.Solution;");
                    scriptWrt.WriteLine("using BoSSS.Solution.Utils;");
                    scriptWrt.WriteLine();

                    if (!m_AdditionalPrefixCode.IsEmptyOrWhite()) {
                        scriptWrt.WriteLine(m_AdditionalPrefixCode);
                        scriptWrt.WriteLine();
                    }

                    {
                        string Prefix = m_TimeDep ? "Func<double[], double, double>" : "Func<double[], double>";
                        Prefix = Prefix + " myfunc = ";
                        scriptWrt.WriteLine(Prefix + m_Code + ";");
                        scriptWrt.Write("myfunc");
                    }


                    script = scriptWrt.ToString();
                }

                Assembly[] allAssis = BoSSS.Solution.Application.GetAllAssemblies(typeof(Formula)).ToArray();
                //foreach(var a in allAssis) {
                //    Console.WriteLine("---" + a.FullName + "   " + (a.Location != null ? a.Location: "NULL"));
                //}
                var scriptOptions = ScriptOptions.Default;
                scriptOptions = scriptOptions.AddReferences(allAssis);

                var formula = CSharpScript.EvaluateAsync(script, scriptOptions).Result;


                if(formula != null) {
                    if(formula is Func<double[], double, double>) {
                        m_Xt_Del = (Func<double[], double, double>)formula;
                        return;
                    }

                    if(formula is Func<double[], double>) {
                        m_X__Del = (Func<double[], double>)formula;
                        return;
                    }

                    throw new ArgumentException("Unable to cast result of code snippet '" + m_Code + " to a valid expression (Func<double[],double,double> or Func<double[],double>).");
                } else {
                    throw new ArgumentException("Unable to cast result of code snippet '" + m_Code + " to a valid expression (Func<double[],double,double> or Func<double[],double>).");
                }

            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override string ToString() {
            return m_Code;
        }

        /// <summary>
        /// Evaluates the function.
        /// </summary>
        /// <param name="X">global/physical coordinates.</param>
        /// <param name="t">Physical time.</param>
        /// <returns>Function value.</returns>
        public double Evaluate(double[] X, double t) {
            Compile();
            Debug.Assert((m_Xt_Del == null) != (m_X__Del == null));

            if (m_Xt_Del != null) {
                return m_Xt_Del(X, t);
            } else if (m_X__Del != null) {
                return m_X__Del(X);
            } else {
                throw new ApplicationException();
            }
        }
        
        /// <summary>
        /// %
        /// </summary>
        public override bool Equals(object obj) {
            Formula f = obj as Formula;
            if (f == null)
                return false;

            if (this.m_TimeDep != f.m_TimeDep)
                return false;
            if (!m_Code.Equals(f.m_Code))
                return false;
            if (!this.m_AdditionalPrefixCode.Equals(f.m_AdditionalPrefixCode))
                return false;

            return true;
        }

        /// <summary>
        /// %
        /// </summary>
        public override int GetHashCode() {
            return m_Code.GetHashCode();
        }

        /// <summary>
        /// Vectorized evaluation
        /// </summary>
        public void EvaluateV(MultidimensionalArray input, double time, MultidimensionalArray output) {
            NonVectorizedScalarFunction.Vectorize(this.Evaluate, time)(input, output);
        }
    }
}
