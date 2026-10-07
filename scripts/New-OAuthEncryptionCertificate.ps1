[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string] $AppPoolName,
    [string] $EnvironmentName = 'Staging',
    [ValidateRange(1, 5)] [int] $ValidYears = 2
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$administrator = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent()
)
if (-not $administrator.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell session on the IIS server.'
}

$subject = "CN=WiseLine Portal OAuth Encryption $EnvironmentName"
$certificate = Get-ChildItem Cert:\LocalMachine\My |
    Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $certificate) {
    if (-not $PSCmdlet.ShouldProcess($subject, 'Create non-exportable OAuth encryption certificate')) {
        return
    }

    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -CertStoreLocation 'Cert:\LocalMachine\My' `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -KeyUsage KeyEncipherment `
        -KeyExportPolicy NonExportable `
        -NotAfter (Get-Date).AddYears($ValidYears)
}

$rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
if ($rsa -is [Security.Cryptography.RSACng]) {
    $keyPath = Join-Path $env:ProgramData "Microsoft\Crypto\Keys\$($rsa.Key.UniqueName)"
}
elseif ($rsa -is [Security.Cryptography.RSACryptoServiceProvider]) {
    $keyName = $rsa.CspKeyContainerInfo.UniqueKeyContainerName
    $keyPath = Join-Path $env:ProgramData "Microsoft\Crypto\RSA\MachineKeys\$keyName"
}
else {
    throw 'The encryption certificate private-key provider is not supported by this script.'
}

if (-not (Test-Path -LiteralPath $keyPath -PathType Leaf)) {
    throw "The encryption private-key file was not found at '$keyPath'."
}

$identity = "IIS AppPool\$AppPoolName"
$acl = Get-Acl -LiteralPath $keyPath
$rule = New-Object Security.AccessControl.FileSystemAccessRule(
    $identity,
    [Security.AccessControl.FileSystemRights]::Read,
    [Security.AccessControl.AccessControlType]::Allow
)
$acl.SetAccessRule($rule)
Set-Acl -LiteralPath $keyPath -AclObject $acl

[pscustomobject]@{
    Subject = $certificate.Subject
    Thumbprint = $certificate.Thumbprint
    NotAfter = $certificate.NotAfter
    AppPoolIdentity = $identity
}
