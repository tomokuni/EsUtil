@echo off
setlocal
chcp 65001 >nul

rem ====================================================================
rem  ThumbnailControl sample application (WinUI 3, build and run)
rem
rem  ASCII only.  cmd.exe reads a batch file with the console code page and counts the
rem  read position in decoded characters, so non-ASCII text garbles the output and can
rem  make cmd execute fragments of the file as commands.  The chcp below keeps the UTF-8
rem  log of dotnet readable (safe because this file is ASCII).  See .github/ for the rules.
rem
rem  Builds the sample in Release, and starts it when the build succeeded.
rem
rem  Runs unpackaged (WindowsPackageType=None), so no MSIX deployment is needed.  The
rem  self-contained Windows App SDK is published into the runtime identifier (win-x64) folder.
rem
rem  Usage:
rem     RunSample.bat
rem ====================================================================

set "PROJECT=%~dp0EsUtil.Ui.WinUI3.Thumbnail.Sample.csproj"
set "EXE=%~dp0bin\Release\net10.0-windows10.0.22621.0\win-x64\EsUtil.Ui.WinUI3.Thumbnail.Sample.exe"

echo [1/2] Building the sample in Release...
dotnet build "%PROJECT%" -c Release --nologo
if errorlevel 1 (
    echo.
    echo [ERROR] The Release build failed.  Check the errors above.
    pause
    exit /b 1
)

echo.
echo [2/2] Starting the sample application...
if not exist "%EXE%" (
    echo [ERROR] Executable not found: %EXE%
    pause
    exit /b 1
)

start "" "%EXE%"

echo Started (the window opens now).
endlocal
