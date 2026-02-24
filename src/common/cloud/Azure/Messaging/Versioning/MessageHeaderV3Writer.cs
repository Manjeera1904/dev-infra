using Azure.Messaging.ServiceBus;

namespace EI.API.Cloud.Clients.Azure.Messaging.Versioning;

internal sealed class MessageHeaderV3Writer : IMessageHeaderWriter
{
    public void Write(ServiceBusMessage message, MessageHeader header)
    {
        message.ApplicationProperties[nameof(MessageHeader.MessageSource)] = header.MessageSource;
        message.ApplicationProperties[nameof(MessageHeader.MessageStatus)] = header.MessageStatus;
        message.ApplicationProperties[nameof(MessageHeader.ClientIdentifier)] = header.ClientIdentifier;
        message.ApplicationProperties[nameof(MessageHeader.SendingApplication)] = header.SendingApplication;
        message.ApplicationProperties[nameof(MessageHeader.Requestor)] = header.Requestor;
        message.ApplicationProperties[nameof(MessageHeader.ActionType)] = header.ActionType;
        message.ApplicationProperties[nameof(MessageHeader.MessageVersion)] = "3.0";
    }
}
