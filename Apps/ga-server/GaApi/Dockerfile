# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["AllProjects.sln", "./"]
COPY ["Apps/ga-server/GaApi/GaApi.csproj", "Apps/ga-server/GaApi/"]
COPY ["AllProjects.ServiceDefaults/AllProjects.ServiceDefaults.csproj", "AllProjects.ServiceDefaults/"]
COPY ["GA.Data.MongoDB/GA.Data.MongoDB.csproj", "GA.Data.MongoDB/"]
COPY ["GA.Data.SemanticKernel.Embeddings/GA.Data.SemanticKernel.Embeddings.csproj", "GA.Data.SemanticKernel.Embeddings/"]
COPY ["Common/GA.Business.Core/GA.Business.Core.csproj", "Common/GA.Business.Core/"]
COPY ["Common/GA.Core/GA.Core.csproj", "Common/GA.Core/"]

# Restore dependencies
RUN dotnet restore "Apps/ga-server/GaApi/GaApi.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/Apps/ga-server/GaApi"
RUN dotnet build "GaApi.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "GaApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GaApi.dll"]

