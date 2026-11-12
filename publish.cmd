@echo off
title Publish AiDailyExpense Backend API
echo Dang publish backend ra thu muc publish/...
cd /d "%~dp0"
dotnet publish backend/AiDailyExpense.Api/AiDailyExpense.Api.csproj -c Release -o publish
echo.
echo ========================================================
echo Publish hoan tat! Thu muc output: %~dp0publish
echo ========================================================
pause
