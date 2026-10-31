# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble AS build
WORKDIR /src

# Set build arguments
ARG RUNTIME=linux-x64
ARG BUILD_VERSION=1.0.0

# Copy solution file and project files
COPY ["CosmosDbDataMigrationTool.sln", "."]
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["global.json", "."]
COPY ["Core/", "Core/"]
COPY ["Interfaces/", "Interfaces/"]
COPY ["Extensions/", "Extensions/"]

# Restore dependencies
RUN dotnet restore "Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj"

# Build Core app package (self-contained)
RUN dotnet publish \
    Core/Cosmos.DataTransfer.Core/Cosmos.DataTransfer.Core.csproj \
    --configuration Release \
    --output /app/${RUNTIME} \
    --self-contained true \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=true \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

# Build Cosmos Extension
RUN dotnet publish \
    Extensions/Cosmos/Cosmos.DataTransfer.CosmosExtension/Cosmos.DataTransfer.CosmosExtension.csproj \
    --configuration Release \
    --output /app/${RUNTIME}/Extensions \
    --self-contained false \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

# Build JSON Extension
RUN dotnet publish \
    Extensions/Json/Cosmos.DataTransfer.JsonExtension/Cosmos.DataTransfer.JsonExtension.csproj \
    --configuration Release \
    --output /app/${RUNTIME}/Extensions \
    --self-contained false \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

# Build Azure Table Extension
RUN dotnet publish \
    Extensions/AzureTableAPI/Cosmos.DataTransfer.AzureTableAPIExtension/Cosmos.DataTransfer.AzureTableAPIExtension.csproj \
    --configuration Release \
    --output /app/${RUNTIME}/Extensions \
    --self-contained false \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

# Build Mongo Extension
RUN dotnet publish \
    Extensions/Mongo/Cosmos.DataTransfer.MongoExtension/Cosmos.DataTransfer.MongoExtension.csproj \
    --configuration Release \
    --output /app/${RUNTIME}/Extensions \
    --self-contained false \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

# Build SQL Server Extension
RUN dotnet publish \
    Extensions/SqlServer/Cosmos.DataTransfer.SqlServerExtension/Cosmos.DataTransfer.SqlServerExtension.csproj \
    --configuration Release \
    --output /app/${RUNTIME}/Extensions \
    --self-contained false \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

# Build Parquet Extension
RUN dotnet publish \
    Extensions/Parquet/Cosmos.DataTransfer.ParquetExtension/Cosmos.DataTransfer.ParquetExtension.csproj \
    --configuration Release \
    --output /app/${RUNTIME}/Extensions \
    --self-contained false \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

# Build Cognitive Search Extension
RUN dotnet publish \
    Extensions/CognitiveSearch/Cosmos.DataTransfer.CognitiveSearchExtension/Cosmos.DataTransfer.CognitiveSearchExtension.csproj \
    --configuration Release \
    --output /app/${RUNTIME}/Extensions \
    --self-contained false \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

# Build CSV Extension
RUN dotnet publish \
    Extensions/Csv/Cosmos.DataTransfer.CsvExtension/Cosmos.DataTransfer.CsvExtension.csproj \
    --configuration Release \
    --output /app/${RUNTIME}/Extensions \
    --self-contained false \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

# Build PostgreSQL Extension
RUN dotnet publish \
    Extensions/PostgreSQL/Cosmos.DataTransfer.PostgresqlExtension.csproj \
    --configuration Release \
    --output /app/${RUNTIME}/Extensions \
    --self-contained false \
    --runtime ${RUNTIME} \
    -p:PublishSingleFile=false \
    -p:DebugType=embedded \
    -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:Version=${BUILD_VERSION}

RUN cp Core/Cosmos.DataTransfer.Core/bin/Release/net10.0/${RUNTIME}/dmt.runtimeconfig.json /app/${RUNTIME}/runtime-info.json

# Runtime stage - explicit Ubuntu 24.04 base includes ICU, OpenSSL and CA roots.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.14-noble AS runtime
WORKDIR /app

# Enable this section for local development verification to use Microsoft Entra ID authentication
# Install curl and Azure CLI
# RUN apt-get clean && apt-get update
# RUN apt install curl -y
# RUN curl -sL https://aka.ms/InstallAzureCLIDeb | bash

# Copy the built application
ARG RUNTIME=linux-x64
COPY --from=build /app/${RUNTIME} ./

# Verify the contents of the final image
RUN echo "=== Final image contents ===" && ls -la ./ && echo "=== Extensions directory ===" && ls -la ./Extensions/

# Create volumes for configuration and data
VOLUME /config
VOLUME /data

# Make the executable file executable (for Linux)
RUN chmod +x dmt

# Set the entrypoint to the self-contained executable
ENTRYPOINT ["./dmt"]