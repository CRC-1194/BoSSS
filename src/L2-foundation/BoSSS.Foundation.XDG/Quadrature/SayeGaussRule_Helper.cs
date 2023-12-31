﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using BoSSS.Foundation.Grid.RefElements;
using System.Diagnostics;
using ilPSP;
using BoSSS.Foundation.Quadrature;

namespace BoSSS.Foundation.XDG.Quadrature
{
    class LinearPSI<S>
        where S : RefElement

    {
        BitArray fixedDims;
        double[] fixedValues;
        private static S refElement;

        public LinearPSI(S _RefElement)
        {
            refElement = _RefElement;
            fixedDims = new BitArray(refElement.SpatialDimension);
            fixedValues = new double[refElement.SpatialDimension];
        }

        public LinearPSI()
        {
            Debug.Assert(refElement != null);
            fixedDims = new BitArray(refElement.SpatialDimension);
            fixedValues = new double[refElement.SpatialDimension];
        }

        public static int SpatialDimension {
            get => refElement.SpatialDimension;
        }

        private LinearPSI(BitArray FixedDims, double[] FixedValues)
        {
            fixedDims = FixedDims;
            fixedValues = FixedValues;
        }

        public LinearPSI<S> ReduceDim(int dimension, double value)
        {
            BitArray FixedDims = new BitArray(refElement.SpatialDimension);
            double[] FixedValues = new double[refElement.SpatialDimension];
            for (int i = 0; i < fixedDims.Count; ++i)
            {
                if (this.fixedDims[i] || (dimension == i))
                {
                    FixedDims[i] = true;
                    if (dimension == i)
                    {
                        FixedValues[i] = value;
                    }
                    else
                    {
                        FixedValues[i] = this.fixedValues[i];
                    }
                }
            }
            return new LinearPSI<S>(FixedDims, FixedValues);
        }

        public NodeSet ProjectOnto(NodeSet nodes)
        {
            NodeSet projection = new NodeSet(refElement, nodes.NoOfNodes, refElement.SpatialDimension, false);
            for (int j = 0; j < nodes.NoOfNodes; ++j)
            {
                for (int i = 0; i < fixedDims.Count; ++i)
                {
                    if (fixedDims[i])
                    {
                        projection[j, i] = fixedValues[i];
                    }
                    else
                    {
                        projection[j, i] = nodes[j, i];
                    }
                }
            }
            projection.LockForever();
            return projection;
        }

        public MultidimensionalArray ProjectOnto(MultidimensionalArray nodes)
        {
            MultidimensionalArray projection = nodes.CloneAs();
            for (int j = 0; j < nodes.GetLength(0); ++j)
            {
                for (int i = 0; i < fixedDims.Count; ++i)
                {
                    if (fixedDims[i])
                    {
                        projection[j, i] = fixedValues[i];
                    }
                    else
                    {
                        projection[j, i] = nodes[j, i];
                    }
                }
            }
            projection.LockForever();
            return projection;
        }

        public void SetInactiveDimsToZero(double[] arr)
        {
            Debug.Assert(arr.Length == SpatialDimension);

            for (int i = 0; i < SpatialDimension; ++i)
            {
                if (fixedDims[i])
                    arr[i] = 0;
            }
        }

        public bool DirectionIsFixed(int heightDirection)
        {
            return fixedDims[heightDirection];
        }
    }

    class LinearSayeSpace<T> :
        SayeArgument<LinearPSI<T>>
        where T : RefElement
    {
        static T refElement; 

        BitArray removedDims;
        protected bool subdivided = false;

        public List<Tuple<LinearPSI<T>, int>> psiAndS = new List<Tuple<LinearPSI<T>, int>>();

        public LinearSayeSpace(T RefElement, bool _Surface)
        {
            refElement = RefElement;
            dim = refElement.SpatialDimension;
            surface = _Surface;
            StandardSetup();
        }

        public LinearSayeSpace(T RefElement, Tuple<LinearPSI<T>, int> PsiAndS, bool _Surface)
        {
            refElement = RefElement;
            dim = refElement.SpatialDimension;
            surface = _Surface;
            StandardSetup();
            psiAndS.Add(PsiAndS);
        }

        private LinearSayeSpace(double[][] _Boundaries, int Dim, BitArray newRemovedDims)
        {
            dim = Dim;
            removedDims = newRemovedDims;
            SetBoundaries(_Boundaries);
            data = new NodesAndWeights(refElement);
        }

        private void StandardSetup()
        {
            data = new NodesAndWeights(refElement);
            removedDims = new BitArray(refElement.SpatialDimension);
            boundaries = new double[refElement.SpatialDimension][];
            diameters = new double[refElement.SpatialDimension];
            for (int i = 0; i < refElement.SpatialDimension; ++i)
            {
                boundaries[i] = new double[] { -1, 1 };
                diameters[i] = 1;
            }        
        }

        public LinearSayeSpace<T> DeriveNew()
        {
            double[][] newBoundary = boundaries.Copy();
            BitArray newRemovedDims = removedDims.CloneAs();
            LinearSayeSpace<T> arg = new LinearSayeSpace<T>(newBoundary, dim, newRemovedDims);
            arg.subdivided = this.subdivided;
            arg.Surface = this.surface;
            return arg;
        }

        public LinearSayeSpace<T> Subdivide()
        {
            subdivided = true;
            double[][] newBoundary = boundaries.Copy();

            //Figure out new Boundaries
            double max = double.MinValue;
            int k_MaxDiameter = 0; 
            for(int i = 0; i < refElement.SpatialDimension; ++i)
            {
                //Only consider active Dimensions 
                if (diameters[i] > max  && !removedDims[i])
                {
                    max = diameters[i];
                    k_MaxDiameter = i;
                }
            }

            double[] maxBounds = boundaries[k_MaxDiameter];
            double newBound = (maxBounds[0] + maxBounds[1]) / 2;
            maxBounds[1] = newBound;
            SetBoundaries(boundaries);

            newBoundary[k_MaxDiameter][0] = newBound;

            LinearSayeSpace<T> sibling = new LinearSayeSpace<T>(newBoundary, dim, this.removedDims.CloneAs());
            sibling.psiAndS = new List<Tuple<LinearPSI<T>, int>>(this.psiAndS);
            sibling.subdivided = true;
            sibling.Surface = this.surface;

            return sibling;
        }

        private void RecalculateCenter()
        {
            double[] centerArr = new double[refElement.SpatialDimension];
            for(int i = 0; i < refElement.SpatialDimension; ++i)
            {
                centerArr[i] = (boundaries[i][0] + boundaries[i][1]) / 2.0;
            }
            this.center = new NodeSet(refElement, centerArr, false);
            this.center.LockForever();
        }

        double[][] boundaries;

        private void SetBoundaries(double[][] _Boundaries)
        {
            boundaries = _Boundaries;
            diameters = new double[refElement.SpatialDimension];
            for(int i = 0; i < refElement.SpatialDimension; ++i)
            {
                diameters[i] = (_Boundaries[i][1] - _Boundaries[i][0]) / 2;
            }
            RecalculateCenter();
        }

        public double[][] Boundaries {
            get => boundaries;
        }

        public double[] GetBoundaries(int dimension)
        {
            return boundaries[dimension];
        }

        double[] diameters;

        public double[] Diameters {
            get => diameters;
        }

        public bool DimActive(int direction)
        {
            return !removedDims[direction];
        }

        #region ISayeArgument

        NodesAndWeights data;

        public void Reset()
        {
            //data.Reset();
        }

        public override ISayeQuadRule NodesAndWeights {
            get => data;
        }

        int heightDirection;
        public override int HeightDirection {
            get {
                return heightDirection;
            }
            set {
                heightDirection = value;
            }
        }

        bool surface;

        public override bool Surface {
            get => surface;
            set => surface = value;
        }

        public override void RemoveDimension(int k)
        {
            --dim;
            Debug.Assert(removedDims[k] == false);
            removedDims[k] = true;
            if (Dimension == 1)
            {
                heightDirection = 0;
                while (removedDims[heightDirection])
                {
                    ++heightDirection;
                }
            }
        }

        NodeSet center;

        public override NodeSet GetCellCenter()
        {
            NodeSet _center;
            if (!subdivided)
            {
                _center = refElement.Center;
            }
            else
            {
                _center = this.center;
            }

            return _center;
        }

        int dim;

        public override int Dimension => dim;

        public override IList<Tuple<LinearPSI<T>, int>> PsiAndS {
            get => psiAndS;
        }

        public override int n => psiAndS.Count;

        #endregion
    }

    class TreeNode<T>
    {
        LinkedList<TreeNode<T>> children = new LinkedList<TreeNode<T>>();
        TreeNode<T> parent;
        T value;
        public int level { get; }

        public TreeNode()
        {
            value = default(T);
            parent = null;
            level = 0;
        }

        public TreeNode(T Value)
        {
            value = Value;
            parent = null;
            level = 0;
        }

        public TreeNode(T Value, TreeNode<T> Parent)
        {
            value = Value;
            parent = Parent;
            level = parent.level + 1;
        }

        public TreeNode<T> AddSibling(T Sibling)
        {
            if (parent != null)
            {
                TreeNode<T> sibling = parent.AddChild(Sibling);
                return sibling;
            }
            return null;
        }

        public TreeNode<T> AddChild(T Child)
        {
            TreeNode<T> childNode = new TreeNode<T>(Child, this);
            children.AddFirst(childNode);
            return childNode;
        }

        public LinkedList<TreeNode<T>> Children {
            get {
                return children;
            }
        }

        public T Value {
            get { return value; }
        }

        public int SumOverTree(Func<T, int, int> sumFunc)
        {
            int n = 0; //Holds the sum of function( children)
            foreach (TreeNode<T> child in this.Children)
            {
                n += child.SumOverTree(sumFunc);
            }
            if (this.value == null)
                return n;
            return sumFunc(this.value, n);
        }

        public void UnrollFunc(Action<TreeNode<T>> Func)
        {
            foreach (TreeNode<T> child in this.Children)
            {
                child.UnrollFunc(Func);
            }
            Func(this);
        }
    }

    class SayeQuadRule //: IEnumerable<Tuple<MultidimensionalArray, double>>
    {
        private SayeQuadRule() { }

        public SayeQuadRule(MultidimensionalArray _Nodes, MultidimensionalArray _Weights, RefElement refElement)
        {
            Nodes = _Nodes;
            Weights = _Weights;
            RefElement = refElement;
        }

        public MultidimensionalArray Nodes;

        public MultidimensionalArray Weights;

        public RefElement RefElement;

        public SayeQuadRule Clone()
        {
            return new SayeQuadRule()
            {
                Nodes = this.Nodes.CloneAs(),
                Weights = this.Weights.CloneAs()
            };
        }

        public int NoOfNodes 
        {
            get 
            { 
                return Nodes.Lengths[0];
            }
        }   
    }

    class NodesAndWeights : ISayeQuadRule {
        
        LinkedList<SayeQuadRule> nodes;

        int spatialDim { 
            get { return refElement.SpatialDimension; } }

        RefElement refElement;

        public NodesAndWeights() {
            nodes = new LinkedList<SayeQuadRule>();
        }

        public NodesAndWeights(RefElement refElement) {
            nodes = new LinkedList<SayeQuadRule>();
            this.refElement = refElement;
        }

        public IEnumerable<Tuple<MultidimensionalArray, double>> IntegrationNodes {
            get {
                foreach(SayeQuadRule rule in nodes) {
                    for (int i = 0; i < rule.NoOfNodes; ++i) {
                        MultidimensionalArray node = rule.Nodes.ExtractSubArrayShallow(new int[] { i, -1 }).ResizeShallow(new int[] { 1, spatialDim });
                        double weight = rule.Weights[i];
                        Tuple<MultidimensionalArray, double> integrationNode = new Tuple<MultidimensionalArray, double>(node, weight);
                        yield return integrationNode;
                    }
                }
            }
        }

        public void AddRule(SayeQuadRule rule) {
            if(rule.RefElement != refElement) {
                throw new Exception("Wrong ref element");
            }
            nodes.AddLast(rule);
        }

        public QuadRule GetQuadRule() {
            int count = 0;
            foreach (SayeQuadRule rule in this.nodes) {
                count += rule.NoOfNodes;
            }

            MultidimensionalArray nodes = MultidimensionalArray.Create(new int[] { count, spatialDim });
            MultidimensionalArray weights = MultidimensionalArray.Create(new int[] { count });

            int rowPointer = 0;
            foreach(SayeQuadRule rule in this.nodes) {
                nodes.SetSubArray(rule.Nodes, new int[] { rowPointer, 0  }, new int[] { rowPointer + rule.NoOfNodes - 1, spatialDim - 1});
                weights.SetSubArray(rule.Weights, new int[] { rowPointer }, new int[] { rowPointer + rule.NoOfNodes - 1});
                rowPointer += rule.NoOfNodes;
            }

            QuadRule RuleToRuleThemAll = QuadRule.CreateEmpty(refElement, count, spatialDim);
            RuleToRuleThemAll.Nodes = new NodeSet(refElement, nodes, true);
            RuleToRuleThemAll.Weights = weights;
            return RuleToRuleThemAll;
        }

        public IEnumerable<SayeQuadRule> GetRules() {
            return nodes;
        }

    }

    class SayeSortedList
        : ISortedBoundedList<double>
    {
        List<double> list;
        double min;
        double max;
        readonly double tolerance;

        public SayeSortedList(int initialSize, double Tolerance)
        {
            list = new List<double>( initialSize);
            tolerance = Tolerance;
        }

        public void SetBounds(double Min, double Max)
        {
            min = Min;
            max = Max;
            list.Add(min);
            list.Add(max);
        }

        public void Add(params double[] arr)
        {
           foreach (double entry in arr)
            {
                if (entry > min && entry < max )
                {
                    for (int i = 1; i < list.Count ; ++i)
                    {
                        if (Math.Abs(entry - list[i]) < tolerance)
                        {
                            break;
                        }
                        else if (entry < list[i]) 
                        {
                            list.Insert(i, entry);
                            break;
                        }
                    }
                }
            }
        }

        public double this[int index] {
            get {
                return list.ElementAt(index);
            } 
        }

        public IEnumerator<double> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }
    }

    static class Extensions
    {
        public static T[][] Copy<T>(this T[][] arr) 
        {
            T[][] copy = new T[arr.Length][];
            for(int i = 0; i < arr.Length; ++i)
            {
                copy[i] = new T[arr[i].Length];
                Array.Copy(arr[i], copy[i], arr[i].Length);
            }
            return copy;
        }
    }
}
