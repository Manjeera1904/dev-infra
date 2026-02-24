using Azure.Messaging.ServiceBus;

namespace EI.API.Cloud.Clients.Azure.Messaging.Versioning;
public interface IMessageHeaderWriter
{
    void Write(ServiceBusMessage message, MessageHeader header);
}
