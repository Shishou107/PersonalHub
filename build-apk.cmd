@echo off
title Dong goi Android APK - AiDailyExpense
echo ========================================================
echo   DANG DONG GOI UNG DUNG ANDROID APK...
echo ========================================================

cd /d "%~dp0mobile\AiDailyExpense.Mobile"

REM 1. Kiem tra thu muc node_modules
if not exist "node_modules" (
    echo [THONG BAO] Khong tim thay node_modules. Dang cai dat dependencies (npm install)...
    call npm install
    if %ERRORLEVEL% NEQ 0 (
        echo [LOI] Khong the cai dat thu vien npm!
        pause
        exit /b %ERRORLEVEL%
    )
)

REM 2. Tu dong khoi tao local.properties neu chua co
if not exist "android\local.properties" (
    if defined ANDROID_HOME (
        echo [THONG BAO] Phat hien ANDROID_HOME. Dang tao android\local.properties...
        powershell -Command "Set-Content -Path 'android\local.properties' -Value ('sdk.dir=' + ($env:ANDROID_HOME -replace '\\', '/'))"
    ) else if exist "%LOCALAPPDATA%\Android\Sdk" (
        echo [THONG BAO] Phat hien Android SDK mac dinh. Dang tao android\local.properties...
        powershell -Command "Set-Content -Path 'android\local.properties' -Value ('sdk.dir=' + ($env:LOCALAPPDATA -replace '\\', '/') + '/Android/Sdk')"
    )
)

REM 3. Chay Gradle Build APK
cd /d "%~dp0mobile\AiDailyExpense.Mobile\android"
call gradlew.bat assembleRelease

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo  [THANH CONG] Dong goi APK hoan tat!
    echo.
    echo  File APK cai dat tren dien thoai nam tai:
    echo  %~dp0mobile\AiDailyExpense.Mobile\android\app\build\outputs\apk\release\app-release.apk
    echo ========================================================
) else (
    echo.
    echo ========================================================
    echo  [THAT BAI] Co loi xay ra trong qua trinh dong goi APK!
    echo ========================================================
)
pause
