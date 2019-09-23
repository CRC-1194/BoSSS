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
using ilPSP;
using ilPSP.Utils;

namespace BoSSS.Application.FSI_Solver {
    [DataContract]
    [Serializable]
    public class Particle_Sphere : Particle {
        /// <summary>
        /// Empty constructor used during de-serialization
        /// </summary>
        private Particle_Sphere() : base() {

        }

        public Particle_Sphere(ParticleMotionInit motionInit, double radius, double[] startPos = null, double startAngl = 0, double activeStress = 0, double[] startTransVelocity = null, double startRotVelocity = 0) : base(motionInit, startPos, startAngl, activeStress, startTransVelocity, startRotVelocity) {
            radius_P = radius;
            Motion.GetParticleLengthscale(radius);
            Motion.GetParticleArea(Area_P());
            Motion.GetParticleMomentOfInertia(MomentOfInertia_P);

        }

        /// <summary>
        /// Radius of the particle. Not necessary for particles defined by their length and thickness
        /// </summary>
        private readonly double radius_P;

        public override double Area_P() {
            return Math.PI * radius_P.Pow2();
        }
        protected override double Circumference_P {
            get {
                return 2 * Math.PI * radius_P;
            }
        }
        override public double MomentOfInertia_P {
            get {
                return (1 / 2.0) * (Mass_P * radius_P.Pow2());
            }
        }

        public override double LevelSetFunction(double[] X) {
            double x0 = Motion.Position[0][0];
            double y0 = Motion.Position[0][1];
            return -(X[0] - x0).Pow2() + -(X[1] - y0).Pow2() + radius_P.Pow2();
        }

        public override bool Contains(double[] point, double h_min, double h_max = 0, bool WithoutTolerance = false) {
            // only for rectangular cells
            if (h_max == 0)
                h_max = h_min;
            double radiusTolerance = !WithoutTolerance ? radius_P + Math.Sqrt(h_max.Pow2() + h_min.Pow2()) : radius_P;
            var distance = point.L2Distance(Motion.Position[0]);
            if (distance < (radiusTolerance)) {
                return true;
            }
            return false;
        }

        override public MultidimensionalArray GetSurfacePoints(double hMin) {
            if (spatialDim != 2)
                throw new NotImplementedException("Only two dimensions are supported at the moment");

            int NoOfSurfacePoints = Convert.ToInt32(10 * Circumference_P / hMin) + 1;
            MultidimensionalArray SurfacePoints = MultidimensionalArray.Create(NoOfSubParticles, NoOfSurfacePoints, spatialDim);
            double[] InfinitisemalAngle = GenericBlas.Linspace(0, 2 * Math.PI, NoOfSurfacePoints + 1);
            if (Math.Abs(10 * Circumference_P / hMin + 1) >= int.MaxValue)
                throw new ArithmeticException("Error trying to calculate the number of surface points, overflow");

            for (int j = 0; j < NoOfSurfacePoints; j++) {
                SurfacePoints[0, j, 0] = Math.Cos(InfinitisemalAngle[j]) * radius_P + Motion.Position[0][0];
                SurfacePoints[0, j, 1] = Math.Sin(InfinitisemalAngle[j]) * radius_P + Motion.Position[0][1];
            }
            return SurfacePoints;
        }

        override public void GetSupportPoint(int SpatialDim, double[] Vector, out double[] SupportPoint) {
            double length = Math.Sqrt(Vector[0].Pow2() + Vector[1].Pow2());
            double CosT = Vector[0] / length;
            double SinT = Vector[1] / length;
            SupportPoint = new double[SpatialDim];
            if (SpatialDim != 2)
                throw new NotImplementedException("Only two dimensions are supported at the moment");
            SupportPoint[0] = CosT * radius_P + Motion.Position[0][0];
            SupportPoint[1] = SinT * radius_P + Motion.Position[0][1];
            if (double.IsNaN(SupportPoint[0]) || double.IsNaN(SupportPoint[1]))
                throw new ArithmeticException("Error trying to calculate point0 Value:  " + SupportPoint[0] + " point1 " + SupportPoint[1]);
        }

        override public double[] GetLengthScales() {
            return new double[] { radius_P, radius_P };
        }
    }
}

