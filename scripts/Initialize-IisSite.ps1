[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string] $SiteName,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string] $AppPoolName,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9.-]+$')]
    [string] $HostName,

    [Parameter(Mandatory)]
    [string] $ReleaseRoot,

    [Parameter(Mandatory)]
    [string] $ConfigPath,

    [ValidatePattern('^[A-Fa-f0-9]{40,64}$')]
    [string] $CertificateThumbprint = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$principal = [Security.Principal.WindowsPrincipal]::new(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated Windows PowerShell session.'
}

Import-Module WebAdministration -ErrorAction Stop

$hostingModuleCandidates = @(
    (Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'),
    (Join-Path ${env:ProgramFiles(x86)} 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll')
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
if (-not ($hostingModuleCandidates | Where-Object { Test-Path -LiteralPath $_ })) {
    throw 'ASP.NET Core Module V2 was not found. Install the .NET Hosting Bundle before provisioning IIS.'
}

function Resolve-DedicatedPath {
    param([Parameter(Mandatory)] [string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ([System.IO.Path]::GetPathRoot($fullPath) -eq $fullPath) {
        throw "Path '$Path' cannot be a drive root."
    }
    return $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
}

$releasePath = Resolve-DedicatedPath -Path $ReleaseRoot
$configurationPath = [System.IO.Path]::GetFullPath($ConfigPath)
$configurationDirectory = Resolve-DedicatedPath -Path ([System.IO.Path]::GetDirectoryName($configurationPath))
$bootstrapPath = Join-Path $releasePath '_bootstrap'

foreach ($directory in @($releasePath, $configurationDirectory, $bootstrapPath)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$bootstrapPage = Join-Path $bootstrapPath 'index.html'
if (-not (Test-Path -LiteralPath $bootstrapPage)) {
    Set-Content -LiteralPath $bootstrapPage -Encoding UTF8 -Value @'
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>WiseLine Portal staging</title></head>
<body><h1>WiseLine Portal staging is awaiting its first deployment.</h1></body></html>
'@
}

$poolPath = "IIS:\AppPools\$AppPoolName"
if (-not (Test-Path -LiteralPath $poolPath)) {
    New-WebAppPool -Name $AppPoolName | Out-Null
}
Set-ItemProperty -LiteralPath $poolPath -Name managedRuntimeVersion -Value ''
Set-ItemProperty -LiteralPath $poolPath -Name managedPipelineMode -Value Integrated
Set-ItemProperty -LiteralPath $poolPath -Name processModel.identityType -Value ApplicationPoolIdentity
Set-ItemProperty -LiteralPath $poolPath -Name processModel.loadUserProfile -Value $true
Set-ItemProperty -LiteralPath $poolPath -Name startMode -Value AlwaysRunning

$sitePath = "IIS:\Sites\$SiteName"
if (-not (Test-Path -LiteralPath $sitePath)) {
    New-Website `
        -Name $SiteName `
        -PhysicalPath $bootstrapPath `
        -ApplicationPool $AppPoolName `
        -Port 80 `
        -HostHeader $HostName | Out-Null
}
else {
    Set-ItemProperty -LiteralPath $sitePath -Name applicationPool -Value $AppPoolName
}

$httpBinding = "*:80:$HostName"
if (-not (Get-WebBinding -Name $SiteName -Protocol http | Where-Object bindingInformation -eq $httpBinding)) {
    New-WebBinding -Name $SiteName -Protocol http -Port 80 -HostHeader $HostName | Out-Null
}

Set-WebConfigurationProperty `
    -PSPath 'MACHINE/WEBROOT/APPHOST' `
    -Filter "system.applicationHost/sites/site[@name='$SiteName']/application[@path='/']" `
    -Name preloadEnabled `
    -Value $true

$environmentFilter =
    "system.applicationHost/applicationPools/add[@name='$AppPoolName']/environmentVariables"
$configVariable = Get-WebConfigurationProperty `
    -PSPath 'MACHINE/WEBROOT/APPHOST' `
    -Filter $environmentFilter `
    -Name '.' |
    Where-Object name -eq 'WISELINE_PORTAL_CONFIG_FILE'
if ($configVariable) {
    Set-WebConfigurationProperty `
        -PSPath 'MACHINE/WEBROOT/APPHOST' `
        -Filter "$environmentFilter/add[@name='WISELINE_PORTAL_CONFIG_FILE']" `
        -Name value `
        -Value $configurationPath
}
else {
    Add-WebConfigurationProperty `
        -PSPath 'MACHINE/WEBROOT/APPHOST' `
        -Filter $environmentFilter `
        -Name '.' `
        -Value @{ name = 'WISELINE_PORTAL_CONFIG_FILE'; value = $configurationPath }
}

$appPoolIdentity = "IIS AppPool\$AppPoolName"
$readGrant = "${appPoolIdentity}:(OI)(CI)(RX)"
foreach ($directory in @($releasePath, $configurationDirectory)) {
    & icacls.exe $directory /grant $readGrant | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to grant the app pool access to '$directory'." }
}

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $certificate = Get-Item -LiteralPath "Cert:\LocalMachine\My\$CertificateThumbprint" -ErrorAction Stop
    if (-not $certificate.HasPrivateKey) { throw 'The selected TLS certificate has no private key.' }

    $httpsBinding = "*:443:$HostName"
    if (-not (Get-WebBinding -Name $SiteName -Protocol https | Where-Object bindingInformation -eq $httpsBinding)) {
        New-WebBinding `
            -Name $SiteName `
            -Protocol https `
            -Port 443 `
            -HostHeader $HostName `
            -SslFlags 1 | Out-Null
    }

    $httpsBindingObject = Get-WebBinding -Name $SiteName -Protocol https |
        Where-Object bindingInformation -eq $httpsBinding
    $httpsBindingObject.AddSslCertificate($certificate.Thumbprint, 'My')
}
else {
    Write-Warning 'HTTPS was not configured. Import a certificate and rerun with -CertificateThumbprint.'
}

$poolState = (Get-WebAppPoolState -Name $AppPoolName).Value
if ($poolState -eq 'Started') { Restart-WebAppPool -Name $AppPoolName } else { Start-WebAppPool -Name $AppPoolName }
$siteState = (Get-WebsiteState -Name $SiteName).Value
if ($siteState -ne 'Started') { Start-Website -Name $SiteName }

Write-Output "IIS site '$SiteName' is ready at '$bootstrapPath'."
Write-Output "External configuration path: $configurationPath"
