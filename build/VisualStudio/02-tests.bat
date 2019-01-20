cd ../..
dotnet test NetCoreServer.sln
if %errorlevel% neq 0 exit /b %errorlevel%
cd build/VisualStudio
