using EI.API.Cloud.Clients.Azure.Messaging.Exceptions;

namespace EI.API.Cloud.Clients.Azure.Messaging.Versioning;

internal static class MessageHeaderVersionFactory
{
    public static IMessageHeaderReader ResolveReader(string? version)
        => version switch
        {
            "3.0" => new MessageHeaderV3Reader(),
            "2.0" => new MessageHeaderV2Reader(),
            "1.0" => new MessageHeaderV1Reader(),
            null => throw new MissingMessageVersionException(),
            _ => throw new UnsupportedMessageVersionException(version)
        };

    public static IMessageHeaderWriter ResolveWriter(string? version)
        => version switch
        {
            "3.0" => new MessageHeaderV3Writer(),
            "2.0" => new MessageHeaderV2Writer(),
            "1.0" => new MessageHeaderV1Writer(),
            null => throw new MissingMessageVersionException(),
            _ => throw new UnsupportedMessageVersionException(version)
        };
}
