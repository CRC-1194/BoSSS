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
using System.Threading.Tasks;
using BoSSS.Foundation.Grid.RefElements;
using BoSSS.Platform.Utils.Geom;
using ilPSP;
using System.Diagnostics;
using ilPSP.Utils;

namespace BoSSS.Foundation.Grid.Aggregation {
    partial class AggregationGridData {

        public IGeometricalCellsData iGeomCells {
            get {
                return m_GeomCellData;
            }
        }

        GeomCellData m_GeomCellData;

        /// <summary>
        /// Just a wrapper/proxy around the geometrical cell data (<see cref="IGridData.iGeomCells"/>) 
        /// of the parent grid (<see cref="AggregationGridData.ParentGrid"/>).
        /// </summary>
        class GeomCellData : IGeometricalCellsData {
            internal AggregationGridData m_Owner;

            public int[][] CellVertices {
                get {
                    return m_Owner.ParentGrid.iGeomCells.CellVertices;
                }
            }

            public MultidimensionalArray h_max {
                get {
                    return m_Owner.ParentGrid.iGeomCells.h_max;
                }
            }

            public MultidimensionalArray h_min {
                get {
                    return m_Owner.ParentGrid.iGeomCells.h_min;
                }
            }

            public CellInfo[] InfoFlags {
                get {
                    return m_Owner.ParentGrid.iGeomCells.InfoFlags;
                }
            }

            public MultidimensionalArray InverseTransformation {
                get {
                    return m_Owner.ParentGrid.iGeomCells.InverseTransformation;
                }
            }

            public MultidimensionalArray JacobiDet {
                get {
                    return m_Owner.ParentGrid.iGeomCells.JacobiDet;
                }
            }

            public int Count {
                get {
                    return m_Owner.ParentGrid.iGeomCells.Count;
                }
            }

            public RefElement[] RefElements {
                get {
                    return m_Owner.ParentGrid.iGeomCells.RefElements;
                }
            }

            public MultidimensionalArray Transformation {
                get {
                    return m_Owner.ParentGrid.iGeomCells.Transformation;
                }
            }

            public int[] GeomCell2LogicalCell {
                get;
                internal set;
            }

            public int NoOfLocalUpdatedCells {
                get {
                    return m_Owner.ParentGrid.iGeomCells.NoOfLocalUpdatedCells;
                }
            }

            public void GetCellBoundingBox(int j, BoundingBox bb) {
                m_Owner.ParentGrid.iGeomCells.GetCellBoundingBox(j, bb);
            }


            CellMask[] m_Cells4Refelement;

            public GeomCellData() {
            }

            public CellMask GetCells4Refelement(RefElement Kref) {
                int iKref = Array.IndexOf(this.RefElements, Kref);

                if (m_Cells4Refelement == null) {
                    m_Cells4Refelement = new CellMask[this.RefElements.Length];
                }

                if(m_Cells4Refelement[iKref] == null) { 
                    var OrgMsk = m_Owner.ParentGrid.iGeomCells.GetCells4Refelement(Kref);
                    Debug.Assert(OrgMsk.MaskType == MaskType.Geometrical);
                    Debug.Assert(object.ReferenceEquals(OrgMsk.GridData, m_Owner.ParentGrid));

                    m_Cells4Refelement[iKref] = new CellMask(m_Owner, OrgMsk, MaskType.Geometrical);
                }
                return m_Cells4Refelement[iKref];
            }

            public CellType GetCellType(int jCell) {
                return m_Owner.ParentGrid.iGeomCells.GetCellType(jCell);
            }

            public double GetCellVolume(int j) {
                return m_Owner.ParentGrid.iGeomCells.GetCellVolume(j);
            }

            public int GetNoOfSimilarConsecutiveCells(CellInfo mask, int j0, int Lmax) {
                throw new NotImplementedException();
            }

            public RefElement GetRefElement(int j) {
                return this.RefElements[GetRefElementIndex(j)];
            }

            public int GetRefElementIndex(int jCell) {
                return m_Owner.ParentGrid.iGeomCells.GetRefElementIndex(jCell);
            }

            /// <summary>
            /// Always false for aggregation grids, since they require the orthonormalization (<see cref="BasisData.OrthonormalizationTrafo"/>) in each geometrical cell.
            /// </summary>
            /// <param name="j">
            /// Geometric cell index
            /// </param>
            /// <returns>
            /// always false
            /// </returns>
            public bool IsCellAffineLinear(int j) {
                if (j < 0)
                    throw new IndexOutOfRangeException();
                if( j >= Count)
                    throw new IndexOutOfRangeException();
                return false; 
            }

            public int GetInterpolationDegree(int jCell) {
                return m_Owner.ParentGrid.iGeomCells.GetInterpolationDegree(jCell);
            }

            /// <summary>
            /// Center-of-gravity
            /// </summary>
            public double[] GetCenter(int jCell) {
                return m_Owner.ParentGrid.iGeomCells.GetCenter(jCell);
            }
        }

        LogicalCellData m_LogicalCellData;

        public ILogicalCellData iLogicalCells {
            get {
                return m_LogicalCellData;
            }
        }

        public class LogicalCellData : ILogicalCellData {
            internal AggregationGridData m_Owner;

            public int[][] AggregateCellToParts {
                get;
                internal set;
            }

            public int[][] CellNeighbours {
                get;
                internal set;
            }

            public int[][] Cells2Edges {
                get;
                internal set;
            }

            public int Count {
                get {
                    return NoOfLocalUpdatedCells + NoOfExternalCells;
                }
            }

            public int NoOfExternalCells {
                get {
                    return m_Owner.m_Parallel.GlobalIndicesExternalCells.Length;
                }
            }

            public int NoOfLocalUpdatedCells {
                get {
                    Debug.Assert(m_Owner.CellPartitioning.LocalLength == CellNeighbours.Length);
                    return m_Owner.CellPartitioning.LocalLength;
                }
            }

            MultidimensionalArray m_CellLengthScales;

            public MultidimensionalArray CellLengthScale{
                get{
                    if (m_CellLengthScales == null){
                        m_CellLengthScales = CreateCellLengthScales();
                    }
                    return m_CellLengthScales;
                }
            }

            MultidimensionalArray CreateCellLengthScales(){
                MultidimensionalArray scales = MultidimensionalArray.Create(Count);
                for (int iLogicalCells = 0; iLogicalCells < Count; ++iLogicalCells)
                {
                    double edgeArea = 0;
                    int[] logicalEdges = Cells2Edges[iLogicalCells];
                    foreach(int jLogical in logicalEdges)
                    {
                        edgeArea += m_Owner.iLogicalEdges.GetEdgeArea(Math.Abs(jLogical) -1);
                    }
                    double volume = GetCellVolume(iLogicalCells);
                    scales[iLogicalCells] = volume / edgeArea;
                }
                return scales;
            }

            public void GetCellBoundingBox(int j, BoundingBox bb) {
                BoundingBox bbTemp = new BoundingBox(bb.D);
                bb.Clear();
                foreach(int jPart in this.AggregateCellToParts[j]) {
                    m_Owner.m_GeomCellData.GetCellBoundingBox(jPart, bbTemp);
                    bb.AddBB(bbTemp);
                }
            }

            public CellMask GetCells4Refelement(RefElement Kref) {
                throw new NotImplementedException();
            }

            public double GetCellVolume(int j) {
                double Sum = 0;
                foreach(int jPart in AggregateCellToParts[j]) {
                    Sum += m_Owner.m_GeomCellData.GetCellVolume(jPart);
                }
                return Sum;
            }

            public long GetGlobalID(int j) {
                return m_Owner.aggregationGrid.AggregationCells[j].GlobalID;
            }

            public int GetInterpolationDegree(int j) {
                int r = int.MinValue;
                foreach(int jPart in AggregateCellToParts[j]) {
                    r = Math.Max(r, m_Owner.m_GeomCellData.GetInterpolationDegree(j));
                }
                return r;
            }

            public bool IsCellAffineLinear(int j) {
                bool ret = true;
                foreach(int jPart in AggregateCellToParts[j]) {
                    ret &= m_Owner.m_GeomCellData.IsCellAffineLinear(j);
                }
                return ret;
            }

            /// <summary>
            /// Center-of-gravity
            /// </summary>
            public double[] GetCenter(int jCell) {
                int D = m_Owner.SpatialDimension;
                double VolAcc = 0.0;
                double[] CenAcc = new double[D];

                foreach (int jG in this.AggregateCellToParts[jCell]) {
                    double Vol = m_Owner.m_GeomCellData.GetCellVolume(jG);
                    var g_cent = m_Owner.m_GeomCellData.GetCenter(jG);

                    VolAcc += Vol;
                    CenAcc.AccV(Vol, g_cent);
                }

                CenAcc.ScaleV(1 / VolAcc);
                return CenAcc;
            }
        }

    }
}
