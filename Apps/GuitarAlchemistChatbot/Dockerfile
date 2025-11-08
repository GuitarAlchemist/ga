# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["AllProjects.sln", "./"]
COPY ["Apps/GuitarAlchemistChatbot/GuitarAlchemistChatbot.csproj", "Apps/GuitarAlchemistChatbot/"]
COPY ["AllProjects.ServiceDefaults/AllProjects.ServiceDefaults.csproj", "AllProjects.ServiceDefaults/"]
COPY ["GA.Data.MongoDB/GA.Data.MongoDB.csproj", "GA.Data.MongoDB/"]
COPY ["Common/GA.Business.Core/GA.Business.Core.csproj", "Common/GA.Business.Core/"]
COPY ["Common/GA.Core/GA.Core.csproj", "Common/GA.Core/"]

# Restore dependencies
RUN dotnet restore "Apps/GuitarAlchemistChatbot/GuitarAlchemistChatbot.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/Apps/GuitarAlchemistChatbot"
RUN dotnet build "GuitarAlchemistChatbot.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "GuitarAlchemistChatbot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GuitarAlchemistChatbot.dll"]

