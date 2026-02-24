namespace EI.API.Cloud.Clients.Azure.Messaging.Versioning;
public interface IMessageBodyDispatcher<TMessage>
    where TMessage : class
{
    TMessage Deserialize(string body, string version);
    IReadOnlyCollection<string> SupportedVersions { get; }
}