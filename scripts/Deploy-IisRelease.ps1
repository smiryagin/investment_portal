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
if (Test-Path -LiteralPath $target) {
    if ([string]::Equals($target, $previous, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Release $ReleaseId is already the active IIS release."
    }

    $existingTarget = Get-Item -LiteralPath $target
    if (($existingTarget.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to replace release target '$target' because it is a reparse point."
    }
    Remove-Item -LiteralPath $target -Recurse -Force
}

New-Item -ItemType Directory -Path $target | Out-Null
Expand-Archive -LiteralPath $package -DestinationPath $target
if (-not (Test-Path -LiteralPath (Join-Path $target 'web.config'))) {
    throw 'The release package does not contain web.config.'
}

$offlineFile = if (Test-Path -LiteralPath $previous) { Join-Path $previous 'app_offline.htm' } else { $null }

function Test-PortalHealth {
    param([uri] $Uri)
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 15
            if ($response.StatusCode -eq 200) { return $true }
        }
        catch {
            if ($attempt -eq 6) { return $false }
        }
        Start-Sleep -Seconds 2
    }
    return $false
}

try {
    if ($offlineFile) {
        Set-Content -LiteralPath $offlineFile -Value '<html><body><h1>WiseLine Trade is being updated.</h1></body></html>' -Encoding UTF8
        Start-Sleep -Seconds 2
    }

    Set-IisSitePhysicalPath -Name $SiteName -Path $target
    $poolState = (Get-WebAppPoolState -Name $AppPoolName).Value
    if ($poolState -eq 'Started') { Restart-WebAppPool -Name $AppPoolName } else { Start-WebAppPool -Name $AppPoolName }

    if (-not (Test-PortalHealth -Uri $HealthUrl)) {
        throw "Health check failed for release $ReleaseId."
    }

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
catch {
    if (Test-Path -LiteralPath $previous) {
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
