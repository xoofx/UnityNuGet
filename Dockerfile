FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /app

RUN mkdir -p src/UnityNuGet && mkdir -p src/UnityNuGet.Server && mkdir -p src/UnityNuGet.Tests

COPY src/Directory.Build.props src/Directory.Build.props
COPY src/Directory.Packages.props src/Directory.Packages.props
COPY src/*.sln src
COPY src/UnityNuGet/*.csproj src/UnityNuGet
COPY src/UnityNuGet.Server/*.csproj src/UnityNuGet.Server
COPY src/UnityNuGet.Tests/*.csproj src/UnityNuGet.Tests
RUN dotnet restore src -a $TARGETARCH

COPY . ./
RUN dotnet publish src -a $TARGETARCH -c Release -o /app/src/out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/src/out .
ENTRYPOINT ["dotnet", "UnityNuGet.Server.dll"]
