$VerbosePreference = "Continue"

. E:\Dev\ei_e\dev-infra\.azure-pipelines\maintenance\scripts\Update-AllApplication-NuGetPackages.ps1                                                             `
    -RepositoryName 'process-log'                                                                                                                              `
    -PackageNamesCsv 'EI.API.Platform.Sdk,EI.API.Service.Data.Helpers,EI.API.Service.Rest.Helpers,EI.API.ServiceDefaults,EI.API.Cloud.Clients,EI.Data.TestHelpers'        `
    -WorkItemId 3087                                                                                                                                             `
    -SourcesDirectory "e:\Dev\ei_e\process-log"                                                                                                                               `
    -SourcesDirectoryTest "source/api"                                                                                                                           `
    -BuildId "1.47.4747"                                                                                                                                         `
    -Organization "https://dev.azure.com/EclipseInsightsHC/"                                                                                                   `
    -Project "Commercialization"                                                                                                                               `
    -FeedName "eclipse-insights-nuget"
