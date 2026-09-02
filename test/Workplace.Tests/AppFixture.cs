using TUnit.Aspire;

namespace Workplace.Tests;

public class AppFixture : AspireFixture<Projects.Workplace_AppHost>
{
    protected override void ConfigureBuilder(IDistributedApplicationTestingBuilder builder)
    {
        // webfrontend won't start without these — provide placeholders so the
        // fixture can boot without real OAuth client secrets.
        builder.Configuration["Parameters:msgraph-client-secret"] = "test-msgraph-secret";
        builder.Configuration["Parameters:google-client-secret"] = "test-google-secret";
        builder.Configuration["Parameters:outlook-sync-key"] = "test-outlook-sync-key";

        builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();

            // Web now requires login by default; tests must observe the redirect itself
            // rather than following it out to Microsoft's real login page.
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                // CI runners don't trust the local ASP.NET Core dev HTTPS certificate,
                // so health checks/requests against https endpoints fail with UntrustedRoot.
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            });
        });
    }
}
