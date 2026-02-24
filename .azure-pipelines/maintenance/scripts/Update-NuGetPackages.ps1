param(
  [Parameter(Mandatory = $true)]
  [string]$PackageName,
    
  [Parameter(Mandatory = $false)]
  [string]$PackageVersion,
    
  [Parameter(Mandatory = $true)]
  [string]$WorkItemId,
    
  [Parameter(Mandatory = $true)]
  [string]$SourcesDirectory,
    
  [Parameter(Mandatory = $true)]
  [string]$BuildId,
    
  [Parameter(Mandatory = $false)]
  [string]$Organization = "https://dev.azure.com/EclipseInsightsHC/",
    
  [Parameter(Mandatory = $false)]
  [string]$Project = "Commercialization",

  [Parameter(Mandatory = $false)]
  [string]$FeedName = "eclipse-insights-nuget"
)

$VerbosePreference = "Continue"

# Import the Azure DevOps NuGet module
$moduleFile = Join-Path $PSScriptRoot "modules\AzureDevOpsNuGet.psm1"
if (Test-Path $moduleFile) {
  Import-Module $moduleFile -Force
}
else {
  Write-Error "Required module not found: $moduleFile"
  exit 1
}

# Get Azure DevOps token from environment variable for security
$Token = $env:AZURE_DEVOPS_TOKEN
if ([string]::IsNullOrWhiteSpace($Token)) {
  Write-Error "Azure DevOps token not found in environment variable AZURE_DEVOPS_TOKEN"
  Write-Host "Please ensure the token is passed as an environment variable from the pipeline"
  exit 1
}

Write-Host "SourcesDirectory: $SourcesDirectory"
Write-Host "Current directory: $(Get-Item . | Select-Object -ExpandProperty FullName)"

# Ensure we're in the `dev-infra` folder; this may vary based on where
# the pipeline is run from - manually or called from another pipeline.
Set-RepositoryWorkingDirectory `
  -SourcesDirectory $SourcesDirectory `
  -RepositoryName "dev-infra" `
  -TestPath "src/aspire"

$priorChangeSourceRepo = "dev-infra"
$priorChangeSourceDir = ""
$NugetFeedName = "eclipse-insights-nuget"

$projects = @()

switch ($PackageName) {
  "EI.API.Platform.Sdk" {
    $priorChangeSourceRepo = "platform-core"
    $priorChangeSourceDir = "source/api/EI.API.Platform.Sdk"
    $projects = @("$sourcesDirectory/src/common/data.helpers/EI.API.Service.Data.Helpers.csproj")
  }
  "EI.API.Service.Data.Helpers" {
    $priorChangeSourceDir = "src/common/data.helpers"
    $projects = @("$sourcesDirectory/src/common/rest.helpers/EI.API.Service.Rest.Helpers.csproj")
  }
  "EI.API.Service.Rest.Helpers" {
    $priorChangeSourceDir = "src/common/rest.helpers"
    $projects = @(
      "$sourcesDirectory/src/common/defaults/EI.API.ServiceDefaults.csproj",
      "$sourcesDirectory/src/common/test.helpers/EI.Data.TestHelpers.csproj"
      )
  }
  "EI.API.Cloud.Clients" {
    $priorChangeSourceDir = "src/common/cloud"
    $projects = @("$sourcesDirectory/src/common/cloud/EI.API.Cloud.Clients.csproj")
  }
  default { }
}

#########################################################################################################
#
#     Get the latest version of the specified package from the Azure DevOps Feed
#
#########################################################################################################
if ($null -eq $PackageVersion -or $PackageVersion.Trim() -eq '') {
  Write-Host "No PackageVersion specified, will retrieve latest version from feed"
  try {
    Write-Host "Retrieving latest version of $PackageName from Azure DevOps feed..."
    $PackageVersion = Get-LatestNuGetPackageVersion -PackageName $PackageName -Organization $Organization -Project $Project -FeedName $NugetFeedName -Token $Token
    Write-Host "Latest version of $PackageName is $PackageVersion"
  }
  catch {
    Write-Host "Could not determine the latest version of $PackageName from the feed: $($_.Exception.Message)"
    exit 1
  }
}
else {
  try {
    Write-Host "Validating specified version $PackageVersion of $PackageName in Azure DevOps feed..."
    $versionExists = Test-NuGetPackageVersion -PackageName $PackageName -PackageVersion $PackageVersion -Organization $Organization -Project $Project -FeedName $NugetFeedName -Token $Token
    if ($versionExists) {
      Write-Host "Version $PackageVersion of $PackageName exists in the feed."
    }
    else {
      Write-Error "Version $PackageVersion of $PackageName does not exist in the feed."
      exit 1
    }
  }
  catch {
    Write-Host "Could not validate version $PackageVersion of $PackageName in the feed: $($_.Exception.Message)"

    # Try to get the latest version instead for our error message
    Write-Host "Retrieving latest version of $PackageName from Azure DevOps feed..."
    $PackageVersion = Get-LatestNuGetPackageVersion -PackageName $PackageName -Organization $Organization -Project $Project -Token $Token
    Write-Host "Latest version of $PackageName is $PackageVersion"
    exit 1
  }
}

#########################################################################################################
#
#     Loop through the affected projects and make the Package Version update changes
#
#########################################################################################################

$madeChanges = $false
foreach ($CsprojPath in $projects) {
  if (Test-Path $CsprojPath) {
    $projectUpdated = Update-CsProjectNugetPackageReference `
      -CsprojPath $CsprojPath `
      -PackageName $PackageName `
      -PackageVersion $PackageVersion

    if ($projectUpdated) {
      $madeChanges = $true
      Write-Host "Updated $PackageName to version $PackageVersion in $CsprojPath"
    }
    else {
      Write-Host "No changes to $CsprojPath"
    }
  }
  else {
    Write-Error "Project file not found: $CsprojPath"
    $projects = Get-ChildItem -Path . -Filter *.csproj -Recurse
    Write-Host "Available projects:"
    $projects | ForEach-Object { Write-Host $_.FullName }
    exit 1
  }
}

#########################################################################################################
#
#     Create a PR with the package updates if any changes were made
#
#########################################################################################################
if ($madeChanges) {
  Write-Host "Changes made - need to branch, push, create PR..."

  # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #
  #     Need to find out details to link to the PR: WorkItem URL and User
  # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #
  
  $WorkItemInfo = $null

  if ([string]::IsNullOrWhiteSpace($WorkItemId)) {
    $WorkItemId = ""
  }

  if ($WorkItemId -ne '') {
    # We were provided with a Work Item ID, so to figure out who to assign the PR to, see who is
    # the assignee of the Work Item

    Write-Host "Using provided WorkItem ID: $WorkItemId"

    $WorkItemInfo = Get-WorkItemDetails `
      -WorkItemId $WorkItemId `
      -Token $env:AZURE_DEVOPS_TOKEN `
      -Organization $Organization `
      -Project $Project

  }
  else {
    if ($priorChangeSourceDir -ne "") {
      Write-Host "Trying to determine WorkItem ID from most recent commit"

      $WorkItemInfo = Get-LatestWorkItemDetails `
        -RepoName $priorChangeSourceRepo `
        -ProjectFolder $priorChangeSourceDir `
        -Token $env:AZURE_DEVOPS_TOKEN `
        -Organization $Organization `
        -Project $Project
    }
  }

  $WorkItemUrl = ''
  $WorkItemUser = ''

  if ($null -ne $WorkItemInfo) {
    $WorkItemUrl = $WorkItemInfo.Url
    $WorkItemUser = $WorkItemInfo.User
    Write-Host "Found Work Item ID: $WorkItemId by $WorkItemUser at $WorkItemUrl"
  }
  else {
    Write-Host "Could not determine Work Item ID from recent commits"
    $WorkItemId = $null
  }


  # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #
  #     Configure Git, create branch, commit changes, push branch
  # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #
  Write-Host "Performing Git Config...."
  Update-GitConfig `
    -UserEmail "buildagent@eclipseinsightshc.com" `
    -UserName "Azure DevOps Build Agent"

  $branchPrefix = $WorkItemId
  if ($null -eq $branchPrefix) {
    $branchPrefix = "auto"
  }
  $branchName = "maint/$branchPrefix$PackageName-$PackageVersion-$BuildId"

  Write-Host "Branch name: $branchName"

  # create new branch
  Write-Host "Creating new branch..."
  git checkout -b $branchName

  git add .
  git diff
  git commit -m "#$($branchPrefix): [Automated NuGet Maintenance] Update $PackageName nuget package version"

  Write-Host "Pushing branch to remote..."
  git push --set-upstream origin $branchName
  Write-Host "Creating Pull Request..."


  # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #
  #     Create PR using Azure DevOps REST API
  # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #
  Write-Host "Creating PR via REST API..."

  $prWebUrl = Submit-PullRequest `
                          -RepositoryName "dev-infra" `
                          -SourceBranch $branchName `
                          -TargetBranch "main" `
                          -Title "#$($branchPrefix): [Automated NuGet Maintenance] $($BuildId): Update $PackageName to $PackageVersion" `
                          -Description "[Automated NuGet Maintenance] Created by pipeline, updates $PackageName to version $PackageVersion" `
                          -AssignedTo $WorkItemUser `
                          -WorkItemId $WorkItemId `
                          -WorkItemUrl $WorkItemUrl `
                          -Token $env:AZURE_DEVOPS_TOKEN `

  Write-Host "##########################################################################################"
  Write-Host "#"
  Write-Host "#    Pull Request created: $prWebUrl"
  Write-Host "#"
  Write-Host "##########################################################################################"
}