using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Workplace.ApiService.Auth;
using Workplace.ApiService.Data;

namespace Workplace.ApiService.ConnectedAccounts;

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
    public ConnectedAccount ToNewEntity(string userId, TokenProtector protector) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
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

public static class ConnectedAccountsEndpoints
{
    public static void MapConnectedAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/connected-accounts");

        group.MapGet("/", GetConnectedAccountsAsync);
        group.MapPost("/", ConnectAccountAsync);
        group.MapDelete("/{id:guid}", DisconnectAccountAsync);
    }

    private static async Task<Ok<List<ConnectedAccountResponse>>> GetConnectedAccountsAsync(
        WorkplaceDbContext db, ICurrentUser currentUser)
    {
        var accounts = await db.ConnectedAccounts
            .Where(a => a.UserId == currentUser.UserId)
            .ToListAsync();

        return TypedResults.Ok(accounts.Select(ConnectedAccountResponse.FromEntity).ToList());
    }

    private static async Task<Ok> ConnectAccountAsync(
        ConnectAccountRequest request, WorkplaceDbContext db, TokenProtector protector, ICurrentUser currentUser)
    {
        var existing = await db.ConnectedAccounts.SingleOrDefaultAsync(a =>
            a.UserId == currentUser.UserId &&
            a.Provider == request.Provider &&
            a.ProviderAccountId == request.ProviderAccountId);

        if (existing is null)
        {
            db.ConnectedAccounts.Add(request.ToNewEntity(currentUser.UserId, protector));
        }
        else
        {
            request.ApplyTo(existing, protector);
        }

        await db.SaveChangesAsync();

        return TypedResults.Ok();
    }

    private static async Task<Results<NotFound, NoContent>> DisconnectAccountAsync(
        Guid id, WorkplaceDbContext db, ICurrentUser currentUser)
    {
        var account = await db.ConnectedAccounts
            .SingleOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.UserId);

        if (account is null)
        {
            return TypedResults.NotFound();
        }

        db.ConnectedAccounts.Remove(account);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
