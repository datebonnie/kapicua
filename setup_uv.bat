@echo off
echo === Step 1: Installing uv ===
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"

echo.
echo === Step 2: Setting up python_tools ===
set UV=%USERPROFILE%\.local\bin\uv.exe

if not exist "%UV%" (
    echo ERROR: uv not found at %UV%. Check if install succeeded.
    pause
    exit /b 1
)

cd /d "C:\Users\emipe\Kapicua!"
"%UV%" init python_tools
cd python_tools
"%UV%" add numpy

echo.
echo === Done! uv version: ===
"%UV%" --version
echo.
echo Press any key to close.
pause
