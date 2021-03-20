﻿using BoSSS.Foundation.XDG;
using BoSSS.Solution.NSECommon;
using ilPSP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoSSS.Solution.XNSECommon {
    /// <summary>
    /// 
    /// </summary>
    public class LowMach_Gravity : BuoyancyJacobi, ISpeciesFilter {
        public LowMach_Gravity(string spcsName, Vector GravityDirection, int SpatialComponent, double Froude, PhysicsMode physicsMode, MaterialLaw EoS, int noOfChemComponents) : base(GravityDirection, SpatialComponent, Froude, physicsMode, EoS, noOfChemComponents) {
            ValidSpecies = spcsName;
        }

        public string ValidSpecies {
            get;
            private set;
        }
    }


    /// <summary>
    /// Heat release term on energy equation
    /// </summary>
    public class LowMach_HeatSource : ReactionHeatSourceJacobi, ISpeciesFilter {
        public LowMach_HeatSource(string spcName, double HeatReleaseFactor, double[] ReactionRateConstants, double[] molarmasses, MaterialLaw EoS, double TRef, double cpRef, bool VariableOneStepParameters) : base(HeatReleaseFactor, ReactionRateConstants, molarmasses, EoS, TRef, cpRef, VariableOneStepParameters) {
            ValidSpecies = spcName;
        }

        public string ValidSpecies {
            get;
            private set;
        }
    }



    /// <summary>
    /// Reaction term on mass fraction equation
    /// </summary>
    public class LowMach_MassFractionSource : ReactionSpeciesSourceJacobi, ISpeciesFilter {
        public LowMach_MassFractionSource(string spcName, double[] ReactionRateConstants, double[] StoichiometricCoefficients, double[] MolarMasses, MaterialLaw EoS, int NumberOfReactants, int SpeciesIndex, double TRef, double cpRef, bool VariableOneStepParameters) : base(ReactionRateConstants, StoichiometricCoefficients, MolarMasses, EoS, NumberOfReactants, SpeciesIndex, TRef, cpRef, VariableOneStepParameters) {

            ValidSpecies = spcName;
        }

        public string ValidSpecies {
            get;
            private set;
        }
    }


    /// <summary>
    /// Manufactured solution 
    /// </summary>
    public class LowMach_ContiManSolution : RHSManuSourceDivKonti, ISpeciesFilter {
        public LowMach_ContiManSolution(string spcName, double Reynolds, double[] MolarMasses, PhysicsMode physicsMode, bool rhoOne, Func<double[], double, double> _sourceFunc = null) : base(Reynolds, MolarMasses, physicsMode, rhoOne, _sourceFunc) {
            ValidSpecies = spcName;
        }

        public string ValidSpecies {
            get;
            private set;
        }
    }
    /// <summary>
    /// Manufactured solution 
    /// </summary>
    public class LowMach_MomentumManSolution :RHSManuSourceNS, ISpeciesFilter {
        public LowMach_MomentumManSolution(string spcName, double Reynolds, double Froude, double[] MolarMasses, string direction, PhysicsMode physMode, bool rhoOne, Func<double[], double, double> _SourceTerm) : base(Reynolds, Froude, MolarMasses, direction, physMode, rhoOne, _SourceTerm) {
            ValidSpecies = spcName;
        }

        public string ValidSpecies {
            get;
            private set;
        }

    }
    /// <summary>
    /// Manufactured solution 
    /// </summary>
    public class LowMach_ScalarManSolution:RHSManuSourceTransportEq, ISpeciesFilter {
        public LowMach_ScalarManSolution(string spcName, double HeatRelease, double Reynolds, double Prandtl, double Schmidt, double[] StoichiometricCoefficients, double[] ReactionRateConstants, double[] MolarMasses, MaterialLaw EoS, string EqType, PhysicsMode physicsMode, int SpeciesIndex = -1, bool chemReactionOK = true, bool rhoOne = false) : base(HeatRelease, Reynolds, Prandtl, Schmidt, StoichiometricCoefficients, ReactionRateConstants, MolarMasses, EoS, EqType, physicsMode, SpeciesIndex, chemReactionOK, rhoOne) {

            ValidSpecies = spcName;
        }

        public string ValidSpecies {
            get;
            private set;
        }
    }


}
