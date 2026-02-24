using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);


const string databaseNameForPlatformCfg = "EIPlatformCore";

var commonResourcesGroup = builder.AddGroup("Common");

#region Shared Resources
var sqlServerName = "EclipseEnterprise";
var sqlUsername = "sa";
var sqlPassword = builder.Configuration[$"{sqlServerName}-SQLServer-{sqlUsername.ToUpper()}"];

// Build the "secret" JSON that would otherwise be in KeyVault
var sqlServerSecretJson = JsonSerializer.Serialize(new { username = sqlUsername, password = sqlPassword, });


var sqlserver =
    builder.AddSqlServer(sqlServerName, port: 63029 /*, password: sqlPassword*/)
           .WithDataVolume("EclipseInsightsEnterprise")
           .InGroup(commonResourcesGroup);

var messageBus =
    builder.AddAzureServiceBus("AzureServiceBus")
           .RunAsEmulator(options =>
                              {
                                  options.WithHostPort(57160); // keep the port consistent instead of random on every start-up
                                  options.WithConfigurationFile("./config/service-bus-config.json");
                              })
           .InGroup(commonResourcesGroup);

#endregion Shared Resources

#region Platform Config
var platformGroup = builder.AddGroup("Core-Platform");
var platformCoreDb =
    sqlserver
        .AddDatabase(databaseNameForPlatformCfg);

var efMigrationService = builder.AddProject<EI_API_Platform_Core_Data_MigrationService>("platformcore-dbmigration")
                                .WaitFor(platformCoreDb)
                                .WithReference(platformCoreDb, connectionName: databaseNameForPlatformCfg, optional: false)
                                .InGroup(platformGroup);

IResourceBuilder<ProjectResource>? devDataService = null;
if (builder.Environment.IsDevelopment())
{
    devDataService = builder.AddProject<EI_API_Platform_Core_Data_DevDataService>("platformcore-dbdevdata")
                            .WithReference(platformCoreDb, connectionName: databaseNameForPlatformCfg, optional: false)
                            .WaitForCompletion(efMigrationService)
                            .InGroup(platformGroup);
}

var platformCoreApi =
    builder.AddProject<EI_API_Platform_Core>("platformcoreapi")
           .WithReference(platformCoreDb, connectionName: databaseNameForPlatformCfg, optional: false)
           .WithExternalHttpEndpoints()
           .WithHttpEndpoint(name: "graphql", port: 5000)
           .WaitForCompletion(devDataService ?? efMigrationService)

           // Configure secrets required to impersonate as an Azure Service Principal:
           .WithEnvironment("ConnectionStrings__AzureKeyVault", "https://kv-eclipsesaas-dev.vault.azure.net/")

           // Add the "secret" for the local Aspire DB:
           .WithEnvironment("LocalDevOnly_SqlDb_Secret", sqlServerSecretJson)

           .WithEnvironment("AZURE_CLIENT_ID", builder.Configuration["AZURE_CLIENT_ID"])
           .WithEnvironment("AZURE_CLIENT_SECRET", builder.Configuration["AZURE_CLIENT_SECRET"])
           .WithEnvironment("AZURE_TENANT_ID", builder.Configuration["AZURE_TENANT_ID"])

           .InGroup(platformGroup);

var platformCoreWeb =
    builder.AddNpmApp("platformcoreweb", "../../../platform-core/source/web")
           .WithReference(platformCoreApi)
           .WithHttpEndpoint(env: "PORT")
           .WithExternalHttpEndpoints()
           .PublishAsDockerFile()
           .InGroup(platformGroup);

#endregion Platform Config

#region Process Log

var processLogGroup = builder.AddGroup("Module-Process-Log");

IResourceBuilder<ProjectResource>? processLogApi = null;
IResourceBuilder<NodeAppResource>? processLogWeb = null;
if (Directory.Exists("../../../process-log"))
{
    processLogApi =
        builder.AddProject<EI_API_ProcessLogging>("processlogapi")
               .WithReference(platformCoreApi)
               .WithReference(messageBus)
               .WithExternalHttpEndpoints()

               // Configure secrets required to impersonate as an Azure Service Principal:
               .WithEnvironment("ConnectionStrings__AzureKeyVault", "https://kv-eclipsesaas-dev.vault.azure.net/")

               // Add the "secret" for the local Aspire DB:
               .WithEnvironment("LocalDevOnly_SqlDb_Secret", sqlServerSecretJson)

               .WithEnvironment("AZURE_CLIENT_ID", builder.Configuration["AZURE_CLIENT_ID"])
               .WithEnvironment("AZURE_CLIENT_SECRET", builder.Configuration["AZURE_CLIENT_SECRET"])
               .WithEnvironment("AZURE_TENANT_ID", builder.Configuration["AZURE_TENANT_ID"])
               .InGroup(processLogGroup);

    processLogWeb =
        builder.AddNpmApp("processlogweb", "../../../process-log/source/web")
               .WithReference(platformCoreApi)
               .WithReference(processLogApi)
               .WithReference(platformCoreWeb)
               .WithHttpEndpoint(env: "PORT")
               .WithExternalHttpEndpoints()
               .PublishAsDockerFile()
               .InGroup(processLogGroup);

}
else
{
    Console.Error.WriteLine("Workspace not found: process-log");
}


#endregion Process Log

#region User Work Queues

var workQueuesGroup = builder.AddGroup("Module-Work-Queues");
IResourceBuilder<SqlServerDatabaseResource>? workQueuesDb = null;
IResourceBuilder<ProjectResource>? workQueuesApi = null;
IResourceBuilder<NodeAppResource>? workQueuesWeb = null;
if (Directory.Exists("../../../web-user-work-queues"))
{
    /*

    workQueuesDb =
        sqlserver
            .AddDatabase(databaseNameForWorkQueues);

    workQueuesApi =
        builder.AddProject<EI_API_UserWorkQueues>("userworkqueueapi")
               .WithReference(platformCoreApi)
               .WithReference(workQueuesDb, connectionName: databaseNameForWorkQueues, optional: false)
               .WithExternalHttpEndpoints()
               .InGroup(workQueuesGroup();

    workQueuesWeb =
        builder.AddNpmApp("userworkqueuesweb", "../../../web-user-work-queues/source/web")
               .WithReference(workQueuesApi)
               .WithHttpEndpoint(env: "PORT")
               .WithExternalHttpEndpoints()
               .PublishAsDockerFile()
               .InGroup(workQueuesGroup();
    */
}
else
{
    Console.Error.WriteLine("Workspace not found: web-user-work-queues");
}

#endregion User Work Queues

#region FHIR Connector

var fhirConnectorGroup = builder.AddGroup("Module-FHIR-Connector");
IResourceBuilder<ProjectResource>? fhirConnectorApi = null;
IResourceBuilder<NodeAppResource>? fhirConnectorWeb = null;
if (Directory.Exists("../../../fhir-connector"))
{
    fhirConnectorApi =
        builder.AddProject<EI_API_FhirConnector>("fhirconnectorapi")
               .WithExternalHttpEndpoints()
               .WithReference(platformCoreApi)

               // Configure secrets required to impersonate as an Azure Service Principal:
               .WithEnvironment("ConnectionStrings__AzureKeyVault", "https://kv-eclipsesaas-dev.vault.azure.net/")

               .WithEnvironment("AZURE_CLIENT_ID", builder.Configuration["AZURE_CLIENT_ID"])
               .WithEnvironment("AZURE_CLIENT_SECRET", builder.Configuration["AZURE_CLIENT_SECRET"])
               .WithEnvironment("AZURE_TENANT_ID", builder.Configuration["AZURE_TENANT_ID"])
               .InGroup(fhirConnectorGroup);

    fhirConnectorWeb =
        builder.AddNpmApp("fhirconnectorweb", "../../../fhir-connector/source/web")
               .WithReference(fhirConnectorApi)
               .WithHttpEndpoint(env: "PORT")
               .WithExternalHttpEndpoints()
               .PublishAsDockerFile()
               .InGroup(fhirConnectorGroup);

}
else
{
    Console.Error.WriteLine("Workspace not found: fhir-connector");
}

#endregion FHIR Connector

#region File Manager

var fileManagerGroup = builder.AddGroup("Module-File-Manager");
IResourceBuilder<ProjectResource>? fileManagerApi = null;
IResourceBuilder<NodeAppResource>? fileManagerWeb = null;
if (Directory.Exists("../../../file-manager"))
{
    fileManagerApi =
        builder.AddProject<EI_API_FileIngestion>("filemanagerapi")
               .WithExternalHttpEndpoints()
               .WithReference(platformCoreApi)
               .WithReference(messageBus)

               // Configure secrets required to impersonate as an Azure Service Principal:
               .WithEnvironment("ConnectionStrings__AzureKeyVault", "https://kv-eclipsesaas-dev.vault.azure.net/")

               // Add the "secret" for the local Aspire DB:
               .WithEnvironment("LocalDevOnly_SqlDb_Secret", sqlServerSecretJson)

               .WithEnvironment("AZURE_CLIENT_ID", builder.Configuration["AZURE_CLIENT_ID"])
               .WithEnvironment("AZURE_CLIENT_SECRET", builder.Configuration["AZURE_CLIENT_SECRET"])
               .WithEnvironment("AZURE_TENANT_ID", builder.Configuration["AZURE_TENANT_ID"])

               .InGroup(fileManagerGroup);

    fileManagerWeb =
        builder.AddNpmApp("filemanagerweb", "../../../file-manager/source/web")
               .WithReference(fileManagerApi)
               .WithHttpEndpoint(env: "PORT")
               .WithExternalHttpEndpoints()
               .PublishAsDockerFile()
               .InGroup(fileManagerGroup);

}
else
{
    Console.Error.WriteLine("Workspace not found: file-manager");
}

#endregion File Manager

#region Report Viewer

var reportViewerGroup = builder.AddGroup("Module-Report-Viewer");
IResourceBuilder<SqlServerDatabaseResource>? reportViewerDb = null;
IResourceBuilder<ProjectResource>? reportViewerApi = null;
IResourceBuilder<NodeAppResource>? reportViewerWeb = null;
if (Directory.Exists("../../../report-viewer"))
{
    reportViewerApi =
        builder.AddProject<EI_API_ReportViewer>("reportviewerapi")
               .WithExternalHttpEndpoints()
               .WithHttpEndpoint(name: "graphql")

               // Configure secrets required to impersonate as an Azure Service Principal:
               .WithEnvironment("ConnectionStrings__AzureKeyVault", "https://kv-eclipsesaas-dev.vault.azure.net/")

               .WithEnvironment("AZURE_CLIENT_ID", builder.Configuration["AZURE_CLIENT_ID"])
               .WithEnvironment("AZURE_CLIENT_SECRET", builder.Configuration["AZURE_CLIENT_SECRET"])
               .WithEnvironment("AZURE_TENANT_ID", builder.Configuration["AZURE_TENANT_ID"])

               .InGroup(reportViewerGroup);

    reportViewerWeb =
        builder.AddNpmApp("reportviewerweb", "../../../report-viewer/source/web")
               .WithReference(reportViewerApi)
               .WithHttpEndpoint(env: "PORT")
               .WithExternalHttpEndpoints()
               .PublishAsDockerFile()
               .InGroup(reportViewerGroup);
}
else
{
    Console.Error.WriteLine("Workspace not found: report-viewer");
}


#endregion Report Viewer

#region Rules Engine

var rulesEngineGroup = builder.AddGroup("Module-Rules-Engine");
// IResourceBuilder<SqlServerDatabaseResource>? rulesEngineDb = null;
IResourceBuilder<ProjectResource>? rulesEngineApi = null;
IResourceBuilder<NodeAppResource>? rulesEngineWeb = null;
if (Directory.Exists("../../../rules-engine"))
{
    rulesEngineApi =
        builder.AddProject<EI_API_RulesEngine>("rulesengineapi")
               .WithExternalHttpEndpoints()

               // Configure secrets required to impersonate as an Azure Service Principal:
               .WithEnvironment("ConnectionStrings__AzureKeyVault", "https://kv-eclipsesaas-dev.vault.azure.net/")

               .WithEnvironment("AZURE_CLIENT_ID", builder.Configuration["AZURE_CLIENT_ID"])
               .WithEnvironment("AZURE_CLIENT_SECRET", builder.Configuration["AZURE_CLIENT_SECRET"])
               .WithEnvironment("AZURE_TENANT_ID", builder.Configuration["AZURE_TENANT_ID"])

               .InGroup(rulesEngineGroup);

    rulesEngineWeb =
        builder.AddNpmApp("rulesengineweb", "../../../rules-engine/source/web")
               .WithReference(rulesEngineApi)
               .WithHttpEndpoint(env: "PORT")
               .WithExternalHttpEndpoints()
               .PublishAsDockerFile()
               .InGroup(rulesEngineGroup);

}
else
{
    Console.Error.WriteLine("Workspace not found: rules-engine");
}
#endregion Rules Engine

var solution = builder.AddNpmApp("host-ui", "../../../web-frontend/source/web")
           // Platform
           .WithReference(platformCoreApi)
           .InGroup(platformGroup);

// ProcessLog
if (processLogApi != null)
    solution = solution.WithReference(processLogApi);

if (processLogWeb != null)
    solution = solution.WithReference(processLogWeb);

// UserWorkQueues
if (workQueuesApi != null)
    solution = solution.WithReference(workQueuesApi);
// TODO:
// .WithReference(userWorkQueuesWeb)

// FHIR Connector
if (fhirConnectorWeb != null)
    solution = solution.WithReference(fhirConnectorWeb);

if (fhirConnectorApi != null)
    solution = solution.WithReference(fhirConnectorApi);

// File Manager
if (fileManagerWeb != null)
    solution = solution.WithReference(fileManagerWeb);

if (fileManagerApi != null)
    solution = solution.WithReference(fileManagerApi);

// Report Viewer
if (reportViewerWeb != null)
    solution = solution.WithReference(reportViewerWeb);

if (reportViewerApi != null)
    solution = solution.WithReference(reportViewerApi);

// Rules Engine
if (rulesEngineWeb != null)
    solution = solution.WithReference(rulesEngineWeb);

if (rulesEngineApi != null)
    solution = solution.WithReference(rulesEngineApi);


var webHostUI = solution
                .WithHttpEndpoint(env: "PORT")
                .WithExternalHttpEndpoints()
                .PublishAsDockerFile();


builder.Build().Run();


