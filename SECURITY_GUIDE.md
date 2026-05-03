# OrderService Security Guide

## 🔒 **Security Architecture Overview**

The OrderService microservice implements defense-in-depth security with multiple layers of protection:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Frontend      │───▶│   JWT Token     │───▶│   TLS/HTTPS     │
│   (Browser)     │    │   Validation    │    │   Encryption    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Rate Limiting  │───▶│  Tenant Isolation│───▶│  Input Validation│
│   Per Tenant     │    │  Database Level  │    │  & Sanitization  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## 🎫 **JWT Authentication System**

### **Token Structure**
```json
{
  "sub": "user123",
  "email": "user@example.com", 
  "role": "User",
  "TenantId": 1,
  "UserId": 123,
  "permission": ["order.create", "order.read"],
  "exp": 1640995200,
  "iat": 1640991600,
  "iss": "saasify-orderservice",
  "aud": "saasify-client",
  "jti": "unique-token-id"
}
```

### **JWT Validation Process**
```csharp
// In Program.cs
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});
```

### **Token Extraction & Validation**
```csharp
// Base controller for all API endpoints
[ApiController]
[Authorize]
public abstract class BaseController : ControllerBase
{
    protected int GetCurrentTenantId()
    {
        var tenantIdClaim = User.FindFirst("TenantId")?.Value;
        if (!int.TryParse(tenantIdClaim, out var tenantId))
        {
            throw new UnauthorizedAccessException("Invalid or missing TenantId in token");
        }
        return tenantId;
    }

    protected int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid or missing UserId in token");
        }
        return userId;
    }

    protected bool HasPermission(string permission)
    {
        var permissions = User.FindAll("permission")?.Select(p => p.Value) ?? [];
        return permissions.Contains(permission);
    }
}
```

## 🏢 **Multi-Tenant Security**

### **Tenant Isolation Layers**

#### **1. API Level Isolation**
```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Order>> GetOrder(int id)
{
    var tenantId = GetCurrentTenantId(); // Extracted from JWT
    var userId = GetCurrentUserId();       // Extracted from JWT
    
    // Always include tenant filter
    var order = await _orderService.GetOrderAsync(id, tenantId);
    
    if (order == null)
    {
        return NotFound(); // Tenant isolation prevents cross-tenant access
    }
    
    // Additional user-level check if needed
    if (order.UserId != userId && !HasPermission("order.read.all"))
    {
        return Forbid();
    }
    
    return Ok(order);
}
```

#### **2. Database Level Isolation**
```csharp
public async Task<Order> GetOrderAsync(int orderId, int tenantId)
{
    // Always filter by tenant ID - no exceptions
    return await _context.Orders
        .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);
}

public async Task<IEnumerable<Order>> GetOrdersByTenantAsync(int tenantId, int page = 1, int pageSize = 10)
{
    // Tenant-scoped query
    return await _context.Orders
        .Where(o => o.TenantId == tenantId)
        .OrderByDescending(o => o.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
}
```

#### **3. Message Level Isolation**
```csharp
// RabbitMQ messages always include tenant context
public async Task PublishOrderCreatedAsync(int tenantId, int userId, decimal amount, string description, string customerEmail)
{
    var orderMessage = new OrderMessage
    {
        TenantId = tenantId,  // Critical for tenant isolation
        UserId = userId,
        Amount = amount,
        Description = description,
        CustomerEmail = customerEmail,
        MessageType = "OrderCreated",
        Timestamp = DateTime.UtcNow,
        CorrelationId = Guid.NewGuid().ToString()
    };

    await PublishMessageAsync(orderMessage, "order.queue");
}

// Consumer validates tenant context
public async Task ProcessOrderMessageAsync(OrderMessage message, CancellationToken cancellationToken)
{
    // Verify tenant exists and is active
    var tenant = await _tenantService.GetTenantAsync(message.TenantId);
    if (tenant == null || !tenant.IsActive)
    {
        _logger.LogWarning("Invalid tenant in message: TenantId={TenantId}", message.TenantId);
        return; // Silently reject invalid tenant messages
    }
    
    // Process order with tenant context
    await _orderService.ProcessOrderAsync(message);
}
```

## 🛡️ **Input Validation & Sanitization**

### **API Input Validation**
```csharp
public record CreateOrderRequest(
    [Required][Range(0.01, 10000.00)] decimal Amount,
    [Required][StringLength(500)] string Description,
    [EmailAddress][StringLength(255)] string? CustomerEmail,
    Dictionary<string, string>? Metadata
);

// Validation in controller
[HttpPost]
public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
{
    // Model validation happens automatically
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    // Additional business validation
    if (request.Amount <= 0)
    {
        return BadRequest("Amount must be greater than 0");
    }

    // Sanitize input
    var sanitizedDescription = SanitizeHtml(request.Description);
    var sanitizedEmail = request.CustomerEmail?.Trim().ToLowerInvariant();

    // Process order...
}
```

### **SQL Injection Prevention**
```csharp
// Entity Framework automatically parameterizes queries
public async Task<Order> GetOrderAsync(int orderId, int tenantId)
{
    // Safe - parameterized query
    return await _context.Orders
        .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);
}

// Never do this - vulnerable to SQL injection
// var query = $"SELECT * FROM orders WHERE id = {orderId} AND tenant_id = {tenantId}";
// return await _context.Orders.FromSqlRaw(query).FirstOrDefaultAsync();
```

### **XSS Prevention**
```csharp
public static class InputSanitizer
{
    public static string SanitizeHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove potentially dangerous HTML
        var sanitized = System.Web.HttpUtility.HtmlEncode(input);
        
        // Additional sanitization if needed
        sanitized = sanitized.Replace("<script>", "").Replace("</script>", "");
        
        return sanitized;
    }

    public static Dictionary<string, string> SanitizeMetadata(Dictionary<string, string> metadata)
    {
        return metadata?.ToDictionary(
            kvp => SanitizeHtml(kvp.Key),
            kvp => SanitizeHtml(kvp.Value)
        ) ?? new Dictionary<string, string>();
    }
}
```

## 🚦 **Rate Limiting & DDoS Protection**

### **Per-Tenant Rate Limiting**
```csharp
// In Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("PerTenantLimiter", context =>
    {
        var tenantId = context.User.FindFirst("TenantId")?.Value ?? "anonymous";
        
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: tenantId,
            factory: partition => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,           // 100 requests per minute per tenant
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 10,             // Allow 10 requests in queue
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});

// Apply to controllers
[ApiController]
[Authorize]
[EnableRateLimiting("PerTenantLimiter")]
public class OrdersController : BaseController
{
    // Controller methods...
}
```

### **API-Specific Rate Limiting**
```csharp
// Different limits for different operations
builder.Services.AddRateLimiter(options =>
{
    // Order creation - more restrictive
    options.AddPolicy("OrderCreationLimiter", context =>
    {
        var tenantId = context.User.FindFirst("TenantId")?.Value ?? "anonymous";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: $"order-creation-{tenantId}",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,            // 10 orders per minute per tenant
                Window = TimeSpan.FromMinutes(1)
            });
    });

    // Order reading - more permissive
    options.AddPolicy("OrderReadLimiter", context =>
    {
        var tenantId = context.User.FindFirst("TenantId")?.Value ?? "anonymous";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: $"order-read-{tenantId}",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 1000,          // 1000 reads per minute per tenant
                Window = TimeSpan.FromMinutes(1)
            });
    });
});
```

## 🔐 **Transport Layer Security**

### **HTTPS Configuration**
```csharp
// In Program.cs
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

app.UseHttpsRedirection();
app.UseHsts();

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'; script-src 'self'");
    await next();
});
```

### **Database Connection Security**
```csharp
// Secure database connection string
var connectionString = $"Host={host};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=false;";

options.UseNpgsql(connectionString, npgsqlOptions =>
{
    npgsqlOptions.EnableRetryOnFailure(
        maxRetryCount: 3,
        maxRetryDelay: TimeSpan.FromSeconds(30),
        errorCodesToAdd: null);
});
```

### **RabbitMQ SSL Configuration**
```csharp
// CloudAMQP SSL configuration
var factory = new ConnectionFactory()
{
    HostName = _configuration["RabbitMQ:HostName"],
    UserName = _configuration["RabbitMQ:UserName"],
    Password = _configuration["RabbitMQ:Password"],
    VirtualHost = _configuration["RabbitMQ:VirtualHost"],
    Port = int.Parse(_configuration["RabbitMQ:Port"]),
    Ssl = new SslOption
    {
        Enabled = true,
        AcceptablePolicyErrors = SslPolicyErrors.None,
        Version = SslProtocols.Tls12 | SslProtocols.Tls13
    }
};
```

## 📊 **Security Monitoring & Logging**

### **Security Event Logging**
```csharp
public class SecurityLogger
{
    private readonly ILogger<SecurityLogger> _logger;

    public void LogAuthenticationAttempt(string email, bool success, string ipAddress)
    {
        _logger.LogInformation("Authentication attempt: Email={Email}, Success={Success}, IP={IP}", 
            email, success, ipAddress);
    }

    public void LogAuthorizationFailure(string userId, string resource, string action)
    {
        _logger.LogWarning("Authorization failure: UserId={UserId}, Resource={Resource}, Action={Action}", 
            userId, resource, action);
    }

    public void LogTenantAccessViolation(int tenantId, int attemptedTenantId, string userId)
    {
        _logger.LogError("Tenant access violation: UserId={UserId}, TenantId={TenantId}, AttemptedTenantId={AttemptedTenantId}", 
            userId, tenantId, attemptedTenantId);
    }

    public void LogSuspiciousActivity(string description, string userId, string ipAddress)
    {
        _logger.LogWarning("Suspicious activity: Description={Description}, UserId={UserId}, IP={IP}", 
            description, userId, ipAddress);
    }
}
```

### **Security Metrics**
```csharp
public class SecurityMetrics
{
    private readonly Counter<int> _authenticationAttempts;
    private readonly Counter<int> _authorizationFailures;
    private readonly Counter<int> _rateLimitHits;
    private readonly Histogram<double> _requestDuration;

    public SecurityMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("OrderService.Security");
        
        _authenticationAttempts = meter.CreateCounter<int>("authentication_attempts");
        _authorizationFailures = meter.CreateCounter<int>("authorization_failures");
        _rateLimitHits = meter.CreateCounter<int>("rate_limit_hits");
        _requestDuration = meter.CreateHistogram<double>("request_duration_seconds");
    }

    public void RecordAuthenticationAttempt(bool success)
    {
        _authenticationAttempts.Add(1, new KeyValuePair<string, object>("success", success));
    }

    public void RecordAuthorizationFailure()
    {
        _authorizationFailures.Add(1);
    }

    public void RecordRateLimitHit(string tenantId)
    {
        _rateLimitHits.Add(1, new KeyValuePair<string, object>("tenant_id", tenantId));
    }
}
```

## 🔍 **Security Best Practices Checklist**

### ✅ **Implemented Security Measures**
- [x] **JWT Authentication** with tenant context
- [x] **Multi-tenant isolation** at all levels
- [x] **HTTPS/TLS encryption** for all communications
- [x] **Input validation** and sanitization
- [x] **Rate limiting** per tenant
- [x] **SQL injection prevention** via EF Core
- [x] **XSS protection** with output encoding
- [x] **Security headers** (HSTS, CSP, XSS protection)
- [x] **Comprehensive logging** of security events
- [x] **Secure database connections** with SSL

### 🔄 **Regular Security Tasks**
- [ ] **Rotate JWT secrets** every 90 days
- [ ] **Monitor security logs** for suspicious activity
- [ ] **Update dependencies** regularly
- [ ] **Security audits** quarterly
- [ ] **Penetration testing** annually
- [ ] **Review access logs** monthly
- [ ] **Backup security configuration** regularly

### 🚨 **Security Incident Response**
```csharp
public class SecurityIncidentResponse
{
    public async Task HandleSecurityIncident(string incidentType, string details)
    {
        // Log incident
        _logger.LogError("Security incident: Type={Type}, Details={Details}", incidentType, details);
        
        // Notify security team
        await _notificationService.NotifySecurityTeam(incidentType, details);
        
        // Block suspicious IP if necessary
        if (incidentType == "BruteForceAttack")
        {
            await _firewallService.BlockIpAddress(details);
        }
        
        // Revoke compromised tokens
        if (incidentType == "TokenCompromise")
        {
            await _tokenService.RevokeTokensByUserId(details);
        }
    }
}
```

## 🔑 **Secret Management**

### **Environment Variables**
```bash
# Never commit secrets to git
# Use platform-specific secret management

# JWT Settings (High Security)
JwtSettings__SecretKey=your-super-secret-jwt-key-256-bits-minimum
JwtSettings__Issuer=saasify-orderservice
JwtSettings__Audience=saasify-client

# Database Connection (High Security)
DATABASE_URL=postgresql://user:pass@host:5432/order_service?sslmode=require

# RabbitMQ (High Security)
RabbitMQ__UserName=your-cloudamqp-username
RabbitMQ__Password=your-cloudamqp-password
RabbitMQ__VirtualHost=your-cloudamqp-vhost
```

### **Secret Rotation Strategy**
```csharp
public class SecretRotationService
{
    public async Task RotateJwtSecret()
    {
        // Generate new secret
        var newSecret = GenerateSecureSecret();
        
        // Update configuration
        _configuration["JwtSettings:SecretKey"] = newSecret;
        
        // Revoke existing tokens
        await _tokenService.RevokeAllTokens();
        
        // Notify users to re-authenticate
        await _notificationService.NotifyTokenRotation();
    }

    private string GenerateSecureSecret()
    {
        var key = new byte[32]; // 256 bits
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return Convert.ToBase64String(key);
    }
}
```

---

## 📋 **Security Compliance**

### **Data Protection**
- **GDPR Compliance**: Personal data encryption and access controls
- **Data Minimization**: Only collect necessary order data
- **Right to Deletion**: Order data deletion on request
- **Audit Logging**: All data access is logged

### **Access Control**
- **Principle of Least Privilege**: Users only access necessary data
- **Role-Based Access**: Different permissions for different roles
- **Tenant Isolation**: Complete data separation between tenants
- **Audit Trails**: All actions are traceable

---

## 🎯 **Security Summary**

Your OrderService implements **enterprise-grade security** with:

✅ **Zero Trust Architecture**: Every request is authenticated and authorized  
✅ **Defense in Depth**: Multiple layers of security controls  
✅ **Tenant Isolation**: Complete data separation between tenants  
✅ **Real-time Monitoring**: Comprehensive logging and metrics  
✅ **Automated Protection**: Rate limiting and input validation  
✅ **Secure Communications**: TLS encryption everywhere  
✅ **Compliance Ready**: GDPR and data protection compliance  

The microservice is **production-ready** with security best practices built into every layer! 🛡️
