using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Order.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly string[] _topics;

    public KafkaConsumerService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<KafkaConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

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
        // This is where you'd handle different event types
        switch (topic)
        {
            case "product-updated":
                await HandleProductUpdatedAsync(message, cancellationToken);
                break;
            
            // Add handlers for other topics as needed
            
            default:
                _logger.LogWarning("No handler for topic {Topic}", topic);
                break;
        }
    }

    private async Task HandleProductUpdatedAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            // Deserialize message and update product information in order items
            // This is just an example - you would implement the actual logic based on your needs
            _logger.LogInformation("Processing product update: {Message}", message);
            
            // In a real implementation, you would:
            // 1. Deserialize the message to get product ID, new price, etc.
            // 2. Use a scoped service to update any pending orders with the updated product info
            
            await Task.CompletedTask; // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling product update");
        }
    }
}
