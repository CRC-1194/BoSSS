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
using System.Diagnostics;
using System.IO;
using BoSSS.Foundation;
using BoSSS.Foundation.XDG;
using BoSSS.Solution.CompressibleFlowCommon.Boundary;
using BoSSS.Solution.CompressibleFlowCommon.MaterialProperty;
using ilPSP;

namespace BoSSS.Solution.CompressibleFlowCommon.Convection {

    /// <summary>
    /// Optimized version of the HLLC energy flux for ideal gases.
    /// </summary>
    public class OptimizedHLLCEnergyFlux : OptimizedHLLCFlux {

        /// <summary>
        /// Constructs a new flux
        /// </summary>
        /// <param name="boundaryMap">
        /// Mapping for boundary conditions
        /// </param>
        public OptimizedHLLCEnergyFlux(IBoundaryConditionMap boundaryMap, Material material)
            : base(boundaryMap, material) {
        }

        //static int count = 0;
        //static int quadPoints = 0;
        //StreamWriter writer;

        /// <summary>
        /// <see cref="INonlinearFlux.InnerEdgeFlux"/>
        /// </summary>
        public override void InnerEdgeFlux(
            double time,
            int jEdge,
            MultidimensionalArray x,
            MultidimensionalArray normal,
            MultidimensionalArray[] Uin,
            MultidimensionalArray[] Uout,
            int Offset,
            int Length,
            MultidimensionalArray Output) {

            int NoOfNodes = Uin[0].GetLength(1);
            int D = CompressibleEnvironment.NumberOfDimensions;
            double gamma = this.material.EquationOfState.HeatCapacityRatio;
            double Mach = this.material.MachNumber;
            double MachScaling = gamma * Mach * Mach;

            for (int e = 0; e < Length; e++) {
                for (int n = 0; n < NoOfNodes; n++) {
                    double densityIn = Uin[0][e + Offset, n];
                    double densityOut = Uout[0][e + Offset, n];

                    double momentumSquareIn = 0.0;
                    double momentumSquareOut = 0.0;
                    double normalVelocityIn = 0.0;
                    double normalVelocityOut = 0.0;
                    for (int d = 0; d < D; d++) {
                        momentumSquareIn += Uin[d + 1][e + Offset, n] * Uin[d + 1][e + Offset, n];
                        momentumSquareOut += Uout[d + 1][e + Offset, n] * Uout[d + 1][e + Offset, n];
                        normalVelocityIn += Uin[d + 1][e + Offset, n] * normal[e + Offset, n, d];
                        normalVelocityOut += Uout[d + 1][e + Offset, n] * normal[e + Offset, n, d];
                    }
                    normalVelocityIn /= densityIn;
                    normalVelocityOut /= densityOut;

                    double energyIn = Uin[D + 1][e + Offset, n];
                    double energyOut = Uout[D + 1][e + Offset, n];

                    double pIn = (gamma - 1.0) * (energyIn - MachScaling * 0.5 * momentumSquareIn / densityIn);
                    double pOut = (gamma - 1.0) * (energyOut - MachScaling * 0.5 * momentumSquareOut / densityOut);

                    double speedOfSoundIn = Math.Sqrt(pIn / densityIn) / Mach;
                    double speedOfSoundOut = Math.Sqrt(pOut / densityOut) / Mach;
                    //double speedOfSoundIn = (pIn / densityIn).Pow2() / Mach;
                    //double speedOfSoundOut = (pOut / densityOut).Pow2() / Mach;

                    double densityMean = 0.5 * (densityIn + densityOut);
                    double pressureMean = 0.5 * (pIn + pOut);
                    double speedOfSoundMean = 0.5 * (speedOfSoundIn + speedOfSoundOut);
                    double velocityJump = normalVelocityOut - normalVelocityIn;

                    // Calculate the pressure estimate at the edge and use it to
                    // calculate the components of the correction factor qIn and qOut
                    double pStar = pressureMean - 0.5 * MachScaling * velocityJump * speedOfSoundMean * densityMean;
                    pStar = Math.Max(0.0, pStar);

                    double qIn = 1.0;
                    if (pStar > pIn) {
                        qIn = Math.Sqrt(1.0 + 0.5 * (gamma + 1.0) * (pStar / pIn - 1.0) / gamma);
                    }

                    double qOut = 1.0;
                    if (pStar > pOut) {
                        qOut = Math.Sqrt(1.0 + 0.5 * (gamma + 1.0) * (pStar / pOut - 1.0) / gamma);
                    }

                    // Determine the wave speeds
                    double waveSpeedIn = normalVelocityIn - speedOfSoundIn * qIn;
                    double waveSpeedOut = normalVelocityOut + speedOfSoundOut * qOut;

                    double cIn = densityIn * (waveSpeedIn - normalVelocityIn);
                    double cOut = densityOut * (waveSpeedOut - normalVelocityOut);

                    // cf. Toro2009, equation 10.70
                    double speedDiff = cOut * normalVelocityOut - cIn * normalVelocityIn;
                    //if (Math.Abs(speedDiff) < 1e-13) {
                    //    speedDiff = 0.0;
                    //}

                    double pIn_minus_pOut = pIn - pOut;
                    //if (Math.Abs(pIn_minus_pOut) < 1e-13) {
                    //    pIn_minus_pOut = 0.0;
                    //}

                    double intermediateWaveSpeed =
                        (speedDiff + pIn_minus_pOut / MachScaling) /
                        (cOut - cIn);

                    //double intermediateWaveSpeed =
                    //    (cOut * normalVelocityOut - cIn * normalVelocityIn + (pIn - pOut) / MachScaling) /
                    //    (cOut - cIn);

                    double edgeFlux = 0.0;
                    // cf. Toro2009, equation 10.71
                    // flux = state.Velocity * (state.Energy + state.Pressure);
                    if (intermediateWaveSpeed > 0.0) {
                        edgeFlux = normalVelocityIn * (energyIn + pIn);
                        if (waveSpeedIn <= 0.0) {
                            double factor = densityIn * intermediateWaveSpeed * MachScaling
                                + pIn / (waveSpeedIn - normalVelocityIn);
                            double modifiedEnergy = (waveSpeedIn - normalVelocityIn) /
                                (waveSpeedIn - intermediateWaveSpeed) *
                                (energyIn + factor * (intermediateWaveSpeed - normalVelocityIn));
                            edgeFlux += waveSpeedIn * (modifiedEnergy - energyIn);
                        }
                    } else {
                        edgeFlux = normalVelocityOut * (energyOut + pOut);
                        if (waveSpeedOut >= 0.0) {
                            double factor = densityOut * intermediateWaveSpeed * MachScaling
                                + pOut / (waveSpeedOut - normalVelocityOut);
                            double modifiedEnergy = (waveSpeedOut - normalVelocityOut) /
                                (waveSpeedOut - intermediateWaveSpeed) *
                                (energyOut + factor * (intermediateWaveSpeed - normalVelocityOut));
                            edgeFlux += waveSpeedOut * (modifiedEnergy - energyOut);
                        }
                    }

                    Debug.Assert(!double.IsNaN(edgeFlux) || double.IsInfinity(edgeFlux));
                    Output[e + Offset, n] += edgeFlux;
                }
            }

            // Sort
            //if (writer == null) {
            //    writer = new StreamWriter(String.Format("InnerEdgeFlux_rhoE.txt"));
            //}

            //for (int e = 0; e < Length; e++) {
            //    for (int n = 0; n < NoOfNodes; n++) {
            //        writer.WriteLine(String.Format("{0:0.0000000000}\t{1:0.0000000000}\t{2:0.0000000000}", x[e + Offset, n, 0], x[e + Offset, n, 1], Output[e + Offset, n]));
            //        writer.Flush();
            //    }
            //}
            //writer.WriteLine("#####################################################################################");
            //writer.Flush();
        }

        /// <summary>
        /// <see cref="INonlinearFlux.Flux"/>
        /// </summary>
        public override void Flux(
            double time,
            MultidimensionalArray x,
            MultidimensionalArray[] U,
            int Offset,
            int Length,
            MultidimensionalArray Output) {

            int D = CompressibleEnvironment.NumberOfDimensions;
            int NoOfNodes = Output.GetLength(1);
            double gamma = this.material.EquationOfState.HeatCapacityRatio;
            double Mach = this.material.MachNumber;
            double gammaMachSquared = gamma * Mach * Mach;

            for (int e = 0; e < Length; e++) {
                for (int n = 0; n < NoOfNodes; n++) {
                    double density = U[0][e + Offset, n];
                    double energy = U[D + 1][e + Offset, n];

                    double momentumSquared = 0.0;
                    for (int d = 0; d < D; d++) {
                        momentumSquared += U[d + 1][e + Offset, n] * U[d + 1][e + Offset, n];
                    }
                    double pressure = (gamma - 1.0) * (energy - gammaMachSquared * 0.5 * momentumSquared / density);

                    //return state.Velocity * (state.Energy + state.Pressure);
                    for (int d = 0; d < D; d++) {
                        double flux = U[d + 1][e + Offset, n] / density * (energy + pressure);
                        Debug.Assert(!double.IsNaN(flux) || double.IsInfinity(flux));
                        Output[e + Offset, n, d] += flux;
                    }
                }
            }
        }
    }
}
