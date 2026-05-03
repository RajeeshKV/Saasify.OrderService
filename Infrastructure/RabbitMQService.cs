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
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _exchangeName = "order.exchange";

        public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var factory = new ConnectionFactory()
            {
                HostName = _configuration["RabbitMQ:HostName"],
                UserName = _configuration["RabbitMQ:UserName"],
                Password = _configuration["RabbitMQ:Password"],
                VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
                Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            // Configure SSL if needed
            if (bool.Parse(_configuration["RabbitMQ:SslEnabled"] ?? "false"))
            {
                factory.Ssl = new SslOption
                {
                    Enabled = true,
                    AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch |
                                           SslPolicyErrors.RemoteCertificateChainErrors
                };
            }

            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare exchange
                _channel.ExchangeDeclare(
                    exchange: _exchangeName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                _logger.LogInformation("RabbitMQ connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        public async Task PublishMessageAsync<T>(T message, string queueName)
        {
            try
            {
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

                await Task.Yield(); // Ensure async implementation

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Message published to queue {QueueName}: {MessageId}", queueName, properties.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to queue {QueueName}", queueName);
                throw;
            }
        }

        public async Task SubscribeAsync<T>(string queueName, Func<T, Task> onMessageReceived)
        {
            try
            {
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
