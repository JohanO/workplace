using TUnit.Aspire;

namespace Workplace.Tests;

public class AppFixture : AspireFixture<Projects.Workplace_AppHost>
{
    protected override void ConfigureBuilder(IDistributedApplicationTestingBuilder builder)
    {
        builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });
    }
}
