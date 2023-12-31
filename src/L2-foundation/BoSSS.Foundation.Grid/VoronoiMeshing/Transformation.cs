﻿using BoSSS.Platform.LinAlg;
using ilPSP;
using System;

namespace BoSSS.Foundation.Grid.Voronoi.Meshing
{
    class Transformation
    {
        readonly int dim;

        MultidimensionalArray matrix;

        Vector affineTransformation;

        public Transformation(int dim)
        {
            this.dim = dim;
        }

        public MultidimensionalArray Matrix {
            get {
                return matrix;
            }
            set {
                if (value.Dimension != dim && value.Lengths[1] != dim)
                {
                    throw new Exception("Mismatching dimensions");
                }
                matrix = value;
            }
        }

        public Vector AffineTransformation {
            get {
                return affineTransformation;
            }
            set {
                if (value.Dim != dim)
                {
                    throw new Exception("Mismatching dimensions");
                }
                affineTransformation = value;
            }
        }

        public Vector Transform(Vector vtx)
        {
            Vector result = Matrix * vtx + AffineTransformation;
            return result;
        }

        /// <summary>
        ///Combine a(x) and b(x) to c(x) = a(b(x))
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Transformation Combine(Transformation a, Transformation b)
        {
            if(a.dim != b.dim)
            {
                throw new NotSupportedException("Transformation dimensions do not align.");
            }

            Transformation combination = new Transformation(a.dim)
            {
                matrix = a.matrix * b.matrix,
                affineTransformation = a.matrix * b.affineTransformation + a.affineTransformation
            };
            return combination;
        }
    }
}
