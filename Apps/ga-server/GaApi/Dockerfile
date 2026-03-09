# Build stage — .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer-cached restore
COPY ["Apps/ga-server/GaApi/GaApi.csproj", "Apps/ga-server/GaApi/"]
COPY ["Apps/ga-server/GA.Fretboard.Service/GA.Fretboard.Service.csproj", "Apps/ga-server/GA.Fretboard.Service/"]
COPY ["AllProjects.ServiceDefaults/AllProjects.ServiceDefaults.csproj", "AllProjects.ServiceDefaults/"]
COPY ["GA.Data.SemanticKernel.Embeddings/GA.Data.SemanticKernel.Embeddings.csproj", "GA.Data.SemanticKernel.Embeddings/"]
COPY ["Common/GA.Core/GA.Core.csproj", "Common/GA.Core/"]
COPY ["Common/GA.Domain.Core/GA.Domain.Core.csproj", "Common/GA.Domain.Core/"]
COPY ["Common/GA.Domain.Services/GA.Domain.Services.csproj", "Common/GA.Domain.Services/"]
COPY ["Common/GA.Business.Core/GA.Business.Core.csproj", "Common/GA.Business.Core/"]
COPY ["Common/GA.Business.Config/GA.Business.Config.fsproj", "Common/GA.Business.Config/"]
COPY ["Common/GA.Business.DSL/GA.Business.DSL.fsproj", "Common/GA.Business.DSL/"]
COPY ["Common/GA.Business.ML/GA.Business.ML.csproj", "Common/GA.Business.ML/"]
COPY ["Common/GA.Business.Core.Orchestration/GA.Business.Core.Orchestration.csproj", "Common/GA.Business.Core.Orchestration/"]
COPY ["Common/GA.Infrastructure/GA.Infrastructure.csproj", "Common/GA.Infrastructure/"]

RUN dotnet restore "Apps/ga-server/GaApi/GaApi.csproj"

# Copy remaining source and publish
COPY . .
WORKDIR "/src/Apps/ga-server/GaApi"
RUN dotnet publish "GaApi.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false -p:DebugType=none -p:DebugSymbols=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

RUN useradd -m -u 1000 appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser

ENTRYPOINT ["dotnet", "GaApi.dll"]
