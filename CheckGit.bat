@echo off
set PATH=D:\app\Git\Git\bin;D:\app\Git\Git\cmd;%PATH%
cd /d D:\app\project\ClipboardTool
echo LOCAL:
git rev-parse HEAD
echo REMOTE:
git rev-parse origin/main
