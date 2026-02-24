namespace EI.API.Cloud.Clients.Azure.Messaging.Exceptions;
internal sealed class UnsupportedMessageVersionException : Exception
{
    public string? Version { get; }
    public UnsupportedMessageVersionException(string? version)
        : base($"Unsupported message version '{version}'")
    {
        Version = version;
    }
}