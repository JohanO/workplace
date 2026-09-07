using Microsoft.EntityFrameworkCore;

using Workplace.Web.Data;

namespace Workplace.Web.Places;

// Plain scoped service, same pattern as CalendarColorService — no HTTP layer, no
// per-user scoping (this app is single-user by construction).
public class PlacesService(WorkplaceDbContext db)
{
    public async Task<List<Place>> GetPlacesAsync(CancellationToken cancellationToken = default) =>
        await db.Places.OrderBy(p => p.Name).ToListAsync(cancellationToken);

    public async Task<Place> AddAsync(string name, string color, CancellationToken cancellationToken = default)
    {
        var place = new Place { Id = Guid.NewGuid(), Name = name.Trim(), Color = color };
        db.Places.Add(place);
        await db.SaveChangesAsync(cancellationToken);

        return place;
    }

    public async Task SetNameAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        var place = await GetRequiredAsync(id, cancellationToken);
        place.Name = name.Trim();

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetColorAsync(Guid id, string color, CancellationToken cancellationToken = default)
    {
        var place = await GetRequiredAsync(id, cancellationToken);
        place.Color = color;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var place = await db.Places.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (place is null)
        {
            return;
        }

        db.Places.Remove(place);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Place> GetRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Places.SingleAsync(p => p.Id == id, cancellationToken);
}
