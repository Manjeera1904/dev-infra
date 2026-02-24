using Azure.Messaging.ServiceBus;
using EI.API.Cloud.Clients.Azure.Messaging.Versioning;
using System.Text.Json;

namespace EI.API.Cloud.Clients.Azure.Messaging;

public class ServiceBusTopicMessageSender : IMessageSender
{
    private readonly ServiceBusSender _sender;

    public ServiceBusTopicMessageSender(ServiceBusClient client, string topicName)
    {
        _sender = client.CreateSender(topicName);
    }

    public async Task SendMessageAsync<T>(MessageHeader header, T message, CancellationToken? cancellationToken = null) where T : class
    {
        var body = JsonSerializer.Serialize(message);
        var serviceBusMessage = new ServiceBusMessage(body)
        {
            // Note: need to keep this in sync with ServiceBusSubscriptionMessageReceiver::StartListeningAsync
            MessageId = string.IsNullOrWhiteSpace(header.MessageId) ? Guid.NewGuid().ToString() : header.MessageId,
            CorrelationId = header.CorrelationId,
            Subject = header.MessageType,
        };

        var writer = MessageHeaderVersionFactory
          .ResolveWriter(header.MessageVersion);

        writer.Write(serviceBusMessage, header);



        if (header.Properties != null)
        {
            foreach (var kvp in header.Properties)
            {
                serviceBusMessage.ApplicationProperties[kvp.Key] = kvp.Value;
            }
        }

        await _sender.SendMessageAsync(serviceBusMessage, cancellationToken ?? CancellationToken.None);
    }
}