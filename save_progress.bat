@echo off
cd /d "C:\Users\emipe\Kapicua!"
echo Staging all changes...
git add -A
echo Committing...
git commit -m "Checkpoint: %date% %time%"
echo Pushing to GitHub...
git push origin main
echo.
echo === Done! Press any key to close. ===
pause
