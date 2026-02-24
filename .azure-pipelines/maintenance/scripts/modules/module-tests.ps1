$moduleFile = Join-Path $PSScriptRoot "AzureDevOpsNuGet.psm1"
if (Test-Path $moduleFile) {
    Import-Module $moduleFile -Force
} else {
    Write-Error "Required module not found: $moduleFile"
    exit 1
}

$VerbosePreference = "Continue"
$SourcesDirectory = "E:\Dev\ei_e"
$Organization = "https://dev.azure.com/EclipseInsightsHC/"
$Project = "Commercialization"

$repoWorkingDir = Set-RepositoryWorkingDirectory `
                            -SourcesDirectory $SourcesDirectory `
                            -RepositoryName "dev-infra" `
                            -TestPath "src/aspire"

Write-Host "Set-RepositoryWorkingDirectory result: $repoWorkingDir"

# if ($repoWorkingDir) {
#     Test-NuGetPackageVersion `
#             -PackageName "EI.API.Service.Data.Helpers" `
#             -PackageVersion "1.1.10" `
#             -Organization $Organization `
#             -Project $Project `
#             -Token $env:AZURE_DEVOPS_TOKEN

    $latestVersion = Get-LatestNuGetPackageVersion `
            -PackageName "EI.API.Service.Data.Helpers" `
            -FeedName "eclipse-insights-nuget" `
            -Organization $Organization `
            -Project $Project `
            -Token $env:AZURE_DEVOPS_TOKEN

    Write-Host "Latest version retrieved: $latestVersion"


#     $updated = Update-CsProjectNugetPackageReference `
#             -CsprojPath "src/common/rest.helpers/EI.API.Service.Rest.Helpers.csproj" `
#             -PackageName "EI.API.Service.Data.Helpers" `
#             -PackageVersion "1.1.20"

#     Write-Host "Update-CsProjectNugetPackageReferences result: $updated"
# }

# $workItemDetail = Get-LatestWorkItemDetails `
#         -RepoName "dev-infra" `
#         -ProjectFolder "src/common/data.helpers" `
#         -Token $env:AZURE_DEVOPS_TOKEN `
#         -Organization $Organization `
#         -Project $Project

# Write-Host "Get-LatestWorkItemDetails result: $($workItemDetail | Out-String)"

# $workItemDetail = Get-WorkItemDetails `
#         -WorkItemId 3087 `
#         -Token $env:AZURE_DEVOPS_TOKEN `
#         -Organization $Organization `
#         -Project $Project

# Write-Host "Get-WorkItemDetails result: $($workItemDetail | Out-String)"

# Write-Host "............................................................................"
# Write-Host "............................................................................"
# $prUrl = Submit-PullRequest `
#             -SourceBranch "maint/-update-EI.API.Service.Data.Helpers-1.1.21-12193" `
#             -TargetBranch "main" `
#             -Title "#3087: [Automated NuGet Maintenance] 12193: Update EI.API.Service.Data.Helpers to 1.1.21" `
#             -Description "[Automated NuGet Maintenance] Created by pipeline, updates EI.API.Service.Data.Helpers to version 1.1.21" `
#             -AssignedTo $workItemDetail.User `
#             -WorkItemId $workItemDetail.Id `
#             -WorkItemUrl $workItemDetail.Url `
#             -Token $env:AZURE_DEVOPS_TOKEN

# Write-Host "Submit-PullRequest result: $prUrl"