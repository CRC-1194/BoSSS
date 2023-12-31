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

using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.Quadrature;
using BoSSS.Platform.LinAlg;
using BoSSS.Solution.CompressibleFlowCommon;
using BoSSS.Solution.Utils;
using CNS.EquationSystem;
using CNS.IBM;
using CNS.ShockCapturing;
using ilPSP;
using ilPSP.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CNS {

    /// <summary>
    /// Container for all fields representing the current state of the
    /// variables of the compressible Navier-stokes equations.
    /// </summary>
    public class CNSFieldSet : ICloneable {

        /// <summary>
        /// The omnipresent context;
        /// </summary>
        protected IGridData gridData;

        /// <summary>
        /// <see cref="CNSControl"/>
        /// </summary>
        protected internal CNSControl config;

        /// <summary>
        /// Fields representing the derivatives of the primal variables
        /// </summary>
        /// <summary>
        /// The density $\rho$
        /// </summary>
        public readonly DGField Density;

        /// <summary>
        /// The momentum field $\rho \vec{u}$
        /// </summary>
        public readonly VectorField<DGField> Momentum;

        /// <summary>
        /// The total energy per volume $\rho E$
        /// </summary>
        public readonly DGField Energy;

        /// <summary>
        /// Private constructor for cloning purposes
        /// </summary>
        /// <param name="gridData">The omnipresent grid data</param>
        /// <param name="config">Configurations options</param>
        /// <param name="template">The object to be cloned</param>
        protected CNSFieldSet(IGridData gridData, CNSControl config, CNSFieldSet template) {
            this.gridData = gridData;
            this.config = config;

            Density = template.Density.CloneAs();
            DGField[] momentumComponents = new DGField[CompressibleEnvironment.NumberOfDimensions];
            for (int d = 0; d < CompressibleEnvironment.NumberOfDimensions; d++) {
                momentumComponents[d] = template.Momentum[d].CloneAs();
            }
            Momentum = new VectorField<DGField>(momentumComponents);
            Energy = template.Energy.CloneAs();

            DerivedFields = new Dictionary<DerivedVariable, DGField>();
            foreach (var variableFieldPair in template.DerivedFields) {
                DerivedFields.Add(variableFieldPair.Key, variableFieldPair.Value.CloneAs());
            }
        }

        /// <summary>
        /// Uses information from <paramref name="config"/> to create
        /// <see cref="SinglePhaseField"/>s for <see cref="Density"/>,
        /// <see cref="Momentum"/> and <see cref="Energy"/>.
        /// </summary>
        /// <param name="gridData">The omnipresent grid data</param>
        /// <param name="config">CNS specific control options</param>
        public CNSFieldSet(IGridData gridData, CNSControl config) {
            this.gridData = gridData;
            this.config = config;

            int numberOfDimensions = CompressibleEnvironment.NumberOfDimensions;

            SinglePhaseField[] momentumFields = new SinglePhaseField[numberOfDimensions];
            Basis momentumBasis = new Basis(gridData, config.MomentumDegree);

            // Mandatory fields
            Density = new SinglePhaseField(
                new Basis(gridData, config.DensityDegree),
                CompressibleVariables.Density);

            for (int d = 0; d < numberOfDimensions; d++) {
                string variableName = CompressibleVariables.Momentum[d];
                momentumFields[d] = new SinglePhaseField(momentumBasis, variableName);
            }
            Momentum = new VectorField<DGField>(momentumFields);

            Energy = new SinglePhaseField(
                new Basis(gridData, config.EnergyDegree), CompressibleVariables.Energy);

            // Derived fields
            foreach (var fieldConfig in config.VariableToDegreeMap) {
                DerivedVariable key = fieldConfig.Key as DerivedVariable;
                if (key == null) {
                    continue;
                }

                SinglePhaseField field = new SinglePhaseField(
                    new Basis(gridData, fieldConfig.Value), key);
                DerivedFields.Add(key, field);
            }
        }

        /// <summary>
        /// Vector representation of <see cref="Density"/>,
        /// <see cref="Momentum"/> and <see cref="Energy"/>.
        /// </summary>
        public DGField[] ConservativeVariables {
            get {
                DGField[] fields = new DGField[CompressibleEnvironment.NumberOfDimensions + 2];

                fields[CompressibleEnvironment.PrimalArgumentToIndexMap[CompressibleVariables.Density]] = Density;
                for (int d = 0; d < CompressibleEnvironment.NumberOfDimensions; d++) {
                    fields[CompressibleEnvironment.PrimalArgumentToIndexMap[CompressibleVariables.Momentum[d]]] = Momentum[d];
                }
                fields[CompressibleEnvironment.PrimalArgumentToIndexMap[CompressibleVariables.Energy]] = Energy;

                return fields;
            }
        }

        /// <summary>
        /// Optional fields required as parameters for spatial differential
        /// operators
        /// </summary>
        public virtual DGField[] ParameterFields {
            get {
                if (this.config.ActiveOperators.HasFlag(Operators.ArtificialViscosity)) {
                    return new DGField[] { DerivedFields[CNSVariables.ArtificialViscosity] };
                } else {
                    return new DGField[0];
                }
            }
        }

        /// <summary>
        /// Returns a list of all derived fields that are requested in the
        /// control file
        /// </summary>
        public Dictionary<DerivedVariable, DGField> DerivedFields = new Dictionary<DerivedVariable, DGField>();

        /// <summary>
        /// Union of <see cref="ConservativeVariables"/> and <see cref="DerivedFields"/>
        /// </summary>
        public virtual IEnumerable<DGField> AllFields {
            get {
                return ConservativeVariables.Union(ParameterFields).Union(DerivedFields.Values);
            }
        }

        /// <summary>
        /// Projects the given <paramref name="initialValues"/> onto the
        /// conservative variable fields <see cref="Density"/>,
        /// <see cref="Momentum"/> and <see cref="Energy"/>. Initial values
        /// may either be given in conservative (see
        /// <see cref="VariableTypes.ConservativeVariables"/>) or primitive
        /// (see <see cref="VariableTypes.PrimitiveVariables"/>) variables
        /// </summary>
        /// <param name="speciesMap"></param>
        /// <param name="initialValues">
        /// The given initial value functions, where the dictionary keys
        /// represent variable names. 
        /// </param>
        public virtual void ProjectInitialValues(ISpeciesMap speciesMap, IDictionary<string, Func<double[], double>> initialValues) {
            int numberOfDimensions = CompressibleEnvironment.NumberOfDimensions;
            CellQuadratureScheme scheme = new CellQuadratureScheme(true, speciesMap.SubGrid.VolumeMask);

            if (config.GetInitialValueVariables() == VariableTypes.ConservativeVariables) {
                Density.ProjectField(1.0, initialValues[CompressibleVariables.Density], scheme);

                for (int d = 0; d < numberOfDimensions; d++) {
                    Momentum[d].ProjectField(1.0, initialValues[CompressibleVariables.Momentum[d]], scheme);
                }

                Energy.ProjectField(1.0, initialValues[CompressibleVariables.Energy], scheme);
            } else if (config.GetInitialValueVariables() == VariableTypes.PrimitiveVariables) {
                var densityFunction = initialValues[CompressibleVariables.Density];
                Density.ProjectField(1.0, densityFunction, scheme);

                Func<double[], double>[] velocityFunctions = new Func<double[], double>[numberOfDimensions];
                for (int d = 0; d < numberOfDimensions; d++) {
                    velocityFunctions[d] = initialValues[CNSVariables.Velocity[d]];
                    Momentum[d].ProjectField(1.0, X => densityFunction(X) * velocityFunctions[d](X), scheme);
                }

                var pressureFunction = initialValues[CNSVariables.Pressure];
                Energy.ProjectField(
                    1.0,
                    delegate (double[] X) {
                        double rho = densityFunction(X);
                        double p = pressureFunction(X);

                        Vector u = new Vector(numberOfDimensions);
                        for (int d = 0; d < numberOfDimensions; d++) {
                            u[d] = velocityFunctions[d](X);
                        }

                        StateVector state = StateVector.FromPrimitiveQuantities(
                            speciesMap.GetMaterial(double.NaN), rho, u, p);
                        return state.Energy;
                    },
                    scheme);
            } else {
                throw new ArgumentException(
                    "Please specify initial values either in primitive or in conservative variables");
            }
        }

        /// <summary>
        /// Updates all derived variables within
        /// <see cref="CNSFieldSet.DerivedFields"/> using their corresponding
        /// update function <see cref="DerivedVariable.UpdateFunction"/>
        /// </summary>
        public void UpdateDerivedVariables(IProgram<CNSControl> program, CellMask cellMask) {
            using (var tr = new FuncTrace()) {

                program.Control.CNSShockSensor?.UpdateSensorValues(program.WorkingSet.AllFields, program.SpeciesMap, cellMask);
                foreach (var pair in DerivedFields) {
                    pair.Key.UpdateFunction(pair.Value, cellMask, program);
                }
            }
        }

        /// <summary>
        /// Updates the sensor value
        /// <see cref="BoSSS.Solution.CompressibleFlowCommon.ShockCapturing.IShockSensor.UpdateSensorValues(IEnumerable{DGField}, ISpeciesMap, CellMask)"/>
        /// and the artificial viscosity value <see cref="Variables.ArtificialViscosity"/> in every cell
        /// </summary>
        /// <param name="program"></param>
        /// <param name="cellMask"></param>
        public void UpdateShockCapturingVariables(IProgram<CNSControl> program, CellMask cellMask) {
            using (var tr = new FuncTrace()) {
                // Update sensor
                program.Control.CNSShockSensor.UpdateSensorValues(program.WorkingSet.AllFields, program.SpeciesMap, cellMask);

                // Update sensor variable (not necessary as only needed for IO)
                using (new BlockTrace("UpdateShockCapturingVariables.Sensor_Plot", tr)) {
                    var sensorField = program.WorkingSet.DerivedFields[CNSVariables.ShockSensor];
                    CNSVariables.ShockSensor.UpdateFunction(sensorField, program.SpeciesMap.SubGrid.VolumeMask, program);
                }

                // Update artificial viscosity variable
                var avField = program.WorkingSet.DerivedFields[CNSVariables.ArtificialViscosity];
                CNSVariables.ArtificialViscosity.UpdateFunction(avField, program.SpeciesMap.SubGrid.VolumeMask, program);
            }
        }

        #region ICloneable Members

        /// <summary>
        /// Creates a clone using non-shallow copy of all members
        /// </summary>
        /// <returns>A new, independent field set</returns>
        public virtual object Clone() {
            return new CNSFieldSet(gridData, config, this);
        }

        #endregion
    }
}
