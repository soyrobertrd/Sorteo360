namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>Si una fila de <c>campaign_recipient</c> guarda cada uno de sus dos hashes.</summary>
internal sealed record RecipientTokens(bool HasRegistrationToken, bool HasUnsubscribeToken);
