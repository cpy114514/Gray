@echo off
setlocal enabledelayedexpansion

cd /d "%~dp0"

git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo Not a git repository.
    pause
    exit /b 1
)

git remote get-url origin >nul 2>&1
if errorlevel 1 (
    echo Remote origin is not configured.
    pause
    exit /b 1
)

set "COMMIT_MESSAGE=Update Gray project"
if not "%~1"=="" set "COMMIT_MESSAGE=%~1"

git add -A
git diff --cached --quiet
if not errorlevel 1 (
    echo No changes to commit.
    pause
    exit /b 0
)

git commit -m "%COMMIT_MESSAGE%"
if errorlevel 1 (
    echo Commit failed.
    pause
    exit /b 1
)

git push -u origin main
if errorlevel 1 (
    echo Push failed.
    pause
    exit /b 1
)

echo Uploaded to GitHub successfully.
pause
