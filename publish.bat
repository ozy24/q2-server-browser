@echo off
echo Publishing Q2Browser for release...
echo.

REM Create publish directory
if not exist "publish" mkdir publish
if exist "publish\*" (
    echo Cleaning publish directory...
    del /q "publish\*"
    for /d %%d in ("publish\*") do rmdir /s /q "%%d"
)

echo.
echo Building and publishing Q2Browser.Wpf...
dotnet publish Q2Browser.Wpf/Q2Browser.Wpf.csproj ^
    --configuration Release ^
    --output "publish" ^
    --self-contained true ^
    --runtime win-x64 ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo Publish completed successfully!
    echo ========================================
    echo.
    echo Output location: %CD%\publish
    echo.
    echo The application is ready for distribution.
    echo You can find Q2Browser.Wpf.exe in the publish folder.
    echo.
) else (
    echo.
    echo ========================================
    echo Publish failed with error code %ERRORLEVEL%
    echo ========================================
    exit /b %ERRORLEVEL%
)


