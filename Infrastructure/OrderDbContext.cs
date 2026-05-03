using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure
{
    public class OrderDbContext : DbContext
    {
        public DbSet<Order> Orders { get; set; }

        public OrderDbContext(DbContextOptions<OrderDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Order entity
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Amount)
                    .HasPrecision(18, 2);
                
                entity.Property(e => e.Status)
                    .HasMaxLength(50)
                    .HasDefaultValue("Created");
                
                entity.Property(e => e.Currency)
                    .HasMaxLength(10)
                    .HasDefaultValue("USD");
                
                entity.Property(e => e.Description)
                    .HasMaxLength(500);
                
                entity.Property(e => e.ErrorMessage)
                    .HasMaxLength(1000);
                
                entity.Property(e => e.ExternalOrderId)
                    .HasMaxLength(100);
                
                entity.Property(e => e.CustomerEmail)
                    .HasMaxLength(255);
                
                entity.Property(e => e.Metadata)
                    .HasColumnType("jsonb");

                // Indexes for performance
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.ExternalOrderId);
                entity.HasIndex(e => new { e.TenantId, e.Status });
            });

            // Apply snake case naming
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(property.Name.ToSnakeCase());
                }
                
                foreach (var key in entity.GetKeys())
                {
                    key.SetName(key.GetName()?.ToSnakeCase());
                }
                
                // Foreign key names are set automatically by EF Core
                
                foreach (var index in entity.GetIndexes())
                {
                    index.SetDatabaseName(index.GetDatabaseName()?.ToSnakeCase());
                }
            }
        }
    }

    public static class StringExtensions
    {
        public static string ToSnakeCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString())).ToLower();
        }
    }
}
