; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyDateTimeString GetDateTimeString('yyyy-mm-dd', '', '');

#define MyAppName "BoSSS"
#define MyAppVersion MyDateTimeString;
#define MyAppPublisher "Chair of Fluid Dynamics (FDY), TU Darmstadt"
#define MyAppURL "http://www.fdy.tu-darmstadt.de/fdy/fdyresearch/bossscode/index.de.jsp"
;#define MyAppBuildNo {%BUILD_NUMBER|0}

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{4155DC6D-D5E2-43D8-A458-8ACAD4F931BC}}
AppName={#MyAppName}
; BUILD_NUMBER is a jenkins enviroment variable
AppVersion={#MyDateTimeString} ({#MyAppVersion})
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={code:DefDirRoot}\FDY\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=.\license.txt
OutputBaseFilename=BoSSS-setup-{#MyDateTimeString}
;OutputBaseFilename={%BUILD_NUMBER%|0}
Compression=lzma
SolidCompression=yes
PrivilegesRequired=none
; absolutly neccesary to forward the chnages of PATH without restart/logoff
ChangesEnvironment=yes


[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: ".\bin\*"; Excludes: "old" ;DestDir: "{app}\bin"; Flags: ignoreversion recursesubdirs createallsubdirs
;Source: ".\doc\APIreference\*"; DestDir: "{app}\doc\APIreference"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: ".\doc\BoSSSPad_Command_Overview.pdf"; DestDir: "{app}\doc"; Flags: ignoreversion recursesubdirs createallsubdirs
;Source: ".\doc\BoSSShandbook.pdf"; DestDir: "{app}\doc"; Flags: ignoreversion recursesubdirs createallsubdirs
;Source: ".\doc\ControlExamples\IBM\*"; DestDir: "{app}\doc\ControlExamples\IBM"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: ".\doc\ControlExamples\CNS\*"; DestDir: "{app}\doc\ControlExamples\CNS"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"         
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
;Name: "{group}\Install Visualizers for Visual Studio"; Filename: "{app}\bin\Release\bcl.exe"; Parameters: "--visualizers-inst"
;Name: "{group}\BoSSSpad (console)"; Filename: "{app}\bin\Release\BoSSSpad.exe"; Parameters: "--console"        
;Name: "{group}\BoSSSpad (worksheet)"; Filename: "{app}\bin\Release\BoSSSpad.exe"
;Name: "{group}\BoSSSpad (electron)"; Filename: "{app}\bin\BoSSSpad-win32-x64\BoSSSpad.exe"         
;Name: "{group}\BoSSS Handbook"; Filename: "{app}\doc\BoSSShandbook.pdf" 
;Name: "{group}\BoSSS API Reference"; Filename: "{app}\doc\APIreference\index.html" 

[InstallDelete]
; BLACKLIST: these .dll are obsolete or replaced by newer versions, 
; especially msmpi (conflict with mpiexec)
Type: files; Name: "{app}\bin\native\win\amd64\msmpi.dll";
Type: files; Name: "{app}\bin\native\win\amd64\libacml_dll.dll";
Type: files; Name: "{app}\bin\native\win\amd64\libacml_mv_dll.dll";
Type: files; Name: "{app}\bin\native\win\amd64\msvcm90.dll";
Type: files; Name: "{app}\bin\native\win\amd64\msvcp90.dll";
Type: files; Name: "{app}\bin\native\win\amd64\msvcr90.dll";
Type: files; Name: "{app}\bin\native\win\amd64\msvcp100.dll";
Type: files; Name: "{app}\bin\native\win\amd64\msvcr100.dll";
Type: files; Name: "{app}\bin\native\win\amd64\tec360.dll";
Type: files; Name: "{app}\bin\native\win\amd64\dmumps.dll";

[Registry]
; as elevated user (Admin): set BOSSS_INSTALL system-wide
Root: HKLM; \
   Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
   Flags: deletevalue; \
   ValueType: expandsz; \
   ValueName: "BOSSS_INSTALL"; \
   ValueData: "{app}"; \
   Check: not IsRegularUser()
Root: HKLM; \
    Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
    ValueType: expandsz; \
    ValueName: "Path"; \
    ValueData: "{olddata};{app}\bin\Release;"; \
    Check: NeedsAddPath(ExpandConstant('{app}\bin\Release\net6.0')) and ( not IsRegularUser() )
; as regular user (non-admin): set BOSSS_INSTALL only local
Root: HKCU; \
   Subkey: "Environment"; \
   Flags: deletevalue; \
   ValueType: expandsz; \
   ValueName: "BOSSS_INSTALL"; \
   ValueData: "{app}"; \
   Check: IsRegularUser()
Root: HKCU; \
    Subkey: "Environment"; \
    ValueType: expandsz; \
    ValueName: "Path"; \
    ValueData: "{olddata};{app}\bin\Release;"; \
    Check: NeedsAddPath(ExpandConstant('{app}\bin\Release\net6.0')) and ( IsRegularUser() )

;[Run]
; Filename: bcl.exe; \
; Flags: waituntilterminated runhidden; \
; Description: "Initialization run of bcl";

[Run]
Filename: {app}\bin\native\win\redist\vcredist_x64.exe; \
    Parameters: "/install /passive /norestart"; \
    StatusMsg: "Installing Visual C++ 2017 Redistributables..."; \
	Flags: waituntilterminated postinstall;
Filename: {app}\bin\native\win\redist\MSMpiSetup-9.0.1.exe; \
    Parameters: "-force"; \
    StatusMsg: "Installing Microsoft MPI..."; \
	Flags: waituntilterminated skipifsilent postinstall;
;Filename: {app}\bin\native\win\redist\NDP472-KB4054531-Web.exe;  \
;    StatusMsg: "Installing Microsoft .NET 4.7.2..."; \
;	Flags: waituntilterminated skipifsilent postinstall;
	
 
[Code]
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    'Path', OrigPath)
  then begin
    Result := True;
    exit;
  end;
  // look for the path with leading and trailing semicolon
  // Pos() returns 0 if not found
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;

// checks is the user is Admin or Regular (non-elevated)
function IsRegularUser(): Boolean;
 begin
 Result := not (IsAdminInstallMode or IsPowerUserLoggedOn);
 end;

// the program files dir. for admins,
// the local app directory for regular users.  
function DefDirRoot(Param: String): String;
 begin
 if IsRegularUser then
 Result := ExpandConstant('{localappdata}')
 else
 Result := ExpandConstant('{pf}')
 end;

