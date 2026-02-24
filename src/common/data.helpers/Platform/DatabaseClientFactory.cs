using System.Collections.Concurrent;
using System.Text.Json;
using EI.API.Platform.Sdk;
using EI.API.Platform.Sdk.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;

namespace EI.API.Service.Data.Helpers.Platform;

public interface IDatabaseClientFactory
{
    Task<TDbContext> GetDbContext<TDbContext>(Guid clientId, string dataSourceKey)
        where TDbContext : DbContext;
}

internal static class DatabaseHelper
{
    private static readonly ConcurrentDictionary<string, object> _migratedFlags = new();

    internal static async Task Migrate(ILogger<DatabaseClientFactory> logger, string connectionString, DbContext context)
    {
        if (_migratedFlags.TryAdd(connectionString, new object()))
        {
            try
            {
                var retryPolicy = Policy
                                  .Handle<InvalidOperationException>()
                                  .Or<SqlException>()
                                  .WaitAndRetryAsync(retryCount: 5, sleepDurationProvider: attemptNumber => TimeSpan.FromSeconds(attemptNumber));

                await retryPolicy.ExecuteAsync(async () =>
                                                   {
                                                       var serverName = string.Empty;
                                                       var databaseName = string.Empty;
                                                       #region Try Parsing the connection string so the logs are more informative
                                                       if (logger.IsEnabled(LogLevel.Information))
                                                       {
                                                           try
                                                           {
                                                               var connStrParser = new SqlConnectionStringBuilder(connectionString);
                                                               serverName = connStrParser.DataSource;
                                                               databaseName = connStrParser.InitialCatalog;
                                                           }
                                                           catch (Exception e)
                                                           {
                                                               serverName = "(unknown)";
                                                               databaseName = "(unknown)";
                                                           }
                                                       }
                                                       #endregion Try Parsing the connection string so the logs are more informative

                                                       var dbCreator = context.GetService<IRelationalDatabaseCreator>();
                                                       if (!await dbCreator.ExistsAsync())
                                                       {
                                                           logger.LogInformation("Database '{0}/{1}' not found, creating...", serverName, databaseName);
                                                           await dbCreator.CreateAsync();
                                                           logger.LogInformation("Created database '{0}/{1}'", serverName, databaseName);
                                                       }

                                                       logger.LogInformation("Beginning migrations for database {0}/{1}", serverName, databaseName);
                                                       await context.Database.MigrateAsync();
                                                       logger.LogInformation("Completed migrations for database {0}/{1}", serverName, databaseName);
                                                   });
            }
            catch (Exception e)
            {
                _migratedFlags.TryRemove(connectionString, out _);
                throw;
            }
        }
    }
}

public class DatabaseClientFactory(ILogger<DatabaseClientFactory> logger, IPlatformConfigClient platform, IConfiguration configuration, IHostEnvironment hostEnvironment)
    : IDatabaseClientFactory
{
    protected readonly ILogger<DatabaseClientFactory> _logger = logger;
    protected readonly IConfiguration _configuration = configuration;
    protected readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    protected readonly IPlatformConfigClient _platform = platform;

    public async Task<TDbContext> GetDbContext<TDbContext>(Guid clientId, string dataSourceKey)
        where TDbContext : DbContext
    {
        TDbContext context;
        string connectionString;

        if (dataSourceKey == "EIPlatformCore")
        {
            // Special case: this is the platform's own database, and the
            // connection string needs to be supplied by the platform API's
            // configuration (e.g. appSettings.json or container environment variable)
            (context, connectionString) = GetPlatformDataContext<TDbContext>(dataSourceKey);
        }
        else
        {
            (context, connectionString) = await GetClientDataContext<TDbContext>(clientId, dataSourceKey);
        }

        await DatabaseHelper.Migrate(_logger, connectionString, context);

        return context;
    }

    protected virtual async Task<(TDbContext Context, string ConnectionString)> GetClientDataContext<TDbContext>(Guid clientId, string dataSourceKey)
        where TDbContext : DbContext
    {
        var dataSourceConfig = await _platform.GetDataSourceAsync(clientId, dataSourceKey);

        if (dataSourceConfig == null)
        {
            if (_hostEnvironment.IsDevelopment())
            {
                // Try loading a connection string from configuration (appSettings.json or env var)
                // only if this is a development environment (ex: local dev, or integration tests)
                var testConnectionString = _configuration.GetConnectionString(dataSourceKey);
                if (!string.IsNullOrEmpty(testConnectionString))
                {
                    var testContext = BuildFromConnectionString<TDbContext>(testConnectionString);
                    return (testContext, testConnectionString);
                }
            }

            throw new Exception($"DataSource '{dataSourceKey}' not defined for this client");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = dataSourceConfig.Port < 1 ? dataSourceConfig.Uri : $"{dataSourceConfig.Uri},{dataSourceConfig.Port}",
            InitialCatalog = dataSourceConfig.ResourceName,
            Encrypt = true,
            ConnectTimeout = 30,
            TrustServerCertificate = true,
            // Optional: Additional settings
        };

        switch (dataSourceConfig.AuthenticationScheme)
        {
            case "None":
                // Assume that the connection string is complete as it was
                // returned from the platform as the DataSourceInfo's URI
                break;

            case "Service Principal":
                // Assuming that we're running in Azure, we need our connection string to be formatted as:
                //
                //      Server={dataSourceConfig.Uri};Authentication=Active Directory Default; Database={dataSourceConfig.ResourceName}; User Id={current identity's UUID}

                // We get the managed identity's client ID from KeyVault, and the DataSource
                // specifies which secret to use via its SecretURI property.
                if (string.IsNullOrWhiteSpace(dataSourceConfig.SecretURI))
                {
                    throw new Exception($"Data Source {dataSourceConfig.Id} does not specify the Secret URI value");
                }

                var managedIdentityId = _configuration[dataSourceConfig.SecretURI];
                if (string.IsNullOrWhiteSpace(managedIdentityId))
                {
                    throw new Exception($"Data Source {dataSourceConfig.Id} does not refer to an existing secret");
                }

                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
                builder.UserID = managedIdentityId;

                break;

            case "User Name and Password":
                // Assume secrets are loaded into IConfiguration:
                var userInfo = _configuration[dataSourceConfig.SecretURI ?? throw new Exception("missing secret uri")]
                               ?? throw new Exception($"user config not found for dataSource {dataSourceConfig.Id}");

                var json = JsonDocument.Parse(userInfo, new JsonDocumentOptions());
                var username = json.RootElement.GetProperty("username").GetString();
                var password = json.RootElement.GetProperty("password").GetString();

                if (string.IsNullOrEmpty(password))
                {
                    if (_hostEnvironment.IsDevelopment())
                    {
                        // Try loading a connection string from configuration (appSettings.json or env var)
                        // only if this is a development environment (ex: local dev, or integration tests)
                        var testConnectionString = _configuration.GetConnectionString(dataSourceKey);
                        if (!string.IsNullOrEmpty(testConnectionString))
                        {
                            var parser = new SqlConnectionStringBuilder(testConnectionString);
                            username = parser.UserID;
                            password = parser.Password;
                        }
                    }
                }

                builder.UserID = username;
                builder.Password = password;

                break;

            /*
            // TODO: Implement KeyVault access to retrieve secrets:
            case AuthenticationScheme.Basic:
                // Generally should only be used for tests and local dev; deployed environments
                // should generally require ServicePrincipal authentication.
                var secret = GetSecret(dataSourceConfig.SecretURI);
                builder.IntegratedSecurity = false;
                builder.UserID = secret.Username;
                builder.Password = secret.Password;
                break;
            */

            default:
                throw new Exception($"Unsupported authentication scheme '{dataSourceConfig.AuthenticationScheme}' for DataSource '{dataSourceKey}' (id:{dataSourceConfig.Id})");
        }

        var connectionString = builder.ConnectionString;
        var context = BuildFromConnectionString<TDbContext>(connectionString);

        return (context, connectionString);
    }

    protected virtual (TDbContext Context, string ConnectionString) GetPlatformDataContext<TDbContext>(string dataSourceKey)
        where TDbContext : DbContext
    {
        // Special case: this is the platform's own database, and the
        // connection string needs to be supplied by the platform API's
        // configuration (e.g. appSettings.json or container environment variable)
        var connectionString = _configuration.GetConnectionString(dataSourceKey);
        if (connectionString == null)
        {
            throw new Exception($"Connection string '{dataSourceKey}' not found in configuration");
        }

        var context = BuildFromConnectionString<TDbContext>(connectionString);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var parser = new SqlConnectionStringBuilder(connectionString);
            _logger.LogInformation("Retrieved {0} ConnectionString: Server: {1} / DB: {2} / Auth: {3}",
                                   dataSourceKey, parser.DataSource, parser.InitialCatalog, parser.Authentication);
        }
        return (context, connectionString);
    }

    protected virtual TDbContext BuildFromConnectionString<TDbContext>(string connectionString)
        where TDbContext : DbContext
    {
        var ctor = typeof(TDbContext).GetConstructor([typeof(string)]);
        if (ctor == null)
        {
            throw new Exception($"DbContext '{typeof(TDbContext).Name}' does not have a constructor that takes a connection string");
        }

        return (TDbContext)ctor.Invoke([connectionString]);
    }
}
