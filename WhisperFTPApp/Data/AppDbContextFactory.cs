using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WhisperFTPApp.Services;

namespace WhisperFTPApp.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var pathManager = new PathManagerService();
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var dbPath = pathManager.GetDatabasePath();

        optionsBuilder.UseSqlite($"Data Source={dbPath}",
            sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(30);
                sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });

#if DEBUG
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();
#endif

        return new AppDbContext(optionsBuilder.Options);
    }
}
