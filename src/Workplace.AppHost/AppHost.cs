var builder = DistributedApplication.CreateBuilder(args);

var workplaceDb = builder.AddSqlite("workplacedb",
    databasePath: Path.Combine(builder.AppHostDirectory, "data"),
    databaseFileName: "workplace.db");

var msGraphClientSecret = builder.AddParameter("msgraph-client-secret", secret: true);
var googleClientSecret = builder.AddParameter("google-client-secret", secret: true);
var outlookSyncKey = builder.AddParameter("outlook-sync-key", secret: true);

var apiService = builder.AddProject<Projects.Workplace_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(workplaceDb)
    .WithEnvironment("AzureAd__ClientSecret", msGraphClientSecret)
    .WithEnvironment("GoogleCalendar__ClientSecret", googleClientSecret)
    .WithEnvironment("OutlookSync__SyncKey", outlookSyncKey);

builder.AddProject<Projects.Workplace_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("AzureAd__ClientSecret", msGraphClientSecret)
    .WithEnvironment("GoogleCalendar__ClientSecret", googleClientSecret)
    .WithEnvironment("OutlookSync__SyncKey", outlookSyncKey);

await builder.Build().RunAsync();
