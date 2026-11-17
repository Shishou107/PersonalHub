@echo off
title Dong goi toan bo he thong AiDailyExpense
echo ========================================================
echo   BAT DAU DONG GOI HE THONG (BACKEND + ANDROID APK)
echo ========================================================
echo.

echo [1/2] Dang dong goi Backend API (.NET 9)...
cd /d "%~dp0"
dotnet publish backend/AiDailyExpense.Api/AiDailyExpense.Api.csproj -c Release -o publish
if %ERRORLEVEL% NEQ 0 (
    echo [LOI] Dong goi Backend that bai!
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] Backend da dong goi xong tai thu muc publish/
echo.

echo [2/2] Dang dong goi Mobile App APK (Android)...
cd /d "%~dp0mobile\AiDailyExpense.Mobile"

REM Kiem tra thu muc node_modules
if not exist "node_modules" (
    echo [THONG BAO] Khong tim thay node_modules. Dang cai dat dependencies (npm install)...
    call npm install
    if %ERRORLEVEL% NEQ 0 (
        echo [LOI] Khong the cai dat thu vien npm!
        pause
        exit /b %ERRORLEVEL%
    )
)

REM Tu dong khoi tao local.properties neu chua co
if not exist "android\local.properties" (
    if defined ANDROID_HOME (
        echo [THONG BAO] Phat hien ANDROID_HOME. Dang tao android\local.properties...
        powershell -Command "Set-Content -Path 'android\local.properties' -Value ('sdk.dir=' + ($env:ANDROID_HOME -replace '\\', '/'))"
    ) else if exist "%LOCALAPPDATA%\Android\Sdk" (
        echo [THONG BAO] Phat hien Android SDK mac dinh. Dang tao android\local.properties...
        powershell -Command "Set-Content -Path 'android\local.properties' -Value ('sdk.dir=' + ($env:LOCALAPPDATA -replace '\\', '/') + '/Android/Sdk')"
    )
)

cd /d "%~dp0mobile\AiDailyExpense.Mobile\android"
call gradlew.bat assembleRelease
if %ERRORLEVEL% NEQ 0 (
    echo [LOI] Dong goi APK that bai!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================================
echo              DONG GOI TOAN BO HOAN TAT!
echo ========================================================
echo 1. Thu muc Backend API:
echo    %~dp0publish\
echo.
echo 2. File APK cai dat tren dien thoai:
echo    %~dp0mobile\AiDailyExpense.Mobile\android\app\build\outputs\apk\release\app-release.apk
echo ========================================================
pause
