@echo off
chcp 65001 >nul
set PATH=D:\app\Git\Git\bin;D:\app\Git\Git\cmd;%PATH%
cd /d D:\app\project\ClipboardTool

echo === Pushing to GitHub ===
git push origin main

if %ERRORLEVEL% equ 0 (
    echo === Push OK ===
) else (
    echo === Push failed: %ERRORLEVEL% ===
)

echo === Verify ===
git log --oneline -3
echo ---
git branch -a

pause
