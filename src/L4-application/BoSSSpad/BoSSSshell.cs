﻿using BoSSS.Foundation;
using BoSSS.Foundation.Grid;
using BoSSS.Foundation.Grid.Classic;
using BoSSS.Foundation.IO;
using BoSSS.Solution.Gnuplot;
using BoSSS.Solution.GridImport;
using ilPSP;
using ilPSP.LinSolvers;
using ilPSP.Utils;
using Microsoft.DotNet.Interactive.Formatting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace BoSSS.Application.BoSSSpad {
    
    /// <summary>
    /// Standard commands to be used in the Jupyter Notebooks **with a C# - kernel**, of course.
    /// </summary>
    static public class BoSSSshell {

        /// <summary>
        /// Provides a help text
        /// </summary>
        public static string help {
            get {
                try {
                    string dbeCommandOverviewDocPath = Path.Combine(
                        Utils.GetBoSSSInstallDir(),
                        "doc",
                        "BoSSSPad_Command_Overview.pdf");
                    System.Diagnostics.Process.Start(dbeCommandOverviewDocPath);
                    return "Displaying external help document...";
                } catch (Exception e) {
                    return "Displaying external help failed ( " + e.GetType().Name + ": " + e.Message + ")";
                }
            }
        }

        /// <summary>
        /// <see cref="help"/>
        /// </summary>
        public static string Help {
            get {
                return help;
            }
        }


        /// <summary>
        /// Called on top of the Jupyter notebook, to initialize the BoSSS functionality
        /// </summary>
        /// <remarks>
        /// The following startup command in a Jupyter notebook are recommended:
        /// ```csharp
        /// #r "C:\  ....  \Release\BoSSSpad.exe"
        /// using static BoSSS.Application.BoSSSpad.BoSSSshell;
        /// Init();
        /// ```
        /// </remarks>
        public static void Init() {
            BoSSSpadGnuplotExtensions.PlotMode = PlotNowMode.SVG;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            BoSSS.Solution.Application.InitMPI();
            CallRandomStuff();
            try {



                databases = DatabaseController.LoadDatabaseInfosFromXML();

                ReloadExecutionQueues();
                string summary = databases.Summary();
                Console.WriteLine("Databases loaded: ");
                Console.WriteLine(summary);
            } catch(Exception e) {
                Console.WriteLine();
                Console.WriteLine(
                    "{0} occurred with message '{1}' while loading the databases.",
                     e.GetType(),
                     e.Message);
                InteractiveShell.LastError = e;
            }


            AddObjectFormatter<SinglePhaseField>();
            AddObjectFormatter<Foundation.XDG.XDGField>();
            AddObjectFormatter<Foundation.XDG.XDGField.SpeciesShadowField>();
            AddObjectFormatter<IGridData>();

            AddObjectFormatter<ISessionInfo>();
            AddObjectFormatter<IDatabaseInfo>();
            AddObjectFormatter<ITimestepInfo>();

            AddEnumFormatter<IGridInfo>();
            AddEnumFormatter<IDatabaseInfo>();
            AddEnumFormatter<ISessionInfo>();
            AddEnumFormatter<ITimestepInfo>();

            AddDictFormatter<string, Job>();
            AddDictFormatter<string, IEnumerable<ISessionInfo>>(optValFormatter: (SessionEnum => SessionEnum.Count() + " sessions"));
            AddObjectFormatter<Job>();
            AddEnumFormatter<Job>();
        }

        /// <summary>
        /// calls random functions from BoSSS libraries to enforce loading of assemblies;
        /// seems to be required for JSON serialization in order to resolve classes/assemblies
        /// </summary>
        static void CallRandomStuff() {
            new BatchProcessorConfig();
            
            new BoSSS.Solution.Control.Formula("X => Math.Sin(X[0])");

            var g = Grid2D.Cartesian2DGrid(GenericBlas.Linspace(-1, 1, 4), GenericBlas.Linspace(-1, 1, 4));
            var u = new SinglePhaseField(new Basis(g, 1), "u");

            var mtx = new BlockMsrMatrix(u.Mapping, u.Mapping);
            mtx.AccEyeSp(2.0);
            int L = mtx.RowPartitioning.LocalLength;
            mtx.Solve_Direct(new double[L], new double[L]);
            mtx.Solve_CG(new double[L], new double[L]);

            using(var gp = new Gnuplot()) {

            }

            var ls = new Foundation.XDG.LevelSet(new Basis(g, 2), "phi");

        }



        /// <summary>
        /// Sets Text Formatter for objects of specific type
        /// </summary>
        public static void AddObjectFormatter<T>(Func<T, string> optValFormatter = null) {
            Formatter.SetPreferredMimeTypeFor(typeof(T), "text/plain");
            Formatter.Register(
                type: typeof(T),
                formatter: (object obj, System.IO.TextWriter writer) => {

                    T v = (T)obj;

                    string valString;
                    if(optValFormatter == null)
                        valString = v != null ? v.ToString() : "NULL";
                    else
                        valString = v != null ? optValFormatter(v) : "NULL";
                    writer.Write(valString);
                });
        }

        /// <summary>
        /// Sets Text Formatter for Dictionaries of specific type: <see cref="IDictionary{TKey, TValue}"/> 
        /// </summary>
        public static void AddDictFormatter<KeyType, ValType>(Func<ValType, string> optKeyFormatter = null, Func<ValType, string> optValFormatter = null) {
            var t = typeof(IDictionary<KeyType, ValType>);

            Formatter.SetPreferredMimeTypeFor(t, "text/plain");
            Formatter.Register(
                type: t,
                formatter: (object obj, System.IO.TextWriter writer) => {
                    int i = 0;

                    var enu = (IDictionary<KeyType, ValType>)obj;

                    foreach(var kv in enu) {

                        string valString;
                        if(optValFormatter == null)
                            valString = kv.Value != null ? kv.Value.ToString() : "NULL";
                        else
                            valString = kv.Value != null ? optValFormatter(kv.Value) : "NULL";

                        string keyString;
                        if(optKeyFormatter == null)
                            keyString = kv.Key != null ? kv.Key.ToString() : "NULL";
                        else
                            keyString = kv.Key != null ? optKeyFormatter(kv.Value) : "NULL";

                        writer.WriteLine("#" + i + ": " + kv.Value + "\t" + kv.Value.ToString());
                        i++;
                    }
                });
        }

        /// <summary>
        /// Sets Text Formatter for enumerations/lists of specific type: <see cref="IEnumerable{ValType}"/> 
        /// </summary>
        public static void AddEnumFormatter<ValType>(Func<ValType, string> optValFormatter = null) {
            var t = typeof(IEnumerable<ValType>);

            Formatter.SetPreferredMimeTypeFor(t, "text/plain");
            Formatter.Register(
                type: t,
                formatter: (object obj, System.IO.TextWriter writer) => {
                    int i = 0;

                    var enu = (IEnumerable<ValType>)obj;

                    foreach(var v in enu) {
                        string valString;
                        if(optValFormatter == null)
                            valString = v != null ? v.ToString() : "NULL";
                        else
                            valString = v != null ? optValFormatter(v) : "NULL";

                        writer.WriteLine("#" + i + ": " + v.ToString());
                        i++;
                    }
                });
        }



        /// <summary>
        /// Just Demo
        /// </summary>
        public static void SayHello() {
            Console.WriteLine("Hello");
        }



        /// <summary>
        /// Opens the folder containing config files like the DBE.xml
        /// </summary>
        public static void OpenConfigDirectory() {
            string dbeXmlPath = Path.Combine(Utils.GetBoSSSUserSettingsPath(), "etc");
            Process.Start(dbeXmlPath);
        }

        /// <summary>
        /// Opens the DBE.xml
        /// </summary>
        public static void OpenConfigFile() {
            string dbeXmlPath = Path.Combine(
                Utils.GetBoSSSUserSettingsPath(), "etc", "DBE.xml");
            Process.Start(dbeXmlPath);
        }

        /// <summary>
        /// Saves the current interactive session as a worksheet that can be
        /// loaded by the worksheet edition of the BoSSSPad
        /// </summary>
        /// <param name="path"></param>
        public static void SaveSessionAsWorksheet(string path) {
            ReadEvalPrintLoop.SaveSessionAsWorksheet(path);
        }

        /// <summary>
        /// Clears the console window.
        /// </summary>
        public static void Clear() {
            Console.Clear();
        }

        internal static WorkflowMgm m_WorkflowMgm;

        /// <summary>
        /// Link to the workflow-management facility
        /// </summary>
        public static WorkflowMgm WorkflowMgm {
            get {
                if (m_WorkflowMgm == null)
                    m_WorkflowMgm = new WorkflowMgm();
                return m_WorkflowMgm;
            }
        }

        /// <summary>
        /// Alias for <see cref="WorkflowMgm"/>
        /// </summary>
        public static WorkflowMgm wmg {
            get {
                return WorkflowMgm;
            }
        }

        /// <summary>
        /// Reset states which survive a restart of the interpreter.
        /// </summary>
        static void Reset() {
            databases = new IDatabaseInfo[0];
            m_WorkflowMgm = null;
            executionQueues = null;
        }
        

        /// <summary>
        /// Prints Information on an object state.
        /// </summary>
        static public void Info(object o, int MaxRecursionDepth = 20) {
            InfoRecursive(o, 0, MaxRecursionDepth);
        }


        static void InfoRecursive(object obj, int RecursionDepth, int MaxRecursionDepth) {
            if(obj == null) {
                Console.WriteLine("Null");
                return;
            }

            if(RecursionDepth > MaxRecursionDepth) {
                Console.WriteLine(" ... no further recursion - max recursion depth reached.");
                return;
            }

            Type objT = obj.GetType();
            if ((objT.IsPrimitive || objT.IsEnum || objT == typeof(string))) {
                Console.WriteLine(obj.ToString() + " (" + objT.Name + ")");
                return;
            }

            if (objT.IsSubclassOf(typeof(System.Delegate))) {
                // unable to log delegates
                Console.WriteLine("Delegate");
                return;
            }

            void WriteSpaces() {
                //Console.WriteLine();
                for(int i = 0; i < RecursionDepth; i++)
                    Console.Write(" ");
            }


            if (obj is System.Collections.IEnumerable) {
                System.Collections.IEnumerable objEnu = (System.Collections.IEnumerable)obj;
                int cnt = 0;
                foreach (var objE in objEnu) {
                    WriteSpaces();
                    Console.Write("[{0}]: ", cnt);
                    cnt++;
                    InfoRecursive(objE, RecursionDepth + 1, MaxRecursionDepth);
                }
                return;
            }

            BindingFlags biFlags = BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty;

            MemberInfo[] PIs = objT.GetProperties(biFlags);
            MemberInfo[] FIs = objT.GetFields(biFlags);

            foreach (MemberInfo mi in ArrayTools.Cat(PIs, FIs)) {
                WriteSpaces();
                Console.Write(mi.Name + ": ");
                
                object Val;
                if (mi is PropertyInfo) {
                    PropertyInfo pi = ((PropertyInfo)mi);
                    if(!pi.CanRead) {
                        Console.WriteLine("cannot read.");
                        continue;
                    }
                    if(pi.GetIndexParameters() != null && pi.GetIndexParameters().Length > 0) {
                        // no support for indexed properties.
                        Console.WriteLine("indexed property - not supported.");
                        continue;
                    }

                    //pi.GetIndexParameters
                    try {
                        Val = pi.GetValue(obj, biFlags, null, null, null);
                    } catch (TargetInvocationException tie) {
                        Console.WriteLine(tie.GetType().Name + ": " + tie.Message);
                        continue;
                    }
                } else if (mi is FieldInfo) {
                    Val = ((FieldInfo)mi).GetValue(obj);
                } else {
                    Console.WriteLine("unsupported member type: " + mi.GetType().FullName + ".");
                    continue;
                }

                InfoRecursive(Val, RecursionDepth + 1, MaxRecursionDepth);
            }
        }


        /// <summary>
        /// All the databases; the workflow-management (see <see cref="WorkflowMgm"/>) must have access to those.
        /// </summary>
        public static IList<IDatabaseInfo> databases = new IDatabaseInfo[0];

        /// <summary>
        /// Sessions in all Databases
        /// </summary>
        static public IList<ISessionInfo> AllSessions {
            get {
                var ret = new List<ISessionInfo>();
                foreach(var db in databases) {
                    ret.AddRange(db.Sessions);
                }
                return ret;
            }
        }

        /// <summary>
        /// Grids in all Databases
        /// </summary>
        static public IList<IGridInfo> AllGrids {
            get {
                var ret = new List<IGridInfo>();
                foreach(var db in databases) {
                    ret.AddRange(db.Grids);
                }
                return ret;
            }
        }


        /// <summary>
        /// path to the default BoSSS database directory for the current user
        /// </summary>
        static public string GetDefaultDatabaseDir() {
            string basepath = null;
            basepath = System.Environment.GetEnvironmentVariable("USERPROFILE");

            if (basepath.IsEmptyOrWhite())
                basepath = System.Environment.GetEnvironmentVariable("HOME");

            string path = Path.Combine(basepath, "default_bosss_db");

            return path;
        }


        /// <summary>
        /// Similar to <see cref="OpenOrCreateDatabase"/>, but uses a default database
        /// </summary>
        static public IDatabaseInfo OpenOrCreateDefaultDatabase() {
            string path = GetDefaultDatabaseDir();
            return OpenOrCreateDatabase(path);
        }
        
        /// <summary>
        /// Creates a database in a temporary directory
        /// </summary>
        static public IDatabaseInfo CreateTempDatabase() {

            DirectoryInfo TempDir;
            {
                var rnd = new Random();
                bool Exists = false;
                do {
                    var tempPath = Path.GetTempPath();
                    var tempDir = rnd.Next().ToString();
                    TempDir = new DirectoryInfo(Path.Combine(tempPath, tempDir));
                    Exists = TempDir.Exists;
                } while (Exists == true);
            }
            
            string path = TempDir.FullName;
            return OpenOrCreateDatabase(path);
        }

        /// <summary>
        /// Opens a database at a specific path, resp. creates one if the 
        /// </summary>
        static public IDatabaseInfo OpenOrCreateDatabase(string dbDir) {
           
            return InteractiveShell.OpenOrCreateDatabase_Impl(dbDir, true);
        }

        /// <summary>
        /// Opens an existing database at a specific path
        /// </summary>
        static public IDatabaseInfo OpenDatabase(string dbDir) {
            return InteractiveShell.OpenOrCreateDatabase_Impl(dbDir, false);
        }

        /*
        static IDatabaseInfo OpenOrCreateDatabase_Impl(string dbDir, bool allowCreation) {
            foreach (var existing_dbi in InteractiveShell.databases) {
                if (existing_dbi.PathMatch(dbDir)) {
                    return existing_dbi;
                }
            }
        
            
            if (Directory.Exists(dbDir)) {
                if (!DatabaseUtils.IsValidBoSSSDatabase(dbDir)) {
                    throw new ArgumentException("Directory '" + dbDir + "' exists, but is not a valid BoSSS database.");
                }
                Console.WriteLine("Opening existing database '" + dbDir + "'.");
            } else {
                if (allowCreation) {
                    DatabaseUtils.CreateDatabase(dbDir);
                    Console.WriteLine("Creating database '" + dbDir + "'.");
                } else {
                    throw new ArgumentException("Database Directory '" + dbDir + "' does not exist.");
                }
            }

            var dbi = DatabaseInfo.Open(dbDir);

            List<IDatabaseInfo> mod_databases = new List<IDatabaseInfo>();
            if (databases != null) {
                mod_databases.AddRange(databases);
            }
            mod_databases.Add(dbi);
            databases = mod_databases.ToArray();

            return dbi;
        }
        */

        static internal Document CurrentDoc = null;

        /// <summary>
        /// Extracts the source code of some function, which can be used as an initial value or boundary condition.
        /// </summary>
        /// <param name="f">
        /// Must be the reference to a static method of a static class.
        /// </param>
        static public BoSSS.Solution.Control.Formula GetFormulaObject(Func<double[], double> f) {
            return GetFormulaObject(f, false);
        }

        /// <summary>
        /// Extracts the source code of some function, which can be used as an initial value or boundary condition.
        /// </summary>
        /// <param name="f">
        /// Must be the reference to a static method of a static class.
        /// </param>
        static public BoSSS.Solution.Control.Formula GetFormulaObject(Func<double[], double, double> f) {
            return GetFormulaObject(f, true);
        }

        private static Solution.Control.Formula GetFormulaObject(System.Delegate f, bool timedep) {
            if (CurrentDoc == null) {
                throw new NotSupportedException("Only supported when a bws-document is present (GUI or batch mode).");
            }
            if (f == null)
                throw new ArgumentNullException();
            Assembly SearchedAssembly = f.Method.DeclaringType.Assembly;

            if (SearchedAssembly == null)
                throw new ApplicationException("Unable to find some assembly for delegate.");

            string AssemblyCode = null;
            foreach (var entry in CurrentDoc.CommandAndResult) {
                if (SearchedAssembly.Equals(entry.AssemblyProduced)) {
                    AssemblyCode = entry.Command;
                }
            }
            if (AssemblyCode == null) {
                throw new ApplicationException("Unable to find code of " + SearchedAssembly.FullName);
            } else {
                //Console.WriteLine("Found code:");
                //Console.WriteLine(AssemblyCode);
            }

            var fo = new Solution.Control.Formula(f.Method.DeclaringType.Name + "." + f.Method.Name, timedep, AssemblyCode);

            return fo;
        }

        static internal string _CurrentDocFile = null;

        /// <summary>
        /// Path of the current file.
        /// </summary>
        static public string CurrentDocFile {
            get {
                if (_CurrentDocFile == null) {
                    Console.WriteLine("No current document/not saved yet.");
                    return null;
                }

                if (!File.Exists(_CurrentDocFile)) {
                    Console.WriteLine("Document path '{0}' seems non-existent.", _CurrentDocFile);
                }

                return _CurrentDocFile;
            }
        }

        /// <summary>
        /// Directory where the current file is stored.
        /// </summary>
        static public string CurrentDocDir {
            get {
                string f = CurrentDocFile;
                if (f == null)
                    return null;
                return Path.GetDirectoryName(f);
            }
        }

        /// <summary>
        /// <see cref="GridImporter.Import(string)"/>
        /// </summary>
        public static GridCommons ImportGrid(string fileName) {
            GridCommons r = GridImporter.Import(fileName);

            return r;
        }



        /// <summary>
        /// Simple plotting interface
        /// </summary>
        /// <returns>Output of <see cref="BoSSSpadGnuplotExtensions.PlotNow(Gnuplot)"/></returns>
        static public object Plot(IEnumerable<double> X1, IEnumerable<double> Y1, string Name1 = null, string Format1 = null,
            IEnumerable<double> X2 = null, IEnumerable<double> Y2 = null, string Name2 = null, string Format2 = null,
            IEnumerable<double> X3 = null, IEnumerable<double> Y3 = null, string Name3 = null, string Format3 = null,
            IEnumerable<double> X4 = null, IEnumerable<double> Y4 = null, string Name4 = null, string Format4 = null,
            IEnumerable<double> X5 = null, IEnumerable<double> Y5 = null, string Name5 = null, string Format5 = null,
            IEnumerable<double> X6 = null, IEnumerable<double> Y6 = null, string Name6 = null, string Format6 = null,
            IEnumerable<double> X7 = null, IEnumerable<double> Y7 = null, string Name7 = null, string Format7 = null,
            bool logX = false, bool logY = false) {

            using (var gp = new Gnuplot()) {
                

                IEnumerable<double>[] Xs = new[] { X1, X2, X3, X4, X5, X6, X7 };
                IEnumerable<double>[] Ys = new[] { Y1, Y2, Y3, Y4, Y5, Y6, Y7 };
                string[] Ns = new string[] { Name1, Name2, Name3, Name4, Name5, Name6, Name7 };
                string[] Fs = new string[] { Format1, Format2, Format3, Format4, Format5, Format6, Format7 };

                for (int i = 0; i < 7; i++) {
                    if (Ys[i] != null) {
                        var f1 = new PlotFormat();
                        if (Fs[i] != null)
                            f1.FromString(Fs[i]);
                        gp.PlotXY(Xs[i], Ys[i], title: Ns[i], format: f1, logX: logX, logY: logY);
                    }
                }

                return gp.PlotSVG();
            }
        }

        /// <summary>
        /// Simple plotting interface
        /// </summary>
        /// <returns>Output of <see cref="BoSSSpadGnuplotExtensions.PlotNow(Gnuplot)"/></returns>
        static public object Plot(IEnumerable<double>[] X, IEnumerable<double>[] Y, string[] Name1 = null, string[] Format1 = null,
            bool logX = false, bool logY = false) {

            using (var gp = new Gnuplot()) {

                IEnumerable<double>[] Xs = X;
                IEnumerable<double>[] Ys = Y;

                for (int i = 0; i < Xs.Length; i++) {
                    if (Ys.ElementAtOrDefault(i) != null) {
                        var f1 = new PlotFormat();
                        if (Format1?.ElementAtOrDefault(i) != null)
                            f1.FromString(Format1[i]);
                        gp.PlotXY(Xs[i], Ys[i], title: Name1?.ElementAtOrDefault(i), format: f1, logX: logX, logY: logY);
                    }
                }

                return gp.PlotSVG();
            }
        }

        /// <summary>
        /// Plotting of an grid with dummy data, 
        /// Driver interface for the <see cref="BoSSS.Solution.Tecplot.Tecplot"/> functionality.
        /// </summary>
        static public void PlotGrid(string filename, IGridData grd) {


            string SanitizeName(string s) {
                char[] ot = s.ToCharArray();
                for(int k = 0; k < ot.Length; k++) {
                    if(char.IsWhiteSpace(ot[k])) {
                        ot[k] = '_';
                    }

                    if (ot[k] == '(')
                        ot[k] = 'L';
                    if (ot[k] == ')')
                        ot[k] = 'R';
                }
                return new string(ot);
            }

           

            var et2Name = grd.EdgeTagNames;
            Console.WriteLine($"Grid containing {et2Name.Count} EdgeTag names: ");
            int i = 0;
            foreach (var t in et2Name) { // loop over all different edge tag names...
                string name = t.Value;
                byte tag2color = t.Key;

                string sname = SanitizeName(name);

                if (name.Equals(sname)) {
                    Console.WriteLine($"   {i}: {name} -- tag = {tag2color}");
                } else {
                    Console.WriteLine($"   {i}: {name} -- tag = {tag2color}   (marked as '{sname}') in output file.");
                }
                i++;
            }


            var B0 = new Basis(grd, 0);
            SinglePhaseField[] bndyMarkers = new SinglePhaseField[et2Name.Count + 1];

            int[,] Edge2GeomCell = grd.iGeomEdges.CellIndices;
            int[] G2L = grd.iGeomCells.GeomCell2LogicalCell;
            byte[] EdgeTags = grd.iGeomEdges.EdgeTags;

            i = 0;
            foreach(var t in et2Name) { // loop over all different edge tag names...
                string name = t.Value;
                byte tag2color = t.Key;
                string sname = SanitizeName(name);


                var FI = new SinglePhaseField(B0, "Marker-" + sname);
                bndyMarkers[i] = FI;
                i++;

                for (int e = 0; e < EdgeTags.Length; e++) { // loop over edges...
                    byte tag_e = EdgeTags[e];

                    if(tag_e == tag2color) {
                        // mar cells next to edge e

                        foreach(int jG in Edge2GeomCell.GetRow(e)) {
                            if (jG < 0)
                                continue;

                            // convert geometrical cell index to logical cell index
                            int jL;
                            if (G2L == null)
                                jL = jG;
                            else
                                jL = G2L[jG];

                            // color respective cell
                            FI.SetMeanValue(jL, tag2color);
                        }

                    }
                }

            }

            var dummy = new SinglePhaseField(B0, "DummyData");
            bndyMarkers[bndyMarkers.Length - 1] = dummy;

            Tecplot(filename, 0.0, 0, bndyMarkers);
        }

        /// <summary>
        /// Plotting of an grid with dummy data, 
        /// Driver interface for the <see cref="BoSSS.Solution.Tecplot.Tecplot"/> functionality.
        /// </summary>
        static public void PlotGrid(string filename, IGridInfo grdInfo) {
            if (grdInfo is IGridData gdata) {
                PlotGrid(filename, gdata);
            } else {
                Console.WriteLine("Initializing gird...");
                var dbi = grdInfo.Database;
                var drv = dbi.Controller.DBDriver;

                var grd = drv.LoadGrid(grdInfo.ID, dbi);
                var gdat = grd.iGridData;
                Console.WriteLine("done.");
                PlotGrid(filename, gdat);
            }
        }

        /// <summary>
        /// Plotting of an grid with dummy data, 
        /// Driver interface for the <see cref="BoSSS.Solution.Tecplot.Tecplot"/> functionality.
        /// </summary>
        static public void PlotGrid(string filename, IGrid grd) {
            var gdat = grd.iGridData;
            PlotGrid(filename, gdat);
        }



        /// <summary>
        /// Driver interface for the <see cref="BoSSS.Solution.Tecplot.Tecplot"/> functionality.
        /// </summary>
        static public void Tecplot(string filename, params BoSSS.Foundation.DGField[] flds) {
            if (flds == null || flds.Length <= 0) {
                Console.WriteLine("No DG fields specified - not writing any output files.");
                return;
            }

            int susamp = Math.Min(flds.Max(f => f.Basis.Degree) + 1, 4);


            Tecplot(filename, 0.0, susamp, flds);
        }


        /// <summary>
        /// Driver interface for the <see cref="BoSSS.Solution.Tecplot.Tecplot"/> functionality.
        /// </summary>
        static public void Tecplot(string filename, double time, int supersampling, params BoSSS.Foundation.DGField[] flds) {
            if (flds == null || flds.Length <= 0) {
                Console.WriteLine("No DG fields specified - not writing any output files.");
                return;
            }

            if (supersampling > 3) {
                Console.WriteLine("Plotting with a supersampling greater than 3 is deactivated because it would very likely exceed this machines memory.");
                Console.WriteLine("Higher supersampling values are supported by external plot application.");
                supersampling = 3;
            }

            string directory = Path.GetDirectoryName(filename);
            string FullPath;
            if (directory == null || directory.Length <= 0) {
                directory = CurrentDocDir ?? "";
                FullPath = Path.Combine(directory, filename);
            } else {
                FullPath = filename;
            }

            Console.WriteLine("Writing output file {0}...", FullPath);


            BoSSS.Solution.Tecplot.Tecplot.PlotFields(flds, FullPath, time, supersampling);

        }

        /// <summary>
        /// Reload from configuration file
        /// </summary>
        public static void ReloadExecutionQueues() {
            executionQueues = new List<BatchProcessorClient>();
            //Debugger.Launch();
            BatchProcessorConfig bpc;
            try {
                bpc = BatchProcessorConfig.LoadOrDefault();
               
            } catch (Exception e) {
                Console.Error.WriteLine($"{e.GetType().Name} caught while loading batch processor configuration file - using a default configuration. Message: {e.Message}");

                executionQueues.Add(new MiniBatchProcessorClient());
                return;
            }

            executionQueues.AddRange(bpc.AllQueues);
            //foreach (var q in bpc.AllQueues)
            //    _ = q.AllowedDatabases;

        }





        /// <summary>
        /// A list of predefined batch system clients.
        /// </summary>
        public static IReadOnlyList<BatchProcessorClient> ExecutionQueues {
            get {
                if(executionQueues == null) {
                    ReloadExecutionQueues();
                }

                return executionQueues.AsReadOnly();
            }
        }

        internal static List<BatchProcessorClient> executionQueues = null;

        /// <summary>
        /// Adds an entry to <see cref="ExecutionQueues"/>.
        /// </summary>
        public static int AddExecutionQueue(BatchProcessorClient bpc) {
            if(executionQueues == null)
                executionQueues = new List<BatchProcessorClient>();
            executionQueues.Add(bpc);
            return executionQueues.Count - 1;
        }

        /// <summary>
        /// Writes the configuration file according to the current status of <see cref="ExecutionQueues"/>.
        /// </summary>
        public static void SaveExecutionQueues() {
            var conf = new BatchProcessorConfig() {
                AllQueues = ExecutionQueues.ToArray()
            };

            //for(int i = 0; i < ExecutionQueues.Count; i++) {
            //    conf.AllQueus[i] = ExecutionQueues[i].GetConfig();
            //}

            BatchProcessorConfig.SaveConfiguration(conf);
        }
    }
}
