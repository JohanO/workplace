namespace Workplace.Tests;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public class WebTests(AppFixture app)
{
    [Test]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        var httpClient = app.CreateHttpClient("webfrontend");

        var response = await httpClient.GetAsync("/");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
