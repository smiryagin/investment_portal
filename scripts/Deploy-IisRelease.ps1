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
if (Test-Path -LiteralPath $target) {
    throw "Release $ReleaseId already exists."
}

$sitePath = "IIS:\Sites\$SiteName"
$poolPath = "IIS:\AppPools\$AppPoolName"
if (-not (Test-Path -LiteralPath $sitePath)) { throw "IIS site '$SiteName' does not exist." }
if (-not (Test-Path -LiteralPath $poolPath)) { throw "IIS app pool '$AppPoolName' does not exist." }

New-Item -ItemType Directory -Path $target | Out-Null
Expand-Archive -LiteralPath $package -DestinationPath $target
if (-not (Test-Path -LiteralPath (Join-Path $target 'web.config'))) {
    throw 'The release package does not contain web.config.'
}

$previous = [Environment]::ExpandEnvironmentVariables((Get-ItemProperty -LiteralPath $sitePath -Name physicalPath).physicalPath)
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

    Set-ItemProperty -LiteralPath $sitePath -Name physicalPath -Value $target
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
        Set-ItemProperty -LiteralPath $sitePath -Name physicalPath -Value $previous
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
