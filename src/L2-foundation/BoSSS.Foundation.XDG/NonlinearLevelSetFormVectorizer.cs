﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ilPSP;
using System.Diagnostics;
using BoSSS.Platform.LinAlg;

namespace BoSSS.Foundation.XDG {
    class NonlinearLevelSetFormVectorizer :
        INonlinLevelSetForm_V,
        INonlinLevelSetForm_GradV //
    {
        /// <summary>
        /// ctor.
        /// </summary>
        public NonlinearLevelSetFormVectorizer(ILevelSetForm _OrgComponent) {
            this.ArgumentOrdering = _OrgComponent.ArgumentOrdering.ToArray();
            this.ParameterOrdering = _OrgComponent.ParameterOrdering != null ? _OrgComponent.ParameterOrdering.ToArray() : null;
            this.LevelSetIndex = _OrgComponent.LevelSetIndex;
            this.PositiveSpecies = _OrgComponent.PositiveSpecies;
            this.NegativeSpecies = _OrgComponent.NegativeSpecies;
            this.LevelSetTerms = _OrgComponent.LevelSetTerms;
            this.OrgComponent = _OrgComponent;
        }

        
        /// <summary>
        /// The original component that is being vectorized.
        /// </summary>
        ILevelSetForm OrgComponent;


        public int LevelSetIndex {
            get;
            private set;
        }

        public SpeciesId PositiveSpecies {
            get;
            private set;
        }

        public SpeciesId NegativeSpecies {
            get;
            private set;
        }

        public TermActivationFlags LevelSetTerms {
            get;
            private set;
        }

        public IList<string> ArgumentOrdering {
            get;
            private set;
        }

        public IList<string> ParameterOrdering {
            get;
            private set;
        }


        double ILevelSetForm.LevelSetForm(ref CommonParams inp, double[] uA, double[] uB, double[,] Grad_uA, double[,] Grad_uB, double vA, double vB, double[] Grad_vA, double[] Grad_vB) {
            return OrgComponent.LevelSetForm(ref inp, uA, uB, Grad_uA, Grad_uB, vA, vB, Grad_vA, Grad_vB);
        }


        void INonlinLevelSetForm_V.LevelSetForm_V(LevSetIntParams inp, MultidimensionalArray[] uA, MultidimensionalArray[] uB, MultidimensionalArray[] Grad_uA, MultidimensionalArray[] Grad_uB, MultidimensionalArray Koeff_V) {
            int L = inp.Len;
            Debug.Assert(Koeff_V.GetLength(0) == L);
            int K = Koeff_V.GetLength(1); // no of nodes per cell
            int D = inp.Nodes.GetLength(2); // spatial dimension
            int _NOParams = this.ParameterOrdering == null ? 0 : this.ParameterOrdering.Count;
            Debug.Assert(_NOParams == inp.ParamsNeg.Length);
            Debug.Assert(_NOParams == inp.ParamsPos.Length);
            int _NOargs = this.ArgumentOrdering.Count;
            Debug.Assert(_NOargs == uA.Length);
            Debug.Assert(_NOargs == uB.Length);
            Debug.Assert(_NOargs == Grad_uA.Length);
            Debug.Assert(_NOargs == Grad_uB.Length);
            var lsTrk = inp.LsTrk;
            var Reg = lsTrk.Regions;
            SpeciesId posSpc = this.PositiveSpecies;
            SpeciesId negSpc = this.NegativeSpecies;

            CommonParams cp;
            cp.Normal = new Vector(D);
            cp.X = new Vector(D);
            cp.Parameters_OUT = new double[_NOParams];
            cp.Parameters_IN = new double[_NOParams];
            cp.time = inp.time;
            cp.iEdge = -123456;
            cp.GridDat = null;
            double[] _Grad_vA = new double[D];
            double[] _Grad_vB = new double[D];
            double[,] _Grad_uA = new double[_NOargs, D];
            double[,] _Grad_uB = new double[_NOargs, D];
            double[] _uA = new double[_NOargs];
            double[] _uB = new double[_NOargs];
            double _vA = 0.0, _vB = 0.0;


            for (int l = 0; l < L; l++) { // loop over cells...
                cp.jCellIn = inp.i0 + l;
                cp.jCellOut = cp.jCellIn;
                //if (inp.PosCellLengthScale != null)
                //    cp.PosCellLengthScale = inp.PosCellLengthScale[cp.jCell];
                //else
                //    cp.PosCellLengthScale = double.NaN;
                //if (inp.NegCellLengthScale != null)
                //    cp.NegCellLengthScale = inp.NegCellLengthScale[cp.jCell];
                //else
                //    cp.NegCellLengthScale = double.NaN;

                ReducedRegionCode rrc;
                int NoOf = Reg.GetNoOfSpecies(l + inp.i0, out rrc);
                Debug.Assert(NoOf == 2);
                int iSpcPos = lsTrk.GetSpeciesIndex(rrc, posSpc);
                int iSpcNeg = lsTrk.GetSpeciesIndex(rrc, negSpc);

                for (int k = 0; k < K; k++) { // loop over nodes...

                    for (int np = 0; np < _NOParams; np++) {
                        cp.Parameters_OUT[np] = inp.ParamsPos[np][l, k];
                        cp.Parameters_IN[np] = inp.ParamsNeg[np][l, k];
                    }
                    for (int d = 0; d < D; d++) {
                        cp.X[d] = inp.Nodes[l, k, d];
                        cp.Normal[d] = inp.Normals[l, k, d];
                    }

                    for (int na = 0; na < _NOargs; na++) {
                        Debug.Assert((uA[na] != null) == (uB[na] != null));
                        if (uA[na] != null) {
                            _uA[na] = uA[na][l, k];
                            _uB[na] = uB[na][l, k];
                        } else {
                            _uA[na] = 0;
                            _uA[na] = 0;
                        }
                        Debug.Assert((Grad_uA[na] != null) == (Grad_uB[na] != null));
                        if (Grad_uA[na] != null) {
                            for (int d = 0; d < D; d++) {
                                _Grad_uA[na, d] = Grad_uA[na][l, k, d];
                                _Grad_uB[na, d] = Grad_uB[na][l, k, d];
                            }
                        } else {
                            for (int d = 0; d < D; d++) {
                                _Grad_uA[na, d] = 0;
                                _Grad_uB[na, d] = 0;
                            }
                        }
                    }

                    _vA = 1;
                    _vB = 0;
                    Koeff_V[l, k, iSpcNeg] += OrgComponent.LevelSetForm(ref cp, _uA, _uB, _Grad_uA, _Grad_uB, _vA, _vB, _Grad_vA, _Grad_vB);
                    _vA = 0;
                    _vB = 1;
                    Koeff_V[l, k, iSpcPos] += OrgComponent.LevelSetForm(ref cp, _uA, _uB, _Grad_uA, _Grad_uB, _vA, _vB, _Grad_vA, _Grad_vB);
                }
            }
        }

        

        void INonlinLevelSetForm_GradV.LevelSetForm_GradV(LevSetIntParams inp, MultidimensionalArray[] uA, MultidimensionalArray[] uB, MultidimensionalArray[] Grad_uA, MultidimensionalArray[] Grad_uB, MultidimensionalArray Koeff_GradV) {
            int L = inp.Len;
            Debug.Assert(Koeff_GradV.GetLength(0) == L);
            int K = Koeff_GradV.GetLength(1); // no of nodes per cell
            int D = inp.Nodes.GetLength(2); // spatial dimension
            int _NOParams = this.ParameterOrdering == null ? 0 : this.ParameterOrdering.Count;
            Debug.Assert(_NOParams == inp.ParamsNeg.Length);
            Debug.Assert(_NOParams == inp.ParamsPos.Length);
            int _NOargs = this.ArgumentOrdering.Count;
            Debug.Assert(_NOargs == uA.Length);
            Debug.Assert(_NOargs == uB.Length);
            Debug.Assert(_NOargs == Grad_uA.Length);
            Debug.Assert(_NOargs == Grad_uB.Length);
            var lsTrk = inp.LsTrk;
            var Reg = lsTrk.Regions;
            SpeciesId posSpc = this.PositiveSpecies;
            SpeciesId negSpc = this.NegativeSpecies;

            CommonParams cp;
            cp.Normal = new Vector(D);
            cp.X = new Vector(D);
            cp.Parameters_OUT = new double[_NOParams];
            cp.Parameters_IN = new double[_NOParams];
            cp.time = inp.time;
            cp.iEdge = -123456;
            cp.GridDat = null;
            double[] _Grad_vA = new double[D];
            double[] _Grad_vB = new double[D];
            double[,] _Grad_uA = new double[_NOargs, D];
            double[,] _Grad_uB = new double[_NOargs, D];
            double[] _uA = new double[_NOargs];
            double[] _uB = new double[_NOargs];
            double _vA = 0.0, _vB = 0.0;


            for (int l = 0; l < L; l++) { // loop over cells...
                cp.jCellIn = inp.i0 + l;
                cp.jCellOut = cp.jCellIn;
                //if (inp.PosCellLengthScale != null)
                //    cp.PosCellLengthScale = inp.PosCellLengthScale[cp.jCell];
                //else
                //    cp.PosCellLengthScale = double.NaN;
                //if (inp.NegCellLengthScale != null)
                //    cp.NegCellLengthScale = inp.NegCellLengthScale[cp.jCell];
                //else
                //    cp.NegCellLengthScale = double.NaN;

                ReducedRegionCode rrc;
                int NoOf = Reg.GetNoOfSpecies(l + inp.i0, out rrc);
                Debug.Assert(NoOf == 2);
                int iSpcPos = lsTrk.GetSpeciesIndex(rrc, posSpc);
                int iSpcNeg = lsTrk.GetSpeciesIndex(rrc, negSpc);

                for (int k = 0; k < K; k++) { // loop over nodes...

                    for (int np = 0; np < _NOParams; np++) {
                        cp.Parameters_OUT[np] = inp.ParamsPos[np][l, k];
                        cp.Parameters_IN[np] = inp.ParamsNeg[np][l, k];
                    }
                    for (int d = 0; d < D; d++) {
                        cp.X[d] = inp.Nodes[l, k, d];
                        cp.Normal[d] = inp.Normals[l, k, d];
                    }

                    for (int na = 0; na < _NOargs; na++) {
                        Debug.Assert((uA[na] != null) == (uB[na] != null));
                        if (uA[na] != null) {
                            _uA[na] = uA[na][l, k];
                            _uB[na] = uB[na][l, k];
                        } else {
                            _uA[na] = 0;
                            _uA[na] = 0;
                        }
                        Debug.Assert((Grad_uA[na] != null) == (Grad_uB[na] != null));
                        if (Grad_uA[na] != null) {
                            for (int d = 0; d < D; d++) {
                                _Grad_uA[na, d] = Grad_uA[na][l, k, d];
                                _Grad_uB[na, d] = Grad_uB[na][l, k, d];
                            }
                        } else {
                            for (int d = 0; d < D; d++) {
                                _Grad_uA[na, d] = 0;
                                _Grad_uB[na, d] = 0;
                            }
                        }
                    }
                    for (int d = 0; d < D; d++) {
                        _Grad_vA[d] = 1.0;
                        Koeff_GradV[l, k, iSpcNeg, d] += OrgComponent.LevelSetForm(ref cp, _uA, _uB, _Grad_uA, _Grad_uB, _vA, _vB, _Grad_vA, _Grad_vB);
                        _Grad_vA[d] = 0.0;

                        _Grad_vB[d] = 1.0;
                        Koeff_GradV[l, k, iSpcPos, d] += OrgComponent.LevelSetForm(ref cp, _uA, _uB, _Grad_uA, _Grad_uB, _vA, _vB, _Grad_vA, _Grad_vB);
                        _Grad_vB[d] = 0.0;
                    }
                }
            }
        }
    }
}
