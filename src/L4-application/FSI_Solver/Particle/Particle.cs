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
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using BoSSS.Solution.Control;
using System.Runtime.InteropServices;
using BoSSS.Foundation.XDG;
using BoSSS.Foundation.Quadrature;
using ilPSP;
using ilPSP.Utils;
using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using System.Diagnostics;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.Grid.RefElements;
using MPI.Wrappers;
using NUnit.Framework;
using FSI_Solver;
using System.Collections;

namespace BoSSS.Application.FSI_Solver
{

    /// <summary>
    /// Particle properties (for disk shape and spherical particles only).
    /// </summary>
    [DataContract]
    [Serializable]
    abstract public class Particle : ICloneable {

        /// <summary>
        /// <summary>
        /// Empty constructor used during de-serialization
        /// </summary>
        protected Particle()
        {
            // noop
        }
        
        public Particle(int Dim, double[] startPos = null, double startAngl = 0.0) {
            
            SpatialDim = Dim;

            // Particle history
            // =============================   
            for (int i = 0; i < m_HistoryLength; i++) {
                Position.Add(new double[Dim]);
                Angle.Add(new double());
                TranslationalVelocity.Add(new double[Dim]);
                TranslationalAcceleration.Add(new double[Dim]);
                RotationalVelocity.Add(new double());
                RotationalAcceleration.Add(new double());
                HydrodynamicForces.Add(new double[Dim]);
                HydrodynamicTorque.Add(new double());
            }

            // ============================= 
            if (startPos == null) {
                startPos = new double[Dim];
            }
            Position[0] = startPos;
            Position[1] = startPos;
            //From degree to radiant
            Angle[0] = StartingAngle = startAngl * 2 * Math.PI / 360;
            Angle[1] = startAngl * 2 * Math.PI / 360;

            //UpdateLevelSetFunction();
        }

        /// <summary>
        /// Check whether any particles is collided with another particle
        /// </summary>
        public bool Collided;
        
        /// <summary>
        /// Skip calculation of hydrodynamic force and Torque if particles are too close
        /// </summary>
        [DataMember]
        public bool skipForceIntegration = false;

        /// <summary>
        /// Number of iterations
        /// </summary>
        [DataMember]
        public int iteration_counter_P = 0;

        /// <summary>
        /// Constant Forces and Torque underrelaxation?
        /// </summary>
        [DataMember]
        public bool AddaptiveUnderrelaxation = false;

        /// <summary>
        /// Defines the order of the underrelaxation factor
        /// </summary>
        [DataMember]
        public int underrelaxationFT_exponent = 0;

        /// <summary>
        /// Underrelaxation factor
        /// </summary>
        [DataMember]
        public double underrelaxation_factor = -1;

        /// <summary>
        /// Set true if you want to delete all values of the Forces anf Torque smaller than convergenceCriterion*1e-2
        /// </summary>
        [DataMember]
        public bool ClearSmallValues = false;

        #region Misc parameters

        /// <summary>
        /// The color of the particle.
        /// </summary>
        public int ParticleColor = new int();

        /// <summary>
        /// Colored cells of this particle. 0: CellID, 1: Color
        /// </summary>
        public int[] ParticleColoredCells;

        /// <summary>
        /// Length of history for time, velocity, position etc.
        /// </summary>
        readonly int m_HistoryLength = 4;
        #endregion

        #region Added dampig parameters
        /// <summary>
        /// Set false if you want to include the effects of added damping
        /// </summary>
        [DataMember]
        public bool neglectAddedDamping = true;

        /// <summary>
        /// Complete added damping tensor, for reference: Banks et.al. 2017
        /// </summary>
        [DataMember]
        public double[,] AddedDampingTensor = new double[6, 6];
        #endregion

        #region Geometric parameters
        /// <summary>
        /// Spatial Dimension of the particle 
        /// </summary>
        [DataMember]
        private readonly int SpatialDim;
        
        virtual internal int NoOfSubParticles() { return 1; }

        /// <summary>
        /// some length scale 
        /// </summary>
        abstract protected double AverageDistance { get; }
        #endregion

        #region Virtual force model parameter
        ///// <summary>
        ///// needed for second velocity model
        ///// </summary>
        //public double C_v = 0.5;

        ///// <summary>
        ///// needed for second velocity model, obsolete?
        ///// </summary>
        //public double velResidual_ConvergenceCriterion = 1e-6;

        ///// <summary>
        ///// needed for second velocity model, obsolete?
        ///// </summary>
        //public double MaxParticleVelIterations = 10000;

        //private int vel_iteration_counter;
        #endregion

        /// <summary>
        /// Density of the particle.
        /// </summary>
        [DataMember]
        public double particleDensity = 1;
        
        /// <summary>
        /// The position (center of mass) of the particle in the current time step.
        /// </summary>
        [DataMember]
        public List<double[]> Position = new List<double[]>();
        
        /// <summary>
        /// The angle (center of mass) of the particle in the current time step.
        /// </summary>
        [DataMember]
        public List<double> Angle = new List<double>();

        ///// <summary>
        /// The angle (center of mass) of the particle at the starting point.
        /// </summary>
        [DataMember]
        private readonly double StartingAngle = new double();
        
        /// <summary>
        /// The translational velocity of the particle in the current time step.
        /// </summary>
        [DataMember]
        public List<double[]> TranslationalVelocity = new List<double[]>();

        /// <summary>
        /// The translational velocity of the particle in the current time step. This list is used by the momentum conservation model.
        /// </summary>
        [DataMember]
        public List<double[]> CollisionTranslationalVelocity = new List<double[]>();

        /// <summary>
        /// The translational velocity of the particle in the current time step. This list is used by the momentum conservation model.
        /// </summary>
        [DataMember]
        public List<double[]> CollisionNormal = new List<double[]>();

        /// <summary>
        /// The translational velocity of the particle in the current time step. This list is used by the momentum conservation model.
        /// </summary>
        [DataMember]
        public List<double[]> CollisionTangential = new List<double[]>();

        /// <summary>
        /// The translational velocity of the particle in the current time step. This list is used by the momentum conservation model.
        /// </summary>
        [DataMember]
        public List<double[]> CollisionPositionCorrection = new List<double[]>();

        /// <summary>
        /// The translational velocity of the particle in the current time step. This list is used by the momentum conservation model.
        /// </summary>
        [DataMember]
        public double CollisionTimestep = new double();

        /// <summary>
        /// The translational velocity of the particle in the current time step. This list is used by the momentum conservation model.
        /// </summary>
        [DataMember]
        public double CollisionPreviousTimestep = new double();

        /// <summary>
        /// The translational velocity of the particle in the current time step. This list is used by the momentum conservation model.
        /// </summary>
        [DataMember]
        public double[] TotalCollisionPositionCorrection = new double[2];

        /// <summary>
        /// The angular velocity of the particle in the current time step.
        /// </summary>
        [DataMember]
        public List<double> RotationalVelocity = new List<double>();

        /// <summary>
        /// The angular velocity of the particle in the current time step. This list is used by the momentum conservation model.
        /// </summary>
        [DataMember]
        public List<double> CollisionRotationalVelocity = new List<double>();

        /// <summary>
        /// The translational velocity of the particle in the current time step.
        /// </summary>
        [DataMember]
        public List<double[]> TranslationalAcceleration = new List<double[]>();
        
        /// <summary>
        /// The angular velocity of the particle in the current time step.
        /// </summary>
        [DataMember]
        public List<double> RotationalAcceleration = new List<double>();
        
        /// <summary>
        /// The force acting on the particle in the current time step.
        /// </summary>
        [DataMember]
        public List<double[]> HydrodynamicForces = new List<double[]>();

        /// <summary>
        /// The force acting on the particle in the current time step.
        /// </summary>
        [DataMember]
        public double[] ForcesPrevIteration = new double[2];

        /// <summary>
        /// The Torque acting on the particle in the current time step.
        /// </summary>
        [DataMember]
        public List<double> HydrodynamicTorque = new List<double>();

        /// <summary>
        /// The force acting on the particle in the current time step.
        /// </summary>
        [DataMember]
        public double TorquePrevIteration = new double();

        /// <summary>
        /// AddedDampingCoefficient
        /// </summary>
        [DataMember]
        public double AddedDampingCoefficient = 1;

        /// <summary>
        /// Level set function describing the particle.
        /// </summary>       
        public abstract double Phi_P(double[] X);

        /// <summary>
        /// Sets the gravity in vertical direction, default is 0.0
        /// </summary>
        [DataMember]
        public double GravityVertical = 0.0;

        /// <summary>
        /// Set true if the particle should be an active particle, i.e. self driven
        /// </summary>
        [DataMember]
        public bool ActiveParticle = false;

        /// <summary>
        /// Convergence criterion for the calculation of the Forces and Torque
        /// </summary>
        [DataMember]
        public double ForceAndTorque_convergence = 1e-8;

        /// <summary>
        /// Active stress on the current particle.
        /// </summary>
        public double ActiveStress = 0;

        /// <summary>
        /// Active velocity (alternative to active stress) on the current particle.
        /// </summary>
        [DataMember]
        public double ActiveVelocity;

        /// <summary>
        /// Area of the current particle.
        /// </summary>
        abstract public double Area_P {
            get;
        }

        /// <summary>
        /// Mass of the current particle.
        /// </summary>
        public double Mass_P {
            get {
                double a = Area_P;
                if (a <= 0.0 || double.IsNaN(a) || double.IsInfinity(a))
                    throw new ArithmeticException("Particle volume/area is " + a);
                return Area_P * particleDensity;
            }
        }

        /// <summary>
        /// Circumference of the current particle.
        /// </summary>
        abstract protected double Circumference_P {
            get;
        }

        /// <summary>
        /// Moment of inertia of the current particle.
        /// </summary>
        abstract public double MomentOfInertia_P {
            get;
        }

        [NonSerialized]
        readonly internal ParticleAuxillary Aux = new ParticleAuxillary();
        [NonSerialized]
        readonly private ParticleForceIntegration ForceIntegration = new ParticleForceIntegration();
        [NonSerialized]
        readonly private ParticleAddedDamping AddedDamping = new ParticleAddedDamping();
        [NonSerialized]
        readonly private ParticleUnderrelaxation Underrelaxation = new ParticleUnderrelaxation();
        [NonSerialized]
        readonly private ParticleAcceleration Acceleration = new ParticleAcceleration();
        internal void UpdateParticleVelocity(double dt)
        {
            CalculateTranslationalVelocity(dt);
            CalculateAngularVelocity(dt);
        }

        internal void UpdateParticlePositionAndAngle(double dt)
        {
            CalculateParticlePosition(dt);
            CalculateParticleAngle(dt);
            CollisionTimestep = 0;
        }

        /// <summary>
        /// Calculate the new particle position
        /// </summary>
        /// <param name="dt"></param>
        public void CalculateParticlePosition(double dt)
        {
            if (iteration_counter_P == 0)
            {
                Aux.SaveMultidimValueOfLastTimestep(Position);
            }

            if (SpatialDim != 2 && SpatialDim != 3)
                throw new NotSupportedException("Unknown particle dimension: SpatialDim = " + SpatialDim);

            int ClearAcceleartion = CollisionTimestep != 0 ? 0 : 1;
            if (IncludeTranslation == true) {
                for (int d = 0; d < SpatialDim; d++)
                {
                    Position[0][d] = Position[1][d] + (ClearAcceleartion * TranslationalVelocity[1][d] + TranslationalVelocity[0][d]) * (dt - CollisionTimestep) / 2 + ClearAcceleartion * (TranslationalAcceleration[1][d] + TranslationalAcceleration[0][d]) * (dt - CollisionTimestep).Pow2() / 4;
                    if (double.IsNaN(Position[0][d]) || double.IsInfinity(Position[0][d]))
                        throw new ArithmeticException("Error trying to update particle position. Value:  " + Position[0][d]);
                }
            }
            else
            {
                for (int d = 0; d < SpatialDim; d++)
                {
                    Position[0][d] = Position[1][d];
                    TranslationalAcceleration[0][d] = 0;
                    TranslationalVelocity[0][d] = 0;
                    //Assert.LessOrEqual(TranslationalVelocity[1][d].Abs(), 0, "Non-zero velocity for stationary particle");
                    //Assert.LessOrEqual(TranslationalAcceleration[1][d].Abs(), 0, "Non-zero acceleration for stationary particle");
                    //Assert.LessOrEqual(TranslationalAcceleration[0][d].Abs(), 0, "Non-zero acceleration for stationary particle");// does not work together with collison models
                }
            }
        }

        /// <summary>
        /// Calculate the new particle angle
        /// </summary>
        /// <param name="dt"></param>
        public void CalculateParticleAngle(double dt)
        {
            if (iteration_counter_P == 0)
            {
                Aux.SaveValueOfLastTimestep(Angle);
            }
            int ClearAcceleartion = CollisionTimestep != 0 ? 0 : 1;
            if (IncludeRotation == true) {
                if (SpatialDim != 2)
                    throw new NotSupportedException("Unknown particle dimension: SpatialDim = " + SpatialDim);

                Angle[0] = Angle[1] + (RotationalVelocity[1] + RotationalVelocity[0]) * (dt - CollisionTimestep) / 2 + ClearAcceleartion * (dt - CollisionTimestep).Pow2() * (RotationalAcceleration[1] + RotationalAcceleration[0]) / 4;
                //for (int p = 0; p < m_collidedWithParticle.Length; p++)
                //{
                //    if (m_collidedWithParticle[p])
                //    {
                //        Angle[0] = Angle[1] + dt * (RotationalVelocity[1] + RotationalVelocity[0]) / 2;
                //        m_collidedWithParticle[p] = false;
                //    }
                //}
                if (double.IsNaN(Angle[0]) || double.IsInfinity(Angle[0]))
                    throw new ArithmeticException("Error trying to update particle angle. Value:  " + Angle[0]);
            } else {
                Angle[0] = Angle[1];
                RotationalAcceleration[0] = 0;
                RotationalVelocity[0] = 0;
                //Assert.LessOrEqual(RotationalVelocity[1].Abs(), 0, "Non-zero rotational acceleration for non-rotating particle");
                //Assert.LessOrEqual(RotationalAcceleration[1].Abs(), 0, "Non-zero rotational acceleration for non-rotating particle");
                //Assert.LessOrEqual(RotationalAcceleration[0] .Abs(), 0, "Non-zero rotational acceleration for non-rotating particle");
            }
        }

        /// <summary>
        /// Calculate the new acceleration (translational and rotational)
        /// </summary>
        /// <param name="dt"></param>
        public void PredictAcceleration()
        {
            if (iteration_counter_P == 0)
            {
                Aux.SaveMultidimValueOfLastTimestep(TranslationalAcceleration);
                Aux.SaveValueOfLastTimestep(RotationalAcceleration);
                Aux.SaveMultidimValueOfLastTimestep(HydrodynamicForces);
                Aux.SaveValueOfLastTimestep(HydrodynamicTorque);
            }
            for (int d = 0; d < SpatialDim; d++)
            {
                //TranslationalAcceleration[0][d] = 2 * TranslationalAcceleration[1][d] - TranslationalAcceleration[2][d];
                TranslationalAcceleration[0][d] = (TranslationalAcceleration[1][d] + 4 * TranslationalAcceleration[2][d] + TranslationalAcceleration[3][d]) / 8;
                //HydrodynamicForces[0][d] = 2 * HydrodynamicForces[1][d] - HydrodynamicForces[2][d];
                if (Math.Abs(TranslationalAcceleration[0][d]) < 1e-20)// || double.IsNaN(TranslationalAcceleration[0][d]))
                    TranslationalAcceleration[0][d] = 0;
            }
            TranslationalAcceleration.MPIBroadcast(0);
            //RotationalAcceleration[0] = 2 * RotationalAcceleration[1] - RotationalAcceleration[2];
            RotationalAcceleration[0] = (RotationalAcceleration[1] + 4 * RotationalAcceleration[2] + RotationalAcceleration[3]) / 8;
            //HydrodynamicTorque[0] = 2 * HydrodynamicTorque[1] - HydrodynamicTorque[2];
            if (Math.Abs(RotationalAcceleration[0]) < 1e-20)// || double.IsNaN(RotationalAcceleration[0]))
                RotationalAcceleration[0] = 0;
            RotationalAcceleration.MPIBroadcast(0);
        }

        /// <summary>
        /// Calculate the new acceleration (translational and rotational)
        /// </summary>
        /// <param name="dt"></param>
        public void CalculateAcceleration(double dt, bool FullyCoupled, bool IncludeHydrodynamics)
        {
            if (iteration_counter_P == 0 || FullyCoupled == false)
            {
                Aux.SaveMultidimValueOfLastTimestep(TranslationalAcceleration);
                Aux.SaveValueOfLastTimestep(RotationalAcceleration);
            }
            // Include Gravitiy
            if (!Collided && !IncludeHydrodynamics)
            {
                HydrodynamicForces[0][1] += GravityVertical * Mass_P;
            }
            double[,] CoefficientMatrix = Acceleration.CalculateCoefficients(AddedDampingTensor, Mass_P, MomentOfInertia_P, dt, AddedDampingCoefficient);
            double Denominator = Acceleration.CalculateDenominator(CoefficientMatrix);

            if (IncludeTranslation) { }
                TranslationalAcceleration[0] = Acceleration.Translational(CoefficientMatrix, Denominator, HydrodynamicForces[0], HydrodynamicTorque[0]);

            for (int d = 0; d < SpatialDim; d++)
            {
                if (Math.Abs(TranslationalAcceleration[0][d]) < 1e-20 || IncludeTranslation == false)
                    TranslationalAcceleration[0][d] = 0;
            }

            TranslationalAcceleration.MPIBroadcast(0);
            if (IncludeRotation)
                RotationalAcceleration[0] = Acceleration.Rotational(CoefficientMatrix, Denominator, HydrodynamicForces[0], HydrodynamicTorque[0]);
            if (Math.Abs(RotationalAcceleration[0]) < 1e-20 || IncludeRotation == false)
                RotationalAcceleration[0] = 0;
            RotationalAcceleration.MPIBroadcast(0);
        }

        /// <summary>
        /// Calculate the new translational velocity of the particle using a Crank Nicolson scheme.
        /// </summary>
        /// <param name="dt">Timestep</param>
        /// <returns></returns>
        public void CalculateTranslationalVelocity(double dt)
        {
            if (iteration_counter_P == 0)
            {
                Aux.SaveMultidimValueOfLastTimestep(TranslationalVelocity);
            }

            double[] tempActiveVelcotiy = new double[2];
            

            if (this.IncludeTranslation == false) {
                for (int d = 0; d < SpatialDim; d++) {
                    TranslationalVelocity[0][d] = 0;
                }
            }
            else if (ActiveVelocity != 0)
            {
                tempActiveVelcotiy[0] = Math.Cos(Angle[0]) * ActiveVelocity;
                tempActiveVelcotiy[1] = Math.Sin(Angle[0]) * ActiveVelocity;
                for (int d = 0; d < SpatialDim; d++)
                {
                    if (!Collided)
                        TranslationalVelocity[0][d] = tempActiveVelcotiy[d];
                    if (double.IsNaN(TranslationalVelocity[0][d]) || double.IsInfinity(TranslationalVelocity[0][d]))
                        throw new ArithmeticException("Error trying to calculate particle velocity Value:  " + TranslationalVelocity[0][d]);
                }
            }
            else {

                for (int d = 0; d < SpatialDim; d++) {
                    
                    if (!Collided)
                        TranslationalVelocity[0][d] = TranslationalVelocity[1][d] + (TranslationalAcceleration[1][d] + TranslationalAcceleration[0][d]) * dt / 2;
                    //else
                    //    TranslationalVelocity[0][d] = TranslationalVelocity[1][d];
                    if (double.IsNaN(TranslationalVelocity[0][d]) || double.IsInfinity(TranslationalVelocity[0][d]))
                        throw new ArithmeticException("Error trying to calculate particle velocity Value:  " + TranslationalVelocity[0][d]);
                }
            }
        }

        /// <summary>
        /// Calculate the new angular velocity of the particle using explicit Euler scheme.
        /// </summary>
        /// <param name="dt">Timestep</param>
        /// <returns></returns>
        public void CalculateAngularVelocity(double dt)
        {
            if (iteration_counter_P == 0)
            {
                Aux.SaveValueOfLastTimestep(RotationalVelocity);
            }

            if (this.IncludeRotation == false) {
                RotationalVelocity[0] = 0;
                return;
            } else {
                RotationalVelocity[0] = RotationalVelocity[1] + dt * (RotationalAcceleration[1] + RotationalAcceleration[0]) / 2;
                if (double.IsNaN(RotationalVelocity[0]) || double.IsInfinity(RotationalVelocity[0]))
                    throw new ArithmeticException("Error trying to calculate particle angluar velocity. Value:  " + RotationalVelocity[0]);
            }
            RotationalVelocity.MPIBroadcast(0);
        }
        
        /// <summary>
        /// clone, not implemented
        /// </summary>
        virtual public object Clone() {
            throw new NotImplementedException("Currently cloning of a particle is not available");
        }

        /// <summary>
        /// Calculate tensors to implement the added damping model (Banks et.al. 2017)
        /// </summary>
        public void CalculateDampingTensor(LevelSetTracker LsTrk, double muA, double rhoA, double dt)
        {
            AddedDampingTensor = AddedDamping.IntegrationOverLevelSet(LsTrk, muA, rhoA, dt, Position[0]);
        }

        /// <summary>
        /// Update in every timestep tensors to implement the added damping model (Banks et.al. 2017)
        /// </summary>
        public void UpdateDampingTensors()
        {
            AddedDampingTensor = AddedDamping.RotateTensor(Angle[0], StartingAngle, AddedDampingTensor);
        }
        
        /// <summary>
        /// Update Forces and Torque acting from fluid onto the particle
        /// </summary>
        /// <param name="U"></param>
        /// <param name="P"></param>
        /// <param name="LsTrk"></param>
        /// <param name="muA"></param>
        public void UpdateForcesAndTorque(VectorField<SinglePhaseField> U, SinglePhaseField P, LevelSetTracker LsTrk, double muA, double dt, double fluidDensity, bool NotFullyCoupled) {

            if (skipForceIntegration) {
                skipForceIntegration = false;
                return;
            }
            HydrodynamicForces[0][0] = 0;
            HydrodynamicForces[0][1] = 0;
            HydrodynamicTorque[0] = 0;
            int RequiredOrder = U[0].Basis.Degree * 3 + 2;
            Console.WriteLine("Forces coeff: {0}, order = {1}", LsTrk.CutCellQuadratureType, RequiredOrder);
            double[] Forces = new double[SpatialDim];
            SinglePhaseField[] UA = U.ToArray();
            ConventionalDGField pA = null;
            pA = P;
            if (IncludeTranslation)
            {
                for (int d = 0; d < SpatialDim; d++)
                {
                    void ErrFunc(int CurrentCellID, int Length, NodeSet Ns, MultidimensionalArray result)
                    {

                        int NumberOfNodes = result.GetLength(1);
                        MultidimensionalArray Grad_UARes = MultidimensionalArray.Create(Length, NumberOfNodes, SpatialDim, SpatialDim);
                        MultidimensionalArray pARes = MultidimensionalArray.Create(Length, NumberOfNodes);
                        var Normals = LsTrk.DataHistories[0].Current.GetLevelSetNormals(Ns, CurrentCellID, Length);
                        for (int i = 0; i < SpatialDim; i++)
                        {
                            UA[i].EvaluateGradient(CurrentCellID, Length, Ns, Grad_UARes.ExtractSubArrayShallow(-1, -1, i, -1), 0, 1);
                        }
                        pA.Evaluate(CurrentCellID, Length, Ns, pARes);
                        for (int j = 0; j < Length; j++)
                        {
                            for (int k = 0; k < NumberOfNodes; k++)
                            {
                                result[j, k] = ForceIntegration.CalculateStressTensor(Grad_UARes, pARes, Normals, muA, k, j, this.SpatialDim, d);
                            }
                        }
                    }
                    var SchemeHelper = LsTrk.GetXDGSpaceMetrics(new[] { LsTrk.GetSpeciesId("A") }, RequiredOrder, 1).XQuadSchemeHelper;
                    CellQuadratureScheme cqs = SchemeHelper.GetLevelSetquadScheme(0, CutCells_P(LsTrk));
                    CellQuadrature.GetQuadrature(new int[] { 1 }, LsTrk.GridDat,
                        cqs.Compile(LsTrk.GridDat, RequiredOrder),
                        delegate (int i0, int Length, QuadRule QR, MultidimensionalArray EvalResult)
                        {
                            ErrFunc(i0, Length, QR.Nodes, EvalResult.ExtractSubArrayShallow(-1, -1, 0));
                        },
                        delegate (int i0, int Length, MultidimensionalArray ResultsOfIntegration)
                        {
                            Forces[d] = ParticleAuxillary.ForceTorqueSummationWithNeumaierArray(Forces[d], ResultsOfIntegration, Length);
                        }
                    ).Execute();
                }
            }

            double Torque = 0;
            if (IncludeRotation)
            {
                void ErrFunc2(int j0, int Len, NodeSet Ns, MultidimensionalArray result)
                {
                    int K = result.GetLength(1); // No nof Nodes
                    MultidimensionalArray Grad_UARes = MultidimensionalArray.Create(Len, K, SpatialDim, SpatialDim); ;
                    MultidimensionalArray pARes = MultidimensionalArray.Create(Len, K);
                    // Evaluate tangential velocity to level-set surface
                    var Normals = LsTrk.DataHistories[0].Current.GetLevelSetNormals(Ns, j0, Len);
                    for (int i = 0; i < SpatialDim; i++)
                    {
                        UA[i].EvaluateGradient(j0, Len, Ns, Grad_UARes.ExtractSubArrayShallow(-1, -1, i, -1), 0, 1);
                    }
                    pA.Evaluate(j0, Len, Ns, pARes);
                    for (int j = 0; j < Len; j++)
                    {
                        MultidimensionalArray tempArray = Ns.CloneAs();
                        LsTrk.GridDat.TransformLocal2Global(Ns, tempArray, j0 + j);
                        for (int k = 0; k < K; k++)
                        {
                            result[j, k] = ForceIntegration.CalculateTorqueFromStressTensor2D(Grad_UARes, pARes, Normals, tempArray, muA, k, j, Position[0]);
                        }
                    }
                }
                var SchemeHelper2 = LsTrk.GetXDGSpaceMetrics(new[] { LsTrk.GetSpeciesId("A") }, RequiredOrder, 1).XQuadSchemeHelper;
                CellQuadratureScheme cqs2 = SchemeHelper2.GetLevelSetquadScheme(0, CutCells_P(LsTrk));
                CellQuadrature.GetQuadrature(new int[] { 1 }, LsTrk.GridDat,
                    cqs2.Compile(LsTrk.GridDat, RequiredOrder),
                    delegate (int i0, int Length, QuadRule QR, MultidimensionalArray EvalResult)
                    {
                        ErrFunc2(i0, Length, QR.Nodes, EvalResult.ExtractSubArrayShallow(-1, -1, 0));
                    },
                    delegate (int i0, int Length, MultidimensionalArray ResultsOfIntegration)
                    {
                        Torque = ParticleAuxillary.ForceTorqueSummationWithNeumaierArray(Torque, ResultsOfIntegration, Length);
                    }
                ).Execute();
            }
            // add gravity
            {
                Forces[1] += (particleDensity - fluidDensity) * Area_P * GravityVertical;
            }
            // Sum forces and moments over all MPI processors
            // ==============================================
            {
                int NoOfVars = 1 + SpatialDim;
                double[] StateBuffer = new double[NoOfVars];
                StateBuffer[0] = Torque;
                for (int d = 0; d < SpatialDim; d++)
                {
                    StateBuffer[1 + d] = Forces[d];
                }
                double[] GlobalStateBuffer = StateBuffer.MPISum();
                Torque = GlobalStateBuffer[0];
                for (int d = 0; d < SpatialDim; d++)
                {
                    Forces[d] = GlobalStateBuffer[1 + d];
                }
            }
            if (neglectAddedDamping == false)
            {
                double fest = Forces[0];
                Forces[0] = Forces[0] + AddedDampingCoefficient * dt * (AddedDampingTensor[0, 0] * TranslationalAcceleration[0][0] + AddedDampingTensor[1, 0] * TranslationalAcceleration[0][1] + AddedDampingTensor[0, 2] * RotationalAcceleration[0]);
                double test = AddedDampingCoefficient * dt * (AddedDampingTensor[0, 0] * TranslationalAcceleration[0][0] + AddedDampingTensor[1, 0] * TranslationalAcceleration[0][1] + AddedDampingTensor[0, 2] * RotationalAcceleration[0]);
                Forces[1] = Forces[1] + AddedDampingCoefficient * dt * (AddedDampingTensor[0, 1] * TranslationalAcceleration[0][0] + AddedDampingTensor[1, 1] * TranslationalAcceleration[0][1] + AddedDampingTensor[1, 2] * RotationalAcceleration[0]);
                Torque += AddedDampingCoefficient * dt * (AddedDampingTensor[2, 0] * TranslationalAcceleration[0][0] + AddedDampingTensor[2, 1] * TranslationalAcceleration[0][1] + AddedDampingTensor[2, 2] * RotationalAcceleration[0]);
            }

            if (iteration_counter_P == 1 || NotFullyCoupled || iteration_counter_P == 250)
            {
                Console.WriteLine();
                if(iteration_counter_P == 1)
                    Console.WriteLine("First iteration of the current timestep, all relaxation factors are set to 1");
                if (iteration_counter_P == 250)
                    Console.WriteLine("250 iterations, I'm trying to jump closer to the real solution");
                for (int d = 0; d < SpatialDim; d++)
                {
                    HydrodynamicForces[0][d] = 0;
                    if (Math.Abs(Forces[d]) < ForceAndTorque_convergence * 1e-2 && ClearSmallValues == true)
                    {
                        Forces[d] = 0;
                    }
                    HydrodynamicForces[0][d] = Forces[d];
                }
                HydrodynamicTorque[0] = 0;
                if (Math.Abs(Torque) < ForceAndTorque_convergence * 1e-2 && ClearSmallValues == true)
                {
                    Torque = 0;
                }
                HydrodynamicTorque[0] = Torque;
            }
            else
            {
                double[] RelaxatedForceAndTorque = Underrelaxation.RelaxatedForcesAndTorque(Forces, Torque, ForcesPrevIteration, TorquePrevIteration, ForceAndTorque_convergence, underrelaxation_factor, ClearSmallValues, AddaptiveUnderrelaxation, AverageDistance, iteration_counter_P);
                for (int d = 0; d < SpatialDim; d++)
                {
                    HydrodynamicForces[0][d] = RelaxatedForceAndTorque[d];
                }
                HydrodynamicTorque[0] = RelaxatedForceAndTorque[SpatialDim];
            }
            //for (int d = 0; d < SpatialDim; d++)// changes sign depending on the sign of Forces[d], should increase the convergence rate. (testing needed)
            //{
            //    if (Math.Abs(HydrodynamicForces[0][d] - Forces[0]) > Math.Abs(Forces[d]))
            //    {
            //        HydrodynamicForces[0][d] *= -1;
            //    }
            //}
            if (double.IsNaN(HydrodynamicForces[0][0]) || double.IsInfinity(HydrodynamicForces[0][0]))
                throw new ArithmeticException("Error trying to calculate hydrodynamic forces (x). Value:  " + HydrodynamicForces[0][0]);
            if (double.IsNaN(HydrodynamicForces[0][1]) || double.IsInfinity(HydrodynamicForces[0][1]))
                throw new ArithmeticException("Error trying to calculate hydrodynamic forces (y). Value:  " + HydrodynamicForces[0][1]);
            if (double.IsNaN(HydrodynamicTorque[0]) || double.IsInfinity(HydrodynamicTorque[0]))
                throw new ArithmeticException("Error trying to calculate hydrodynamic torque. Value:  " + HydrodynamicTorque[0]);
        }

        

        public double[] CalculateParticleMomentum()
        {
            double[] temp = new double[SpatialDim + 1];
            for (int d = 0; d < SpatialDim; d++)
            {
                temp[d] = Mass_P * TranslationalVelocity[0][d];
            }
            temp[SpatialDim] = MomentOfInertia_P * RotationalVelocity[0];
            return temp;
        }

        public double[] CalculateParticleKineticEnergy()
        {
            double[] temp = new double[SpatialDim + 1];
            for (int d = 0; d < SpatialDim; d++)
            {
                temp[d] = 0.5 * Mass_P * TranslationalVelocity[0][d].Pow2();
            }
            temp[SpatialDim] = 0.5 * MomentOfInertia_P * RotationalVelocity[0].Pow2();
            return temp;
        }
        
        /// <summary>
        /// Calculating the particle reynolds number
        /// </summary>
        public double ComputeParticleRe(double ViscosityFluid)
        {
            return Math.Sqrt(TranslationalVelocity[0][0] * TranslationalVelocity[0][0] + TranslationalVelocity[0][1] * TranslationalVelocity[0][1]) * GetLengthScales().Max() / ViscosityFluid;
        }

        /// <summary>
        /// get cut cells describing the boundary of this particle
        /// </summary>
        /// <param name="LsTrk"></param>
        /// <returns></returns>
        public CellMask CutCells_P(LevelSetTracker LsTrk)
        {
            BitArray CellArray = new BitArray(LsTrk.GridDat.Cells.NoOfLocalUpdatedCells);
            MultidimensionalArray CellCenters = LsTrk.GridDat.Cells.CellCenter;
            double h_min = LsTrk.GridDat.Cells.h_minGlobal;
            double h_max = LsTrk.GridDat.Cells.h_maxGlobal;
            for (int i = 0; i < CellArray.Length; i++)
            {
                CellArray[i] = Contains(new double[] { CellCenters[i, 0], CellCenters[i, 1] }, h_min, h_max, false);
            }
            CellMask CutCells = new CellMask(LsTrk.GridDat, CellArray, MaskType.Logical);
            CutCells = CutCells.Intersect(LsTrk.Regions.GetCutCellMask());
            return CutCells;
        }

        /// <summary>
        /// Gives a bool whether the particle contains a certain point or not
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public abstract bool Contains(double[] point, double h_min, double h_max = 0, bool WithoutTolerance = false);

        virtual public double[] GetLengthScales()
        {
            throw new NotImplementedException();
        }

        virtual public MultidimensionalArray GetSurfacePoints(LevelSetTracker lsTrk, double[] Position, double Angle)
        {
            throw new NotImplementedException();
        }

        virtual public void GetSupportPoint(int SpatialDim, double[] Vector, double[] Position, double Angle, out double[] SupportPoint)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Set true if translation of the particle should be induced by hydrodynamical forces.
        /// </summary>
        [DataMember]
        public bool IncludeTranslation = true;

        /// <summary>
        /// Set true if rotation of the particle should be induced by hydrodynamical torque.
        /// </summary>
        [DataMember]
        public bool IncludeRotation = true;
    }
}

