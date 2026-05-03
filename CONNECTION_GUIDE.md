# OrderService Connection Guide

## 🌐 **How to Connect to OrderService**

### **Base URLs**
```
Production: https://orderservice.onrender.com
Development: http://localhost:10000
Health Check: https://orderservice.onrender.com/health
Detailed Health: https://orderservice.onrender.com/healthz
```

### **API Endpoints**
```
POST /api/orders              - Create new order
GET /api/orders              - List orders (paginated)
GET /api/orders/{id}          - Get specific order
PUT /api/orders/{id}/status   - Update order status
GET /api/migration/status      - Check migration status
POST /api/migration/apply     - Apply migrations manually
GET /health                    - Simple health check
GET /healthz                   - Detailed health check
```

## 🔐 **Authentication Required**

### **JWT Token Format**
```json
{
  "sub": "user123",
  "email": "user@example.com",
  "role": "User",
  "TenantId": 1,
  "UserId": 123,
  "permission": ["order.create", "order.read"],
  "exp": 1640995200,
  "iss": "MultiTenantSaaS",
  "aud": "MultiTenantSaaS"
}
```

### **Getting JWT Token**
```bash
# Option 1: From SaaSify API
curl -X POST "https://saasifyapi.onrender.com/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "password"
  }'

# Option 2: From existing authenticated session
# Token is stored in localStorage in frontend
const token = localStorage.getItem('accessToken');
```

### **Making Authenticated Requests**
```bash
# Using JWT token in API calls
curl -X POST "https://orderservice.onrender.com/api/orders" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "amount": 99.99,
    "description": "Test Order",
    "customerEmail": "test@example.com"
  }'
```

## 🐰 **RabbitMQ Requirements**

### **Is RabbitMQ Required?**

#### **For OrderService Operation: YES**
- **Order Processing**: Required for consuming order messages
- **Status Updates**: Required for publishing order status changes
- **Background Processing**: Required for async order handling

#### **For Basic API Testing: NO**
- **Create Orders**: Can work without RabbitMQ (saves to database)
- **Get Orders**: Can work without RabbitMQ (reads from database)
- **Health Checks**: Can work without RabbitMQ

### **RabbitMQ Setup Options**

#### **Option 1: CloudAMQP (Recommended for Production)**
```bash
# CloudAMQP Environment Variables
RabbitMQ__HostName=your-cloudamqp-host.rmq.cloudamqp.com
RabbitMQ__UserName=your-cloudamqp-username
RabbitMQ__Password=your-cloudamqp-password
RabbitMQ__VirtualHost=your-cloudamqp-vhost
RabbitMQ__Port=5672
RabbitMQ__SslEnabled=true
```

#### **Option 2: Local RabbitMQ (Development)**
```bash
# Local RabbitMQ Environment Variables
RabbitMQ__HostName=localhost
RabbitMQ__UserName=guest
RabbitMQ__Password=guest
RabbitMQ__VirtualHost=/
RabbitMQ__Port=5672
RabbitMQ__SslEnabled=false
```

#### **Option 3: Docker RabbitMQ (Development)**
```bash
# Run RabbitMQ in Docker
docker run -d --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=guest \
  -e RABBITMQ_DEFAULT_PASS=guest \
  rabbitmq:3.8-management
```

## 🔄 **Complete Connection Flow**

### **Step 1: Get JWT Token**
```bash
# Login to SaaSify API to get JWT
curl -X POST "https://saasifyapi.onrender.com/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "your-email@example.com",
    "password": "your-password"
  }'

# Response contains JWT token
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600,
  "user": {
    "id": 123,
    "email": "user@example.com",
    "tenantId": 1
  }
}
```

### **Step 2: Create Order**
```bash
# Create order with JWT token
curl -X POST "https://orderservice.onrender.com/api/orders" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -d '{
    "amount": 99.99,
    "description": "Premium Subscription",
    "customerEmail": "user@example.com"
  }'

# Response
{
  "message": "Order created successfully",
  "orderId": 456,
  "tenantId": 1,
  "userId": 123,
  "amount": 99.99,
  "status": "Processing",
  "timestamp": "2026-05-03T10:00:00Z"
}
```

### **Step 3: Check Order Status**
```bash
# Poll order status
curl -X GET "https://orderservice.onrender.com/api/orders/456" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

# Response
{
  "id": 456,
  "tenantId": 1,
  "userId": 123,
  "amount": 99.99,
  "status": "Completed",
  "createdAt": "2026-05-03T10:00:00Z",
  "processedAt": "2026-05-03T10:02:00Z"
}
```

## 🌍 **Frontend Integration**

### **JavaScript/React Example**
```javascript
// OrderService API Client
class OrderServiceAPI {
  constructor(baseUrl) {
    this.baseUrl = baseUrl;
    this.token = localStorage.getItem('accessToken');
  }

  async createOrder(orderData) {
    const response = await fetch(`${this.baseUrl}/api/orders`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${this.token}`
      },
      body: JSON.stringify(orderData)
    });

    if (!response.ok) {
      throw new Error(`Order creation failed: ${response.statusText}`);
    }

    return await response.json();
  }

  async getOrder(orderId) {
    const response = await fetch(`${this.baseUrl}/api/orders/${orderId}`, {
      headers: {
        'Authorization': `Bearer ${this.token}`
      }
    });

    return await response.json();
  }

  async getOrders(page = 1, pageSize = 10) {
    const response = await fetch(
      `${this.baseUrl}/api/orders?page=${page}&pageSize=${pageSize}`, {
      headers: {
        'Authorization': `Bearer ${this.token}`
      }
    }
    );

    return await response.json();
  }
}

// Usage
const orderAPI = new OrderServiceAPI('https://orderservice.onrender.com');

// Create order
const newOrder = await orderAPI.createOrder({
  amount: 99.99,
  description: 'Premium Subscription',
  customerEmail: 'user@example.com'
});

console.log('Order created:', newOrder);
```

## 🔧 **Development Setup**

### **Local Development Environment**
```bash
# 1. Clone OrderService
git clone https://github.com/your-repo/OrderService.git
cd OrderService

# 2. Configure environment variables
cp .env.example .env
# Edit .env with your settings

# 3. Run RabbitMQ (optional for testing)
docker run -d --name rabbitmq -p 5672:5672 rabbitmq:3.8-management

# 4. Start OrderService
dotnet run

# 5. Test connection
curl http://localhost:10000/health
```

### **Environment File (.env)**
```bash
# .env for local development
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Database=order_service;Username=postgres;Password=password
RUN_MIGRATIONS=true
MIGRATION_MAX_ATTEMPTS=5

# JWT (matches SaaSify project)
JwtSettings__SecretKey=your-super-secret-key-that-is-at-least-32-characters-long-for-production
JwtSettings__Issuer=MultiTenantSaaS
JwtSettings__Audience=MultiTenantSaaS

# RabbitMQ (optional for testing)
RabbitMQ__HostName=localhost
RabbitMQ__UserName=guest
RabbitMQ__Password=guest
RabbitMQ__VirtualHost=/
RabbitMQ__Port=5672
RabbitMQ__SslEnabled=false
```

## 🚀 **Production Deployment**

### **Render Deployment Steps**
```bash
# 1. Push to GitHub
git add .
git commit -m "Deploy OrderService"
git push origin main

# 2. Configure Render Dashboard
# - Connect GitHub repository
# - Create Web Service (OrderService)
# - Create PostgreSQL database (orderservice-db)
# - Set environment variables

# 3. Deploy
# Render automatically builds and deploys
# Migrations run automatically on startup
```

### **Production URLs**
```
OrderService API: https://orderservice.onrender.com
Health Check: https://orderservice.onrender.com/health
Swagger UI: https://orderservice.onrender.com/swagger
```

## 🔍 **Testing Connection**

### **Health Check Tests**
```bash
# Test basic health
curl https://orderservice.onrender.com/health

# Test detailed health
curl https://orderservice.onrender.com/healthz

# Expected response
{
  "Status": "Healthy",
  "Service": "OrderService",
  "Timestamp": "2026-05-03T10:00:00Z",
  "Version": "1.0.0"
}
```

### **API Integration Test**
```bash
# Test complete flow
TOKEN=$(curl -s -X POST "https://saasifyapi.onrender.com/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password"}' | \
  jq -r '.token')

# Create order
curl -X POST "https://orderservice.onrender.com/api/orders" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "amount": 29.99,
    "description": "Test Order",
    "customerEmail": "test@example.com"
  }'

# Check order
curl -X GET "https://orderservice.onrender.com/api/orders/1" \
  -H "Authorization: Bearer $TOKEN"
```

## 🛠️ **Troubleshooting**

### **Connection Issues**
```bash
# Check if service is running
curl -I https://orderservice.onrender.com/health

# Check service logs in Render dashboard
# Verify environment variables are set correctly
# Check JWT token is valid and not expired
```

### **RabbitMQ Issues**
```bash
# Test RabbitMQ connection
telnet your-cloudamqp-host.rmq.cloudamqp.com 5672

# Check RabbitMQ logs in CloudAMQP dashboard
# Verify SSL certificate is valid
# Confirm virtual host exists
```

### **Authentication Issues**
```bash
# Verify JWT issuer and audience match
# Check token is not expired
# Confirm TenantId and UserId are present
# Test with valid user credentials
```

## 📋 **Connection Summary**

### **Required Components**
- ✅ **JWT Token** - From SaaSify API authentication
- ✅ **Base URL** - `https://orderservice.onrender.com`
- ✅ **Authorization Header** - `Bearer <JWT_TOKEN>`
- ✅ **Content-Type** - `application/json`

### **Optional Components**
- 🔄 **RabbitMQ** - For message processing (production recommended)
- 🐳 **Docker** - For local development
- 🗄️ **PostgreSQL** - Database (Render provides)

### **Connection Methods**
1. **Direct API** - HTTP requests to OrderService endpoints
2. **SDK Integration** - Use fetch/axios in frontend
3. **WebSocket** - Real-time updates (future enhancement)
4. **Message Queue** - RabbitMQ for async processing

---

## 🎯 **Quick Start**

1. **Get JWT** from SaaSify API login
2. **Create Order** via POST `/api/orders`
3. **Track Status** via GET `/api/orders/{id}`
4. **Monitor Health** via `/health` endpoint

**RabbitMQ is required for full order processing but not for basic API testing!** 🚀
