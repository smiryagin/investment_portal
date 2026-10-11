namespace WiseLine.Portal.Api.Models;

public sealed record OAuthSubscriptionRequiredViewModel(
    string ClientName,
    string SubscriptionStatus);
