namespace Workplace.ApiService.Agenda;

public record ProviderCalendarEvent(string Title, DateTimeOffset Start, DateTimeOffset End, bool IsAllDay);
