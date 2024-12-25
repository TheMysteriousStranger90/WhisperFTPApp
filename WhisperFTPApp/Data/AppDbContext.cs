using Microsoft.EntityFrameworkCore;
using WhisperFTPApp.Models;

namespace WhisperFTPApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<FtpConnectionEntity> FtpConnections { get; set; }
    public DbSet<SettingsEntity> Settings { get; set; }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<FtpConnectionEntity>(entity =>
        {
            entity.ToTable("FtpConnections");
            entity.Property(e => e.Name).IsRequired(true);
            entity.Property(e => e.Address).IsRequired(true);
            entity.Property(e => e.Username).IsRequired(true);
            entity.Property(e => e.Password).IsRequired(true);
        });
        
        modelBuilder.Entity<SettingsEntity>(entity =>
        {
            entity.ToTable("Settings");
            entity.Property(e => e.BackgroundPathImage).IsRequired(true);
        });
    }
}