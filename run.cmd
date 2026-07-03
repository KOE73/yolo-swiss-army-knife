@echo off
echo Killing existing YoloHelperApp process if running...
taskkill /F /IM YoloHelperApp.exe >nul 2>&1

echo Starting YoloHelperApp...
start "" dotnet run --project YoloHelperApp\YoloHelperApp.csproj
exit
