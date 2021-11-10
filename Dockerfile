FROM mcr.microsoft.com/dotnet/sdk:6.0.100 AS build
WORKDIR /app

RUN mkdir -p src/UnityNuGet && mkdir -p src/UnityNuGet.Server && mkdir -p src/UnityNuGet.Tests

COPY src/*.sln src
COPY src/UnityNuGet/*.csproj src/UnityNuGet
COPY src/UnityNuGet.Server/*.csproj src/UnityNuGet.Server
COPY src/UnityNuGet.Tests/*.csproj src/UnityNuGet.Tests
RUN dotnet restore src

COPY . ./
RUN dotnet publish src -c Release -o /app/src/out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /app/src/out .
ENTRYPOINT ["dotnet", "UnityNuGet.Server.dll"]