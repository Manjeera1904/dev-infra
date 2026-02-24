function Invoke-RestMethodWithRetry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,

        [Parameter(Mandatory = $false)]
        [hashtable]$Headers,

        [Parameter(Mandatory = $false)]
        [string]$Method = "GET",

        [Parameter(Mandatory = $false)]
        [string]$Body,

        [Parameter(Mandatory = $false)]
        [int]$MaxRetries = 3
    )

    $retryDelays = @(5, 30, 60)  # Delays in seconds for each retry attempt
    $attempt = 0

    while ($attempt -lt $MaxRetries) {
        try {
            $params = @{
                Uri = $Uri
                Method = $Method
            }
            
            if ($Headers) {
                $params.Headers = $Headers
            }
            
            if ($Body) {
                $params.Body = $Body
            }

            Write-Verbose "REST API call attempt $($attempt + 1) to: $Uri"
            return Invoke-RestMethod @params -ErrorAction Stop
        }
        catch {
            $attempt++
            
            if ($attempt -ge $MaxRetries) {
                Write-Verbose "REST API call failed after $MaxRetries attempts to: $Uri"
                throw
            }
            
            $delay = $retryDelays[$attempt - 1]
            Write-Verbose "REST API call attempt $attempt failed for: $Uri. Retrying in $delay seconds. Error: $($_.Exception.Message)"
            Start-Sleep -Seconds $delay
        }
    }
}

function Get-LatestNuGetPackageVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageName,

        [Parameter(Mandatory = $false)]
        [string]$Token,

        [Parameter(Mandatory = $false)]
        [string]$FeedName = "eclipse-insights-nuget",

        [Parameter(Mandatory = $false)]
        [string]$Organization = "https://dev.azure.com/EclipseInsightsHC/",
    
        [Parameter(Mandatory = $false)]
        [string]$Project = "Commercialization"
    )

    try {
        Write-Verbose "Retrieving latest version for package: $PackageName"
        
        # Get token from parameter or environment variable
        if ([string]::IsNullOrWhiteSpace($Token)) {
            $Token = $env:AZURE_DEVOPS_TOKEN
            if ([string]::IsNullOrWhiteSpace($Token)) {
                throw "Azure DevOps token not provided via parameter or AZURE_DEVOPS_TOKEN environment variable"
            }
            Write-Verbose "Using token from environment variable"
        }
        else {
            Write-Verbose "Using token from parameter"
        }
        
        # Prepare headers for authentication
        $headers = @{ 
            'Authorization' = "Bearer $Token"
            'Content-Type'  = 'application/json'
        }

        # Azure DevOps feed URI uses a different base than other REST APIs
        # Organization URI is like https://dev.azure.com/EclipseInsightsHC/
        # Feed URI needs to be like https://feeds.dev.azure.com/EclipseInsightsHC/
        $feedUri = $Organization.Replace("://", "://feeds.")
        
        # Construct the API query URL
        $pkgVerQuery = "$feedUri$Project/_apis/packaging/Feeds/$FeedName/packages?api-version=7.2-preview.1&packageNameQuery=$PackageName"
        
        Write-Verbose "Package Version Query: $pkgVerQuery"
        
        # Make the API call
        $response = Invoke-RestMethodWithRetry -Uri $pkgVerQuery -Headers $headers
        
        # Extract the latest version
        $latestVersion = $response.value.versions.version
        
        if ([string]::IsNullOrWhiteSpace($latestVersion)) {
            throw "No version information found for package '$PackageName' in feed '$FeedName'"
        }
        
        Write-Verbose "Latest version of $PackageName is $latestVersion"
        return $latestVersion
    }
    catch {
        Write-Host "---- Exception Details: -------------------------------"
        Write-Host "Exception Type: $($_.Exception.GetType().FullName)"
        Write-Host "Exception Message: $($_.Exception.Message)"
        if ($_.Exception.InnerException) {
            Write-Host "Inner Exception: $($_.Exception.InnerException.Message)"
        }
        Write-Host "-------------------------------------------------------"
        $errorMessage = "Failed to retrieve latest version for package '$PackageName': $($_.Exception.Message)"
        Write-Error $errorMessage
        throw $errorMessage
    }
}

function Test-NuGetPackageVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageName,

        [Parameter(Mandatory = $true)]
        [string]$PackageVersion,

        [Parameter(Mandatory = $false)]
        [string]$Token,

        [Parameter(Mandatory = $false)]
        [string]$FeedName = "eclipse-insights-nuget",

        [Parameter(Mandatory = $false)]
        [string]$Organization = "https://dev.azure.com/EclipseInsightsHC/",
    
        [Parameter(Mandatory = $false)]
        [string]$Project = "Commercialization"
    )

    try {
        Write-Verbose "Validating package version: $PackageName v$PackageVersion"
        
        $headers = @{ 
            'Authorization' = "Bearer $Token"
            'Content-Type'  = 'application/json'
        }

        $feedUri = $Organization.Replace("://", "://pkgs.")
        $pkgQuery = "$feedUri$Project/_apis/packaging/Feeds/$FeedName/nuget/packages/$PackageName/versions/$PackageVersion`?api-version=7.2-preview.1"
        Write-Verbose "Package Query: $pkgQuery"
        
        try {
            $response = Invoke-RestMethodWithRetry -Uri $pkgQuery -Headers $headers
            Write-Verbose "Package version $PackageVersion exists for $PackageName (response: $($response.name)/$($response.version))"
            return $true
        }
        catch {
            if ($_.Exception.Response.StatusCode -eq 404) {
                Write-Verbose "Package version $PackageVersion not found for $PackageName"
                return $false
            }
            throw
        }
    }
    catch {
        Write-Host "---- Exception Details: -------------------------------"
        Write-Host "Exception Type: $($_.Exception.GetType().FullName)"
        Write-Host "Exception Message: $($_.Exception.Message)"
        if ($_.Exception.InnerException) {
            Write-Host "Inner Exception: $($_.Exception.InnerException.Message)"
        }
        Write-Host "-------------------------------------------------------"
        $errorMessage = "Failed to validate package version '$PackageName' version $PackageVersion - $($_.Exception.Message)"
        Write-Error $errorMessage
        throw $errorMessage
    }
}

function Set-RepositoryWorkingDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourcesDirectory,

        [Parameter(Mandatory = $true)]
        [string] $RepositoryName,

        [Parameter(Mandatory = $true)]
        [string] $TestPath
    )

    Set-Location $SourcesDirectory

    if (Test-Path $TestPath) {
        Write-Verbose "Already in $RepositoryName working directory (tested via $TestPath)"
        return $true
    }

    if (Test-Path "$SourcesDirectory/$RepositoryName") {
        Write-Verbose "Changing to repository directory: $SourcesDirectory/$RepositoryName"
        Set-Location $SourcesDirectory/$RepositoryName
        return $true
    }

    Write-Verbose "SourcesDirectory: $SourcesDirectory"
    Write-Verbose "Current directory: $(Get-Item . | Select-Object -ExpandProperty FullName)"
    Write-Verbose "Current directory contents:"
    Get-ChildItem -Path "./.azure-pipelines" -Recurse -Depth 3 | ForEach-Object { Write-Verbose $_.FullName }
    Get-ChildItem -Path "." -Recurse -Depth 3 | ForEach-Object { Write-Verbose $_.FullName }
    if (Test-Path "./README.md") {
        Get-Content "./README.md"
    }
      
    Write-Error "Could not find $RepositoryName folder under SourcesDirectory $SourcesDirectory"
    return $false
}

function Update-CsProjectNugetPackageReference {    
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $CsprojPath,

        [Parameter(Mandatory = $true)]
        [string] $PackageName,

        [Parameter(Mandatory = $true)]
        [string] $PackageVersion
    )

    $madeChanges = $false

    if (-Not (Test-Path $CsprojPath)) {
        Write-Error "Project file not found: $CsprojPath"
        return $false
    }

    $csprojFile = Get-Item $CsprojPath
    [xml]$csproj = Get-Content $CsprojPath

    $nsMgr = New-Object System.Xml.XmlNamespaceManager -ArgumentList $csproj.NameTable
    $nsMgr.AddNamespace("ns", $csproj.Project.NamespaceURI)

    $packageRef = $csproj.SelectSingleNode("//ns:PackageReference[@Include='$PackageName']", $nsMgr)

    if ($packageRef) {
        $currVer = $packageRef.GetAttribute("Version")
        if ($currVer -ne $PackageVersion) {
            $packageRef.SetAttribute("Version", $PackageVersion)
            Write-Verbose "Updated $PackageName to version $PackageVersion in $CsprojPath (was $($packageRef.Version))"

            $fullpath = $csprojFile.FullName
            Write-Verbose "Saving updated project file to $fullpath"
            $csproj.Save($fullPath)

            $madeChanges = $true
        }
        else {
            Write-Verbose "$PackageName is already at version $PackageVersion in $CsprojPath"
        }
    }
    else {
        Write-Verbose "PackageReference for $PackageName not found in $CsprojPath"
    }


    return $madeChanges
}

function Update-GitConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string] $UserName = "Azure DevOps Build Agent",

        [Parameter(Mandatory = $false)]
        [string] $UserEmail = "buildagent@eclipseinsightshc.com"
    )

    try {
        git config --global user.name $UserName
        git config --global user.email $UserEmail
        Write-Verbose "Git global config updated: user.name=$UserName, user.email=$UserEmail"
        return $true
    }
    catch {
        Write-Host "---- Exception Details: -------------------------------"
        Write-Host "Exception Type: $($_.Exception.GetType().FullName)"
        Write-Host "Exception Message: $($_.Exception.Message)"
        if ($_.Exception.InnerException) {
            Write-Host "Inner Exception: $($_.Exception.InnerException.Message)"
        }
        Write-Host "-------------------------------------------------------"
        $errorMessage = "Failed to update Git config - $($_.Exception.Message)"
        Write-Error $errorMessage
        throw $errorMessage
    }
}

function Get-LatestWorkItemDetails {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoName,

        [Parameter(Mandatory = $true)]
        [string]$ProjectFolder,

        [Parameter(Mandatory = $false)]
        [string]$Token,

        [Parameter(Mandatory = $false)]
        [string]$Organization = "https://dev.azure.com/EclipseInsightsHC/",
    
        [Parameter(Mandatory = $false)]
        [string]$Project = "Commercialization"
    )

    Write-Verbose "Trying to determine WorkItem ID from most recent commit"

    $headers = @{ 'Authorization' = "Bearer $Token" }
    $commitQuery = "$Organization$Project/_apis/git/repositories/$RepoName/commits?searchCriteria.itemPath=$ProjectFolder&searchCriteria.`$top=1&searchCriteria.includeWorkItems=true&api-version=7.2-preview.2"
    Write-Verbose "Commit Query: $commitQuery"

    $commits = Invoke-RestMethodWithRetry -Uri $commitQuery -Headers $headers

    $workItem = $null

    if ($commits.count -gt 0) {
        $commit = $commits.value[0]
        $workItems = $commits.value[0].workItems

        if ($workItems.length -gt 0) {
            $WorkItemId = $workItems[0].id
            $WorkItemUrl = $workItems[0].url
            $WorkItemUser = $commit.committer.email
            foreach ($wi in $workItems) {
                Write-Verbose "Work Item ID: $($wi.id) by $($wi.committer.email)"
            }

            return @{
                Id   = $WorkItemId
                Url  = $WorkItemUrl
                User = $WorkItemUser
            }
        }
        else {
            Write-Verbose "No Work Items associated with $($commits.value[0].commitId)"
        }
    }
    else {
        Write-Verbose "No commits found affecting $RepoName folder $ProjectFolder"
    }

    return $null
}

function Get-WorkItemDetails {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkItemId,

        [Parameter(Mandatory = $false)]
        [string]$Token,

        [Parameter(Mandatory = $false)]
        [string]$Organization = "https://dev.azure.com/EclipseInsightsHC/",

        [Parameter(Mandatory = $false)]
        [string]$Project = "Commercialization"
    )

    Write-Verbose "Trying to retrieve Work Item details for folder $ProjectFolder"

    $headers = @{ 'Authorization' = "Bearer $Token" }
    $workItemQuery = "$Organization$Project/_apis/wit/workitems/$WorkItemId`?`$expand=relations&api-version=7.2-preview.2"
    Write-Verbose "Work Item Query: $workItemQuery"
    
    try {
        $workItem = Invoke-RestMethodWithRetry -Uri $workItemQuery -Headers $headers
        
        $WorkItemId = $WorkItemId
        $WorkItemUrl = $workItem.url
        $WorkItemAssignedTo = $workItem.fields.PSObject.Properties["System.AssignedTo"].Value

        # Check if it's an object with email properties
        if ($WorkItemAssignedTo.PSObject.Properties['uniqueName']) {
            Write-Verbose "AssignedTo is an object with uniqueName property"
            $WorkItemUser = $WorkItemAssignedTo.uniqueName
        }
        elseif ($WorkItemAssignedTo.PSObject.Properties['emailAddress']) {
            Write-Verbose "AssignedTo is an object with emailAddress property"
            $WorkItemUser = $WorkItemAssignedTo.emailAddress
        }
        elseif ($WorkItemAssignedTo.PSObject.Properties['mailAddress']) {
            Write-Verbose "AssignedTo is an object with mailAddress property"
            $WorkItemUser = $WorkItemAssignedTo.mailAddress
        }
        # If it's a string, try to extract email
        elseif ($WorkItemAssignedTo -is [string]) {
            Write-Verbose "AssignedTo is a string: $WorkItemAssignedTo"
            # Try to extract email from "Display Name <email@domain.com>" format
            if ($WorkItemAssignedTo -match '<(.+?)>') {
                $WorkItemUser = $matches[1]
                Write-Verbose "Extracted email from angle brackets: $WorkItemUser"
            }
            # Check if the string itself is an email
            elseif ($WorkItemAssignedTo -match '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$') {
                $WorkItemUser = $WorkItemAssignedTo
                Write-Verbose "AssignedTo is a valid email: $WorkItemUser"
            }
        }

        if ($null -ne $WorkItemUrl -and $null -ne $WorkItemUser) {
            Write-Verbose "Found Work Item ID: $WorkItemId assigned to $WorkItemUser"
            return @{
                Id   = $WorkItemId
                Url  = $WorkItemUrl
                User = $WorkItemUser
            }
        }
        else {
            Write-Verbose "Work Item ID: $WorkItemId found but missing URL or AssignedTo"
            Write-Verbose $workItem.GetType().FullName
            Write-Verbose $workItem | Out-String
            Write-Verbose ">>>>       id: $($workItem.id)"
            Write-Verbose ">>>>      url: $($workItem.url)"
            Write-Verbose ">>>>   fields: $($workItem.fields)"
            Write-Verbose ">>>> fields-1: $($workItem.fields['System.AreaPath'])"
            Write-Verbose ">>>> fields-2: $($workItem.fields.PSObject.Properties['System.AssignedTo'].Value)"
            Write-Verbose ">>>> fields-2: $($workItem.fields.PSObject.Properties['System.AssignedTo'].Value.GetType().FullName)"
        }
    }
    catch {
        Write-Host "---- Exception Details: -------------------------------"
        Write-Host "Exception Type: $($_.Exception.GetType().FullName)"
        Write-Host "Exception Message: $($_.Exception.Message)"
        if ($_.Exception.InnerException) {
            Write-Host "Inner Exception: $($_.Exception.InnerException.Message)"
        }
        Write-Host "-------------------------------------------------------"
        $errorMessage = "Failed to retrieve work item details: $($_.Exception.Message)"
        Write-Error $errorMessage
        throw $errorMessage
    }

    return $null
}

function Submit-PullRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryName,

        [Parameter(Mandatory = $true)]
        [string]$SourceBranch,

        [Parameter(Mandatory = $true)]
        [string]$TargetBranch,

        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $false)]
        [string]$Description = "",

        [Parameter(Mandatory = $false)]
        [string]$AssignedTo = "",

        [Parameter(Mandatory = $false)]
        [int]$WorkItemId,

        [Parameter(Mandatory = $false)]
        [string]$WorkItemUrl,

        [Parameter(Mandatory = $true)]
        [string]$Token,

        [Parameter(Mandatory = $false)]
        [string]$Organization = "https://dev.azure.com/EclipseInsightsHC/",

        [Parameter(Mandatory = $false)]
        [string]$Project = "Commercialization"
    )

    try {
        $headers = @{
            'Authorization' = "Bearer $Token"
            "Content-Type"  = "application/json"
        }

        $bodyObj = @{
            sourceRefName = "refs/heads/$SourceBranch"
            targetRefName = "refs/heads/$TargetBranch"
            title         = $Title
            description   = $Description
        } 

        if ($null -ne $WorkItemId -and $WorkItemId -ne '' -and $WorkItemUrl -ne '') {
            $bodyObj.workItemRefs = @(@{ id = $WorkItemId; url = $WorkItemUrl })
        }

        if ($AssignedTo -ne '') {
            # need to use the Graph API to look up the user by email and get their descriptor
            # Unhelpfully, the vssps URI has a different base than the other REST APIs:
            #   Organization URI is like https://dev.azure.com/EclipseInsightsHC/
            #   .......but the feed URI is like https://vssps.dev.azure.com/EclipseInsightsHC/
            $userUri = $Organization.Replace("://", "://vssps.")
            $userQuery = "$($userUri)_apis/identities?searchFilter=MailAddress&filterValue=$AssignedTo&api-version=7.2-preview.1"
            Write-Verbose "User Query: $userQuery"
            $userIdentity = Invoke-RestMethodWithRetry -Uri $userQuery -Headers $headers

            $prReviewerDescriptor = $null
            if ($userIdentity.count -gt 0) {
                $prReviewerDescriptor = $userIdentity.value[0].id
                Write-Verbose "Found user descriptor: $prReviewerDescriptor"
                $bodyObj.reviewers = @(@{ id = $prReviewerDescriptor })
            }
            else {
                Write-Verbose "Could not find user descriptor for $AssignedTo"
            }
        }

  
        $body = $($bodyObj | ConvertTo-Json -Depth 3)

        $prPostUri = "$Organization$Project/_apis/git/repositories/$RepositoryName/pullrequests?api-version=7.1-preview.1"
        Write-Verbose "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -"
        Write-Verbose "PR Post URI: $prPostUri"
        Write-Verbose "PR Body: $body"
        Write-Verbose "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -"

        $prInfo = Invoke-RestMethodWithRetry `
            -Uri $prPostUri `
            -Method Post `
            -Headers $headers `
            -Body $body

        Write-Verbose "Created PR ID: $($prInfo.pullRequestId)"

        # Update the PR to Auto-Complete if possible
        if ($false) {
        # TODO: Get this working....
        # if ($null -ne $prReviewerDescriptor) {
            try {
                $profileUrl = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=7.2-preview.3"
                $selfProfile = Invoke-RestMethodWithRetry -Uri $profileUrl -Headers $headers
                Write-Verbose "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -"
                Write-Verbose "Current user profile: $($selfProfile.displayName) <$($selfProfile.emailAddress)>"
                Write-Verbose "Current user ID: $($selfProfile.id)"
                Write-Verbose "Full Profile: $($selfProfile | ConvertTo-Json -Depth 5)"
                Write-Verbose "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -"

                Write-Verbose "Setting PR to Auto-Complete as user $AssignedTo"
                $bodyObj = @{
                    autoCompleteSetBy = @{ id = $prReviewerDescriptor }
                    completionOptions = @{
                        "deleteSourceBranch" = $true
                        "squashMerge"        = $true
                    }
                }
        
                $body = $($bodyObj | ConvertTo-Json -Depth 5)

                $autoCompleteUrl = "$Organization$Project/_apis/git/repositories/$RepositoryName/pullrequests/$($prInfo.pullRequestId)?api-version=7.1-preview.1"
                Write-Verbose "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -"
                Write-Verbose "Auto-Complete URL: $autoCompleteUrl"
                Write-Verbose "Auto-Complete Body: $body"
                Write-Verbose "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -"

                Write-Verbose "...waiting 5 seconds for DevOps to catch up..."
                Start-Sleep -Seconds 5
                Write-Verbose "...continuing to PATCH PR to enable auto-complete..."

                $prInfo = Invoke-RestMethodWithRetry `
                    -Uri $autoCompleteUrl `
                    -Method Patch `
                    -Headers $headers `
                    -Body $body
            }
            catch {
                Write-Host "Failed to set auto-complete on PR - $($_.Exception.Message)"
            }
        }

        $prWebUrl = "$Organization$Project/_git/$RepositoryName/pullrequest/$($prInfo.pullRequestId)"
        return $prWebUrl
    }
    catch {
        $errorMessage = "Failed to create Pull Request - $($_.Exception.Message)"
        Write-Host "---- Exception Details: -------------------------------"
        Write-Host "Exception Type: $($_.Exception.GetType().FullName)"
        Write-Host "Exception Message: $($_.Exception.Message)"
        if ($_.Exception.InnerException) {
            Write-Host "Inner Exception: $($_.Exception.InnerException.Message)"
        }
        Write-Host "-------------------------------------------------------"
        Write-Error $errorMessage
        throw $errorMessage
    }
}

# Export the functions
Export-ModuleMember -Function                   `
    Get-LatestNuGetPackageVersion               `
    , Get-LatestWorkItemDetails                 `
    , Get-WorkItemDetails                       `
    , Submit-PullRequest                        `
    , Test-NuGetPackageVersion                  `
    , Set-RepositoryWorkingDirectory            `
    , Update-CsProjectNugetPackageReference     `
    , Update-GitConfig