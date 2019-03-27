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
using System.Runtime.Serialization;
using BoSSS.Foundation.XDG;
using ilPSP;
using BoSSS.Foundation.Grid;

namespace BoSSS.Application.FSI_Solver {
    [DataContract]
    [Serializable]
    public class Particle_Hippopede : Particle {
        /// <summary>
        /// Empty constructor used during de-serialization
        /// </summary>
        private Particle_Hippopede() : base() {

        }

        public Particle_Hippopede(int Dim, double[] startPos = null, double startAngl = 0) : 
            base(Dim, startPos, startAngl) //
        {
            
        }
        protected override double Circumference_P {
            get {
                return 2 * Math.PI * radius_P;
            }
        }
        protected override double Area_P {
            get {
                // not correct area
                return Math.PI * radius_P * radius_P;
            }
        }
        override public double MomentOfInertia_P {
            get {
                // not correct moment of inertia
                return (1 / 2.0) * (Mass_P * radius_P * radius_P);
            }
        }
        //override public void UpdateLevelSetFunction() {
        //    double a = 4.0 * radius_P.Pow2();
        //    double b = 1.0 * radius_P.Pow2();
        //    double alpha = -(Angle[0]);
        //    Phi_P = (X, t) => -((((X[0] - Position[0][0]) * Math.Cos(alpha) - (X[1] - Position[0][1]) * Math.Sin(alpha)).Pow(2) + ((X[0] - Position[0][0]) * Math.Sin(alpha) + (X[1] - Position[0][1]) * Math.Cos(alpha)).Pow(2)).Pow2() - a * ((X[0] - Position[0][0]) * Math.Cos(alpha) - (X[1] - Position[0][1]) * Math.Sin(alpha)).Pow2() - b * ((X[0] - Position[0][0]) * Math.Sin(alpha) + (X[1] - Position[0][1]) * Math.Cos(alpha)).Pow2());
        //}
        public override double Phi_P(double[] X) {
            double a = 4.0 * radius_P.Pow2();
            double b = 1.0 * radius_P.Pow2();
            double alpha = -(Angle[0]);
            return -((((X[0] - Position[0][0]) * Math.Cos(alpha) - (X[1] - Position[0][1]) * Math.Sin(alpha)).Pow(2) + ((X[0] - Position[0][0]) * Math.Sin(alpha) + (X[1] - Position[0][1]) * Math.Cos(alpha)).Pow(2)).Pow2() - a * ((X[0] - Position[0][0]) * Math.Cos(alpha) - (X[1] - Position[0][1]) * Math.Sin(alpha)).Pow2() - b * ((X[0] - Position[0][0]) * Math.Sin(alpha) + (X[1] - Position[0][1]) * Math.Cos(alpha)).Pow2());
        }
        override public CellMask CutCells_P(LevelSetTracker LsTrk) {
            // tolerance is very important
            var radiusTolerance = radius_P + LsTrk.GridDat.Cells.h_minGlobal;// +2.0*Math.Sqrt(2*LsTrk.GridDat.Cells.h_minGlobal.Pow2());

            CellMask cellCollection;
            CellMask cells = null;
            double alpha = -(Angle[0]);
            double a = 4.0 * radiusTolerance.Pow2();
            double b = 1.0 * radiusTolerance.Pow2();
            cells = CellMask.GetCellMask(LsTrk.GridDat, X => -((((X[0] - Position[0][0]) * Math.Cos(alpha) - (X[1] - Position[0][1]) * Math.Sin(alpha)).Pow(2) + ((X[0] - Position[0][0]) * Math.Sin(alpha) + (X[1] - Position[0][1]) * Math.Cos(alpha)).Pow(2)).Pow2() - a * ((X[0] - Position[0][0]) * Math.Cos(alpha) - (X[1] - Position[0][1]) * Math.Sin(alpha)).Pow2() - b * ((X[0] - Position[0][0]) * Math.Sin(alpha) + (X[1] - Position[0][1]) * Math.Cos(alpha)).Pow2()) > 0);

            CellMask allCutCells = LsTrk.Regions.GetCutCellMask();
            cellCollection = cells.Intersect(allCutCells);
            return cellCollection;
        }

        /// <summary>
        /// Radius of the particle. Not necessary for particles defined by their length and thickness
        /// </summary>
        [DataMember]
        public double radius_P;


        /// <summary>
        /// Length of an elliptic particle.
        /// </summary>
        [DataMember]
        public double length_P;

        /// <summary>
        /// Thickness of an elliptic particle.
        /// </summary>
        [DataMember]
        public double thickness_P;

        /// <summary>
        /// %
        /// </summary>
        protected override double AverageDistance {
            get {
                throw new NotImplementedException("todo");
            }
        }


        override public bool Contains(double[] point, LevelSetTracker LsTrk) {
            // only for squared cells
            double radiusTolerance = radius_P + 2.0 * Math.Sqrt(2 * LsTrk.GridDat.Cells.h_minGlobal.Pow2());
            double a = 4.0 * radiusTolerance.Pow2();
            double b = 1.0 * radiusTolerance.Pow2();
            if (-((((point[0] - Position[0][0]) * Math.Cos(Angle[0]) - (point[1] - Position[0][1]) * Math.Sin(Angle[0])).Pow(2) + ((point[0] - Position[0][0]) * Math.Sin(Angle[0]) + (point[1] - Position[0][1]) * Math.Cos(Angle[0])).Pow(2)).Pow2() - length_P * ((point[0] - Position[0][0]) * Math.Cos(Angle[0]) - (point[1] - Position[0][1]) * Math.Sin(Angle[0])).Pow2() - thickness_P * ((point[0] - Position[0][0]) * Math.Sin(Angle[0]) + (point[1] - Position[0][1]) * Math.Cos(Angle[0])).Pow2()) > 0) {
                return true;
            }
            return false;
        }

        override public double ComputeParticleRe(double mu_Fluid) {
            double particleReynolds = 0;
            particleReynolds = Math.Sqrt(TranslationalVelocity[0][0] * TranslationalVelocity[0][0] + TranslationalVelocity[0][1] * TranslationalVelocity[0][1]) * 2 * 4.0 * particleDensity / mu_Fluid;
            Console.WriteLine("Particle Reynolds number:  " + particleReynolds);
            return particleReynolds;
        }
    }
}
