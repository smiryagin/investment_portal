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
            # connection failures. Keep the HTTP response body and status code
            # so an IIS/ANCM startup error is visible in the deployment log.
            $ErrorActionPreference = 'Continue'
            $curlOutput = & $curlCommand `
                --silent `
                --show-error `
                --max-time 15 `
                --noproxy '*' `
                --resolve $resolve `
                --write-out "`n__WISELINE_HTTP_STATUS__:%{http_code}" `
                $Uri.AbsoluteUri 2>&1
            $curlExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $savedErrorActionPreference
        }

        $details = (($curlOutput | Out-String).Trim())
        $statusMatch = [regex]::Match($details, '(?m)^__WISELINE_HTTP_STATUS__:(\d{3})$')
        $statusCode = if ($statusMatch.Success) { [int] $statusMatch.Groups[1].Value } else { 0 }
        $details = [regex]::Replace(
            $details,
            '(?m)^__WISELINE_HTTP_STATUS__:\d{3}$',
            '').Trim()

        if ($curlExitCode -eq 0 -and $statusCode -eq 200) {
            return $true
        }

        if ($details.Length -gt 500) {
            $details = $details.Substring(0, 500)
        }
        Write-Warning (
            "Local IIS health attempt {0}/6 failed (HTTP {1}, curl exit {2}): {3}" -f `
                $attempt, $statusCode, $curlExitCode, $details)

        if ($attempt -eq 6) { return $false }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Write-PortalStartupDiagnostics {
    param([Parameter(Mandatory)] [string] $ReleasePath)

    Write-Warning 'Recent IIS/ASP.NET Core startup diagnostics follow.'
    $events = Get-WinEvent `
        -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddMinutes(-10) } `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProviderName -match 'AspNetCore|\.NET Runtime|Application Error' -and
            ($_.Message -match 'WiseLine\.Portal|aspnetcore|500\.3|startup|certificate|OpenIddict' -or
             $_.Message -like "*$ReleasePath*")
        } |
        Select-Object -First 10

    if (-not $events) {
        Write-Warning 'No matching Application event-log entries were found.'
        return
    }

    foreach ($event in $events) {
        $message = ([string] $event.Message).Trim()
        if ($message.Length -gt 2000) {
            $message = $message.Substring(0, 2000)
        }
        Write-Warning (
            "[{0:u}] {1} event {2}: {3}" -f `
                $event.TimeCreated, $event.ProviderName, $event.Id, $message)
    }
}

function Write-PortalStdoutDiagnostics {
    param(
        [Parameter(Mandatory)] [string] $ReleasePath,
        [Parameter(Mandatory)] [string] $PoolName,
        [Parameter(Mandatory)] [uri] $Uri
    )

    try {
        $webConfigPath = Join-Path $ReleasePath 'web.config'
        $diagnosticsPath = Join-Path $ReleasePath '_diagnostics'
        New-Item -ItemType Directory -Path $diagnosticsPath -Force | Out-Null

        & icacls.exe $diagnosticsPath /grant:r "IIS AppPool\${PoolName}:(OI)(CI)M" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not grant the app pool access to '$diagnosticsPath'."
        }

        [xml] $webConfig = Get-Content -LiteralPath $webConfigPath -Raw
        $aspNetCore = $webConfig.SelectSingleNode('//aspNetCore')
        if ($null -eq $aspNetCore) {
            throw 'The deployed web.config does not contain an aspNetCore element.'
        }

        $aspNetCore.SetAttribute('stdoutLogEnabled', 'true')
        $aspNetCore.SetAttribute('stdoutLogFile', '.\_diagnostics\stdout')
        $webConfig.Save($webConfigPath)

        Write-Warning 'Restarting the failed release once with bounded ANCM stdout capture enabled.'
        $poolState = (Get-WebAppPoolState -Name $PoolName).Value
        if ($poolState -eq 'Started') {
            Restart-WebAppPool -Name $PoolName
        }
        else {
            Start-WebAppPool -Name $PoolName
        }

        [void] (Test-PortalHealth -Uri $Uri)
        Start-Sleep -Seconds 1

        $stdoutLog = Get-ChildItem -LiteralPath $diagnosticsPath -Filter 'stdout*.log' -File `
            -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($null -eq $stdoutLog) {
            Write-Warning 'ANCM stdout capture did not produce a log file.'
            return
        }

        $content = (Get-Content -LiteralPath $stdoutLog.FullName -Raw).Trim()
        if ($content.Length -gt 12000) {
            $content = $content.Substring($content.Length - 12000)
        }
        Write-Warning "Captured ANCM stdout from the failed release:`n$content"
    }
    catch {
        Write-Warning "Could not capture ANCM stdout diagnostics: $($_.Exception.Message)"
    }
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
        Write-PortalStartupDiagnostics -ReleasePath $target
        Write-PortalStdoutDiagnostics `
            -ReleasePath $target `
            -PoolName $AppPoolName `
            -Uri $HealthUrl
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
