using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

using Workplace.Contracts;

namespace Workplace.Web.Agenda;

public class GoogleCalendarEventsClient(HttpClient httpClient)
{
    public async Task<List<ProviderCalendarEvent>> GetEventsAsync(
        string accessToken, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var timeMin = ToUtcInstant(startDate);
        // timeMax is exclusive, so add a day to cover all of endDate.
        var timeMax = ToUtcInstant(endDate.AddDays(1));

        var events = new List<ProviderCalendarEvent>();
        string? pageToken = null;

        do
        {
            var url =
                "https://www.googleapis.com/calendar/v3/calendars/primary/events" +
                $"?timeMin={Uri.EscapeDataString(timeMin)}&timeMax={Uri.EscapeDataString(timeMax)}" +
                "&singleEvents=true&orderBy=startTime" +
                $"&fields={Uri.EscapeDataString("items(summary,start,end),nextPageToken")}" +
                (pageToken is null ? string.Empty : $"&pageToken={Uri.EscapeDataString(pageToken)}");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Google Calendar events.list returned {(int)response.StatusCode}: {errorBody}");
            }

            var payload = await response.Content.ReadFromJsonAsync<EventsListResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Empty events.list response.");

            events.AddRange(payload.Items.Select(ToProviderEvent));
            pageToken = payload.NextPageToken;
        } while (pageToken is not null);

        return events;
    }

    private static string ToUtcInstant(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToString("o", CultureInfo.InvariantCulture);

    private static ProviderCalendarEvent ToProviderEvent(GoogleEvent googleEvent) => new(
        googleEvent.Summary ?? string.Empty,
        ParseAsUtc(googleEvent.Start),
        ParseAsUtc(googleEvent.End),
        IsAllDay: googleEvent.Start.Date is not null);

    // Timed events carry a "dateTime" with an explicit UTC offset; all-day events carry only a
    // calendar "date" (e.g. "2026-08-25"), which has no real time zone — treated as midnight UTC.
    private static DateTimeOffset ParseAsUtc(GoogleEventDateTime value) => value.DateTime is not null
        ? DateTimeOffset.Parse(value.DateTime, CultureInfo.InvariantCulture).ToUniversalTime()
        : new DateTimeOffset(DateOnly.Parse(value.Date!, CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private sealed record EventsListResponse(
        [property: JsonPropertyName("items")] List<GoogleEvent> Items,
        [property: JsonPropertyName("nextPageToken")] string? NextPageToken);

    private sealed record GoogleEvent(
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("start")] GoogleEventDateTime Start,
        [property: JsonPropertyName("end")] GoogleEventDateTime End);

    private sealed record GoogleEventDateTime(
        [property: JsonPropertyName("dateTime")] string? DateTime,
        [property: JsonPropertyName("date")] string? Date);
}
