using Microsoft.EntityFrameworkCore;

namespace Workplace.Web.Data;

public class WorkplaceDbContext(DbContextOptions<WorkplaceDbContext> options) : DbContext(options)
{
    public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();
    public DbSet<WorkCalendarSnapshot> WorkCalendarSnapshots => Set<WorkCalendarSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConnectedAccount>()
            .HasIndex(c => new { c.Provider, c.ProviderAccountId })
            .IsUnique();
    }
}
