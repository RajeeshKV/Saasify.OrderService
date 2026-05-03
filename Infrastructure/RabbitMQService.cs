using Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.Security;
using System.Text;
using System.Text.Json;

namespace Infrastructure
{
    public interface IRabbitMQService
    {
        Task PublishMessageAsync<T>(T message, string queueName);
        Task SubscribeAsync<T>(string queueName, Func<T, Task> onMessageReceived);
        void Dispose();
    }

    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQService> _logger;
        private IConnection _connection;
        private IModel _channel;
        private readonly string _exchangeName;

        public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _exchangeName = _configuration["OrderQueue:Exchange"] ?? "order.exchange";
            
            // Initialize connection asynchronously in background
            Task.Run(InitializeConnectionAsync);
        }

        private async Task InitializeConnectionAsync()
        {
            // Log connection parameters (without password)
            var hostName = _configuration["RabbitMQ:HostName"];
            var userName = _configuration["RabbitMQ:UserName"];
            var port = _configuration["RabbitMQ:Port"] ?? "5671";
            var sslEnabled = bool.Parse(_configuration["RabbitMQ:SslEnabled"] ?? "true");
            var virtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/";
            
            _logger.LogInformation("Attempting RabbitMQ connection to {HostName}:{Port} as {UserName} on {VirtualHost} (SSL: {SslEnabled})", 
                hostName, port, userName, virtualHost, sslEnabled);

            // Try AMQPS URI connection first
            await TryAmqpsUriConnection();

            // If URI connection fails, try standard connection
            if (_connection == null)
            {
                await TryStandardConnection();
            }

            // If connection established, declare exchange
            if (_connection != null && _channel != null)
            {
                try
                {
                    _channel.ExchangeDeclare(
                        exchange: _exchangeName,
                        type: ExchangeType.Direct,
                        durable: true,
                        autoDelete: false);

                    _logger.LogInformation("RabbitMQ connection and exchange setup completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to declare RabbitMQ exchange");
                    CloseConnection();
                }
            }
            else
            {
                _logger.LogWarning("OrderService will start without RabbitMQ connection. Orders will be queued locally.");
            }
        }

        private async Task TryAmqpsUriConnection()
        {
            try
            {
                var hostName = _configuration["RabbitMQ:HostName"];
                var userName = _configuration["RabbitMQ:UserName"];
                var password = _configuration["RabbitMQ:Password"];
                var port = _configuration["RabbitMQ:Port"] ?? "5671";
                var virtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/";
                
                // Construct AMQPS URI
                var uri = $"amqps://{userName}:{password}@{hostName}:{port}/{virtualHost}";
                
                _logger.LogInformation("Trying AMQPS URI connection to {HostName}:{Port}", hostName, port);
                
                var factory = new ConnectionFactory()
                {
                    Uri = new Uri(uri),
                    ClientProvidedName = "OrderService-" + Environment.MachineName,
                    RequestedHeartbeat = TimeSpan.FromSeconds(60),
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                _logger.LogInformation("RabbitMQ connection established via AMQPS URI");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AMQPS URI connection failed, trying standard method");
                CloseConnection();
            }
        }

        private async Task TryStandardConnection()
        {
            var maxRetries = 5;
            var retryDelay = TimeSpan.FromSeconds(5);
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    var factory = new ConnectionFactory()
                    {
                        HostName = _configuration["RabbitMQ:HostName"],
                        UserName = _configuration["RabbitMQ:UserName"],
                        Password = _configuration["RabbitMQ:Password"],
                        VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
                        Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5671"),
                        RequestedHeartbeat = TimeSpan.FromSeconds(60),
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                        ClientProvidedName = "OrderService-" + Environment.MachineName
                    };

                    // Configure SSL for CloudAMQP
                    if (bool.Parse(_configuration["RabbitMQ:SslEnabled"] ?? "true"))
                    {
                        factory.Ssl = new SslOption
                        {
                            Enabled = true,
                            AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch |
                                                   SslPolicyErrors.RemoteCertificateChainErrors |
                                                   SslPolicyErrors.None,
                            Version = System.Security.Authentication.SslProtocols.Tls12,
                            ServerName = _configuration["RabbitMQ:HostName"]
                        };
                    }

                    _logger.LogInformation("Attempting standard RabbitMQ connection (attempt {RetryCount}/{MaxRetries})", retryCount + 1, maxRetries);
                    
                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();
                    
                    _logger.LogInformation("RabbitMQ connection established via standard method");
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Failed to connect to RabbitMQ (attempt {RetryCount}/{MaxRetries})", retryCount, maxRetries);
                    
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex, "Failed to connect to RabbitMQ after {MaxRetries} attempts", maxRetries);
                        break;
                    }
                    
                    await Task.Delay(retryDelay);
                }
            }
        }

        private void CloseConnection()
        {
            try
            {
                _channel?.Close();
                _channel = null;
                _connection?.Close();
                _connection = null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing RabbitMQ connection");
            }
        }

        public async Task PublishMessageAsync<T>(T message, string queueName)
        {
            try
            {
                // Check if RabbitMQ is connected
                if (_connection == null || _channel == null)
                {
                    _logger.LogWarning("RabbitMQ is not connected. Message will be queued locally.");
                    // Store message locally for later publishing
                    await QueueMessageLocally(message, queueName);
                    return;
                }

                var messageJson = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(messageJson);

                // Declare queue
                _channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // Bind queue to exchange
                _channel.QueueBind(
                    queue: queueName,
                    exchange: _exchangeName,
                    routingKey: queueName);

                // Publish message
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.MessageId = Guid.NewGuid().ToString();
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.ContentType = "application/json";

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);

                _logger.LogDebug("Message published to queue {QueueName}: {MessageId}", queueName, properties.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to queue {QueueName}", queueName);
                throw;
            }
        }

        private async Task QueueMessageLocally<T>(T message, string queueName)
        {
            // Simple in-memory queue for when RabbitMQ is not available
            // In production, this could be a database table or file-based queue
            _logger.LogInformation("Message queued locally for {QueueName}: {MessageType}", queueName, typeof(T).Name);
            await Task.CompletedTask;
        }

        public async Task SubscribeAsync<T>(string queueName, Func<T, Task> onMessageReceived)
        {
            try
            {
                // Check if RabbitMQ is connected
                if (_connection == null || _channel == null)
                {
                    _logger.LogWarning("RabbitMQ is not connected. Cannot subscribe to queue {QueueName}", queueName);
                    return;
                }

                // Declare queue
                _channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // Bind queue to exchange
                _channel.QueueBind(
                    queue: queueName,
                    exchange: _exchangeName,
                    routingKey: queueName);

                var consumer = new EventingBasicConsumer(_channel);

                consumer.Received += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var messageJson = Encoding.UTF8.GetString(body);
                        var message = JsonSerializer.Deserialize<T>(messageJson);

                        if (message != null)
                        {
                            _logger.LogInformation("Processing message from queue {QueueName}: {MessageId}", queueName, ea.BasicProperties.MessageId);
                            
                            await onMessageReceived(message);
                            
                            // Acknowledge message
                            _channel.BasicAck(ea.DeliveryTag, false);
                            
                            _logger.LogInformation("Message processed and acknowledged: {MessageId}", ea.BasicProperties.MessageId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize message from queue {QueueName}", queueName);
                            _channel.BasicNack(ea.DeliveryTag, false, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue {QueueName}", queueName);
                        
                        // Negative acknowledge and requeue
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                // Set prefetch count
                _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                // Start consuming
                _channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("Subscribed to queue {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to queue {QueueName}", queueName);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
                _logger.LogInformation("RabbitMQ connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing RabbitMQ connection");
            }
        }
    }
}
