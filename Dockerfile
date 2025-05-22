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
RUN dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org
ENV NUGET_PACKAGES=/nuget-packages
ENV NUGET_HTTP_CACHE_PATH=/nuget-http-cache
RUN mkdir -p /nuget-packages /nuget-http-cache

# Restore and build the main project and the core project
RUN dotnet restore "Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj" --disable-parallel
RUN dotnet build "Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj" -c Release -o /app/build/Core --no-restore
RUN dotnet publish "Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj" -c Release -o /app/publish/Core --no-restore

# Build and publish all standard extensions
RUN dotnet publish "Extensions/Json/Cosmos.DataTransfer.JsonExtension/Cosmos.DataTransfer.JsonExtension.csproj" -c Release -o /app/publish/Core/Extensions || echo "Skipping Json extension"
RUN dotnet publish "Extensions/Cosmos/Cosmos.DataTransfer.CosmosExtension/Cosmos.DataTransfer.CosmosExtension.csproj" -c Release -o /app/publish/Core/Extensions || echo "Skipping Cosmos extension"
RUN dotnet publish "Extensions/Csv/Cosmos.DataTransfer.CsvExtension/Cosmos.DataTransfer.CsvExtension.csproj" -c Release -o /app/publish/Core/Extensions || echo "Skipping CSV extension"
RUN dotnet publish "Extensions/SqlServer/Cosmos.DataTransfer.SqlServerExtension/Cosmos.DataTransfer.SqlServerExtension.csproj" -c Release -o /app/publish/Core/Extensions || echo "Skipping SqlServer extension"

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