@echo off
title AiDailyExpense API - Swagger
echo Dang khoi dong Backend API va mo Swagger UI...
start http://localhost:5000/swagger
cd /d "%~dp0backend\AiDailyExpense.Api"
dotnet run --urls "http://0.0.0.0:5000"
pause
