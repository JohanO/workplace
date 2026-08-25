using TUnit.Aspire;

namespace Workplace.Tests;

public class AppFixture : AspireFixture<Projects.Workplace_AppHost>
{
    protected override void ConfigureBuilder(IDistributedApplicationTestingBuilder builder)
    {
        builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();

            // Web now requires login by default; tests must observe the redirect itself
            // rather than following it out to Microsoft's real login page.
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        });
    }
}
