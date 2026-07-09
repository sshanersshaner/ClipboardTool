@echo off
chcp 65001 >nul
echo ========================================
echo   ClipboardTool - 推送到 GitHub
echo ========================================
echo.

set PATH=D:\app\Git\Git\bin;D:\app\Git\Git\cmd;%PATH%
cd /d D:\app\project\ClipboardTool

echo 正在推送到 https://github.com/sshanersshaner/ClipboardTool.git
echo.
echo 如果弹出浏览器，请在浏览器中完成 GitHub 登录授权
echo 如果提示输入用户名和密码：
echo   用户名：你的 GitHub 用户名
echo   密码：使用 GitHub Personal Access Token (不是登录密码)
echo.

git push -u origin main

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================
    echo   推送成功!
    echo ========================================
) else (
    echo.
    echo ========================================
    echo   推送失败，错误码: %ERRORLEVEL%
    echo ========================================
)

pause
