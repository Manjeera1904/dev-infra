using Autofac;
using EI.API.Cloud.Clients.Azure.Messaging;
using EI.API.Cloud.Clients.Azure.Secrets;

namespace EI.API.Cloud.Clients.Azure;

public class AzureModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<AzureCredentialProvider>().As<IAzureCredentialProvider>().SingleInstance();
        builder.RegisterType<KeyVaultSecretsClient>().As<ISecretsClient>();

        builder.RegisterType<ServiceBusMessageClientFactory>().As<IMessageClientFactory>();
    }
}