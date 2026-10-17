@echo off
cd /d "%~dp0"
echo Preparando J.A.R.V.I.S. Pro V0.8 Preview 1...
dotnet build Jarvis.Core\Jarvis.Core.csproj --configuration Release --nologo
if errorlevel 1 (
  echo No se pudo compilar. Cierra otra instancia de Jarvis y revisa el error.
  pause
  exit /b 1
)
dotnet Jarvis.Core\bin\Release\net10.0-windows\Jarvis.Core.dll
if errorlevel 1 pause

