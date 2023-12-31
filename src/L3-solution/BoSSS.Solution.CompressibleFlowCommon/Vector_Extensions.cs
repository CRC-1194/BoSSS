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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using BoSSS.Platform.LinAlg;
using ilPSP;
using ilPSP.Utils;

namespace BoSSS.Solution.CompressibleFlowCommon {

    /// <summary>
    /// Extension methods for the <see cref="Vector"/> structure
    /// </summary>
    static public class Vector_Extensions {

        /// <summary>
        /// Computes the transformation for a arbitrary vector from the
        /// standard coordinate system into a coordinate system with the axes
        /// \f$  \vec{e}_1 = \vec{n}\f$ ,
        /// \f$  \vec{e}_2 = (-n_2, n_1, 0)^T\f$  and
        /// \f$  \vec{e}_3 = \vec{e}_1 \times \vec{e}_2\f$ ,
        /// where \f$  \vec{n} = (n_1, n_2, n_3)^T\f$ 
        /// is given by <paramref name="_edgeNormal"/>.
        /// </summary>
        /// <param name="_edgeNormal">
        /// The normal of an edge
        /// </param>
        /// <returns>
        /// An orthonormal 3x3 matrix that, when applied to a vector,
        /// transforms this vector into the above-mentioned coordinate system.
        /// </returns>
        static private MultidimensionalArray GetTransformationToEdgeCoordinates(Vector _edgeNormal) {
            int D = _edgeNormal.Dim;
            Vector edgeNormal = new Vector(_edgeNormal.x, _edgeNormal.y, _edgeNormal.z);

            Vector t1 = new Vector(D > 1 ? -edgeNormal[1] : 0.0, edgeNormal[0], 0.0);
            Vector t2 = edgeNormal.CrossProduct(t1);
            MultidimensionalArray trafo = MultidimensionalArray.Create(3, 3);
            for (int i = 0; i < 3; i++) {
                trafo[0, i] = edgeNormal[i];
                trafo[1, i] = t1[i];
                trafo[2, i] = t2[i];
            }

            return trafo;
        }

        /// <summary>
        /// Transforms this vector into a new vector in a coordinate system whose
        /// first axis is aligned with the given <paramref name="edgeNormal"/>.
        /// </summary>
        /// <param name="edgeNormal">
        /// The normal of an edge
        /// </param>
        /// <param name="_this"></param>
        /// <returns>
        /// This vector in a coordinate system with the axes
        /// \f$  \vec{e}_1 = \vec{n}\f$ ,
        /// \f$  \vec{e}_2 = (-n_2, n_1, 0)^T\f$  and
        /// \f$  \vec{e}_3 = \vec{e}_1 \times \vec{e}_2\f$ ,
        /// where \f$  \vec{n} = (n_1, n_2, n_3)^T\f$ 
        /// is given by <paramref name="edgeNormal"/>
        /// </returns>
        static public Vector ToEdgeCoordinates(this Vector _this, Vector edgeNormal) {
            if (_this.Dim != edgeNormal.Dim)
                throw new ArgumentException();

            double[] transformedVector = new double[3];
            double[] __this = new double[] { _this.x, _this.y, _this.z };

            GetTransformationToEdgeCoordinates(edgeNormal).GEMV(
                1.0, __this, 0.0, transformedVector);

            //Debug.Assert(transformedVector.Skip(edgeNormal.Dim).L2Norm() == 0.0);
            var R = new Vector(transformedVector.GetSubVector(0, edgeNormal.Dim));
            return R;
        }

        /// <summary>
        /// Transforms this vector from a coordinate system as defined by
        /// <see cref="ToEdgeCoordinates"/> into a new vector in the standard
        /// coordinate system
        /// </summary>
        /// <param name="edgeNormal">
        /// The normal of an edge
        /// </param>
        /// <param name="_this"></param>
        /// <returns>
        /// This vector in the standard coordinate system
        /// </returns>
        static public Vector FromEdgeCoordinates(this Vector _this, Vector edgeNormal) {
            if (_this.Dim != edgeNormal.Dim)
                throw new ArgumentException();

            double[] transformedVector = new double[3];
            double[] __this =  new double[] { _this.x, _this.y, _this.z };

            GetTransformationToEdgeCoordinates(edgeNormal).TransposeTo().GEMV(
                1.0, __this, 0.0, transformedVector);

            //Debug.Assert(transformedVector.Skip(edgeNormal.Dim).L2Norm() == 0.0);
            var R = new Vector(transformedVector.GetSubVector(0, edgeNormal.Dim));
            return R;
        }
    }
}