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
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FtpConnectionEntity>(entity =>
        {
            entity.ToTable("FtpConnections");
            entity.Property(e => e.Name).IsRequired(true);
            entity.Property(e => e.Address).IsRequired(true);
            entity.Property(e => e.Username).IsRequired(true);
            entity.Property(e => e.Password).IsRequired(true);

            entity.HasData(new FtpConnectionEntity
            {
                Id = 1,
                Name = "ftp://demo.wftpserver.com",
                Address = "ftp://demo.wftpserver.com",
                Username = "demo",
                Password = "demo",
                LastUsed = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<SettingsEntity>(entity =>
        {
            entity.ToTable("Settings");
            entity.Property(e => e.Id).IsRequired(true);
            entity.Property(e => e.BackgroundPathImage).IsRequired(true);

            entity.HasData(new SettingsEntity
            {
                Id = 1,
                BackgroundPathImage = "/Assets/Image (3).jpg",
            });
        });
    }
}
