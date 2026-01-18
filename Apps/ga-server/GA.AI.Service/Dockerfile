# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
# Copy everything immediately to ensure all dependencies are present
COPY . .

# Restore dependencies
RUN dotnet restore "Apps/ga-server/GA.AI.Service/GA.AI.Service.csproj"

# Build
WORKDIR "/src/Apps/ga-server/GA.AI.Service"
RUN dotnet build "GA.AI.Service.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "GA.AI.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy notebooks and published output
COPY --from=publish /app/publish .
COPY Apps/ga-server/GA.AI.Service/Notebooks ./Notebooks

ENTRYPOINT ["dotnet", "GA.AI.Service.dll"]
