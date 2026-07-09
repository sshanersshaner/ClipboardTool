@echo off
chcp 65001 >nul
set PATH=D:\app\Git\Git\bin;D:\app\Git\Git\cmd;%PATH%
cd /d D:\app\project\ClipboardTool

echo === Git Status ===
git status --short

echo.
echo === Adding files ===
git add -A
git status --short

echo.
echo === Committing ===
git commit -m "添加应用图标、README文档，修复颜色设置崩溃问题"

echo.
echo === Pushing ===
git push origin main

echo.
echo === Result ===
git log --oneline -3
echo ---
git branch -a

pause
