cd ../..
dotnet test NetCoreServer.sln -c Release
if %errorlevel% neq 0 exit /b %errorlevel%
cd build/VisualStudio
