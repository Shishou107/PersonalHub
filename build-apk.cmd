@echo off
title Dong goi Android APK - AiDailyExpense
echo ========================================================
echo   DANG DONG GOI UNG DUNG ANDROID APK...
echo ========================================================
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

