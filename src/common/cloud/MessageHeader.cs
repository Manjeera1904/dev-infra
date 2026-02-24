using EI.API.Cloud.Clients.Azure.Messaging.Versioning;

namespace EI.API.Cloud.Clients;

public class MessageHeader
{
    public string MessageId { get; set; } = default!;
    public string CorrelationId { get; set; } = default!;
    public string MessageSource { get; set; } = default!;
    public string MessageType { get; set; } = default!;
    public string MessageVersion { get; set; } = default!;
    public string MessageStatus { get; set; } = default!;
    public string ClientIdentifier { get; set; } = default!;
    public string SendingApplication { get; set; } = default!;

    [Obsolete("Deprecated since MessageVersion 2.0. Use SendingApplication instead.")]
    public string SendingApplicationId { get; set; } = default!;
    public string ActionType { get; set; } = default!;
    public string Requestor { get; set; } = default!;
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.InvariantCultureIgnoreCase);
}

public interface IMessageClientFactory
{
    Task<IMessageSender> CreateMessageSenderAsync(string topicName, CancellationToken? cancellationToken = null);
    Task<IMessageReceiver> CreateMessageReceiverAsync(string topicName, string subscriptionName, CancellationToken? cancellationToken = null);
}

public interface IMessageSender
{
    Task SendMessageAsync<T>(MessageHeader header, T message, CancellationToken? cancellationToken = null) where T : class;
}

public interface IMessageReceiver : IAsyncDisposable
{

    Task StartListeningAsync<T>(Func<MessageHeader, T, Task> messageHandler, CancellationToken? cancellationToken = null) where T : class;
    Task StartListeningAsync<T>(Func<MessageHeader, T, Task> messageHandler, IMessageBodyDispatcher<T> dispatcher, CancellationToken? cancellationToken = null) where T : class;
    Task StopListeningAsync(CancellationToken? cancellationToken = null);
}
