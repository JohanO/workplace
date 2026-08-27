using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using System.Net;
using Workplace.Web.CalendarConnections;
using Workplace.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Trust the reverse proxy (Cloudflare tunnel) running inside the Docker network so that
// X-Forwarded-Proto is respected. This ensures OIDC/OAuth callback URIs use https:// and
// UseHttpsRedirection does not loop when the container itself runs on plain HTTP.
// Restrict to the Docker bridge network range (172.16.0.0/12) rather than trusting all proxies.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, configSectionName: "AzureAd");

// Single-user allowlist: reject any account that isn't the configured owner, before any
// cookie is issued — see decision #3 in the plan. Chains onto Microsoft.Identity.Web's own
// OnTokenValidated handler rather than replacing it.
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    var innerHandler = options.Events.OnTokenValidated;
    options.Events.OnTokenValidated = async context =>
    {
        if (innerHandler is not null)
        {
            await innerHandler(context);
        }

        var objectId = context.Principal?.GetObjectId();
        var allowedObjectId = builder.Configuration["AllowedUser:ObjectId"];

        if (string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(allowedObjectId) ||
            !string.Equals(objectId, allowedObjectId, StringComparison.OrdinalIgnoreCase))
        {
            context.Fail("This account is not authorized to use this app.");
        }
    };
});

// Calendar connections: separate OAuth grants from app login (decision #2), all landing on a
// short-lived temp cookie so they never touch the primary login session.
builder.Services.AddAuthentication()
    .AddCookie(CalendarConnectionSchemes.ExternalConnect, options =>
    {
        options.Cookie.Name = ".Workplace.ExternalConnect";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddOAuth(CalendarConnectionSchemes.MicrosoftGraphPersonal, options =>
    {
        ConfigureMicrosoftGraphConnection(options, builder.Configuration, calendarScope: "Calendars.ReadWrite");
        options.CallbackPath = "/signin-microsoftgraph-personal";
    })
    .AddOAuth(CalendarConnectionSchemes.MicrosoftGraphWork, options =>
    {
        ConfigureMicrosoftGraphConnection(options, builder.Configuration, calendarScope: "Calendars.Read");
        options.CallbackPath = "/signin-microsoftgraph-work";
    })
    .AddGoogle(CalendarConnectionSchemes.GoogleCalendar, options =>
    {
        options.SignInScheme = CalendarConnectionSchemes.ExternalConnect;
        options.ClientId = builder.Configuration["GoogleCalendar:ClientId"]!;
        options.ClientSecret = builder.Configuration["GoogleCalendar:ClientSecret"]!;
        options.CallbackPath = "/signin-googlecalendar";
        options.AccessType = "offline";
        options.SaveTokens = true;
        options.Scope.Add("https://www.googleapis.com/auth/calendar.readonly");
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            // Force the consent screen every time so Google always issues a refresh token —
            // it's otherwise only returned on the very first authorization for an app.
            context.RedirectUri += "&prompt=consent";
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnCreatingTicket = context =>
        {
            // Google's handler already populates these via its default ClaimActions;
            // copy them into the same normalized claim types the Microsoft flow uses,
            // so ConnectEndpoints can read either provider's ticket identically.
            var subject = context.Identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = context.Identity?.FindFirst(ClaimTypes.Email)?.Value;

            if (subject is not null)
            {
                context.Identity?.AddClaim(new Claim(ConnectClaimTypes.ProviderAccountId, subject));
            }
            if (email is not null)
            {
                context.Identity?.AddClaim(new Claim(ConnectClaimTypes.Label, email));
            }

            return Task.CompletedTask;
        };
        options.Events.OnTicketReceived = ConnectEndpoints.CompleteConnectionAsync;
    });

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<UserContextHandler>();
builder.Services.AddHttpClient<ConnectedAccountsApiClient>(client =>
    {
        client.BaseAddress = new("https+http://apiservice");
    })
    .AddHttpMessageHandler<UserContextHandler>();

// Deny by default — every page requires login unless explicitly marked [AllowAnonymous].
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/login", (string? returnUrl) =>
    TypedResults.Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? "/" }))
    .AllowAnonymous();

app.MapGet("/logout", () =>
    TypedResults.SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
    .AllowAnonymous();

app.MapConnectEndpoints();

app.MapDefaultEndpoints();

await app.RunAsync();

static void ConfigureMicrosoftGraphConnection(OAuthOptions options, IConfiguration configuration, string calendarScope)
{
    options.SignInScheme = CalendarConnectionSchemes.ExternalConnect;
    options.ClientId = configuration["AzureAd:ClientId"]!;
    options.ClientSecret = configuration["AzureAd:ClientSecret"]!;
    options.AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    options.TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    options.SaveTokens = true;
    options.Scope.Add("offline_access");
    options.Scope.Add(calendarScope);
    options.Scope.Add("User.Read");
    options.Events = new OAuthEvents
    {
        OnCreatingTicket = PopulateMicrosoftGraphClaimsAsync,
        OnTicketReceived = ConnectEndpoints.CompleteConnectionAsync
    };
}

static async Task PopulateMicrosoftGraphClaimsAsync(OAuthCreatingTicketContext context)
{
    // Graph's /me is the reliable source for the account's object id and a human-readable
    // label — Graph access tokens are frequently opaque (not JWTs), so the id can't always
    // be read by decoding the token itself.
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me?$select=id,mail,userPrincipalName");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

    using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
    response.EnsureSuccessStatusCode();

    var user = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: context.HttpContext.RequestAborted);

    var objectId = user.GetProperty("id").GetString();
    if (objectId is not null)
    {
        context.Identity?.AddClaim(new Claim(ConnectClaimTypes.ProviderAccountId, objectId));
    }

    var label = user.TryGetProperty("mail", out var mail) && mail.GetString() is { Length: > 0 } mailValue
        ? mailValue
        : user.GetProperty("userPrincipalName").GetString();
    if (label is not null)
    {
        context.Identity?.AddClaim(new Claim(ConnectClaimTypes.Label, label));
    }

    // Tenant id is diagnostic-only (nullable) — best-effort only, since an opaque access
    // token simply means it isn't available, not a failure worth breaking the connection over.
    var jwtHandler = new JwtSecurityTokenHandler();
    if (jwtHandler.CanReadToken(context.AccessToken))
    {
        var jwt = jwtHandler.ReadJwtToken(context.AccessToken);
        var tenantId = jwt.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
        if (tenantId is not null)
        {
            context.Identity?.AddClaim(new Claim(ConnectClaimTypes.TenantId, tenantId));
        }
    }
}
