using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Workplace.Web.CalendarConnections;

public static class ConnectEndpoints
{
    private static readonly Dictionary<string, (ConnectedAccountProvider Provider, string GrantedScopes)> SchemeMetadata = new()
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
        // At this point in the pipeline, HttpContext.User has not yet been populated with the
        // primary login identity — remote-auth callback handling runs before that step — so
        // UserContextHandler would otherwise see an anonymous user. Authenticate the login
        // cookie explicitly and fix HttpContext.User up before calling out to ApiService.
        var loginResult = await context.HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (loginResult.Succeeded && loginResult.Principal is not null)
        {
            context.HttpContext.User = loginResult.Principal;
        }

        var scheme = context.Scheme.Name;
        var (provider, grantedScopes) = SchemeMetadata[scheme];

        var principal = context.Principal!;
        var tokens = context.Properties!.GetTokens().ToDictionary(t => t.Name, t => t.Value);

        var providerAccountId = principal.FindFirstValue(ConnectClaimTypes.ProviderAccountId)
            ?? throw new InvalidOperationException($"Missing provider account id for scheme '{scheme}'.");
        var displayLabel = principal.FindFirstValue(ConnectClaimTypes.Label) ?? providerAccountId;
        var tenantId = principal.FindFirstValue(ConnectClaimTypes.TenantId);

        var expiresAtUtc = tokens.TryGetValue("expires_at", out var expiresAtRaw) &&
            DateTimeOffset.TryParse(expiresAtRaw, out var parsedExpiresAt)
            ? parsedExpiresAt
            : DateTimeOffset.UtcNow.AddHours(1);

        var apiClient = context.HttpContext.RequestServices.GetRequiredService<ConnectedAccountsApiClient>();
        await apiClient.ConnectAsync(new ConnectAccountRequest(
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
