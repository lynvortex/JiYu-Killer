@echo off
rem ============================================================
rem  JiYu Killer - offline build (no NuGet / no internet needed)
rem  Uses Roslyn csc from VS Build Tools + .NET Framework 4.8
rem  runtime assemblies already installed on the machine.
rem
rem  Usage:
rem    build.cmd          build bin\JiYuKiller.exe
rem    build.cmd test     build + run protocol regression tests
rem ============================================================
setlocal
set ROOT=%~dp0
set CSC="C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
set FX=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FX%\WPF
set OUT=%ROOT%bin
set SRC=%ROOT%src

if not exist "%OUT%" mkdir "%OUT%"

rem --- copy icon next to the exe (loaded at runtime) ---
copy /y "%ROOT%assets\Control.ico" "%OUT%\Control.ico" >nul

%CSC% -nologo -target:winexe -platform:anycpu -langversion:7.3 -optimize+ -codepage:65001 ^
  -out:"%OUT%\JiYuKiller.exe" ^
  -win32manifest:"%ROOT%assets\app.manifest" ^
  -win32icon:"%ROOT%assets\Control.ico" ^
  -r:"%FX%\mscorlib.dll" ^
  -r:"%FX%\System.dll" ^
  -r:"%FX%\System.Core.dll" ^
  -r:"%FX%\System.Xaml.dll" ^
  -r:"%WPF%\PresentationFramework.dll" ^
  -r:"%WPF%\PresentationCore.dll" ^
  -r:"%WPF%\WindowsBase.dll" ^
  "%SRC%\*.cs"

if errorlevel 1 (
  echo.
  echo BUILD FAILED
  exit /b 1
)
echo BUILD OK: %OUT%\JiYuKiller.exe


rem --- x86 build (for 32-bit-only hook scenarios) ---
%CSC% -nologo -target:winexe -platform:x86 -langversion:7.3 -optimize+ -codepage:65001 ^
  -out:"%OUT%\x86\JiYuKiller.x86.exe" ^
  -win32manifest:"%ROOT%assets\app.manifest" ^
  -win32icon:"%ROOT%assets\Control.ico" ^
  -r:"%FX%\mscorlib.dll" ^
  -r:"%FX%\System.dll" ^
  -r:"%FX%\System.Core.dll" ^
  -r:"%FX%\System.Xaml.dll" ^
  -r:"%WPF%\PresentationFramework.dll" ^
  -r:"%WPF%\PresentationCore.dll" ^
  -r:"%WPF%\WindowsBase.dll" ^
  "%SRC%\*.cs"
if errorlevel 1 (
  echo X86 BUILD FAILED
) else (
  echo BUILD OK: %OUT%\x86\JiYuKiller.x86.exe
)

if /i "%~1"=="test" call :test
exit /b 0

:test
%CSC% -nologo -t:exe -out:"%OUT%\PacketTest.exe" -codepage:65001 ^
  -r:"%FX%\mscorlib.dll" -r:"%FX%\System.dll" -r:"%FX%\System.Core.dll" ^
  "%SRC%\JyPackets.g.cs" "%SRC%\JyPackets.cs" "%SRC%\JyVersion.cs" ^
  "%SRC%\JySender.cs" "%SRC%\IpGenerator.cs" "%SRC%\JyPassword.cs" ^
  "%ROOT%test\PacketTest.cs"
if errorlevel 1 exit /b 1
"%OUT%\PacketTest.exe"
