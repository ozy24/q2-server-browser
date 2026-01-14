@echo off
echo Running Q2Browser...
dotnet run --project Q2Browser.Wpf/Q2Browser.Wpf.csproj
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Application exited with error code %ERRORLEVEL%
    pause
)








