@echo off
setlocal
chcp 65001 >nul

rem ====================================================================
rem  LoupeControl サンプルアプリケーション（WinUI 3 版・ビルド＆起動）
rem
rem  リリース ビルド（dotnet build -c Release）を行い、成功したら
rem  サンプル アプリケーション（EsUtil.Feature.Loupe.ControlWinUI3.Sample.exe）を起動する。
rem
rem  アンパッケージ（WindowsPackageType=None）で実行するため、MSIX の配置は不要である。
rem  自己完結の Windows App SDK は実行時識別子（win-x64）のフォルダへ出力される。
rem
rem  使い方:
rem     RunSample.bat
rem ====================================================================

set "PROJECT=%~dp0EsUtil.Feature.Loupe.ControlWinUI3.Sample.csproj"
set "EXE=%~dp0bin\Release\net10.0-windows10.0.22621.0\win-x64\EsUtil.Feature.Loupe.ControlWinUI3.Sample.exe"

echo [1/2] リリース ビルドを開始します...
dotnet build "%PROJECT%" -c Release --nologo
if errorlevel 1 (
    echo.
    echo [ERROR] リリース ビルドに失敗しました。エラーを確認してください。
    pause
    exit /b 1
)

echo.
echo [2/2] サンプル アプリケーションを起動します...
if not exist "%EXE%" (
    echo [ERROR] 実行ファイルが見つかりません: %EXE%
    pause
    exit /b 1
)

start "" "%EXE%"

echo 起動しました（ウィンドウが表示されます）。
endlocal
