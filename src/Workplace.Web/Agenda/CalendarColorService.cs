using Microsoft.EntityFrameworkCore;

using Workplace.Web.Data;

namespace Workplace.Web.Agenda;

public record CalendarSource(string Key, string DisplayLabel, string Color);

// Plain scoped service, same pattern as ConnectedAccountsService — no HTTP layer, no
// per-user scoping (this app is single-user by construction).
public class CalendarColorService(WorkplaceDbContext db)
{
    public const string WorkCalendarKey = "work-calendar";

    private static readonly string[] DefaultPalette =
    [
        "#4A90D9", "#D9704A", "#5AAE61", "#9B59B6", "#E0B93D", "#4ABDAC"
    ];

    public async Task<List<CalendarSource>> GetCalendarsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await db.ConnectedAccounts.ToListAsync(cancellationToken);
        var savedSettings = await db.CalendarColorSettings
            .ToDictionaryAsync(c => c.CalendarKey, cancellationToken);

        var sources = accounts
            .Select(a => (Key: AccountKey(a.Id), FallbackLabel: a.DisplayLabel))
            .Append((Key: WorkCalendarKey, FallbackLabel: "Work calendar (Outlook)"))
            .Select(x => new CalendarSource(
                x.Key,
                ResolveDisplayName(x.Key, x.FallbackLabel, savedSettings),
                ResolveColor(x.Key, savedSettings)))
            .ToList();

        return sources;
    }

    public async Task SetColorAsync(string calendarKey, string color, CancellationToken cancellationToken = default)
    {
        var setting = await GetOrCreateAsync(calendarKey, cancellationToken);
        setting.Color = color;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDisplayNameAsync(string calendarKey, string? displayName, CancellationToken cancellationToken = default)
    {
        var setting = await GetOrCreateAsync(calendarKey, cancellationToken);
        // Blank clears the override — falls back to the account's own label again.
        setting.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();

        await db.SaveChangesAsync(cancellationToken);
    }

    public static string AccountKey(Guid connectedAccountId) => $"account:{connectedAccountId}";

    private async Task<CalendarColorSetting> GetOrCreateAsync(string calendarKey, CancellationToken cancellationToken)
    {
        var existing = await db.CalendarColorSettings
            .SingleOrDefaultAsync(c => c.CalendarKey == calendarKey, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var created = new CalendarColorSetting { CalendarKey = calendarKey, Color = DefaultColorFor(calendarKey) };
        db.CalendarColorSettings.Add(created);

        return created;
    }

    private static string ResolveDisplayName(
        string calendarKey, string fallbackLabel, Dictionary<string, CalendarColorSetting> savedSettings) =>
        savedSettings.TryGetValue(calendarKey, out var setting) && !string.IsNullOrWhiteSpace(setting.DisplayName)
            ? setting.DisplayName!
            : fallbackLabel;

    private static string ResolveColor(string calendarKey, Dictionary<string, CalendarColorSetting> savedSettings) =>
        savedSettings.TryGetValue(calendarKey, out var setting) ? setting.Color : DefaultColorFor(calendarKey);

    // string.GetHashCode() is randomized per process in .NET, so it can't be used here — the
    // default color must stay the same across app restarts until the user overrides it.
    private static string DefaultColorFor(string calendarKey)
    {
        var hash = 2166136261u;
        foreach (var c in calendarKey)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return DefaultPalette[hash % (uint)DefaultPalette.Length];
    }
}
