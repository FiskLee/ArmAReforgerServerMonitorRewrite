using Microsoft.EntityFrameworkCore;
using ArmaReforgerServerMonitor.Backend.Models;

namespace ArmaReforgerServerMonitor.Backend
{
    // EF Core database context using SQLite.
    public class DatabaseContext : DbContext
    {
        // Although EF Core will set this property, we initialize it with a non-null default.
        public DbSet<Player> Players { get; set; } = default!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=armamonitor.db");
        }
    }
}
