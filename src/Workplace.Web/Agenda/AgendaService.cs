using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Workplace.Contracts;
using Workplace.Web.ConnectedAccounts;
using Workplace.Web.Data;

namespace Workplace.Web.Agenda;

public record AgendaEvent(
    string Title, DateTimeOffset Start, DateTimeOffset End, bool IsAllDay,
    string CalendarKey, string CalendarLabel, string Color);

public record AgendaDay(DateOnly Date, List<AgendaEvent> Events);

public record AgendaSourceError(string CalendarLabel, string Message);

public record AgendaWarning(string CalendarLabel, string Message);

public record AgendaResult(List<AgendaDay> Days, List<AgendaSourceError> Errors, List<AgendaWarning> Warnings);

// Plain scoped service, same pattern as ConnectedAccountsService/CalendarColorService — no
// HTTP layer, no per-user scoping (this app is single-user by construction).
public class AgendaService(
    WorkplaceDbContext db,
    TokenRefreshService tokenRefreshService,
    GraphCalendarEventsClient graphClient,
    GoogleCalendarEventsClient googleClient,
    CalendarColorService calendarColorService)
{
    // The date-range and day-bucketing both anchor to local time, not UTC — events fetched
    // near midnight would otherwise land on the wrong day for a Sweden-based user.
    private static readonly TimeZoneInfo LocalZone = ResolveStockholmTimeZone();

    public async Task<AgendaResult> GetAgendaAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, LocalZone).DateTime);
        var dates = AgendaDateRange.GetWorkdays(today);
        var startDate = dates[0];
        var endDate = dates[^1];

        var accounts = await db.ConnectedAccounts.ToListAsync(cancellationToken);
        var calendarsByKey = (await calendarColorService.GetCalendarsAsync(cancellationToken))
            .ToDictionary(c => c.Key);

        var accountResults = await Task.WhenAll(accounts.Select(account =>
            FetchAccountEventsAsync(account, startDate, endDate, calendarsByKey, cancellationToken)));
        var workResult = await FetchWorkCalendarEventsAsync(
            calendarsByKey[CalendarColorService.WorkCalendarKey], cancellationToken);

        var allEvents = accountResults.SelectMany(r => r.Events).Concat(workResult.Events).ToList();
        var errors = accountResults
            .Select(r => r.Error)
            .Append(workResult.Error)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        var days = dates.Select(date => BuildDay(date, allEvents)).ToList();
        var warnings = new[] { workResult.Warning }.Where(w => w is not null).Select(w => w!).ToList();

        return new AgendaResult(days, errors, warnings);
    }

    private async Task<(List<AgendaEvent> Events, AgendaSourceError? Error)> FetchAccountEventsAsync(
        ConnectedAccount account, DateOnly startDate, DateOnly endDate,
        Dictionary<string, CalendarSource> calendarsByKey, CancellationToken cancellationToken)
    {
        try
        {
            var token = await tokenRefreshService.GetValidAccessTokenAsync(account, cancellationToken);
            if (token is null)
            {
                // A dead refresh token (expired/revoked) can't be repaired automatically — the
                // provider requires a fresh user consent, which only re-connecting can provide.
                var reason = account.LastRefreshError ?? "Could not obtain an access token.";
                return ([], new AgendaSourceError(
                    account.DisplayLabel, $"{reason} Reconnect this calendar on the Connections page."));
            }

            var providerEvents = account.Provider switch
            {
                ConnectedAccountProvider.MicrosoftGraph =>
                    await graphClient.GetEventsAsync(token, startDate, endDate, cancellationToken),
                ConnectedAccountProvider.GoogleCalendar =>
                    await googleClient.GetEventsAsync(token, startDate, endDate, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(account), account.Provider, null)
            };

            var source = calendarsByKey[CalendarColorService.AccountKey(account.Id)];
            return (ToAgendaEvents(providerEvents, source), null);
        }
        catch (Exception ex)
        {
            return ([], new AgendaSourceError(account.DisplayLabel, ex.Message));
        }
    }

    // The work calendar isn't fetched live like the connected accounts — it's pushed by a
    // separately-run Outlook COM sync (see Workplace.OutlookSync), so its snapshot can go stale
    // if that sync stops running without anyone noticing.
    private static readonly TimeSpan StaleWorkCalendarThreshold = TimeSpan.FromHours(24);

    private async Task<(List<AgendaEvent> Events, AgendaSourceError? Error, AgendaWarning? Warning)> FetchWorkCalendarEventsAsync(
        CalendarSource source, CancellationToken cancellationToken)
    {
        try
        {
            // SQLite can't translate ORDER BY on DateTimeOffset server-side — order client-side.
            // The snapshot table is small (one row per manual sync run), so this is cheap.
            var snapshot = (await db.WorkCalendarSnapshots.ToListAsync(cancellationToken))
                .OrderByDescending(s => s.SyncedAtUtc)
                .FirstOrDefault();

            if (snapshot is null)
            {
                return ([], null, null);
            }

            var age = DateTimeOffset.UtcNow - snapshot.SyncedAtUtc;
            var warning = age > StaleWorkCalendarThreshold
                ? new AgendaWarning(source.DisplayLabel, $"Last synced {snapshot.SyncedAtUtc:yyyy-MM-dd HH:mm} UTC — more than a day old.")
                : null;

            var providerEvents = JsonSerializer.Deserialize<List<ProviderCalendarEvent>>(snapshot.EventsJson) ?? [];
            return (ToAgendaEvents(providerEvents, source), null, warning);
        }
        catch (Exception ex)
        {
            return ([], new AgendaSourceError(source.DisplayLabel, ex.Message), null);
        }
    }

    private static List<AgendaEvent> ToAgendaEvents(List<ProviderCalendarEvent> events, CalendarSource source) =>
        events
            .Select(e => new AgendaEvent(
                e.Title, ToLocal(e.Start), ToLocal(e.End), e.IsAllDay,
                source.Key, source.DisplayLabel, source.Color))
            .ToList();

    // Providers normalize differently — Graph/Google both return UTC (offset 0), while the
    // Outlook-COM sync path preserves a local offset — so displaying an event's own offset
    // directly (e.g. Start.ToString("HH:mm")) is inconsistent across sources. Converting every
    // event to the same local zone right at ingestion makes every downstream consumer (day
    // bucketing, display) consistent without needing to know which source an event came from.
    private static DateTimeOffset ToLocal(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, LocalZone);

    private static AgendaDay BuildDay(DateOnly date, List<AgendaEvent> allEvents)
    {
        var dayEvents = allEvents.Where(e => LocalDate(e.Start) == date).ToList();

        // All-day events get their own row(s) at the top, ahead of the timed events below.
        var allDayEvents = dayEvents.Where(e => e.IsAllDay).OrderBy(e => e.Title).ToList();

        // Everything else is one plain list sorted by start time — side-by-side columns for
        // overlapping events were tried and rejected as confusing for a quick glance at the day.
        var timedEvents = dayEvents.Where(e => !e.IsAllDay).OrderBy(e => e.Start).ToList();

        return new AgendaDay(date, [.. allDayEvents, .. timedEvents]);
    }

    private static DateOnly LocalDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, LocalZone).DateTime);

    private static TimeZoneInfo ResolveStockholmTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        }
        catch (TimeZoneNotFoundException)
        {
            // IANA id not found — likely running on Windows without ICU globalization data.
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}
