﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ilPSP.Connectors.Matlab {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resource1 {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resource1() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("ilPSP.Connectors.Matlab.Resource1", typeof(Resource1).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to function Mtx = ReadMsr(filename)
        /// 
        ///fid = fopen(filename);
        ///
        ///% matrix dimensions
        ///% -----------------
        ///NoOfRows = fscanf(fid,&apos;%d&apos;,1);
        ///NoOfCols = fscanf(fid,&apos;%d&apos;,1);
        ///NonZeros = fscanf(fid,&apos;%d&apos;,1);
        ///cnt = 1;
        ///
        ///% read row and column array
        ///% -------------------------
        ///iCol = zeros(NonZeros,1);
        ///iRow = zeros(NonZeros,1);
        ///entries = zeros(NonZeros,1);
        ///l0 = 0;
        ///str = char(zeros(1,6));
        ///for i = 1:NoOfRows
        ///    NonZerosInRow = fscanf(fid,&apos;%d&apos;,1);
        ///    if(l0 ~= NonZerosInRow)
        ///        str = char(zeros(1,NonZer [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ReadMsr {
            get {
                return ResourceManager.GetString("ReadMsr", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to function [] = SaveVoronoi(C, filename)
        ///fileID = fopen(filename, &apos;w&apos;);
        ///[NoOfRows,NoOfCols] = size(C);
        ///
        ///for iRow = 1:NoOfRows
        ///	R = C{iRow,1};
        ///	len = length(R);
        ///	for j = 1:len
        ///	    fprintf(fileID, &apos;%d &apos;, R(j));
        ///	end
        ///	
        ///	if(iRow &lt; NoOfRows)
        ///		fprintf(fileID, &apos;\n&apos;);
        ///	end
        ///end
        ///
        ///fclose(fileID);
        ///end.
        /// </summary>
        internal static string SaveVoronoi {
            get {
                return ResourceManager.GetString("SaveVoronoi", resourceCulture);
            }
        }
    }
}
