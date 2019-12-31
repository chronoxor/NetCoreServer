cd ../..
dotnet restore NetCoreServer.sln
MSBuild NetCoreServer.sln /p:Configuration=Release
if %errorlevel% neq 0 exit /b %errorlevel%
cd build/VisualStudio
