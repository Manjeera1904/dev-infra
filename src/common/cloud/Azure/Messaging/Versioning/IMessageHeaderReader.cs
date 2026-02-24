using Azure.Messaging.ServiceBus;

namespace EI.API.Cloud.Clients.Azure.Messaging.Versioning;
public interface IMessageHeaderReader
{
    MessageHeader Read(ServiceBusReceivedMessage message);
}