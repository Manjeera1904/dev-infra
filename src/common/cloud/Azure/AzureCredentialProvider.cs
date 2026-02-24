using Azure.Core;
using Azure.Identity;

namespace EI.API.Cloud.Clients.Azure;

public interface IAzureCredentialProvider
{
    TokenCredential GetCredential();
}
public class AzureCredentialProvider : IAzureCredentialProvider
{
    protected readonly Lazy<TokenCredential> _credential;

    public AzureCredentialProvider()
    {
        _credential = new Lazy<TokenCredential>(CreateCredential);
    }

    private TokenCredential CreateCredential()
    {
        var defaultAzureCredentialOptions = new DefaultAzureCredentialOptions();

        // See: https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/local-development-service-principal
        //
        // When running locally in development, these environment variables are used to authenticate with Azure:
        //   - AZURE_TENANT_ID
        //   - AZURE_CLIENT_ID
        //   - AZURE_CLIENT_SECRET
        //
        // The DefaultAzureCredential follows a sequence when determining how to authenticate, with the environment
        // variables being the first attempt. In a deployed environment, the DefaultAzureCredential will use the
        // Managed Identity of the Azure App Service to authenticate with other Azure resources.

        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            defaultAzureCredentialOptions.ManagedIdentityClientId = clientId;

            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
            if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
            {
                // For some reason (yet to be determined and documented), we have to explicitly set the TenantId in the
                // AdditionallyAllowedTenants in the DefaultAzureCredentialOptions, otherwise we get an error stating that
                // "The current credential is not configured to acquire tokens for tenant" -- even though the tenantId is
                // already the one that owns the Service Principal, and the AZURE_TENANT_ID variable matches it....
                defaultAzureCredentialOptions.AdditionallyAllowedTenants.Add(tenantId);
            }
        }

        return new DefaultAzureCredential(defaultAzureCredentialOptions);
    }

    public TokenCredential GetCredential() => _credential.Value;
}
