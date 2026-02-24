namespace EI.API.Cloud.Clients.Azure.Messaging.Exceptions;
public sealed class MissingMessageVersionException : Exception
{
    public MissingMessageVersionException()
        : base("Message version header is missing.") { }
}