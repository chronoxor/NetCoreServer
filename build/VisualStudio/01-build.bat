cd ../..
nuget restore NetCoreServer.sln
MSBuild NetCoreServer.sln /p:Configuration=Release /t:pack
if %errorlevel% neq 0 exit /b %errorlevel%
cd build/VisualStudio
