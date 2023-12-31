@echo off
setlocal EnableDelayedExpansion

set "TARGET=%1"
if not defined TARGET echo No directory specified: Define target directory!
if not defined TARGET goto :EOF
echo target database: %TARGET%
echo %TARGET% > tmp.txt
set "TARGET=%TARGET%\sessions"
::"%BOSSS_INSTALL%\bin\Release\BoSSSpad.exe" --batch GetSomeInfo.bws
del tmp.txt
::set "TARGET=E:\bosss_db_performance\sessions"
set "CUBEPATH=ilPSP.Cube_new"

::for /f %%I in (projectinfo.txt) do mkdir ilPSP.Cube_new\%%I

for /d %%D in (%TARGET%\*) do (
	set "sessionname=%%~nD"
	set "sessionpath=%%~fD"
	echo these are the bins of %%~nD
	for /f "tokens=1,2 delims=:" %%G in (sessioninfo.txt) do (
		if %%~nD == %%G (
		set counter=0
		for /f %%F in ('dir /b %%~fD ^| find "profiling_bin" ') do (
			copy "%%~fD\%%F" "."
			set /A counter=!counter!+1
		)
		echo !counter!
		(
		start /w /b cmd /c %CUBEPATH%\ilPSP.Cube.exe
		)
		rename "calc.p!counter!.r1" "calc.%%H.r1"
		)
	)
)
:EOF