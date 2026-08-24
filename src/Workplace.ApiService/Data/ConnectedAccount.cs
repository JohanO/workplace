namespace Workplace.ApiService.Data;

public enum ConnectedAccountProvider
{
    MicrosoftGraph,
    GoogleCalendar
}

public class ConnectedAccount
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public ConnectedAccountProvider Provider { get; set; }
    public required string ProviderAccountId { get; set; }
    public string? TenantId { get; set; }
    public required string DisplayLabel { get; set; }
    public string? EncryptedAccessToken { get; set; }
    public required string EncryptedRefreshToken { get; set; }
    public required string GrantedScopes { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastRefreshedAtUtc { get; set; }
    public string? LastRefreshError { get; set; }
}
