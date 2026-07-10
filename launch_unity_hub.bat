@echo off
:: Launch Unity Hub with updated PATH so it (and Unity) can find Python and uv
set PATH=%LOCALAPPDATA%\Microsoft\WindowsApps;%USERPROFILE%\.local\bin;%PATH%
echo PATH updated. Launching Unity Hub...
start "" "C:\Program Files\Unity Hub\Unity Hub.exe"
