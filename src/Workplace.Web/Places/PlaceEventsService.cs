using Microsoft.EntityFrameworkCore;

using Workplace.Web.Agenda;
using Workplace.Web.ConnectedAccounts;
using Workplace.Web.Data;

namespace Workplace.Web.Places;

// Plain scoped service, same pattern as PlacesService/AgendaService — no HTTP layer, no
// per-user scoping (this app is single-user by construction).
public class PlaceEventsService(
    WorkplaceDbContext db,
    TokenRefreshService tokenRefreshService,
    GraphCalendarEventsClient graphClient)
{
    public async Task<Dictionary<DateOnly, PlaceDayEvent>> GetAssignmentsAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default) =>
        await db.PlaceDayEvents
            .AsNoTracking()
            .Where(e => e.Date >= start && e.Date <= end)
            .ToDictionaryAsync(e => e.Date, cancellationToken);

    public async Task SetPlaceForDayAsync(DateOnly date, Guid placeId, CancellationToken cancellationToken = default)
    {
        var existing = await db.PlaceDayEvents.SingleOrDefaultAsync(e => e.Date == date, cancellationToken);
        if (existing is not null && existing.PlaceId == placeId)
        {
            return;
        }

        var place = await db.Places.SingleAsync(p => p.Id == placeId, cancellationToken);

        // Only the personal Microsoft connection has Calendars.ReadWrite — the work connection
        // (see Program.cs's ConfigureMicrosoftGraphConnection) is read-only by design.
        var account = await db.ConnectedAccounts
            .Where(a => a.Provider == ConnectedAccountProvider.MicrosoftGraph)
            .Where(a => a.GrantedScopes.Contains("Calendars.ReadWrite"))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Connect a personal Microsoft account with calendar write access on the Connections page.");

        var token = await tokenRefreshService.GetValidAccessTokenAsync(account, cancellationToken)
            ?? throw new InvalidOperationException(
                $"{account.LastRefreshError ?? "Could not obtain an access token."} Reconnect this calendar on the Connections page.");

        if (existing is not null)
        {
            await graphClient.DeleteEventAsync(token, existing.GraphEventId, cancellationToken);
        }

        var eventId = await graphClient.CreateAllDayEventAsync(token, place.Name, date, cancellationToken);

        if (existing is not null)
        {
            existing.PlaceId = placeId;
            existing.ConnectedAccountId = account.Id;
            existing.GraphEventId = eventId;
        }
        else
        {
            db.PlaceDayEvents.Add(new PlaceDayEvent
            {
                Date = date,
                PlaceId = placeId,
                ConnectedAccountId = account.Id,
                GraphEventId = eventId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
