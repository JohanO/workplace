using Workplace.ApiService.Agenda;

namespace Workplace.ApiService.UnitTests.Agenda;

public class AgendaDateRangeTests
{
    [Test]
    public async Task MondayStart_ReturnsThisWeekPlusNextTwoFullWeeks()
    {
        var result = AgendaDateRange.GetWorkdays(new DateOnly(2026, 8, 24)); // Monday

        await Assert.That(result).IsEquivalentTo(
        [
            new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 25), new DateOnly(2026, 8, 26), new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 28),
            new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 4),
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 11),
        ]);
    }

    [Test]
    [Arguments(2026, 8, 24, 15)] // Monday: full week + 2 full weeks
    [Arguments(2026, 8, 25, 14)] // Tuesday
    [Arguments(2026, 8, 26, 13)] // Wednesday
    [Arguments(2026, 8, 27, 12)] // Thursday
    [Arguments(2026, 8, 28, 11)] // Friday: only today left this week
    [Arguments(2026, 8, 29, 10)] // Saturday: this week's workdays are already gone
    [Arguments(2026, 8, 30, 10)] // Sunday: same as Saturday
    public async Task DayCount_MatchesRemainingWorkdaysThisWeekPlusTwoFullWeeks(int year, int month, int day, int expectedCount)
    {
        var result = AgendaDateRange.GetWorkdays(new DateOnly(year, month, day));

        await Assert.That(result).Count().IsEqualTo(expectedCount);
    }

    [Test]
    [Arguments(2026, 8, 24)]
    [Arguments(2026, 8, 25)]
    [Arguments(2026, 8, 26)]
    [Arguments(2026, 8, 27)]
    [Arguments(2026, 8, 28)]
    [Arguments(2026, 8, 29)]
    [Arguments(2026, 8, 30)]
    public async Task NeverIncludesAWeekendDay(int year, int month, int day)
    {
        var result = AgendaDateRange.GetWorkdays(new DateOnly(year, month, day));

        await Assert.That(result.Any(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)).IsFalse();
    }

    [Test]
    [Arguments(2026, 8, 24)]
    [Arguments(2026, 8, 28)]
    [Arguments(2026, 8, 29)]
    [Arguments(2026, 8, 30)]
    public async Task IsStrictlyAscendingAndStartsOnOrAfterToday(int year, int month, int day)
    {
        var today = new DateOnly(year, month, day);

        var result = AgendaDateRange.GetWorkdays(today);

        await Assert.That(result).IsInOrder();
        await Assert.That(result[0]).IsGreaterThanOrEqualTo(today);
    }

    [Test]
    public async Task MatchesAnIndependentReferenceImplementationAcrossEveryStartingWeekday()
    {
        // Cross-checks against a differently-shaped implementation (anchor back to this week's
        // Monday, then filter forward) across 40 consecutive calendar days — enough to cover
        // every weekday as a starting point several times over.
        var start = new DateOnly(2026, 1, 1);

        for (var offset = 0; offset < 40; offset++)
        {
            var today = start.AddDays(offset);

            var result = AgendaDateRange.GetWorkdays(today);

            await Assert.That(result).IsEquivalentTo(ReferenceImplementation(today));
        }
    }

    private static List<DateOnly> ReferenceImplementation(DateOnly today)
    {
        var mondayThisWeek = today;
        while (mondayThisWeek.DayOfWeek != DayOfWeek.Monday)
        {
            mondayThisWeek = mondayThisWeek.AddDays(-1);
        }

        var days = new List<DateOnly>();
        for (var offset = 0; offset < 21; offset++)
        {
            var day = mondayThisWeek.AddDays(offset);
            if (day >= today && day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                days.Add(day);
            }
        }

        return days;
    }
}
