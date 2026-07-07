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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string connectionString = DbConnectionManager.Instance.ConnectionString;
                optionsBuilder.UseSqlite(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);   

            modelBuilder.Entity<Graph>(entity =>
            {
                entity.HasKey(g => g.GraphId);
                entity.Property(g => g.Name).IsRequired();

                entity.HasMany(g => g.DataPoints)
                    .WithOne(d => d.Graph)
                    .HasForeignKey(d => d.GraphId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DataPoint>(entity =>
            {
                entity.HasKey(d => d.DataPointId);

                entity.HasIndex(d => d.GraphId);
            });
        }
    }
}