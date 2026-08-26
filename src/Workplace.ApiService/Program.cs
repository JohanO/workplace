using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Workplace.ApiService.Auth;
using Workplace.ApiService.ConnectedAccounts;
using Workplace.ApiService.Data;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<WorkplaceDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("workplacedb")));

var dataProtectionKeyPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Workplace", "dpkeys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
    .SetApplicationName("Workplace");
builder.Services.AddSingleton<TokenProtector>();

builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());

builder.Services.AddHttpClient<TokenRefreshService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<WorkplaceDbContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Only the connected-accounts endpoints require the internal identity header —
// health checks and other endpoints must stay reachable without it.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/connected-accounts"),
    branch => branch.UseMiddleware<CurrentUserMiddleware>());

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapConnectedAccountsEndpoints();

app.MapDefaultEndpoints();

// Map the liveness health check endpoint so Docker Compose can use it to
// wait for the API to be ready before starting the web container.
app.MapHealthChecks("/alive", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

await app.RunAsync();
