using System.Globalization;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;

using Workplace.Web.Data;

namespace Workplace.Web.CalendarConnections;

public static class ConnectEndpoints
{
    // Scopes actually requested per scheme — used as a fallback label only. The real scopes
    // granted come from the token response itself (see below): a provider can silently grant
    // fewer scopes than requested (e.g. a Workspace admin policy blocking an API for
    // unverified apps), and that mismatch is exactly what needs to be visible, not papered over.
    private static readonly Dictionary<string, (ConnectedAccountProvider Provider, string RequestedScopes)> SchemeMetadata = new()
    {
        [CalendarConnectionSchemes.MicrosoftGraphPersonal] = (ConnectedAccountProvider.MicrosoftGraph, "Calendars.ReadWrite offline_access User.Read"),
        [CalendarConnectionSchemes.MicrosoftGraphWork] = (ConnectedAccountProvider.MicrosoftGraph, "Calendars.Read offline_access User.Read"),
        [CalendarConnectionSchemes.GoogleCalendar] = (ConnectedAccountProvider.GoogleCalendar, "https://www.googleapis.com/auth/calendar.readonly")
    };

    public static void MapConnectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/connect/{scheme}", (string scheme) =>
            TypedResults.Challenge(new AuthenticationProperties { RedirectUri = "/connections" }, [scheme]));
    }

    public static async Task CompleteConnectionAsync(TicketReceivedContext context)
    {
        var scheme = context.Scheme.Name;
        var (provider, requestedScopes) = SchemeMetadata[scheme];

        var principal = context.Principal!;
        var tokens = context.Properties!.GetTokens().ToDictionary(t => t.Name, t => t.Value);

        // Prefer the "scope" field the provider actually returned in the token response over
        // what was requested — they can differ (see note above), and that difference is the
        // whole point of storing this.
        var grantedScopes = tokens.GetValueOrDefault("scope") is { Length: > 0 } actualScopes
            ? actualScopes
            : requestedScopes;

        var providerAccountId = principal.FindFirstValue(ConnectClaimTypes.ProviderAccountId)
            ?? throw new InvalidOperationException($"Missing provider account id for scheme '{scheme}'.");
        var displayLabel = principal.FindFirstValue(ConnectClaimTypes.Label) ?? providerAccountId;
        var tenantId = principal.FindFirstValue(ConnectClaimTypes.TenantId);

        var expiresAtUtc = tokens.TryGetValue("expires_at", out var expiresAtRaw) &&
            DateTimeOffset.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedExpiresAt)
            ? parsedExpiresAt
            : DateTimeOffset.UtcNow.AddHours(1);

        var accountsService = context.HttpContext.RequestServices.GetRequiredService<ConnectedAccountsService>();
        await accountsService.ConnectAsync(new ConnectAccountRequest(
            provider,
            providerAccountId,
            tenantId,
            displayLabel,
            tokens.GetValueOrDefault("access_token"),
            tokens["refresh_token"],
            grantedScopes,
            expiresAtUtc));

        await context.HttpContext.SignOutAsync(CalendarConnectionSchemes.ExternalConnect);

        context.HandleResponse();
        context.Response.Redirect("/connections");
    }
}
