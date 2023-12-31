﻿using BoSSS.Foundation.XDG;
using BoSSS.Foundation.XDG.OperatorFactory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZwoLevelSetSolver.ContactLine;

namespace ZwoLevelSetSolver.Boundary {
    class LinearDisplacementBoundary : SurfaceEquation {
        string fluidSpecies;
        string solidSpecies;
        string codomainName;

        public LinearDisplacementBoundary(LevelSetTracker LsTrkr, string fluidSpecies, string solidSpecies, int d, int D, double artificialViscosity) {
            codomainName = ZwoLevelSetSolver.EquationNames.DisplacementEvolutionComponent(d);
            this.fluidSpecies = fluidSpecies;
            this.solidSpecies = solidSpecies;
            //Stress equality
            AddVariableNames(ZwoLevelSetSolver.VariableNames.DisplacementVector(D));
            AddComponent(new SolidInterfaceLinearTransportForm(ZwoLevelSetSolver.VariableNames.DisplacementVector(D), 1.0, d, D, 1, fluidSpecies, solidSpecies));
            AddParameter(BoSSS.Solution.NSECommon.VariableNames.Velocity0Vector(D));
            AddParameter(BoSSS.Solution.NSECommon.VariableNames.Velocity0MeanVector(D));

            AddComponent(new SolidTensionForm(fluidSpecies, solidSpecies, ZwoLevelSetSolver.VariableNames.DisplacementVector(D), d, D, 1, artificialViscosity));

            AddSurfaceComponent(new BoundaryViscosityForm(ZwoLevelSetSolver.VariableNames.DisplacementVector(D), d, D, 0.000));
            AddParameter(BoSSS.Solution.NSECommon.VariableNames.AsLevelSetVariable(
                ZwoLevelSetSolver.VariableNames.SolidLevelSetCG, BoSSS.Solution.NSECommon.VariableNames.NormalVector(D)).ToArray());
        }

        public override string FirstSpeciesName => fluidSpecies;

        public override string SecondSpeciesName => solidSpecies;

        public override string CodomainName => codomainName;
    }
}
