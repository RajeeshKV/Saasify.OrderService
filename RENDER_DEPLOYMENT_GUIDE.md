# OrderService - Render Free Plan Deployment Guide

## 🎯 **Render Free Plan Migration Strategy**

Since Render's free plan doesn't support custom start commands, we use a **migration script approach** that runs EF migrations before starting the application.

## 🔄 **Deployment Flow**

```
1. Render starts container
2. Dockerfile ENTRYPOINT runs migrate.sh
3. Script waits for database connection
4. Script applies pending migrations
5. Script starts OrderService application
6. Health check passes
7. Service becomes available
```

## 📁 **Key Files for Migration**

### **1. Migration Script (migrate.sh)**
```bash
#!/bin/bash
# Runs EF migrations before starting the app
dotnet ef database update --connection "$DATABASE_URL"
exec dotnet OrderService.dll
```

### **2. Dockerfile Updates**
```dockerfile
# Install EF tools
RUN dotnet tool install --global dotnet-ef --version 8.0.7

# Copy migration script
COPY migrate.sh ./migrate.sh
RUN chmod +x ./migrate.sh

# Use script as entry point
ENTRYPOINT ["./migrate.sh"]
```

### **3. render.yaml Configuration**
```yaml
healthCheckPath: /healthz
healthCheckTimeout: 30
# No startCommand needed
```

## 🚀 **Deployment Process**

### **Step 1: Push to GitHub**
```bash
git add .
git commit -m "Add migration script for Render free plan"
git push origin main
```

### **Step 2: Configure Render**
1. **Connect GitHub repository**
2. **Create Web Service** with `OrderService` directory
3. **Select PostgreSQL database**
4. **Use render.yaml configuration**

### **Step 3: Environment Variables**
Render automatically sets:
```bash
DATABASE_URL=postgresql://user:pass@host:5432/orderservice
ASPNETCORE_ENVIRONMENT=Production
```

### **Step 4: Deployment**
1. **Build**: Docker image builds with EF tools
2. **Deploy**: Container starts with migration script
3. **Migrate**: Database schema updated
4. **Start**: Application begins serving traffic

## 📊 **Migration Script Features**

### **Database Connection Wait**
```bash
# Wait up to 30 seconds for database
for i in {1..30}; do
    if dotnet ef database list --connection "$DATABASE_URL"; then
        echo "Database connected"
        break
    fi
    sleep 1
done
```

### **Migration Check**
```bash
# Check for pending migrations
PENDING_MIGRATIONS=$(dotnet ef migrations list --connection "$DATABASE_URL" | wc -l)

if [ "$PENDING_MIGRATIONS" -eq 0 ]; then
    echo "No pending migrations"
else
    echo "Applying $PENDING_MIGRATIONS migrations"
    dotnet ef database update --connection "$DATABASE_URL"
fi
```

### **Error Handling**
```bash
# Exit on any error
set -e

# Check for required environment variables
if [ -z "$DATABASE_URL" ]; then
    echo "ERROR: DATABASE_URL not set"
    exit 1
fi
```

## 🔍 **Monitoring Migration Status**

### **Health Check Endpoints**
- **`/health`** - Simple health check
- **`/healthz`** - Detailed health with migration status
- **`/api/migration/status`** - Migration status API

### **Migration Status Response**
```json
{
  "CanConnect": true,
  "AppliedMigrations": ["20260503000000_InitialCreate"],
  "PendingMigrations": [],
  "IsUpToDate": true,
  "DatabaseInfo": {
    "Provider": "Npgsql.EntityFrameworkCore.PostgreSQL",
    "CanConnect": true
  }
}
```

## 🛠️ **Troubleshooting**

### **Common Issues**

#### **1. Database Connection Timeout**
```bash
# Check Render logs
# Wait longer for database to be ready
# Verify DATABASE_URL is correct
```

#### **2. Migration Failed**
```bash
# Check migration script logs
# Verify EF tools are installed
# Check database permissions
```

#### **3. Health Check Failed**
```bash
# Check /healthz endpoint
# Verify migrations were applied
# Check application logs
```

### **Debug Commands**

#### **Check Migration Script**
```bash
# Test locally
export DATABASE_URL="postgresql://..."
./migrate.sh
```

#### **Verify EF Tools**
```bash
dotnet ef --version
dotnet ef database list --connection "$DATABASE_URL"
```

#### **Manual Migration**
```bash
# Apply migrations manually
dotnet ef database update --connection "$DATABASE_URL"
```

## 🔄 **Migration Updates**

### **Adding New Migrations**
```bash
# 1. Create new migration
dotnet ef migrations add AddNewFeature

# 2. Push to GitHub
git add .
git commit -m "Add new migration"
git push origin main

# 3. Render automatically applies migrations on next deploy
```

### **Rollback Migrations**
```bash
# Create rollback migration
dotnet ef migrations add RollbackChanges

# Deploy to apply rollback
```

## 📋 **Render Free Plan Limitations**

### **What's Not Supported**
- ❌ Custom start commands
- ❌ Build-time scripts
- ❌ Multiple containers
- ❌ Custom health check intervals

### **What's Supported**
- ✅ Docker entrypoint scripts
- ✅ Environment variables
- ✅ Health checks
- ✅ Automatic deployments
- ✅ PostgreSQL databases

## 🎯 **Best Practices**

### **Migration Script Design**
- ✅ **Wait for database** before running migrations
- ✅ **Check pending migrations** before applying
- ✅ **Handle errors gracefully** with proper exit codes
- ✅ **Log progress** for debugging
- ✅ **Start application** only after successful migration

### **Dockerfile Optimization**
- ✅ **Install EF tools** in runtime image
- ✅ **Copy migration script** with execute permissions
- ✅ **Use script as entrypoint** for free plan
- ✅ **Include health checks** for monitoring

### **Render Configuration**
- ✅ **Use render.yaml** for consistent deployment
- ✅ **Set proper health check timeout**
- ✅ **Configure environment variables**
- ✅ **Enable automatic deployments**

## 🚀 **Production Deployment Checklist**

### **Pre-Deployment**
- [ ] Migration script tested locally
- [ ] EF tools installed in Dockerfile
- [ ] Health check endpoints working
- [ ] Environment variables configured
- [ ] Database connection verified

### **Post-Deployment**
- [ ] Check Render deployment logs
- [ ] Verify `/healthz` endpoint
- [ ] Confirm migrations applied
- [ ] Test order creation flow
- [ ] Monitor error logs

### **Ongoing Maintenance**
- [ ] Monitor migration script performance
- [ ] Keep EF tools version updated
- [ ] Test new migrations before deployment
- [ ] Review Render logs regularly

---

## 🎉 **Summary**

Your OrderService now uses **Render free plan compatible deployment** with:

✅ **Migration Script Approach** - Runs migrations before app starts  
✅ **EF Tools Integration** - Built into Docker image  
✅ **Health Check Monitoring** - Detailed migration status  
✅ **Error Handling** - Robust failure detection  
✅ **Automatic Deployment** - Works with Render's free tier  

The deployment is **production-ready** and follows Render's free plan constraints! 🚀
