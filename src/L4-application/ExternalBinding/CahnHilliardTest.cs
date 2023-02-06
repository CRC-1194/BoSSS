using ilPSP;
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Solution.Utils;
using BoSSS.Solution.Statistic;
using ilPSP.Utils;
using NUnit.Framework;

namespace BoSSS.Application.ExternalBinding {

    [TestFixture]
    static public class CahnHilliardTest {


        public static SinglePhaseField RunDropletTest(string GridPath, string PlotTargetDir = "./plots/", FixedOperators chOp = null) {
            Init();
            GridImportTest.ConvertFOAMGrid();
            Console.WriteLine("Running Cahn-Hilliard Droplet Test");
            if (chOp == null) {
                chOp = new FixedOperators();
            }
            OpenFOAMGrid grd = GridImportFromDirectory.GenerateFOAMGrid(GridPath);
            OpenFoamDGField f = new OpenFoamDGField(grd, 2, 2);
            OpenFoamMatrix mtx = new OpenFoamMatrix(grd, f);
            OpenFoamPatchField cPtch;
            int[] safeEts = new int[] { 1, 2, 3 };
            string[] safeEtyps = new string[] { "neumann", "neumann", "neumann" };
            double[] safeVals = new double[] { 0.0, 0.0, 0 };
            cPtch = new OpenFoamPatchField(grd, 1, safeEts, safeEtyps, safeVals);

            double[] safeValsU = new double[] { 1.0, -1.0, 0, 0, 0, 0, 0, 0, 0 };
            OpenFoamPatchField uPtch = new OpenFoamPatchField(grd, 3, safeEts, safeEtyps, safeValsU);
            OpenFoamDGField U = new OpenFoamDGField(grd, 2, 3);

            int noOfTotalCells = grd.GridData.Grid.NumberOfCells;
            // ScalarFunction func()
            // {
            //     double radius = 7;
            //     // return ((_3D)((x, y, z) => Math.Tanh((-Math.Sqrt(Math.Pow(x - 1.0e-3, 2) + Math.Pow(z - 0.0e-3, 2)) + Math.Pow(radius, 1)) * 50000))).Vectorize();
            //     return ((_3D)((x, y, z) => Math.Tanh((-Math.Sqrt(Math.Pow(x, 2) + Math.Pow(z, 2)) + Math.Pow(radius, 1)) * Math.Sqrt(2)))).Vectorize();
            // }

            chOp.CahnHilliard(mtx, U, cPtch, uPtch);

            var field = new SinglePhaseField(mtx.ColMap.BasisS[0], "c");
            field.Acc(1.0, mtx.Fields[0].Fields[0] as SinglePhaseField);

            // move all plt files into their own directory before starting the next calculation
            var source = new System.IO.DirectoryInfo("./");
            System.IO.FileInfo[] files = source.GetFiles("*.plt");
            var targetPath = PlotTargetDir;
            System.IO.Directory.CreateDirectory(targetPath);
            foreach (var file in files)
            {
                file.MoveTo(targetPath + "/" + file.Name, true);
            }
            return field;
        }
        // [Test]
        public static void DropletTest() {

            Init();

            GridImportTest.ConvertFOAMGrid();
            Console.WriteLine("Running Cahn-Hilliard Test");
            // OpenFOAMGrid grd = GridImportTestSmall.GenerateFOAMGrid();
            string smallGrd = "./meshes/big/small/polyMesh/";
            string mediumGrd = "./meshes/big/medium/polyMesh/";
            string largeGrd = "./meshes/big/large/polyMesh/";
            var chOp = new FixedOperators();
            var normRelChanges = new List<double>();
            var jumpNorms = new List<double>();
            // OpenFOAMGrid grd = GridImportTest.GenerateFOAMGrid();
            // var EdgeValues = new List<List<double>>();
            // foreach (var val in new double[]{1, -1, 0}){
            //     EdgeValues.Add(new List<double>{val});
            // }
            // OpenFoamPatchField cPtch = new(grd, 1, new int[]{1,2,3}, new string[]{"dirichlet","dirichlet","neumann"}, new double[]{1,-1,0});
            int i = 0;
            // foreach (var grd in new List<OpenFOAMGrid>{smallGrd, mediumGrd, largeGrd}){
            foreach (var grd in new List<string>{smallGrd, mediumGrd}){
            // i++;
            // foreach (var grd in new List<string>{mediumGrd}){

                RunDropletTest(grd, new List<string>{"./small/", "./medium/", "./large/"}[i]);
                ScalarFunction func()
                {
                    double radius = 7;
                    // return ((_3D)((x, y, z) => Math.Tanh((-Math.Sqrt(Math.Pow(x - 1.0e-3, 2) + Math.Pow(z - 0.0e-3, 2)) + Math.Pow(radius, 1)) * 50000))).Vectorize();
                    return ((_3D)((x, y, z) => Math.Tanh((-Math.Sqrt(Math.Pow(x, 2) + Math.Pow(z, 2)) + Math.Pow(radius, 1)) * Math.Sqrt(2)))).Vectorize();
                }
                double normRelChange = chOp.NormRelChange();
                double jumpNorm = chOp.JumpNorm();
                normRelChanges.Add(normRelChange);
                jumpNorms.Add(jumpNorm);

                // move all plt files into their own directory before starting the next calculation
                var source = new System.IO.DirectoryInfo("./");
                System.IO.FileInfo[] files = source.GetFiles("*.plt");
                var targetPath = new List<string>{"./small/", "./medium/", "./large/"}[i];
                System.IO.Directory.CreateDirectory(targetPath);
                foreach (var file in files) {
                    file.MoveTo(targetPath + file.Name, true);
                }
                i++;
            }

            Console.WriteLine("normrelchanges:");
            normRelChanges.ForEach(i => Console.WriteLine("{0}", i));
            Console.WriteLine("jumpNorms:");
            jumpNorms.ForEach(i => Console.WriteLine("{0}", i));

            // make sure it works better with a finer grid
            // Assert.IsTrue(normRelChanges[0] > normRelChanges[1]);
            Assert.IsTrue(jumpNorms[0] > jumpNorms[1]);
            // Assert.IsTrue(normRelChanges[1] > normRelChanges[2]);
            Assert.IsTrue(jumpNorms[1] > jumpNorms[2]);

            // also have some absolute constraints in place
            // Assert.IsTrue(normRelChanges[2] < 1e-2);
            Assert.IsTrue(jumpNorms[2] < 1e-3);

            Cleanup();

        }
        [NUnitFileToCopyHack(
        "../src/L4-application/ExternalBinding/meshes/big/small/polyMesh/boundarySmall",
        "../src/L4-application/ExternalBinding/meshes/big/small/polyMesh/facesSmall",
        "../src/L4-application/ExternalBinding/meshes/big/small/polyMesh/neighbourSmall",
        "../src/L4-application/ExternalBinding/meshes/big/small/polyMesh/ownerSmall",
        "../src/L4-application/ExternalBinding/meshes/big/small/polyMesh/pointsSmall",

        "../src/L4-application/ExternalBinding/meshes/big/medium/polyMesh/boundaryMedium",
        "../src/L4-application/ExternalBinding/meshes/big/medium/polyMesh/facesMedium",
        "../src/L4-application/ExternalBinding/meshes/big/medium/polyMesh/neighbourMedium",
        "../src/L4-application/ExternalBinding/meshes/big/medium/polyMesh/ownerMedium",
        "../src/L4-application/ExternalBinding/meshes/big/medium/polyMesh/pointsMedium",

        "../src/L4-application/ExternalBinding/meshes/big/large/polyMesh/boundaryLarge",
        "../src/L4-application/ExternalBinding/meshes/big/large/polyMesh/facesLarge",
        "../src/L4-application/ExternalBinding/meshes/big/large/polyMesh/neighbourLarge",
        "../src/L4-application/ExternalBinding/meshes/big/large/polyMesh/ownerLarge",
        "../src/L4-application/ExternalBinding/meshes/big/large/polyMesh/pointsLarge"
            )]
        [Test]
        public static void ConvergenceTest() {

            Console.WriteLine("Running Cahn-Hilliard Test");
            var chOp = new FixedOperators();
            // OpenFOAMGrid grd = GridImportTestSmall.GenerateFOAMGrid();
            string currentDirectory = "";
            string smallGrd = currentDirectory + "./meshes/big/small/polyMesh/";
            string mediumGrd = currentDirectory + "./meshes/big/medium/polyMesh/";
            string largeGrd = currentDirectory + "./meshes/big/large/polyMesh/";
            // string smallGrd = "/home/klingenberg/Documents-work/programming/foam-dg/foam-dg/run/dummyConvAnalysis/small/constant/polyMesh/";
            // string mediumGrd = "/home/klingenberg/Documents-work/programming/foam-dg/foam-dg/run/dummyConvAnalysis/medium/constant/polyMesh/";
            // string largeGrd = "/home/klingenberg/Documents-work/programming/foam-dg/foam-dg/run/dummyConvAnalysis/large/constant/polyMesh/";
            int i = 0;
            List<IEnumerable<DGField>> solutionOnDifferentResolutions = new List<IEnumerable<DGField>>();
            List<DGField> solutionOnDifferentResolutions2 = new List<DGField>();
            foreach (var grd in new List<string>{smallGrd, mediumGrd, largeGrd}){
            // foreach (var grd in new List<OpenFOAMGrid>{smallGrd, mediumGrd}){
                var field = RunDropletTest(grd, new List<string>{"./small/", "./medium/", "./large/"}[i], chOp);
                solutionOnDifferentResolutions.Add(new DGField[]{field});
                solutionOnDifferentResolutions2.Add(field);
            }
            DGFieldComparison.ComputeErrors(
                solutionOnDifferentResolutions, out var hS, out var DOFs, out var errorS, NormType.L2_embedded);
            var slope = (Math.Log(errorS["c"][1]) - Math.Log(errorS["c"][0]))/(Math.Log(Math.Sqrt(1.0/DOFs["c"][1])) - Math.Log(Math.Sqrt(1.0/DOFs["c"][0])));
            Console.WriteLine("Slope: " + slope);
            Console.WriteLine("DOFs[0]: " + DOFs["c"][0]);
            Console.WriteLine("DOFs[1]: " + DOFs["c"][1]);
            Console.WriteLine("errorsS[0]: " + errorS["c"][0]);
            Console.WriteLine("errorsS[1]: " + errorS["c"][1]);
            Assert.IsTrue(slope >= 2.0);

            Cleanup();

        }

        public static void Main() {

            // GridImportTest.ConvertFOAMGrid();
            string currentDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string grd = currentDirectory + "/../../../meshes/big/small/polyMesh/";
            RunDropletTest(grd);

            // DropletTest();
            // ConvergenceTest();
            // Init();

            // GridImportTest.ConvertFOAMGrid();
            // Console.WriteLine("Running Cahn-Hilliard Test");
            // var chOp = new FixedOperators();
            // // OpenFOAMGrid grd = GridImportTestSmall.GenerateFOAMGrid();
            // // OpenFOAMGrid grd = GridImportFromDirectory.GenerateFOAMGrid("./meshes/big/large/polyMesh/");
            // OpenFOAMGrid grd = GridImportFromDirectory.GenerateFOAMGrid("./meshes/big/medium/polyMesh/");
            // // OpenFOAMGrid grd = GridImportFromDirectory.GenerateFOAMGrid("./meshes/big/small/polyMesh/");
            // var norms = new List<double>();
            // var preNorms = new List<double>();
            // var normRelChanges = new List<double>();
            // var jumpNorms = new List<double>();
            // OpenFoamDGField f = new OpenFoamDGField(grd, 2, 2);
            // OpenFoamMatrix mtx = new OpenFoamMatrix(grd, f);
            // OpenFoamPatchField cPtch;
            // int[] safeEts = new int[] { 1, 2, 3 };
            // // int* ets = (int*)safeEts[0];
            // string[] safeEtyps = new string[] { "neumann", "neumann", "neumann" };
            // // string[] safeEtyps = new string[] { "dirichlet", "dirichlet", "neumann" };
            // // int* eTyps = (int*)safeEtyps[0];
            // double[] safeVals = new double[] { 0.0, 0.0, 0 };
            // // double[] safeVals = new double[] { -1.0, -1.0, 0 };
            // cPtch = new OpenFoamPatchField(grd, 1, safeEts, safeEtyps, safeVals);

            // double[] safeValsU = new double[] { 1.0, -1.0, 0, 0, 0, 0, 0, 0, 0 };
            // OpenFoamPatchField uPtch = new OpenFoamPatchField(grd, 3, safeEts, safeEtyps, safeValsU);
            // OpenFoamDGField U = new OpenFoamDGField(grd, 2, 3);

            // int noOfTotalCells = grd.GridData.Grid.NumberOfCells;
            // ScalarFunction func()
            // {
            //     double radius = 7;
            //     // double radius = rMin * 1.3;
            //     // return ((_3D)((x, y, z) => Math.Tanh((-Math.Sqrt(Math.Pow(x - 1.0e-3, 2) + Math.Pow(z - 0.0e-3, 2)) + Math.Pow(radius, 1)) * 50000))).Vectorize();
            //     return ((_3D)((x, y, z) => Math.Tanh((-Math.Sqrt(Math.Pow(x, 2) + Math.Pow(z - 0.0e-3, 2)) + Math.Pow(radius, 1)) * Math.Sqrt(2)))).Vectorize();
            // }

            // double preNorm = chOp.Norm(mtx, func());
            // chOp.CahnHilliard(mtx, U, cPtch, uPtch, func());
            // double postNorm = chOp.Norm();
            // double normRelChange = Math.Abs((postNorm - preNorm) / preNorm);
            // double jumpNorm = chOp.JumpNorm();
            // preNorms.Add(preNorm);
            // norms.Add(postNorm);
            // jumpNorms.Add(jumpNorm);
            // normRelChanges.Add(normRelChange);

            // // move all plt files into their own directory before starting the next calculation
            // // var source = new System.IO.DirectoryInfo("./");
            // // System.IO.FileInfo[] files = source.GetFiles("*.plt");
            // // var targetPath = "./big/";
            // // System.IO.Directory.CreateDirectory(targetPath);
            // // foreach (var file in files)
            // // {
            // //     file.MoveTo(targetPath + file.Name, true);
            // // }
            // // i++;

            // Console.WriteLine("preNorms:");
            // preNorms.ForEach(i => Console.WriteLine("{0}", i));
            // Console.WriteLine("postNorms:");
            // norms.ForEach(i => Console.WriteLine("{0}", i));
            // Console.WriteLine("normrelchanges:");
            // normRelChanges.ForEach(i => Console.WriteLine("{0}", i));
            // Console.WriteLine("jumpNorms:");
            // jumpNorms.ForEach(i => Console.WriteLine("{0}", i));

            // Cleanup();

        }

        static Initializer MyInit;

        /// <summary>
        /// MPI Init
        /// </summary>
        public static void Init() {
            MyInit = new Initializer();
            MyInit.BoSSSInitialize();

            // recover directory structure
            Directory.CreateDirectory("./meshes/");
            Directory.CreateDirectory("./meshes/big/");
            Directory.CreateDirectory("./meshes/big/small/");
            Directory.CreateDirectory("./meshes/big/small/polyMesh/");
            Directory.CreateDirectory("./meshes/big/medium/");
            Directory.CreateDirectory("./meshes/big/medium/polyMesh/");
            Directory.CreateDirectory("./meshes/big/large/");
            Directory.CreateDirectory("./meshes/big/large/polyMesh/");
            File.Copy("boundarySmall", "./meshes/big/small/polyMesh/");
            File.Copy("facesSmall", "./meshes/big/small/polyMesh/");
            File.Copy("neighbourSmall", "./meshes/big/small/polyMesh/");
            File.Copy("ownerSmall", "./meshes/big/small/polyMesh/");
            File.Copy("pointsSmall", "./meshes/big/small/polyMesh/");

            File.Copy("boundaryMedium", "./meshes/big/medium/polyMesh/");
            File.Copy("facesMedium", "./meshes/big/medium/polyMesh/");
            File.Copy("neighbourMedium", "./meshes/big/medium/polyMesh/");
            File.Copy("ownerMedium", "./meshes/big/medium/polyMesh/");
            File.Copy("pointsMedium", "./meshes/big/medium/polyMesh/");

            File.Copy("boundaryLarge", "./meshes/big/large/polyMesh/");
            File.Copy("facesLarge", "./meshes/big/large/polyMesh/");
            File.Copy("neighbourLarge", "./meshes/big/large/polyMesh/");
            File.Copy("ownerLarge", "./meshes/big/large/polyMesh/");
            File.Copy("pointsLarge", "./meshes/big/large/polyMesh/");
        }

        /// <summary>
        /// MPI shutdown
        /// </summary>
        public static void Cleanup() {
            MyInit.BoSSSFinalize();
        }
    }
}
