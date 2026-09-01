using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Workplace.OutlookSync;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

using var http = new HttpClient();
var sync = new OutlookCalendarSync(http, builder.Configuration);

try
{
    await sync.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Sync failed: {ex}");
    Environment.Exit(1);
}
