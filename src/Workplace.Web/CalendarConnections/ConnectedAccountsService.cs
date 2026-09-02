using Microsoft.EntityFrameworkCore;

using Workplace.Web.Data;

namespace Workplace.Web.CalendarConnections;

public record ConnectAccountRequest(
    ConnectedAccountProvider Provider,
    string ProviderAccountId,
    string? TenantId,
    string DisplayLabel,
    string? AccessToken,
    string RefreshToken,
    string GrantedScopes,
    DateTimeOffset ExpiresAtUtc)
{
    public ConnectedAccount ToNewEntity(TokenProtector protector) => new()
    {
        Id = Guid.NewGuid(),
        Provider = Provider,
        ProviderAccountId = ProviderAccountId,
        TenantId = TenantId,
        DisplayLabel = DisplayLabel,
        EncryptedAccessToken = AccessToken is null ? null : protector.Protect(AccessToken),
        EncryptedRefreshToken = protector.Protect(RefreshToken),
        GrantedScopes = GrantedScopes,
        ExpiresAtUtc = ExpiresAtUtc,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    public void ApplyTo(ConnectedAccount existing, TokenProtector protector)
    {
        existing.TenantId = TenantId;
        existing.DisplayLabel = DisplayLabel;
        existing.EncryptedAccessToken = AccessToken is null ? null : protector.Protect(AccessToken);
        existing.EncryptedRefreshToken = protector.Protect(RefreshToken);
        existing.GrantedScopes = GrantedScopes;
        existing.ExpiresAtUtc = ExpiresAtUtc;
        existing.LastRefreshedAtUtc = null;
        existing.LastRefreshError = null;
    }
}

public record ConnectedAccountResponse(
    Guid Id,
    ConnectedAccountProvider Provider,
    string DisplayLabel,
    string GrantedScopes,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? LastRefreshedAtUtc,
    string? LastRefreshError)
{
    public static ConnectedAccountResponse FromEntity(ConnectedAccount account) => new(
        account.Id,
        account.Provider,
        account.DisplayLabel,
        account.GrantedScopes,
        account.ExpiresAtUtc,
        account.LastRefreshedAtUtc,
        account.LastRefreshError);
}

// Replaces the ConnectedAccountsApiClient (Web) + ConnectedAccountsEndpoints (ApiService) HTTP
// round trip — both sides ran in the same process anyway, so this is a plain scoped service
// that Razor components and ConnectEndpoints call directly. No per-user scoping: this app is
// single-user, gated at login by AllowedUser:ObjectId (see Program.cs), so every connected
// account in the database belongs to the one allowed user by construction.
public class ConnectedAccountsService(WorkplaceDbContext db, TokenProtector protector)
{
    public async Task<List<ConnectedAccountResponse>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await db.ConnectedAccounts.ToListAsync(cancellationToken);

        return accounts.Select(ConnectedAccountResponse.FromEntity).ToList();
    }

    public async Task ConnectAsync(ConnectAccountRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await db.ConnectedAccounts.SingleOrDefaultAsync(a =>
            a.Provider == request.Provider &&
            a.ProviderAccountId == request.ProviderAccountId, cancellationToken);

        if (existing is null)
        {
            db.ConnectedAccounts.Add(request.ToNewEntity(protector));
        }
        else
        {
            request.ApplyTo(existing, protector);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisconnectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await db.ConnectedAccounts.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (account is null)
        {
            throw new InvalidOperationException($"Connected account '{id}' was not found.");
        }

        db.ConnectedAccounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
    }
}
