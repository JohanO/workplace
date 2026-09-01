using Microsoft.AspNetCore.Http.HttpResults;
using Workplace.Contracts;

namespace Workplace.Web.WorkCalendar;

public static class WorkCalendarSyncEndpoints
{
    public const string SyncKeyHeaderName = "X-Sync-Key";

    public static void MapWorkCalendarSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-calendar/sync", SyncAsync).AllowAnonymous();
    }

    private static async Task<Results<UnauthorizedHttpResult, NoContent>> SyncAsync(
        HttpRequest request, List<ProviderCalendarEvent> events, WorkCalendarSyncApiClient client, IConfiguration configuration)
    {
        var expectedKey = configuration["OutlookSync:SyncKey"];

        if (!request.Headers.TryGetValue(SyncKeyHeaderName, out var providedKey) ||
            expectedKey is null ||
            providedKey != expectedKey)
        {
            return TypedResults.Unauthorized();
        }

        await client.SyncAsync(providedKey!, events);

        return TypedResults.NoContent();
    }
}
