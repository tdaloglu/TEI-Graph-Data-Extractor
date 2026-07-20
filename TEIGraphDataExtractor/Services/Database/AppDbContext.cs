using Microsoft.EntityFrameworkCore;
using TEIGraphDataExtractor.Models;

namespace TEIGraphDataExtractor.Services.Database
{
    public class AppDbContext : DbContext
    {
        public DbSet<Graph> Graphs {get; set; } = null!;
        public DbSet<DataPoint> DataPoints {get; set; } = null!;

        public AppDbContext()
        {
            Database.EnsureCreated();
        }
    }
}