using System.Net.Http.Json;
using Workplace.Contracts;

namespace Workplace.Web.WorkCalendar;

public class WorkCalendarSyncApiClient(HttpClient httpClient)
{
    public async Task SyncAsync(string syncKey, List<ProviderCalendarEvent> events, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/work-calendar/sync")
        {
            Content = JsonContent.Create(events)
        };
        request.Headers.Add(WorkCalendarSyncEndpoints.SyncKeyHeaderName, syncKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
