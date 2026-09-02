using System.Text.Json.Serialization;

using Workplace.Web.Data;

namespace Workplace.Web.ConnectedAccounts;

public class TokenRefreshService(
    HttpClient httpClient,
    WorkplaceDbContext db,
    TokenProtector protector,
    IConfiguration configuration,
    ILogger<TokenRefreshService> logger)
{
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);

    public async Task<string?> GetValidAccessTokenAsync(ConnectedAccount account, CancellationToken cancellationToken = default)
    {
        return account.EncryptedAccessToken is not null && account.ExpiresAtUtc - DateTimeOffset.UtcNow > RefreshBuffer
            ? protector.Unprotect(account.EncryptedAccessToken)
            : await RefreshAsync(account, cancellationToken);
    }

    private async Task<string?> RefreshAsync(ConnectedAccount account, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await RequestNewTokenAsync(account, cancellationToken);
            ApplyRefreshedToken(account, payload);
            await db.SaveChangesAsync(cancellationToken);

            return payload.AccessToken;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh token for connected account {AccountId}", account.Id);
            account.LastRefreshError = ex.Message;
            await db.SaveChangesAsync(cancellationToken);

            return null;
        }
    }

    private async Task<TokenRefreshResponse> RequestNewTokenAsync(ConnectedAccount account, CancellationToken cancellationToken)
    {
        var refreshToken = protector.Unprotect(account.EncryptedRefreshToken);
        var (tokenEndpoint, clientId, clientSecret) = GetProviderConfig(account.Provider);

        var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        }), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Token endpoint returned {(int)response.StatusCode}: {errorBody}");
        }

        return await response.Content.ReadFromJsonAsync<TokenRefreshResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Empty token refresh response.");
    }

    private void ApplyRefreshedToken(ConnectedAccount account, TokenRefreshResponse payload)
    {
        account.EncryptedAccessToken = protector.Protect(payload.AccessToken);
        if (!string.IsNullOrEmpty(payload.RefreshToken))
        {
            // Microsoft always rotates the refresh token; Google may omit it, in which case
            // the existing one is still valid and must be kept rather than cleared.
            account.EncryptedRefreshToken = protector.Protect(payload.RefreshToken);
        }
        account.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
        account.LastRefreshedAtUtc = DateTimeOffset.UtcNow;
        account.LastRefreshError = null;
    }

    private (string TokenEndpoint, string ClientId, string ClientSecret) GetProviderConfig(ConnectedAccountProvider provider) => provider switch
    {
        ConnectedAccountProvider.MicrosoftGraph => (
            "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            RequireConfig("AzureAd:ClientId"),
            RequireConfig("AzureAd:ClientSecret")),
        ConnectedAccountProvider.GoogleCalendar => (
            "https://oauth2.googleapis.com/token",
            RequireConfig("GoogleCalendar:ClientId"),
            RequireConfig("GoogleCalendar:ClientSecret")),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };

    private string RequireConfig(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"Missing configuration value '{key}'.");

    private sealed record TokenRefreshResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
