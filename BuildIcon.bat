@echo off
chcp 65001 >nul
echo Compiling IconGen...

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFDIR=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2

%CSC% /target:exe /out:IconGen.exe /reference:"%REFDIR%\PresentationFramework.dll" /reference:"%REFDIR%\PresentationCore.dll" /reference:"%REFDIR%\WindowsBase.dll" /reference:"%REFDIR%\System.Xaml.dll" IconGen.cs

if %ERRORLEVEL% equ 0 (
    echo Compiled OK, running...
    IconGen.exe
) else (
    echo Compile failed: %ERRORLEVEL%
)
pause
