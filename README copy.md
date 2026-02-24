# Introduction

This project is developer infrastructure for running the Eclipse Insights suite of applications.
It uses the [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) framework to provide
a common set of services and settings across all applications, and a convenient portal for viewing
logs and telemetry for the running applications.

This repository contains (currently) five projects:

- `src/aspire/EI.Dev.Aspire.AppHost.csproj` - This is the .NET Aspire host application that orchestrates
  the launching of the applications that make up the Eclipse Insights suite. It is never used outside of
  a developement environment, and is never deployed to a hosted environment.
- `src/common/defaults/EI.API.ServiceDefaults.csproj` - This project is published as a
  [private NuGet package](https://dev.azure.com/EclipseInsightsHC/Commercialization/_artifacts/feed/eclipse-insights-nuget/NuGet/EI.API.ServiceDefaults/)
  that applications that run in the Eclipse Insights suite can reference to get common services and settings.
- `src/common/data.helpers/EI.API.Service.Data.Helpers.csproj` - This project is published as a
  [private NuGet package](https://dev.azure.com/EclipseInsightsHC/Commercialization/_artifacts/feed/eclipse-insights-nuget/NuGet/EI.API.Service.Data.Helpers/)
  that applications that run in the Eclipse Insights suite can reference to utilize common Entity Framework
  base classes and patterns.
- `src/common/rest.helpers/EI.API.Service.Rest.Helpers.csproj` - This project is published as a
  [private NuGet package](https://dev.azure.com/EclipseInsightsHC/Commercialization/_artifacts/feed/eclipse-insights-nuget/NuGet/EI.API.Service.Rest.Helpers/)
  that applications that run in the Eclipse Insights suite can reference to utilize common ASP.NET REST API
  base classes and patterns, for example bidirectional DTO-Entity mapping and common Controller logic.
- `src/common/test.helpers/EI.Data.TestHelpers.csproj` - This project can be directly referenced to use
  the base test classes and helpers it contains for testing Controllers, Repositories, Mappers, etc.
  

# Getting Started

## Prerequisites

- Install [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- Install the [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling?tabs=windows&pivots=visual-studio#install-net-aspire)
- Install a container runtime (ex: [Docker Desktop](https://www.docker.com/products/docker-desktop))
- Install [VS Code](https://code.visualstudio.com/), optionally with the [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
- Install Node, either directly or via nvm
  - Current expected Node version: 20.x
- Install [pnpm](https://pnpm.io/installation)
- Optional: Install Visual Studio 2022

## Fetch and Build Source

Clone this repository, and related repositories to the same folder. The repositories are:

- `dev-infra` (this repository)
- `cloud-infrastructure` - contains Bicep templates for deploying shared infrastructure
- `fhir-connector` - contains the UI and API for retrieving and displaying FHIR data
- `file-manager` - contains the UI and API for uploading PDF files to Azure Blob Storage
- `platform-core` - contains the UI and API for the base platform services
- `process-log` - contains the UI and API for logging processes such as file ETL
- `report-viewer` - contains the UI and API for displaying Power BI reports
- `rules-engine` - contains the UI and API for the base rules services
- `web-frontend` - contains the primary UI for the application, which loads UI modules from other repositories
- `web-user-work-queues` - holds the UI for User Work Queues functionality
- `web-module-template` - a base repository that is a template for creating new modules (UI, Services, etc.)

### Private NuGet Feed Authentication

To retrive privately published NuGet packages, authenticate with the Azure DevOps artifact feed, do a one-time
interactive restore to be prompted for credentials. Install the
[Artifact Credential Provider](https://github.com/microsoft/artifacts-credprovider), then run:

```sh
pushd dev-infra/src/
dotnet restore --interactive
```

If you get an error, you may need to configure the NuGet package source:
```sh
dotnet nuget add source --name Eclipse-Insights https://pkgs.dev.azure.com/EclipseInsightsHC/Commercialization/_packaging/eclipse-insights-nuget/nuget/v3/index.json
```


That should prompt you to navigate to a web page to enter a code that will configure the NuGet access.



## Connect repos to supernova-core artifact

Go to artifacts in azure and click on "Connect to Feed" [azure artifacts](https://dev.azure.com/EclipseInsightsHC/Commercialization/_artifacts/feed/eclipse-insights-nuget/connect)

Select npm, and execute azure artifacts instruction inside one of the web projects (for example: web-frontend).





### Running the .NET API Applications

#### Configure Azure Access via Service Principals

When running the API applications locally, they will often need access to Azure resources (KeyVault, Storage Accounts, etc.).
To authenticate and authorize, the applications [use a Service Principal](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/local-development-service-principal)
configured in Azure, requiring that secret values be configured for the local environment.

Go to Azure KeyVault for the development environment, `kv-eclipsesaas-dev`, find the secrets named `Dev-SP-ClientId` and `Dev-SP-ClientSecret`, and get their values.
In a console window, navigate to the `dev-infra/src` directory and run these commands to securely store the secret values:
```bash
dotnet user-secrets set AZURE_TENANT_ID 3972C759-11D9-41AA-ADE0-2315CCAC1D7F
dotnet user-secrets set AZURE_CLIENT_ID __the_client_id_value_from_KeyVault__
dotnet user-secrets set AZURE_CLIENT_SECRET __the_client_secret_value_from_KeyVault__
```

If you are going to run the Platform API via Docker Compose, you'll also need to put these settings into a `.env` file in the `platform-core/source/api` folder:
```
AZURE_TENANT_ID=3972C759-11D9-41AA-ADE0-2315CCAC1D7F
AZURE_CLIENT_ID=__the_client_id_value_from_KeyVault__
AZURE_CLIENT_SECRET=__the_client_secret_value_from_KeyVault__
```

Repeat the steps in the `platform-core/source/api` folder if you plan on running just the Platform API outside of the .NET Aspire solution as well.

### Running the Apsire .NET AppHost

If you have Visual Studio 2022, open the main solution (`dev-infra/src/EI.Dev.Aspire.sln`) and
run the default startup profile.

## Running the Applications

### Pre-Build Node Applications

Run `pnpm install` in each node application:

```sh
pushd platform-core/source/web
pnpm install
popd

pushd fhir-connector/source/web
pnpm install
popd

pushd file-manager/source/web
pnpm install
popd

pushd process-log/source/web
pnpm install
popd

pushd report-viewer/source/web
pnpm install
popd

pushd web-frontend/source/web
pnpm install
popd

pushd web-user-work-queues/source/web
pnpm install
popd

pushd web-module-template/source/web
pnpm install
popd
```

# Instructions to run pytest for all API's  
we can use the below comment -
    python Name_of_the_test_file (with location)

For now we have named the file as runtests-pipeline.py and it is located under source/api/Tests folder so we can use the below comment
    python source/api/Tests/runtests-pipeline.py

# Instructions to run pytest for particular API 
Make sure there is no dependency for the API, if so first run the parent API then the child API
we can use the below comment -
    python -m pytest Name_of_the_test_file (with location)

We can take one test script which is located under source/api/Tests folder
we can use the below comment -
    python -m pytest source/api/Tests/test_ApplicationApi.py

#Prerequisites to be installed to run pytest 
1. Pytest 
2. Request
3. Python
4. SQLAlchemy
We have a file named requiremets.txt which will contain the list of prerequisites to be installed
Test will run in the pipeline with the current versions of these requirements.

# Database connection
We currently use a docker container with SQLServer for local environments. To populate database you need to run EI.Dev.Aspire (dev-infra repo). To connect to database ensure connectionString uses `HostPort` and `MSSQL_SA_PASSWORD` as is defined in the container file (use Docker Desktop, select Container and select Inspect):
```json
	"NetworkSettings": {
    ...
		"Ports": {
			"1433/tcp": [
				{
					"HostIp": "127.0.0.1",
					"HostPort": "54953"
				}
			]
		},
  ...
"Config": {
  ...
		"Env": [
			"ACCEPT_EULA=Y",
			"MSSQL_SA_PASSWORD=0kasC66YrwW)df*cqdWevn",
      ...
		],
```
