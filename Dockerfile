# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file and project files
COPY ["CosmosDbDataMigrationTool.sln", "."]
COPY ["Directory.Packages.props", "."]
COPY ["Core/", "Core/"]
COPY ["Interfaces/", "Interfaces/"]
COPY ["Extensions/", "Extensions/"]

# Restore dependencies
RUN dotnet restore

# Build the core application
RUN dotnet build "Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj" -c Release -o /app/build/Core

# Publish the core application
RUN dotnet publish "Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj" -c Release -o /app/publish/Core

# Build and publish all extensions
RUN find Extensions -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*" -not -path "*/UnitTests/*" | \
    xargs -I {} sh -c 'echo "Publishing extension: {}"; dotnet publish "{}" -c Release -o /app/publish/Core/Extensions'

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish/Core ./

# Create volumes for configuration and data
VOLUME /config
VOLUME /data

# Optional volume for custom extensions
VOLUME /extensions

# Set the entrypoint
ENTRYPOINT ["dotnet", "dmt.dll"]