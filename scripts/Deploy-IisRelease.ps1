[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SiteName,
    [Parameter(Mandatory)] [string] $AppPoolName,
    [Parameter(Mandatory)] [string] $PackagePath,
    [Parameter(Mandatory)] [string] $ReleaseRoot,
    [Parameter(Mandatory)] [string] $ReleaseId,
    [Parameter(Mandatory)] [uri] $HealthUrl
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($ReleaseId -notmatch '^[a-fA-F0-9]{7,64}$') {
    throw 'ReleaseId must be a Git commit SHA.'
}

Import-Module WebAdministration -ErrorAction Stop

function Get-IisSitePhysicalPath {
    param([Parameter(Mandatory)] [string] $Name)

    $filter =
        "system.applicationHost/sites/site[@name='$Name']/application[@path='/']/virtualDirectory[@path='/']"
    $property = Get-WebConfigurationProperty `
        -PSPath 'MACHINE/WEBROOT/APPHOST' `
        -Filter $filter `
        -Name physicalPath
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string] $property.Value)) {
        throw "IIS site '$Name' does not have a root physical path."
    }

    return [Environment]::ExpandEnvironmentVariables([string] $property.Value)
}

function Set-IisSitePhysicalPath {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $Path
    )

    $filter =
        "system.applicationHost/sites/site[@name='$Name']/application[@path='/']/virtualDirectory[@path='/']"
    Set-WebConfigurationProperty `
        -PSPath 'MACHINE/WEBROOT/APPHOST' `
        -Filter $filter `
        -Name physicalPath `
        -Value $Path
}

$package = (Resolve-Path -LiteralPath $PackagePath).Path
$root = [System.IO.Path]::GetFullPath($ReleaseRoot)
if ([System.IO.Path]::GetPathRoot($root) -eq $root) {
    throw 'ReleaseRoot cannot be a drive root.'
}

New-Item -ItemType Directory -Path $root -Force | Out-Null
$target = [System.IO.Path]::GetFullPath((Join-Path $root $ReleaseId))
if (-not $target.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved release target is outside ReleaseRoot.'
}
$sitePath = "IIS:\Sites\$SiteName"
$poolPath = "IIS:\AppPools\$AppPoolName"
if (-not (Test-Path -LiteralPath $sitePath)) { throw "IIS site '$SiteName' does not exist." }
if (-not (Test-Path -LiteralPath $poolPath)) { throw "IIS app pool '$AppPoolName' does not exist." }

$previous = [System.IO.Path]::GetFullPath((Get-IisSitePhysicalPath -Name $SiteName))
$refreshActiveRelease = $false
if (Test-Path -LiteralPath $target) {
    if ([string]::Equals($target, $previous, [StringComparison]::OrdinalIgnoreCase)) {
        $refreshActiveRelease = $true
    }
    else {
        $existingTarget = Get-Item -LiteralPath $target
        if (($existingTarget.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to replace release target '$target' because it is a reparse point."
        }
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}

if (-not $refreshActiveRelease) {
    New-Item -ItemType Directory -Path $target | Out-Null
    Expand-Archive -LiteralPath $package -DestinationPath $target
    if (-not (Test-Path -LiteralPath (Join-Path $target 'web.config'))) {
        throw 'The release package does not contain web.config.'
    }
}

$offlineFile = if (-not $refreshActiveRelease -and (Test-Path -LiteralPath $previous)) {
    Join-Path $previous 'app_offline.htm'
}
else {
    $null
}
$curlCommand = (Get-Command curl.exe -ErrorAction Stop).Source

function Test-PortalHealth {
    param([uri] $Uri)

    $resolve = "{0}:{1}:127.0.0.1" -f $Uri.Host, $Uri.Port
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        $savedErrorActionPreference = $ErrorActionPreference
        try {
            # curl uses a non-zero exit code for transient HTTP failures such as
            # the 503 IIS returns while the application pool is warming up.
            # Capture that result so the retry loop can decide when to fail.
            $ErrorActionPreference = 'Continue'
            $curlOutput = & $curlCommand `
                --fail `
                --silent `
                --show-error `
                --max-time 15 `
                --noproxy '*' `
                --resolve $resolve `
                $Uri.AbsoluteUri 2>&1
            $curlExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $savedErrorActionPreference
        }

        if ($curlExitCode -eq 0) {
            return $true
        }

        $details = (($curlOutput | Out-String).Trim())
        if ($details.Length -gt 500) {
            $details = $details.Substring(0, 500)
        }
        Write-Warning (
            "Local IIS health attempt {0}/6 failed (curl exit {1}): {2}" -f `
                $attempt, $curlExitCode, $details)

        if ($attempt -eq 6) { return $false }
        Start-Sleep -Seconds 2
    }
    return $false
}

try {
    if ($offlineFile) {
        Set-Content -LiteralPath $offlineFile -Value '<html><body><h1>WiseLine Trade is being updated.</h1></body></html>' -Encoding UTF8
        Start-Sleep -Seconds 2
    }

    if (-not $refreshActiveRelease) {
        Set-IisSitePhysicalPath -Name $SiteName -Path $target
    }
    $poolState = (Get-WebAppPoolState -Name $AppPoolName).Value
    if ($poolState -eq 'Started') { Restart-WebAppPool -Name $AppPoolName } else { Start-WebAppPool -Name $AppPoolName }

    if (-not (Test-PortalHealth -Uri $HealthUrl)) {
        throw "Health check failed for release $ReleaseId."
    }

    if ($refreshActiveRelease) {
        Write-Output "Active release configuration refreshed successfully: $ReleaseId"
    }
    else {
        $stateDirectory = Join-Path $root '_state'
        New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
        [ordered]@{
            releaseId = $ReleaseId
            releasePath = $target
            previousPath = $previous
            deployedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stateDirectory 'last-deployment.json') -Encoding UTF8

        Write-Output "Deployment succeeded: $ReleaseId"
    }
}
catch {
    if (-not $refreshActiveRelease -and (Test-Path -LiteralPath $previous)) {
        Set-IisSitePhysicalPath -Name $SiteName -Path $previous
        $poolState = (Get-WebAppPoolState -Name $AppPoolName).Value
        if ($poolState -eq 'Started') { Restart-WebAppPool -Name $AppPoolName } else { Start-WebAppPool -Name $AppPoolName }
    }
    throw
}
finally {
    if ($offlineFile -and (Test-Path -LiteralPath $offlineFile)) {
        Remove-Item -LiteralPath $offlineFile -Force
    }
}
