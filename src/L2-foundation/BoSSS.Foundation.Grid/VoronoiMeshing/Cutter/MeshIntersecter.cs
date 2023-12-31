﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using ilPSP;
using BoSSS.Foundation.Grid.Voronoi.Meshing.DataStructures;
using BoSSS.Foundation.Grid.Classic;

namespace BoSSS.Foundation.Grid.Voronoi.Meshing.Cutter
{
    class MeshIntersecter<T>
        where T: ILocatable, new()
    {
        IDMesh<T> mesh;

        static EdgeComparer<T> edgeComparer = new EdgeComparer<T>();

        public class AfterCutEdgeEnumerator : IEnumerator<Edge<T>>
        {
            Edge<T>[] edges;
            Edge<T> current;
            int startIndex;
            int currentIndex;
            int counter;

            int length; //Changes after Reset

            public AfterCutEdgeEnumerator(Edge<T>[] Edges, Edge<T> StartEdge)
            {
                edges = Edges;
                counter = -1;
                length = Edges.Length - 2;
                for (int i = 0; i < Edges.Length; ++i)
                {
                    if (edgeComparer.Equals(StartEdge, Edges[i]))
                    {
                        startIndex = i;
                        currentIndex = i;
                        break;
                    }
                }
            }

            public Edge<T> Current => current;

            object IEnumerator.Current => Current;

            public void Dispose() { }

            public bool MoveNext()
            {

                if (++counter >= length)
                {
                    return false;
                }
                else
                {
                    currentIndex = (currentIndex + 1) % edges.Length;
                    current = edges[currentIndex];
                    return true;
                }
            }

            public void Reset()
            {
                counter = -1;
                length = edges.Length;
                currentIndex = startIndex;
            }

            public MeshCell<T> Cell {
                get {
                    return edges[0].Cell;
                }
            }
        }

        public MeshIntersecter(IDMesh<T> idMesh)
        {
            mesh = idMesh;
        }

        public IEnumerator<Edge<T>> GetFirstEnumerator(MeshCell<T> first)
        {
            AfterCutEdgeEnumerator enumerator = new AfterCutEdgeEnumerator(first.Edges, first.Edges[1]);
            enumerator.Reset();
            return enumerator;
        }

        public IEnumerator<Edge<T>> GetNeighborFromEdgeNeighbor(Edge<T> edge)
        {
            MeshCell<T> newCell = MeshMethods.GetNeighbour(edge);
            AfterCutEdgeEnumerator ridgeEnum = new AfterCutEdgeEnumerator(newCell.Edges, edge);

            return ridgeEnum;
        }

        public Edge<T> Subdivide(Edge<T> edge, List<BoundaryLine> lines, double alpha, CyclicInterval boundaryCount)
        {
            MeshCell<T> cell = edge.Cell;
            //Divide Ridge and update Ridge Arrays
            //-------------------------------------
            Vertex newVertex = DivideEdge(edge, alpha, out Edge<T> newRidge);
            edge.Twin.Cell.IntersectionVertex = newVertex.ID;
            //cell.IntersectionVertex = newVertex.ID;

            //Divide this cell
            //================================================================
            //NewVertices
            Vertex[] verticesOfNewRidgeBoundary = new Vertex[lines.Count + 2];
            verticesOfNewRidgeBoundary[0] = newVertex;
            verticesOfNewRidgeBoundary[verticesOfNewRidgeBoundary.Length - 1] = mesh.Vertices[cell.IntersectionVertex];
            //Add Vertices of lines
            for (int i = 1; i < verticesOfNewRidgeBoundary.Length - 1; ++i)
            {
                verticesOfNewRidgeBoundary[verticesOfNewRidgeBoundary.Length - 1 - i] = lines[i - 1].End;
                int ID = mesh.AddVertex(verticesOfNewRidgeBoundary[verticesOfNewRidgeBoundary.Length - 1 - i]);
            }
            //New Ridges
            Edge<T>[] newEdges;
            Edge<T>[] newNeighborEdges;
            MeshCell<T> newCell = new MeshCell<T> { Node = new T() };
            newCell.Node.Position = cell.Node.Position;
            mesh.AddCell(newCell);
            MeshMethods.CreateBoundaryEdge(verticesOfNewRidgeBoundary, cell, newCell, out newEdges, out newNeighborEdges, boundaryCount);
            //Link Ridges to old neighbors
            MeshMethods.InsertEdgesAndVertices(newEdges, newNeighborEdges);

            //dOnE, DoNe!
            return edge;
        }

        static double accuracy = 1e-10;

        public Edge<T> FirstCut(Edge<T> edge, double alpha)
        {
            if(alpha > 1 - accuracy )
            {
                edge.Cell.IntersectionVertex = edge.Start.ID;
            }else if ( alpha < accuracy)
            {
                edge.Cell.IntersectionVertex = edge.End.ID;
            }
            else
            {
                //Divide Ridge and update Ridge Arrays
                //-------------------------------------
                Vertex newVertex = DivideEdge(edge, alpha, out Edge<T> newEdge);
                edge.Twin.Cell.IntersectionVertex = newVertex.ID;

                //Find Intersection and insert Ridge
                edge.Cell.IntersectionVertex = newVertex.ID;
            }
            return edge;
        }

        public Vertex DivideEdge(Edge<T> edge, double alpha, out Edge<T> newEdge)
        {
            Vector start = edge.Start.Position;
            Vector end = edge.End.Position;

            Vector intersection = start * (1 - alpha) + end * alpha;
            Vertex newVertex = new Vertex
            {
                Position = intersection,
            };
            mesh.AddVertex(newVertex);

            newEdge = new Edge<T>
            {
                Start = newVertex,
                End = edge.End,
                Cell = edge.Cell
            };
            Edge<T> newRidgeTwin = new Edge<T>
            {
                End = newVertex,
                Start = edge.End,
                Cell = edge.Twin.Cell,
                Twin = newEdge
            };
            newEdge.Twin = newRidgeTwin;

            edge.End = newVertex;
            edge.Twin.Start = newVertex;

            MeshMethods.InsertEdgesAndVertices(newEdge);
            MeshMethods.InsertEdgesAndVertices(newRidgeTwin);

            return newVertex;
        }

        public void CloseMesh(List<BoundaryLine> lines, Edge<T> firstCutEdge, CyclicInterval boundaryCount)
        {
            MeshCell<T> cell = firstCutEdge.Cell;
            //Divide this cell
            //================================================================
            //NewVertices
            Vertex[] verticesOfNewRidgeBoundary = new Vertex[lines.Count + 2];
            verticesOfNewRidgeBoundary[0] = firstCutEdge.End;
            verticesOfNewRidgeBoundary[verticesOfNewRidgeBoundary.Length - 1] = mesh.Vertices[cell.IntersectionVertex];
            //Add Vertices of lines
            for (int i = 1; i < verticesOfNewRidgeBoundary.Length - 1; ++i)
            {
                verticesOfNewRidgeBoundary[i] = lines[lines.Count - i].End;
                int ID = mesh.AddVertex(verticesOfNewRidgeBoundary[i]);
            }

            //New Edges
            MeshCell<T> newCell = new MeshCell<T> { Node = new T() };
            newCell.Node.Position = cell.Node.Position;
            mesh.AddCell(newCell);
            MeshMethods.CreateBoundaryEdge(
               verticesOfNewRidgeBoundary,
               cell,
               newCell,
               out Edge<T>[] newEdges,
               out Edge<T>[] newNeighborEdges,
               boundaryCount);
            MeshMethods.InsertEdgesAndVertices(newEdges, newNeighborEdges);
        }

        public void CloseMesh(Edge<T> firstCutEdge, CyclicInterval boundaryCount)
        {
            MeshCell<T> cell = firstCutEdge.Cell;
            //Divide this cell
            //================================================================
            //NewVertices
            Vertex[] verticesOfNewEdgeBoundary = new Vertex[2];
            verticesOfNewEdgeBoundary[0] = firstCutEdge.End;
            verticesOfNewEdgeBoundary[verticesOfNewEdgeBoundary.Length - 1] = mesh.Vertices[cell.IntersectionVertex];

            //New Edges
            MeshCell<T> newCell = new MeshCell<T>();
            mesh.AddCell(newCell);
            MeshMethods.CreateBoundaryEdge(
                verticesOfNewEdgeBoundary, 
                cell, 
                newCell, 
                out Edge<T>[] newEdges, 
                out Edge<T>[] newNeighborEdges,
                boundaryCount);
            MeshMethods.InsertEdgesAndVertices(newEdges, newNeighborEdges);
        }

        public IEnumerator<Edge<T>> GetConnectedEdgeEnum(Edge<T> edge)
        {
            List<Edge<T>> outgoingEdges = new List<Edge<T>>();
            bool newNeighbor = true;
            Edge<T> worker = edge;
            while (newNeighbor)
            {
                worker = worker.Twin;
                Edge<T>[] workerRidges = worker.Cell.Edges;
                outgoingEdges.Add(worker);
                for (int i = 0; i < workerRidges.Length; ++i)
                {
                    if (edgeComparer.Equals(workerRidges[i], worker))
                    {
                        if (i == 0)
                            worker = workerRidges.Last();
                        else
                            worker = workerRidges[i - 1];
                        if (edgeComparer.Equals(worker, edge))
                        {
                            newNeighbor = false;
                        }
                        break;
                    }
                }
            }
            return new ArrayEnumerator<Edge<T>>(outgoingEdges);
        }

        public void VertexCut(Edge<T> edge, double alphaCut)
        {
            MeshCell<T> cell = edge.Cell;
            Vertex newOldVertex = edge.End;
            cell.IntersectionVertex = newOldVertex.ID;
            cell = edge.Twin.Cell;
            cell.IntersectionVertex = newOldVertex.ID;
        }

        public AfterCutEdgeEnumerator GetNeighborFromLineDirection(Edge<T> edge, BoundaryLine line)
        {
            IEnumerator<Edge<T>> outgoingEdges = GetConnectedEdgeEnum(edge);
            Edge<T> A = null;
            Edge<T> B = null;
            if (outgoingEdges.MoveNext())
            {
                A = outgoingEdges.Current;
            }
            else
            {
                throw new Exception();
            }
            bool found = false;
            while (!found && outgoingEdges.MoveNext())
            {
                B = outgoingEdges.Current;
                if (IsPositiveRotation(A, line, B))
                {
                    found = true;
                }
                else
                {
                    A = B;
                }
            }
            if(found == false)
            {
                throw new Exception("Neighbor could not be determined from line direction!");
            }
            A.Cell.IntersectionVertex = A.Start.ID;
            return new AfterCutEdgeEnumerator(A.Cell.Edges, A);
        }

        public AfterCutEdgeEnumerator GetAfterCutEdgeEnumerator(Edge<T>[] edges, Edge<T> edge)
        {
            return new AfterCutEdgeEnumerator(edges, edge);
        }

        //Check if in positive rotation a, c, b order
        static bool IsPositiveRotation(Edge<T> a, BoundaryLine b, Edge<T> c)
        {
            Vector A1 = a.End.Position - a.Start.Position;
            Vector B1 = b.End.Position - b.Start.Position;
            Vector C1 = c.End.Position - c.Start.Position;

            double crossAB = A1.CrossProduct2D(B1);
            double crossBC = B1.CrossProduct2D(C1);
            if (crossAB > 0 && crossBC > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        class MultiNeighRidgesAfterCutEnum : IEnumerator<Edge<T>>
        {
            IList<Edge<T>> enumEdges;
            IList<int> block;
            int pointer;
            int blockPointer;
            bool filter;
            public MultiNeighRidgesAfterCutEnum(IList<Edge<T>> edges, IList<int> blockInFirstRun)
            {
                enumEdges = edges;
                block = blockInFirstRun;
                pointer = -1;
                blockPointer = 0;
                filter = true;
            }
            public Edge<T> Current => enumEdges[pointer];

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public bool MoveNext()
            {
                ++pointer;
                if (filter)
                {
                    while (blockPointer < block.Count && pointer == block[blockPointer])
                    {
                        ++pointer;
                        ++blockPointer;
                    }
                }
                return (pointer < enumEdges.Count);
            }

            public void Reset()
            {
                pointer = -1;
                filter = false;
            }
        }

        public Edge<T> AddLineSegment(Edge<T> edge, double alpha, CyclicInterval boundaryCount)
        {
            DivideEdge(edge, alpha, out Edge<T> newRidge);
            edge.IsBoundary = true;
            edge.Twin.IsBoundary = true;
            edge.BoundaryEdgeNumber = boundaryCount.Current();
            edge.Twin.BoundaryEdgeNumber = boundaryCount.Current();
            edge.Cell.IntersectionVertex = edge.End.ID;
            edge.Twin.Cell.IntersectionVertex = edge.End.ID;
            return edge;
        }

        public Edge<T> SubdivideWithoutNewVertex(Edge<T> edge, List<BoundaryLine> lines, CyclicInterval boundaryCount)
        {
            MeshCell<T> cell = edge.Cell;
            Vertex cutVertex = edge.End;
            IEnumerator<Edge<T>> neighEdges = GetConnectedEdgeEnum(edge);
            while (neighEdges.MoveNext())
            {
                MeshCell<T> neighbor = neighEdges.Current.Cell;
                if (neighbor.ID != cell.ID)
                    neighbor.IntersectionVertex = cutVertex.ID;
            }
            //Divide this cell
            //================================================================
            Vertex[] verticesOfNewEdgeBoundary = new Vertex[lines.Count + 2];
            verticesOfNewEdgeBoundary[0] = cutVertex;
            verticesOfNewEdgeBoundary[verticesOfNewEdgeBoundary.Length - 1] = mesh.Vertices[cell.IntersectionVertex];
            //Add Vertices of lines
            for (int i = 1; i < verticesOfNewEdgeBoundary.Length - 1; ++i)
            {
                verticesOfNewEdgeBoundary[verticesOfNewEdgeBoundary.Length - 1 - i] = lines[i - 1].End;
                mesh.AddVertex(verticesOfNewEdgeBoundary[verticesOfNewEdgeBoundary.Length - 1 - i]);
            }
            //New Ridges
            Edge<T>[] newEdges;
            Edge<T>[] newNeighborEdges;
            MeshCell<T> newCell = new MeshCell<T> { Node = new T() };
            newCell.Node.Position = cell.Node.Position;
            mesh.AddCell(newCell);
            MeshMethods.CreateBoundaryEdge(verticesOfNewEdgeBoundary, cell, newCell, out newEdges, out newNeighborEdges, boundaryCount);
            //Link Ridges to old neighbors
            MeshMethods.InsertEdgesAndVertices(newEdges, newNeighborEdges);


            //dOnE, DoNe!
            return edge;
        }

        public void AddEdge(Edge<T> edge, CyclicInterval boundaryCount)
        {
            edge.IsBoundary = true;
            edge.Twin.IsBoundary = true;
            edge.BoundaryEdgeNumber = boundaryCount.Current();
            edge.Twin.BoundaryEdgeNumber = boundaryCount.Current();
            edge.Cell.IntersectionVertex = edge.End.ID;
            edge.Twin.Cell.IntersectionVertex = edge.End.ID;
        }
    }
}
