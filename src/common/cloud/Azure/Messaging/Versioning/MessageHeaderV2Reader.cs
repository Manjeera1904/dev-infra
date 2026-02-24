using Azure.Messaging.ServiceBus;

namespace EI.API.Cloud.Clients.Azure.Messaging.Versioning;

internal sealed class MessageHeaderV2Reader : IMessageHeaderReader
{
    public MessageHeader Read(ServiceBusReceivedMessage message)
    {
        var props = message.ApplicationProperties;

        return new MessageHeader
        {

            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            MessageVersion = GetProperty(message.ApplicationProperties, nameof(MessageHeader.MessageVersion)),
            MessageType = message.Subject ?? GetProperty(message.ApplicationProperties, nameof(MessageHeader.MessageType)),
            MessageSource = GetProperty(message.ApplicationProperties, nameof(MessageHeader.MessageSource)),
            MessageStatus = GetProperty(message.ApplicationProperties, nameof(MessageHeader.MessageStatus)),
            ClientIdentifier = GetProperty(message.ApplicationProperties, nameof(MessageHeader.ClientIdentifier)),
            SendingApplication = GetProperty(message.ApplicationProperties, nameof(MessageHeader.SendingApplication)),
            SendingApplicationId = GetProperty(message.ApplicationProperties, nameof(MessageHeader.SendingApplicationId)),
            ActionType = GetProperty(message.ApplicationProperties, nameof(MessageHeader.ActionType)),
            Requestor = GetProperty(message.ApplicationProperties, nameof(MessageHeader.Requestor)),
            Properties = message.ApplicationProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value as string ?? ""),
        };
    }

    protected string GetProperty(IReadOnlyDictionary<string, object> props, string propertyName)
         => props.TryGetValue(propertyName, out var ver) ? ver as string ?? string.Empty : string.Empty;
}