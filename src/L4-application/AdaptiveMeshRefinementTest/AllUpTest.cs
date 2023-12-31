﻿using BoSSS.Solution;
using MPI.Wrappers;
using NUnit.Framework;
using System;

namespace BoSSS.Application.AdaptiveMeshRefinementTest {

    /// <summary>
    /// Complete Test for the load balancing.
    /// </summary>
    [TestFixture]
    static public class AllUpTest {

       
        /// <summary>
        /// Da Test!
        /// </summary>
        [Test, Sequential]
        static public void RuntimeCostDynamicBalanceTest(
            [Values(2, 3, 4)] int DGdegree,
            [Values(1, 1, 2)] int MeshCaseIndex
            ) {
            AdaptiveMeshRefinementTestMain p = null;
            
            BoSSS.Solution.Application._Main(
                new string[0],
                true,
                delegate () {
                    p = new AdaptiveMeshRefinementTestMain();
                    p.DEGREE = DGdegree;
                    p.TestCase = MeshCaseIndex;
                    return p;
                });
        }
        
    }
}
