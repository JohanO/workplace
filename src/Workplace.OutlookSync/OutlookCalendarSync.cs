using System.Net.Http.Json;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Office.Interop.Outlook;
using Workplace.Contracts;

namespace Workplace.OutlookSync;

public class OutlookCalendarSync(HttpClient http, IConfiguration configuration)
{
    private const string SyncKeyHeaderName = "X-Sync-Key";

    public async Task RunAsync()
    {
        var workdays = AgendaDateRange.GetWorkdays(DateOnly.FromDateTime(DateTime.Today));
        var rangeStart = workdays[0].ToDateTime(TimeOnly.MinValue);
        var rangeEnd = workdays[^1].ToDateTime(TimeOnly.MaxValue);

        var events = ReadOutlookEvents(rangeStart, rangeEnd);
        Console.WriteLine($"Read {events.Count} event(s) from Outlook for {workdays[0]:yyyy-MM-dd} through {workdays[^1]:yyyy-MM-dd}.");

        var baseUrl = configuration["SyncTarget:BaseUrl"]
            ?? throw new InvalidOperationException("SyncTarget:BaseUrl is not configured.");
        var syncKey = configuration["OutlookSync:SyncKey"]
            ?? throw new InvalidOperationException("OutlookSync:SyncKey is not configured (set it via user-secrets).");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/work-calendar/sync")
        {
            Content = JsonContent.Create(events)
        };
        request.Headers.Add(SyncKeyHeaderName, syncKey);

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        Console.WriteLine("Synced successfully.");
    }

    // Outlook only expands recurring appointments correctly when IncludeRecurrences is combined
    // with a Sort by [Start] and a Restrict to a bounded date range — enumerating the raw Items
    // collection returns unexpanded recurrence masters instead of individual instances.
    private static List<ProviderCalendarEvent> ReadOutlookEvents(DateTime rangeStart, DateTime rangeEnd)
    {
        var comObjects = new List<object>();
        T Track<T>(T comObject) where T : class
        {
            comObjects.Add(comObject);
            return comObject;
        }

        try
        {
            var outlookApp = Track(new Application());
            var ns = Track(outlookApp.GetNamespace("MAPI"));
            var calendar = Track(ns.GetDefaultFolder(OlDefaultFolders.olFolderCalendar));
            var items = Track(calendar.Items);
            items.IncludeRecurrences = true;
            items.Sort("[Start]");

            var filter = $"[Start] >= '{rangeStart:g}' AND [Start] <= '{rangeEnd:g}'";
            var restrictedItems = Track(items.Restrict(filter));

            var events = new List<ProviderCalendarEvent>();
            foreach (var item in restrictedItems)
            {
                if (item is not AppointmentItem appointment)
                {
                    continue;
                }

                events.Add(new ProviderCalendarEvent(
                    appointment.Subject ?? string.Empty,
                    appointment.Start,
                    appointment.End,
                    appointment.AllDayEvent));

                Marshal.ReleaseComObject(appointment);
            }

            return events;
        }
        finally
        {
            foreach (var comObject in comObjects)
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
    }
}
