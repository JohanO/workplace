using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Workplace.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

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

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Deny by default — every page requires login unless explicitly marked [AllowAnonymous].
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

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

app.MapDefaultEndpoints();

await app.RunAsync();
