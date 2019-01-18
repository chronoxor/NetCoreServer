cd VisualStudio
call 01-build.bat
if %errorlevel% neq 0 exit /b %errorlevel%
call 02-release.bat
if %errorlevel% neq 0 exit /b %errorlevel%
