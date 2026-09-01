namespace Workplace.Contracts;

public record ProviderCalendarEvent(string Title, DateTimeOffset Start, DateTimeOffset End, bool IsAllDay);
