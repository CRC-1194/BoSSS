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
using ilPSP.Tracing;
using ilPSP.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BoSSS.Application.XNSERO_Solver {

    /// <summary>
    /// Handles the collision between particles
    /// </summary>
    class ParticleCollision {
        private readonly double TimestepSize;
        private readonly double GridLengthScale;
        private readonly double CoefficientOfRestitution;
        private double AccumulatedCollisionTimestep = 0;
        private double[][] AccumulatedLocalSaveTimestep;
        private bool[][] Overlapping;
        private readonly bool ExceptionWhenOverlap;
        private Vector[][] DistanceVector;
        private Vector[][] ClosestPoints;
        private readonly double[][] WallCoordinates;
        private readonly bool[] IsPeriodicBoundary;
        private readonly double MinDistance;
        private double[][] TemporaryVelocity;
        private Particle[] Particles;
        private int[][] CollisionCluster;
        private int[][] ParticleCollidedWith;

        /// <summary>
        /// Constructor for the collision model.
        /// </summary>
        /// <param name="gridLenghtscale">
        /// Characteristic length of the grid.
        /// </param>
        /// <param name="coefficientOfRestitution">
        /// Static coefficient of restitution
        /// </param>
        /// <param name="dt">
        /// time step size
        /// </param>
        /// <param name="wallCoordinates">
        /// Contains the position of the wall. First index defines vertical/horizontal walls, [0]: vertical, [1]: horizontal. Second index, [0]: left/upper wall [1]: right/lower wall
        /// </param>
        /// <param name="IsPeriodicBoundary">
        /// Determines whether a boundary is periodic. [0]: vertical boundary, [1]: horizontal boundary.
        /// </param>
        /// <param name="minDistance">
        /// Min. distance threshold.
        /// </param>
        public ParticleCollision(double gridLenghtscale, double coefficientOfRestitution, double dt, double[][] wallCoordinates, bool[] IsPeriodicBoundary, double minDistance, bool DetermineOnlyOverlap) {
            CoefficientOfRestitution = coefficientOfRestitution;
            TimestepSize = dt;
            GridLengthScale = gridLenghtscale;
            WallCoordinates = wallCoordinates;
            MinDistance = minDistance;
            this.IsPeriodicBoundary = IsPeriodicBoundary;
            this.ExceptionWhenOverlap = DetermineOnlyOverlap;
        }

        /// <summary>
        /// Update collision forces between two arbitrary particles and add them to forces acting on the corresponding particle
        /// </summary>
        /// <param name="particles">
        /// List of all particles
        /// </param>
        public void Calculate(Particle[] particles, RTree Tree) {
            Particles = particles;
            int[][] potentialCollisionPartners = new int[Particles.Length][];
            double subTimeStepWithoutCollision = 0;
            int ParticleOffset = Particles.Length;
            double distanceThreshold = MinDistance == 0 ? GridLengthScale / 2 : MinDistance;

            // Step 1
            // Loop over time until the particles collide.
            // =======================================================
            while (AccumulatedCollisionTimestep < TimestepSize) {
                CreateCollisionArrarys(Particles.Length);
                double globalMinimalDistance = double.MaxValue;
                // Step 2.1
                // Loop over the distance until it is smaller  
                // than a predefined threshold
                // -------------------------------------------------------
                while (globalMinimalDistance > distanceThreshold) {
                    // Step 2.1.1
                    // Move the particle with the current save time-step.
                    // -------------------------------------------------------
                    MoveParticlesWithSubTimestep(Particles, subTimeStepWithoutCollision);
                    subTimeStepWithoutCollision = double.MaxValue;
                    bool[][] AlreadyAnalysed = new bool[Particles.Length][];
                    for (int p0 = 0; p0 < Particles.Length; p0++) {
                        // Step 2.1.2
                        // Test for particle-wall collisions 
                        // -------------------------------------------------------
                        Vector[] nearFieldWallPoints = GetNearFieldWall(Particles[p0]);
                        for (int w = 0; w < nearFieldWallPoints.Length; w++) {
                            if (!nearFieldWallPoints[w].IsNullOrEmpty()) {
                                Particles[p0].ClosestPointOnOtherObjectToThis = new Vector(Particles[p0].Motion.GetPosition(0));
                                Particles[p0].ClosestPointOnOtherObjectToThis = new Vector(nearFieldWallPoints[w]);
                                CalculateLocalMinimumDistance(Particles[p0], out Vector temp_DistanceVector, out Vector temp_ClosestPoint_p0, out bool temp_Overlapping);
                                int wallID = ParticleOffset + w;
                                globalMinimalDistance = CalculateGlobalMinimumDistance(globalMinimalDistance, p0, wallID, temp_DistanceVector, new Vector[] { temp_ClosestPoint_p0 }, temp_Overlapping);
                                subTimeStepWithoutCollision = CalculateSubTimeStepWithoutCollision(subTimeStepWithoutCollision, p0, wallID);
                            }
                        }
                        // Step 2.1.3
                        // Test for particle-particle collisions 
                        // -------------------------------------------------------
                        potentialCollisionPartners[p0] = Tree.SearchForOverlap(Particles[p0], p0, TimestepSize - AccumulatedCollisionTimestep).ToArray();
                        AlreadyAnalysed[p0] = new bool[Particles.Length];
                        for (int p1 = 0; p1 < potentialCollisionPartners[p0].Length; p1++) {
                            int secondParticleID = potentialCollisionPartners[p0][p1];
                            if (!AlreadyAnalysed[p0][secondParticleID] && secondParticleID > p0) {
                                AlreadyAnalysed[p0][secondParticleID] = true;
                                CalculateLocalMinimumDistance(new Particle[] { Particles[p0], Particles[secondParticleID] }, out Vector temp_DistanceVector, out Vector[] temp_ClosestPoints, out bool temp_Overlapping);
                                globalMinimalDistance = CalculateGlobalMinimumDistance(globalMinimalDistance, p0, secondParticleID, temp_DistanceVector, temp_ClosestPoints, temp_Overlapping);
                                subTimeStepWithoutCollision = CalculateSubTimeStepWithoutCollision(subTimeStepWithoutCollision, p0, secondParticleID);
                            }
                        }
                    }
                    if (subTimeStepWithoutCollision >= 0)
                        AccumulatedCollisionTimestep += subTimeStepWithoutCollision;
                    if (AccumulatedCollisionTimestep >= TimestepSize)
                        break;
                    if (ExceptionWhenOverlap)
                        break;
                }
                if (ExceptionWhenOverlap)
                    break;
                if (AccumulatedCollisionTimestep == double.MaxValue)
                    break;

                ParticleCollidedWith = CreateArrayWithCollidedParticles(distanceThreshold, potentialCollisionPartners);
                CollisionCluster = ClusterCollisionsContainingSameParticles();
                CalculateCollision(distanceThreshold);
                SetParticleVariables(particles, subTimeStepWithoutCollision);
                Tree.UpdateTree(Particles, AccumulatedCollisionTimestep);
            }
        }

        private void CalculateCollision(double distanceThreshold) {
            for (int c = 0; c < CollisionCluster.Length; c++) {
                for (int i = 0; i < CollisionCluster[c].Length; i++) {
                    int currentParticleID = CollisionCluster[c][i];
                    if (IsParticle(currentParticleID)) {
                        for (int j = 0; j < ParticleCollidedWith[currentParticleID].Length; j++) {
                            int secondObjectID = ParticleCollidedWith[currentParticleID][j];
                            CalculateBinaryCollision(currentParticleID, secondObjectID, distanceThreshold);
                        }
                    }
                }
            }
        }

        private void CreateCollisionArrarys(int noOfParticles) {
            AccumulatedLocalSaveTimestep = new double[noOfParticles][];
            Overlapping = new bool[noOfParticles][];
            DistanceVector = new Vector[noOfParticles][];
            ClosestPoints = new Vector[noOfParticles][];
            TemporaryVelocity = new double[noOfParticles][];
            for (int p = 0; p < noOfParticles; p++) {
                AccumulatedLocalSaveTimestep[p] = new double[noOfParticles + 4];
                Overlapping[p] = new bool[noOfParticles + 4];
                DistanceVector[p] = new Vector[noOfParticles + 4];
                ClosestPoints[p] = new Vector[noOfParticles + 4];
                TemporaryVelocity[p] = new double[3];
                TemporaryVelocity[p][0] = Particles[p].Motion.GetTranslationalVelocity()[0];
                TemporaryVelocity[p][1] = Particles[p].Motion.GetTranslationalVelocity()[1];
                TemporaryVelocity[p][2] = Particles[p].Motion.GetRotationalVelocity();
            }
        }

        private void SetParticleVariables(Particle[] particles, double subTimeStepWithoutCollision) {
            for (int p = 0; p < Particles.Length; p++) {
                if (particles[p].IsCollided) {
                    particles[p].Motion.InitializeParticleVelocity(new double[] { TemporaryVelocity[p][0], TemporaryVelocity[p][1] }, TemporaryVelocity[p][2]);
                    particles[p].Motion.InitializeParticleAcceleration(new double[] { 0, 0 }, 0);
                }
                particles[p].Motion.SetCollisionTimestep(AccumulatedCollisionTimestep - subTimeStepWithoutCollision);
                CollisionCluster.Clear();
                ParticleCollidedWith.Clear();
            }
        }

        private double CalculateSubTimeStepWithoutCollision(double subTimeStepWithoutCollision, int firstObjectID, int secondObjectID) {
            double temp_SaveTimeStep = DynamicTimestep(firstObjectID, secondObjectID);
            AccumulatedLocalSaveTimestep[firstObjectID][secondObjectID] += temp_SaveTimeStep;
            if(IsParticle(secondObjectID))
                AccumulatedLocalSaveTimestep[secondObjectID][firstObjectID] += temp_SaveTimeStep;
            if (temp_SaveTimeStep < subTimeStepWithoutCollision && temp_SaveTimeStep > 0) {
                subTimeStepWithoutCollision = temp_SaveTimeStep;
            }
            return subTimeStepWithoutCollision;
        }

        private double CalculateGlobalMinimumDistance(double globalMinimalDistance, int firstObjectID, int secondObjectID, Vector temp_DistanceVector, Vector[] temp_ClosestPoints, bool temp_Overlapping) {
            Overlapping[firstObjectID][secondObjectID] = temp_Overlapping;
            ClosestPoints[firstObjectID][secondObjectID] = temp_ClosestPoints[0];
            DistanceVector[firstObjectID][secondObjectID] = new Vector(temp_DistanceVector);
            if (IsParticle(secondObjectID)) {
                Overlapping[secondObjectID][firstObjectID] = temp_Overlapping;
                ClosestPoints[secondObjectID][firstObjectID] = temp_ClosestPoints[1];
                temp_DistanceVector.ScaleInPlace(-1);
                DistanceVector[secondObjectID][firstObjectID] = new Vector(temp_DistanceVector);
            }
            if (DistanceVector[firstObjectID][secondObjectID].Abs() < globalMinimalDistance) {
                globalMinimalDistance = DistanceVector[firstObjectID][secondObjectID].Abs();
            }
            if (temp_Overlapping) {
                if (ExceptionWhenOverlap)
                    throw new Exception("Static particles overlap");
                DistanceVector[firstObjectID][secondObjectID] = new Vector(Particles[firstObjectID].Motion.GetPosition(0) - Particles[secondObjectID].Motion.GetPosition(0));
                DistanceVector[secondObjectID][firstObjectID] = new Vector(Particles[secondObjectID].Motion.GetPosition(0) - Particles[firstObjectID].Motion.GetPosition(0));
                globalMinimalDistance = 0;
            }

            return globalMinimalDistance;
        }

        private int[][] ClusterCollisionsContainingSameParticles() {
            List<int[]> globalParticleCluster = new List<int[]>();
            bool[] partOfCollisionCluster = new bool[Particles.Length + 4];
            for (int p0 = 0; p0 < Particles.Length; p0++) {
                if (!partOfCollisionCluster[p0]) {
                    List<int> currentParticleCluster = new List<int> { p0 };
                    partOfCollisionCluster[p0] = true;
                    for (int p1 = 1; p1 < ParticleCollidedWith[p0].Count(); p1++) {
                        currentParticleCluster.Add(ParticleCollidedWith[p0][p1]);
                        partOfCollisionCluster[ParticleCollidedWith[p0][p1]] = true;
                        FindCollisionClusterRecursive(ParticleCollidedWith, ParticleCollidedWith[p0][p1], currentParticleCluster, partOfCollisionCluster);
                    }
                    globalParticleCluster.Add(currentParticleCluster.ToArray());
                }
            }
            return globalParticleCluster.ToArray();
        }

        private void FindCollisionClusterRecursive(int[][] ParticleCollidedWith, int p0, List<int> currentParticleCluster, bool[] PartOfCollisionCluster) {
            if (IsParticle(p0)) {
                for (int p1 = 1; p1 < ParticleCollidedWith[p0].Count(); p1++) {
                    if (!PartOfCollisionCluster[ParticleCollidedWith[p0][p1]]) {
                        currentParticleCluster.Add(ParticleCollidedWith[p0][p1]);
                        PartOfCollisionCluster[p0] = true;
                        PartOfCollisionCluster[ParticleCollidedWith[p0][p1]] = true;
                        FindCollisionClusterRecursive(ParticleCollidedWith, p1, currentParticleCluster, PartOfCollisionCluster);
                    }
                }
            }
        }

        private int[][] CreateArrayWithCollidedParticles(double distanceThreshold, int[][] PotentialCollisionPartners) {
            int ParticleOffset = Particles.Length;
            int[][] particleCollidedWith = new int[Particles.Length][];
            for (int p0 = 0; p0 < Particles.Length; p0++) {
                List<int> currentParticleCollidedWith = new List<int>();
                for (int w = 0; w < 4; w++) {
                    FindCollisionPartners(p0, ParticleOffset + w, currentParticleCollidedWith, distanceThreshold);
                }
                for (int p1 = 0; p1 < PotentialCollisionPartners[p0].Length; p1++) {
                    FindCollisionPartners(p0, PotentialCollisionPartners[p0][p1], currentParticleCollidedWith, distanceThreshold);
                }
                particleCollidedWith[p0] = currentParticleCollidedWith.ToArray();
            }
            return particleCollidedWith;
        }

        private void FindCollisionPartners(int particle, int potentialCollisionPartner, List<int> currentParticleCollidedWith, double distanceThreshold) {
            if ((DistanceVector[particle][potentialCollisionPartner].Abs() <= distanceThreshold && AccumulatedCollisionTimestep < TimestepSize && AccumulatedLocalSaveTimestep[particle][potentialCollisionPartner] > 0) || Overlapping[particle][potentialCollisionPartner]) {
                int insertAtIndex = currentParticleCollidedWith.Count();
                for (int i = 1; i < currentParticleCollidedWith.Count(); i++) {
                    if (DistanceVector[particle][currentParticleCollidedWith[i]].Abs() > DistanceVector[particle][potentialCollisionPartner].Abs())
                        insertAtIndex = i;
                }
                currentParticleCollidedWith.Insert(insertAtIndex, potentialCollisionPartner);
            }
        }

        private double DynamicTimestep(int FirstParticleID, int SecondParticleID) {
            double detectCollisionVn_P0 = 0;
            double detectCollisionVn_P1 = 0;
            Vector normalVector = DistanceVector[FirstParticleID][SecondParticleID];
            double distance = normalVector.Abs();
            normalVector /= distance;
            if (Particles[FirstParticleID].Motion.IncludeTranslation() || Particles[FirstParticleID].Motion.IncludeRotation()) 
                detectCollisionVn_P0 = CalculateNormalSurfaceVelocity(FirstParticleID, normalVector, ClosestPoints[FirstParticleID][SecondParticleID]);
            if (IsParticle(SecondParticleID)) {
                if (Particles[FirstParticleID].Motion.IncludeTranslation() || Particles[FirstParticleID].Motion.IncludeRotation())
                    detectCollisionVn_P1 = CalculateNormalSurfaceVelocity(SecondParticleID, normalVector, ClosestPoints[SecondParticleID][FirstParticleID]);
            } else {
                detectCollisionVn_P0 = 10 * detectCollisionVn_P0;
            }
            return (detectCollisionVn_P1 - detectCollisionVn_P0 == 0) ? double.MaxValue : 0.01 * distance / (detectCollisionVn_P1 - detectCollisionVn_P0);
        }

        private double CalculateNormalSurfaceVelocity(int particleID, Vector normalVector, Vector closestPoint) {
            Vector pointVelocity = new Vector(2);
            Vector radialVector = Particles[particleID].CalculateRadialVector(closestPoint);
            pointVelocity[0] = TemporaryVelocity[particleID][0] - TemporaryVelocity[particleID][2] * radialVector[1];
            pointVelocity[1] = TemporaryVelocity[particleID][1] + TemporaryVelocity[particleID][2] * radialVector[0];
            return pointVelocity * normalVector;
        }

        private void MoveParticlesWithSubTimestep(Particle[] particles, double dynamicTimestep) {
            for (int p = 0; p < particles.Length; p++) {
                Particle currentParticle = particles[p];
                if (dynamicTimestep != 0) {
                    currentParticle.Motion.CollisionParticlePositionAndAngle(dynamicTimestep);
                }
            }
        }

        private void CalculateLocalMinimumDistance(Particle[] Particles, out Vector DistanceVector, out Vector[] ClosestPoints, out bool Overlapping) {
            using (new FuncTrace()) {
            int spatialDim = Particles[0].Motion.GetPosition(0).Dim;
            double distance = double.MaxValue;
            DistanceVector = new Vector(spatialDim);
            ClosestPoints = new Vector[2];
            ClosestPoints[0] = new Vector(spatialDim);
            ClosestPoints[1] = new Vector(spatialDim);
            Overlapping = false;
            int NoOfSubParticles1 = Particles[1] == null ? 1 : Particles[1].NoOfSubParticles;
;
                for (int i = 0; i < Particles[0].NoOfSubParticles; i++) {
                    for (int j = 0; j < NoOfSubParticles1; j++) {
                        GJK_DistanceAlgorithm(Particles[0], i, Particles[1], j, out Vector temp_DistanceVector, out Vector[] temp_ClosestPoints, out Overlapping);
                        if (Overlapping)
                            break;
                        if (temp_DistanceVector.Abs() < distance) {
                            distance = temp_DistanceVector.Abs();
                            DistanceVector = new Vector(temp_DistanceVector);
                            ClosestPoints = temp_ClosestPoints.CloneAs();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Computes the minimal distance between a particle and the wall.
        /// </summary>
        /// <param name="particle">
        /// The first particle.
        /// </param>
        /// <param name="DistanceVector">
        /// The vector of the minimal distance between the two objects.
        /// </param>
        /// <param name="ClosestPoint">
        /// The point on the first object closest to the second one.
        /// </param>
        /// <param name="Overlapping">
        /// Is true if the two particles are overlapping.
        /// </param>
        internal void CalculateLocalMinimumDistance(Particle particle, out Vector DistanceVector, out Vector ClosestPoint, out bool Overlapping) {
            using (new FuncTrace()) {
            int spatialDim = particle.Motion.GetPosition(0).Dim;
            double distance = double.MaxValue;
            DistanceVector = new Vector(spatialDim);
            ClosestPoint = new Vector(spatialDim);
            Overlapping = false;

                for (int i = 0; i < particle.NoOfSubParticles; i++) {
                    GJK_DistanceAlgorithm(particle, i, null, 1, out Vector temp_DistanceVector, out Vector[] temp_ClosestPoints, out Overlapping);
                    if (Overlapping)
                        break;
                    if (temp_DistanceVector.Abs() < distance) {
                        distance = temp_DistanceVector.Abs();
                        DistanceVector = new Vector(temp_DistanceVector);
                        ClosestPoint = new Vector(temp_ClosestPoints[0]);
                    }
                }
            }
        }

        /// <summary>
        /// Computes the distance between two objects (particles or walls). Algorithm based on
        /// E.G.Gilbert, D.W.Johnson, S.S.Keerthi.
        /// </summary>
        /// <param name="Particle">
        /// The first particle.
        /// </param>
        /// <param name="SubParticleID0">
        /// In case of concave particles the particle is devided into multiple convex subparticles. Each of them has its one ID and needs to be tested as if it was a complete particle.
        /// </param>
        ///  <param name="SecondObject">
        /// The second particle, if Particle1 == null it is assumed to be a wall.
        /// </param>
        /// <param name="SubParticleID1">
        /// In case of concave particles the particle is devided into multiple convex subparticles. Each of them has its one ID and needs to be tested as if it was a complete particle.
        /// </param>
        /// <param name="DistanceVec">
        /// The vector of the minimal distance between the two objects.
        /// </param>
        /// <param name="closestPoints">
        /// The point on one object closest to the other one.
        /// </param>
        /// <param name="Overlapping">
        /// Is true if the two particles are overlapping.
        /// </param>
        private void GJK_DistanceAlgorithm(Particle Particle, int SubParticleID0, Particle SecondObject, int SubParticleID1, out Vector DistanceVec, out Vector[] closestPoints, out bool Overlapping) {
            int NoOfVirtualDomainsP0 = Particle.Motion.GetOriginInVirtualPeriodicDomain().Count;
            int NoOfVirtualDomainsP1 = IsParticle(SecondObject) ? SecondObject.Motion.GetOriginInVirtualPeriodicDomain().Count : 0;
            Debug.Assert(NoOfVirtualDomainsP0 == NoOfVirtualDomainsP1);
            int spatialDim = Particle.Motion.GetPosition(0).Dim;
            Overlapping = false;
            DistanceVec = spatialDim switch {
                2 => new(int.MaxValue, int.MaxValue),
                3 => new(int.MaxValue, int.MaxValue, int.MaxValue),
                _ => throw new NotImplementedException("GJK-Algorithm only for 2D or 3D"),
            };
            closestPoints = new Vector[2];
            //Vector supportVector = Vector.StdBasis(0, spatialDim);
            Vector[] tempClosestPoints = new Vector[2];
            for (int d1 = 0; d1 < NoOfVirtualDomainsP0 + 1; d1++) {// Brute force: calculate distance in all virtual domains, could be made faster...
                for (int d2 = 0; d2 < NoOfVirtualDomainsP1 + 1; d2++) {
                    // Step 1
                    // Initialize the algorithm with the particle position
                    // =======================================================
                    Vector[] positionVectors = new Vector[2];
                    positionVectors[0] = d1 == NoOfVirtualDomainsP0
                        ? new Vector(Particle.Motion.GetPosition(0))
                        : new Vector(Particle.Motion.GetPosition(0) + Particle.Motion.GetOriginInVirtualPeriodicDomain()[d1]);
                    if (IsParticle(SecondObject))
                        positionVectors[1] = d2 == NoOfVirtualDomainsP0
                                                ? SecondObject.Motion.GetPosition(0)
                                                : SecondObject.Motion.GetPosition(0) + SecondObject.Motion.GetOriginInVirtualPeriodicDomain()[d2];
                    else positionVectors[1] = Particle.ClosestPointOnOtherObjectToThis;

                    Vector[] orientationAngle = new Vector[2];
                    orientationAngle[0] = new Vector(Particle.Motion.GetAngle(0));
                    if (IsParticle(SecondObject))
                        orientationAngle[1] = new Vector(SecondObject.Motion.GetAngle(0));

                    Vector supportVector = positionVectors[0] - positionVectors[1];
                    if (d1 == d2 && d1 != NoOfVirtualDomainsP0)
                        continue;
                    if (SecondObject == null) {
                        if (supportVector.Abs() > 1.5 * Particle.GetLengthScales().Max() && d1 != NoOfVirtualDomainsP0)
                            continue;
                    } else if (supportVector.Abs() > 1.5 * (Particle.GetLengthScales().Max() + SecondObject.GetLengthScales().Max()) && (d1 != NoOfVirtualDomainsP0 || d2 != NoOfVirtualDomainsP1))
                        continue;

                    if (supportVector.Abs() == 0)
                        throw new ArgumentOutOfRangeException("Support vector cannot have zero length");
                    supportVector.CheckForNanOrInfV();

                    // Define the simplex, which contains all points to be tested for their distance (max. 3 points in 2D)
                    List<Vector> Simplex = new() { new Vector(supportVector) };

                    tempClosestPoints[0] = new Vector(spatialDim);
                    tempClosestPoints[1] = new Vector(spatialDim);
                    int maxNoOfIterations = 1000;

                    // Step 2
                    // Start the iteration
                    // =======================================================
                    for (int i = 0; i <= maxNoOfIterations; i++) {
                        Vector negativeSupportVector = new Vector(spatialDim) - supportVector;//-= not possible with vectors?

                        // Calculate the support point of the two particles, 
                        // which are the closest points if the algorithm is finished.
                        // -------------------------------------------------------
                        tempClosestPoints[0] = Particle.GetSupportPoint(negativeSupportVector, positionVectors[0], orientationAngle[0], SubParticleID0, GridLengthScale);
                        
                        if (IsParticle(SecondObject)) // Particle-Particle collision
                            tempClosestPoints[1] = SecondObject.GetSupportPoint(supportVector, positionVectors[1], orientationAngle[1], SubParticleID1, GridLengthScale);
                        else {// Particle-wall collision
                            if (positionVectors[0][0] == positionVectors[1][0])
                                tempClosestPoints[1] = new Vector(tempClosestPoints[0][0], positionVectors[1][1]);
                            else
                                tempClosestPoints[1] = new Vector(positionVectors[1][0], tempClosestPoints[0][1]);
                        }

                        // The current support point can be found by forming 
                        // the difference of the support points on the two particles
                        // -------------------------------------------------------
                        Vector supportPoint = tempClosestPoints[0] - tempClosestPoints[1];
                        supportPoint.CheckForNanOrInfV();
                        if (d1 < NoOfVirtualDomainsP0)
                            tempClosestPoints[0] = new Vector(tempClosestPoints[0] - Particle.Motion.GetOriginInVirtualPeriodicDomain()[d1]);
                        if (d2 < NoOfVirtualDomainsP1)
                            tempClosestPoints[1] = new Vector(tempClosestPoints[1] - SecondObject.Motion.GetOriginInVirtualPeriodicDomain()[d2]);

                        // If the condition is true
                        // we have found the closest points!
                        // -------------------------------------------------------
                        if (((supportVector * negativeSupportVector) - (supportPoint * negativeSupportVector)) >= -1e-12 && i > 1)
                            break;

                        // Add new support point to simplex
                        // -------------------------------------------------------
                        Simplex.Insert(0, new Vector(supportPoint));

                        // Calculation the new support vector with the distance
                        // algorithm
                        // -------------------------------------------------------
                        supportVector = DistanceAlgorithm(Simplex, out Overlapping);

                        // End algorithm if the two objects are overlapping.
                        // -------------------------------------------------------
                        if (Overlapping)
                            break;

                        // Could not find the closest points... crash!
                        // -------------------------------------------------------
                        if (i > maxNoOfIterations)
                            throw new Exception("No convergence in GJK-algorithm, reached iteration #" + i);
                    }
                    if (supportVector.Abs() < DistanceVec.Abs()) {//w/o periodic bndy: this condition is always true; w periodic bndy: necessary!
                        DistanceVec = new Vector(supportVector);
                        closestPoints[0] = new Vector(tempClosestPoints[0]);
                        closestPoints[1] = new Vector(tempClosestPoints[1]);
                    }
                }
            }
        }

        /// <summary>
        /// Calculating the distance between the origin and the <paramref name="simplex"/>. 
        /// See Ericson, Christer. Real-Time Collision Detection. 2nd ed. San Francisco, CA: Morgan Kaufmann Publishers, 2005.
        /// </summary>
        /// <param name="simplex"></param>
        /// <param name="overlapping"></param>
        /// <returns></returns>
        private static Vector DistanceAlgorithm(List<Vector> simplex, out bool overlapping) {
            int spatialDimension = simplex[0].Dim;
            Vector supportVector = new(spatialDimension);
            overlapping = false;

            // Step 1
            // Test for multiple Simplex-points 
            // and remove the duplicates
            // =======================================================
            for (int s1 = 0; s1 < simplex.Count; s1++) {
                for (int s2 = s1 + 1; s2 < simplex.Count; s2++) {
                    if ((simplex[s1] - simplex[s2]).Abs() < 1e-8) {
                        simplex.RemoveAt(s2);
                    }
                }
            }

            // Step 2
            // Calculate dot product between all simplex vectors and 
            // save to an 2D-array.
            // =======================================================
            double[][] dotProductSimplex = new double[simplex.Count][];
            for (int s1 = 0; s1 < simplex.Count; s1++) {
                dotProductSimplex[s1] = new double[simplex.Count];
                for (int s2 = s1; s2 < simplex.Count; s2++) {
                    dotProductSimplex[s1][s2] = simplex[s1] * simplex[s2];
                }
            }

            // Step 3
            // Main routine to determine the relatve position of
            // the simplex towards the origin.
            // =======================================================
            // The simplex contains only one element, which must be
            // the closest point of this simplex to the origin
            // -------------------------------------------------------
            if (simplex.Count == 1) 
                supportVector = new Vector(simplex[0]);

            // The simplex contains two elements, lets test which is
            // closest to the origin
            // -------------------------------------------------------
            else if (simplex.Count == 2) {
                // One of the simplex point is closest to the origin, 
                // choose this and delete the other one.
                // -------------------------------------------------------
                bool continueAlgorithmFlag = true;
                for (int s = 0; s < simplex.Count; s++) {
                    if (dotProductSimplex[s][s] - dotProductSimplex[0][1] <= 0) {
                        supportVector = new Vector(simplex[s]);
                        simplex.RemoveAt(Math.Abs(s - 1));
                        continueAlgorithmFlag = false;
                        break;
                    }
                }
                // A point at the line between the two simplex points is
                // closest to the origin, thus we need to keep both points.
                // -------------------------------------------------------
                if (continueAlgorithmFlag) {
                    Vector simplexDistanceVector = simplex[1] - simplex[0];
                    double lambda = spatialDimension switch {
                        2 => Math.Abs(simplex[1].CrossProduct2D(simplexDistanceVector)) / simplexDistanceVector.AbsSquare(),
                        3 => (simplex[1].CrossProduct(simplexDistanceVector)).Abs() / simplexDistanceVector.AbsSquare(),
                        _ => throw new ArgumentOutOfRangeException("Irregular spatial dimension"),
                    };
                    if (lambda == 0) // if the origin lies on the line between the two simplex points, the two objects are overlapping in one point
                        overlapping = true;
                    supportVector[0] = -lambda * simplexDistanceVector[1];
                    supportVector[1] = lambda * simplexDistanceVector[0];
                }
            }

            // The simplex contains three elements, lets test which is
            // closest to the origin
            // -------------------------------------------------------
            else if (simplex.Count == 3) {
                bool continueAlgorithmFlag = true;
                // Test whether one of the simplex points is closest to 
                // the origin
                // -------------------------------------------------------
                for (int s1 = 0; s1 < simplex.Count; s1++) {
                    int s2 = s1 == 2 ? 2 : 1;
                    int s3 = s1 == 0 ? 0 : 1;
                    if (dotProductSimplex[s1][s1] - dotProductSimplex[0][s2] <= 0 && dotProductSimplex[s1][s1] - dotProductSimplex[s3][2] <= 0) {
                        supportVector = new Vector(simplex[s1]);
                        // Delete the complete simplex and add back the point closest to the origin
                        simplex.Clear();
                        simplex.Add(new Vector(supportVector));
                        continueAlgorithmFlag = false;
                        break;
                    }
                }
                // None of the simplex points was the closest point, 
                // thus, it has to be any point at the edges
                // -------------------------------------------------------
                if (continueAlgorithmFlag) {
                    for (int s1 = simplex.Count() - 1; s1 >= 0; s1--) {
                        int s2 = s1 == 0 ? 1 : 2;
                        int s3 = s1 == 2 ? 1 : 0;
                        // Calculate a triple crossproduct and dotproduct ("quadrupelproduct") of the form (BC x BA) x BA * BX = (BA(BA*BC)-BC(BA*BA))*BX
                        double quadrupelProduct = new();
                        switch (s1) {
                            case 0:
                                double temp1 = dotProductSimplex[1][2] - dotProductSimplex[0][2] - dotProductSimplex[1][1] + dotProductSimplex[0][1];
                                double temp2 = dotProductSimplex[0][1] - dotProductSimplex[0][0] - dotProductSimplex[1][2] + dotProductSimplex[0][2];
                                double temp3 = dotProductSimplex[1][1] - 2 * dotProductSimplex[0][1] + dotProductSimplex[0][0];
                                quadrupelProduct = dotProductSimplex[0][1] * temp1 + dotProductSimplex[1][1] * temp2 + dotProductSimplex[1][2] * temp3;
                                break;
                            case 1:
                                temp1 = -dotProductSimplex[2][2] + dotProductSimplex[0][2] + dotProductSimplex[1][2] - dotProductSimplex[0][1];
                                temp2 = dotProductSimplex[2][2] - 2 * dotProductSimplex[0][2] + dotProductSimplex[0][0];
                                temp3 = dotProductSimplex[0][2] - dotProductSimplex[0][0] - dotProductSimplex[1][2] + dotProductSimplex[0][1];
                                quadrupelProduct = dotProductSimplex[0][2] * temp1 + dotProductSimplex[1][2] * temp2 + dotProductSimplex[2][2] * temp3;
                                break;
                            case 2:
                                temp1 = dotProductSimplex[2][2] - 2 * dotProductSimplex[1][2] + dotProductSimplex[1][1];
                                temp2 = -dotProductSimplex[2][2] + dotProductSimplex[1][2] + dotProductSimplex[0][2] - dotProductSimplex[0][1];
                                temp3 = dotProductSimplex[1][2] - dotProductSimplex[1][1] - dotProductSimplex[0][2] + dotProductSimplex[0][1];
                                quadrupelProduct = dotProductSimplex[0][2] * temp1 + dotProductSimplex[1][2] * temp2 + dotProductSimplex[2][2] * temp3;
                                break;
                        }
                        // A point on one of the edges is closest to the origin.
                        if (dotProductSimplex[s3][s3] - dotProductSimplex[s3][s2] >= 0 && dotProductSimplex[s2][s2] - dotProductSimplex[s3][s2] >= 0 && quadrupelProduct >= 0 && continueAlgorithmFlag) {
                            Vector simplexDistanceVector = simplex[s2] - simplex[s3];
                            double lambda = spatialDimension switch {
                                2 => Math.Abs(simplex[s2].CrossProduct2D(simplexDistanceVector)) / simplexDistanceVector.AbsSquare(),
                                3 => (simplex[s2].CrossProduct(simplexDistanceVector)).Abs() / simplexDistanceVector.AbsSquare(),
                                _ => throw new ArgumentOutOfRangeException("Irregular spatial dimension"),
                            };
                            supportVector[0] = -lambda * simplexDistanceVector[1];
                            supportVector[1] = lambda * simplexDistanceVector[0];
                            // save the two remaining simplex points and clear the simplex.
                            Vector tempSimplex1 = new(simplex[s2]);
                            Vector tempSimplex2 = new(simplex[s3]);
                            simplex.Clear();
                            // Re-add the remaining points
                            simplex.Add(tempSimplex1);
                            simplex.Add(tempSimplex2);
                            continueAlgorithmFlag = false;
                            break;
                        }
                    }
                }
                // None of the conditions above are true, 
                // thus, the simplex must contain the origin and 
                // the two particles do overlap.
                // -------------------------------------------------------
                if (continueAlgorithmFlag)
                    overlapping = true;
            } else { //In case of 3D 3rd order simplices might arise, necessary to add them here!
                throw new NotImplementedException("Distance algorithm is only implemented for max 2nd order simplex (a triangle)");
            }
            return supportVector;
        }

        /// <summary>
        /// Physical collision model
        /// </summary>
        /// <param name="particleID"></param>
        /// <param name="secondObjectID"></param>
        /// <param name="threshold"></param>
        private void CalculateBinaryCollision(int particleID, int secondObjectID, double threshold) {
            if (DistanceVector[particleID][secondObjectID].Count != 2)
                throw new NotImplementedException("Physical collision model only implemented for 2D");

            double distance = DistanceVector[particleID][secondObjectID].Abs();
            Vector normalVector = DistanceVector[particleID][secondObjectID];
            normalVector.NormalizeInPlace();
            Vector tangentialVector = new(-normalVector[1], normalVector[0]);

            if (distance <= threshold || Overlapping[particleID][secondObjectID]) {
                double[] normalSurfaceVelocity = new double[2];
                if (Particles[0].Motion.IncludeTranslation() || Particles[0].Motion.IncludeRotation())
                    normalSurfaceVelocity[0] = CalculateNormalSurfaceVelocity(particleID, normalVector, ClosestPoints[particleID][secondObjectID]);
                if (IsParticle(secondObjectID)) {
                    if (Particles[1].Motion.IncludeTranslation() || Particles[1].Motion.IncludeRotation())
                        normalSurfaceVelocity[1] = CalculateNormalSurfaceVelocity(secondObjectID, normalVector, ClosestPoints[secondObjectID][particleID]);
                }
                if (normalSurfaceVelocity[1] - normalSurfaceVelocity[0] <= 0)
                    return;

                Particles[particleID].CalculateEccentricity(normalVector, ClosestPoints[particleID][secondObjectID]);
                if (IsParticle(secondObjectID))
                    Particles[secondObjectID].CalculateEccentricity(normalVector, ClosestPoints[secondObjectID][particleID]);

                double[] collisionCoefficient = CalculateCollisionCoefficient(particleID, secondObjectID, normalVector);

                Vector velocityP0 = CalculateNormalAndTangentialVelocity(particleID, normalVector);
                Vector radialVectorP0 = Particles[particleID].CalculateRadialVector(ClosestPoints[particleID][secondObjectID]);
                Vector tempVel0 = Particles[particleID].Motion.IncludeTranslation()
                    ? (velocityP0[0] + collisionCoefficient[0] / Particles[particleID].Mass) * normalVector + (velocityP0[1] + collisionCoefficient[1] / Particles[particleID].Mass) * tangentialVector
                    : new Vector(0, 0);
                TemporaryVelocity[particleID][0] = tempVel0[0];
                TemporaryVelocity[particleID][1] = tempVel0[1];
                TemporaryVelocity[particleID][2] = Particles[particleID].Motion.IncludeRotation() ? TemporaryVelocity[particleID][2] + (radialVectorP0[0] * normalVector[1] - radialVectorP0[1] * normalVector[0]) * collisionCoefficient[0] / Particles[particleID].MomentOfInertia + (radialVectorP0[0] * tangentialVector[1] - radialVectorP0[1] * tangentialVector[0]) * collisionCoefficient[1] / Particles[particleID].MomentOfInertia : 0;
                Particles[particleID].IsCollided = true;
                Overlapping[particleID][secondObjectID] = false;

                if (IsParticle(secondObjectID)) {
                    Vector velocityP1 = CalculateNormalAndTangentialVelocity(secondObjectID, normalVector);
                    Vector tempVel1 = Particles[secondObjectID].Motion.IncludeTranslation()
                        ? (velocityP1[0] - collisionCoefficient[0] / Particles[secondObjectID].Mass) * normalVector + (velocityP1[1] - collisionCoefficient[1] / Particles[secondObjectID].Mass) * tangentialVector
                        : new Vector(0, 0);
                    Vector radialVectorP1 = Particles[secondObjectID].CalculateRadialVector(ClosestPoints[secondObjectID][particleID]);
                    TemporaryVelocity[secondObjectID][0] = tempVel1[0];
                    TemporaryVelocity[secondObjectID][1] = tempVel1[1];
                    TemporaryVelocity[secondObjectID][2] = Particles[secondObjectID].Motion.IncludeRotation() ? TemporaryVelocity[secondObjectID][2] - (radialVectorP1[0] * normalVector[1] - radialVectorP1[1] * normalVector[0]) * collisionCoefficient[0] / Particles[secondObjectID].MomentOfInertia - (radialVectorP1[0] * tangentialVector[1] - radialVectorP1[1] * tangentialVector[0]) * collisionCoefficient[1] / Particles[secondObjectID].MomentOfInertia : 0;
                    Overlapping[secondObjectID][particleID] = false;
                    Particles[secondObjectID].IsCollided = true;
                    Console.WriteLine("Particle " + particleID + " and particle " + secondObjectID + " collided");
                } else {
                    Console.WriteLine("Particle " + particleID + " and wall " + secondObjectID + " collided");
                }
            }
        }

        private Vector CalculateNormalAndTangentialVelocity(int particleID, Vector normalVector) {
            Vector tangentialVector = new(-normalVector[1], normalVector[0]);
            Vector velocity = new(TemporaryVelocity[particleID][0], TemporaryVelocity[particleID][1]);
            return new Vector(velocity * normalVector, velocity * tangentialVector);
        }

        private double[] CalculateCollisionCoefficient(int particleID, int secondObjectID, Vector normalVector) {
            Vector tangentialVector = new(-normalVector[1], normalVector[0]);
            Vector[] translationalVelocity = new Vector[2];
            translationalVelocity[0] = CalculateNormalAndTangentialVelocity(particleID, normalVector);
            translationalVelocity[1] = new Vector(0, 0);
            double[] massReciprocal = new double[] { 0, 0 };
            double[] normalMomentOfInertiaReciprocal = new double[] { 0, 0 };
            double[] tangentialMomentOfInertiaReciprocal = new double[] { 0, 0 };
            double[] normalEccentricity = new double[] { 0, 0 };
            double[] tangentialEccentricity = new double[] { 0, 0 };

            massReciprocal[0] = Particles[particleID].Motion.IncludeTranslation() ? 1 / Particles[particleID].Mass : 0;
            normalMomentOfInertiaReciprocal[0] = Particles[particleID].Motion.IncludeRotation() ? Particles[particleID].CalculateSecondOrderEccentricity(normalVector, ClosestPoints[particleID][secondObjectID]) / Particles[particleID].MomentOfInertia : 0;
            tangentialMomentOfInertiaReciprocal[0] = Particles[particleID].Motion.IncludeRotation() ? Particles[particleID].CalculateSecondOrderEccentricity(tangentialVector, ClosestPoints[particleID][secondObjectID]) / Particles[particleID].MomentOfInertia : 0;
            normalEccentricity[0] = Particles[particleID].CalculateEccentricity(normalVector, ClosestPoints[particleID][secondObjectID]);
            tangentialEccentricity[0] = Particles[particleID].CalculateEccentricity(tangentialVector, ClosestPoints[particleID][secondObjectID]);

            if (IsParticle(secondObjectID)) {
                translationalVelocity[1] = CalculateNormalAndTangentialVelocity(secondObjectID, normalVector);
                massReciprocal[1] = Particles[secondObjectID].Motion.IncludeTranslation() ? 1 / Particles[secondObjectID].Mass : 0;
                normalMomentOfInertiaReciprocal[1] = Particles[secondObjectID].Motion.IncludeRotation() ? Particles[secondObjectID].CalculateSecondOrderEccentricity(normalVector, ClosestPoints[secondObjectID][particleID]) / Particles[secondObjectID].MomentOfInertia : 0;
                tangentialMomentOfInertiaReciprocal[1] = Particles[secondObjectID].Motion.IncludeRotation() ? Particles[secondObjectID].CalculateSecondOrderEccentricity(tangentialVector, ClosestPoints[secondObjectID][particleID]) / Particles[secondObjectID].MomentOfInertia : 0;
                normalEccentricity[1] = Particles[secondObjectID].CalculateEccentricity(normalVector, ClosestPoints[secondObjectID][particleID]);
                tangentialEccentricity[1] = Particles[secondObjectID].CalculateEccentricity(tangentialVector, ClosestPoints[secondObjectID][particleID]);
            }
            double[] collisionCoefficient = new double[2];
            collisionCoefficient[0] = -(1 + CoefficientOfRestitution) * ((translationalVelocity[0][0] - translationalVelocity[1][0]) / (massReciprocal[0] + massReciprocal[1] + normalMomentOfInertiaReciprocal[0] + normalMomentOfInertiaReciprocal[1]));
            collisionCoefficient[1] = 0;// -(translationalVelocity[0][1] - translationalVelocity[1][1]) / (massReciprocal[0] + massReciprocal[1] + tangentialMomentOfInertiaReciprocal[0] + tangentialMomentOfInertiaReciprocal[1]);
            double tempRotVelocity2 = 0;
            if (IsParticle(secondObjectID))
                tempRotVelocity2 = TemporaryVelocity[secondObjectID][2];
            collisionCoefficient[0] -= (1 + CoefficientOfRestitution) * ((normalEccentricity[0] * TemporaryVelocity[particleID][2] - normalEccentricity[1] * tempRotVelocity2) / (massReciprocal[0] + massReciprocal[1] + normalMomentOfInertiaReciprocal[0] + normalMomentOfInertiaReciprocal[1]));
            collisionCoefficient[1] -= 0;// (tangentialEccentricity[0] * TemporaryVelocity[p0][2] - tangentialEccentricity[1] * tempRotVelocity2) / (massReciprocal[0] + massReciprocal[1] + tangentialMomentOfInertiaReciprocal[0] + tangentialMomentOfInertiaReciprocal[1]);
            return collisionCoefficient;
        }

        private Vector[] GetNearFieldWall(Particle particle) {
            Vector[] nearFieldWallPoint = new Vector[4];
            Vector particlePosition = particle.Motion.GetPosition(0);
            double particleMaxLengthscale = particle.GetLengthScales().Max();
            for (int w0 = 0; w0 < WallCoordinates.Length; w0++) {
                for (int w1 = 0; w1 < WallCoordinates[w0].Length; w1++) {
                    if (WallCoordinates[w0][w1] != 0 && !IsPeriodicBoundary[w0]) {
                        double minDistance = Math.Abs(particlePosition[w0] - WallCoordinates[w0][w1]) - particleMaxLengthscale;
                        if (minDistance < 5 * GridLengthScale) {
                            if (w0 == 0)
                                nearFieldWallPoint[w0 + w1] = new Vector(WallCoordinates[0][w1], particlePosition[1]);
                            else
                                nearFieldWallPoint[w0 * 2 + w1] = new Vector(particlePosition[0], WallCoordinates[1][w1]);
                        }
                    }
                }
            }
            return nearFieldWallPoint;
        }

        /// <summary>
        /// Returns true if <paramref name="Object"/> is a particle. Returns <see langword="false"/> if <paramref name="Object"/> is a wall.
        /// </summary>
        /// <param name="Object"></param>
        /// <returns></returns>
        private static bool IsParticle(Particle Object) {
            return Object != null;
        }

        private bool IsParticle(int objectID) {
            return objectID < Particles.Length;
        }
    }
}
