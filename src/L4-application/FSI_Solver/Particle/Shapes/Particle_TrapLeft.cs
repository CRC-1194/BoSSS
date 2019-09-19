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
using BoSSS.Foundation;
using System.Linq;
using System.Collections.Generic;
using BoSSS.Foundation.Grid.RefElements;
using BoSSS.Foundation.Grid.Classic;
using System.Text;
using BoSSS.Platform;
using ilPSP.Tracing;
using System.Diagnostics;
using ilPSP.Utils;
using System.IO;
using System.Collections;
using BoSSS.Platform.Utils.Geom;

namespace BoSSS.Application.FSI_Solver {
    [DataContract]
    [Serializable]
    public class Particle_TrapLeft : Particle {
        /// <summary>
        /// Empty constructor used during de-serialization
        /// </summary>
        private Particle_TrapLeft() : base() {

        }

        public Particle_TrapLeft(ParticleMotionInit motionInit, double width, double[] startPos = null, double startAngl = 0, double activeStress = 0, double[] startTransVelocity = null, double startRotVelocity = 0) : base(motionInit, startPos, startAngl, activeStress, startTransVelocity, startRotVelocity) {
            width_P = width;
            Motion.GetParticleLengthscale(width);
            Motion.GetParticleArea(Area_P());
            Motion.GetParticleMomentOfInertia(MomentOfInertia_P);

        }

        /// <summary>
        /// Radius of the particle. Not necessary for particles defined by their length and thickness
        /// </summary>
        public double width_P;

        internal override int NoOfSubParticles => 2;

        public override double Area_P() {
            return (7 * width_P * width_P) / 8;
        }

        protected override double Circumference_P {
            get {
                return width_P * 5;
            }
        }

        override public double MomentOfInertia_P {
            get {
                return Math.Pow(width_P, 4) * 0.13785958;
            }
        }

        public override double LevelSetFunction(double[] X) {
            double alpha = -(Motion.angle[0]);
            double r;
            // Rechteck:
            //        r = Math.Max(X[0] - Motion.position[0][0]  - Width_P,  Motion.position[0][0] - Width_P - X[0]);
            //        r = Math.Max(r, X[1] - Motion.position[0][1] - Width_P);
            //        r = Math.Max(r,  Motion.position[0][1] - 0.5*Width_P - X[1]);

            // parallelogram
            //          r = Math.Abs(Motion.position[0][1] + 0.5 * width_P - X[1]);
            //          r = Math.Max(r, Math.Abs(-X[1] - 0.5 * X[0] + Motion.position[0][1] + width_P) + Math.Abs(X[1] - Motion.position[0][1]));
            //          r = Math.Max(r, Math.Abs(Motion.position[0][0] - 0.5 * width_P - X[0]));

            // Falle_Links:
            r = Math.Abs(Motion.position[0][1] - X[1]);
            r = Math.Max(r, Math.Abs(-X[1] + 0.5 * X[0] + Motion.position[0][1] - Motion.position[0][0] - width_P) - Math.Abs(X[1] - Motion.position[0][1]));
            r = Math.Max(r, Math.Abs(Motion.position[0][0] - X[0] - 0.5 * width_P));
            r = r - 4.5 * width_P;
            r = -r;
            return r;
        }

        public override bool Contains(double[] point, double h_min, double h_max = 0, bool WithoutTolerance = false) {
            // only for rectangular cells
            if (h_max == 0)
                h_max = h_min;
            double radiusTolerance = !WithoutTolerance ? 5 * width_P + Math.Sqrt(h_max.Pow2() + h_min.Pow2()) : 1;
            var distance = point.L2Distance(Motion.position[0]);
            if (distance < (radiusTolerance)) {
                return true;
            }
            return false;
        }

        public override bool ParticleInternalCell(double[] point, double h_min, double h_max = 0, bool WithoutTolerance = false) {
            // only for rectangular cells
            if (h_max == 0)
                h_max = h_min;
            double radiusTolerance = !WithoutTolerance ? 5 * width_P - Math.Sqrt(h_max.Pow2() + h_min.Pow2()) : 1;
            var distance = point.L2Distance(Motion.position[0]);
            if (distance < (radiusTolerance)) {
                return true;
            }
            return false;
        }

        override public MultidimensionalArray GetSurfacePoints(double hMin) {
            if (spatialDim != 2)
                throw new NotImplementedException("Only two dimensions are supported at the moment");

            int NoOfSurfacePoints = Convert.ToInt32(20 * Circumference_P / hMin) + 1;
            MultidimensionalArray SurfacePoints = MultidimensionalArray.Create(NoOfSubParticles, NoOfSurfacePoints, spatialDim);
            double[] InfinitisemalAngle = GenericBlas.Linspace(-Math.PI / 4, 5 * Math.PI / 4, NoOfSurfacePoints + 1);
            double[] InfinitisemalLength = GenericBlas.Linspace(0, width_P / 4, NoOfSurfacePoints + 1);
            if (Math.Abs(10 * Circumference_P / hMin + 1) >= int.MaxValue)
                throw new ArithmeticException("Error trying to calculate the number of surface points, overflow");

            for (int k = 0; k < NoOfSurfacePoints; k++) {
                SurfacePoints[0, k, 0] = Motion.position[0][0] - width_P / 2 + InfinitisemalLength[k];
                SurfacePoints[0, k, 1] = Motion.position[0][1] - width_P / 2 - 1.5 * SurfacePoints[0, k, 0] + width_P / 2;
            }

            for (int j = 0; j < NoOfSurfacePoints; j++) {
                SurfacePoints[0, j, 0] = Math.Sign(Math.Cos(InfinitisemalAngle[j])) * width_P * 7 + Motion.position[0][0] + 7 * width_P / 4;
                SurfacePoints[0, j, 1] = Math.Sign(Math.Sin(InfinitisemalAngle[j])) * width_P * 7 + Motion.position[0][1] + 7 * width_P / 2;
            }
            for (int j = 0; j < NoOfSurfacePoints; j++) {
                SurfacePoints[1, j, 0] = -Math.Sign(Math.Cos(InfinitisemalAngle[j])) * width_P * 2.5 + Motion.position[0][0];
                SurfacePoints[1, j, 1] = -Math.Sign(Math.Sin(InfinitisemalAngle[j])) * width_P * 2.5 + Motion.position[0][1];
            }
            return SurfacePoints;
        }

        override public double[] GetLengthScales() {
            return new double[] { width_P, width_P };
        }
    }
}
