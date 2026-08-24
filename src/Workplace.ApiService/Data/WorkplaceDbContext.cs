using Microsoft.EntityFrameworkCore;

namespace Workplace.ApiService.Data;

public class WorkplaceDbContext(DbContextOptions<WorkplaceDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConnectedAccount>()
            .HasIndex(c => new { c.UserId, c.Provider, c.ProviderAccountId })
            .IsUnique();
    }
}
