namespace EI.API.Cloud.Clients.Azure.Messaging.Exceptions;
public sealed class MessageDeserializationException : Exception
{
    public Type TargetType { get; }
    public string? MessageVersion { get; }

    public MessageDeserializationException(Type targetType, string? version, Exception? inner = null)
        : base($"Could not deserialize message body as {targetType.FullName} (version {version})", inner)
    {
        TargetType = targetType;
        MessageVersion = version;
    }
}