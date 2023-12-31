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
using System.Runtime.CompilerServices;
using BoSSS.Solution.Utils;
using BoSSS.Solution.CompressibleFlowCommon.Boundary;
using BoSSS.Solution.CompressibleFlowCommon.MaterialProperty;
using BoSSS.Foundation.XDG;

namespace BoSSS.Solution.CompressibleFlowCommon.Convection {

    /// <summary>
    /// Abstract class for all numerical flux function applicable to the Euler
    /// equation system.
    /// </summary>
    public abstract class EulerFlux : NonlinearFlux, IEquationComponentSpeciesNotification {

        /// <summary>
        /// Configuration options
        /// </summary>
        protected CompressibleControl config;

        /// <summary>
        /// Boundary value definition
        /// </summary>
        protected IBoundaryConditionMap boundaryMap;

        /// <summary>
        /// The material being used
        /// </summary>
        protected Material material;

        /// <summary>
        /// Concerned component of the Euler equations
        /// </summary>
        protected IEulerEquationComponent equationComponent;

        private string speciesName;

        /// <summary>
        /// Constructs a new Euler flux
        /// </summary>
        /// <param name="config">Configuration options</param>
        /// <param name="boundaryMap">Boundary value definition</param>
        /// <param name="equationComponent">
        /// Concerned component of the Euler equations
        /// </param>
        /// <param name="material">
        /// Mapping that determines the active species in some point.
        /// </param>
        protected EulerFlux(CompressibleControl config, IBoundaryConditionMap boundaryMap, IEulerEquationComponent equationComponent, Material material) {
            this.config = config;
            this.boundaryMap = boundaryMap;
            this.equationComponent = equationComponent;
            this.material = material;
        }

        /// <summary>
        /// <see cref="CompressibleEnvironment.PrimalArgumentOrdering"/>
        /// </summary>
        public override IList<string> ArgumentOrdering {
            get {
                return CompressibleEnvironment.PrimalArgumentOrdering;
            }
        }

        public void SetParameter(string speciesName) {
            this.speciesName = speciesName;
        }

        /// <summary>
        /// Converts <paramref name="Uin"/> and <paramref name="Uout"/> into
        /// instances of <see cref="StateVector"/> and calls
        /// <see cref="InnerEdgeFlux(double[], double, StateVector, StateVector, ref Vector, int)"/>
        /// </summary>
        /// <param name="time">
        /// <see cref="NonlinearFlux.InnerEdgeFlux(double, double[], double[],double[], double[], int)"/>
        /// </param>
        /// <param name="x">
        /// <see cref="NonlinearFlux.InnerEdgeFlux(double, double[], double[],double[], double[], int)"/>
        /// </param>
        /// <param name="normal">
        /// <see cref="NonlinearFlux.InnerEdgeFlux(double, double[], double[],double[], double[], int)"/>
        /// </param>
        /// <param name="Uin">
        /// <see cref="NonlinearFlux.InnerEdgeFlux(double, double[], double[],double[], double[], int)"/>
        /// </param>
        /// <param name="Uout">
        /// <see cref="NonlinearFlux.InnerEdgeFlux(double, double[], double[],double[], double[], int)"/>
        /// </param>
        /// <param name="jEdge">
        /// <see cref="NonlinearFlux.InnerEdgeFlux(double, double[], double[],double[], double[], int)"/>
        /// </param>
        /// <returns>
        /// <see cref="InnerEdgeFlux(double[], double, StateVector, StateVector, ref Vector, int)"/>
        /// </returns>
        protected override double InnerEdgeFlux(double time, double[] x, double[] normal, double[] Uin, double[] Uout, int jEdge) {
            StateVector stateIn = new StateVector(Uin, material);
            StateVector stateOut = new StateVector(Uout, material);

            ilPSP.Vector Normal = new ilPSP.Vector(stateIn.Dimension);
            for (int i = 0; i < normal.Length; i++) {
                Normal[i] = normal[i];
            }

            double flux = InnerEdgeFlux(x, time, stateIn, stateOut, ref Normal, jEdge);
            Debug.Assert(!double.IsNaN(flux));
            return flux;
        }

        /// <summary>
        /// Actual calculation of flux across the edge.
        /// </summary>
        /// <param name="x">Evaluation point</param>
        /// <param name="time">Point in time</param>
        /// <param name="stateIn">The state inside the cell</param>
        /// <param name="stateOut">The state in the neighboring cell</param>
        /// <param name="normal">The unit normal vector</param>
        /// <param name="edgeIndex">
        /// Processor-local index of the current edge
        /// </param>
        /// <returns>See Toro2009 (p. 332)</returns>
        public abstract double InnerEdgeFlux(double[] x, double time, StateVector stateIn, StateVector stateOut, ref ilPSP.Vector normal, int edgeIndex);

        /// <summary>
        /// Weakly imposes the specific boundary condition for this boundary
        /// type (defined by the edge tag) by calculating the outer value via a
        /// subclass of <see cref="BoundaryCondition"/> and
        /// inserting it into
        /// <see cref="InnerEdgeFlux(double, double[], double[], double[], double[], int)"/>
        /// </summary>
        protected override double BorderEdgeFlux(double time, double[] x, double[] normal, byte EdgeTag, double[] Uin, int jEdge) {
            StateVector stateIn = new StateVector(Uin, material);

            ilPSP.Vector Normal = new ilPSP.Vector(stateIn.Dimension);
            for (int i = 0; i < normal.Length; i++) {
                Normal[i] = normal[i];
            }

            // Get boundary condition on this edge
            BoundaryCondition boundaryCondition;
            if (this.boundaryMap is XDGCompressibleBoundaryCondMap xdgBoudaryMap) {
                boundaryCondition = xdgBoudaryMap.GetBoundaryConditionForSpecies(EdgeTag, this.speciesName);
            } else if (this.boundaryMap is CompressibleBoundaryCondMap boundaryMap) {
                boundaryCondition = boundaryMap.GetBoundaryCondition(EdgeTag);
            } else {
                throw new NotSupportedException("This type of boundary condition map is not supported.");
            }

            //StateVector stateBoundary = boundaryMap.GetBoundaryState(
            //    EdgeTag, time, x, normal, stateIn);

            StateVector stateBoundary = boundaryCondition.GetBoundaryState(time, x, normal, stateIn);

            double flux = InnerEdgeFlux(x, time, stateIn, stateBoundary, ref Normal, jEdge);
            Debug.Assert(!double.IsNaN(flux));
            return flux;
        }

        /// <summary>
        /// Uses <see cref="IEulerEquationComponent.Flux(StateVector)"/> to
        /// calculate the flux which will be stored in
        /// <paramref name="output"/>.
        /// </summary>
        /// <param name="time">
        /// <see cref="NonlinearFlux.Flux(double, double[], double[], double[])"/>
        /// </param>
        /// <param name="x">
        /// <see cref="NonlinearFlux.Flux(double, double[], double[], double[])"/>
        /// </param>
        /// <param name="U">
        /// <see cref="NonlinearFlux.Flux(double, double[], double[], double[])"/>
        /// </param>
        /// <param name="output">
        /// <see cref="NonlinearFlux.Flux(double, double[], double[], double[])"/>
        /// </param>
        protected override void Flux(double time, double[] x, double[] U, double[] output) {
            StateVector state = new StateVector(U, material);
            equationComponent.Flux(state).CopyTo(output, output.Length);
        }

        /// <summary>
        /// Utility function that computes the fastest signal velocities in
        /// both affected cells according to Toro2009 (p. 329)
        /// </summary>
        /// <param name="stateIn">The flow state inside a cell</param>
        /// <param name="stateOut">
        /// The flow state in the considered neighbor cell
        /// </param>
        /// <param name="normal">A unit vector normal to the edge</param>
        /// <param name="waveSpeedIn">
        /// On exit, contains the fastest signal velocity in the direction of
        /// <paramref name="normal"/> inside a cell
        /// </param>
        /// <param name="waveSpeedOut">
        /// On exit, contains the fastest signal velocity in the direction of
        /// <paramref name="normal"/> in the considered neighbor cell
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void EstimateWaveSpeeds(StateVector stateIn, StateVector stateOut, ref ilPSP.Vector normal, out double waveSpeedIn, out double waveSpeedOut) {
            double normalVelocityIn = stateIn.Velocity * normal;
            double normalVelocityOut = stateOut.Velocity * normal;

            double meanPressure = 0.5 * (stateIn.Pressure + stateOut.Pressure);
            double meanDensity = 0.5 * (stateIn.Density + stateOut.Density);
            double meanSpeedOfSound = 0.5 * (stateIn.SpeedOfSound + stateOut.SpeedOfSound);

            double velocityJump = normalVelocityOut - normalVelocityIn;
            double gamma = config.EquationOfState.HeatCapacityRatio;
            double MachScaling = gamma * config.MachNumber * config.MachNumber;

            // Calculate the pressure estimate at the edge and use it to
            // calculate the components of the correction factor qIn and qOut.
            // See Toro2009, equation 10.61 and corrected according to 
            // dimensionless equations 
            double pStar = meanPressure
                - 0.5 * MachScaling * velocityJump * meanSpeedOfSound * meanDensity;
            pStar = Math.Max(0.0, pStar);

            double qIn = 1.0;
            if (pStar > stateIn.Pressure) {
                qIn = Math.Sqrt(
                    1.0 + 0.5 * (gamma + 1.0) * (pStar / stateIn.Pressure - 1.0) / gamma);
            }

            double qOut = 1.0;
            if (pStar > stateOut.Pressure) {
                qOut = Math.Sqrt(
                    1.0 + 0.5 * (gamma + 1.0) * (pStar / stateOut.Pressure - 1.0) / gamma);
            }

            // Finally determine the wave speeds
            waveSpeedIn = normalVelocityIn - stateIn.SpeedOfSound * qIn;
            waveSpeedOut = normalVelocityOut + stateOut.SpeedOfSound * qOut;
        }
    }
}
