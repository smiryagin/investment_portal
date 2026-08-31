[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SiteName,
    [Parameter(Mandatory)] [string] $AppPoolName,
    [Parameter(Mandatory)] [string] $ReleaseRoot,
    [Parameter(Mandatory)] [string] $ReleaseId,
    [Parameter(Mandatory)] [uri] $HealthUrl
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($ReleaseId -notmatch '^[a-fA-F0-9]{7,64}$') { throw 'ReleaseId must be a Git commit SHA.' }
Import-Module WebAdministration -ErrorAction Stop

$root = [System.IO.Path]::GetFullPath($ReleaseRoot)
$target = [System.IO.Path]::GetFullPath((Join-Path $root $ReleaseId))
if (-not $target.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved rollback target is outside ReleaseRoot.'
}
if (-not (Test-Path -LiteralPath (Join-Path $target 'web.config'))) {
    throw "Release '$ReleaseId' is not present or is incomplete."
}

$sitePath = "IIS:\Sites\$SiteName"
Set-ItemProperty -LiteralPath $sitePath -Name physicalPath -Value $target
$poolState = (Get-WebAppPoolState -Name $AppPoolName).Value
if ($poolState -eq 'Started') { Restart-WebAppPool -Name $AppPoolName } else { Start-WebAppPool -Name $AppPoolName }

$healthy = $false
for ($attempt = 1; $attempt -le 6; $attempt++) {
    try {
        if ((Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 15).StatusCode -eq 200) {
            $healthy = $true
            break
        }
    }
    catch { }
    Start-Sleep -Seconds 2
}

if (-not $healthy) { throw "Rollback target '$ReleaseId' failed its health check." }
Write-Output "Rollback succeeded: $ReleaseId"
