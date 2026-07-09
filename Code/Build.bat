@echo off
chcp 65001 >nul
echo 正在编译 ClipboardTool...

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFDIR1=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2
set REFDIR2=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8
set REFDIR3=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.1

set REFDIR=%REFDIR1%
if not exist "%REFDIR%\PresentationFramework.dll" set REFDIR=%REFDIR2%
if not exist "%REFDIR%\PresentationFramework.dll" set REFDIR=%REFDIR3%
if not exist "%REFDIR%\PresentationFramework.dll" (
    echo 未找到 WPF 参考程序集！
    pause
    exit /b 1
)

set REFS=/reference:"%REFDIR%\PresentationFramework.dll" /reference:"%REFDIR%\PresentationCore.dll" /reference:"%REFDIR%\WindowsBase.dll" /reference:"%REFDIR%\System.Xaml.dll" /reference:Tesseract.dll

%CSC% /target:winexe /win32icon:app.ico /out:ClipboardTool.exe %REFS% App.cs MainWindow.cs

if %ERRORLEVEL% equ 0 (
    echo 编译成功！输出: ClipboardTool.exe
) else (
    echo 编译失败，错误码: %ERRORLEVEL%
)
pause
