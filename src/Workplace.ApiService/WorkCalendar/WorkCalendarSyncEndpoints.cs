using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Workplace.ApiService.Data;
using Workplace.Contracts;

namespace Workplace.ApiService.WorkCalendar;

public static class WorkCalendarSyncEndpoints
{
    public static void MapWorkCalendarSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-calendar/sync", SyncAsync);
    }

    private static async Task<NoContent> SyncAsync(List<ProviderCalendarEvent> events, WorkplaceDbContext db)
    {
        db.WorkCalendarSnapshots.Add(new WorkCalendarSnapshot
        {
            Id = Guid.NewGuid(),
            SyncedAtUtc = DateTimeOffset.UtcNow,
            EventsJson = JsonSerializer.Serialize(events)
        });

        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
