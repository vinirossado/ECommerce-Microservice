using Confluent.Kafka;
using ProductCatalog.Models;
using System.Text.Json;

namespace ProductCatalog.Kafka;

public interface IKafkaProducer
{
    Task ProduceAsync(string topic, object message);
}

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private bool _disposed;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            ClientId = configuration["Kafka:ClientId"]
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task ProduceAsync(string topic, object message)
    {
        var key = Guid.NewGuid().ToString();
        var value = JsonSerializer.Serialize(message);

        try
        {
            var deliveryResult = await _producer.ProduceAsync(
                topic,
                new Message<string, string>
                {
                    Key = key,
                    Value = value
                });

            _logger.LogInformation(
                "Message delivered to {Topic} at partition {Partition} with offset {Offset}",
                deliveryResult.Topic, deliveryResult.Partition, deliveryResult.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex,
                "Failed to deliver message to {Topic}: {Error}",
                topic, ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _producer?.Dispose();
            }

            _disposed = true;
        }
    }
}
