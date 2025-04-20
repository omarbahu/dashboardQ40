using dashboardQ40.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.DAL
{
    public class AppDbContext : DbContext
    {
        private readonly string _connectionString;

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // ✅ Solo mantenemos las tablas que realmente usamos
        public DbSet<DashboardTemplate> DashboardTemplates { get; set; }
        public DbSet<DashboardWidget> DashboardWidgets { get; set; }

        public AppDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DashboardTemplate>()
                .HasKey(t => t.TemplateID);

            modelBuilder.Entity<DashboardWidget>()
                .HasKey(w => w.WidgetID);

            modelBuilder.Entity<DashboardWidget>()
                .HasOne(w => w.Template)
                .WithMany(t => t.Widgets)
                .HasForeignKey(w => w.TemplateID)
                .OnDelete(DeleteBehavior.Cascade);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!string.IsNullOrEmpty(_connectionString))
            {
                optionsBuilder.UseSqlServer(_connectionString);
            }
        }
    }
}
