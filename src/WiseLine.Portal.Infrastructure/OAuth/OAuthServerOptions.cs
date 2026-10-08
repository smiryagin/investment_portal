namespace WiseLine.Portal.Infrastructure.OAuth;

public sealed class OAuthServerOptions
{
    public const string SectionName = "OAuth";

    public bool Enabled { get; set; }

    public string Issuer { get; set; } = "https://wiselinetrade.com";

    public string Resource { get; set; } = "https://investments-mcp.torusystems.com/mcp";

    public string SigningCertificateThumbprint { get; set; } = string.Empty;

    public string EncryptionCertificateThumbprint { get; set; } = string.Empty;

    public bool UseDevelopmentSigningCertificate { get; set; }

    public int AccessTokenMinutes { get; set; } = 10;

    public int RefreshTokenDays { get; set; } = 30;

    public int RefreshTokenAbsoluteDays { get; set; } = 90;

    public bool ClientIdMetadataDocumentSupported { get; set; }

    public List<OAuthClientOptions> Clients { get; set; } = [];
}

public sealed class OAuthClientOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ApplicationType { get; set; } = "web";

    public List<string> RedirectUris { get; set; } = [];

    public string TokenEndpointAuthenticationMethod { get; set; } = "none";

    public string JsonWebKeySetUri { get; set; } = string.Empty;
}
