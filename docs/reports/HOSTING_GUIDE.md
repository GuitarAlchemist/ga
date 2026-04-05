# Guitar Alchemist - Low Budget Hosting Guide

## Architecture Overview

Your Guitar Alchemist application consists of:
- **Frontend**: React/Vite application (ga-client) - Port 5173
- **Backend API**: ASP.NET Core (GaApi) - Port 7001
- **Chatbot**: Blazor application - Port 7002
- **Database**: MongoDB - Port 27017
- **Admin UI**: Mongo Express - Port 8081
- **Monitoring**: Jaeger (optional) - Port 16686

## Low-Budget Hosting Options (Ranked by Cost)

### 🥇 **Option 1: Render.com (Recommended - Best Free Tier)**
**Cost**: $0/month (with limitations)

**What's Included**:
- Free static site hosting (ga-client)
- Free web service with 750 hours/month
- Automatic SSL certificates
- GitHub auto-deployment
- Environment variables management

**Limitations**:
- Services spin down after 15 minutes of inactivity (30-50 second cold start)
- 512 MB RAM per service
- Shared CPU
- No persistent disk storage (need external MongoDB)

**Setup**:
1. **Frontend (ga-client)**:
   - Deploy as Static Site
   - Build command: `cd Apps/ga-client && npm install && npm run build`
   - Publish directory: `Apps/ga-client/dist`
   - Cost: **FREE**

2. **Backend API (GaApi)**:
   - Deploy as Web Service
   - Build command: `dotnet publish Apps/ga-server/GaApi -c Release -o out`
   - Start command: `cd out && dotnet GaApi.dll`
   - Cost: **FREE** (750 hours/month)

3. **MongoDB**:
   - Use MongoDB Atlas free tier (M0)
   - 512 MB storage
   - Cost: **FREE**

**Total Monthly Cost**: **$0** (with cold starts)

---

### 🥈 **Option 2: Railway.app (Best Price/Performance)**
**Cost**: $5-10/month

**What's Included**:
- $5 credit/month on free plan
- Pay only for usage
- No cold starts
- Automatic SSL
- GitHub deployment
- Built-in monitoring

**Pricing**:
- ~$0.000463/GB-hour for RAM
- ~$0.000231/vCPU-hour
- Free outbound bandwidth (first 100 GB)

**Setup**:
1. Deploy all services via `docker-compose.yml`
2. Estimated usage:
   - MongoDB: ~$2/month (512 MB RAM)
   - API: ~$2/month (512 MB RAM)
   - Frontend: ~$1/month (static)
   - Chatbot: ~$2/month (512 MB RAM)

**Total Monthly Cost**: **$5-10** (no cold starts)

---

### 🥉 **Option 3: Oracle Cloud Free Tier (Most Powerful Free Option)**
**Cost**: $0/month (forever free)

**What's Included**:
- 2 AMD-based compute instances (1/8 OCPU + 1 GB RAM each)
- OR 4 ARM-based instances (Ampere A1 - 4 OCPUs + 24 GB RAM total)
- 200 GB block storage
- 10 TB bandwidth/month
- Load balancer

**Limitations**:
- Requires credit card
- Complex setup (Linux VM management)
- Account approval can take time

**Setup**:
1. Create VM with Ubuntu
2. Install Docker and Docker Compose
3. Clone your repository
4. Run `docker-compose up -d`
5. Configure firewall (ports 80, 443, 5173, 7001, 7002)

**Total Monthly Cost**: **$0** (but requires DevOps skills)

---

### 💡 **Option 4: Fly.io**
**Cost**: $0-5/month

**What's Included**:
- 3 shared-cpu-1x VMs with 256 MB RAM (free)
- 3 GB persistent storage (free)
- 160 GB outbound bandwidth (free)
- Automatic SSL
- Global deployment

**Setup**:
1. Install Fly CLI: `curl -L https://fly.io/install.sh | sh`
2. Deploy services:
   ```bash
   fly launch --dockerfile Apps/ga-server/GaApi/Dockerfile
   fly launch --dockerfile Apps/ga-client/Dockerfile
   fly launch --dockerfile Apps/GuitarAlchemistChatbot/Dockerfile
   ```
3. Create MongoDB volume: `fly volumes create mongodb_data --size 1`

**Total Monthly Cost**: **$0-5** (may need to upgrade for better performance)

---

### 💰 **Option 5: DigitalOcean App Platform**
**Cost**: $12/month

**What's Included**:
- 512 MB RAM per service
- Automatic scaling
- Built-in CI/CD
- Load balancing
- SSL certificates

**Setup**:
1. Connect GitHub repository
2. Configure build settings in App Spec
3. Deploy via dashboard

**Total Monthly Cost**: **$12** (3 services × $4)

---

### 🐳 **Option 6: Self-Hosted VPS (DigitalOcean/Linode/Hetzner)**
**Cost**: $4-6/month

**Recommended**: Hetzner Cloud (best price/performance)
- CPX11: 2 vCPU, 2 GB RAM, 40 GB SSD - **€4.15/month (~$4.50)**
- Includes 20 TB bandwidth

**Setup**:
1. Create Ubuntu server
2. Install Docker: `curl -fsSL https://get.docker.com | sh`
3. Clone repository and deploy:
   ```bash
   git clone <your-repo>
   cd ga
   docker-compose up -d
   ```
4. Configure reverse proxy (Caddy/Nginx)
5. Setup automatic SSL with Certbot

**Total Monthly Cost**: **$4-6**

---

## Recommended Architecture for Low Budget

### **Strategy A: Maximize Free Resources**
```
┌─────────────────────┐
│  Render.com         │
│  - Frontend (FREE)  │
│  - API (FREE)       │
└─────────┬───────────┘
          │
          ↓
┌─────────────────────┐
│  MongoDB Atlas      │
│  - Free M0 Tier     │
│  - 512 MB           │
└─────────────────────┘
```
**Total Cost**: $0/month (with cold starts)

---

### **Strategy B: Best User Experience on Budget**
```
┌─────────────────────┐
│  Railway.app        │
│  - Frontend         │
│  - API              │
│  - Chatbot          │
│  - MongoDB          │
└─────────────────────┘
```
**Total Cost**: $5-10/month (no cold starts)

---

### **Strategy C: Maximum Control**
```
┌─────────────────────┐
│  Hetzner VPS        │
│  - All services     │
│  - Docker Compose   │
│  - 2GB RAM          │
└─────────────────────┘
```
**Total Cost**: $4.50/month

---

## MongoDB Hosting Options

### Free Options:
1. **MongoDB Atlas Free Tier (M0)** - RECOMMENDED
   - 512 MB storage
   - Shared cluster
   - Good for development/small apps
   - **Cost: $0**

2. **Railway.app MongoDB Plugin**
   - Included in $5 credit
   - ~$2-3/month usage
   - Automatic backups

### Paid Options:
1. **MongoDB Atlas M2** - $9/month
   - 2 GB storage
   - Better performance
   - Backups included

---

## Deployment Steps (Recommended: Railway.app)

### Prerequisites:
1. GitHub account with your repository
2. Railway.app account (free)

### Step 1: Prepare Repository
```bash
# Ensure docker-compose.yml is at root
# Ensure all Dockerfiles are present
git add .
git commit -m "Prepare for deployment"
git push origin main
```

### Step 2: Deploy on Railway
1. Go to https://railway.app/new
2. Click "Deploy from GitHub repo"
3. Select your repository
4. Railway will detect docker-compose.yml
5. Click "Deploy"

### Step 3: Configure Environment Variables
In Railway dashboard:
- Set `MongoDB__ConnectionString` for API and Chatbot
- Set `VITE_API_URL` for frontend
- Set `ASPNETCORE_ENVIRONMENT=Production`

### Step 4: Setup Custom Domain (Optional)
1. In Railway, go to Settings > Domains
2. Add your domain
3. Configure DNS (CNAME or A record)

---

## Cost Comparison Table

| Option | Monthly Cost | Cold Starts | Setup Difficulty | Best For |
|--------|-------------|-------------|------------------|----------|
| Render.com | $0 | Yes (30-50s) | Easy ⭐⭐⭐ | Testing/Demos |
| Railway.app | $5-10 | No | Easy ⭐⭐⭐ | Production (Budget) |
| Oracle Cloud | $0 | No | Hard ⭐ | DevOps Experts |
| Fly.io | $0-5 | No | Medium ⭐⭐ | Global Apps |
| DigitalOcean | $12 | No | Easy ⭐⭐⭐ | Managed Solution |
| Hetzner VPS | $4.50 | No | Medium ⭐⭐ | Best Value |

---

## Performance Optimization Tips

### 1. Frontend Optimization
```bash
# Enable compression in vite.config.ts
import compression from 'vite-plugin-compression'

export default {
  plugins: [compression()]
}
```

### 2. API Caching
- Enable response caching in ASP.NET Core
- Use Redis for session storage (Railway.app has free Redis plugin)

### 3. Database Optimization
- Create indexes on frequently queried fields
- Enable MongoDB connection pooling
- Use MongoDB Atlas auto-scaling (M10+)

### 4. CDN for Static Assets
- Use Cloudflare CDN (free tier)
- Cache images and fonts
- Enable Brotli compression

---

## Monitoring (Free Options)

1. **UptimeRobot** (free)
   - Monitor up to 50 endpoints
   - 5-minute intervals
   - Email/SMS alerts

2. **Better Stack** (free tier)
   - 10 monitors
   - Status page
   - Incident management

3. **Railway.app Built-in**
   - CPU/RAM metrics
   - Request logs
   - Deployment history

---

## Security Checklist

- [ ] Enable HTTPS (automatic on most platforms)
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Use environment variables for secrets
- [ ] Enable CORS with specific origins
- [ ] Set up MongoDB authentication
- [ ] Enable rate limiting on API
- [ ] Regular security updates via Docker

---

## Backup Strategy

### MongoDB Backups:
1. **MongoDB Atlas** (M2+): Automatic daily backups
2. **Manual backup** (free tier):
   ```bash
   mongodump --uri="mongodb://..." --out=backup
   ```
3. **Automated script** (run weekly):
   ```bash
   #!/bin/bash
   mongodump --uri=$MONGO_URI --out=/backup/$(date +%Y%m%d)
   aws s3 cp /backup s3://my-bucket/ga-backup/ --recursive
   ```

---

## My Recommendation for You

Based on your application requirements:

### **For Development/Testing**:
✅ **Render.com (Free)**
- Zero cost
- Easy GitHub integration
- Good for demos
- Accept cold starts

### **For Production (Low Traffic)**:
✅ **Railway.app ($5-10/month)**
- No cold starts
- Excellent DX
- Built-in monitoring
- Auto-scaling

### **For Production (Cost-Optimized)**:
✅ **Hetzner VPS ($4.50/month)**
- Best price/performance
- Full control
- No cold starts
- Requires Docker knowledge

---

## Quick Start: Railway.app Deployment

1. **Install Railway CLI**:
   ```bash
   npm i -g @railway/cli
   ```

2. **Login and Deploy**:
   ```bash
   railway login
   cd /path/to/ga
   railway init
   railway up
   ```

3. **Set Environment Variables**:
   ```bash
   railway variables set ASPNETCORE_ENVIRONMENT=Production
   railway variables set MongoDB__ConnectionString=mongodb://...
   ```

4. **Access Your App**:
   ```bash
   railway open
   ```

---

## Support and Resources

- **Railway Docs**: https://docs.railway.app
- **Render Docs**: https://render.com/docs
- **MongoDB Atlas**: https://www.mongodb.com/cloud/atlas
- **Docker Compose**: https://docs.docker.com/compose/

---

## Questions?

Feel free to reach out or consult each platform's documentation for detailed setup instructions.

**Last Updated**: January 2025
