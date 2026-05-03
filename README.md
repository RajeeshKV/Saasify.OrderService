# OrderService - SaaSify Microservice

A production-ready microservice for order processing in the SaaSify multi-tenant SaaS platform.

## 🏗️ **Architecture Overview**

```
Frontend
   ↓
SaaSify API (Publisher)
   ↓
RabbitMQ (CloudAMQP)
   ↓
OrderService (Consumer)
   ↓
Order Database (PostgreSQL)
```

## ✨ **Features**

### 🎯 **Core Functionality**
- **Event-Driven Architecture** with RabbitMQ/CloudAMQP
- **Multi-Tenant Support** with tenant isolation
- **JWT Authentication** with tenant propagation
- **Order Processing** with status tracking
- **Message Queuing** with retry mechanisms
- **Health Checks** and monitoring

### 🔧 **Technical Features**
- **.NET 8.0** with Entity Framework Core
- **PostgreSQL** with snake_case naming
- **RabbitMQ.Client** with CloudAMQP integration
- **Docker** containerization
- **Render deployment** ready
- **Comprehensive logging** and error handling

## 📁 **Project Structure**

```
OrderService/
├── Controllers/
│   └── OrdersController.cs          # REST API endpoints
├── Domain/
│   ├── Order.cs                     # Order entity
│   └── OrderMessage.cs              # Message contracts
├── Application/
│   ├── OrderService.cs              # Business logic
│   └── MessageProcessorService.cs   # Background service
├── Infrastructure/
│   ├── OrderDbContext.cs            # Database context
│   ├── RabbitMQService.cs           # RabbitMQ client
│   ├── DependencyInjection.cs       # DI configuration
│   └── DesignTimeDbContextFactory.cs # EF migrations
├── Program.cs                       # Application entry point
├── appsettings.json                 # Configuration
├── Dockerfile                       # Container config
└── render.yaml                      # Render deployment
```

## 🚀 **Quick Start**

### **Prerequisites**
- .NET 8.0 SDK
- PostgreSQL
- RabbitMQ/CloudAMQP
- Docker (optional)

### **Local Development**

1. **Clone and Build**
```bash
cd OrderService
dotnet restore
dotnet build
```

2. **Configure Database**
```bash
# Update appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=order_service;Username=postgres;Password=password"
  }
}
```

3. **Run Migrations**
```bash
dotnet ef database update
```

4. **Start the Service**
```bash
dotnet run
```

### **Docker Development**

1. **Build Image**
```bash
docker build -t orderservice .
```

2. **Run Container**
```bash
docker run -p 8080:8080 orderservice
```

## 🔗 **API Endpoints**

### **Order Management**

#### **Create Order**
```http
POST /api/orders
Authorization: Bearer <jwt-token>
Content-Type: application/json

{
  "amount": 99.99,
  "currency": "USD",
  "description": "Premium subscription",
  "customerEmail": "user@example.com"
}
```

#### **Get Order**
```http
GET /api/orders/{id}
Authorization: Bearer <jwt-token>
```

#### **Get Orders (Paginated)**
```http
GET /api/orders?page=1&pageSize=10
Authorization: Bearer <jwt-token>
```

#### **Update Order Status**
```http
PUT /api/orders/{id}/status
Authorization: Bearer <jwt-token>
Content-Type: application/json

{
  "status": "Completed"
}
```

### **Health Check**

#### **Service Health**
```http
GET /health
```

## 🐰 **RabbitMQ Integration**

### **Message Flow**

1. **SaaSify API** publishes `OrderCreated` event
2. **OrderService** consumes message from `order.queue`
3. **OrderService** processes order and updates status
4. **OrderService** publishes `OrderStatusUpdated` event

### **Message Contracts**

#### **OrderCreated Message**
```json
{
  "tenantId": 1,
  "userId": 123,
  "amount": 99.99,
  "currency": "USD",
  "description": "Premium subscription",
  "customerEmail": "user@example.com",
  "metadata": {},
  "messageType": "OrderCreated",
  "timestamp": "2026-05-03T10:00:00Z",
  "correlationId": "guid-here"
}
```

#### **OrderStatusUpdated Message**
```json
{
  "orderId": 456,
  "tenantId": 1,
  "status": "Completed",
  "messageType": "OrderStatusUpdated",
  "timestamp": "2026-05-03T10:01:00Z",
  "correlationId": "guid-here"
}
```

### **CloudAMQP Configuration**

```bash
# Environment Variables
RabbitMQ__HostName=your-cloudamqp-host.rmq.cloudamqp.com
RabbitMQ__UserName=your-cloudamqp-username
RabbitMQ__Password=your-cloudamqp-password
RabbitMQ__VirtualHost=your-cloudamqp-vhost
RabbitMQ__Port=5672
RabbitMQ__SslEnabled=true
```

## 🏢 **Multi-Tenancy**

### **Tenant Isolation**
- **Database Level**: Orders filtered by `tenant_id`
- **API Level**: JWT token contains `TenantId` claim
- **Message Level**: Messages include tenant context

### **Tenant Propagation**
```csharp
// JWT Claims
{
  "TenantId": 1,
  "UserId": 123,
  "permissions": ["order.create", "order.read"]
}
```

## 🚀 **Deployment**

### **Render Deployment**

1. **Push to GitHub**
```bash
git add .
git commit -m "Add OrderService microservice"
git push origin main
```

2. **Configure Render**
- Connect GitHub repository
- Use `render.yaml` configuration
- Set environment variables
- Deploy automatically

### **Environment Variables**

#### **Required**
```bash
ASPNETCORE_ENVIRONMENT=Production
DATABASE_URL=postgresql://...
JwtSettings__SecretKey=your-secret-key
JwtSettings__Issuer=saasify-orderservice
JwtSettings__Audience=saasify-client
```

#### **CloudAMQP**
```bash
RabbitMQ__HostName=your-cloudamqp-host.rmq.cloudamqp.com
RabbitMQ__UserName=your-cloudamqp-username
RabbitMQ__Password=your-cloudamqp-password
RabbitMQ__VirtualHost=your-cloudamqp-vhost
RabbitMQ__Port=5672
RabbitMQ__SslEnabled=true
```

## 📊 **Monitoring & Logging**

### **Health Checks**
- **Service Health**: `/health`
- **Database Connectivity**: Built-in EF Core health check
- **RabbitMQ Connectivity**: Connection validation on startup

### **Logging Levels**
```json
{
  "Logging": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Microsoft.EntityFrameworkCore": "Information",
    "RabbitMQ": "Information"
  }
}
```

### **Key Metrics**
- Order processing rate
- Message queue depth
- Database connection health
- API response times

## 🔄 **Error Handling & Resilience**

### **Message Processing**
- **Automatic Retries**: Failed messages are re-queued
- **Dead Letter Queue**: Failed after max retries
- **Idempotent Processing**: Safe message reprocessing
- **Circuit Breaker**: Prevents cascade failures

### **Database Operations**
- **Transaction Management**: ACID compliance
- **Connection Resilience**: Automatic retry on failures
- **Migration Support**: Database versioning

## 🧪 **Testing**

### **Unit Tests**
```bash
dotnet test
```

### **Integration Tests**
```bash
# Test with TestContainers for RabbitMQ and PostgreSQL
dotnet test --filter "Category=Integration"
```

### **Load Testing**
```bash
# Use k6 or Apache Bench for load testing
k6 run load-test.js
```

## 🔧 **Configuration**

### **appsettings.json**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=order_service;Username=postgres;Password=password"
  },
  "JwtSettings": {
    "SecretKey": "your-super-secret-jwt-key",
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
  }
}
```

## 📈 **Scalability**

### **Horizontal Scaling**
- **Stateless Design**: Easy horizontal scaling
- **Load Balancer Ready**: Multiple instances supported
- **Database Scaling**: Read replicas for queries
- **Queue Scaling**: Multiple consumers per queue

### **Performance Optimization**
- **Connection Pooling**: Database and RabbitMQ
- **Async Processing**: Non-blocking operations
- **Caching**: In-memory for frequent data
- **Pagination**: Large result sets

## 🔒 **Security**

### **Authentication**
- **JWT Tokens**: Bearer token authentication
- **Tenant Isolation**: Cross-tenant access prevention
- **Role-Based Access**: Permission-based authorization

### **Data Protection**
- **Encryption**: TLS 1.3 for all communications
- **PII Protection**: Personal data encryption
- **Audit Logging**: All operations tracked

## 🤝 **Integration with SaaSify API**

### **Event Publishing**
```csharp
// In SaaSify API
await _orderEventPublisher.PublishOrderCreatedAsync(
    tenantId: 1,
    userId: 123,
    amount: 99.99,
    description: "Premium subscription",
    customerEmail: "user@example.com"
);
```

### **Status Updates**
```csharp
// Order status updates flow back to main API
await _orderEventPublisher.PublishOrderUpdatedAsync(
    orderId: 456,
    tenantId: 1,
    status: "Completed"
);
```

## 📝 **Development Notes**

### **Best Practices**
- **Domain-Driven Design**: Clear domain boundaries
- **SOLID Principles**: Maintainable code structure
- **Clean Architecture**: Separation of concerns
- **Test-Driven Development**: Comprehensive test coverage

### **Performance Considerations**
- **Database Indexes**: Optimized for tenant queries
- **Message Batching**: Efficient queue processing
- **Memory Management**: Proper disposal of resources
- **Async/Await**: Non-blocking I/O operations

---

## 🎉 **You're Ready!**

Your OrderService microservice is now production-ready with:

- ✅ **Event-driven architecture** with CloudAMQP
- ✅ **Multi-tenant support** with proper isolation
- ✅ **Production deployment** configuration
- ✅ **Comprehensive monitoring** and health checks
- ✅ **Scalable design** for high availability
- ✅ **Security best practices** implemented

Deploy to Render and start processing orders in minutes! 🚀
