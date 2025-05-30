using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Notification.Services;
using System.Text.Json;

namespace Notification.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly IEmailService _emailService;
    private readonly string?[] _topics;

    public KafkaConsumerService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<KafkaConsumerService> logger,
        IEmailService emailService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _emailService = emailService;

        // Configure Kafka consumer
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = configuration["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        
        // Get topics from configuration
        _topics = configuration.GetSection("Kafka:Topics")
            .GetChildren()
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // Ensure we don't block startup

        try
        {
            _consumer.Subscribe(_topics);
            _logger.LogInformation("Kafka consumer subscribed to topics: {Topics}", string.Join(", ", _topics));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(stoppingToken);
                    
                    if (consumeResult == null)
                    {
                        continue;
                    }

                    _logger.LogInformation(
                        "Received message from topic {Topic}: {Message}",
                        consumeResult.Topic, consumeResult.Message.Value);

                    await ProcessMessageAsync(consumeResult.Topic, consumeResult.Message.Value, stoppingToken);
                    
                    // Commit the offset after processing
                    _consumer.Commit(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Consumer was stopped, no need to log an error
            _logger.LogInformation("Kafka consumer stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Kafka consumer");
        }
        finally
        {
            _consumer.Close();
            _consumer.Dispose();
        }
    }

    private async Task ProcessMessageAsync(string topic, string message, CancellationToken cancellationToken)
    {
        // Process messages based on topic
        switch (topic)
        {
            case "order-created":
                await HandleOrderCreatedAsync(message, cancellationToken);
                break;
            
            case "order-status-changed":
                await HandleOrderStatusChangedAsync(message, cancellationToken);
                break;
            
            default:
                _logger.LogWarning("No handler for topic {Topic}", topic);
                break;
        }
    }

    private async Task HandleOrderCreatedAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            // Deserialize the order created message
            var orderCreated = JsonSerializer.Deserialize<OrderCreatedMessage>(message);
            
            if (orderCreated != null)
            {
                _logger.LogInformation("Processing order creation notification for order {OrderId}", orderCreated.OrderId);
                await _emailService.SendOrderConfirmationAsync(orderCreated.UserId, orderCreated.OrderId, orderCreated.TotalAmount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling order created notification");
        }
    }

    private async Task HandleOrderStatusChangedAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            // Deserialize the order status changed message
            var statusChanged = JsonSerializer.Deserialize<OrderStatusChangedMessage>(message);
            
            if (statusChanged != null)
            {
                _logger.LogInformation("Processing order status change notification for order {OrderId}: {Status}", 
                    statusChanged.OrderId, statusChanged.NewStatus);
                
                await _emailService.SendOrderStatusUpdateAsync(statusChanged.UserId, statusChanged.OrderId, statusChanged.NewStatus);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling order status change notification");
        }
    }
}

// Message type classes
public class OrderCreatedMessage
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
}

public class OrderStatusChangedMessage
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
}
