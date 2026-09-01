namespace Workplace.Contracts;

public static class AgendaDateRange
{
    /// <summary>
    /// Today's remaining workdays, plus the next two full Monday–Friday workweeks.
    /// </summary>
    public static IReadOnlyList<DateOnly> GetWorkdays(DateOnly today)
    {
        var sundayCount = 0;
        return [.. Enumerable.Range(0, 21)
            .Select(today.AddDays)
            .TakeWhile(day => !(day.DayOfWeek == DayOfWeek.Sunday && ++sundayCount == 3))
            .Where(day => day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)];
    }
}
