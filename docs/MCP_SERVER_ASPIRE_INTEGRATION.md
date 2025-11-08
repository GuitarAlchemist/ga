# MCP Server Aspire Integration

## Overview

This document explains how the GaMcpServer has been integrated into the Aspire AppHost orchestration to ensure it survives restarts and is managed alongside other services.

## Problem

Previously, MCP servers were standalone console applications that:
- ❌ Were not managed by Aspire orchestration
- ❌ Did not survive environment restarts
- ❌ Had to be manually started and monitored
- ❌ Were not visible in the Aspire Dashboard
- ❌ Did not benefit from Aspire's telemetry and health checks

## Solution

The GaMcpServer has been added to the Aspire AppHost configuration, making it a first-class managed service.

### Changes Made

#### 1. AppHost Configuration (`AllProjects.AppHost/Program.cs`)

Added GaMcpServer to the orchestration:

```csharp
// Add GaMcpServer (MCP server for AI integrations)
var gaMcpServer = builder.AddProject("ga-mcp-server", @"..\GaMcpServer\GaMcpServer.csproj")
    .WithReference(mongoDatabase)
    .WithReference(redis);
```

#### 2. GaMcpServer Project File (`GaMcpServer/GaMcpServer.csproj`)

Added reference to Aspire service defaults:

```xml
<ItemGroup>
  <ProjectReference Include="..\AllProjects.ServiceDefaults\AllProjects.ServiceDefaults.csproj" />
  <!-- ... other references ... -->
</ItemGroup>
```

#### 3. GaMcpServer Program (`GaMcpServer/Program.cs`)

Added Aspire service defaults with graceful fallback:

```csharp
// Add Aspire service defaults (telemetry, health checks, service discovery)
// Note: This is optional for MCP servers but provides better observability
try
{
    builder.AddServiceDefaults();
}
catch
{
    // If Aspire is not available (running standalone), continue without it
}
```

## Benefits

Now the GaMcpServer:
- ✅ Starts automatically when you run `.\Scripts\start-all.ps1`
- ✅ Survives environment restarts
- ✅ Is monitored and restarted if it crashes
- ✅ Appears in the Aspire Dashboard with logs and metrics
- ✅ Benefits from Aspire's telemetry and health checks
- ✅ Can use service discovery to find MongoDB and Redis
- ✅ Can still run standalone (graceful fallback)

## How to Use

### Starting All Services (Including MCP Server)

```powershell
# Start all services with Aspire orchestration
.\Scripts\start-all.ps1 -Dashboard
```

The GaMcpServer will now start automatically alongside:
- MongoDB
- Redis
- GaApi
- GuitarAlchemistChatbot
- ScenesService
- FloorManager
- ga-client (React frontend)

### Viewing MCP Server in Aspire Dashboard

1. Navigate to https://localhost:15001
2. Look for "ga-mcp-server" in the services list
3. View logs, metrics, and traces for the MCP server

### Running MCP Server Standalone

The MCP server can still run independently:

```powershell
cd GaMcpServer
dotnet run
```

When running standalone, it will gracefully skip Aspire service defaults and run as a normal console application.

## Verification

To verify the integration works:

1. **Start all services:**
   ```powershell
   .\Scripts\start-all.ps1 -Dashboard
   ```

2. **Check Aspire Dashboard:**
   - Open https://localhost:15001
   - Verify "ga-mcp-server" appears in the services list
   - Check that it shows as "Running"

3. **Check logs:**
   - In the Aspire Dashboard, click on "ga-mcp-server"
   - View the logs to ensure it started correctly

4. **Test restart:**
   - Stop the Aspire AppHost (Ctrl+C)
   - Restart with `.\Scripts\start-all.ps1 -Dashboard`
   - Verify the MCP server starts automatically

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Aspire AppHost                           │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Orchestrates all services:                          │  │
│  │  • MongoDB                                           │  │
│  │  • Redis                                             │  │
│  │  • GaApi                                             │  │
│  │  • GuitarAlchemistChatbot                           │  │
│  │  • ScenesService                                     │  │
│  │  • FloorManager                                      │  │
│  │  • GaMcpServer ← Now managed!                       │  │
│  │  • ga-client (React)                                 │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Troubleshooting

### MCP Server Not Starting

1. **Check project reference:**
   ```powershell
   dotnet build GaMcpServer/GaMcpServer.csproj
   ```

2. **Check Aspire Dashboard logs:**
   - Open https://localhost:15001
   - Click on "ga-mcp-server"
   - Review error messages

3. **Verify ServiceDefaults reference:**
   ```powershell
   dotnet list GaMcpServer/GaMcpServer.csproj reference
   ```
   Should include `AllProjects.ServiceDefaults.csproj`

### MCP Server Crashes on Startup

The try-catch block ensures graceful fallback if Aspire is not available. If it crashes:

1. Check that `AllProjects.ServiceDefaults` project builds successfully
2. Verify all dependencies are restored: `dotnet restore`
3. Check for configuration issues in `appsettings.json`

## Next Steps

Consider adding other MCP servers to Aspire orchestration using the same pattern:

```csharp
var anotherMcpServer = builder.AddProject("another-mcp-server", @"..\Path\To\Server.csproj")
    .WithReference(mongoDatabase)
    .WithReference(redis);
```

## Related Documentation

- [Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [MCP Server Documentation](../GaMcpServer/README_WEB_INTEGRATION.md)
- [DevOps Guide](DEVOPS_COMPLETE.md)
- [Developer Guide](DEVELOPER_GUIDE.md)

