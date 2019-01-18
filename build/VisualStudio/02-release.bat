mkdir ..\..\release
cd ..\..\release

mkdir NetCoreServer
cd NetCoreServer
xcopy /S /Y ..\..\source\NetCoreServer\bin\Release\*.* .
7z a ..\NetCoreServer.zip *.*
cd ..
rd /S /Q NetCoreServer

mkdir examples
cd examples
xcopy /S /Y ..\..\examples\SslChatClient\bin\Release\*.* .
xcopy /S /Y ..\..\examples\SslChatServer\bin\Release\*.* .
xcopy /S /Y ..\..\examples\TcpChatClient\bin\Release\*.* .
xcopy /S /Y ..\..\examples\TcpChatServer\bin\Release\*.* .
xcopy /S /Y ..\..\examples\UdpEchoClient\bin\Release\*.* .
xcopy /S /Y ..\..\examples\UdpEchoServer\bin\Release\*.* .
xcopy /S /Y ..\..\examples\UdpMulticastClient\bin\Release\*.* .
xcopy /S /Y ..\..\examples\UdpMulticastServer\bin\Release\*.* .
xcopy /S /Y ..\..\tools\certificates\*.pem .
7z a ..\Examples.zip *.*
cd ..
rd /S /Q examples

mkdir performance
cd performance
xcopy /S /Y ..\..\performance\SslEchoClient\bin\Release\*.* .
xcopy /S /Y ..\..\performance\SslEchoServer\bin\Release\*.* .
xcopy /S /Y ..\..\performance\SslMulticastClient\bin\Release\*.* .
xcopy /S /Y ..\..\performance\SslMulticastServer\bin\Release\*.* .
xcopy /S /Y ..\..\performance\TcpEchoClient\bin\Release\*.* .
xcopy /S /Y ..\..\performance\TcpEchoServer\bin\Release\*.* .
xcopy /S /Y ..\..\performance\TcpMulticastClient\bin\Release\*.* .
xcopy /S /Y ..\..\performance\TcpMulticastServer\bin\Release\*.* .
xcopy /S /Y ..\..\performance\UdpEchoClient\bin\Release\*.* .
xcopy /S /Y ..\..\performance\UdpEchoServer\bin\Release\*.* .
xcopy /S /Y ..\..\performance\UdpMulticastClient\bin\Release\*.* .
xcopy /S /Y ..\..\performance\UdpMulticastServer\bin\Release\*.* .
xcopy /S /Y ..\..\tools\certificates\*.pem .
7z a ..\Benchmarks.zip *.*
cd ..
rd /S /Q performance

cd ../build/VisualStudio
