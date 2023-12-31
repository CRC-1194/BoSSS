﻿
using BoSSS.Foundation.XDG;
using BoSSS.Solution;
using BoSSS.Solution.LevelSetTools.FourierLevelSet;
using BoSSS.Solution.LevelSetTools.SolverWithLevelSetUpdater;
using BoSSS.Solution.NSECommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoSSS.Application.XNSE_Solver {

    /// <summary>
    /// base-class for post-processing modules in XNSE
    /// the generic type is necessary to make the methods e.g. <see cref="CurrentVel"/> also available in solvers
    /// deriving from <see cref="XNSE{T}"/>. otherwise we are unable to cast e.g. XNSE<T> to XNSE<P> where T:P.
    /// </summary>
    public abstract class XNSEinSituPostProcessingModule<T> : InSituPostProcessingModule where T:XNSE_Control, new() {

        /// <summary>
        /// reference to solver application class
        /// </summary>
        protected XNSE<T> SolverMainOverride {
            get {
                return (XNSE<T>) base.SolverMain;
            }
        }

        /// <summary>
        /// control object
        /// </summary>
        new protected T Control {
            get {
                return (T)(base.Control);
            }
        }


        /// <summary>
        /// current velocity solution
        /// </summary>
        protected XDGField[] CurrentVel {
            get {                
                int D = this.SolverMainOverride.GridData.SpatialDimension;

                var ret = this.SolverMainOverride.CurrentState.Fields.Take(D).Select(f => (XDGField)f).ToArray();
                for(int d = 0; d < D; d++) {
                    if(ret[d].Identification != VariableNames.Velocity_d(d))
                        throw new ApplicationException("Unable to identify velocity fields.");
                }

                return ret;
            }
        }

        /// <summary>
        /// current velocity solution
        /// </summary>
        protected XDGField CurrentPressure {
            get {               
                int D = this.SolverMainOverride.GridData.SpatialDimension;

                var ret = this.SolverMainOverride.CurrentState.Fields.ElementAt(D) as XDGField;
                if(ret.Identification != VariableNames.Pressure)
                    throw new ApplicationException("Unable to identify pressure field.");
                    
                return ret;               
            }
        }


        /// <summary>
        /// the level-set which represents the fluid interface
        /// </summary>
        protected LevelSet LevSet {
            get {
                return (LevelSet)(this.LsTrk.LevelSets[0]);
            }
        }


        /// <summary>
        /// Cut-Cell quadrature order used for the flow solver
        /// </summary>
        protected int m_HMForder {
            get {
                return this.SolverMainOverride.QuadOrder();   
            }
        }


        /// <summary>
        /// Cut-Cell quadrature order used for the flow solver
        /// </summary>
        protected XDGField KineticEnergy {
            get {
                var a = this.SolverMainOverride.Timestepping.Parameters.First(t => t.Identification == BoSSS.Solution.NSECommon.VariableNames.KineticEnergy);
                return (XDGField) a;
            }
        }

        /// <summary>
        /// If used, the Fourier level set; otherwise null;
        /// </summary>
        protected FourierLevSetBase Fourier_LevSet {
            get {                 
                if(this.SolverMainOverride.LsUpdater.LevelSets[VariableNames.LevelSetCG].DGLevelSet is FourierLevelSet fls) {
                    return fls.Fourier_LevSet;
                } else {
                    return null;
                }
            }
        }

        /// <summary>
        /// les gird
        /// </summary>
        protected BoSSS.Foundation.Grid.Classic.GridData GridData {
            get {
                return (Foundation.Grid.Classic.GridData)(this.SolverMainOverride.GridData);
            }
        }



    }
}
