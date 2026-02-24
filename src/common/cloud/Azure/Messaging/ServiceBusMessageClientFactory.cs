using Autofac;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace EI.API.Cloud.Clients.Azure.Messaging;

public class ServiceBusMessageClientFactory : IMessageClientFactory
{
    protected readonly ILifetimeScope _lifetimeScope;

    protected readonly ServiceBusClient _client;

    public ServiceBusMessageClientFactory(ILifetimeScope lifetimeScope, IConfiguration configuration, IAzureCredentialProvider credentialProvider)
    {
        _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));

        var connectionString = configuration.GetConnectionString("AzureServiceBus");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("AzureServiceBus connection string not provided");
        }

        // If the connection string uses a SAS Key, use that, otherwise get a credential for the
        // current Managed Identity or Service Principal.
        if (connectionString.Contains("SharedAccessKeyName", StringComparison.OrdinalIgnoreCase))
        {
            _client = new ServiceBusClient(connectionString);
        }
        else
        {
            var credential = credentialProvider.GetCredential();
            _client = new ServiceBusClient(connectionString, credential);
        }
    }

    public Task<IMessageSender> CreateMessageSenderAsync(string topicName, CancellationToken? cancellationToken = null)
        => Task.FromResult<IMessageSender>(new ServiceBusTopicMessageSender(_client, topicName));

    public Task<IMessageReceiver> CreateMessageReceiverAsync(string topicName, string subscriptionName, CancellationToken? cancellationToken = null)
        => Task.FromResult<IMessageReceiver>(new ServiceBusSubscriptionMessageReceiver(_lifetimeScope, _client, topicName, subscriptionName));
}