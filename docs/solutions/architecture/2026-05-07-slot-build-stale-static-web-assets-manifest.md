---
date: 2026-05-07
module: build-pipeline
tags: [msbuild, blue-green, static-web-assets, slot-build, dev-loop]
problem_type: silent-staleness
status: resolved
---

# Slot build leaves a stale static-web-assets runtime manifest in `bin/<slot>/`

## Symptom

GaApi crashed at startup with:

```
System.IO.DirectoryNotFoundException:
  C:\Users\spare\source\repos\ga\.dotnet\.nuget\packages\mudblazor\8.14.0\staticwebassets\
```

The path doesn't exist and never has on this machine. The package is in the standard global cache (`~/.nuget/packages/mudblazor/8.14.0/`). Yet `dotnet restore` says "All projects are up-to-date" and `dotnet build` reports zero errors.

After fixing the missing path manually, a second 404 surfaced: `https://demos.guitaralchemist.com/chatbot/` returned 404 even though `/chatbot/index.html` returned 200. The chatbot SPA shell wasn't reachable through the trailing-slash form.

## Root cause

Two distinct but coupled defects:

### 1. `start-dev.ps1` builds to one path and launches from another

`Scripts/start-dev.ps1` builds GaApi with `dotnet build … -c Debug` (no slot flag), then launches `bin/$slot/net10.0/GaApi.exe`. GaApi.csproj defaults `UseBlueGreenSlots` to `false`, so the build output goes to **`bin/Debug/net10.0/`**, not `bin/blue/`. Whatever was last in `bin/blue/` — possibly weeks old — gets launched as if it were fresh. The `Test-Path $gaApiExe` check passes because the stale exe still exists. There's no warning that the binary is older than the source.

### 2. The stale manifest pointed at a NuGet cache that no longer existed

The April 26 `bin/blue/net10.0/GaApi.staticwebassets.runtime.json` had been generated when this repo was using a project-local NuGet cache at `.dotnet/.nuget/packages/`. That cache was later cleaned/removed. The runtime manifest baked in absolute paths like `…\.dotnet\.nuget\packages\mudblazor\…\staticwebassets\` and ASP.NET's `StaticWebAssetsLoader` opens those paths during `WebApplicationBuilder.CreateBuilder(args)` — *before* user code runs — so the missing directory becomes a startup-fatal `DirectoryNotFoundException`.

A fresh `dotnet build … -p:UseBlueGreenSlots=true` rewrites the slot manifest correctly: `bin/blue/net10.0/GaApi.staticwebassets.runtime.json` lands fresh, references the live global cache (`C:\Users\spare\.nuget\packages\…`), and the chatbot directory entries (`/chatbot/index.html` etc.) appear in the manifest tree.

### 3. Trailing-slash 404 for `/chatbot/` (independent)

ASP.NET's `ManifestStaticWebAssetFileProvider` doesn't expose nested static-web-asset directories as enumerable, so `UseDefaultFiles` can't auto-resolve `/chatbot/` to `/chatbot/index.html`. The named file at `/chatbot/index.html` works because it's a direct manifest lookup.

## Fix

Three small patches:

**`Scripts/start-dev.ps1`** — pass the slot flag so the build path matches the launch path:

```powershell
dotnet build "$repoRoot\Apps\ga-server\GaApi\GaApi.csproj" -c Debug -p:UseBlueGreenSlots=true | Out-Null
```

**`Apps/ga-server/GaApi/Program.cs`** — explicit rewrite for the SPA shell so `UseDefaultFiles` doesn't depend on manifest directory enumeration:

```csharp
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value;
    if (path is "/chatbot" or "/chatbot/")
    {
        ctx.Request.Path = "/chatbot/index.html";
    }
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
```

**Recovery procedure when a slot build is already stale**: a one-shot `dotnet clean … -p:UseBlueGreenSlots=true && dotnet build … -p:UseBlueGreenSlots=true` regenerates the slot manifest. Don't manually edit the runtime JSON or symlink `.dotnet/` — both are fragile and don't address why the slot got out of sync.

## How to detect this earlier

- A startup `DirectoryNotFoundException` referencing a NuGet package path is almost always a stale `*.staticwebassets.runtime.json`. Check the timestamp against the rest of `bin/<slot>/`.
- If `Test-Path $gaApiExe` succeeds but the file is older than the source tree by more than a day, the dev script is silently launching an old binary. Add a `Get-Item $gaApiExe | Where-Object LastWriteTime -lt (Get-ChildItem … -Recurse | Sort-Object LastWriteTime -Descending | Select -First 1).LastWriteTime` guard.
- Trailing-slash 404 vs `/index.html` 200 is the manifest-directory-enumeration limitation, not a routing bug. Don't chase the wrong layer.

## What we did **not** do

- We did **not** create or symlink `.dotnet/.nuget/packages/`. The path is fictional; making it real to placate a stale manifest hides the actual problem.
- We did **not** drop `UseBlueGreenSlots`. Slot deploys are intentional infrastructure for production cutovers; the bug was in dev tooling, not the slot model.
- We did **not** reach for `app.MapFallbackToFile("/chatbot/{*path}", "chatbot/index.html")`. That swallows every 404 under `/chatbot` and would mask future routing mistakes; the explicit `Use()` rewrite is scoped tightly to the two known shell paths.
