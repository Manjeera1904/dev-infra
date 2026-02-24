using EI.API.Cloud.Clients.Azure.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EI.API.Cloud.Clients.Azure;

public static class ApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddKeyVaultConfiguration(this IHostApplicationBuilder builder)
    {
        var credBuilder = new AzureCredentialProvider();

        var uri = builder.Configuration.GetConnectionString(KeyVaultSecretsClient.ConnectionString_KeyVault);
        if (string.IsNullOrWhiteSpace(uri))
            throw new Exception($"Connection string not found: {KeyVaultSecretsClient.ConnectionString_KeyVault}");

        builder.Configuration.AddAzureKeyVault(new Uri(uri), credBuilder.GetCredential());

        return builder;
    }
}
