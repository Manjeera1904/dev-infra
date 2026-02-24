namespace EI.API.Cloud.Clients;

public interface ISecretsClient
{
    Task<string?> GetSecretAsync(string secretIdentifier);
    Task<bool> SetSecretAsync(string secretIdentifier, string secretValue);
}