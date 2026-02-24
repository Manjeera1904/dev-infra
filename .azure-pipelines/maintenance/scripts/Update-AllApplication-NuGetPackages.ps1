param(
  [Parameter(Mandatory = $true)]
  [string]$RepositoryName,
    
  [Parameter(Mandatory = $true)]
  [string]$PackageNamesCsv,

  [Parameter(Mandatory = $true)]
  [string]$WorkItemId,
    
  [Parameter(Mandatory = $true)]
  [string]$SourcesDirectory,
    
  [Parameter(Mandatory = $true)]
  [string]$SourcesDirectoryTest,
    
  [Parameter(Mandatory = $true)]
  [string]$BuildId,
    
  [Parameter(Mandatory = $false)]
  [string]$Organization = "https://dev.azure.com/EclipseInsightsHC/",
    
  [Parameter(Mandatory = $false)]
  [string]$Project = "Commercialization",

  [Parameter(Mandatory = $false)]
  [string]$FeedName = "eclipse-insights-nuget",

  [Parameter(Mandatory = $false)]
  [string]$priorChangeSourceRepo = "dev-infra",

  [Parameter(Mandatory = $false)]
  [string]$priorChangeSourceDir  = "src/common/defaults"
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
if ([string]::IsNullOrEmpty($Token)) {
  Write-Error "Azure DevOps token not found in environment variable AZURE_DEVOPS_TOKEN"
  Write-Host "Please ensure the token is passed as an environment variable from the pipeline"
  exit 1
}

Write-Host "SourcesDirectory: $SourcesDirectory"
Write-Host "Current directory: $(Get-Item . | Select-Object -ExpandProperty FullName)"

# Ensure we're in the repo's root folder; this may vary based on where
# the pipeline is run from - manually or called from another pipeline.
Set-RepositoryWorkingDirectory `
  -SourcesDirectory $SourcesDirectory `
  -RepositoryName $RepositoryName `
  -TestPath $SourcesDirectoryTest

$projects = Get-ChildItem -Path source/api -Recurse -Filter *.csproj | ForEach-Object { $_.FullName }
Write-Host "Found projects:"
$projects | ForEach-Object { Write-Host " - $_" }


#########################################################################################################
#
#     Get the latest version for each specified package from the Azure DevOps Feed
#
#########################################################################################################
# Build package version lookup hashtable
$PackageNames = $PackageNamesCsv -split ','

Write-Host "Building package version lookup for $($PackageNames.Count) packages..."
$packageVersionLookup = @{}
$packageVersionUpdates = @{}

foreach ($PackageName in $PackageNames) {
  try {
    Write-Verbose "Retrieving latest version for $packageName..."
    $version = Get-LatestNuGetPackageVersion -PackageName $packageName -Organization $Organization -Project $Project -FeedName $FeedName -Token $Token
    $packageVersionLookup[$packageName] = $version
    $packageVersionUpdates[$packageName] = @()
    Write-Verbose "  $packageName -> $version"
  }
  catch {
    Write-Error "Failed to get version for $packageName`: $($_.Exception.Message)"
    throw
  }
}

$packageVersionLookup | Format-Table -AutoSize


#########################################################################################################
#
#     Loop through the affected projects and make the Package Version update changes
#
#########################################################################################################

$madeChanges = $false
foreach ($CsprojPath in $projects) {
  if (Test-Path $CsprojPath) {
    foreach ($PackageName in $PackageNames) {
      $PackageVersion = $packageVersionLookup[$PackageName]
      $packageChange = Update-CsProjectNugetPackageReference `
        -CsprojPath $CsprojPath `
        -PackageName $PackageName `
        -PackageVersion $PackageVersion

      if ($packageChange) {
        Write-Host "Updated $PackageName to version $PackageVersion in $CsprojPath"
        $madeChanges = $true
        $packageVersionUpdates[$PackageName] += Split-Path -Leaf $CsprojPath
      }
      else {
        Write-Verbose "No changes to $CsprojPath for package $PackageName"
      }
    }
  }
  else {
    Write-Error "Project file not found: $CsprojPath"
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
    if ($priorChangeSourceDir -ne "" -and $WorkItemId -eq "") {
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

  $branchName = "maint/$branchPrefix-auto-update-eclipse-nugets-$BuildId"

  Write-Host "Branch name: $branchName"

  # create new branch
  Write-Host "Creating new branch..."
  git checkout -b $branchName

  git add .
  git status
  git commit -m "#$($branchPrefix): [Automated NuGet Maintenance] Update EI nuget package versions"

  Write-Host "Pushing branch to remote..."
  git push --set-upstream origin $branchName
  Write-Host "Creating Pull Request..."


  # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #
  #     Create PR using Azure DevOps REST API
  # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # # #
  Write-Host "Creating PR via REST API..."

  # Build package list for PR description
  $description = @"
[Automated NuGet Maintenance] Created by pipeline, ensures NuGet references are their latest versions.
**Build ID:** $BuildId

**Updated Packages:**
$packageListText

| Package Name | Version | Updated In Projects |
|--------------|---------|---------------------|
"@

  foreach ($PackageInfo in $packageVersionUpdates.GetEnumerator()) {
    $PackageName = $PackageInfo.Key
    $ProjectNames = $PackageInfo.Value
    $ProjectsList = if ($ProjectNames.Count -gt 0) { [string]::Join(', ', $ProjectNames) } else { 'N/A' }

    Write-Verbose "Package: $PackageName, Projects: $ProjectsList"

    $PackageVersion = $packageVersionLookup[$PackageName]

    $description += "`n| $PackageName | $PackageVersion | $ProjectsList |"
  }

  $prWebUrl = Submit-PullRequest `
    -RepositoryName $RepositoryName `
    -SourceBranch $branchName `
    -TargetBranch "main" `
    -Title "#$($branchPrefix): [Automated NuGet Maintenance] $($BuildId): Updating EI NuGet packages to latest versions" `
    -Description $description `
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