# /devfix — Emergency Dev Stack Recovery

## When to use
- Site shows 502 Bad Gateway on demos.guitaralchemist.com
- Vite dev server is down (port 5176 not responding)
- API server is down or unhealthy (port 5232)
- User says "site is down", "502", "cloudflare error", "can't access demos"

## What it does

Diagnoses and fixes the dev stack in 30 seconds:

### Step 1: Diagnose
```bash
# Check what's running
curl -s http://localhost:5176 -o /dev/null -w "%{http_code}" 2>&1  # Vite
curl -s http://localhost:5232/health 2>&1                           # API
curl -s -o /dev/null -w "%{http_code}" https://demos.guitaralchemist.com 2>&1  # Tunnel
```

### Step 2: Report
Tell the user exactly what's wrong:
- **Vite down (port 5176)** → 502 on demos site. Most common issue.
- **API down (port 5232)** → API calls fail, but static site may work.
- **Both down** → Full outage.
- **Tunnel down (cloudflared not running)** → Rare. Check `Get-Process cloudflared`.

### Step 3: Fix
Tell the user to run ONE of these:

**Quick fix (Vite only):**
```
! cd ReactComponents/ga-react-components && npx vite --host --port 5176
```

**Full fix (Vite + API):**
```
! pwsh Scripts/start-dev.ps1
```

**Nuclear option (everything including Aspire):**
```
! pwsh Scripts/start-all.ps1 -NoBuild -Dashboard
```

### Step 4: Verify
```bash
# Wait 10s then check
curl -s -o /dev/null -w "%{http_code}" https://demos.guitaralchemist.com
```

## Architecture
- **Cloudflare Tunnel** (cloudflared) → routes demos.guitaralchemist.com to localhost:5176
- **Vite** (port 5176) → serves React frontend, proxies /api/* to localhost:5232
- **GaApi** (port 5232) → .NET backend
- Tunnel process usually survives reboots (Windows service), but Vite/API don't auto-restart

## Common causes of 502
1. Vite crashed or was never started after a reboot
2. `npm run build` killed the dev server process
3. Port conflict (another process on 5176)
4. Windows update restarted the machine

## Do NOT
- Don't restart cloudflared unless the tunnel itself is broken
- Don't run `start-all.ps1` with `-Dashboard` unless Aspire is needed
- Don't rebuild — use `-NoBuild` flag for speed
