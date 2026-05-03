# OrderService Environment Variables Configuration

## 🌐 **Required Environment Variables**

### **Core Application Settings**
```bash
# Application Environment
ASPNETCORE_ENVIRONMENT=Production

# Database Connection (Render provides this automatically)
ConnectionStrings__DefaultConnection=postgresql://user:password@host:5432/orderservice

# Migration Control
RUN_MIGRATIONS=true
MIGRATION_MAX_ATTEMPTS=5
```

### **JWT Authentication**
```bash
# JWT Token Configuration (Must match original SaaSify project)
JwtSettings__SecretKey=your-super-secret-key-that-is-at-least-32-characters-long-for-production
JwtSettings__Issuer=MultiTenantSaaS
JwtSettings__Audience=MultiTenantSaaS
```

### **CloudAMQP Integration**
```bash
# CloudAMQP Connection (Same as RabbitMQ client)
RabbitMQ__HostName=your-cloudamqp-host.rmq.cloudamqp.com
RabbitMQ__UserName=your-cloudamqp-username
RabbitMQ__Password=your-cloudamqp-password
RabbitMQ__VirtualHost=your-cloudamqp-vhost
RabbitMQ__Port=5672
RabbitMQ__SslEnabled=true
```

### **Logging Configuration**
```bash
# Logging Levels
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft=Warning
Logging__LogLevel__Microsoft.EntityFrameworkCore=Information
Logging__LogLevel__RabbitMQ=Information
```

## 🚀 **Render Deployment Variables**

### **Automatic Variables (Set by Render)**
```bash
# Database Connection (automatically set)
ConnectionStrings__DefaultConnection=postgresql://postgres:generated_password@host:5432/orderservice

# Application Port (automatically set)
PORT=10000
ASPNETCORE_URLS=http://0.0.0.0:10000
```

### **Render-Specific Variables**
```bash
# Migration Control for Render
RUN_MIGRATIONS=true
MIGRATION_MAX_ATTEMPTS=5

# .NET Runtime Configuration
DOTNET_ROOT=/usr/share/dotnet
PATH="${PATH}:/root/.dotnet/tools"
```

## 📋 **Complete Environment Variable List**

### **Required for Production**
| Variable | Example Value | Description |
|-----------|---------------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Application environment |
| `ConnectionStrings__DefaultConnection` | `postgresql://...` | Database connection string |
| `RUN_MIGRATIONS` | `true` | Enable automatic migrations |
| `MIGRATION_MAX_ATTEMPTS` | `5` | Max migration retry attempts |
| `JwtSettings__SecretKey` | `your-super-secret-key-32-chars` | JWT signing key (32+ chars) |
| `JwtSettings__Issuer` | `MultiTenantSaaS` | JWT token issuer (matches SaaSify) |
| `JwtSettings__Audience` | `MultiTenantSaaS` | JWT token audience (matches SaaSify) |

### **CloudAMQP (Required for Event Processing)**
| Variable | Example Value | Description |
|-----------|---------------|-------------|
| `RabbitMQ__HostName` | `your-cloudamqp-host.rmq.cloudamqp.com` | CloudAMQP server host |
| `RabbitMQ__UserName` | `your-cloudamqp-username` | CloudAMQP username |
| `RabbitMQ__Password` | `your-cloudamqp-password` | CloudAMQP password |
| `RabbitMQ__VirtualHost` | `your-cloudamqp-vhost` | CloudAMQP virtual host |
| `RabbitMQ__Port` | `5672` | CloudAMQP port |
| `RabbitMQ__SslEnabled` | `true` | Enable SSL for CloudAMQP |

### **Queue Configuration**
| Variable | Example Value | Description |
|-----------|---------------|-------------|
| `ORDER_EXCHANGE` | `order.exchange` | Message exchange name |
| `ORDER_QUEUE` | `order.queue` | Incoming order queue |
| `STATUS_QUEUE` | `order.status.queue` | Status update queue |
| `QUEUE_DURABLE` | `true` | Messages survive restarts |

### **Logging & Debugging**
| Variable | Example Value | Description |
|-----------|---------------|-------------|
| `Logging__LogLevel__Default` | `Information` | Default logging level |
| `Logging__LogLevel__Microsoft` | `Warning` | Microsoft library logging |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore` | `Information` | EF Core logging |
| `Logging__LogLevel__RabbitMQ` | `Information` | CloudAMQP client logging |

## 🔄 **Event-Driven Architecture**

### **How Main API Knows Order is Done**

#### **Option 1: Event-Driven (Recommended)**
```bash
# OrderService publishes OrderStatusUpdated events
SaaSify API consumes events → Updates local database → Frontend polls/gets notified
```

#### **Option 2: Database Synchronization**
```bash
# SaaSify API queries OrderService database directly
SaaSify API → GET /api/orders/{id} → OrderService returns status → Frontend updates
```

#### **Option 3: Hybrid Approach**
```bash
# Both events and direct queries for critical updates
Events for async processing + Direct queries for immediate status checks
```

### **Event Flow Summary**
```
1. SaaSify API creates order → Publishes OrderCreated event
2. OrderService consumes event → Processes order → Publishes OrderStatusUpdated event  
3. SaaSify API consumes event → Updates order status → Frontend notified
4. Frontend receives real-time updates via polling/WebSocket
```

### **Logging & Debugging**
| Variable | Example Value | Description |
|-----------|---------------|-------------|
| `Logging__LogLevel__Default` | `Information` | Default logging level |
| `Logging__LogLevel__Microsoft` | `Warning` | Microsoft library logging |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore` | `Information` | EF Core logging |
| `Logging__LogLevel__RabbitMQ` | `Information` | RabbitMQ client logging |

## 🔧 **Render Configuration**

### **render.yaml Environment Variables**
```yaml
envVars:
  # Core Settings
  - key: ASPNETCORE_ENVIRONMENT
    value: Production
  - key: ConnectionStrings__DefaultConnection
    fromDatabase:
      name: orderservice-db
      property: connectionString
  - key: RUN_MIGRATIONS
    value: true
  - key: MIGRATION_MAX_ATTEMPTS
    value: 5

  # JWT Configuration
  - key: JwtSettings__SecretKey
    generateValue: true
  - key: JwtSettings__Issuer
    value: saasify-orderservice
  - key: JwtSettings__Audience
    value: saasify-client

  # RabbitMQ Configuration
  - key: RabbitMQ__HostName
    value: your-cloudamqp-host.rmq.cloudamqp.com
  - key: RabbitMQ__UserName
    value: your-cloudamqp-username
  - key: RabbitMQ__Password
    value: your-cloudamqp-password
  - key: RabbitMQ__VirtualHost
    value: your-cloudamqp-vhost
  - key: RabbitMQ__Port
    value: 5672
  - key: RabbitMQ__SslEnabled
    value: true

  # Logging Configuration
  - key: Logging__LogLevel__Default
    value: Information
  - key: Logging__LogLevel__Microsoft
    value: Warning
  - key: Logging__LogLevel__Microsoft.EntityFrameworkCore
    value: Information
  - key: Logging__LogLevel__RabbitMQ
    value: Information
```

### **Database Environment Variables**
```yaml
# PostgreSQL Database
- type: pserv
  name: orderservice-db
  runtime: postgres
  plan: free
  envVars:
    - key: POSTGRES_DB
      value: orderservice
    - key: POSTGRES_USER
      value: postgres
    - key: POSTGRES_PASSWORD
      generateValue: true
```

## 🏠 **Local Development Variables**

### **appsettings.json for Local Development**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=order_service;Username=postgres;Password=password"
  },
  "JwtSettings": {
    "SecretKey": "your-super-secret-jwt-key-for-development",
    "Issuer": "saasify-orderservice",
    "Audience": "saasify-client"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "Port": "5672",
    "SslEnabled": "false"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "RabbitMQ": "Information"
    }
  }
}
```

### **.env File for Local Development**
```bash
# Application
ASPNETCORE_ENVIRONMENT=Development
RUN_MIGRATIONS=true
MIGRATION_MAX_ATTEMPTS=5

# Database
ConnectionStrings__DefaultConnection=Host=localhost;Database=order_service;Username=postgres;Password=password

# JWT (Must match original SaaSify project)
JwtSettings__SecretKey=your-super-secret-key-that-is-at-least-32-characters-long-for-production
JwtSettings__Issuer=MultiTenantSaaS
JwtSettings__Audience=MultiTenantSaaS

# RabbitMQ (Local)
RabbitMQ__HostName=localhost
RabbitMQ__UserName=guest
RabbitMQ__Password=guest
RabbitMQ__VirtualHost=/
RabbitMQ__Port=5672
RabbitMQ__SslEnabled=false
```

## 🔒 **Security Considerations**

### **Sensitive Variables**
- **Never commit** secrets to version control
- **Use Render's secret management** for production
- **Generate secure JWT keys** (minimum 256 bits)
- **Rotate secrets regularly** (every 90 days)

### **CloudAMQP Security**
- **Use SSL/TLS** for all connections
- **Create dedicated virtual host** per tenant
- **Use strong passwords** for RabbitMQ credentials
- **Monitor connection logs** for suspicious activity

### **Database Security**
- **Use SSL connections** to PostgreSQL
- **Limit database access** to application only
- **Regular backups** of production data
- **Monitor connection attempts**

## 🚨 **Troubleshooting**

### **Common Issues**

#### **Migration Failures**
```bash
# Check connection string
ConnectionStrings__DefaultConnection=postgresql://user:pass@host:5432/db

# Increase retry attempts
MIGRATION_MAX_ATTEMPTS=10

# Disable migrations temporarily
RUN_MIGRATIONS=false
```

#### **RabbitMQ Connection Issues**
```bash
# Verify CloudAMQP credentials
RabbitMQ__HostName=your-host.rmq.cloudamqp.com
RabbitMQ__UserName=your-username
RabbitMQ__Password=your-password

# Test SSL configuration
RabbitMQ__SslEnabled=true
```

#### **JWT Authentication Issues**
```bash
# Verify JWT settings
JwtSettings__SecretKey=your-256-bit-secret
JwtSettings__Issuer=saasify-orderservice
JwtSettings__Audience=saasify-client
```

### **Debug Commands**

#### **Check Environment Variables**
```bash
# In container
printenv | grep -E "(DATABASE|JWT|RABBITMQ)"

# Check specific variable
echo $ConnectionStrings__DefaultConnection
```

#### **Test Database Connection**
```bash
# Test connection string
dotnet ef database list --connection "$ConnectionStrings__DefaultConnection"
```

#### **Verify RabbitMQ Connection**
```bash
# Test RabbitMQ connection
telnet $RabbitMQ__HostName $RabbitMQ__Port
```

## 📊 **Environment Variable Priority**

### **Priority Order (Highest to Lowest)**
1. **Environment variables** (set by Render)
2. **appsettings.{Environment}.json**
3. **appsettings.json**
4. **Default values** in code

### **Render Environment Variables**
Render automatically sets these variables:
```bash
# Database connection
ConnectionStrings__DefaultConnection=postgresql://...

# Application port
PORT=10000

# Application URLs
ASPNETCORE_URLS=http://0.0.0.0:10000
```

## 🎯 **Production Checklist**

### **Before Deployment**
- [ ] All required variables configured in render.yaml
- [ ] JWT secret key generated (256-bit minimum)
- [ ] CloudAMQP credentials configured
- [ ] Database connection tested
- [ ] SSL/TLS enabled for all connections

### **After Deployment**
- [ ] Verify migrations applied successfully
- [ ] Check health endpoint `/health`
- [ ] Test order creation flow
- [ ] Monitor application logs
- [ ] Verify RabbitMQ connectivity

---

## 📝 **Summary**

Your OrderService requires these **core environment variables** for production:

✅ **Database Connection** - PostgreSQL connection string  
✅ **JWT Configuration** - Authentication and authorization  
✅ **RabbitMQ Settings** - Message queue integration  
✅ **Migration Control** - Database schema management  
✅ **Logging Configuration** - Application monitoring  

The configuration matches the original SaaSify project exactly and is **production-ready** for Render deployment! 🚀
