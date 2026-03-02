# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first for better cache efficiency
COPY ["Apps/ga-server/GA.AI.Service/GA.AI.Service.csproj", "Apps/ga-server/GA.AI.Service/"]
COPY ["AllProjects.ServiceDefaults/AllProjects.ServiceDefaults.csproj", "AllProjects.ServiceDefaults/"]
COPY ["Common/GA.Business.Core/GA.Business.Core.csproj", "Common/GA.Business.Core/"]
COPY ["GA.Data.MongoDB/GA.Data.MongoDB.csproj", "GA.Data.MongoDB/"]

# Restore dependencies (cached unless .csproj changes)
RUN dotnet restore "Apps/ga-server/GA.AI.Service/GA.AI.Service.csproj"

# Copy remaining source code
COPY . .

# Build
WORKDIR "/src/Apps/ga-server/GA.AI.Service"
RUN dotnet build "GA.AI.Service.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "GA.AI.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false -p:DebugType=none -p:DebugSymbols=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user
RUN useradd -m -u 1000 appuser

# Copy notebooks and published output
COPY --from=publish --chown=appuser:appuser /app/publish .
COPY --chown=appuser:appuser Apps/ga-server/GA.AI.Service/Notebooks ./Notebooks

# Switch to non-root user
USER appuser

ENTRYPOINT ["dotnet", "GA.AI.Service.dll"]
