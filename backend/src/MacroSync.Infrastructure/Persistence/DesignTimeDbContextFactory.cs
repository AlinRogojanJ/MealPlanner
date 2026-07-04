using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MacroSync.Infrastructure.Persistence;

// Lets `dotnet ef migrations add` run without a live database.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MacroSyncDbContext>
{
    public MacroSyncDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MacroSyncDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MacroSync;Trusted_Connection=True;")
            .Options;
        return new MacroSyncDbContext(options);
    }
}
