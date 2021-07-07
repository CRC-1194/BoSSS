﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using BoSSS.Foundation.IO;
using System.IO;

namespace BoSSS.Application.BoSSSpad{

    /// <summary>
    /// Entrypoint used by <see cref="ElectronWorksheet"/> project. 
    /// Realizes communication between electron BoSSSpad and C# BoSSSpad.
    /// </summary>
    public sealed class ElectronWorksheet : ResolvableAssembly {

        /// <summary>
        /// Will only work for one instance
        /// </summary>
        /// <param name="BoSSSpath">
        /// Path to the ElectronWorksheet.dll, ElectronBoSSSpad.exe and affiliated DLLs
        /// </param>
        public ElectronWorksheet(string BoSSSpath) : base(BoSSSpath){
            
            //Setup Environment
            BoSSS.Solution.Application.InitMPI();
        }

        /// <summary>
        /// 
        /// </summary>
        public Tuple<string, string> RunCommand(string command) {
            Document.Tuple singleCommandAndResult = new Document.Tuple {
                Command = command
            };
            singleCommandAndResult.Evaluate();
            String base64Result = TryConvertToBase64ImageString(singleCommandAndResult.Result);
            
            return new Tuple<string, string>(
                singleCommandAndResult.InterpreterTextOutput,
                base64Result);
        }

        string TryConvertToBase64ImageString(object result)
        {
            String base64Result = null;
            if (result != null
                && result is System.Drawing.Image img)
            {
                Byte[] resultAsByte = null;
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                {
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    resultAsByte = ms.ToArray();
                    base64Result = Convert.ToBase64String(resultAsByte);
                };
            }
            return base64Result;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Save(string path, string[] commands, string[] results) {
            //build document 
            Document document = new Document();
            for (int i = 0; i < commands.Length; ++i)
            {
                Document.Tuple commandBox = new Document.Tuple()
                {
                    Command = commands[i],
                    InterpreterTextOutput = results[i]
                };
                document.CommandAndResult.Add(commandBox);
            }

            //Save document
            document.Serialize(path);

            var fi = (new FileInfo(path));
            if (fi.Exists)
                InteractiveShell._CurrentDocFile = fi.FullName;
            else
                InteractiveShell._CurrentDocFile = null;
        }

        /// <summary>
        /// 
        /// </summary>
        public Tuple<string[], string[]> Load(string path){

            Document document = Document.Deserialize(path);
            int numberOfBoxes = document.CommandAndResult.Count;
            string[] commands = new string[numberOfBoxes];
            string[] results = new string[numberOfBoxes];
            for(int i = 0; i < numberOfBoxes; ++i){
                commands[i] = document.CommandAndResult[i].Command;
                results[i] = document.CommandAndResult[i].InterpreterTextOutput;
            }

            var fi = (new FileInfo(path));
            if (fi.Exists)
                InteractiveShell._CurrentDocFile = fi.FullName;
            else
                InteractiveShell._CurrentDocFile = null;
            return new Tuple<string[], string[]>(commands, results);
        }

        /// <summary>
        /// 
        /// </summary>
        public string[] GetAutoCompleteSuggestions(string textToBeCompleted){
            
            string[] completions = null;
            string originalPrefix = null;
            int timeout = 1000;

            if (ReadEvalPrintLoop.eval != null){
                bool completed = ReadEvalPrintLoop.eval.TryGetCompletions(
                    textToBeCompleted, out completions, out originalPrefix, timeout);   
            }

            if (completions != null){
                for (int i = 0; i < completions.Length; ++i){
                    completions[i] = originalPrefix + completions[i];
                }
            }

            return completions;
        }
    }
}

