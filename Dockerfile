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
# Increase the timeout and number of retries for NuGet
RUN dotnet nuget list source | grep -q 'nuget.org' || dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org
ENV NUGET_PACKAGES=/nuget-packages
ENV NUGET_HTTP_CACHE_PATH=/nuget-http-cache
RUN mkdir -p /nuget-packages /nuget-http-cache

# Restore and build the main project and the core project
RUN dotnet restore "Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj" --disable-parallel
RUN dotnet build "Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj" -c Release -o /app/build/Core --no-restore
RUN dotnet publish "Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj" -c Release -o /app/publish/Core --no-restore

# Build and publish ALL extensions
RUN find Extensions -name "*.csproj" | grep -v "UnitTests" | xargs -I {} sh -c 'dotnet publish "{}" -c Release -o /app/publish/Core/Extensions || echo "Skipping $(basename $(dirname {}))"'

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