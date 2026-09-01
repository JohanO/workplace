namespace Workplace.ApiService.Data;

public class WorkCalendarSnapshot
{
    public Guid Id { get; set; }
    public DateTimeOffset SyncedAtUtc { get; set; }
    public required string EventsJson { get; set; }
}
