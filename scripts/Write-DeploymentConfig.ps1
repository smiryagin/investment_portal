[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ConfigPath,
    [Parameter(Mandatory)] [string] $AppPoolName
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-RequiredEnvironmentValue {
    param([string] $Name)
    $value = [Environment]::GetEnvironmentVariable($Name, 'Process')
    if ([string]::IsNullOrWhiteSpace($value)) { throw "Required deployment secret '$Name' is missing." }
    return $value
}

function Get-EnvironmentValue {
    param([string] $Name)
    $value = [Environment]::GetEnvironmentVariable($Name, 'Process')
    if ($null -eq $value) { return '' }
    return $value
}

$fullPath = [System.IO.Path]::GetFullPath($ConfigPath)
$directory = [System.IO.Path]::GetDirectoryName($fullPath)
if ([string]::IsNullOrWhiteSpace($directory) -or [System.IO.Path]::GetPathRoot($fullPath) -eq $directory) {
    throw 'ConfigPath must be a file below a dedicated configuration directory.'
}
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$dataProtectionPath = Join-Path $directory 'DataProtectionKeys'
New-Item -ItemType Directory -Path $dataProtectionPath -Force | Out-Null

$tradeConnection = Get-EnvironmentValue 'TRADE_DATABASE_CONNECTION_STRING'
$stripeSecret = Get-EnvironmentValue 'STRIPE_SECRET_KEY'
$paypalClientId = Get-EnvironmentValue 'PAYPAL_CLIENT_ID'
$resendApiKey = Get-EnvironmentValue 'RESEND_API_KEY'
$resendFromAddress = Get-EnvironmentValue 'RESEND_FROM_ADDRESS'
$oauthEnabled = (Get-EnvironmentValue 'OAUTH_ENABLED').Trim().ToLowerInvariant() -in @('1', 'true', 'yes', 'on')
if ([string]::IsNullOrWhiteSpace($resendFromAddress)) {
    $resendFromAddress = 'WiseLine Trade <no-reply@email.wiselinetrade.com>'
}

$configuration = [ordered]@{
    ConnectionStrings = [ordered]@{
        PortalDatabase = Get-RequiredEnvironmentValue 'PORTAL_RUNTIME_DATABASE_CONNECTION_STRING'
        TradeDatabase = $tradeConnection
    }
    DataProtection = [ordered]@{
        KeyRingPath = $dataProtectionPath
    }
    Authentication = [ordered]@{
        Google = [ordered]@{
            ClientId = Get-EnvironmentValue 'GOOGLE_CLIENT_ID'
            ClientSecret = Get-EnvironmentValue 'GOOGLE_CLIENT_SECRET'
        }
    }
    OAuth = [ordered]@{
        Enabled = $oauthEnabled
        Issuer = Get-EnvironmentValue 'OAUTH_ISSUER'
        Resource = Get-EnvironmentValue 'OAUTH_RESOURCE'
        SigningCertificateThumbprint = Get-EnvironmentValue 'OAUTH_SIGNING_CERTIFICATE_THUMBPRINT'
        EncryptionCertificateThumbprint = Get-EnvironmentValue 'OAUTH_ENCRYPTION_CERTIFICATE_THUMBPRINT'
        UseDevelopmentSigningCertificate = $false
        AccessTokenMinutes = 10
        RefreshTokenDays = 30
    }
    Email = [ordered]@{
        Enabled = -not [string]::IsNullOrWhiteSpace($resendApiKey)
        ApiKey = $resendApiKey
        FromAddress = $resendFromAddress
        ReplyToAddress = Get-EnvironmentValue 'RESEND_REPLY_TO_ADDRESS'
        PublicBaseUrl = Get-RequiredEnvironmentValue 'PUBLIC_BASE_URL'
        WebhookSecret = Get-EnvironmentValue 'RESEND_WEBHOOK_SECRET'
        OutboxPollSeconds = 5
    }
    TradeDatabase = [ordered]@{
        Enabled = -not [string]::IsNullOrWhiteSpace($tradeConnection)
    }
    Payments = [ordered]@{
        PublicBaseUrl = Get-RequiredEnvironmentValue 'PUBLIC_BASE_URL'
        Stripe = [ordered]@{
            Enabled = -not [string]::IsNullOrWhiteSpace($stripeSecret)
            SecretKey = $stripeSecret
            PriceId = Get-EnvironmentValue 'STRIPE_PRICE_ID'
            WebhookSecret = Get-EnvironmentValue 'STRIPE_WEBHOOK_SECRET'
        }
        PayPal = [ordered]@{
            Enabled = -not [string]::IsNullOrWhiteSpace($paypalClientId)
            BaseUrl = Get-EnvironmentValue 'PAYPAL_BASE_URL'
            ClientId = $paypalClientId
            ClientSecret = Get-EnvironmentValue 'PAYPAL_CLIENT_SECRET'
            PlanId = Get-EnvironmentValue 'PAYPAL_PLAN_ID'
            WebhookId = Get-EnvironmentValue 'PAYPAL_WEBHOOK_ID'
        }
    }
}

$configuration | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $fullPath -Encoding UTF8
& icacls.exe $fullPath /inheritance:r /grant:r 'SYSTEM:F' 'Administrators:F' "IIS AppPool\${AppPoolName}:R" | Out-Null
& icacls.exe $dataProtectionPath /inheritance:r /grant:r 'SYSTEM:F' 'Administrators:F' "IIS AppPool\${AppPoolName}:(OI)(CI)M" | Out-Null
Write-Output "External configuration updated for app pool '$AppPoolName'."
