# OrderService Event-Driven Architecture

## 🔄 **Event-Driven Order Processing Flow**

### **Complete Architecture Diagram**
```
┌─────────────┐    ┌──────────────┐    ┌─────────────┐    ┌─────────────┐
│ SaaSify API │───▶│  RabbitMQ    │───▶│ OrderService │───▶│ Order DB    │
│ (Publisher)  │    │(CloudAMQP)   │    │(Consumer)   │    │(PostgreSQL) │
└─────────────┘    └──────────────┘    └─────────────┘    └─────────────┘
```

## 🎯 **Event-Driven Processing Explained**

### **Step 1: Order Creation (HTTP → Event)**
```
Frontend → SaaSify API → OrderService Database → RabbitMQ Event
```

1. **Frontend** creates order via HTTP API
2. **SaaSify API** saves order to database
3. **SaaSify API** publishes `OrderCreated` event to CloudAMQP
4. **OrderService** consumes `OrderCreated` event
5. **OrderService** processes order asynchronously

### **Step 2: Order Processing (Async)**
```
OrderService Background Consumer → Payment Processing → Status Update → RabbitMQ Event
```

1. **OrderService** processes payment (simulated or real)
2. **OrderService** updates order status in database
3. **OrderService** publishes `OrderStatusUpdated` event
4. **SaaSify API** consumes `OrderStatusUpdated` event (optional)
5. **Frontend** gets real-time updates (optional)

## 🐰 **CloudAMQP Message Flow**

### **Message Types**

#### **1. OrderCreated Event**
```json
{
  "tenantId": 1,
  "userId": 123,
  "amount": 99.99,
  "currency": "USD",
  "description": "Premium Subscription",
  "customerEmail": "user@example.com",
  "metadata": {},
  "messageType": "OrderCreated",
  "timestamp": "2026-05-03T10:00:00Z",
  "correlationId": "abc-123-def-456"
}
```

#### **2. OrderStatusUpdated Event**
```json
{
  "orderId": 456,
  "tenantId": 1,
  "status": "Completed",
  "messageType": "OrderStatusUpdated",
  "timestamp": "2026-05-03T10:02:00Z",
  "correlationId": "abc-123-def-456",
  "originalMessage": {
    "tenantId": 1,
    "userId": 123,
    "amount": 99.99
  }
}
```

## 🔧 **OrderService Event Processing**

### **Message Consumer Service**
```csharp
// MessageProcessorService.cs - Background service
public class MessageProcessorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to order queue
        await _rabbitMQService.SubscribeAsync<OrderMessage>(
            "order.queue", 
            ProcessOrderMessageAsync);
    }

    private async Task ProcessOrderMessageAsync(OrderMessage message)
    {
        _logger.LogInformation("Processing order: {CorrelationId}", message.CorrelationId);
        
        // Process order (payment, etc.)
        var result = await _orderService.ProcessOrderAsync(message);
        
        // Publish status update
        await PublishOrderStatusAsync(message, result);
    }
}
```

### **Queue Configuration**
```csharp
// RabbitMQService.cs - Queue setup
public async Task PublishMessageAsync<T>(T message, string queueName)
{
    // Declare durable queue
    _channel.QueueDeclare(
        queue: queueName,
        durable: true,
        exclusive: false,
        autoDelete: false);

    // Bind to exchange
    _channel.QueueBind(
        queue: queueName,
        exchange: "order.exchange",
        routingKey: queueName);

    // Publish message
    _channel.BasicPublish(
        exchange: "order.exchange",
        routingKey: queueName,
        basicProperties: properties,
        body: messageBytes);
}
```

## 📊 **How Main API Knows Order is Done**

### **Option 1: Polling Approach (Simple)**
```csharp
// Frontend polls OrderService directly
setInterval(async () => {
    const order = await fetch(`/api/orders/${orderId}`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    
    if (order.status === 'Completed') {
        // Order is done - update UI
        updateOrderUI(order);
    }
}, 2000); // Poll every 2 seconds
```

### **Option 2: Event-Driven Approach (Advanced)**
```csharp
// SaaSify API consumes OrderStatusUpdated events
public class OrderStatusConsumer : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _rabbitMQService.SubscribeAsync<OrderStatusMessage>(
            "order.status.queue", 
            ProcessOrderStatusUpdateAsync);
    }

    private async Task ProcessOrderStatusUpdateAsync(OrderStatusMessage message)
    {
        // Update local database with status
        await _orderRepository.UpdateOrderStatusAsync(
            message.OrderId, 
            message.Status);
        
        // Notify connected clients via WebSocket/SSE
        await _notificationService.NotifyOrderUpdate(message);
    }
}
```

### **Option 3: Database Synchronization (Hybrid)**
```csharp
// SaaSify API checks OrderService database
public async Task<OrderStatus> GetOrderStatusFromService(int orderId, int tenantId)
{
    // Query OrderService database via API
    var response = await _httpClient.GetAsync($"/api/orders/{orderId}", {
        headers: { 'Authorization': `Bearer ${serviceToken}` }
    });
    
    return await response.Content.ReadFromJsonAsync<OrderStatus>();
}
```

## 🔄 **Complete Event Flow Timeline**

```
Time 00:00: User creates order in frontend
Time 00:01: SaaSify API receives request
Time 00:02: Order saved to SaaSify database
Time 00:03: OrderCreated event published to CloudAMQP
Time 00:04: OrderService consumes OrderCreated event
Time 00:05: OrderService starts processing
Time 00:30: Payment processing completes
Time 00:31: OrderService updates database
Time 00:32: OrderStatusUpdated event published
Time 00:33: SaaSify API consumes status update (optional)
Time 00:34: Frontend receives real-time update (optional)
```

## 🛠️ **Implementation Examples**

### **Publisher (SaaSify API)**
```csharp
// OrderEventPublisher.cs
public async Task PublishOrderCreatedAsync(int tenantId, int userId, decimal amount)
{
    var orderMessage = new OrderMessage
    {
        TenantId = tenantId,
        UserId = userId,
        Amount = amount,
        MessageType = "OrderCreated",
        CorrelationId = Guid.NewGuid().ToString()
    };

    await _rabbitMQService.PublishMessageAsync(orderMessage, "order.queue");
}
```

### **Consumer (OrderService)**
```csharp
// MessageProcessorService.cs
public class MessageProcessorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _rabbitMQService.SubscribeAsync<OrderMessage>(
            "order.queue", 
            async (message) => {
                await ProcessOrderAsync(message);
                
                // Publish status update
                var statusMessage = new OrderStatusMessage
                {
                    OrderId = message.CorrelationId, // Use correlation as order ID
                    Status = "Completed",
                    OriginalMessage = message
                };
                
                await _rabbitMQService.PublishMessageAsync(
                    statusMessage, 
                    "order.status.queue");
            });
    }
}
```

## 📋 **Environment Variables for CloudAMQP**

### **Required Variables**
```bash
# CloudAMQP Connection (RabbitMQ compatible)
RabbitMQ__HostName=your-cloudamqp-host.rmq.cloudamqp.com
RabbitMQ__UserName=your-cloudamqp-username
RabbitMQ__Password=your-cloudamqp-password
RabbitMQ__VirtualHost=your-cloudamqp-vhost
RabbitMQ__Port=5672
RabbitMQ__SslEnabled=true
```

### **Queue Configuration**
```bash
# Exchange and Queue Names
ORDER_EXCHANGE=order.exchange
ORDER_QUEUE=order.queue
STATUS_QUEUE=order.status.queue

# Queue Settings
QUEUE_DURABLE=true
AUTO_DELETE=false
EXCLUSIVE=false
```

## 🎯 **Benefits of Event-Driven Architecture**

### **Scalability**
- **Decoupled Services**: OrderService can scale independently
- **Load Balancing**: Multiple consumers can process orders
- **Backpressure Handling**: Queue manages message flow
- **Retry Mechanism**: Failed messages are re-queued

### **Reliability**
- **Message Persistence**: Durable queues survive restarts
- **Acknowledgment**: Consumers confirm message processing
- **Error Isolation**: Failed orders don't affect others
- **Monitoring**: Queue depth and processing metrics

### **Performance**
- **Asynchronous Processing**: Non-blocking order handling
- **Parallel Consumers**: Multiple instances can process concurrently
- **Batch Processing**: Groups of orders can be processed together
- **Caching**: Frequently accessed data can be cached

## 🔍 **Monitoring & Debugging**

### **CloudAMQP Dashboard**
```bash
# Monitor queue depth
# Check message rates
# View consumer connections
# Monitor failed messages
```

### **Application Metrics**
```csharp
// Message processing metrics
public class OrderMetrics
{
    public int OrdersProcessed { get; set; }
    public int OrdersFailed { get; set; }
    public double AverageProcessingTime { get; set; }
    public int QueueDepth { get; set; }
}
```

### **Logging Strategy**
```csharp
// Structured logging for event flow
_logger.LogInformation("Order processing started: {CorrelationId}, {TenantId}, {Amount}", 
    message.CorrelationId, message.TenantId, message.Amount);

_logger.LogInformation("Order processing completed: {CorrelationId}, {Status}, {Duration}ms", 
    message.CorrelationId, status, stopwatch.ElapsedMilliseconds);
```

---

## 🎉 **Summary**

The OrderService uses **event-driven architecture** with CloudAMQP:

✅ **Asynchronous Processing** - Orders processed in background  
✅ **Event Publishing** - Status updates via CloudAMQP  
✅ **Queue Management** - Durable, reliable message delivery  
✅ **Scalable Design** - Multiple consumers possible  
✅ **Error Handling** - Retry and dead-letter mechanisms  
✅ **Monitoring Ready** - Comprehensive logging and metrics  

The main API can know orders are done through **event consumption**, **polling**, or **database synchronization** - choose the best approach for your needs! 🚀
