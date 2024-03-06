#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER app
WORKDIR /app
ENV CACHE_PATH=/src/www/ws

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["examples/WsChatServer/WsChatServer.csproj", "examples/WsChatServer/"]
COPY ["source/NetCoreServer/NetCoreServer.csproj", "source/NetCoreServer/"]
RUN dotnet restore "./examples/WsChatServer/WsChatServer.csproj"
COPY . .
WORKDIR "/src/examples/WsChatServer"
RUN dotnet build "./WsChatServer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WsChatServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WsChatServer.dll"]