using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace EI.API.Cloud.Clients.Azure.Secrets;

public class KeyVaultSecretsClient(IConfiguration configuration, IAzureCredentialProvider credentialProvider) : ISecretsClient
{
    public const string ConnectionString_KeyVault = "AzureKeyVault";

    public async Task<string?> GetSecretAsync(string secretName)
    {
        var secretClient = GetSecretClient(secretName);
        var result = await secretClient.GetSecretAsync(secretName);
        var secret = result?.Value;
        return secret?.Value;
    }

    public async Task<bool> SetSecretAsync(string secretName, string secretValue)
    {
        var secretClient = GetSecretClient(secretName);
        await secretClient.SetSecretAsync(secretName, secretValue);
        return true;
    }

    private SecretClient GetSecretClient(string secretName)
    {
        var keyVaultUrl = configuration.GetConnectionString(ConnectionString_KeyVault);
        if (string.IsNullOrWhiteSpace(keyVaultUrl)) throw new Exception("KeyVault URL Connection String not available");

        var credential = credentialProvider.GetCredential();
        return new SecretClient(new Uri(keyVaultUrl), credential);
    }
}