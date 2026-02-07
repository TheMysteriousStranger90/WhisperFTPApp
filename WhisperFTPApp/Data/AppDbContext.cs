using Microsoft.EntityFrameworkCore;
using WhisperFTPApp.Models;

namespace WhisperFTPApp.Data;

public sealed class AppDbContext : DbContext
{
    public DbSet<FtpConnectionEntity> FtpConnections { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        ConfigureFtpConnectionEntity(modelBuilder);
    }

    private static void ConfigureFtpConnectionEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FtpConnectionEntity>(entity =>
        {
            entity.ToTable("FtpConnections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Address).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Password).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.LastUsed).IsRequired();

            entity.HasIndex(e => e.Address);
        });
    }
}
