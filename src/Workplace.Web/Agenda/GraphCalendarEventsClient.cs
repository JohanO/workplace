using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

using Workplace.Contracts;

namespace Workplace.Web.Agenda;

public class GraphCalendarEventsClient(HttpClient httpClient)
{
    public async Task<List<ProviderCalendarEvent>> GetEventsAsync(
        string accessToken, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        // endDateTime is exclusive, so add a day to cover all of endDate.
        var start = startDate.ToDateTime(TimeOnly.MinValue).ToString("s", CultureInfo.InvariantCulture);
        var end = endDate.ToDateTime(TimeOnly.MinValue).AddDays(1).ToString("s", CultureInfo.InvariantCulture);

        string? url =
            $"https://graph.microsoft.com/v1.0/me/calendarView" +
            $"?startDateTime={Uri.EscapeDataString(start)}&endDateTime={Uri.EscapeDataString(end)}" +
            $"&$orderby=start/dateTime&$select=subject,start,end,isAllDay";

        var events = new List<ProviderCalendarEvent>();

        while (url is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Graph calendarView returned {(int)response.StatusCode}: {errorBody}");
            }

            var payload = await response.Content.ReadFromJsonAsync<CalendarViewResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Empty calendarView response.");

            events.AddRange(payload.Value.Select(ToProviderEvent));
            url = payload.NextLink;
        }

        return events;
    }

    public async Task<string> CreateAllDayEventAsync(
        string accessToken, string title, DateOnly date, CancellationToken cancellationToken = default)
    {
        // Graph's documented shape for an all-day event: start/end at local midnight, one day
        // apart, with isAllDay: true — a time-of-day-bearing start/end would create a timed event.
        // showAs: "free" — otherwise Graph defaults all-day events to "busy", which would make
        // a place event (informational only) block the whole day on everyone else's view of
        // this calendar.
        var body = new CreateEventRequest(
            title,
            true,
            false,
            "free",
            new CreateEventDateTime(date.ToDateTime(TimeOnly.MinValue).ToString("s", CultureInfo.InvariantCulture)),
            new CreateEventDateTime(date.AddDays(1).ToDateTime(TimeOnly.MinValue).ToString("s", CultureInfo.InvariantCulture)));

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/me/events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(body);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Graph event create returned {(int)response.StatusCode}: {errorBody}");
        }

        var created = await response.Content.ReadFromJsonAsync<CreatedEventResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Empty event create response.");

        return created.Id;
    }

    public async Task DeleteEventAsync(string accessToken, string eventId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete, $"https://graph.microsoft.com/v1.0/me/events/{Uri.EscapeDataString(eventId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            // A 404 means the event is already gone (e.g. deleted manually in Outlook) — treat
            // that as success rather than failing the replace operation.
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Graph event delete returned {(int)response.StatusCode}: {errorBody}");
        }
    }

    private static ProviderCalendarEvent ToProviderEvent(GraphEvent graphEvent) => new(
        graphEvent.Subject ?? string.Empty,
        ParseAsUtc(graphEvent.Start.DateTime),
        ParseAsUtc(graphEvent.End.DateTime),
        graphEvent.IsAllDay);

    // Without a Prefer: outlook.timezone header, Graph always returns start/end in UTC
    // (with timeZone: "UTC") but the dateTime string itself carries no offset — it has to
    // be told, not inferred, that it's already universal time.
    private static DateTimeOffset ParseAsUtc(string dateTime) => DateTimeOffset.Parse(
        dateTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private sealed record CalendarViewResponse(
        [property: JsonPropertyName("value")] List<GraphEvent> Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private sealed record GraphEvent(
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("start")] GraphDateTimeTimeZone Start,
        [property: JsonPropertyName("end")] GraphDateTimeTimeZone End,
        [property: JsonPropertyName("isAllDay")] bool IsAllDay);

    private sealed record GraphDateTimeTimeZone(
        [property: JsonPropertyName("dateTime")] string DateTime);

    private sealed record CreateEventRequest(
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("isAllDay")] bool IsAllDay,
        [property: JsonPropertyName("isReminderOn")] bool IsReminderOn,
        [property: JsonPropertyName("showAs")] string ShowAs,
        [property: JsonPropertyName("start")] CreateEventDateTime Start,
        [property: JsonPropertyName("end")] CreateEventDateTime End);

    // All-day events are pure calendar dates, so the timezone attached to their midnight
    // boundaries is arbitrary — UTC avoids any DST-transition edge case a local zone would have.
    private sealed record CreateEventDateTime(
        [property: JsonPropertyName("dateTime")] string DateTime,
        [property: JsonPropertyName("timeZone")] string TimeZone = "UTC");

    private sealed record CreatedEventResponse(
        [property: JsonPropertyName("id")] string Id);
}
