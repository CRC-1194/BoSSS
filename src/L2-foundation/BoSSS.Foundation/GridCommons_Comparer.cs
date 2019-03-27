﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ilPSP;
using ilPSP.Utils;
using BoSSS.Foundation.Comm;
using MPI.Wrappers;
using System.Diagnostics;
namespace BoSSS.Foundation.Grid.Classic
{
    class EqualityComparer<T> : IEqualityComparer<IGrid>
    {
        readonly Func<T, T, bool> CheckEquality;

        public EqualityComparer(Func<T, T, bool> CheckEquality)
        {
            this.CheckEquality = CheckEquality;
        }

        public bool Equals(IGrid x, IGrid y)
        {
            bool isEqual;
            if (x is T X && y is T Y)
            {
                isEqual = CheckEquality(X, Y);
            }
            else
            {
                isEqual = false;
            }
            return isEqual;
        }

        public int GetHashCode(IGrid obj)
        {
            throw new NotImplementedException();
        }
    }

    static class GridCommonsComparer
    {
        public static IEqualityComparer<IGrid> ReferenceComparer {
            get {
                return new EqualityComparer<GridCommons>(AreReferencesEqual);
            }
        }

        public static IEqualityComparer<IGrid> CellComparer {
            get {
                return new EqualityComparer<GridCommons>(AreCellsEqual);
            }
        }

        static bool AreCellsEqual(GridCommons A, GridCommons B)
        {
            if (object.ReferenceEquals(A, B))
                return true;
            if ((A == null) != (B == null))
                return false;


            if (A.Cells == null)
                throw new ArgumentException();
            int A_NumberOfBcCells = A.NumberOfBcCells;

            int match = 1;

            {

                // load cells of grid B, if required
                // ---------------------------------

                Cell[] B_Cells;
                if (B.Cells == null)
                {
                    throw new Exception("Cells are not initialized");
                }
                else
                {
                    B_Cells = B.Cells;
                }

                if (A.Cells.Length != B_Cells.Length)
                    throw new ApplicationException();

                // put the cells of B into the same order as those of A
                // ----------------------------------------------------

                {
                    // tau   is the GlobalID-permutation that we have for the loaded vector
                    // sigma is the current GlobalID-permutation of the grid
                    var sigma = new Permutation(A.Cells.Select(cell => cell.GlobalID).ToArray(), csMPI.Raw._COMM.WORLD);
                    var tau = new Permutation(B_Cells.Select(cell => cell.GlobalID).ToArray(), csMPI.Raw._COMM.WORLD);

                    if (sigma.TotalLength != tau.TotalLength)
                        // should have been checked already
                        throw new ArgumentException();

                    // compute resorting permutation
                    Permutation invSigma = sigma.Invert();
                    Permutation Resorting = invSigma * tau;
                    tau = null;      // Werfen wir sie dem GC zum Fraße vor!
                    invSigma = null;

                    // put dg coordinates into right order
                    Resorting.ApplyToVector(B_Cells.CloneAs(), B_Cells);
                }

                // compare cells
                // -------------

                for (int j = 0; j < A.Cells.Length; j++)
                {
                    Cell Ca = A.Cells[j];
                    Cell Cb = B_Cells[j];

                    Debug.Assert(Ca.GlobalID == Cb.GlobalID);

                    if (!ArrayTools.ListEquals(Ca.NodeIndices, Cb.NodeIndices, (ia, ib) => ia == ib))
                    {
                        match = 0;
                        break;
                    }

                    if (Ca.Type != Cb.Type)
                    {
                        match = 0;
                        break;
                    }

                    if (Ca.CellFaceTags != null || Cb.CellFaceTags != null)
                    {

                        CellFaceTag[] CFTA = Ca.CellFaceTags != null ? Ca.CellFaceTags : new CellFaceTag[0];
                        CellFaceTag[] CFTB = Cb.CellFaceTags != null ? Cb.CellFaceTags : new CellFaceTag[0];

                        if (CFTA.Length != CFTB.Length)
                        {
                            match = 0;
                            break;
                        }

                        bool setMatch = true;
                        for (int i1 = 0; i1 < CFTA.Length; i1++)
                        {
                            bool b = false;
                            for (int j1 = 0; j1 < CFTB.Length; j1++)
                            {
                                if (CFTA[i1].Equals(CFTB[j1]))
                                {
                                    b = true;
                                    break;
                                }
                            }

                            if (b == false)
                            {
                                setMatch = false;
                                break;
                            }
                        }

                        if (!setMatch)
                        {
                            match = 0;
                            break;
                        }
                    }


                    double h = Math.Min(Ca.TransformationParams.MindistBetweenRows(), Cb.TransformationParams.MindistBetweenRows());
                    double L2Dist = Ca.TransformationParams.L2Dist(Cb.TransformationParams);
                    if (L2Dist > h * 1.0e-9)
                    {
                        match = 0;
                        break;
                    }

                }
            }


            if (A_NumberOfBcCells > 0)
            {
                BCElement[] B_BcCells;
                if (B.BcCells == null && !B.BcCellsStorageGuid.Equals(Guid.Empty))
                {
                    throw new Exception("Bc Cells are not initialized");
                }
                else
                {
                    B_BcCells = B.BcCells;
                }

                if (A.BcCells.Length != B_BcCells.Length)
                    throw new ApplicationException("Internal error.");


                // put the cells of B into the same order as those of A
                // ----------------------------------------------------

                {
                    long Offset = A.NumberOfCells_l;

                    // tau   is the GlobalID-permutation that we have for the loaded vector
                    // sigma is the current GlobalID-permutation of the grid
                    var sigma = new Permutation(A.BcCells.Select(cell => cell.GlobalID - Offset).ToArray(), csMPI.Raw._COMM.WORLD);
                    var tau = new Permutation(B_BcCells.Select(cell => cell.GlobalID - Offset).ToArray(), csMPI.Raw._COMM.WORLD);

                    if (sigma.TotalLength != tau.TotalLength)
                        // should have been checked already
                        throw new ArgumentException();

                    // compute resorting permutation
                    Permutation invSigma = sigma.Invert();
                    Permutation Resorting = invSigma * tau;
                    tau = null;      // Werfen wir sie dem GC zum Fraße vor!
                    invSigma = null;

                    // put dg coordinates into right order
                    Resorting.ApplyToVector(B_BcCells.CloneAs(), B_BcCells);
                }


                // compare cells
                // -------------

                for (int j = 0; j < A.BcCells.Length; j++)
                {
                    BCElement Ca = A.BcCells[j];
                    BCElement Cb = B_BcCells[j];

                    Debug.Assert(Ca.GlobalID == Cb.GlobalID);

                    if (!ArrayTools.ListEquals(Ca.NodeIndices, Cb.NodeIndices, (ia, ib) => ia == ib))
                    {
                        match = 0;
                        break;
                    }

                    if (Ca.Type != Cb.Type)
                    {
                        match = 0;
                        break;
                    }

                    if (Ca.Conformal != Cb.Conformal)
                    {
                        match = 0;
                        break;
                    }

                    if (Ca.EdgeTag != Cb.EdgeTag)
                    {
                        match = 0;
                        break;
                    }


                    if (Ca.NeighCell_GlobalIDs != null || Cb.NeighCell_GlobalIDs != null)
                    {

                        long[] NgA = Ca.NeighCell_GlobalIDs != null ? Ca.NeighCell_GlobalIDs : new long[0];
                        long[] NgB = Cb.NeighCell_GlobalIDs != null ? Cb.NeighCell_GlobalIDs : new long[0];

                        if (NgA.Length != NgB.Length)
                        {
                            match = 0;
                            break;
                        }

                        bool setMatch = true;
                        for (int i1 = 0; i1 < NgA.Length; i1++)
                        {
                            bool b = false;
                            for (int j1 = 0; j1 < NgB.Length; j1++)
                            {
                                if (NgA[i1] == NgB[j1])
                                {
                                    b = true;
                                    break;
                                }
                            }

                            if (b == false)
                            {
                                setMatch = false;
                                break;
                            }
                        }

                        if (!setMatch)
                        {
                            match = 0;
                            break;
                        }
                    }


                    double h = Math.Min(Ca.TransformationParams.MindistBetweenRows(), Cb.TransformationParams.MindistBetweenRows());
                    double L2Dist = Ca.TransformationParams.L2Dist(Cb.TransformationParams);
                    if (L2Dist > h * 1.0e-9)
                    {
                        match = 0;
                        break;
                    }

                }
            }


            match = match.MPIMin();
            return (match > 0);
        }

        static bool AreReferencesEqual(GridCommons A, GridCommons B)
        {

            if (object.ReferenceEquals(A, B))
                return true;
            if ((A == null) != (B == null))
                return false;

            ilPSP.MPICollectiveWatchDog.Watch(MPI.Wrappers.csMPI.Raw._COMM.WORLD);

            int glbNoOfCells_A = A.NumberOfCells;
            int glbNoOfCells_B = B.NumberOfCells;
            int glbNoOfBcCells_A = A.NumberOfBcCells;
            int glbNoOfBcCells_B = B.NumberOfBcCells;

            if (glbNoOfCells_A != glbNoOfCells_B)
                return false;

            if (glbNoOfBcCells_A != glbNoOfBcCells_B)
                return false;

            if (!ArrayTools.ListEquals(A.RefElements, B.RefElements, (a, b) => object.ReferenceEquals(a, b)))
                return false;
            if (!ArrayTools.ListEquals(A.EdgeRefElements, B.EdgeRefElements, (a, b) => object.ReferenceEquals(a, b)))
                return false;
            if (!ArrayTools.ListEquals(A.EdgeTagNames, B.EdgeTagNames, (a, b) => (a.Key == b.Key && a.Value.Equals(b.Value))))
                return false;
            if (!ArrayTools.ListEquals(A.PeriodicTrafo, B.PeriodicTrafo, (a, b) => a.ApproximateEquals(b)))
                return false;

            return true;
        }

    }
}