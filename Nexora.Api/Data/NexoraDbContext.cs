using Microsoft.EntityFrameworkCore;
using Nexora.Api.Models;

namespace Nexora.Api.Data;

public class NexoraDbContext : DbContext
{
    public NexoraDbContext(DbContextOptions<NexoraDbContext> options)
        : base(options)
    {
    }

    public DbSet<Event> Events { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<AlertHistory> AlertHistories { get; set; }
    
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Alert>()
            .HasIndex(a => new { a.RecordId, a.EventType })
            .IsUnique()
            .HasFilter("\"Resolved\" = false");
    }

}