# OrderService Queue Configuration for CloudAMQP

## 🎯 **Why Event-Driven (Option 1) is Superior**

### **Scalability Benefits**
- **Multiple Consumers**: Add more OrderService instances to handle high volume
- **Load Balancing**: CloudAMQP automatically distributes messages
- **Backpressure Handling**: Queue manages flow, prevents overload
- **Independent Scaling**: OrderService can scale without affecting SaaSify API

### **Reliability Benefits**
- **Message Persistence**: Durable queues survive service restarts
- **Retry Mechanism**: Failed messages are re-queued automatically
- **Acknowledgment**: Consumers confirm successful processing
- **Dead Letter Queue**: Failed messages can be analyzed later

### **Performance Benefits**
- **Asynchronous Processing**: Non-blocking order handling
- **Parallel Processing**: Multiple orders processed simultaneously
- **Batch Operations**: Groups of messages can be processed together
- **Resource Efficiency**: No polling overhead, event-driven

## 🐰 **CloudAMQP Queue Configuration**

### **Required Queues**

#### **1. Order Queue (Incoming)**
```bash
# Queue for receiving order creation events
ORDER_QUEUE=order.queue
```

#### **2. Order Status Queue (Outgoing)**
```bash
# Queue for publishing order status updates
ORDER_STATUS_QUEUE=order.status.queue
```

#### **3. Exchange Configuration**
```bash
# Exchange for routing messages
ORDER_EXCHANGE=order.exchange
EXCHANGE_TYPE=direct
EXCHANGE_DURABLE=true
EXCHANGE_AUTO_DELETE=false
```

### **Queue Setup in OrderService**

#### **Queue Declaration**
```csharp
// RabbitMQService.cs
public async Task PublishMessageAsync<T>(T message, string queueName)
{
    // Declare durable queue
    _channel.QueueDeclare(
        queue: queueName,
        durable: true,        // Survives restarts
        exclusive: false,      // Multiple consumers
        autoDelete: false,    // Persistent queue
        arguments: null);

    // Bind to exchange
    _channel.QueueBind(
        queue: queueName,
        exchange: _exchangeName,
        routingKey: queueName);

    // Publish message
    _channel.BasicPublish(
        exchange: _exchangeName,
        routingKey: queueName,
        basicProperties: properties,
        body: messageBytes);
}
```

#### **Consumer Configuration**
```csharp
// MessageProcessorService.cs
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Subscribe to order queue
    await _rabbitMQService.SubscribeAsync<OrderMessage>(
        "order.queue", 
        ProcessOrderMessageAsync);
}
```

### **Queue Properties**

#### **Durability**
```yaml
# Messages survive broker restarts
durable: true

# Exchange survives broker restarts
exchange_durable: true
```

#### **Routing**
```yaml
# Direct routing for precise message delivery
exchange_type: direct
routing_key: order.queue
```

#### **Quality of Service**
```yaml
# Message delivery guarantees
delivery_mode: 2  # Persistent
priority: 1        # High priority
```

## 🔧 **CloudAMQP Setup Steps**

### **Step 1: Create Exchange**
```bash
# In CloudAMQP Management Console
# 1. Go to Exchanges tab
# 2. Create new exchange:
#    Name: order.exchange
#    Type: direct
#    Durability: Durable
#    Auto-delete: No
```

### **Step 2: Create Queues**
```bash
# Create order queue
#    Name: order.queue
#    Durability: Durable
#    Auto-delete: No

# Create order status queue
#    Name: order.status.queue
#    Durability: Durable
#    Auto-delete: No
```

### **Step 3: Bind Queues to Exchange**
```bash
# Bind order queue
#    Exchange: order.exchange
#    Routing Key: order.queue

# Bind status queue
#    Exchange: order.exchange
#    Routing Key: order.status.queue
```

### **Step 4: Configure Permissions**
```bash
# Set user permissions in CloudAMQP
# 1. Configure: order.exchange
# 2. Configure: order.queue
# 3. Configure: order.status.queue
# 4. Set permissions: Configure, Read, Write
```

## 📊 **Queue Monitoring**

### **Key Metrics**
```bash
# Queue depth monitoring
rabbitmqctl list_queues name order.queue

# Message rates
rabbitmqctl list_queues name order.queue -f messages

# Consumer information
rabbitmqctl list_consumers
```

### **Health Check Integration**
```csharp
// Add queue health to health checks
public class QueueHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queueInfo = await _rabbitMQService.GetQueueInfoAsync("order.queue");
            
            if (queueInfo.MessageCount > 1000)
            {
                return HealthCheckResult.Degraded(
                    "High queue depth",
                    new Dictionary<string, object>
                    {
                        ["queue_depth"] = queueInfo.MessageCount,
                        ["queue_name"] = "order.queue"
                    });
            }

            return HealthCheckResult.Healthy(
                "Queue operating normally",
                new Dictionary<string, object>
                {
                    ["queue_depth"] = queueInfo.MessageCount,
                    ["queue_name"] = "order.queue",
                    ["consumers"] = queueInfo.ConsumerCount
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Queue health check failed",
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                });
        }
    }
}
```

## 🔄 **Message Flow Examples**

### **Order Creation Flow**
```json
// 1. SaaSify API publishes OrderCreated event
{
  "messageType": "OrderCreated",
  "tenantId": 1,
  "userId": 123,
  "amount": 99.99,
  "correlationId": "abc-123-def-456",
  "timestamp": "2026-05-03T10:00:00Z"
}

// 2. OrderService consumes from order.queue
// 3. OrderService processes order
// 4. OrderService publishes OrderStatusUpdated event
{
  "messageType": "OrderStatusUpdated",
  "orderId": 456,
  "tenantId": 1,
  "status": "Completed",
  "correlationId": "abc-123-def-456",
  "timestamp": "2026-05-03T10:02:00Z",
  "originalMessage": { /* original order data */ }
}

// 5. SaaSify API consumes from order.status.queue
```

### **Queue Configuration Summary**

| Queue Name | Purpose | Direction | Durability |
|-------------|---------|-----------|------------|
| `order.queue` | Receive OrderCreated events | Inbound | Durable |
| `order.status.queue` | Publish OrderStatusUpdated events | Outbound | Durable |

| Exchange | Type | Purpose |
|---------|------|---------|
| `order.exchange` | Direct | Route messages to specific queues |

## 🎯 **Best Practices**

### **Queue Naming**
```bash
# Use descriptive, consistent naming
{service}.{purpose}.queue
{service}.{purpose}.exchange

# Examples:
order.queue          # Incoming orders
order.status.queue   # Outgoing status updates
order.exchange        # Message routing
```

### **Configuration Management**
```bash
# Use environment variables for flexibility
RABBITMQ_ORDER_QUEUE=${ORDER_QUEUE:-order.queue}
RABBITMQ_STATUS_QUEUE=${STATUS_QUEUE:-order.status.queue}
RABBITMQ_EXCHANGE=${EXCHANGE:-order.exchange}
```

### **Error Handling**
```csharp
// Implement dead-letter queue for failed messages
_channel.QueueDeclare(
    queue: "order.queue.dlq",
    durable: true,
    arguments: new Dictionary<string, object>
    {
        ["x-dead-letter-exchange"] = "order.exchange",
        ["x-dead-letter-routing-key"] = "order.queue"
    });
```

## 📋 **Production Checklist**

### **Pre-Deployment**
- [ ] Exchange created in CloudAMQP
- [ ] Queues created and bound
- [ ] Permissions configured correctly
- [ ] Durability enabled for all queues
- [ ] Monitoring dashboards accessible

### **Post-Deployment**
- [ ] Queue depth monitoring active
- [ ] Consumer error rates tracked
- [ ] Message processing metrics collected
- [ ] Dead-letter queue monitoring
- [ ] Health checks passing

---

## 🎉 **Summary**

**Event-driven architecture (Option 1) is superior** because:

✅ **Scales Horizontally** - Add more consumers to handle load  
✅ **More Reliable** - Queue handles failures and retries  
✅ **Better Performance** - Asynchronous, non-blocking processing  
✅ **Decoupled Services** - OrderService scales independently  
✅ **Production Ready** - Proven pattern for high-volume systems  

**Queue Configuration**: 2 durable queues + 1 direct exchange  
**Message Flow**: SaaSify → CloudAMQP → OrderService → CloudAMQP → SaaSify  
**Monitoring**: Queue depth, consumer metrics, health checks  

This is the **enterprise-grade approach** for scalable order processing! 🚀
