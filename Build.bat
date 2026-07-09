@echo off
chcp 65001 >nul
echo ??????...

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFDIR1=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2
set REFDIR2=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8
set REFDIR3=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.1

set REFDIR=%REFDIR1%
if not exist "%REFDIR%\PresentationFramework.dll" set REFDIR=%REFDIR2%
if not exist "%REFDIR%\PresentationFramework.dll" set REFDIR=%REFDIR3%
if not exist "%REFDIR%\PresentationFramework.dll" (
    echo ??? WPF ?????!
    pause
    exit /b 1
)

set REFS=/reference:"%REFDIR%\PresentationFramework.dll" /reference:"%REFDIR%\PresentationCore.dll" /reference:"%REFDIR%\WindowsBase.dll" /reference:"%REFDIR%\System.Xaml.dll" /reference:"Tesseract.dll"

%CSC% /target:winexe /out:ClipboardTool.exe %REFS% ClipboardToolCode\App.cs ClipboardToolCode\MainWindow.cs

if %ERRORLEVEL% equ 0 (
    echo ????! ??: ClipboardTool.exe
) else (
    echo ????, ???: %ERRORLEVEL%
)
pause
