namespace EI.API.Service.Data.Helpers;

public interface IPlatformExecutionContext
{
    Guid ClientId { get; }
    string? Username { get; }
}

public class PlatformExecutionContext : IPlatformExecutionContext
{
    public Guid ClientId { get; set; }
    public string? Username { get; set; }
}
