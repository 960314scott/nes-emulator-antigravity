@echo off
setlocal
echo ========================================================
echo   NES Emulator - One-Click Build Script
echo ========================================================
echo.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] C# Compiler not found at:
    echo %CSC%
    echo Please make sure .NET Framework 4.0 or later is installed.
    pause
    exit /b 1
)

echo Compiling NesEmulator.exe...
"%CSC%" /target:winexe /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:NesEmulator.exe /recurse:src\*.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================================
    echo   [SUCCESS] NesEmulator.exe built successfully!
    echo ========================================================
    echo You can now double click NesEmulator.exe to play!
) else (
    echo.
    echo [ERROR] Compilation failed. See error messages above.
)

echo.
pause
