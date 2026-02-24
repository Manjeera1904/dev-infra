using Autofac;
using Azure.Messaging.ServiceBus;
using EI.API.Cloud.Clients.Azure.Messaging.Exceptions;
using EI.API.Cloud.Clients.Azure.Messaging.Versioning;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EI.API.Cloud.Clients.Azure.Messaging;

public class ServiceBusSubscriptionMessageReceiver : IMessageReceiver
{
    private readonly ILifetimeScope _lifetimeScope;
    private readonly ServiceBusProcessor _processor;

    private readonly ILogger<ServiceBusSubscriptionMessageReceiver> _logger;

    public ServiceBusSubscriptionMessageReceiver(ILifetimeScope lifetimeScope, ServiceBusClient client, string topicName, string subscriptionName)
    {
        _logger = lifetimeScope.Resolve<ILogger<ServiceBusSubscriptionMessageReceiver>>();
        _lifetimeScope = lifetimeScope;
        _processor = client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions() { AutoCompleteMessages = false });
    }

    public Task StartListeningAsync<T>(
    Func<MessageHeader, T, Task> handler,
    CancellationToken? cancellationToken = null)
    where T : class
    {
        var dispatcher = new LegacyJsonDispatcher<T>();
        return StartListeningAsync(handler, dispatcher, cancellationToken);
    }

    public async Task StartListeningAsync<T>(Func<MessageHeader, T, Task> messageHandler, IMessageBodyDispatcher<T> dispatcher, CancellationToken? cancellationToken = null) where T : class
    {
        _processor.ProcessMessageAsync += async args =>
        {
            await using var executionScope = _lifetimeScope.BeginLifetimeScope();
            string? version = null;
            try
            {
                if (!args.Message.ApplicationProperties.TryGetValue(nameof(MessageHeader.MessageVersion), out var rawVersion))
                {
                    throw new MissingMessageVersionException();
                }

                version = rawVersion?.ToString();

                var reader = MessageHeaderVersionFactory.ResolveReader(version);
                var header = reader.Read(args.Message);

                var body = args.Message.Body.ToString();
                var message = dispatcher.Deserialize(body, version!);

                if (message != null)
                {
                    try
                    {
                        await messageHandler(header, message);
                        await args.CompleteMessageAsync(args.Message);
                    }
                    catch (Exception e)
                    {
                        await args.AbandonMessageAsync(args.Message, null, cancellationToken ?? CancellationToken.None);
                    }
                }
                else
                {
                    await args.DeadLetterMessageAsync(args.Message, new Dictionary<string, object>(), $"Could not parse message body as {typeof(T).FullName}");
                }

            }
            catch (UnsupportedMessageVersionException ex)
            {
                _logger.LogError(ex,
                    "Unsupported message version {Version}. MessageId: {MessageId}",
                    args.Message.ApplicationProperties[nameof(MessageHeader.MessageVersion)],
                    args.Message.MessageId);

                await args.DeadLetterMessageAsync(
                    args.Message,
                     new Dictionary<string, object>
                     {
                         ["MessageVersion"] = version ?? "unknown",
                         ["TargetType"] = $"Could not parse message body as {typeof(T).FullName}"
                     },
                    "UnsupportedVersion",
                    ex.Message);

            }
            catch (MissingMessageVersionException ex)
            {
                _logger.LogError(ex,
                    "Missing message version. MessageId: {MessageId}",
                    args.Message.MessageId);

                await args.DeadLetterMessageAsync(
                    args.Message,
                    new Dictionary<string, object>
                    {
                        ["MessageVersion"] = "missing",
                        ["TargetType"] = $"Could not parse message body as {typeof(T).FullName}"
                    },
                    "MissingVersion",
                    ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled error processing message. MessageId: {MessageId}",
                    args.Message.MessageId);

                await args.AbandonMessageAsync(args.Message, null, cancellationToken ?? CancellationToken.None);
            }

        };

        _processor.ProcessErrorAsync += args =>
        {
            // Log or handle the error as needed
            _logger.LogError(args.Exception, "Exception while processing");
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(cancellationToken ?? CancellationToken.None);
    }

    public async Task StopListeningAsync(CancellationToken? cancellationToken = null)
    {
        await _processor.StopProcessingAsync(cancellationToken ?? CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
    }

    protected string GetProperty(IReadOnlyDictionary<string, object> props, string propertyName)
        => props.TryGetValue(propertyName, out var ver) ? ver as string ?? "" : "";

    internal sealed class LegacyJsonDispatcher<T>
    : IMessageBodyDispatcher<T>
    where T : class
    {
        public IReadOnlyCollection<string> SupportedVersions => new[] { "legacy" };

        public T Deserialize(string body, string version)
            => JsonSerializer.Deserialize<T>(body)
               ?? throw new MessageDeserializationException(typeof(T), version);
    }
}