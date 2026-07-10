@echo off
cd /d "C:\Users\emipe\Kapicua!"
echo Renaming branch to main (safe if already done)...
git branch -M main
echo Ensuring remote points at github.com/datebonnie/kapicua ...
git remote remove origin 2>nul
git remote add origin https://github.com/datebonnie/kapicua.git
echo.
echo Pushing to GitHub -- first push uploads ~450 MB, be patient.
echo (A browser window may open asking you to sign in to GitHub -- that's normal.)
echo.
git push -u origin main
echo.
echo === Done! Press any key to close. ===
pause
