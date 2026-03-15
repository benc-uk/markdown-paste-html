@echo off
REM Build script for Markdown Paste HTML

echo Building Markdown Paste HTML (Release - Single File)...
echo.

dotnet publish -c Release -r win-x64 --self-contained ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo.
    echo Executable location:
    echo bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\MarkdownPasteHtml.exe
    echo.
    echo You can copy this file anywhere and run it.
) else (
    echo.
    echo Build failed!
    exit /b 1
)

pause
