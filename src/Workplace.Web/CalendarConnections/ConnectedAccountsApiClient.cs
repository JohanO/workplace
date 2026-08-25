namespace Workplace.Web.CalendarConnections;

public record ConnectAccountRequest(
    ConnectedAccountProvider Provider,
    string ProviderAccountId,
    string? TenantId,
    string DisplayLabel,
    string? AccessToken,
    string RefreshToken,
    string GrantedScopes,
    DateTimeOffset ExpiresAtUtc);

public record ConnectedAccountResponse(
    Guid Id,
    ConnectedAccountProvider Provider,
    string DisplayLabel,
    string GrantedScopes,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? LastRefreshedAtUtc,
    string? LastRefreshError);

public class ConnectedAccountsApiClient(HttpClient httpClient)
{
    public async Task<List<ConnectedAccountResponse>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ConnectedAccountResponse>>("/connected-accounts", cancellationToken)
            ?? [];
    }

    public async Task ConnectAsync(ConnectAccountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/connected-accounts", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DisconnectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/connected-accounts/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
