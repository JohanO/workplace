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
}
