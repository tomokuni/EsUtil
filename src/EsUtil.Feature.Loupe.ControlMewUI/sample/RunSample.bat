@echo off
setlocal
chcp 65001 >nul

rem ====================================================================
rem  LoupeControl サンプルアプリケーション（MewUI 版・ビルド＆起動）
rem
rem  リリースビルド（dotnet build -c Release）を行い、成功したら
rem  サンプルアプリケーション（EsUtil.Feature.Loupe.ControlMewUI.Sample.exe）を起動する。
rem
rem  使い方:
rem     RunSample.bat
rem ====================================================================

set "PROJECT=%~dp0EsUtil.Feature.Loupe.ControlMewUI.Sample.csproj"
set "EXE=%~dp0bin\Release\net10.0-windows\EsUtil.Feature.Loupe.ControlMewUI.Sample.exe"

echo [1/2] リリースビルドを開始します...
dotnet build "%PROJECT%" -c Release --nologo
if errorlevel 1 (
    echo.
    echo [ERROR] リリースビルドに失敗しました。エラーを確認してください。
    pause
    exit /b 1
)

echo.
echo [2/2] サンプルアプリケーションを起動します...
if not exist "%EXE%" (
    echo [ERROR] 実行ファイルが見つかりません: %EXE%
    pause
    exit /b 1
)

start "" "%EXE%"

echo 起動しました（ウィンドウが表示されます）。
endlocal
